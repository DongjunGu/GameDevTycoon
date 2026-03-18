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
    public float maxBarHeight = 300f;

    [Header("UI")]
    public TextMeshProUGUI totalRevenueText;
    public TextMeshProUGUI totalUnitsText;
    public TextMeshProUGUI qualityScoreText;
    [Header("Animation")]
    public float barAnimDuration = 0.4f;   // 바 올라오는 속도
    public float barSpawnDelay = 0.1f;     // 다음 바 생성 대기 시간
    private ProjectScale _cachedScale;
    private ProjectGenre _cachedGenre;
    private ProjectPlatform _cachedPlatform;
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
        GameTimeManager.Instance.StartTime();

        // 기존 바 제거
        foreach (Transform child in chartArea)
            Destroy(child.gameObject);

        qualityScoreText.text = $"품질: {qualityScore:F1}점";

        int totalUnits = Mathf.RoundToInt(qualityScore * UnityEngine.Random.Range(130, 161)); //추후 팬덤영향

        float[] distribution = CalcDistribution(scale);
        int[] unitPerPeriod = new int[8];
        for (int i = 0; i < 8; i++)
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
        int cumulativeUnits = 0;

        for (int i = 0; i < 8; i++)
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

            yield return new WaitForSeconds(barSpawnDelay);
        }
        QuestManager.Instance.UpdateProgress(QuestType.TotalSales, cumulativeUnits);
        yield return new WaitForSeconds(0.5f);

        AlertUI.Instance.Show("피드백 시간!", () =>
        {
            int totalRevenue = cumulativeUnits * 9;
            MoneyManager.Instance.AddGold(totalRevenue);
            DevelopmentManager.Instance.ResetProject();
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            salesPanel.SetActive(false);
            var completedData = new CompletedProjectData
            {
                projectName = DevelopmentResultUI.Instance.LastProjectName,
                scale = (int)_cachedScale,
                genre = (int)_cachedGenre,
                platform = (int)_cachedPlatform,
                planning = DevelopmentResultUI.Instance.LastPlanning,
                develop = DevelopmentResultUI.Instance.LastDevelop,
                art = DevelopmentResultUI.Instance.LastArt,
                creativity = DevelopmentResultUI.Instance.LastCreativity,
                bug = DevelopmentResultUI.Instance.LastBug,
                totalUnits = cumulativeUnits,
                totalRevenue = cumulativeUnits * 9,
                year = GameTimeManager.Instance.Year,
                month = GameTimeManager.Instance.Month,
                week = GameTimeManager.Instance.Week,
            };

            Debug.Log($"저장: scale={completedData.scale} genre={completedData.genre} platform={completedData.platform}");
            CompletedProjectManager.Instance.SaveCompletedProject(completedData);

            FeedbackUI.Instance.Show(
                DevelopmentResultUI.Instance.LastMarketFit,
                DevelopmentResultUI.Instance.LastMarketingBonus,
                DevelopmentResultUI.Instance.LastBug
            );
        });
    }

    // 규모별 감소 곡선
    float[] CalcDistribution(ProjectScale scale)
    {
        float decayRate = scale switch
        {
            ProjectScale.Small => 0.55f, // 급격한 하락
            ProjectScale.Medium => 0.70f, // 중간
            ProjectScale.Large => 0.82f, // 완만한 하락
            _ => 0.65f
        };

        float[] weights = new float[8];
        weights[0] = 1f;
        for (int i = 1; i < 8; i++)
            weights[i] = weights[i - 1] * decayRate;

        // 합계로 정규화
        float sum = 0f;
        foreach (var w in weights) sum += w;
        for (int i = 0; i < 8; i++) weights[i] /= sum;

        return weights;
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