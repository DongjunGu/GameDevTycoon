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
    public static CreativityGameUI Instance { get; private set; }

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

    // ── 런타임 ───────────────────────────────────────────────────────────────
    private int _score;
    private readonly List<CreativityGameBlockUI> _activeBlocks = new();
    private readonly List<CreativityGameData.BlockShape> _earnedBlocks = new();
    private CreativityGameData.GridShape _fixedGrid;
    private System.Action _onClose;

    // ── 생명주기 ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _panel.SetActive(false);
            _confirmBtn.onClick.AddListener(OnClickConfirm);
        }
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

    public int GetFinalScore() => _score;

    // CreativityGameBlockUI.OnEndDrag 에서 호출
    public void OnBlockPlaced(CreativityGameBlockUI block)
    {
        _score = _gridUI.CountFilledCells();
        UpdateScore();
    }

    // CreativityGameBlockUI.LiftFromGrid 에서 호출
    public void OnBlockLifted(CreativityGameBlockUI block)
    {
        _score = _gridUI.CountFilledCells();
        UpdateScore();
    }

    void UpdateScore()
    {
        if (_scoreText != null)
            _scoreText.text = $"창의성 +{_score}";
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
            DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, 0f, score);

        _panel.SetActive(false);
        var cb = _onClose;
        _onClose = null;
        cb?.Invoke();
    }
}

