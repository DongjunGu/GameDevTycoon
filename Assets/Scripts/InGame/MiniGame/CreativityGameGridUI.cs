using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class CreativityGameGridUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const int MaxSize = 7;

    [Header("셀 크기 (고정)")]
    [SerializeField] float _cellSize = 68f;
    [SerializeField] float _cellGap  = 5f;

    [Header("색상")]
    [SerializeField] Color _emptyColor        = new Color(0.86f, 0.91f, 0.98f);
    [SerializeField] Color _inactiveCellColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] Color _ghostValidColor   = new Color(0.28f, 0.92f, 0.46f, 0.6f);
    [SerializeField] Color _ghostInvalidColor = new Color(0.95f, 0.28f, 0.28f, 0.45f);

    // ── 런타임 상태 ──────────────────────────────────────────────────────────
    private HashSet<(int, int)> _validCells;
    private bool[,]             _filled = new bool[MaxSize, MaxSize];
    private Dictionary<(int, int), Image>              _cellImages  = new();
    private Dictionary<(int, int), CreativityGameBlockUI> _cellToBlock = new();
    private List<GameObject> _ghostObjs = new();
    private CreativityGameBlockUI _liftedBlock;

    private RectTransform _rt;
    private Canvas        _canvas;
    private Camera        _eventCam;
    private float         _totalW, _totalH;

    public float CellSize => _cellSize;
    public float CellGap  => _cellGap;
    public float CellStep => _cellSize + _cellGap;

    // ── 초기화 ───────────────────────────────────────────────────────────────
    void Awake() => EnsureInit();

    void EnsureInit()
    {
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                      ? null : _canvas.worldCamera;
    }

    public void BuildGrid(CreativityGameData.GridShape shape)
    {
        // Awake가 아직 안 돈 경우(부모 비활성 등)에 대비한 lazy init
        if (_rt == null) EnsureInit();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        _cellImages.Clear();
        _cellToBlock.Clear();
        HideGhost();
        _filled = new bool[MaxSize, MaxSize];

        // 도형을 7×7 중앙에 배치하기 위한 오프셋
        int rowOffset = (MaxSize - shape.rows) / 2;
        int colOffset = (MaxSize - shape.cols) / 2;

        // validCells를 7×7 좌표계로 변환
        _validCells = new HashSet<(int, int)>();
        foreach (var (r, c) in shape.validCells)
            _validCells.Add((r + rowOffset, c + colOffset));

        // 항상 7×7 셀 전체 생성
        float step = CellStep;
        _totalW = MaxSize * step - _cellGap;
        _totalH = MaxSize * step - _cellGap;

        // Grid RT를 정확히 셀 영역 크기로 고정 (Layout 좌표계 일치)
        _rt.sizeDelta = new Vector2(_totalW, _totalH);

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color         = Color.white;
        bg.raycastTarget = true;
        var le = GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth  = _totalW;
            le.preferredHeight = _totalH;
            le.flexibleWidth   = -1;
            le.flexibleHeight  = -1;
        }

        // GridLayoutGroup 을 쓰는 경우 셀 크기/간격/중앙정렬을 코드 값과 일치시킴.
        // (없으면 CellAnchoredPos 로 수동 중앙 배치 — 두 방식 모두 같은 위치가 나오도록 맞춰둠)
        var glg = GetComponent<GridLayoutGroup>();
        if (glg != null)
        {
            glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = MaxSize;
            glg.cellSize        = new Vector2(_cellSize, _cellSize);
            glg.spacing         = new Vector2(_cellGap, _cellGap);
            glg.childAlignment  = TextAnchor.MiddleCenter;
            glg.padding         = new RectOffset(0, 0, 0, 0);
        }

        for (int r = 0; r < MaxSize; r++)
        for (int c = 0; c < MaxSize; c++)
        {
            bool isValid = _validCells.Contains((r, c));
            CreateCell(r, c, isValid ? _emptyColor : _inactiveCellColor, isValid);
        }
    }

    void CreateCell(int r, int c, Color color, bool trackImage)
    {
        var go = new GameObject($"Cell_{r}_{c}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.sizeDelta        = new Vector2(_cellSize, _cellSize);
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = CellAnchoredPos(r, c); // GridLayoutGroup 있으면 덮어씀, 없으면 이 값 사용

        // 무효 칸(구멍): 비활성화하지 않고 이미지 없는 빈 셀로 둔다.
        // → GridLayoutGroup 이 슬롯 자리를 유지해 모양이 뭉치지 않음 (비활성 자식은 GLG가 건너뜀).
        if (!trackImage)
            return;

        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        _cellImages[(r, c)] = img;
    }

    // 그리드 중앙(anchor 0.5,0.5) 기준 셀 중심 좌표.
    // 7×7 블록을 그리드 RT 크기와 무관하게 항상 중앙에 배치 → 중앙정렬.
    Vector2 CellAnchoredPos(int r, int c)
    {
        float step = CellStep;
        float x = -(_totalW - _cellSize) * 0.5f + c * step;
        float y =  (_totalH - _cellSize) * 0.5f - r * step;
        return new Vector2(x, y);
    }

    // ── 스냅 계산 ─────────────────────────────────────────────────────────────
    public bool TryGetSnapAnchor(Vector2 screenPos, int[][] shape, out Vector2Int anchor)
    {
        anchor = Vector2Int.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, screenPos, _eventCam, out var local))
            return false;

        float step = CellStep;

        // 셀 블록은 그리드 중앙에 배치되므로, 블록 좌상단(중앙 - 절반 크기) 기준으로 계산
        float relX = local.x - (_rt.rect.center.x - _totalW * 0.5f);
        float relY = (_rt.rect.center.y + _totalH * 0.5f) - local.y;

        float fracCol = relX / step;
        float fracRow = relY / step;

        int minR = int.MaxValue, maxR = int.MinValue;
        int minC = int.MaxValue, maxC = int.MinValue;
        foreach (var cell in shape)
        {
            if (cell[0] < minR) minR = cell[0];
            if (cell[0] > maxR) maxR = cell[0];
            if (cell[1] < minC) minC = cell[1];
            if (cell[1] > maxC) maxC = cell[1];
        }

        int anchorRow = Mathf.RoundToInt(fracRow - (minR + maxR) * 0.5f);
        int anchorCol = Mathf.RoundToInt(fracCol - (minC + maxC) * 0.5f);
        anchor = new Vector2Int(anchorRow, anchorCol);

        return IsPlacementValid(shape, anchor);
    }

    public bool IsPlacementValid(int[][] shape, Vector2Int anchor)
    {
        foreach (var cell in shape)
        {
            int r = anchor.x + cell[0];
            int c = anchor.y + cell[1];
            if (r < 0 || r >= MaxSize || c < 0 || c >= MaxSize) return false;
            if (!_validCells.Contains((r, c))) return false;
            if (_filled[r, c]) return false;
        }
        return true;
    }

    // ── 고스트 ───────────────────────────────────────────────────────────────
    public void ShowGhost(int[][] shape, Vector2Int anchor, bool valid)
    {
        HideGhost();
        Color col = valid ? _ghostValidColor : _ghostInvalidColor;

        foreach (var cell in shape)
        {
            int r = anchor.x + cell[0];
            int c = anchor.y + cell[1];
            if (r < 0 || r >= MaxSize || c < 0 || c >= MaxSize) continue;
            if (!_validCells.Contains((r, c))) continue;

            var go = new GameObject("Ghost");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.sizeDelta        = new Vector2(_cellSize, _cellSize);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = CellAnchoredPos(r, c);

            // GridLayoutGroup 이 ghost 를 셀로 취급해 밀어내지 않도록 레이아웃에서 제외
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            rt.SetAsLastSibling();

            var img = go.AddComponent<Image>();
            img.color = col;
            _ghostObjs.Add(go);
        }
    }

    public void HideGhost()
    {
        foreach (var go in _ghostObjs)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _ghostObjs.Clear();
    }

    // ── 블록 배치 ─────────────────────────────────────────────────────────────
    public bool TryPlaceBlock(int[][] shape, Vector2Int anchor, Color color, CreativityGameBlockUI block)
    {
        if (!IsPlacementValid(shape, anchor)) return false;
        foreach (var cell in shape)
        {
            int r = anchor.x + cell[0];
            int c = anchor.y + cell[1];
            _filled[r, c]    = true;
            _cellToBlock[(r, c)] = block;
            if (_cellImages.TryGetValue((r, c), out var img))
                img.color = color;
        }
        return true;
    }

    // ── 그리드에서 블록 들어올리기 ────────────────────────────────────────────
    bool TryLiftBlock(Vector2 screenPos, out CreativityGameBlockUI block)
    {
        block = null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, _eventCam, out var local))
            return false;

        float relX = local.x - (_rt.rect.center.x - _totalW * 0.5f);
        float relY = (_rt.rect.center.y + _totalH * 0.5f) - local.y;
        int col = Mathf.FloorToInt(relX / CellStep);
        int row = Mathf.FloorToInt(relY / CellStep);

        if (row < 0 || row >= MaxSize || col < 0 || col >= MaxSize) return false;
        if (!_cellToBlock.TryGetValue((row, col), out block)) return false;

        var toRemove = new List<(int, int)>();
        foreach (var kv in _cellToBlock)
            if (kv.Value == block) toRemove.Add(kv.Key);

        foreach (var (r, c) in toRemove)
        {
            _filled[r, c] = false;
            if (_cellImages.TryGetValue((r, c), out var img))
                img.color = _emptyColor;
            _cellToBlock.Remove((r, c));
        }
        return true;
    }

    // ── 그리드 드래그 (배치된 블록 들어올리기용) ─────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (!TryLiftBlock(e.pressPosition, out _liftedBlock)) return;
        _liftedBlock.LiftFromGrid(e);
    }

    public void OnDrag(PointerEventData e)
    {
        _liftedBlock?.OnDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_liftedBlock == null) return;
        _liftedBlock.OnEndDrag(e);
        _liftedBlock = null;
    }


    public void ResetPlacedBlocks()
    {
        var placed = new HashSet<CreativityGameBlockUI>(_cellToBlock.Values);
        foreach (var (r, c) in new List<(int, int)>(_cellToBlock.Keys))
        {
            _filled[r, c] = false;
            if (_cellImages.TryGetValue((r, c), out var img))
                img.color = _emptyColor;
        }
        _cellToBlock.Clear();
        foreach (var block in placed)
            block.ResetToSlot();
    }

    public int CountFilledCells()
    {
        int count = 0;
        if (_filled == null || _validCells == null) return 0;
        foreach (var (r, c) in _validCells)
            if (_filled[r, c]) count++;
        return count;
    }

    public int ValidCellCount => _validCells?.Count ?? 0;

    // 디버그/테스트: 유효 셀 전체를 강제 채움 (블록 인스턴스와 무관 → 리프트 불가)
    public void DebugFillAllCells(Color color)
    {
        if (_filled == null || _validCells == null) return;
        foreach (var (r, c) in _validCells)
        {
            _filled[r, c] = true;
            if (_cellImages.TryGetValue((r, c), out var img))
                img.color = color;
        }
    }
}
