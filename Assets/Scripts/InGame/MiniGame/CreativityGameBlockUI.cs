using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class CreativityGameBlockUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── 블록 데이터 ──────────────────────────────────────────────────────────
    private int[][] _shape;
    private Color   _color;

    private float _previewCellSize;
    private float _previewCellGap;
    private float _gridCellSize;
    private float _gridCellGap;
    private float _cellSize;
    private float _cellGap;

    // ── 참조 ─────────────────────────────────────────────────────────────────
    private CreativityGameGridUI _grid;
    private CreativityGameUI     _miniGame;
    private RectTransform _rt;
    private Canvas _canvas;
    private Camera _eventCam;

    // ── 드래그 상태 ──────────────────────────────────────────────────────────
    private Transform  _origParent;
    private int        _origSiblingIdx;
    private bool       _isDragging;
    private Vector2Int _lastAnchor;
    private bool       _lastValid;
    private bool       _ghostShown;
    private bool       _isPlaced;   // 배치 완료 → 드래그 비활성
    private Vector2    _dragOffset; // 포인터와 블록 센터 사이의 오프셋

    // ── 생명주기 ─────────────────────────────────────────────────────────────
    void Awake() => EnsureInit();

    // 비활성 hierarchy에 AddComponent된 직후엔 Awake가 미실행 상태라 _rt가 null일 수 있음
    void EnsureInit()
    {
        if (_rt != null) return;
        _rt = GetComponent<RectTransform>();
        if (!TryGetComponent<Image>(out _))
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = Color.clear;
        }
    }

    // ── 초기화 ───────────────────────────────────────────────────────────────
    public void Init(int[][] shape, Color color,
                     CreativityGameGridUI grid, CreativityGameUI miniGame,
                     float previewCellSize = 20f, float previewCellGap = 2f)
    {
        _shape           = shape;
        _color           = color;
        _grid            = grid;
        _miniGame        = miniGame;
        _previewCellSize = previewCellSize;
        _previewCellGap  = previewCellGap;
        _gridCellSize    = grid.CellSize;
        _gridCellGap     = grid.CellGap;
        _cellSize        = previewCellSize;
        _cellGap         = previewCellGap;

        // 비활성 hierarchy에서도 캔버스 찾도록 includeInactive=true
        _canvas   = GetComponentInParent<Canvas>(true);
        _eventCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                  ? _canvas.worldCamera : null;

        BuildVisual();
    }

    // ── 비주얼 빌드 ──────────────────────────────────────────────────────────
    void BuildVisual()
    {
        if (_rt == null) EnsureInit();

        foreach (Transform child in transform) Destroy(child.gameObject);

        int minR = int.MaxValue, maxR = int.MinValue;
        int minC = int.MaxValue, maxC = int.MinValue;
        foreach (var cell in _shape)
        {
            if (cell[0] < minR) minR = cell[0];
            if (cell[0] > maxR) maxR = cell[0];
            if (cell[1] < minC) minC = cell[1];
            if (cell[1] > maxC) maxC = cell[1];
        }

        float step   = _cellSize + _cellGap;
        float totalW = (maxC - minC + 1) * step - _cellGap;
        float totalH = (maxR - minR + 1) * step - _cellGap;
        _rt.sizeDelta = new Vector2(totalW, totalH);

        foreach (var cell in _shape)
        {
            float cx = (cell[1] - minC) * step;
            float cy = -((cell[0] - minR) * step);
            MakeCell("Cell", cx, cy, _cellSize, _cellSize, _color);
        }
    }

    void MakeCell(string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
    }

    // ── 드래그 핸들러 ────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (_isPlaced) return;
        _isDragging     = true;
        _ghostShown     = false;
        _origParent     = _rt.parent;
        _origSiblingIdx = _rt.GetSiblingIndex();

        _rt.SetParent(_canvas.transform, true);
        _rt.SetAsLastSibling();

        _cellSize = _gridCellSize;
        _cellGap  = _gridCellGap;
        BuildVisual();

        // 클릭 지점과 블록 센터 사이 오프셋 기록 (위치 점프 방지)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, e.pressPosition,
                e.pressEventCamera, out var pressLP))
            _dragOffset = pressLP - _rt.anchoredPosition;
        else
            _dragOffset = Vector2.zero;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_isPlaced || !_isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, e.position,
                e.pressEventCamera, out var lp))
        {
            _rt.anchoredPosition = lp - _dragOffset;
        }

        // 스냅 기준: 블록 센터 스크린 좌표
        Vector2 snapOrigin = _eventCam != null
            ? (Vector2)_eventCam.WorldToScreenPoint(_rt.position)
            : (Vector2)_rt.position;
        bool valid = _grid.TryGetSnapAnchor(snapOrigin, _shape, out var anchor);
        if (!_ghostShown || anchor != _lastAnchor || valid != _lastValid)
        {
            _lastAnchor = anchor;
            _lastValid  = valid;
            _ghostShown = true;
            _grid.ShowGhost(_shape, anchor, valid);
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_isPlaced) return;
        _isDragging = false;
        _ghostShown = false;
        _grid.HideGhost();

        if (_lastValid && _grid.TryPlaceBlock(_shape, _lastAnchor, _color, this))
        {
            _isPlaced = true;
            _miniGame.OnBlockPlaced(this);
            // 슬롯으로 복귀 후 비주얼 숨김 (재사용 가능하도록 Destroy 안 함)
            _rt.SetParent(_origParent, true);
            _rt.SetSiblingIndex(_origSiblingIdx);
            _rt.anchoredPosition = Vector2.zero;
            foreach (Transform child in transform) Destroy(child.gameObject);
        }
        else
        {
            _rt.SetParent(_origParent, true);
            _rt.SetSiblingIndex(_origSiblingIdx);
            _cellSize = _previewCellSize;
            _cellGap  = _previewCellGap;
            BuildVisual();
            _rt.anchoredPosition = Vector2.zero;
            SetAlpha(1f);
        }
    }

    // ── 슬롯으로 리셋 ───────────────────────────────────────────────────────
    public void ResetToSlot()
    {
        _isPlaced = false;
        _cellSize = _previewCellSize;
        _cellGap  = _previewCellGap;
        BuildVisual();
        _rt.anchoredPosition = Vector2.zero;
    }

    // ── 그리드에서 들어올리기 ────────────────────────────────────────────────
    public void LiftFromGrid(PointerEventData e)
    {
        _isPlaced       = false;
        _isDragging     = true;
        _ghostShown     = false;
        _lastValid      = false;
        _origParent     = _rt.parent;
        _origSiblingIdx = _rt.GetSiblingIndex();

        _cellSize = _gridCellSize;
        _cellGap  = _gridCellGap;
        BuildVisual();

        _rt.SetParent(_canvas.transform, true);
        _rt.SetAsLastSibling();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, e.position,
                e.pressEventCamera, out var lp))
            _rt.anchoredPosition = lp;
        _dragOffset = Vector2.zero;

        _miniGame.OnBlockLifted(this);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────
    void SetAlpha(float a)
    {
        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject == gameObject) continue;
            img.color = new Color(img.color.r, img.color.g, img.color.b, a);
        }
    }
}
