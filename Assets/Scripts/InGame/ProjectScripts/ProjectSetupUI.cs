using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProjectSetupUI : MonoBehaviour
{
    public static ProjectSetupUI Instance { get; private set; }

    // ── 패널 ──────────────────────────────────
    [Header("Panels")]
    public GameObject startPanel;       // 프로젝트 시작 버튼
    public GameObject scalePanel;       // 규모 선택
    public GameObject genrePanel;       // 장르 선택
    public GameObject platformPanel;    // 플랫폼 선택
    public GameObject summaryPanel;     // 선택 요약
    public GameObject confirmPanel;     // 최종 확정

    // ── 요약 패널 버튼 ────────────────────────
    [Header("Summary")]
    public TextMeshProUGUI summaryScaleText;
    public TextMeshProUGUI summaryGenreText;
    public TextMeshProUGUI summaryPlatformText;
    public Button summaryScaleButton;
    public Button summaryGenreButton;
    public Button summaryPlatformButton;

    // ── 최종 확정 패널 ────────────────────────
    [Header("Confirm")]
    public TextMeshProUGUI confirmText;
    public static ProjectScale SelectedScale { get; set; }
    public static ProjectGenre SelectedGenre { get; set; }
    public static ProjectPlatform SelectedPlatform { get; set; }
    public static int SelectedGenrePopularity { get; set; } = 1;
    public static int SelectedGenreFatigue { get; set; } = 0;

    // ── 내부 상태 ─────────────────────────────
    private ProjectData _projectData = new();
    private enum Step { Scale, Genre, Platform }
    private Step _editStep; // 요약에서 재선택 시 돌아갈 곳
    private bool _isEditing = false;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ShowPanel(startPanel);
    }

    // ── 패널 전환 ─────────────────────────────
    void ShowPanel(GameObject target)
    {
        //startPanel.SetActive(false);
        scalePanel.SetActive(false);
        genrePanel.SetActive(false);
        platformPanel.SetActive(false);
        summaryPanel.SetActive(false);
        confirmPanel.SetActive(false);

        target.SetActive(true);
    }

    // ══════════════════════════════════════════
    // 버튼 이벤트
    // ══════════════════════════════════════════

    // 프로젝트 시작
    public void OnClickProjectStart()
    {
        var stage = DevelopmentManager.Instance.CurrentStage;
        if (DevelopmentManager.Instance.IsStarted &&
            (stage == ProjectStage.Developing || stage == ProjectStage.BugFixing || stage == ProjectStage.Marketing))
        {
            AlertUI.Instance.Show("진행중인 프로젝트가 있습니다.");
            return;
        }
        GameTimeManager.Instance.StopTime();
        UpdateScaleButtonLabels();
        UpdateGenreButtonLabels();
        ShowPanel(scalePanel);
    }

    // ── 규모 버튼 텍스트 ──────────────────────
    [Header("Scale Buttons")]
    public TextMeshProUGUI scaleLabelSmall;
    public TextMeshProUGUI scaleLabelMedium;
    public TextMeshProUGUI scaleLabelLarge;

    void UpdateScaleButtonLabels()
    {
        if (scaleLabelSmall  != null) scaleLabelSmall.text  = $"소규모 (4개월)\n추천 인원: 2명\n개발금: 3,000G";
        if (scaleLabelMedium != null) scaleLabelMedium.text = $"중형 (6개월)\n추천 인원: 4명\n개발금: 15,000G";
        if (scaleLabelLarge  != null) scaleLabelLarge.text  = $"대규모 (8개월)\n추천 인원: 5명\n개발금: 100,000G";
    }

    // ── 장르 아이콘 스프라이트 이름 ──────────────
    [Header("Genre Icon Sprite Names")]
    public string popularSpriteName = "popular";
    public string fatigueSpriteName   = "skull";

    // ── 장르 버튼 텍스트 ──────────────────────
    [Header("Genre Buttons - Name")]
    public TextMeshProUGUI genreLabelRPG;
    public TextMeshProUGUI genreLabelFPS;
    public TextMeshProUGUI genreLabelArcade;
    public TextMeshProUGUI genreLabelHealingSimulation;
    public TextMeshProUGUI genreLabelHorror;
    public TextMeshProUGUI genreLabelIdle;
    public TextMeshProUGUI genreLabelRTS;
    public TextMeshProUGUI genreLabelVisualNovel;
    public TextMeshProUGUI genreLabelSports;
    public TextMeshProUGUI genreLabelPuzzle;

    [Header("Genre Buttons - Popular")]
    public TextMeshProUGUI genrePopularRPG;
    public TextMeshProUGUI genrePopularFPS;
    public TextMeshProUGUI genrePopularArcade;
    public TextMeshProUGUI genrePopularHealingSimulation;
    public TextMeshProUGUI genrePopularHorror;
    public TextMeshProUGUI genrePopularIdle;
    public TextMeshProUGUI genrePopularRTS;
    public TextMeshProUGUI genrePopularVisualNovel;
    public TextMeshProUGUI genrePopularSports;
    public TextMeshProUGUI genrePopularPuzzle;

    [Header("Genre Buttons - Fatigue")]
    public TextMeshProUGUI genreFatigueRPG;
    public TextMeshProUGUI genreFatigueFPS;
    public TextMeshProUGUI genreFatigueArcade;
    public TextMeshProUGUI genreFatigueHealingSimulation;
    public TextMeshProUGUI genreFatigueHorror;
    public TextMeshProUGUI genreFatigueIdle;
    public TextMeshProUGUI genreFatigueRTS;
    public TextMeshProUGUI genreFatigueVisualNovel;
    public TextMeshProUGUI genreFatigueSports;
    public TextMeshProUGUI genreFatiguePuzzle;

    void UpdateGenreButtonLabels()
    {
        SetGenreLabel(genreLabelRPG,               genrePopularRPG,               genreFatigueRPG,               "RPG",           ProjectGenre.RPG);
        SetGenreLabel(genreLabelFPS,               genrePopularFPS,               genreFatigueFPS,               "FPS",           ProjectGenre.FPS);
        SetGenreLabel(genreLabelArcade,            genrePopularArcade,            genreFatigueArcade,            "아케이드",       ProjectGenre.Arcade);
        SetGenreLabel(genreLabelHealingSimulation, genrePopularHealingSimulation, genreFatigueHealingSimulation, "힐링시뮬레이션", ProjectGenre.HealingSimulation);
        SetGenreLabel(genreLabelHorror,            genrePopularHorror,            genreFatigueHorror,            "공포",           ProjectGenre.Horror);
        SetGenreLabel(genreLabelIdle,              genrePopularIdle,              genreFatigueIdle,              "방치형",         ProjectGenre.Idle);
        SetGenreLabel(genreLabelRTS,               genrePopularRTS,               genreFatigueRTS,               "실시간전략",     ProjectGenre.RTS);
        SetGenreLabel(genreLabelVisualNovel,       genrePopularVisualNovel,       genreFatigueVisualNovel,       "미연시",         ProjectGenre.VisualNovel);
        SetGenreLabel(genreLabelSports,            genrePopularSports,            genreFatigueSports,            "스포츠",         ProjectGenre.Sports);
        SetGenreLabel(genreLabelPuzzle,            genrePopularPuzzle,            genreFatiguePuzzle,            "퍼즐",           ProjectGenre.Puzzle);
    }

    void SetGenreLabel(TextMeshProUGUI nameLabel, TextMeshProUGUI popularLabel, TextMeshProUGUI fatigueLabel, string genreName, ProjectGenre genre)
    {
        int fatigue   = GenreFatigueManager.Instance   != null ? GenreFatigueManager.Instance.GetFatigue(genre)     : 0;
        int popular = GenrePopularityManager.Instance != null ? GenrePopularityManager.Instance.GetPopularity(genre) : 1;

        if (nameLabel      != null) nameLabel.text      = genreName;
        if (popularLabel != null) popularLabel.text = RepeatSprite(popularSpriteName, popular);
        if (fatigueLabel   != null) fatigueLabel.text   = RepeatSprite(fatigueSpriteName,   fatigue);
    }

    string RepeatSprite(string spriteName, int count)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
            sb.Append($"<sprite name=\"{spriteName}\">");
        return sb.ToString();
    }

    // ── 규모 선택 ─────────────────────────────
    public void OnClickScale(int scale)
    {
        _projectData.scale = (ProjectScale)scale;

        if (_isEditing)
        {
            _isEditing = false;
            ShowSummary();
            return;
        }

        ShowPanel(genrePanel);
    }

    // ── 장르 선택 ─────────────────────────────
    public void OnClickGenre(string genre)
    {
        _projectData.genre = System.Enum.Parse<ProjectGenre>(genre);
        SelectedGenrePopularity = GenrePopularityManager.Instance != null
            ? GenrePopularityManager.Instance.GetPopularity(_projectData.genre) : 1;
        SelectedGenreFatigue = GenreFatigueManager.Instance != null
            ? GenreFatigueManager.Instance.GetFatigue(_projectData.genre) : 0;

        if (_isEditing)
        {
            _isEditing = false;
            ShowSummary();
            return;
        }

        ShowPanel(platformPanel);
    }

    // ── 플랫폼 선택 ───────────────────────────
    public void OnClickPlatform(int platform)
    {
        _projectData.platform = (ProjectPlatform)platform;
        _isEditing = false;
        ShowSummary();
    }

    // ── 요약 표시 ─────────────────────────────
    void ShowSummary()
    {
        summaryScaleText.text = _projectData.ScaleInfoString();
        summaryGenreText.text = _projectData.GenreToString();
        summaryPlatformText.text = _projectData.PlatformToString();

        summaryScaleButton.onClick.RemoveAllListeners();
        summaryScaleButton.onClick.AddListener(() =>
        {
            _isEditing = true; // ← 재선택 진입 시 true
            ShowPanel(scalePanel);
        });

        summaryGenreButton.onClick.RemoveAllListeners();
        summaryGenreButton.onClick.AddListener(() =>
        {
            _isEditing = true;
            ShowPanel(genrePanel);
        });

        summaryPlatformButton.onClick.RemoveAllListeners();
        summaryPlatformButton.onClick.AddListener(() =>
        {
            _isEditing = true;
            ShowPanel(platformPanel);
        });

        ShowPanel(summaryPanel);
    }

    // ── 확인 버튼 ─────────────────────────────
    public void OnClickConfirm()
    {
        confirmText.text =
            $"규모: {_projectData.ScaleToString()}\n" +
            $"장르: {_projectData.GenreToString()}\n" +
            $"플랫폼: {_projectData.PlatformToString()}";

        ShowPanel(confirmPanel);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
    confirmPanel.GetComponent<RectTransform>());
    }
    public void OnClickBackToSummary()
    {
        ShowSummary();
    }

    public void OnClickClose()
    {
        _projectData = new ProjectData();
        _isEditing = false;

        scalePanel.SetActive(false);
        genrePanel.SetActive(false);
        platformPanel.SetActive(false);
        summaryPanel.SetActive(false);
        confirmPanel.SetActive(false);
        startPanel.SetActive(true);
        GameTimeManager.Instance.StartTime();
    }

    // ── 개발 시작 ─────────────────────────────
    public void OnClickStartDevelopment()
    {
        int cost = _projectData.Cost;
        if (!MoneyManager.Instance.CanAfford(cost))
        {
            AlertUI.Instance.Show($"개발금이 부족합니다.\n필요: {cost:N0}G / 보유: {MoneyManager.Instance.Gold:N0}G");
            return;
        }

        MoneyManager.Instance.SpendGold(cost);

        SelectedScale = _projectData.scale;
        SelectedGenre = _projectData.genre;
        SelectedPlatform = _projectData.platform;

        if (DevelopmentManager.Instance.CurrentStage == ProjectStage.Sales)
            SalesUI.Instance.NotifyNewProjectStarted();

        Debug.Log($"개발 시작! {_projectData.ScaleToString()} / {_projectData.GenreToString()} / {_projectData.PlatformToString()} / 개발금: -{cost:N0}G");
        DevelopmentManager.Instance.StartDevelopment();
        confirmPanel.SetActive(false);
    }
}