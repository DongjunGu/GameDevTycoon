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
    public TextMeshProUGUI rankText;
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
    private int _completedBarIndex = 0;
    private int _cachedTotalUnits = 0;
    private CompletedProjectData _currentSalesProject = null;

    public void NotifyNewProjectStarted()
    {
        _newProjectStartedDuringSales = true;
        SalesSaveManager.Instance?.SaveNewProjectStarted();
    }
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
        ShowInternal(qualityScore, scale, applyCompletion: true);
    }
    public void ShowWithProjectName(
        float qualityScore, ProjectScale scale, string projectName,
        ProjectScale cachedScale, ProjectGenre cachedGenre, ProjectPlatform cachedPlatform,
        float planning, float develop, float art, float creativity, float bug,
        int completedWeeks = 0, int savedTotalUnits = 0, bool applyCompletion = true)
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
        ShowInternal(qualityScore, scale, completedWeeks, savedTotalUnits, applyCompletion);
    }
    void ShowInternal(float qualityScore, ProjectScale scale, int completedWeeks = 0, int savedTotalUnits = 0, bool applyCompletion = true)
    {
        _cachedQualityScore = qualityScore;
        _newProjectStartedDuringSales = !applyCompletion && SalesSaveManager.Instance != null && SalesSaveManager.Instance.LoadedNewProjectStarted;
        // 새 프로젝트가 진행 중이면 CurrentStage를 Sales로 바꾸지 않음 (Developing 유지 → 틱/애니메이션 정상 작동)
        if (!_newProjectStartedDuringSales)
        {
            DevelopmentManager.Instance.CurrentStage = ProjectStage.Sales;
            OfficeManager.Instance?.RefreshAllDeskAnimations();
        }
        if (!applyCompletion)
        {
            // 복원 시: rowInDate로 정확히 찾아 연결
            string targetRowInDate = SalesSaveManager.Instance?.LoadedCompletedProjectRowInDate ?? "";
            if (!string.IsNullOrEmpty(targetRowInDate))
                _currentSalesProject = CompletedProjectManager.Instance?.completedProjects
                    .Find(p => p.rowInDate == targetRowInDate);
            else
                _currentSalesProject = CompletedProjectManager.Instance?.completedProjects
                    .Find(p => p.totalUnits == 0); // fallback

            if (_currentSalesProject == null)
                Debug.LogWarning("[SalesUI] 복원 시 currentSalesProject를 찾지 못함");
            else
                Debug.Log($"[SalesUI] 복원 시 currentSalesProject 연결: rowInDate={_currentSalesProject.rowInDate}");
        }
        else
        {
            EmployeeManager.Instance?.OnProjectCompleted();
            _currentSalesProject = new CompletedProjectData
            {
                projectName      = _cachedProjectName,
                scale            = (int)_cachedScale,
                genre            = (int)_cachedGenre,
                platform         = (int)_cachedPlatform,
                planning         = _cachedPlanning,
                develop          = _cachedDevelop,
                art              = _cachedArt,
                creativity       = _cachedCreativity,
                bug              = _cachedBug,
                totalUnits       = 0,
                totalRevenue     = 0,
                year             = GameTimeManager.Instance.Year,
                month            = GameTimeManager.Instance.Month,
                week             = GameTimeManager.Instance.Week,
                qualityScore     = qualityScore,
                criticTotalScore = CriticReviewUI.Instance != null ? CriticReviewUI.Instance.LastCriticTotal : 0,
            };
            CompletedProjectManager.Instance.SaveCompletedProject(_currentSalesProject);
        }
        if (!_newProjectStartedDuringSales)
        {
            DevelopmentPanelUI.Instance.ResetValues();
            DevelopmentPanelUI.Instance.ResetMarketFit();
            DevelopmentTimerUI.Instance.ResetTimer();
        }

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
        int totalUnits;
        if (savedTotalUnits > 0)
            totalUnits = savedTotalUnits;
        else
        {
            float rand = UnityEngine.Random.Range(0.9f, 1.1f);
            totalUnits = Mathf.RoundToInt(
                (5000f + 200f * scaleMultiplier * Mathf.Pow(qualityScore / 100f, 2f)) * rand
            );
        }
        _cachedTotalUnits = totalUnits;
        _completedBarIndex = completedWeeks;
        SalesSaveManager.Instance?.SaveSales(
            completedWeeks, totalUnits, _cachedQualityScore, scale, _cachedProjectName,
            _cachedScale, _cachedGenre, _cachedPlatform,
            _cachedPlanning, _cachedDevelop, _cachedArt, _cachedCreativity, _cachedBug
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
        StartCoroutine(ShowBarsSequentially(unitPerPeriod, maxUnits, completedWeeks));
    }
    IEnumerator ShowBarsSequentially(int[] unitPerPeriod, int maxUnits, int completedWeeks = 0)
    {
        yield return null; // ← 한 프레임 대기 후 높이 계산
        maxBarHeight = chartArea.rect.height * 0.9f;

        int cumulativeUnits = 0;

        for (int i = 0; i < barCount; i++)
        {
            float targetHeight = maxUnits > 0 ? ((float)unitPerPeriod[i] / maxUnits) * maxBarHeight : 0f;
            int endUnits = cumulativeUnits + unitPerPeriod[i];

            var barObj = Instantiate(barPrefab, chartArea);
            var barImage = barObj.transform.Find("BarImage").GetComponent<RectTransform>();
            var valueLabel = barObj.transform.Find("ValueLabel").GetComponent<TMPro.TextMeshProUGUI>();
            var periodLabel = barObj.transform.Find("PeriodLabel").GetComponent<TMPro.TextMeshProUGUI>();
            periodLabel.text = $"{i + 1}주";

            // 이미 완료된 주차는 즉시 표시 (돈 지급 없음)
            if (i < completedWeeks)
            {
                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);
                valueLabel.text = $"{unitPerPeriod[i]:N0}";
                cumulativeUnits = endUnits;
                totalUnitsText.text = $"총 판매량: {cumulativeUnits:N0}개";
                totalRevenueText.text = $"총 매출: {cumulativeUnits * 9:N0}G";
                continue;
            }

            // 미완료 주차 - 애니메이션
            valueLabel.text = "";
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, 0f);

            int startUnits = cumulativeUnits;

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
                if (barObj == null) yield break;
                if (!GameTimeManager.Instance.IsRunning) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / barAnimDuration);

                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, t * targetHeight);
                int currentUnits = Mathf.RoundToInt(Mathf.Lerp(startUnits, endUnits, t));
                totalUnitsText.text = $"총 판매량: {currentUnits:N0}개";
                totalRevenueText.text = $"총 매출: {currentUnits * 9:N0}G";

                yield return null;
            }

            if (barObj == null) yield break;
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);

            int weeklyRevenue = unitPerPeriod[i] * 9;
            int rank = CalcRank(weeklyRevenue);
            valueLabel.text = $"{unitPerPeriod[i]:N0}";
            if (rankText != null)
                rankText.text = rank > 0 ? $"{rank}위" : "순위권 밖";
            totalUnitsText.text = $"총 판매량: {endUnits:N0}개";
            totalRevenueText.text = $"총 매출: {endUnits * 9:N0}G";

            MoneyManager.Instance.AddGold(weeklyRevenue);

            _completedBarIndex = i + 1;
            SalesSaveManager.Instance?.SaveSales(
                _completedBarIndex, _cachedTotalUnits, _cachedQualityScore, _cachedScale, _cachedProjectName,
                _cachedScale, _cachedGenre, _cachedPlatform,
                _cachedPlanning, _cachedDevelop, _cachedArt, _cachedCreativity, _cachedBug
            );

            cumulativeUnits = endUnits;

            float gap = weekDuration * 0.3f;
            float gapElapsed = 0f;
            while (gapElapsed < gap)
            {
                if (GameTimeManager.Instance.IsRunning) gapElapsed += Time.deltaTime;
                yield return null;
            }
        }
        QuestManager.Instance.UpdateProgress(QuestType.TotalSales, cumulativeUnits);
        float endWait = 0f;
        while (endWait < 0.5f)
        {
            if (GameTimeManager.Instance.IsRunning) endWait += Time.deltaTime;
            yield return null;
        }

        AlertUI.Instance.Show("판매 완료!", () => OnSalesComplete(cumulativeUnits));
    }

    void OnSalesComplete(int cumulativeUnits)
    {
        salesPanel.SetActive(false);

        if (_currentSalesProject != null)
        {
            Debug.Log($"업데이트: scale={_currentSalesProject.scale} genre={_currentSalesProject.genre} units={cumulativeUnits}");
            CompletedProjectManager.Instance.UpdateSalesResult(_currentSalesProject, cumulativeUnits, cumulativeUnits * 9);
            _currentSalesProject = null;
        }

        SalesSaveManager.Instance?.CompleteSales();

        if (!_newProjectStartedDuringSales)
        {
            DevelopmentManager.Instance.CurrentStage = ProjectStage.Complete;
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            DevelopmentManager.Instance.ResetProject();
        }
        else
        {
            MoneyManager.Instance.SaveMoney();
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
        }
    }

    int CalcRank(int weeklyRevenue)
    {
        if (weeklyRevenue >= 120000) return 1;
        if (weeklyRevenue >= 85000)  return 2;
        if (weeklyRevenue >= 70000)  return 3;
        if (weeklyRevenue >= 55000)  return 4;
        if (weeklyRevenue >= 45000)  return 5;
        if (weeklyRevenue >= 35000)  return 6;
        if (weeklyRevenue >= 28000)  return 7;
        if (weeklyRevenue >= 20000)  return 8;
        if (weeklyRevenue >= 15000)  return 9;
        if (weeklyRevenue >= 10000)  return 10;
        if (weeklyRevenue >= 5000)
            return Mathf.Clamp(Mathf.RoundToInt(30f - ((weeklyRevenue - 5000f) / 5000f * 19f)), 11, 30);
        if (weeklyRevenue >= 2500)
            return Mathf.Clamp(Mathf.RoundToInt(60f - ((weeklyRevenue - 2500f) / 2500f * 29f)), 31, 60);
        if (weeklyRevenue >= 1500)
            return Mathf.Clamp(Mathf.RoundToInt(100f - ((weeklyRevenue - 1500f) / 1000f * 39f)), 61, 100);
        return 0; // 순위권 밖
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