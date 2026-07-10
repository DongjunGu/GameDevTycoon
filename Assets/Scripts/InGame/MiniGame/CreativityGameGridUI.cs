using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
public class CreativityGameGridUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public const int MaxSize = 7;

    [Header("셀 크기 (고정)")]
    [SerializeField] float _cellSize = 68f;
    [SerializeField] float _cellGap  = 5f;

    [Header("셀 스프라이트")]
    [Tooltip("유효 칸(활성화되는 칸)의 Image에 적용할 스프라이트. 비워두면 기본(민무늬 사각형).")]
    [SerializeField] Sprite _cellSprite;

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
    private List<GameObject> _ghostObjs = new(); // 배치 미리보기용(초록/빨강) — 제거 애니메이션용 고스트와 별개.
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
        img.sprite        = _cellSprite;
        img.color         = color;
        // 셀 자체는 클릭/드래그 핸들러가 없지만 raycastTarget=true 로 두면, 셀을 터치했을 때
        // 이벤트가 부모(Grid)의 OnBeginDrag/OnPointerClick 으로 버블링되어 블록 리프트/제거가 동작한다.
        img.raycastTarget = true;
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

    // ── 좌표 변환 헬퍼 ───────────────────────────────────────────────────────
    // 점유 셀 목록의 기하학적 중심을 다른 RectTransform(보통 캔버스) 로컬 좌표로 변환.
    Vector2 GetCellsCanvasPosition(List<(int r, int c)> occupiedCells, RectTransform targetRt, Camera eventCam)
    {
        if (occupiedCells == null || occupiedCells.Count == 0) return Vector2.zero;

        int minR = int.MaxValue, maxR = int.MinValue, minC = int.MaxValue, maxC = int.MinValue;
        foreach (var (r, c) in occupiedCells)
        {
            if (r < minR) minR = r; if (r > maxR) maxR = r;
            if (c < minC) minC = c; if (c > maxC) maxC = c;
        }
        // CellAnchoredPos 는 그리드 "rect 중심" 기준 오프셋(셀들이 anchorMin/Max=0.5,0.5 로 배치되므로).
        Vector2 offsetFromCenter = (CellAnchoredPos(minR, minC) + CellAnchoredPos(maxR, maxC)) * 0.5f;

        // 그리드 rect 의 실제 월드 중심 — 코너 기반으로 구해야 pivot 과 무관하게 정확하다.
        // ⚠ Grid 의 pivot 은 (0,1) 좌상단이라, TransformPoint 를 그대로 쓰면 "중심" 이 아니라
        //   "pivot(좌상단)" 기준으로 해석돼 그리드 크기만큼 크게 어긋난다.
        var corners = new Vector3[4];
        _rt.GetWorldCorners(corners); // 0=좌하단, 2=우상단 (월드 공간, pivot 영향 없음)
        Vector3 gridWorldCenter = (corners[0] + corners[2]) * 0.5f;

        // 오프셋은 "방향"이므로 회전/스케일만 적용(TransformVector) — pivot/위치는 개입시키지 않는다.
        Vector3 worldOffset = _rt.TransformVector(new Vector3(offsetFromCenter.x, offsetFromCenter.y, 0f));
        Vector3 world = gridWorldCenter + worldOffset;

        return WorldToTargetLocal(world, targetRt, eventCam);
    }

    // rt 의 rect 중심을 다른 RectTransform(targetRt) 로컬 좌표로 변환 — pivot 과 무관하게 정확.
    static Vector2 GetRectCanvasPosition(RectTransform rt, RectTransform targetRt, Camera eventCam)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        return WorldToTargetLocal(worldCenter, targetRt, eventCam);
    }

    static Vector2 WorldToTargetLocal(Vector3 world, RectTransform targetRt, Camera eventCam)
    {
        if (targetRt == null) return world;
        Vector2 screenPoint = eventCam != null
            ? (Vector2)eventCam.WorldToScreenPoint(world)
            : (Vector2)world;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRt, screenPoint, eventCam, out var localPos);
        return localPos;
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

    // ── 배치 미리보기 고스트(초록/빨강) ─────────────────────────────────────────
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
    // cellSprites/cellRotations: 블록의 ResolvedCellSprites/ResolvedCellRotations — 경로(뱀) 방향에
    // 맞춰 이미 회전 계산이 끝난 값. 그리드 칸도 같은 스프라이트+회전을 그대로 적용해 모양을 유지한다.
    public bool TryPlaceBlock(int[][] shape, Vector2Int anchor, Color color, CreativityGameBlockUI block, Sprite[] cellSprites = null, float[] cellRotations = null)
    {
        if (!IsPlacementValid(shape, anchor)) return false;
        for (int i = 0; i < shape.Length; i++)
        {
            var cell = shape[i];
            int r = anchor.x + cell[0];
            int c = anchor.y + cell[1];
            _filled[r, c]    = true;
            _cellToBlock[(r, c)] = block;
            if (_cellImages.TryGetValue((r, c), out var img))
            {
                Sprite sp   = (cellSprites   != null && i < cellSprites.Length)   ? cellSprites[i]   : null;
                float  rotZ = (cellRotations != null && i < cellRotations.Length) ? cellRotations[i] : 0f;
                if (sp != null) { img.sprite = sp;   img.color = Color.white; }
                else            { img.sprite = null; img.color = color; }
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotZ);
            }
        }
        return true;
    }

    // ── 배치된 블록 찾기 / 점유 셀 조회 (읽기 전용 — 부수효과 없음) ──────────────────
    bool TryFindBlockAt(Vector2 screenPos, out CreativityGameBlockUI block)
    {
        block = null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, _eventCam, out var local))
            return false;

        float relX = local.x - (_rt.rect.center.x - _totalW * 0.5f);
        float relY = (_rt.rect.center.y + _totalH * 0.5f) - local.y;
        int col = Mathf.FloorToInt(relX / CellStep);
        int row = Mathf.FloorToInt(relY / CellStep);

        if (row < 0 || row >= MaxSize || col < 0 || col >= MaxSize) return false;
        return _cellToBlock.TryGetValue((row, col), out block);
    }

    List<(int, int)> FindOccupiedCells(CreativityGameBlockUI block)
    {
        var cells = new List<(int, int)>();
        foreach (var kv in _cellToBlock)
            if (kv.Value == block) cells.Add(kv.Key);
        return cells;
    }

    // 점유 셀들을 빈 칸으로 되돌린다(색상/스프라이트/회전/필드 전부 초기화 + 매핑 제거).
    void ClearCellsFor(List<(int, int)> cells)
    {
        foreach (var (r, c) in cells)
        {
            _filled[r, c] = false;
            if (_cellImages.TryGetValue((r, c), out var img))
            {
                img.color  = _emptyColor;
                img.sprite = _cellSprite;
                img.rectTransform.localEulerAngles = Vector3.zero;
            }
            _cellToBlock.Remove((r, c));
        }
    }

    // ── 그리드 드래그 (배치된 블록을 집어서 다른 자리로 재배치) ─────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (!TryFindBlockAt(e.pressPosition, out _liftedBlock)) return;
        // 실제로 드래그를 시작하는 시점에만 그리드 셀을 비운다 — 재배치든 실패든 이후는
        // CreativityGameBlockUI.OnEndDrag 가 (성공: 재배치 / 실패: 현재 위치에서 애니메이션 복귀) 처리.
        ClearCellsFor(FindOccupiedCells(_liftedBlock));
        _liftedBlock.LiftFromGrid(e);
    }

    public void OnDrag(PointerEventData e)
    {
        _liftedBlock?.OnDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_liftedBlock == null) return;
        var block = _liftedBlock;
        _liftedBlock = null;
        block.OnEndDrag(e);
    }

    // 움직임 없는 순수 탭 — 블록은 지금껏 한 번도 움직인 적 없이 슬롯에 조용히 있던 상태이므로,
    // 그리드에 실제로 그려져 있던 모습을 그대로 본뜬 고스트를 만들어 애니메이션시킨다.
    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return; // 드래그였으면 무시 (OnEndDrag 가 처리)
        if (TryFindBlockAt(e.position, out var block) && block != null)
            RemoveBlockWithAnimation(block, notifyScore: true);
    }

    public void ResetPlacedBlocks()
    {
        var blocks = new HashSet<CreativityGameBlockUI>(_cellToBlock.Values);
        // 점수 리셋은 CreativityGameUI.ResetBlocks() 가 일괄 처리하므로 블록별 알림은 생략.
        foreach (var block in blocks)
            RemoveBlockWithAnimation(block, notifyScore: false);
    }

    // 배치된 블록 하나를 그리드에서 제거하고, "그리드에 실제로 그려져 있던 그 자리"에서 슬롯까지
    // 스르륵 애니메이션으로 복귀시킨다. 원본 block 오브젝트는 애니메이션 도중 전혀 건드리지 않고
    // (드래그 상태/부모 전환 등 자신의 복잡한 상태와 얽히지 않도록) 즉시 쉬는 상태로 리셋해 두고,
    // 그 자리를 흉내낸 "고스트"만 캔버스 위에서 움직였다가 도착하면 사라진다.
    void RemoveBlockWithAnimation(CreativityGameBlockUI block, bool notifyScore)
    {
        if (block == null) return;
        var cells = FindOccupiedCells(block);
        if (cells.Count == 0) return;

        var canvasTf = (_canvas != null ? _canvas.transform : transform) as RectTransform;
        Vector2 startPos = GetCellsCanvasPosition(cells, canvasTf, _eventCam);

        var ghostRt = ExtractPlacedBlockAsGhost(cells, canvasTf); // 내부에서 셀 정리(clear)까지 완료
        if (notifyScore) block.NotifyLifted();
        block.SnapToRestingState(); // 원본은 즉시 쉬는 상태로 — 화면상으론 고스트가 대신 보인다

        if (ghostRt == null) return; // 방어적: 그릴 셀이 없었으면 애니메이션 없이 종료

        ghostRt.anchoredPosition = startPos;

        // 오버라이드 캔버스 — CreativityCanvas 가 다른 루트 캔버스와 sortingOrder 경쟁을 하므로,
        // ModalLayer 등과 동일한 패턴으로 확실히 위에 보이게 한다.
        var floatCanvas = ghostRt.gameObject.AddComponent<Canvas>();
        floatCanvas.overrideSorting = true;
        if (_canvas != null)
        {
            floatCanvas.sortingLayerID = _canvas.sortingLayerID;
            floatCanvas.sortingOrder   = _canvas.sortingOrder + 100;
        }
        else
        {
            floatCanvas.sortingOrder = 1000;
        }

        Vector2 endPos = startPos;
        var slotRt = block.SlotRectTransform;
        if (slotRt != null)
            endPos = GetRectCanvasPosition(slotRt, canvasTf, _eventCam);

        float dur      = Mathf.Max(0.01f, block.ReturnDuration);
        float endScale = block.GridToPreviewScaleRatio;

        ghostRt.DOAnchorPos(endPos, dur).SetEase(Ease.OutQuad).SetUpdate(true);
        ghostRt.DOScale(endScale, dur).SetEase(Ease.OutQuad).SetUpdate(true)
               .OnComplete(() =>
               {
                   if (ghostRt != null) Destroy(ghostRt.gameObject);
               });
    }

    // 점유 셀들의 현재 모습(색상/스프라이트/회전)을 그대로 복제한 고스트 오브젝트를 만들어 반환한다.
    // 반환 직전 그 점유 셀들은 함께 정리(clear)된다 — 스냅샷과 클리어가 한 번에 원자적으로 일어나야
    // "지금 그려진 것과 다른 걸 복제"하거나 "정리 타이밍이 어긋나 잠깐 이중으로 보이는" 문제가 없다.
    RectTransform ExtractPlacedBlockAsGhost(List<(int, int)> occupiedCells, Transform ghostParent)
    {
        if (occupiedCells == null || occupiedCells.Count == 0 || ghostParent == null) return null;

        int minR = int.MaxValue, maxR = int.MinValue, minC = int.MaxValue, maxC = int.MinValue;
        foreach (var (r, c) in occupiedCells)
        {
            if (r < minR) minR = r; if (r > maxR) maxR = r;
            if (c < minC) minC = c; if (c > maxC) maxC = c;
        }
        Vector2 shapeCenter = (CellAnchoredPos(minR, minC) + CellAnchoredPos(maxR, maxC)) * 0.5f;

        var ghostGO = new GameObject("BlockGhost", typeof(RectTransform));
        var ghostRT = (RectTransform)ghostGO.transform;
        ghostRT.SetParent(ghostParent, false);
        ghostRT.anchorMin = ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRT.pivot     = new Vector2(0.5f, 0.5f);

        foreach (var (r, c) in occupiedCells)
        {
            if (_cellImages.TryGetValue((r, c), out var srcImg))
            {
                var cellGO = new GameObject("Cell", typeof(RectTransform));
                var cellRT = (RectTransform)cellGO.transform;
                cellRT.SetParent(ghostRT, false);
                cellRT.sizeDelta        = new Vector2(_cellSize, _cellSize);
                cellRT.anchorMin        = cellRT.anchorMax = new Vector2(0.5f, 0.5f);
                cellRT.pivot            = new Vector2(0.5f, 0.5f);
                cellRT.anchoredPosition = CellAnchoredPos(r, c) - shapeCenter;
                cellRT.localEulerAngles = srcImg.rectTransform.localEulerAngles;

                var cellImg = cellGO.AddComponent<Image>();
                cellImg.sprite         = srcImg.sprite;
                cellImg.color          = srcImg.color;
                cellImg.raycastTarget  = false;
            }
        }

        ClearCellsFor(occupiedCells);
        return ghostRT;
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
