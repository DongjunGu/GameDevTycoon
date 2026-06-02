using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

// 프로젝트 설정 — 허브식 단일 패널(d1.png).
// mainPanel 하나에서: 규모(소/중/대 토글 — 클릭해서 선택 표시), 플랫폼·장르(누르면 별도 패널), 개발금(규모 따라 자동), 개발 시작.
// 플랫폼/장르 상세 패널(genrePanel/platformPanel)은 추후 개편 — 여기서는 OnClickGenre/OnClickPlatform 으로 선택 결과만 받아 mainPanel 로 복귀.
public class ProjectSetupUI : MonoBehaviour
{
    public static ProjectSetupUI Instance { get; private set; }

    // ── 패널 ──────────────────────────────────
    [Header("Panels")]
    public GameObject mainPanel;       // 허브: 규모 토글 + 플랫폼/장르 버튼 + 개발금 + 개발시작
    [FormerlySerializedAs("genrePlatformPanel")]
    public GameObject genrePanel;      // 장르 선택 패널 (장르 선택하기 → 이 패널)
    public GameObject platformPanel;   // 플랫폼 선택 패널 (플랫폼 선택하기 → 이 패널)

    // ── 규모 토글 버튼 ────────────────────────
    [Header("Scale Toggle (Small / Medium / Large)")]
    public Button scaleButtonSmall;
    public Button scaleButtonMedium;
    public Button scaleButtonLarge;
    public Color scaleSelectedColor = new Color(0.93f, 0.93f, 0.99f); // 선택된 규모 강조색
    public Color scaleNormalColor   = Color.white;

    // ── 플랫폼 / 장르 바 (누르면 별도 패널) ────
    [Header("Platform / Genre Bar")]
    public Button platformButton;             // 누르면 platformPanel
    public Button genreButton;                // 누르면 genrePanel
    public TextMeshProUGUI platformValueText; // 선택된 플랫폼명 (미선택 시 unselectedText)
    public TextMeshProUGUI genreValueText;    // 선택된 장르명
    public string unselectedText = "선택하기";

    [Header("Platform Select Buttons (순서: Mobile/PC/Nintendo/Console)")]
    public Button[] platformButtons;          // PlatformPanel 안 플랫폼 선택 버튼 (인덱스 = ProjectPlatform enum)

    // ── 개발금 ────────────────────────────────
    [Header("Dev Cost")]
    public TextMeshProUGUI devCostText;       // "개발금: {N}G" — 규모 따라 자동 갱신

    // ── 개발시작 / 닫기 ───────────────────────
    [Header("Action Buttons")]
    public Button startButton;                // 개발 시작
    public Button closeButton;                // 닫기

    [Header("Back Buttons (장르/플랫폼 패널 → 메인 복귀)")]
    public Button genreBackButton;            // GenrePanel 뒤로가기
    public Button platformBackButton;         // PlatformPanel 뒤로가기

    // ── 선택 결과 (다른 시스템이 읽음) ────────
    public static ProjectScale SelectedScale { get; set; }
    public static ProjectGenre SelectedGenre { get; set; }
    public static ProjectPlatform SelectedPlatform { get; set; }
    public static int SelectedGenrePopularity { get; set; } = 1;
    public static int SelectedGenreFatigue { get; set; } = 0;

    // ── 내부 상태 ─────────────────────────────
    private ProjectData _projectData = new();
    private bool _genreChosen;
    private bool _platformChosen;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 버튼 리스너 코드 배선(인스펙터 배선 불필요). 규모 버튼은 mainPanel 인라인 토글.
        if (scaleButtonSmall  != null) scaleButtonSmall.onClick.AddListener(()  => OnClickScale(0));
        if (scaleButtonMedium != null) scaleButtonMedium.onClick.AddListener(() => OnClickScale(1));
        if (scaleButtonLarge  != null) scaleButtonLarge.onClick.AddListener(()  => OnClickScale(2));
        if (platformButton != null) platformButton.onClick.AddListener(OpenPlatformPanel);
        if (genreButton    != null) genreButton.onClick.AddListener(OpenGenrePanel);

        if (startButton != null) { startButton.onClick.RemoveAllListeners(); startButton.onClick.AddListener(OnClickStartDevelopment); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(OnClickClose); }

        if (genreBackButton    != null) { genreBackButton.onClick.RemoveAllListeners();    genreBackButton.onClick.AddListener(BackToMain); }
        if (platformBackButton != null) { platformBackButton.onClick.RemoveAllListeners(); platformBackButton.onClick.AddListener(BackToMain); }

        // 플랫폼 선택 버튼 — 장르 버튼처럼 클릭 시 해당 플랫폼 선택(OnClickPlatform). 인덱스 = enum 순서.
        if (platformButtons != null)
            for (int i = 0; i < platformButtons.Length; i++)
            {
                if (platformButtons[i] == null) continue;
                int idx = i;
                platformButtons[i].onClick.RemoveAllListeners();
                platformButtons[i].onClick.AddListener(() => OnClickPlatform(idx));
            }
    }

    // ══════════════════════════════════════════
    // 진입 / 메인 패널
    // ══════════════════════════════════════════

    // 프로젝트 시작 버튼(메뉴) → 메인 패널 오픈
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

        // 새 설정 초기화 — 규모는 기본 소규모(개발금 즉시 표시), 플랫폼·장르는 미선택.
        _projectData = new ProjectData { scale = ProjectScale.Small };
        _genreChosen = false;
        _platformChosen = false;

        UpdateGenreButtonLabels();
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // 메인 패널 표시값 일괄 갱신
    void RefreshMain()
    {
        RefreshScaleHighlight();
        UpdateDevCost();
        if (platformValueText != null) platformValueText.text = _platformChosen ? _projectData.PlatformToString() : unselectedText;
        if (genreValueText    != null) genreValueText.text    = _genreChosen    ? _projectData.GenreToString()    : unselectedText;
    }

    void RefreshScaleHighlight()
    {
        SetScaleBtnColor(scaleButtonSmall,  _projectData.scale == ProjectScale.Small);
        SetScaleBtnColor(scaleButtonMedium, _projectData.scale == ProjectScale.Medium);
        SetScaleBtnColor(scaleButtonLarge,  _projectData.scale == ProjectScale.Large);
    }

    void SetScaleBtnColor(Button b, bool selected)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = selected ? scaleSelectedColor : scaleNormalColor;
    }

    void UpdateDevCost()
    {
        if (devCostText != null) devCostText.text = $"개발금: {_projectData.Cost:N0}G";
    }

    // ── 규모 선택 (인라인 토글, 패널 전환 없음) ──
    public void OnClickScale(int scale)
    {
        _projectData.scale = (ProjectScale)scale;
        RefreshScaleHighlight();
        UpdateDevCost();
    }

    // ── 뒤로가기 → 메인(SummaryPanel) 복귀, 장르/플랫폼 패널은 꺼짐 ──
    public void BackToMain() => ShowOnly(mainPanel);

    // ── 플랫폼/장르 패널 열기 (각각 별도 패널) ──
    public void OpenPlatformPanel() => ShowOnly(platformPanel);
    public void OpenGenrePanel()
    {
        UpdateGenreButtonLabels();
        ShowOnly(genrePanel);
    }

    // ── 플랫폼 선택 (platformPanel 의 버튼 OnClick) → 메인 복귀 ──
    public void OnClickPlatform(int platform)
    {
        _projectData.platform = (ProjectPlatform)platform;
        _platformChosen = true;
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // ── 장르 선택 (genrePanel 의 버튼 OnClick) → 메인 복귀 ──
    public void OnClickGenre(string genre)
    {
        _projectData.genre = System.Enum.Parse<ProjectGenre>(genre);
        SelectedGenrePopularity = GenrePopularityManager.Instance != null
            ? GenrePopularityManager.Instance.GetPopularity(_projectData.genre) : 1;
        SelectedGenreFatigue = GenreFatigueManager.Instance != null
            ? GenreFatigueManager.Instance.GetFatigue(_projectData.genre) : 0;

        _genreChosen = true;
        RefreshMain();
        ShowOnly(mainPanel);
    }

    // ── 개발 시작 ─────────────────────────────
    public void OnClickStartDevelopment()
    {
        if (!_platformChosen || !_genreChosen)
        {
            AlertUI.Instance.Show("플랫폼과 장르를 선택해주세요.");
            return;
        }

        int cost = _projectData.Cost;
        if (!MoneyManager.Instance.CanAfford(cost))
        {
            AlertUI.Instance.Show($"개발금이 부족합니다.\n필요: {cost:N0}G / 보유: {MoneyManager.Instance.Gold:N0}G");
            return;
        }

        MoneyManager.Instance.SpendGold(cost, saveImmediately: false);

        SelectedScale = _projectData.scale;
        SelectedGenre = _projectData.genre;
        SelectedPlatform = _projectData.platform;

        if (DevelopmentManager.Instance.CurrentStage == ProjectStage.Sales)
            SalesUI.Instance.NotifyNewProjectStarted();

        Debug.Log($"개발 시작! {_projectData.ScaleToString()} / {_projectData.GenreToString()} / {_projectData.PlatformToString()} / 개발금: -{cost:N0}G");
        DevelopmentManager.Instance.StartDevelopment();
        InfoUI.Instance?.Show("개발 시작!");
        if (mainPanel != null) mainPanel.SetActive(false);
    }

    // ── 닫기 ──────────────────────────────────
    public void OnClickClose()
    {
        _projectData = new ProjectData();
        _genreChosen = false;
        _platformChosen = false;

        ShowOnly(null);
        GameTimeManager.Instance.StartTime();
    }

    // ── 패널 전환 ─────────────────────────────
    void ShowOnly(GameObject target)
    {
        if (mainPanel     != null) mainPanel.SetActive(target == mainPanel);
        if (genrePanel    != null) genrePanel.SetActive(target == genrePanel);
        if (platformPanel != null) platformPanel.SetActive(target == platformPanel);
    }

    // ══════════════════════════════════════════
    // 장르 패널 라벨 (인기도/피로도) — 기존 유지
    // ══════════════════════════════════════════
    [Header("Genre Icon Sprite Names")]
    public string popularSpriteName = "popular";
    public string fatigueSpriteName = "skull";

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
        // Sales 진행 중인 경우 해당 장르를 포함해 피로도 재계산
        if (CompletedProjectManager.Instance != null && GenreFatigueManager.Instance != null)
            GenreFatigueManager.Instance.RebuildFromHistory(CompletedProjectManager.Instance.completedProjects);

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
        int fatigue = GenreFatigueManager.Instance   != null ? GenreFatigueManager.Instance.GetFatigue(genre)      : 0;
        int popular = GenrePopularityManager.Instance != null ? GenrePopularityManager.Instance.GetPopularity(genre) : 1;

        if (nameLabel    != null) nameLabel.text    = genreName;
        if (popularLabel != null) popularLabel.text = RepeatSprite(popularSpriteName, popular);
        if (fatigueLabel != null) fatigueLabel.text = RepeatSprite(fatigueSpriteName, fatigue);
    }

    string RepeatSprite(string spriteName, int count)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
            sb.Append($"<sprite name=\"{spriteName}\">");
        return sb.ToString();
    }
}
