using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
public class CreativityGameBlockUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── 블록 데이터 ──────────────────────────────────────────────────────────
    private int[][] _shape;
    private Color   _color;
    private Sprite[] _cellSprites; // 셀별 스프라이트 (index = _shape 셀 순서). null 이면 단색 사용.

    // 스프라이트가 "기본으로" 향하고 있다고 가정하는 방향(도) — 오른쪽(0)이 기본 가정.
    // 실제 아트가 다른 방향을 기본으로 그려졌다면 이 값을 90 단위로 조정.
    const float SpriteDefaultFacingAngle = 0f;

    // _shape 를 "머리→꼬리" 경로 순서로 정렬했을 때, 각 인덱스에 배정될 실제 스프라이트/회전.
    // ComputeSnakePathOrder 가 경로(뱀) 형태가 아니라고 판단하면(분기/고리) null — 이 경우 원본
    // _cellSprites[i] 를 회전 없이(0도) 그대로 사용하는 기존 방식으로 fallback.
    private Sprite[] _resolvedCellSprite;
    private float[]  _resolvedCellRotZ;

    public Sprite[] ResolvedCellSprites   => _resolvedCellSprite ?? _cellSprites;
    public float[]  ResolvedCellRotations => _resolvedCellRotZ; // null 이면 전부 무회전(0도)

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

    // 슬롯(원래 부모) — 그리드가 제거 애니메이션의 도착 지점을 계산할 때 참조한다.
    public RectTransform SlotRectTransform => _origParent as RectTransform;
    // 슬롯 복귀 애니메이션 시간(초) — CreativityGameUI 인스펙터의 blockReturnDuration 값을 Init 에서 주입.
    public float ReturnDuration => _returnDuration;
    // 그리드 크기 비주얼 → 프리뷰(슬롯) 크기 비주얼로 줄어드는 비율. 고스트 축소 애니메이션에 사용.
    public float GridToPreviewScaleRatio => (_gridCellSize > 0f && _previewCellSize > 0f) ? _previewCellSize / _gridCellSize : 1f;

    // 캔버스에 떠 있는 동안(드래그/복귀 애니메이션)만 부착하는 오버라이드 캔버스.
    // CreativityCanvas 가 HUDCanvas 등 다른 루트 캔버스와 sortingOrder 를 놓고 경쟁하기 때문에,
    // SetAsLastSibling() 만으로는 다른 루트 캔버스에 가려질 수 있다(ModalLayer 등과 동일한 패턴).
    private Canvas _floatCanvas;
    private GraphicRaycaster _floatRaycaster;

    void EnsureFloatCanvas()
    {
        if (_floatCanvas != null) return;
        _floatCanvas = gameObject.AddComponent<Canvas>();
        _floatCanvas.overrideSorting = true;
        if (_canvas != null)
        {
            _floatCanvas.sortingLayerID = _canvas.sortingLayerID;
            _floatCanvas.sortingOrder   = _canvas.sortingOrder + 100;
        }
        else
        {
            _floatCanvas.sortingOrder = 1000;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            _floatRaycaster = gameObject.AddComponent<GraphicRaycaster>();
    }

    void ClearFloatCanvas()
    {
        if (_floatRaycaster != null) { Destroy(_floatRaycaster); _floatRaycaster = null; }
        if (_floatCanvas != null)    { Destroy(_floatCanvas);    _floatCanvas    = null; }
    }

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

    void OnDestroy()
    {
        _rt?.DOKill();
    }

    // ── 초기화 ───────────────────────────────────────────────────────────────
    public void Init(int[][] shape, Color color,
                     CreativityGameGridUI grid, CreativityGameUI miniGame,
                     float previewCellSize = 20f, float previewCellGap = 2f,
                     Sprite[] cellSprites = null, float returnDuration = 0.5f)
    {
        _shape           = shape;
        _color           = color;
        _cellSprites     = cellSprites;
        _grid            = grid;
        _miniGame        = miniGame;
        _previewCellSize = previewCellSize;
        _previewCellGap  = previewCellGap;
        _gridCellSize    = grid.CellSize;
        _gridCellGap     = grid.CellGap;
        _cellSize        = previewCellSize;
        _cellGap         = previewCellGap;
        _returnDuration  = returnDuration;

        // 비활성 hierarchy에서도 캔버스 찾도록 includeInactive=true
        _canvas   = GetComponentInParent<Canvas>(true);
        _eventCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                  ? _canvas.worldCamera : null;

        ResolveSnakeOrientation();
        BuildVisual();
    }

    // ── 뱀 모양 방향 계산 ────────────────────────────────────────────────────
    // _cellSprites 가 있을 때, 도형이 "한 줄로 이어진 경로"(뱀 모양)인지 판별해
    // 머리(0번)~꼬리(마지막)/몸통(그 외) 스프라이트를 경로 순서로 재배정하고,
    // 각 셀이 진행 방향을 바라보도록 z 회전 값을 계산한다.
    void ResolveSnakeOrientation()
    {
        _resolvedCellSprite = null;
        _resolvedCellRotZ   = null;
        if (_cellSprites == null || _cellSprites.Length == 0 || _shape == null) return;

        var pathOrder = ComputeSnakePathOrder(_shape);
        if (pathOrder == null) return; // 분기/고리 형태 — 회전 없이 기존 방식으로 fallback

        int n = _shape.Length;
        _resolvedCellSprite = new Sprite[n];
        _resolvedCellRotZ   = new float[n];

        for (int k = 0; k < n; k++)
        {
            int idx = pathOrder[k];

            // 스프라이트: 경로상 위치 기준 머리(0)/꼬리(마지막)/몸통(그 외)
            Sprite sp;
            if (k == 0)                sp = _cellSprites[0];
            else if (k == n - 1)       sp = _cellSprites[_cellSprites.Length - 1];
            else                       sp = _cellSprites[1 % _cellSprites.Length];
            _resolvedCellSprite[idx] = sp;

            // 회전: 진행 방향에 맞춰 z축 회전 (코너는 나가는 방향 기준 — 전용 코너 아트가 없어 근사치)
            int dr, dc;
            if (k == 0) // 머리 — 다음 칸을 향함
            {
                dr = _shape[pathOrder[1]][0] - _shape[idx][0];
                dc = _shape[pathOrder[1]][1] - _shape[idx][1];
            }
            else if (k == n - 1) // 꼬리 — 이전 칸에서 이어지는 방향 유지
            {
                dr = _shape[idx][0] - _shape[pathOrder[k - 1]][0];
                dc = _shape[idx][1] - _shape[pathOrder[k - 1]][1];
            }
            else // 몸통 — 나가는 방향 기준(직선이면 들어오는 방향과 동일)
            {
                dr = _shape[pathOrder[k + 1]][0] - _shape[idx][0];
                dc = _shape[pathOrder[k + 1]][1] - _shape[idx][1];
            }
            _resolvedCellRotZ[idx] = SpriteDefaultFacingAngle + DirToAngle(dr, dc);
        }
    }

    // 도형 셀들이 하나로 이어진 "경로"(뱀 모양)인지 판별하고, 경로 순서(머리→꼬리)로
    // 정렬된 _shape 인덱스 배열을 반환한다. 분기(3방향 이상 연결)나 고리(끝점 없음)면 null.
    static int[] ComputeSnakePathOrder(int[][] shape)
    {
        int n = shape.Length;
        if (n == 0) return null;
        if (n == 1) return new[] { 0 };

        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                int dr = Mathf.Abs(shape[i][0] - shape[j][0]);
                int dc = Mathf.Abs(shape[i][1] - shape[j][1]);
                if (dr + dc == 1) { adj[i].Add(j); adj[j].Add(i); }
            }

        int start = -1, endpointCount = 0;
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count > 2) return null; // 분기 있음 — 단순 경로 아님
            if (adj[i].Count == 1) { endpointCount++; if (start < 0) start = i; }
        }
        if (endpointCount != 2) return null; // 고리이거나 끊긴 도형

        var order   = new int[n];
        var visited = new bool[n];
        int cur = start, prev = -1;
        for (int k = 0; k < n; k++)
        {
            order[k]      = cur;
            visited[cur]  = true;
            int next = -1;
            foreach (var nb in adj[cur])
                if (nb != prev && !visited[nb]) { next = nb; break; }
            if (next < 0 && k < n - 1) return null; // 방어적 — 정상 경로라면 발생 불가
            prev = cur;
            cur  = next;
        }
        return order;
    }

    // (dr,dc) 방향을 z회전(도)으로 변환. 스크린 좌표 기준: 열 증가=오른쪽, 행 증가=아래.
    static float DirToAngle(int dr, int dc)
    {
        if (dc > 0) return 0f;
        if (dc < 0) return 180f;
        if (dr > 0) return -90f; // 아래
        if (dr < 0) return 90f;  // 위
        return 0f;
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

        for (int i = 0; i < _shape.Length; i++)
        {
            var cell = _shape[i];
            float cx = (cell[1] - minC) * step;
            float cy = -((cell[0] - minR) * step);

            Sprite sp; float rotZ;
            if (_resolvedCellSprite != null)
            {
                sp    = _resolvedCellSprite[i];
                rotZ  = _resolvedCellRotZ[i];
            }
            else
            {
                sp   = (_cellSprites != null && i < _cellSprites.Length) ? _cellSprites[i] : null;
                rotZ = 0f;
            }
            MakeCell("Cell", cx, cy, _cellSize, _cellSize, _color, sp, rotZ);
        }
    }

    void MakeCell(string name, float x, float y, float w, float h, Color color, Sprite sprite = null, float rotationZ = 0f)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);

        if (sprite == null)
        {
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            return;
        }

        // 회전은 셀 컨테이너(pivot 0,1=좌상단)가 아니라 중앙 피벗의 자식에 적용한다.
        // 좌상단 피벗을 직접 돌리면 그 모서리를 축으로 회전해 칸이 옆으로 밀려 보인다.
        var visGO = new GameObject("Visual");
        var visRT = visGO.AddComponent<RectTransform>();
        visRT.SetParent(rt, false);
        visRT.anchorMin        = visRT.anchorMax = new Vector2(0.5f, 0.5f);
        visRT.pivot            = new Vector2(0.5f, 0.5f);
        visRT.sizeDelta        = new Vector2(w, h);
        visRT.anchoredPosition = Vector2.zero;
        visRT.localEulerAngles = new Vector3(0f, 0f, rotationZ);

        var vimg = visGO.AddComponent<Image>();
        vimg.sprite        = sprite;
        vimg.color         = Color.white;
        vimg.raycastTarget = false;
    }

    // ── 드래그 핸들러 (트레이→그리드 배치 / 배치된 블록을 그리드 위에서 재배치) ────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (_isPlaced) return;
        _rt.DOKill();
        _rt.localScale = Vector3.one;

        _isDragging     = true;
        _ghostShown     = false;
        _origParent     = _rt.parent;
        _origSiblingIdx = _rt.GetSiblingIndex();

        _rt.SetParent(_canvas.transform, true);
        _rt.SetAsLastSibling();
        EnsureFloatCanvas();

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

    // 드래그 종료 — 유효한 자리면 그리드에 배치(즉시), 아니면 현재 위치(캔버스 위, 이미 떠 있는 그
    // 자리)에서 슬롯으로 스르륵 애니메이션 복귀한다. 트레이→그리드 최초 배치 실패와, 그리드 위
    // 재배치 실패(=사실상 제거) 양쪽 모두 이 경로를 공유한다 — 블록이 이미 실제로 떠 있는 상태라
    // 별도 고스트 없이 자기 자신을 그대로 애니메이션하면 된다.
    public void OnEndDrag(PointerEventData e)
    {
        if (_isPlaced) return;
        _isDragging = false;
        _ghostShown = false;
        _grid.HideGhost();

        if (_lastValid && _grid.TryPlaceBlock(_shape, _lastAnchor, _color, this, ResolvedCellSprites, ResolvedCellRotations))
        {
            _isPlaced = true;
            _miniGame.OnBlockPlaced(this);
            // 슬롯으로 복귀 후 비주얼 숨김 (재사용 가능하도록 Destroy 안 함)
            _rt.SetParent(_origParent, true);
            _rt.SetSiblingIndex(_origSiblingIdx);
            ClearFloatCanvas();
            _rt.anchoredPosition = Vector2.zero;
            foreach (Transform child in transform) Destroy(child.gameObject);
        }
        else
        {
            SetAlpha(1f);
            ResetToSlot(true); // 현재(캔버스 위) 위치에서 슬롯까지 스르륵 애니메이션
        }
    }

    // 슬롯 복귀 애니메이션 시간(초) — CreativityGameUI 인스펙터의 blockReturnDuration 값을 Init 에서 주입.
    private float _returnDuration = 0.5f;

    // ── 슬롯으로 복귀 ───────────────────────────────────────────────────────
    // animate=false: 즉시 슬롯의 "쉬는" 상태로(그리드 제거 애니메이션은 고스트가 대신 처리한 뒤 호출).
    // animate=true : 현재 위치(캔버스 위, 드래그로 이미 떠 있는 자리)에서 슬롯까지 스르륵 이동.
    public void ResetToSlot(bool animate)
    {
        _isPlaced = false;

        // 슬롯 밖(드래그 등으로 캔버스에 올라간 상태)이면 원래 슬롯으로 복귀시키되,
        // worldPositionStays=true 로 현재 화면 위치를 유지해 애니메이션 시작점으로 삼는다.
        if (_origParent != null && _rt.parent != _origParent)
        {
            _rt.SetParent(_origParent, true);
            _rt.SetSiblingIndex(_origSiblingIdx);
        }

        _rt.DOKill();

        if (!animate)
        {
            ClearFloatCanvas();
            _cellSize = _previewCellSize;
            _cellGap  = _previewCellGap;
            BuildVisual();
            _rt.anchoredPosition = Vector2.zero;
            _rt.localScale       = Vector3.one;
            return;
        }

        // 현재(그리드 크기) 비주얼을 슬롯 크기로 다시 그리고, 그 차이만큼 localScale 시작값을
        // 보정해서 "스르륵 줄어들며 제자리로" 보이게 한다.
        Vector2 fromPos   = _rt.anchoredPosition;
        float   fromScale = (_cellSize > 0f && _previewCellSize > 0f) ? _cellSize / _previewCellSize : 1f;

        _cellSize = _previewCellSize;
        _cellGap  = _previewCellGap;
        BuildVisual();

        _rt.anchoredPosition = fromPos;
        _rt.localScale       = new Vector3(fromScale, fromScale, 1f);

        float dur = Mathf.Max(0.01f, _returnDuration);
        _rt.DOAnchorPos(Vector2.zero, dur).SetEase(Ease.OutQuad).SetUpdate(true);
        // 애니메이션이 끝나 슬롯에 완전히 정착한 뒤에만 오버라이드 캔버스 제거(그 전에 지우면
        // 이동 도중 다른 루트 캔버스에 가려 안 보이는 문제가 되살아난다).
        _rt.DOScale(1f, dur).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(ClearFloatCanvas);
    }

    // 그리드가 "고스트" 애니메이션을 다 끝낸 뒤 호출 — 이 블록을 즉시 슬롯의 쉬는 상태로 되돌린다.
    public void SnapToRestingState() => ResetToSlot(false);

    // 점수/상태 갱신 콜백 — 그리드에서 제거될 때(탭 등) 그리드가 직접 호출한다.
    public void NotifyLifted() => _miniGame?.OnBlockLifted(this);

    // ── 그리드에서 들어올리기(배치된 블록을 드래그로 재배치할 때) ────────────────────────
    public void LiftFromGrid(PointerEventData e)
    {
        _rt.DOKill();
        _rt.localScale = Vector3.one;

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
        EnsureFloatCanvas();

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
