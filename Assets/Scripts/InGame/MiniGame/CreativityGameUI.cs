using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═════════════════════════════════════════════════════════════════════════════
// 씬 세팅 가이드 (계층 구조 예시)
// ─────────────────────────────────────────────────────────────────────────────
//  Canvas
//  └─ CreativityGamePanel          ← 이 컴포넌트 + [_panel]
//     ├─ Overlay (Image, alpha 0.5 black)
//     └─ Card (Image white, 700×500)
//        ├─ TitleText (TMP)
//        ├─ BodyLayout (HorizontalLayoutGroup, spacing 20)
//        │  ├─ GridArea
//        │  │  ├─ GridNameText (TMP)   ← [_gridNameText]
//        │  │  └─ Grid (CreativityGameGridUI) ← [_gridUI]
//        │  └─ BlockTrayArea (VerticalLayoutGroup, spacing 16)
//        │     ├─ ScoreText (TMP)        ← [_scoreText]
//        │     └─ BlockTray              ← [_blockTray]  (VerticalLayoutGroup)
//        └─ ConfirmButton (Button)       ← [_confirmBtn]
//           └─ Label (TMP "확인")
// ─────────────────────────────────────────────────────────────────────────────
// BlockTray: VerticalLayoutGroup / spacing 12 / ChildAlignment MiddleCenter
//            ChildControlWidth OFF, ChildControlHeight OFF
// ═════════════════════════════════════════════════════════════════════════════
public class CreativityGameUI : MonoBehaviour
{
    // 패널 GameObject가 비활성 상태로 시작하면 Awake가 안 돌아 _instance가 null인 채로 남는다.
    // 게임플레이에서 Instance 호출 시 비활성 인스턴스도 검색해 가져와 EnsureInit 호출.
    private static CreativityGameUI _instance;
    public static CreativityGameUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var found = Resources.FindObjectsOfTypeAll<CreativityGameUI>();
                foreach (var inst in found)
                {
                    if (inst == null || inst.gameObject.scene.IsValid() == false) continue;
                    _instance = inst;
                    inst.EnsureInit();
                    break;
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("패널")]
    [SerializeField] GameObject _panel;

    [Header("그리드")]
    [SerializeField] CreativityGameGridUI _gridUI;

    [Header("블록 트레이 (오른쪽 세로 배치)")]
    [SerializeField] Transform _blockTray;   // VerticalLayoutGroup

    [Header("UI")]
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] TextMeshProUGUI _gridNameText;
    [SerializeField] Button          _confirmBtn;

    [Header("퍼펙트 보너스")]
    [Tooltip("그리드를 전부 채우면 활성화되는 텍스트 (씬에서 비활성 상태로 배치)")]
    [SerializeField] GameObject _perfectBonusText;

    [Header("아이템 (랜덤/전설 블록)")]
    [Tooltip("랜덤 블록 보유 개수 텍스트 (예: x3)")]
    [SerializeField] TextMeshProUGUI _blockRandomCountText;
    [Tooltip("랜덤 블록 사용하기 버튼")]
    [SerializeField] Button _blockRandomUseBtn;
    [Tooltip("전설의 블록 보유 개수 텍스트")]
    [SerializeField] TextMeshProUGUI _blockLegendaryCountText;
    [Tooltip("전설의 블록 사용하기 버튼")]
    [SerializeField] Button _blockLegendaryUseBtn;

    [Header("디버그")]
    [Tooltip("그리드 전부 채움 시뮬레이션 (퍼펙트 보너스 테스트용)")]
    [SerializeField] Button _debugFillBtn;

    [Header("창의성 레벨 (테크트리 미해금시 fallback)")]
    [SerializeField, Range(1, 3)] int _creativityLevel = 1;
    // 테크트리 해금 상태에 따라 1~3단계로 결정 (creativity_grid_1/2)
    // 테크트리가 없는 환경(에디터 직접 테스트 등)에서는 인스펙터 값 사용
    public int CreativityLevel
    {
        get
        {
            if (TechTreeManager.Instance == null) return _creativityLevel;
            if (TechTreeManager.Instance.IsUnlocked("creativity_grid_2")) return 3;
            if (TechTreeManager.Instance.IsUnlocked("creativity_grid_1")) return 2;
            return 1;
        }
        set => _creativityLevel = value;
    }

    // 한 칸당 가산 점수: 기본 5, 테크트리 해금에 따라 10/15
    public int BaseScorePerCell
    {
        get
        {
            if (TechTreeManager.Instance == null) return 5;
            if (TechTreeManager.Instance.IsUnlocked("creativity_score_2")) return 15;
            if (TechTreeManager.Instance.IsUnlocked("creativity_score_1")) return 10;
            return 5;
        }
    }

    // ── 런타임 ───────────────────────────────────────────────────────────────
    private int _score;
    private readonly List<CreativityGameBlockUI> _activeBlocks = new();
    private readonly List<CreativityGameData.BlockShape> _earnedBlocks = new();
    private CreativityGameData.GridShape _fixedGrid;
    private System.Action _onClose;

    // ── 생명주기 ─────────────────────────────────────────────────────────────
    private bool _initialized;

    void Awake()
    {
        if (Application.isPlaying) EnsureInit();
    }

    void EnsureInit()
    {
        if (_initialized) return;
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        if (_panel != null) _panel.SetActive(false);
        if (_confirmBtn != null) _confirmBtn.onClick.AddListener(OnClickConfirm);
        if (_debugFillBtn != null) _debugFillBtn.onClick.AddListener(OnClickDebugFill);
        if (_blockRandomUseBtn != null) _blockRandomUseBtn.onClick.AddListener(() => OnClickUseItem("blockRandom"));
        if (_blockLegendaryUseBtn != null) _blockLegendaryUseBtn.onClick.AddListener(() => OnClickUseItem("blockLegendary"));
        _initialized = true;
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void OpenPanel() => Open();

    public void Open(System.Action onClose = null)
    {
        _onClose = onClose;
        _score   = 0;
        _panel.SetActive(true);

        var gridShape = _fixedGrid ?? CreativityGameData.Grids[Random.Range(0, CreativityGameData.Grids.Length)];
        _gridUI.BuildGrid(gridShape);

        if (_gridNameText != null)
            _gridNameText.text = $"그리드: {gridShape.name}";

        UpdateScore();
        SpawnEarnedBlocks();
        RefreshItemControls();
    }

    // ── 아이템 (랜덤/전설 블록) ─────────────────────────────────────────────
    public void RefreshItemControls()
    {
        int rndCount = ItemManager.Instance != null ? ItemManager.Instance.GetCount("blockRandom")    : 0;
        int legCount = ItemManager.Instance != null ? ItemManager.Instance.GetCount("blockLegendary") : 0;

        if (_blockRandomCountText    != null) _blockRandomCountText.text    = $"x{rndCount}";
        if (_blockLegendaryCountText != null) _blockLegendaryCountText.text = $"x{legCount}";
        if (_blockRandomUseBtn       != null) _blockRandomUseBtn.interactable    = rndCount > 0;
        if (_blockLegendaryUseBtn    != null) _blockLegendaryUseBtn.interactable = legCount > 0;
    }

    void OnClickUseItem(string itemId)
    {
        if (ItemManager.Instance == null) return;
        // UseItemNoTarget 내부에서 GrantItemBlock 까지 호출되므로 트레이는 즉시 갱신됨
        ItemManager.Instance.UseItemNoTarget(itemId);
        RefreshItemControls();
    }

    // ItemManager.ApplyNoTargetEffect 에서 호출. 패널이 열려있으면 즉시 트레이에 1개 추가.
    public void GrantItemBlock(CreativityGameData.BlockShape def)
    {
        if (def == null) return;
        _earnedBlocks.Add(def);
        if (_panel != null && _panel.activeSelf)
            SpawnSingleBlock(def);
    }

    void SpawnSingleBlock(CreativityGameData.BlockShape def)
    {
        Canvas.ForceUpdateCanvases();
        int count = _activeBlocks.Count + 1;
        var (slotSz, previewCell) = CalcSlotAndPreviewCell(count);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _blockTray.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = new Vector2(20f, 20f);
        glg.childAlignment  = TextAnchor.MiddleCenter;

        var slotGO = new GameObject($"Slot_{_blockTray.childCount}");
        slotGO.AddComponent<RectTransform>().SetParent(_blockTray, false);
        slotGO.AddComponent<Image>().color = new Color(0.9f, 0.93f, 1f, 0.4f);

        var blockGO = new GameObject("Block");
        var blockRT = blockGO.AddComponent<RectTransform>();
        blockRT.SetParent(slotGO.transform, false);
        blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
        blockRT.pivot            = new Vector2(0.5f, 0.5f);
        blockRT.anchoredPosition = Vector2.zero;

        var block = blockGO.AddComponent<CreativityGameBlockUI>();
        block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f);
        _activeBlocks.Add(block);
    }

    void SpawnPreviewSlots()
    {
        for (int i = _blockTray.childCount - 1; i >= 0; i--)
            DestroyImmediate(_blockTray.GetChild(i).gameObject);

        Canvas.ForceUpdateCanvases();
        var (slotSz, _) = CalcSlotAndPreviewCell(10);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg != null) { glg.constraintCount = 5; glg.cellSize = new Vector2(slotSz, slotSz); glg.spacing = new Vector2(20, 20); glg.childAlignment = TextAnchor.MiddleCenter; }

        for (int i = 0; i < 10; i++)
        {
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.AddComponent<RectTransform>().SetParent(_blockTray, false);
            slotGO.AddComponent<Image>().color = new Color(0.9f, 0.93f, 1f, 0.4f);
        }
    }

    (float slotSz, float previewCell) CalcSlotAndPreviewCell(int count)
    {
        var trayRT = _blockTray.GetComponent<RectTransform>();
        float trayW = trayRT.rect.width;
        float trayH = trayRT.rect.height;

        int cols    = 5;
        int rows    = Mathf.CeilToInt((float)count / cols);
        float space = 20f;

        float byW = (trayW - space * (cols - 1)) / cols;
        float byH = trayH > 0f ? (trayH - space * (rows - 1)) / rows : byW;

        float slotSz     = Mathf.Max(Mathf.Min(byW, byH), 60f);
        float previewCell = Mathf.Floor(slotSz / 5f);
        return (slotSz, previewCell);
    }

    void SpawnBlocks(int count)
    {
        foreach (var b in _activeBlocks)
            if (b != null) Destroy(b.gameObject);
        _activeBlocks.Clear();

        foreach (Transform child in _blockTray) Destroy(child.gameObject);

        Canvas.ForceUpdateCanvases();
        var (slotSz, previewCell) = CalcSlotAndPreviewCell(count);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _blockTray.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = new Vector2(20f, 20f);
        glg.childAlignment  = TextAnchor.MiddleCenter;

        for (int i = 0; i < count; i++)
        {
            var def = CreativityGameData.Blocks[Random.Range(0, CreativityGameData.Blocks.Length)];

            var slotGO = new GameObject($"Slot_{i}");
            slotGO.AddComponent<RectTransform>().SetParent(_blockTray, false);
            slotGO.AddComponent<Image>().color = new Color(0.9f, 0.93f, 1f, 0.4f);

            var blockGO = new GameObject("Block");
            var blockRT = blockGO.AddComponent<RectTransform>();
            blockRT.SetParent(slotGO.transform, false);
            blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
            blockRT.pivot            = new Vector2(0.5f, 0.5f);
            blockRT.anchoredPosition = Vector2.zero;

            var block = blockGO.AddComponent<CreativityGameBlockUI>();
            block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f);
            _activeBlocks.Add(block);
        }
    }

    void SpawnEarnedBlocks()
    {
        foreach (var b in _activeBlocks)
            if (b != null) Destroy(b.gameObject);
        _activeBlocks.Clear();

        foreach (Transform child in _blockTray) Destroy(child.gameObject);

        int count = _earnedBlocks.Count;
        if (count == 0) return;

        Canvas.ForceUpdateCanvases();
        var (slotSz, previewCell) = CalcSlotAndPreviewCell(count);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _blockTray.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = new Vector2(20f, 20f);
        glg.childAlignment  = TextAnchor.MiddleCenter;

        for (int i = 0; i < count; i++)
        {
            var def = _earnedBlocks[i];

            var slotGO = new GameObject($"Slot_{i}");
            slotGO.AddComponent<RectTransform>().SetParent(_blockTray, false);
            slotGO.AddComponent<Image>().color = new Color(0.9f, 0.93f, 1f, 0.4f);

            var blockGO = new GameObject("Block");
            var blockRT = blockGO.AddComponent<RectTransform>();
            blockRT.SetParent(slotGO.transform, false);
            blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
            blockRT.pivot            = new Vector2(0.5f, 0.5f);
            blockRT.anchoredPosition = Vector2.zero;

            var block = blockGO.AddComponent<CreativityGameBlockUI>();
            block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f);
            _activeBlocks.Add(block);
        }
    }

    public void AddEarnedBlock(CreativityGameData.BlockShape block)
    {
        _earnedBlocks.Add(block);
    }

    public void ClearEarnedBlocks()
    {
        _earnedBlocks.Clear();
        _fixedGrid = null;
    }

    public void SetFixedGrid(CreativityGameData.GridShape grid)
    {
        _fixedGrid = grid;
    }

    public string GetFixedGridName() => _fixedGrid?.name ?? "";

    public void RestoreFixedGrid(string gridName)
    {
        _fixedGrid = System.Array.Find(CreativityGameData.Grids, g => g.name == gridName);
    }

    public string GetEarnedBlocksString()
    {
        return string.Join(",", _earnedBlocks.ConvertAll(b => b.name));
    }

    public void RestoreEarnedBlocks(string serialized)
    {
        _earnedBlocks.Clear();
        if (string.IsNullOrEmpty(serialized)) return;
        foreach (var name in serialized.Split(','))
        {
            var found = System.Array.Find(CreativityGameData.Blocks, b => b.name == name);
            if (found != null) _earnedBlocks.Add(found);
        }
    }

    // 그리드를 전부 채웠을 때 보너스 활성 — 현재 점수의 10% (소수점 버림)
    bool IsGridFullyFilled => _gridUI != null
                              && _gridUI.ValidCellCount > 0
                              && _gridUI.CountFilledCells() >= _gridUI.ValidCellCount;
    int GetBonusScore() => IsGridFullyFilled ? _score / 10 : 0;

    // 최종 적용 점수 = 기본 점수 + 보너스
    public int GetFinalScore() => _score + GetBonusScore();

    // CreativityGameBlockUI.OnEndDrag 에서 호출
    public void OnBlockPlaced(CreativityGameBlockUI block)
    {
        _score = _gridUI.CountFilledCells() * BaseScorePerCell;
        UpdateScore();
    }

    // CreativityGameBlockUI.LiftFromGrid 에서 호출
    public void OnBlockLifted(CreativityGameBlockUI block)
    {
        _score = _gridUI.CountFilledCells() * BaseScorePerCell;
        UpdateScore();
    }

    void UpdateScore()
    {
        int bonus = GetBonusScore();
        if (_scoreText != null)
        {
            _scoreText.text = bonus > 0
                ? $"창의성 +{_score} (+Bonus {bonus})"
                : $"창의성 +{_score}";
        }
        if (_perfectBonusText != null)
            _perfectBonusText.SetActive(bonus > 0);
    }

    // 디버그: 그리드 전부 강제 채움 → 퍼펙트 보너스 표시 테스트
    public void OnClickDebugFill()
    {
        if (_gridUI == null) return;
        _gridUI.DebugFillAllCells(new Color(0.7f, 0.7f, 0.7f));
        _score = _gridUI.CountFilledCells() * BaseScorePerCell;
        UpdateScore();
    }

    public void ResetBlocks()
    {
        _gridUI.ResetPlacedBlocks();
        _score = 0;
        UpdateScore();
    }

    // 확인 버튼 (항상 활성 — Inspector에서 Button.onClick 에 연결)
    public void OnClickConfirm()
    {
        float score = GetFinalScore();
        if (score > 0f)
            DevelopmentPanelUI.Instance.AddValuesInstant(0f, 0f, 0f, 0f, score);

        _panel.SetActive(false);
        var cb = _onClose;
        _onClose = null;
        cb?.Invoke();
    }
}

