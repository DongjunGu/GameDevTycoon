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

    // ── 생명주기 ─────────────────────────────────────────────────────────────
    void Awake()
    {
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

        _canvas   = GetComponentInParent<Canvas>();
        _eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                  ? null : _canvas.worldCamera;

        BuildVisual();
    }

    // ── 비주얼 빌드 ──────────────────────────────────────────────────────────
    void BuildVisual()
    {
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

        // 테두리 두께
        float b = Mathf.Max(3f, _cellGap - 1f);

        // ─ Phase 1: 검은 배경 (step×step, 오프셋 -b) ─────────────────────
        foreach (var cell in _shape)
        {
            float cx = (cell[1] - minC) * step;
            float cy = -((cell[0] - minR) * step);
            MakeCell("Border", cx - b, cy + b, _cellSize + b * 2f, _cellSize + b * 2f, Color.black);
        }

        // ─ Phase 2: 인접 셀 gap 채우기 (블록 내부 검은색 제거) ───────────
        var shapeSet = new HashSet<(int, int)>();
        foreach (var cell in _shape) shapeSet.Add((cell[0], cell[1]));

        foreach (var cell in _shape)
        {
            int r = cell[0], c = cell[1];
            float cx = (c - minC) * step;
            float cy = -((r - minR) * step);

            // 오른쪽 gap
            if (shapeSet.Contains((r, c + 1)))
                MakeCell("GapH", cx + _cellSize, cy, _cellGap, _cellSize, _color);
            // 아래 gap
            if (shapeSet.Contains((r + 1, c)))
                MakeCell("GapV", cx, cy - _cellSize, _cellSize, _cellGap, _color);
            // 코너 (4셀이 모두 있을 때만)
            if (shapeSet.Contains((r, c + 1)) && shapeSet.Contains((r + 1, c)) && shapeSet.Contains((r + 1, c + 1)))
                MakeCell("GapC", cx + _cellSize, cy - _cellSize, _cellGap, _cellGap, _color);
        }

        // ─ Phase 3: 색상 셀 ───────────────────────────────────────────────
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
        SetAlpha(0.88f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_isPlaced || !_isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, e.position,
                e.pressEventCamera, out var lp))
        {
            _rt.anchoredPosition = lp + new Vector2(0f, _rt.sizeDelta.y * 0.5f + 14f);
        }

        bool valid = _grid.TryGetSnapAnchor(e.position, _shape, out var anchor);
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

        if (_lastValid && _grid.TryPlaceBlock(_shape, _lastAnchor, _color))
        {
            _miniGame.OnBlockPlaced(this);
            PlaceVisualOnGrid(_lastAnchor);
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

    // ── 그리드에 비주얼 고정 (드래그 불가) ──────────────────────────────────
    void PlaceVisualOnGrid(Vector2Int anchor)
    {
        _isPlaced = true;
        _rt.SetParent(_grid.transform, false);

        _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot     = new Vector2(0f, 1f);

        _cellSize = _gridCellSize;
        _cellGap  = _gridCellGap;
        BuildVisual();
        SetAlpha(1f);
        _rt.SetAsLastSibling();

        int minR = int.MaxValue, minC = int.MaxValue;
        foreach (var cell in _shape)
        {
            if (cell[0] < minR) minR = cell[0];
            if (cell[1] < minC) minC = cell[1];
        }

        float step = _gridCellSize + _gridCellGap;
        _rt.anchoredPosition = new Vector2(
            (anchor.y + minC) * step,
            -((anchor.x + minR) * step)
        );
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
