using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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

    [Header("Contribution (기여도 1등)")]
    public Image contributorPortrait;          // Row2/PortraitPanel/portraitImage
    public TextMeshProUGUI contributorNameText;// Row2/ContributionPanel/nameText
    public TextMeshProUGUI contributorRateText;// Row2/ContributionPanel/contributionRateText

    [Header("Contribution Detail (전체 순위)")]
    public GameObject contributionDetailPanel; // 중앙 순위 패널 (closeBtn 자식)
    public Button contributionCheckBtn;        // 상세 열기 버튼
    public Button contributionCloseBtn;        // 전체화면 백드롭 닫기 버튼 (CEOInfoUI 방식)
    public GameObject rowPrefab;               // RowPanel 프리팹 (numberPanel/namePanel/contriPanel)
    public Transform contributionContent;      // 행이 생성될 부모 (Content)
    public int defaultRowCount = 10;           // 기본 확보 행 수 (부족하면 추가 생성)
    private readonly List<GameObject> _rowPool = new();
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
        ProjectScale.Small => "소규모",
        ProjectScale.Medium => "중형",
        ProjectScale.Large => "대형",
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
        ProjectGenre.Puzzle           => "퍼즐",
        _ => ""
    };

    string GetPlatformString(ProjectPlatform platform) => platform switch
    {
        ProjectPlatform.Mobile => "모바일",
        ProjectPlatform.PC => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console => "플레이스테이션",
        _ => ""
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        resultPanel.SetActive(false);
        editNamePanel.SetActive(false);
        if (contributionDetailPanel != null) contributionDetailPanel.SetActive(false);
        if (contributionCloseBtn != null) contributionCloseBtn.gameObject.SetActive(false);
        if (contributionCheckBtn != null)
        {
            contributionCheckBtn.onClick.RemoveAllListeners();
            contributionCheckBtn.onClick.AddListener(OnClickContributionDetail);
        }
        if (contributionCloseBtn != null)
        {
            contributionCloseBtn.onClick.RemoveAllListeners();
            contributionCloseBtn.onClick.AddListener(OnCloseContributionDetail);
        }
    }

    // 닫기 — 전체화면 백드롭 + 순위 패널 끄고 메인 복귀 (CEOInfoUI.OnClickClose 방식)
    public void OnCloseContributionDetail()
    {
        if (contributionDetailPanel != null) contributionDetailPanel.SetActive(false);
        if (contributionCloseBtn != null) contributionCloseBtn.gameObject.SetActive(false);
        resultPanel.SetActive(true);
    }

    // 기여도 상세 — 메인 끄고 백드롭 + 순위 패널 켜기. 기본 defaultRowCount 행 확보, 부족하면 추가 생성.
    public void OnClickContributionDetail()
    {
        if (contributionDetailPanel == null || rowPrefab == null || contributionContent == null) return;

        resultPanel.SetActive(false);
        if (contributionCloseBtn != null) contributionCloseBtn.gameObject.SetActive(true);
        contributionDetailPanel.SetActive(true);

        var ranking = DevelopmentManager.Instance.GetContributionRanking();
        int rowCount = Mathf.Max(defaultRowCount, ranking.Count); // 기본 10행 활성, 부족하면 빈 행
        EnsureRowPool(rowCount);

        for (int i = 0; i < _rowPool.Count; i++)
        {
            var row = _rowPool[i];
            if (row == null) continue;
            if (i < rowCount)
            {
                row.SetActive(true);
                if (i < ranking.Count)
                    SetContributionRow(row, i + 1, ranking[i].name, $"{ranking[i].percent:F0}%");
                else
                    SetContributionRow(row, i + 1, "", ""); // 빈 슬롯 — 번호만, 이름·기여도 공백
            }
            else row.SetActive(false);
        }
    }

    // Content 아래 행 풀을 count 개 이상 확보 (모자라면 프리팹 생성)
    void EnsureRowPool(int count)
    {
        while (_rowPool.Count < count)
        {
            var go = Instantiate(rowPrefab);
            go.transform.SetParent(contributionContent, false); // 로컬 트랜스폼 보존(스케일 깨짐 방지)
            _rowPool.Add(go);
        }
    }

    void SetContributionRow(GameObject row, int rank, string name, string rate)
    {
        var numberText = row.transform.Find("numberPanel")?.GetComponentInChildren<TextMeshProUGUI>();
        var nameText   = row.transform.Find("namePanel")?.GetComponentInChildren<TextMeshProUGUI>();
        var rateText   = row.transform.Find("contriPanel")?.GetComponentInChildren<TextMeshProUGUI>();
        if (numberText != null) numberText.text = rank.ToString();
        if (nameText != null)   nameText.text   = name;
        if (rateText != null)   rateText.text   = rate;
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
        if (planningText != null)   planningText.text = $"{Mathf.RoundToInt(planning)}";
        if (developText != null)    developText.text = $"{Mathf.RoundToInt(develop)}";
        if (artText != null)        artText.text = $"{Mathf.RoundToInt(art)}";
        if (bugText != null)        bugText.text = $"{Mathf.RoundToInt(bug)}";
        if (creativityText != null) creativityText.text = $"{Mathf.RoundToInt(creativity)}";

        // Row1 — 규모/장르/플랫폼을 각 텍스트에 개별 출력
        if (scaleResultText != null)    scaleResultText.text    = $"규모: {GetScaleString(ProjectSetupUI.SelectedScale)}";
        if (genreResultText != null)    genreResultText.text    = $"장르: {GetGenreString(ProjectSetupUI.SelectedGenre)}";
        if (platformResultText != null) platformResultText.text = $"플랫폼: {GetPlatformString(ProjectSetupUI.SelectedPlatform)}";

        // Row2 — 기여도 1등 초상화 + 이름(퇴사면 "(퇴사)") + 기여도%
        var top = DevelopmentManager.Instance.GetTopContributor();
        if (!string.IsNullOrEmpty(top.name))
        {
            if (contributorPortrait != null && !string.IsNullOrEmpty(top.portraitId))
            {
                var sprite = Resources.Load<Sprite>($"Portraits/Mini/{top.portraitId}");
                if (sprite != null) contributorPortrait.sprite = sprite;
            }
            if (contributorNameText != null) contributorNameText.text = top.name;
            if (contributorRateText != null) contributorRateText.text = $"기여도 {top.percent:F0}%";
        }

        projectNameText.text = "프로젝트명";
        GameTimeManager.Instance?.StopTime();
        if (contributionDetailPanel != null) contributionDetailPanel.SetActive(false);
        if (contributionCloseBtn != null) contributionCloseBtn.gameObject.SetActive(false);
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

                    // 쏠린 케이스 감점 (중형, 대작만) - criticScore/sAdj 공통 적용
                    // 불균형 = (max(P,D,A) - avg(P,D,A)) / avg(P,D,A)
                    // 감점% = MIN(30%, 30% × MAX(0, 불균형 - 0.30) / 0.50)
                    float casePenalty = 0f;
                    if (ProjectSetupUI.SelectedScale == ProjectScale.Medium || ProjectSetupUI.SelectedScale == ProjectScale.Large)
                    {
                        float avgStat = (p + d + a) / 3f;
                        if (avgStat > 0f)
                        {
                            float maxStat = Mathf.Max(p, Mathf.Max(d, a));
                            float imbalance = (maxStat - avgStat) / avgStat;
                            casePenalty = Mathf.Min(0.30f, 0.30f * Mathf.Max(0f, imbalance - 0.30f) / 0.50f);
                        }
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

                Debug.Log($"원천: {rawScore:F1} / 버그감점: {DevelopmentManager.Instance.BugPenalty * 100f:F1}% / 케이스감점: {casePenalty * 100f:F1}% / S_adj: {sAdj:F1} / 인지도배율: {CalcPopularityMultiplier():F2} / 피로도배율: {CalcFatigueMultiplier():F2} / 최종: {finalScore:F1} / 품질: {quality:F1}");

                ProjectSaveManager.Instance.SetQualityScore(quality, ProjectSetupUI.SelectedScale);
                DevelopmentManager.Instance.CurrentStage = ProjectStage.Marketing;
                MoneyManager.Instance.SaveMoney();
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();

                // 조기 저장 — 앱 종료로 인한 Sales 유실 방지. revenuePerPeriod 는 SalesUI.Show 호출 시점에 결정/저장됨.
                SalesSaveManager.Instance.SaveSales(
                    0, 0, null, quality, ProjectSetupUI.SelectedScale,
                    _lastProjectName,
                    ProjectSetupUI.SelectedScale, ProjectSetupUI.SelectedGenre, ProjectSetupUI.SelectedPlatform,
                    _lastPlanning, _lastDevelop, _lastArt, _lastCreativity, _lastBug
                );

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
            1 => 0.97f,
            2 => 1.0f,
            3 => 1.03f,
            _ => 1.0f
        };
    }

    float CalcFatigueMultiplier()
    {
        int fatigue = ProjectSetupUI.SelectedGenreFatigue;
        return fatigue switch
        {
            0 => 1.0f,
            1 => 0.98f,
            2 => 0.96f,
            3 => 0.93f,
            _ => 0.93f
        };
    }

    float CalcMarketingMultiplier()
    {
        // 마케팅의 신 trait — 마케팅 개수/비용 무관하게 최고치(매출변화 +15% → M=1.15) 적용
        if (TraitEffectApplier.HasMarketingFree())
        {
            Debug.Log($"[마케팅] 마케팅의 신 발동 → M=1.15");
            return 1.15f;
        }

        // P = 마케팅비 / 전체 직원 연봉 (퍼센트 단위)
        int totalSalary = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetTotalSalary() : 0;
        if (totalSalary <= 0) return 1.0f;

        int marketingCost = MarketingUI.Instance.GetTotalCost();
        float P = (float)marketingCost / totalSalary * 100f;

        // 매출변화(%) 구간별 선형 보간 → M = 1 + 매출변화/100
        float salesChange;
        if (P < 10f)
            salesChange = -15f;
        else if (P < 15f)
            salesChange = -15f + (P - 10f) * 2f; // -15% → -5%
        else if (P < 20f)
            salesChange = -5f + (P - 15f) * 2f;  // -5%  → +5%
        else if (P < 25f)
            salesChange = 5f + (P - 20f) * 2f;   // +5%  → +15%
        else
            salesChange = 15f;

        float M = 1f + salesChange / 100f;
        Debug.Log($"[마케팅] 전체연봉: {totalSalary} / 마케팅비: {marketingCost} / P: {P:F1}% / 매출변화: {salesChange:F1}% / M: {M:F3}");
        return M;
    }
}