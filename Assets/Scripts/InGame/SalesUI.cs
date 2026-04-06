using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SalesUI : MonoBehaviour
{
    public static SalesUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject salesPanel;

    [Header("Chart")]
    public RectTransform chartArea;   // HorizontalLayoutGroup 오브젝트
    public GameObject barPrefab;      // Bar 프리팹
    private float maxBarHeight;

    [Header("UI")]
    public TextMeshProUGUI totalRevenueText;
    public TextMeshProUGUI totalUnitsText;
    public TextMeshProUGUI qualityScoreText;
    private int barCount;
    private ProjectScale _cachedScale;
    private ProjectGenre _cachedGenre;
    private ProjectPlatform _cachedPlatform;
    private string _cachedProjectName = "프로젝트명";
    private float _cachedPlanning;
    private float _cachedDevelop;
    private float _cachedArt;
    private float _cachedCreativity;
    private float _cachedBug;
    private bool _newProjectStartedDuringSales = false;
    private float _cachedQualityScore;

    public void NotifyNewProjectStarted() => _newProjectStartedDuringSales = true;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        salesPanel.SetActive(false);
    }

    public void Show(float qualityScore, ProjectScale scale)
    {
        _cachedScale = ProjectSetupUI.SelectedScale;
        _cachedGenre = ProjectSetupUI.SelectedGenre;
        _cachedPlatform = ProjectSetupUI.SelectedPlatform;
        _cachedProjectName = DevelopmentResultUI.Instance.LastProjectName;
        _cachedPlanning = DevelopmentResultUI.Instance.LastPlanning;
        _cachedDevelop = DevelopmentResultUI.Instance.LastDevelop;
        _cachedArt = DevelopmentResultUI.Instance.LastArt;
        _cachedCreativity = DevelopmentResultUI.Instance.LastCreativity;
        _cachedBug = DevelopmentResultUI.Instance.LastBug;
        GameTimeManager.Instance.ForceStartTime();
        ShowInternal(qualityScore, scale);
    }
    public void ShowWithProjectName(
        float qualityScore, ProjectScale scale, string projectName,
        ProjectScale cachedScale, ProjectGenre cachedGenre, ProjectPlatform cachedPlatform,
        float planning, float develop, float art, float creativity, float bug)
    {
        _cachedScale = cachedScale;
        _cachedGenre = cachedGenre;
        _cachedPlatform = cachedPlatform;
        _cachedProjectName = projectName;
        _cachedPlanning = planning;
        _cachedDevelop = develop;
        _cachedArt = art;
        _cachedCreativity = creativity;
        _cachedBug = bug;
        GameTimeManager.Instance.ForceStartTime();
        ShowInternal(qualityScore, scale);
    }
    void ShowInternal(float qualityScore, ProjectScale scale)
    {
        _cachedQualityScore = qualityScore;
        _newProjectStartedDuringSales = false;
        DevelopmentManager.Instance.CurrentStage = ProjectStage.Sales;
        DevelopmentPanelUI.Instance.ResetValues();
        DevelopmentPanelUI.Instance.ResetMarketFit();
        DevelopmentTimerUI.Instance.ResetTimer();

        foreach (Transform child in chartArea)
            Destroy(child.gameObject);

        qualityScoreText.text = $"품질: {qualityScore:F1}점";

        int scaleMultiplier = scale switch
        {
            ProjectScale.Small  => 1,
            ProjectScale.Medium => 3,
            ProjectScale.Large  => 5,
            _ => 1
        };
        float rand = UnityEngine.Random.Range(0.9f, 1.1f);
        int totalUnits = Mathf.RoundToInt(
            (5000f + 200f * scaleMultiplier * Mathf.Pow(qualityScore / 100f, 2f)) * rand
        );

        float[] distribution = CalcDistribution(scale);
        barCount = distribution.Length;
        int[] unitPerPeriod = new int[barCount];
        for (int i = 0; i < barCount; i++)
            unitPerPeriod[i] = Mathf.RoundToInt(totalUnits * distribution[i]);

        int maxUnits = 0;
        foreach (var u in unitPerPeriod) if (u > maxUnits) maxUnits = u;

        int totalRevenue = totalUnits * 9;
        totalUnitsText.text = $"총 판매량: {totalUnits:N0}개";
        totalRevenueText.text = $"총 매출: {totalRevenue:N0}G";

        salesPanel.SetActive(true);
        StartCoroutine(ShowBarsSequentially(unitPerPeriod, maxUnits));
    }
    IEnumerator ShowBarsSequentially(int[] unitPerPeriod, int maxUnits)
    {
        yield return null; // ← 한 프레임 대기 후 높이 계산
        maxBarHeight = chartArea.rect.height * 0.9f;

        int cumulativeUnits = 0;

        for (int i = 0; i < barCount; i++)
        {
            var barObj = Instantiate(barPrefab, chartArea);
            var barImage = barObj.transform.Find("BarImage").GetComponent<RectTransform>();
            var valueLabel = barObj.transform.Find("ValueLabel").GetComponent<TMPro.TextMeshProUGUI>();
            var periodLabel = barObj.transform.Find("PeriodLabel").GetComponent<TMPro.TextMeshProUGUI>();

            periodLabel.text = $"{i + 1}주";
            valueLabel.text = "";
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, 0f);

            float targetHeight = maxUnits > 0 ? ((float)unitPerPeriod[i] / maxUnits) * maxBarHeight : 0f;
            int startUnits = cumulativeUnits;
            int endUnits = cumulativeUnits + unitPerPeriod[i];

            float weekDuration = _cachedScale switch
            {
                ProjectScale.Small  => 5f,
                ProjectScale.Medium => 4.2f,
                ProjectScale.Large  => 3.9f,
                _ => 5f
            };
            float barAnimDuration = weekDuration * 0.7f;
            float elapsed = 0f;
            while (elapsed < barAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / barAnimDuration);

                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, t * targetHeight);
                int currentUnits = Mathf.RoundToInt(Mathf.Lerp(startUnits, endUnits, t));
                totalUnitsText.text = $"총 판매량: {currentUnits:N0}개";
                totalRevenueText.text = $"총 매출: {currentUnits * 9:N0}G";

                yield return null;
            }

            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);
            valueLabel.text = $"{unitPerPeriod[i]:N0}";
            totalUnitsText.text = $"총 판매량: {endUnits:N0}개";
            totalRevenueText.text = $"총 매출: {endUnits * 9:N0}G";

            cumulativeUnits = endUnits;

            yield return new WaitForSeconds(weekDuration * 0.3f);
        }
        QuestManager.Instance.UpdateProgress(QuestType.TotalSales, cumulativeUnits);
        yield return new WaitForSeconds(0.5f);

        AlertUI.Instance.Show("피드백 시간!", () =>
        {
            float cachedMarketFit = DevelopmentResultUI.Instance.LastPopularityMultiplier;
            float cachedMarketingBonus = DevelopmentResultUI.Instance.LastMarketingMultiplier;
            float cachedBug = DevelopmentResultUI.Instance.LastBug;
            float cachedReleaseBonus   = DevelopmentResultUI.Instance.LastReleaseEventBonus;
            salesPanel.SetActive(false);

            // ── 1. 완료 프로젝트 저장 (초기화 전에 먼저) ──
            var completedData = new CompletedProjectData
            {
                projectName = _cachedProjectName,
                scale = (int)_cachedScale,
                genre = (int)_cachedGenre,
                platform = (int)_cachedPlatform,
                planning = _cachedPlanning,    // ← DevelopmentResultUI 대신
                develop = _cachedDevelop,
                art = _cachedArt,
                creativity = _cachedCreativity,
                bug = _cachedBug,
                totalUnits = cumulativeUnits,
                totalRevenue = cumulativeUnits * 9,
                year = GameTimeManager.Instance.Year,
                month = GameTimeManager.Instance.Month,
                week = GameTimeManager.Instance.Week,
                qualityScore = _cachedQualityScore,
                criticTotalScore = CriticReviewUI.Instance != null ? CriticReviewUI.Instance.LastCriticTotal : 0,
            };
            Debug.Log($"저장: scale={completedData.scale} genre={completedData.genre} platform={completedData.platform}");
            CompletedProjectManager.Instance.SaveCompletedProject(completedData);

            // ── 2. 매출 지급 ──
            int totalRevenue = cumulativeUnits * 9;
            MoneyManager.Instance.AddGold(totalRevenue);

            // ── 3. 프로젝트 Complete 저장 후 초기화 ──
            // Sales 중 새 프로젝트가 시작된 경우 ResetProject 스킵
            if (!_newProjectStartedDuringSales)
            {
                DevelopmentManager.Instance.CurrentStage = ProjectStage.Complete;
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                DevelopmentManager.Instance.ResetProject();
            }

            // ── 4. 퀘스트 (이미 위에서 처리됐으므로 여기선 생략 가능)

            // ── 5. 피드백 ──
            FeedbackUI.Instance.Show(cachedMarketFit, cachedMarketingBonus, cachedBug, cachedReleaseBonus);
        });
    }

    // 규모별 주차 분배
    float[] CalcDistribution(ProjectScale scale)
    {
        return scale switch
        {
            ProjectScale.Small  => new float[] { 0.50f, 0.30f, 0.20f },
            ProjectScale.Medium => new float[] { 0.35f, 0.25f, 0.15f, 0.12f, 0.08f, 0.05f },
            ProjectScale.Large  => new float[] { 0.24f, 0.18f, 0.15f, 0.12f, 0.10f, 0.08f, 0.06f, 0.04f, 0.03f },
            _ => new float[] { 0.50f, 0.30f, 0.20f }
        };
    }

    public void OnClickClose()
    {
        salesPanel.SetActive(false);
    }


    [Header("Test")]
    public float testQualityScore = 73f;
    public ProjectScale testScale = ProjectScale.Medium;
    [ContextMenu("테스트 실행")]
    public void TestShow()
    {
        Show(testQualityScore, testScale);
    }
}