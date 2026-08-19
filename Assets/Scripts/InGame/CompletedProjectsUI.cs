using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompletedProjectsUI : MonoBehaviour
{
    public static CompletedProjectsUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject completedProjectPanel; // 리스트+디테일 전체를 담는 최상위 래퍼(ModalLayer 부착) — Open에서 켜고 Close에서 끔
    public GameObject listPanel;
    public GameObject detailPanel;

    [Header("List")]
    public Transform listContent;
    public GameObject projectListItemPrefab;

    [Header("Detail")]
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailScaleText;
    public TextMeshProUGUI detailGenreText;
    public TextMeshProUGUI detailPlatformText;
    public TextMeshProUGUI detailRevenueText;
    public TextMeshProUGUI detailCriticTotalText;   // 평점 (점수 박스 안의 큰 숫자, 라벨은 별도 정적 텍스트)
    public TextMeshProUGUI detailBestRankText;      // 최고 순위 (DPBestScoreText2) — 1위면 숨기고 rank1stBadge로 대체
    public GameObject      rank1stBadge;            // 최고 순위 1위 전용 뱃지 (1stRank) — 평소엔 비활성

    [Header("Detail — 세부 점수 (기획/개발/아트/창의성)")]
    public TextMeshProUGUI detailPlanningScoreText;
    public TextMeshProUGUI detailDevScoreText;
    public TextMeshProUGUI detailArtScoreText;
    public TextMeshProUGUI detailCreativityScoreText;

    [Header("Detail — 세부 점수 신기록 뱃지 (자기 자신 제외 다른 완료작들의 최고치보다 높으면 표시)")]
    public GameObject planningNewRecordText;
    public GameObject devNewRecordText;
    public GameObject artNewRecordText;
    public GameObject creativityNewRecordText;

    [Header("Detail — 규모/장르/플랫폼 아이콘 (현재 전용 에셋 없어 임시 플레이스홀더 사용, 추후 교체)")]
    public Image detailScaleIcon;
    public Image detailGenreIcon;
    public Image detailPlatformIcon;
    public Sprite[] scaleIcons;    // ProjectScale(Small,Medium,Large) 순서
    public Sprite[] genreIcons;    // ProjectGenre 순서
    public Sprite[] platformIcons; // ProjectPlatform 순서

    [Header("Detail — 차기작 (조건 충족 시에만 활성화)")]
    public Button nextProjectButton;
    [Tooltip("차기작 가능 사무실 최소 단계")]
    public int nextProjectMinStage = 3;
    [Tooltip("차기작 가능 최소 평론가 점수")]
    public int nextProjectMinCriticScore = 80;
    [Tooltip("출시 시점 기준 차기작 가능 기간(주). 1년=48주 기준 2년=96주")]
    public int nextProjectMaxWeeksSinceRelease = 96;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        listPanel.SetActive(false);
        detailPanel.SetActive(false);
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        gameObject.SetActive(true);
        if (completedProjectPanel != null) completedProjectPanel.SetActive(true);
        detailPanel.SetActive(false);
        ShowList();
    }

    void ShowList()
    {
        foreach (Transform child in listContent)
            Destroy(child.gameObject);

        var projects = CompletedProjectManager.Instance.completedProjects;

        if (projects.Count == 0)
        {
            var empty = Instantiate(projectListItemPrefab, listContent);
            empty.transform.Find("NameText").GetComponent<TextMeshProUGUI>().text = "완료된 프로젝트가 없습니다.";
            empty.transform.Find("RevenueText").GetComponent<TextMeshProUGUI>().text = "";
            empty.GetComponent<Button>().interactable = false;
        }
        else
        {
            // 최신순 정렬
            var sorted = new List<CompletedProjectData>(projects);

            sorted.Sort((a, b) =>
            {
                if (a.year != b.year) return b.year.CompareTo(a.year);
                if (a.month != b.month) return b.month.CompareTo(a.month);
                return b.week.CompareTo(a.week);
            });
            foreach (var project in sorted)
            {
                var item = Instantiate(projectListItemPrefab, listContent);
                string namePrefix = project.isSequel ? "[차기작] " : "";
                item.transform.Find("NameText").GetComponent<TextMeshProUGUI>().text = namePrefix + project.projectName;
                item.transform.Find("RevenueText").GetComponent<TextMeshProUGUI>().text = $"{project.totalRevenue:N0} G";

                var captured = project;
                item.GetComponent<Button>().onClick.AddListener(() => ShowDetail(captured));
            }
        }

        listPanel.SetActive(true);
    }

    void ShowDetail(CompletedProjectData data)
    {
        detailNameText.text = (data.isSequel ? "[차기작] " : "") + data.projectName;
        detailScaleText.text = ScaleToString((ProjectScale)data.scale);
        detailGenreText.text = GenreToString((ProjectGenre)data.genre);
        detailPlatformText.text = PlatformToString((ProjectPlatform)data.platform);
        detailRevenueText.text = $"{data.totalRevenue:N0} G";
        if (detailCriticTotalText != null)
            detailCriticTotalText.text = $"{data.criticTotalScore}점";

        if (detailPlanningScoreText   != null) detailPlanningScoreText.text   = $"{data.planning:N0}";
        if (detailDevScoreText        != null) detailDevScoreText.text        = $"{data.develop:N0}";
        if (detailArtScoreText        != null) detailArtScoreText.text        = $"{data.art:N0}";
        if (detailCreativityScoreText != null) detailCreativityScoreText.text = $"{data.creativity:N0}";

        // 신기록 뱃지 — 자기 자신을 뺀 다른 완료작들 중 최고치를 이 프로젝트 값이 넘었으면 표시.
        // (DevelopmentResultUI/SalesUI 의 "완료 시점 신기록" 판정과 동일한 방식 — 여기선 완료작 목록에
        // 이미 포함된 프로젝트를 조회하는 것이므로 data 자신만 비교 대상에서 제외한다.)
        if (CompletedProjectManager.Instance != null)
        {
            float bestPlanning = 0f, bestDevelop = 0f, bestArt = 0f, bestCreativity = 0f;
            foreach (var proj in CompletedProjectManager.Instance.completedProjects)
            {
                if (proj == data) continue;
                if (proj.planning   > bestPlanning)   bestPlanning   = proj.planning;
                if (proj.develop    > bestDevelop)    bestDevelop    = proj.develop;
                if (proj.art        > bestArt)        bestArt        = proj.art;
                if (proj.creativity > bestCreativity) bestCreativity = proj.creativity;
            }
            if (planningNewRecordText   != null) planningNewRecordText.SetActive(data.planning   > bestPlanning);
            if (devNewRecordText        != null) devNewRecordText.SetActive(data.develop    > bestDevelop);
            if (artNewRecordText        != null) artNewRecordText.SetActive(data.art        > bestArt);
            if (creativityNewRecordText != null) creativityNewRecordText.SetActive(data.creativity > bestCreativity);
        }

        SetIcon(detailScaleIcon,    scaleIcons,    data.scale);
        SetIcon(detailGenreIcon,    genreIcons,    data.genre);
        SetIcon(detailPlatformIcon, platformIcons, data.platform);

        UpdateNextProjectButton(data);

        // 최고 순위 1위면 전용 뱃지(rank1stBadge)로 대체 — 평소 텍스트(detailBestRankText)는 숨김.
        bool isFirstRank = data.bestRank == 1;
        if (rank1stBadge != null) rank1stBadge.SetActive(isFirstRank);
        if (detailBestRankText != null)
        {
            detailBestRankText.gameObject.SetActive(!isFirstRank);
            detailBestRankText.text = data.bestRank > 0 ? $"{data.bestRank}위" : "순위권 밖";
        }

        // completedProjectPanel(래퍼)과 listPanel(CompletedLeftPanel)은 건드리지 않고 계속 활성 상태 유지
        // — CompletedProjectPanel 이 HorizontalLayoutGroup 으로 리스트/디테일을 나란히 배치하는 구조라,
        // 리스트는 계속 보이게 두고 detailPanel 만 추가로 활성화한다.
        detailPanel.SetActive(true);
    }

    static void SetIcon(Image img, Sprite[] icons, int index)
    {
        if (img == null || icons == null || index < 0 || index >= icons.Length) return;
        if (icons[index] != null) img.sprite = icons[index];
    }

    // 차기작 조건: 사무실 3단계 이상 + 평론가 점수 80점 이상 + 출시 시점 기준 2년 이내. 다 만족해야 버튼 노출.
    // 리스너는 조건 충족 여부와 무관하게 항상 연결한다 — 조건 미충족일 땐 SetActive(false)로만 숨기므로,
    // 인스펙터 등으로 GameObject를 강제로 켜서 테스트할 때도 클릭이 실제로 반응해야 하기 때문.
    void UpdateNextProjectButton(CompletedProjectData data)
    {
        if (nextProjectButton == null) return;

        bool available = IsNextProjectAvailable(data);
        nextProjectButton.gameObject.SetActive(available);

        var captured = data;
        nextProjectButton.onClick.RemoveAllListeners();
        nextProjectButton.onClick.AddListener(() => OnClickNextProject(captured));
    }

    bool IsNextProjectAvailable(CompletedProjectData data)
    {
        bool stageOk = StageManager.Instance != null && StageManager.Instance.CurrentStage >= nextProjectMinStage;
        bool scoreOk = data.criticTotalScore >= nextProjectMinCriticScore;
        bool timeOk  = IsWithinReleaseWindow(data);
        return stageOk && scoreOk && timeOk;
    }

    // 게임 내부 시간 기준(1년=48주=12개월×4주) 경과 주수 — 출시 시점부터 지금까지 nextProjectMaxWeeksSinceRelease 이내인지.
    bool IsWithinReleaseWindow(CompletedProjectData data)
    {
        if (GameTimeManager.Instance == null) return false;
        int weeksElapsed = (GameTimeManager.Instance.Year  - data.year)  * 48
                          + (GameTimeManager.Instance.Month - data.month) * 4
                          + (GameTimeManager.Instance.Week  - data.week);
        return weeksElapsed >= 0 && weeksElapsed <= nextProjectMaxWeeksSinceRelease;
    }

    // 차기작 버튼 클릭 — 완료작 패널을 닫고 "게임개발"과 동일하게 ProjectSetupUI(SummaryPanel)로 진입,
    // 단 장르/플랫폼은 이 원작 값으로 고정한다(ProjectSetupUI.OnClickNextProject 내부 처리).
    // 조건 재검증은 일부러 안 함 — 인스펙터로 버튼을 강제 활성화해 조건 미충족 상태를 테스트할 수 있어야 함.
    void OnClickNextProject(CompletedProjectData baseProject)
    {
        OnClickClose();
        ProjectSetupUI.Instance.OnClickNextProject(baseProject);
    }

    public void OnClickBack()
    {
        detailPanel.SetActive(false);
        ShowList();
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        listPanel.SetActive(false);
        if (completedProjectPanel != null) completedProjectPanel.SetActive(false);
    }

    string ScaleToString(ProjectScale scale) => scale switch
    {
        ProjectScale.Small => "소형",
        ProjectScale.Medium => "중형",
        ProjectScale.Large => "대형",
        _ => ""
    };

    string GenreToString(ProjectGenre genre) => genre switch
    {
        ProjectGenre.RPG              => "RPG",
        ProjectGenre.FPS              => "FPS",
        ProjectGenre.Arcade           => "아케이드",
        ProjectGenre.HealingSimulation => "힐링시뮬레이션",
        ProjectGenre.Horror           => "공포",
        ProjectGenre.Idle             => "방치형",
        ProjectGenre.RTS              => "실시간전략",
        ProjectGenre.VisualNovel      => "미연시",
        ProjectGenre.Sports           => "스포츠",
        _ => ""
    };

    string PlatformToString(ProjectPlatform platform) => platform switch
    {
        ProjectPlatform.Mobile => "모바일",
        ProjectPlatform.PC => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console => "플레이스테이션",
        _ => ""
    };
}