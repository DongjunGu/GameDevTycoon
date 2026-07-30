using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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
    [Tooltip("블록 뒤에 깔리는 슬롯 배경 프리팹 (Image 1개, 인스펙터에서 스프라이트/색상 커스텀 가능)")]
    [SerializeField] GameObject _blockSlotPrefab;
    [Tooltip("블록 모양별 셀 스프라이트 지정 에셋. 특정 모양에 스프라이트가 없으면 단색(BlockShape.color)으로 표시.")]
    [SerializeField] CreativityBlockSpriteConfig _blockSpriteConfig;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI _scoreText;
    [Tooltip("점수 카운트업 중 펀치스케일(1.1배) 대상. 비우면 ScoreText의 부모(ScorePanel) 자동 탐색")]
    [SerializeField] RectTransform   _scorePanel;
    [SerializeField] Button          _confirmBtn;

    [Header("테크트리 단계 표시")]
    [Tooltip("창의성 단계 (creat_box2/3)")]
    [SerializeField] TextMeshProUGUI _gridBagLevelText;
    [Tooltip("블록 가치 단계 (creat_value1/2/3)")]
    [SerializeField] TextMeshProUGUI _gridBlockLevelText;

    [Header("퍼펙트 보너스")]
    [Tooltip("그리드를 전부 채워 보너스를 받는 동안만 활성화되는 라벨 (평소엔 GameObject 비활성)")]
    [SerializeField] TextMeshProUGUI _perfectBonusText;

    [Header("아이템 (랜덤/전설 블록)")]
    [Tooltip("랜덤 블록 보유 개수 텍스트 (예: x3)")]
    [SerializeField] TextMeshProUGUI _blockRandomCountText;
    [Tooltip("랜덤 블록 사용하기 버튼")]
    [SerializeField] Button _blockRandomUseBtn;
    [Tooltip("전설의 블록 보유 개수 텍스트")]
    [SerializeField] TextMeshProUGUI _blockLegendaryCountText;
    [Tooltip("전설의 블록 사용하기 버튼")]
    [SerializeField] Button _blockLegendaryUseBtn;

    [Header("리셋 버튼")]
    [Tooltip("배치된 블록을 슬롯으로 되돌리는 리셋 버튼")]
    [SerializeField] Button _resetBtn;
    [Tooltip("리셋 버튼 자식 아이콘 — 클릭 시 z축으로 360도 회전")]
    [SerializeField] RectTransform _resetImage;
    [SerializeField] float _resetSpinDuration = 0.5f;
    [Tooltip("리셋 버튼 / 그리드에서 빼기로 블록이 슬롯으로 스르륵 복귀하는 시간(초)")]
    [SerializeField] float _blockReturnDuration = 0.5f;

    [Header("디버그")]
    [Tooltip("그리드 전부 채움 시뮬레이션 (퍼펙트 보너스 테스트용)")]
    [SerializeField] Button _debugFillBtn;

    [Header("창의성 레벨 (테크트리 미해금시 fallback)")]
    [SerializeField, Range(1, 3)] int _creativityLevel = 1;
    // 테크트리 해금 상태에 따라 1~3단계로 결정 (creat_box2/3)
    // 테크트리가 없는 환경(에디터 직접 테스트 등)에서는 인스펙터 값 사용
    public int CreativityLevel
    {
        get
        {
            if (TechTreeManager.Instance == null) return _creativityLevel;
            if (TechTreeManager.Instance.IsUnlocked("creat_box3")) return 3;
            if (TechTreeManager.Instance.IsUnlocked("creat_box2")) return 2;
            return 1;
        }
        set => _creativityLevel = value;
    }

    // 한 칸당 가산 점수: 기본 5, 테크트리 해금에 따라 10/15/20 (creat_value1/2/3)
    public int BaseScorePerCell
    {
        get
        {
            if (TechTreeManager.Instance == null) return 5;
            if (TechTreeManager.Instance.IsUnlocked("creat_value3")) return 20;
            if (TechTreeManager.Instance.IsUnlocked("creat_value2")) return 15;
            if (TechTreeManager.Instance.IsUnlocked("creat_value1")) return 10;
            return 5;
        }
    }

    // 블록 가치 단계: 기본 1, creat_value1/2/3 해금에 따라 2/3/4
    public int BlockValueLevel
    {
        get
        {
            if (TechTreeManager.Instance == null) return 1;
            if (TechTreeManager.Instance.IsUnlocked("creat_value3")) return 4;
            if (TechTreeManager.Instance.IsUnlocked("creat_value2")) return 3;
            if (TechTreeManager.Instance.IsUnlocked("creat_value1")) return 2;
            return 1;
        }
    }

    // ── 런타임 ───────────────────────────────────────────────────────────────
    private int _score;
    private int _grantedBonusAmount;   // 현재 지급 중인 퍼펙트 보너스 액수 (0=미지급) — 그리드가 다시 안 차면 이만큼 회수
    private float _displayScore;       // 화면에 현재 표시 중인 점수 (카운트업 lerp 대상)
    private Coroutine _scoreAnimCo;    // 점수 카운트업 코루틴
    private readonly List<CreativityGameBlockUI> _activeBlocks = new();
    private readonly List<CreativityGameData.BlockShape> _earnedBlocks = new();
    private CreativityGameData.GridShape _fixedGrid;
    private System.Action _onClose;

    // 튜토리얼 등 외부에서 그리드 강제배치/마커 API에 접근할 때 사용.
    public CreativityGameGridUI GridUI => _gridUI;
    // 블록이 그리드에 실제로 배치 완료될 때마다 발동 — 튜토리얼이 "그 블록이 놓였는지" 기다릴 때 사용.
    public event System.Action<CreativityGameBlockUI> OnAnyBlockPlaced;

    public CreativityGameBlockUI FindActiveBlockByShapeName(string name)
        => _activeBlocks.Find(b => b != null && b.ShapeName == name);

    // CreativityBlockSpriteConfigEditor 의 "게임에 즉시 적용" 버튼에서 호출 — 이미 스폰된 블록(트레이
    // 대기 중이든 그리드에 배치돼있든 전부)의 셀 스프라이트/회전을 config 최신 값 기준으로 다시 계산해서
    // 다시 그린다. 패널을 닫았다 열지 않아도 Play 모드에서 바로 확인할 수 있게 하기 위함.
    public void ReapplyBlockSprites()
    {
        foreach (var block in _activeBlocks)
        {
            if (block == null) continue;
            var def = System.Array.Find(CreativityGameData.Blocks, b => b.name == block.ShapeName);
            if (def == null) continue;
            block.RefreshVisual(CellSpritesFor(def), CellRotationsFor(def));
        }
    }

    // ── 생명주기 ─────────────────────────────────────────────────────────────
    private bool _initialized;
    // 인스펙터에 미리 세팅된 BlockTray GridLayoutGroup.cellSize — 있으면(양수) 자동계산보다 우선 사용.
    private Vector2 _fixedSlotSize = Vector2.zero;
    // 인스펙터에 미리 세팅된 BlockTray GridLayoutGroup.constraintCount — 있으면 우선 사용, 없으면 3열 기본.
    private int _fixedColumns = 0;
    int Columns => _fixedColumns > 0 ? _fixedColumns : 3;
    // 인스펙터에 미리 세팅된 BlockTray GridLayoutGroup.spacing — 있으면 우선 사용, 없으면 20,20 기본.
    private Vector2 _fixedSpacing = Vector2.zero;
    Vector2 Spacing => (_fixedSpacing.x > 0f || _fixedSpacing.y > 0f) ? _fixedSpacing : new Vector2(20f, 20f);

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
        if (_resetBtn != null) _resetBtn.onClick.AddListener(OnClickReset);
        if (_debugFillBtn != null) _debugFillBtn.onClick.AddListener(OnClickDebugFill);
        if (_blockRandomUseBtn != null) _blockRandomUseBtn.onClick.AddListener(() => OnClickUseItem("blockRandom"));
        if (_blockLegendaryUseBtn != null) _blockLegendaryUseBtn.onClick.AddListener(() => OnClickUseItem("blockLegendary"));
        if (_scorePanel == null && _scoreText != null) _scorePanel = _scoreText.transform.parent as RectTransform;

        // Spawn* 가 매번 덮어쓰기 전, 인스펙터에 미리 세팅해둔 GridLayoutGroup.cellSize/constraintCount를 캡처.
        var glg = _blockTray != null ? _blockTray.GetComponent<GridLayoutGroup>() : null;
        if (glg != null && glg.cellSize.x > 0f && glg.cellSize.y > 0f)
            _fixedSlotSize = glg.cellSize;
        if (glg != null && glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount && glg.constraintCount > 0)
            _fixedColumns = glg.constraintCount;
        if (glg != null && (glg.spacing.x > 0f || glg.spacing.y > 0f))
            _fixedSpacing = glg.spacing;

        _initialized = true;
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void OpenPanel() => Open();

    public void Open(System.Action onClose = null)
    {
        _onClose = onClose;
        _score   = 0;
        _grantedBonusAmount = 0;
        _panel.SetActive(true);
        GameTimeManager.Instance?.StopTime(); // 미니게임 동안 시간 정지 (OnClickConfirm 의 StartTime 과 1:1)
        ModalGate.I.Register(this);

        var gridShape = _fixedGrid ?? CreativityGameData.Grids[Random.Range(0, CreativityGameData.Grids.Length)];
        _gridUI.BuildGrid(gridShape);

        UpdateScore();
        SpawnEarnedBlocks();
        RefreshItemControls();
        RefreshLevelTexts();

        // 온보딩 튜토리얼 13-3~13-5 — 첫 프로젝트 한정, all-or-nothing 구간(5-1~6-2/10-1~10-3과 동일 방식).
        // 13-5(디버깅 시작 안내)까지 끝나야 완료로 치므로, 재접속 등으로 패널이 다시 열렸을 때 13-4까지는
        // 끝났어도 13-5가 아직이면 13-3부터 처음부터 다시 재생한다 — 이 시점엔 그리드/트레이가 항상 새로
        // 빌드되므로(배치 진행상황은 저장 안 됨) 다시 처음부터 유도해도 실제 게임 상태와 자연히 일치한다.
        if (!OnboardingState.Tutorial13_5Done
            && CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0
            && TutorialController.Instance != null)
        {
            StartCoroutine(TutorialController.Instance.PlayTutorial13_3());
        }
    }

    // 테크트리 창의성/블록 가치 단계 표시 갱신
    void RefreshLevelTexts()
    {
        if (_gridBagLevelText != null)
            _gridBagLevelText.text = $"<color=#663D45>창의성 가방</color> <color=#E63356>{CreativityLevel}단계</color>";
        if (_gridBlockLevelText != null)
            _gridBlockLevelText.text = $"<color=#663D45>블록 가치</color> <color=#E63356>{BlockValueLevel}단계</color>";
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

        BlockItemPanelUI.Instance?.Refresh();
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

    // 블록 뒤 슬롯 배경 — 프리팹 지정 시 그걸로, 없으면 기존 방식(기본 Image)으로 생성.
    GameObject CreateSlot(int index)
    {
        if (_blockSlotPrefab != null)
        {
            var prefabGO = Instantiate(_blockSlotPrefab, _blockTray);
            prefabGO.name = $"Slot_{index}";
            return prefabGO;
        }

        var slotGO = new GameObject($"Slot_{index}");
        slotGO.AddComponent<RectTransform>().SetParent(_blockTray, false);
        slotGO.AddComponent<Image>().color = new Color(0.9f, 0.93f, 1f, 0.4f);
        return slotGO;
    }

    // 이 블록에 적용할 셀별 스프라이트 — _blockSpriteConfig 에 블록 이름으로 등록된 스프라이트가 있으면
    // 그대로 사용, 없으면 null(BuildVisual 이 단색 BlockShape.color 로 그림).
    Sprite[] CellSpritesFor(CreativityGameData.BlockShape def)
        => _blockSpriteConfig != null ? _blockSpriteConfig.GetSprites(def.name) : null;

    // CellSpritesFor 로 고른 스프라이트 배열과 같은 인덱스로 대응하는 기본 회전(도). config에 없으면 null(전부 0도).
    float[] CellRotationsFor(CreativityGameData.BlockShape def)
        => _blockSpriteConfig != null ? _blockSpriteConfig.GetRotations(def.name) : null;

    // 슬롯 전체를 덮는 투명 드래그 영역을 만들어 대상 블록으로 이벤트를 위임한다.
    // → 1칸짜리처럼 작은 블록도 슬롯 아무데나 눌러 잡을 수 있다(모바일 대응).
    void AttachSlotDragArea(GameObject slotGO, CreativityGameBlockUI block)
    {
        var areaGO = new GameObject("DragArea");
        var rt = areaGO.AddComponent<RectTransform>();
        rt.SetParent(slotGO.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // 블록 비주얼 뒤에 깔림
        var img = areaGO.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        areaGO.AddComponent<CreativityBlockSlotDrag>().SetTarget(block);
    }

    void SpawnSingleBlock(CreativityGameData.BlockShape def)
    {
        Canvas.ForceUpdateCanvases();
        int count = _activeBlocks.Count + 1;
        var (slotSz, previewCell) = CalcSlotAndPreviewCell(count);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _blockTray.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = Spacing;
        glg.childAlignment  = TextAnchor.UpperLeft;

        var slotGO = CreateSlot(_blockTray.childCount);

        var blockGO = new GameObject("Block");
        var blockRT = blockGO.AddComponent<RectTransform>();
        blockRT.SetParent(slotGO.transform, false);
        blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
        blockRT.pivot            = new Vector2(0.5f, 0.5f);
        blockRT.anchoredPosition = Vector2.zero;

        var block = blockGO.AddComponent<CreativityGameBlockUI>();
        block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f, CellSpritesFor(def), _blockReturnDuration, CellRotationsFor(def));
        block.ShapeName = def.name;
        AttachSlotDragArea(slotGO, block);
        _activeBlocks.Add(block);
    }

    void SpawnPreviewSlots()
    {
        for (int i = _blockTray.childCount - 1; i >= 0; i--)
            DestroyImmediate(_blockTray.GetChild(i).gameObject);

        Canvas.ForceUpdateCanvases();
        var (slotSz, _) = CalcSlotAndPreviewCell(10);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg != null) { glg.constraintCount = Columns; glg.cellSize = new Vector2(slotSz, slotSz); glg.spacing = Spacing; glg.childAlignment = TextAnchor.UpperLeft; }

        for (int i = 0; i < 10; i++)
            CreateSlot(i);
    }

    (float slotSz, float previewCell) CalcSlotAndPreviewCell(int count)
    {
        float slotSz;
        if (_fixedSlotSize.x > 0f)
        {
            // 인스펙터에 미리 세팅된 GridLayoutGroup.cellSize 우선 — 자동계산 안 함.
            slotSz = _fixedSlotSize.x;
        }
        else
        {
            var trayRT = _blockTray.GetComponent<RectTransform>();
            float trayW = trayRT.rect.width;

            int cols    = Columns;
            float space = Spacing.x;

            // 세로 스크롤: 셀 크기는 너비(5열)에 맞춰 고정, 행이 늘면 ContentSizeFitter가 높이를 늘려 스크롤됨
            float byW = (trayW - space * (cols - 1)) / cols;
            slotSz = Mathf.Max(byW, 60f);
        }
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
        glg.constraintCount = Columns;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = Spacing;
        glg.childAlignment  = TextAnchor.UpperLeft;

        for (int i = 0; i < count; i++)
        {
            var def = CreativityGameData.Blocks[Random.Range(0, CreativityGameData.Blocks.Length)];

            var slotGO = CreateSlot(i);

            var blockGO = new GameObject("Block");
            var blockRT = blockGO.AddComponent<RectTransform>();
            blockRT.SetParent(slotGO.transform, false);
            blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
            blockRT.pivot            = new Vector2(0.5f, 0.5f);
            blockRT.anchoredPosition = Vector2.zero;

            var block = blockGO.AddComponent<CreativityGameBlockUI>();
            block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f, CellSpritesFor(def), _blockReturnDuration, CellRotationsFor(def));
            block.ShapeName = def.name;
            AttachSlotDragArea(slotGO, block);
            _activeBlocks.Add(block);
        }
    }

    // 튜토리얼(첫 프로젝트)에서는 획득 순서와 무관하게 항상 이 순서로 트레이에 배치 —
    // 가이드 배치 진행(Sq→T_U)과 화면상 배치가 시각적으로 맞아떨어지게 하기 위함.
    static readonly string[] TutorialBlockDisplayOrder = { "Sq", "V2", "V3", "T_U", "G_TR" };

    void SortEarnedBlocksForTutorial()
    {
        _earnedBlocks.Sort((a, b) =>
        {
            int ia = System.Array.IndexOf(TutorialBlockDisplayOrder, a.name);
            int ib = System.Array.IndexOf(TutorialBlockDisplayOrder, b.name);
            if (ia < 0) ia = int.MaxValue;
            if (ib < 0) ib = int.MaxValue;
            return ia.CompareTo(ib);
        });
    }

    void SpawnEarnedBlocks()
    {
        foreach (var b in _activeBlocks)
            if (b != null) Destroy(b.gameObject);
        _activeBlocks.Clear();

        foreach (Transform child in _blockTray) Destroy(child.gameObject);

        if (CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0)
            SortEarnedBlocksForTutorial();

        int count = _earnedBlocks.Count;
        if (count == 0) return;

        Canvas.ForceUpdateCanvases();
        var (slotSz, previewCell) = CalcSlotAndPreviewCell(count);

        var glg = _blockTray.GetComponent<GridLayoutGroup>();
        if (glg == null) glg = _blockTray.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.cellSize        = new Vector2(slotSz, slotSz);
        glg.spacing         = Spacing;
        glg.childAlignment  = TextAnchor.UpperLeft;

        for (int i = 0; i < count; i++)
        {
            var def = _earnedBlocks[i];

            var slotGO = CreateSlot(i);

            var blockGO = new GameObject("Block");
            var blockRT = blockGO.AddComponent<RectTransform>();
            blockRT.SetParent(slotGO.transform, false);
            blockRT.anchorMin        = blockRT.anchorMax = new Vector2(0.5f, 0.5f);
            blockRT.pivot            = new Vector2(0.5f, 0.5f);
            blockRT.anchoredPosition = Vector2.zero;

            var block = blockGO.AddComponent<CreativityGameBlockUI>();
            block.Init(def.cells, def.color, _gridUI, this, previewCell, 2f, CellSpritesFor(def), _blockReturnDuration, CellRotationsFor(def));
            block.ShapeName = def.name;
            AttachSlotDragArea(slotGO, block);
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

    // CreativityGameBlockUI.OnEndDrag 에서 호출
    public void OnBlockPlaced(CreativityGameBlockUI block)
    {
        _score = _gridUI.CountFilledCells() * BaseScorePerCell;
        UpdateScore();
        OnAnyBlockPlaced?.Invoke(block);
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
        if (_perfectBonusText != null)
        {
            _perfectBonusText.gameObject.SetActive(bonus > 0);
            _perfectBonusText.text = bonus > 0 ? $"+{bonus}" : "";
        }

        // 그리드가 꽉 차는 순간 즉시 지급, 그러다 블록을 빼서 다시 안 차면(bonus==0) 지급했던 만큼 회수.
        // 다시 채우면 또 지급 — "라운드당 1회 고정"이 아니라 확인 시점과 무관하게 꽉 찬 상태 여부를 그대로 따라감.
        if (bonus > 0 && _grantedBonusAmount == 0)
        {
            _grantedBonusAmount = bonus;
            DevelopmentPanelUI.Instance?.AddValuesInstant(0f, 0f, 0f, 0f, bonus);
        }
        else if (bonus == 0 && _grantedBonusAmount > 0)
        {
            DevelopmentPanelUI.Instance?.AddValuesInstant(0f, 0f, 0f, 0f, -_grantedBonusAmount);
            _grantedBonusAmount = 0;
        }

        AnimateScoreText();
    }

    // 점수가 오를 때만 0.3초 동안 카운트업(lerp), 내려가거나 리셋될 때는 즉시 반영.
    void AnimateScoreText()
    {
        if (_scoreText == null) return;

        if (_score > _displayScore && isActiveAndEnabled)
        {
            if (_scoreAnimCo != null) StopCoroutine(_scoreAnimCo);
            _scoreAnimCo = StartCoroutine(CountUpRoutine(_displayScore, _score));

            if (_scorePanel != null)
            {
                _scorePanel.DOKill();
                _scorePanel.localScale = Vector3.one;
                _scorePanel.DOScale(1.1f, 0.3f * 0.4f).SetEase(Ease.OutQuad).SetUpdate(true)
                            .OnComplete(() => _scorePanel.DOScale(1f, 0.3f * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
            }
        }
        else
        {
            if (_scoreAnimCo != null) { StopCoroutine(_scoreAnimCo); _scoreAnimCo = null; }
            SetScoreDisplay(_score);
        }
    }

    // 가속도가 붙는 카운트업 — ease-in(제곱)으로 처음엔 천천히, 끝날수록 빠르게 올라간다.
    IEnumerator CountUpRoutine(float from, int to)
    {
        const float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k *= k; // ease-in
            SetScoreDisplay(Mathf.RoundToInt(Mathf.Lerp(from, to, k)));
            yield return null;
        }
        SetScoreDisplay(to);
        _scoreAnimCo = null;
    }

    // 현재 표시할 점수 값으로 텍스트 갱신.
    void SetScoreDisplay(int shown)
    {
        _displayScore = shown;
        _scoreText.text = $"{shown}";
    }

    // 디버그: 그리드 전부 강제 채움 → 퍼펙트 보너스 표시 테스트
    public void OnClickDebugFill()
    {
        if (_gridUI == null) return;
        _gridUI.DebugFillAllCells(new Color(0.7f, 0.7f, 0.7f));
        _score = _gridUI.CountFilledCells() * BaseScorePerCell;
        UpdateScore();
    }

    // 리셋 버튼 클릭 — 블록 되돌리기 + 아이콘 z축 360도 회전 연출.
    public void OnClickReset()
    {
        ResetBlocks();
        if (_resetImage != null)
        {
            _resetImage.DOKill();
            _resetImage.localEulerAngles = Vector3.zero;
            _resetImage.DORotate(new Vector3(0f, 0f, -360f), _resetSpinDuration, RotateMode.FastBeyond360)
                       .SetEase(Ease.OutCubic)
                       .SetUpdate(true); // 미니게임 중 시간정지와 무관하게 회전
        }
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
        // 퍼펙트 보너스는 UpdateScore()에서 이미 즉시 지급/회수 처리됐으므로 여기선 기본 점수만 지급.
        if (_score > 0f)
            DevelopmentPanelUI.Instance.AddValuesInstant(0f, 0f, 0f, 0f, _score);

        _panel.SetActive(false);
        GameTimeManager.Instance?.StartTime(); // Open 의 StopTime 해소 (이후 콜백의 AlertUI 가 자체적으로 다시 정지)
        ModalGate.I.Unregister(this);
        var cb = _onClose;
        _onClose = null;
        cb?.Invoke();
    }
}

