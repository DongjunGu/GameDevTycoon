using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DevelopmentResultUI : MonoBehaviour
{
    public static DevelopmentResultUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject resultPanel;
    public GameObject editNamePanel;

    [Header("UI")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI bugText;
    public TextMeshProUGUI creativityText;
    [Header("Project Info")]
    public TextMeshProUGUI scaleResultText;
    public TextMeshProUGUI genreResultText;
    public TextMeshProUGUI platformResultText;
    [Header("Project Name")]
    public TextMeshProUGUI projectNameText;
    public TMP_InputField projectNameInput;

    private float _lastPopularityMultiplier;
    private float _lastMarketingMultiplier;
    public float LastPopularityMultiplier => _lastPopularityMultiplier;
    public float LastMarketingMultiplier => _lastMarketingMultiplier;
    public float LastPlanning => _lastPlanning;
    public float LastDevelop => _lastDevelop;
    public float LastArt => _lastArt;
    public float LastCreativity => _lastCreativity;
    public float LastBug => _lastBug;
    private float _lastPlanning;
    private float _lastDevelop;
    private float _lastArt;
    private float _lastCreativity;
    private float _lastBug;
    private string _lastProjectName = "프로젝트명";
    public string LastProjectName => _lastProjectName;
    string GetScaleString(ProjectScale scale) => scale switch
    {
        ProjectScale.Small => "소규모(1인개발)",
        ProjectScale.Medium => "중형(팀)",
        ProjectScale.Large => "대규모(AAA)",
        _ => ""
    };

    string GetGenreString(ProjectGenre genre) => genre switch
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

    string GetPlatformString(ProjectPlatform platform) => platform switch
    {
        ProjectPlatform.Mobile => "모바일",
        ProjectPlatform.PC => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console => "콘솔",
        _ => ""
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        resultPanel.SetActive(false);
        editNamePanel.SetActive(false);
    }

    public void Show(float planning, float develop, float art, float bug, float creativity)
    {
        _lastProjectName = "프로젝트명"; // ← 초기화
        projectNameText.text = _lastProjectName;
        _lastPlanning = planning;
        _lastDevelop = develop;
        _lastArt = art;
        _lastBug = bug;
        _lastCreativity = creativity;
        planningText.text = $"기획: {Mathf.RoundToInt(planning)}";
        developText.text = $"개발: {Mathf.RoundToInt(develop)}";
        artText.text = $"아트: {Mathf.RoundToInt(art)}";
        bugText.text = $"버그: {Mathf.RoundToInt(bug)}";
        creativityText.text = $"창의성: {Mathf.RoundToInt(creativity)}";

        scaleResultText.text = GetScaleString(ProjectSetupUI.SelectedScale);
        genreResultText.text = GetGenreString(ProjectSetupUI.SelectedGenre);
        platformResultText.text = GetPlatformString(ProjectSetupUI.SelectedPlatform);

        projectNameText.text = "프로젝트명";
        GameTimeManager.Instance?.StopTime();
        resultPanel.SetActive(true);
    }

    // 이름변경 버튼
    public void OnClickEditName()
    {
        // 현재 프로젝트명을 input에 기본값으로 설정
        projectNameInput.text = projectNameText.text;
        editNamePanel.SetActive(true);

        // 전체 선택된 것처럼 보이게
        projectNameInput.Select();
        projectNameInput.ActivateInputField();
        projectNameInput.selectionAnchorPosition = 0;
        projectNameInput.selectionFocusPosition = projectNameInput.text.Length;
    }

    // 변경 버튼
    public void OnClickConfirmName()
    {
        string value = projectNameInput.text.Trim();
        if (!string.IsNullOrEmpty(value))
        {
            projectNameText.text = value;
            _lastProjectName = value;
        }
        editNamePanel.SetActive(false);
    }
    // 취소 버튼
    public void OnClickCancelName()
    {
        editNamePanel.SetActive(false);
    }

    public void OnClickClose()
    {
        resultPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
    }
    public void OnClickRelease()
    {
        ProjectSaveManager.Instance.SetProjectName(_lastProjectName);
        resultPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();

        float p = DevelopmentPanelUI.Instance.GetPlanning();
                    float d = DevelopmentPanelUI.Instance.GetDevelop();
                    float a = DevelopmentPanelUI.Instance.GetArt();
                    float c = DevelopmentPanelUI.Instance.GetCreativity();
                    float bRem = DevelopmentPanelUI.Instance.GetBug();

                    float rawScore = CalcRawScore(p, d, a, c, ProjectSetupUI.SelectedPlatform);

                    // 케이스 감점 (중형, 대작) - criticScore/sAdj 공통 적용
                    float casePenalty = 0f;
                    if (ProjectSetupUI.SelectedScale == ProjectScale.Medium || ProjectSetupUI.SelectedScale == ProjectScale.Large)
                    {
                        float maxStat = Mathf.Max(p, Mathf.Max(d, a));
                        float minStat = Mathf.Min(p, Mathf.Min(d, a));
                        float statDiff = maxStat - minStat;

                        if (statDiff > maxStat * 0.4f)
                            casePenalty = UnityEngine.Random.Range(0.04f, 0.08f);
                        else if (statDiff > maxStat * 0.2f)
                            casePenalty = UnityEngine.Random.Range(0.02f, 0.04f);
                    }

                    float criticScore = (rawScore * (1f - DevelopmentManager.Instance.BugPenalty)
                                      + DevelopmentManager.Instance.BugEventBonus) * (1f - casePenalty);

        // ── 1. 평론가 패널 ──
    CriticReviewUI.Instance.Show(criticScore, () =>
    {
        // ── 2. 마케팅 ──
        AlertUI.Instance.Show("마케팅을 시작합니다.", () =>
        {
            MarketingUI.Instance.Show(() =>
            {
                float sAdj = (rawScore * (1f - DevelopmentManager.Instance.BugPenalty)
                           + DevelopmentManager.Instance.BugEventBonus) * (1f - casePenalty);

                sAdj = Mathf.Max(0f, sAdj);

                // float finalScore = CalcFinalScore(sAdj); // 로그 압축 비활성화
                float finalScore = sAdj * CalcPopularityMultiplier() * CalcFatigueMultiplier();
                float quality    = CalcQualityScore(finalScore);

                Debug.Log($"원천: {rawScore:F1} / 케이스감점: {casePenalty * 100f:F1}% / S_adj: {sAdj:F1} / 인지도배율: {CalcPopularityMultiplier():F2} / 피로도배율: {CalcFatigueMultiplier():F2} / 최종: {finalScore:F1} / 품질: {quality:F1}");

                ProjectSaveManager.Instance.SetQualityScore(quality, ProjectSetupUI.SelectedScale);
                DevelopmentManager.Instance.CurrentStage = ProjectStage.Marketing;
                MoneyManager.Instance.SaveMoney();
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();

                AlertUI.Instance.Show("판매 시작!", () =>
                {
                    SalesUI.Instance.Show(quality, ProjectSetupUI.SelectedScale);
                });
            });
        });
    });
    }
    float CalcRawScore(float p, float d, float a, float c, ProjectPlatform platform)
    {
        // n = 1 (기본 배율, 나중에 규모/장르에 따라 조정 가능)
        float n = 1f;

        switch (platform)
        {
            case ProjectPlatform.Mobile:
                return (1.5f * n * p) + (n * d) + (n * a) + (n * c);

            case ProjectPlatform.PC:
                return (n * p) + (1.5f * n * d) + (n * a) + (n * c);

            case ProjectPlatform.Nintendo:
                return (n * p) + (n * d) + (1.5f * n * a) + (n * c);

            case ProjectPlatform.Console:
                return (5f * Mathf.Min(p, Mathf.Min(d, a))) + (n * c);

            default:
                return 0f;
        }
    }

    float CalcFinalScore(float sAdj)
    {
        double maxExpected = 5000.0;
        double final = 100.0 * System.Math.Log(sAdj + 1) / System.Math.Log(maxExpected + 1);
        final = System.Math.Max(0, System.Math.Min(100, final));
        return (float)final;
    }
    float CalcQualityScore(float finalScore)
    {
        _lastPopularityMultiplier = CalcPopularityMultiplier();
        _lastMarketingMultiplier  = CalcMarketingMultiplier();

        float quality = finalScore * _lastMarketingMultiplier;

        Debug.Log($"인지도배율: {_lastPopularityMultiplier:F2} / 마케팅배율: {_lastMarketingMultiplier:F2} / 최종품질: {quality:F1}");

        // TODO: 테크트리 점수
        return quality;
    }

    float CalcPopularityMultiplier()
    {
        return ProjectSetupUI.SelectedGenrePopularity switch
        {
            1 => 0.8f,
            2 => 1.0f,
            3 => 1.3f,
            _ => 1.0f
        };
    }

    float CalcFatigueMultiplier()
    {
        int fatigue = ProjectSetupUI.SelectedGenreFatigue;
        return fatigue switch
        {
            0 => 1.0f,
            1 => 0.9f,
            2 => 0.8f,
            3 => 0.7f,
            _ => 0.7f
        };
    }

    float CalcMarketingMultiplier()
    {
        int devCost = ProjectData.GetCost(ProjectSetupUI.SelectedScale);
        if (devCost <= 0) return 1.0f;

        int marketingCost = MarketingUI.Instance.GetTotalCost();
        float P = (float)marketingCost / devCost * 100f; // 퍼센트 단위

        float M;
        if (P < 30f)
            M = 0.8f + 0.02f * (P - 20f);
        else if (P < 40f)
            M = 1.0f + 0.02f * (P - 30f);
        else
            M = Mathf.Min(1.3f, 1.2f + 0.1f * Mathf.Log(1f + (P - 40f) / 10f));

        Debug.Log($"[마케팅] 개발금: {devCost} / 마케팅비: {marketingCost} / P: {P:F1}% / M: {M:F3}");
        return M;
    }
}