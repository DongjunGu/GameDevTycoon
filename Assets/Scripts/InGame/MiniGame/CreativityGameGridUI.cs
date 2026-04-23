using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class CreativityGameGridUI : MonoBehaviour
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
    private Dictionary<(int, int), Image> _cellImages = new();
    private List<GameObject>    _ghostObjs = new();

    private RectTransform _rt;
    private Canvas        _canvas;
    private Camera        _eventCam;
    private float         _totalW, _totalH;

    public float CellSize => _cellSize;
    public float CellGap  => _cellGap;
    public float CellStep => _cellSize + _cellGap;

    // ── 초기화 ───────────────────────────────────────────────────────────────
    void Awake()
    {
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                      ? null : _canvas.worldCamera;
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && CreativityGameData.Grids != null && CreativityGameData.Grids.Length > 0)
        {
            _rt     = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            BuildGrid(CreativityGameData.Grids[0]);
        }
#endif
    }

    public void BuildGrid(CreativityGameData.GridShape shape)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        _cellImages.Clear();
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
        var le = GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth  = _totalW;
            le.preferredHeight = _totalH;
            le.flexibleWidth   = -1;
            le.flexibleHeight  = -1;
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
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = CellAnchoredPos(r, c);

        if (!trackImage)
        {
            go.SetActive(false);
            return;
        }

        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        _cellImages[(r, c)] = img;
    }

    Vector2 CellAnchoredPos(int r, int c)
    {
        float step = CellStep;
        return new Vector2(c * step, -(r * step));
    }

    // ── 스냅 계산 ─────────────────────────────────────────────────────────────
    public bool TryGetSnapAnchor(Vector2 screenPos, int[][] shape, out Vector2Int anchor)
    {
        anchor = Vector2Int.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, screenPos, _eventCam, out var local))
            return false;

        float step = CellStep;

        // pivot에 무관하게 RT top-left 기준으로 계산
        // rect.xMin = pivot.x 반영 offset, rect.yMax = 상단 offset
        float relX = local.x - _rt.rect.xMin;
        float relY = _rt.rect.yMax - local.y;

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

            var go = new GameObject("Ghost");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.sizeDelta        = new Vector2(_cellSize, _cellSize);
            rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = CellAnchoredPos(r, c);
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
    public bool TryPlaceBlock(int[][] shape, Vector2Int anchor, Color color)
    {
        if (!IsPlacementValid(shape, anchor)) return false;
        foreach (var cell in shape)
        {
            int r = anchor.x + cell[0];
            int c = anchor.y + cell[1];
            _filled[r, c] = true;
            if (_cellImages.TryGetValue((r, c), out var img))
                img.color = color;
        }
        return true;
    }


    public int CountFilledCells()
    {
        int count = 0;
        if (_filled == null || _validCells == null) return 0;
        foreach (var (r, c) in _validCells)
            if (_filled[r, c]) count++;
        return count;
    }
}
