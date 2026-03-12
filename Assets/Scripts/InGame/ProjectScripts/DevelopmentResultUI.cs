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

    private float _lastMarketFit;
    private float _lastMarketingBonus;
    public float LastMarketFit => _lastMarketFit;
    public float LastMarketingBonus => _lastMarketingBonus;
    string GetScaleString(ProjectScale scale) => scale switch
    {
        ProjectScale.Small => "소규모(1인개발)",
        ProjectScale.Medium => "중형(팀)",
        ProjectScale.Large => "대규모(AAA)",
        _ => ""
    };

    string GetGenreString(ProjectGenre genre) => genre switch
    {
        ProjectGenre.RPG => "RPG",
        ProjectGenre.FPS => "FPS",
        ProjectGenre.Simulation => "시뮬레이션",
        ProjectGenre.RhythmGame => "리듬게임",
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
        planningText.text = $"기획: {Mathf.RoundToInt(planning)}";
        developText.text = $"개발: {Mathf.RoundToInt(develop)}";
        artText.text = $"아트: {Mathf.RoundToInt(art)}";
        bugText.text = $"버그: {Mathf.RoundToInt(bug)}";
        creativityText.text = $"창의성: {Mathf.RoundToInt(creativity)}";

        scaleResultText.text = GetScaleString(ProjectSetupUI.SelectedScale);
        genreResultText.text = GetGenreString(ProjectSetupUI.SelectedGenre);
        platformResultText.text = GetPlatformString(ProjectSetupUI.SelectedPlatform);

        projectNameText.text = "프로젝트명";
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
            projectNameText.text = value;

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
    }
    public void OnClickRelease()
    {
        resultPanel.SetActive(false);
        AlertUI.Instance.Show("마케팅을 시작합니다.", () =>
        {
            MarketingUI.Instance.Show(() =>
            {
                float p = DevelopmentPanelUI.Instance.GetPlanning();
                float d = DevelopmentPanelUI.Instance.GetDevelop();
                float a = DevelopmentPanelUI.Instance.GetArt();
                float c = DevelopmentPanelUI.Instance.GetCreativity();
                float bRem = DevelopmentPanelUI.Instance.GetBug();

                float rawScore = CalcRawScore(p, d, a, c, ProjectSetupUI.SelectedPlatform);
                float lBug = CalcLBug(bRem);
                float sAdj = rawScore * (1f - lBug);
                float finalScore = CalcFinalScore(sAdj);
                float quality = CalcQualityScore(finalScore);

                Debug.Log($"원천: {rawScore:F1} / S_adj: {sAdj:F1} / 최종: {finalScore:F1} / 품질: {quality:F1}");

                AlertUI.Instance.Show("판매 시작!", () =>
                {
                    SalesUI.Instance.Show(quality, ProjectSetupUI.SelectedScale);
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
                return (1.5f * n * p) + (n * d) + (n * a) + (1.5f * n * c);

            case ProjectPlatform.PC:
                return (n * p) + (1.5f * n * d) + (n * a) + (1.5f * n * c);

            case ProjectPlatform.Nintendo:
                return (n * p) + (n * d) + (1.5f * n * a) + (1.5f * n * c);

            case ProjectPlatform.Console:
                return (6f * Mathf.Min(p, Mathf.Min(d, Mathf.Min(a, c)))) + (1.5f * n * c);

            default:
                return 0f;
        }
    }
    float CalcLBug(float bRem)
    {
        // 정규분포로 B_found 계산 (평균 = bRem/2, 표준편차 = bRem/4)
        float mean = bRem / 2f;
        float stdDev = bRem / 4f;
        float bFound = Mathf.Clamp(NormalRandom(mean, stdDev), 0f, bRem);

        // L_bug = 1 - 1.03^(-B_found)
        float lBug = 1f - Mathf.Pow(1.03f, -bFound);

        Debug.Log($"B_rem: {bRem:F1} / B_found: {bFound:F1} / L_bug: {lBug:F2}");

        return Mathf.Clamp01(lBug);
    }

    // 정규분포 난수 생성 (Box-Muller)
    float NormalRandom(float mean, float stdDev)
    {
        float u1 = 1f - UnityEngine.Random.value;
        float u2 = 1f - UnityEngine.Random.value;
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return mean + stdDev * z;
    }
    float CalcFinalScore(float sAdj)
    {
        float n = 15f;
        double final = n * (System.Math.Log(sAdj + n) / System.Math.Log(n)) + n;
        final = System.Math.Max(0, System.Math.Min(100, final));
        return (float)final;
    }
    float CalcQualityScore(float finalScore)
    {
        float quality = finalScore;

        _lastMarketFit = CalcMarketFit();
        _lastMarketingBonus = CalcMarketingBonus();

        quality += _lastMarketFit;
        quality += _lastMarketingBonus;

        Debug.Log($"MarketFit: {_lastMarketFit:F1} / MarketingBonus: {_lastMarketingBonus:F1}");

        // TODO: 테크트리 점수
        return Mathf.Clamp(quality, 0f, 100f);
    }
    float CalcMarketFit()
    {
        ProjectGenre popular = DevelopmentManager.Instance.GetCurrentPopularGenre();
        ProjectGenre selected = ProjectSetupUI.SelectedGenre;

        if (popular == selected)
        {
            Debug.Log($"장르 일치! +5점 ({selected})");
            return 5f;
        }

        return 0f;
    }

    float CalcMarketingBonus()
    {
        // 총 연봉
        int totalSalary = 0;
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            totalSalary += employee.salary;

        if (totalSalary <= 0) return 0f;

        int totalCost = MarketingUI.Instance.GetTotalCost();
        float ratio = (float)totalCost / totalSalary;

        // ① 10% 미만 → -3점
        if (ratio < 0.1f)
        {
            Debug.Log($"마케팅 부족 패널티 / ratio: {ratio:F2}");
            return -3f;
        }

        // ② 10% ~ 20% → 비례 상승 (0 ~ +5점)
        if (ratio < 0.2f)
        {
            float linearRatio = (ratio - 0.1f) / (0.2f - 0.1f);
            float score = linearRatio * 5f;
            Debug.Log($"마케팅 비례 상승 / ratio: {ratio:F2} / score: {score:F1}");
            return score;
        }

        // ③ 20% 이상 → 로그적 상승 (+5 ~ +10점)
        float logScore = 5f + 5f * (float)(
            System.Math.Log(1 + (ratio - 0.2f) * 10) /
            System.Math.Log(1 + 0.8f * 10)
        );

        Debug.Log($"마케팅 로그 상승 / ratio: {ratio:F2} / score: {logScore:F1}");
        return Mathf.Min(logScore, 10f);
    }
}