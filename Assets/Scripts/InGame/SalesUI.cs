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
    private int _cachedTotalRevenue = 0;
    private int[] _cachedRevenuePerPeriod = null; // 세션 시작 시 동결, 매 SaveSales 호출에 그대로 전달
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
        int completedWeeks = 0, int savedTotalRevenue = 0, int[] savedRevenuePerPeriod = null, bool applyCompletion = true)
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
        ShowInternal(qualityScore, scale, completedWeeks, savedTotalRevenue, savedRevenuePerPeriod, applyCompletion);
    }
    void ShowInternal(float qualityScore, ProjectScale scale, int completedWeeks = 0, int savedTotalRevenue = 0, int[] savedRevenuePerPeriod = null, bool applyCompletion = true)
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
                    .Find(p => p.totalRevenue == 0); // fallback

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
        float[] distribution = CalcDistribution(scale);
        barCount = distribution.Length;

        int[] revenuePerPeriod;
        int totalRevenue;
        // 복원 분기: 저장된 배열이 distribution 길이와 일치하고 sum>0 이면 그대로 사용 → 해금 가드 재통과 X.
        bool hasSavedArray = savedRevenuePerPeriod != null && savedRevenuePerPeriod.Length == barCount;
        if (hasSavedArray)
        {
            int sum = 0;
            foreach (var r in savedRevenuePerPeriod) sum += r;
            hasSavedArray = sum > 0;
        }

        if (hasSavedArray)
        {
            revenuePerPeriod = savedRevenuePerPeriod;
            // 원본 totalRevenue 는 표시/저장 호환을 위해 배열 합 또는 savedTotalRevenue 중 큰 값으로
            totalRevenue = savedTotalRevenue;
            if (totalRevenue <= 0)
                foreach (var r in revenuePerPeriod) totalRevenue += r;
        }
        else
        {
            // 신규 계산 — 첫 ShowInternal 진입 시점에 1회만 도달. 이후 복원에선 위 분기로 들어감.

            // 테크트리 '장인 정신(money_craftsman)' — 출시 시점에 카운터 +1 (cap 20). 그 카운트로 매출 보너스.
            // 해금 후 첫 게임: 카운트 1 → +0.5%, 20번째: +10%, 21번째 이상: cap 유지.
            TechTreeManager.Instance?.IncrementCraftsmanCount();
            float craftsmanBonus = TechTreeManager.Instance != null
                ? TechTreeManager.Instance.CraftsmanBonusMultiplier() : 1f;

            // 테크트리 '만점 신화(money_perfect)' — 평론가 점수 100점 정확히일 때만 +15%.
            float perfectBonus = 0f;
            if (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("money_perfect")
                && CriticReviewUI.Instance != null && CriticReviewUI.Instance.LastCriticTotal == 100)
                perfectBonus = 0.15f;

            // TODO: 테크트리 '익숙한 맛(money_sequel)' — 후속작 출시 시 +20%. 후속작 시스템 미구현 상태라 보류.
            // 구현 시 perfectBonus 와 같은 패턴으로 sequelBonus 추가 + bonusSum 에 합산.

            if (savedTotalRevenue > 0)
                totalRevenue = savedTotalRevenue;
            else
            {
                float rand         = UnityEngine.Random.Range(0.9f, 1.1f);
                float youtuberBonus = RandomEventManager.Instance != null
                    ? RandomEventManager.Instance.YoutuberSalesBonus : 1.0f;
                // 오타쿠 특성: 보유 오타쿠의 고정 장르가 이 프로젝트 장르와 일치하면 매출 +20%.
                float otakuSalesBonus  = CharacterTraitApplier.GetOtakuSalesBonus(_cachedGenre);
                // 매출 보너스는 합연산 — 유튜버 +5% + 장인정신 +10% + 만점 +15% + 오타쿠 +20% (곱셈 아님).
                // rand 는 별개의 자연스러운 변동성이라 곱셈 유지.
                float bonusSum         = (youtuberBonus - 1f) + (craftsmanBonus - 1f) + perfectBonus + otakuSalesBonus;
                float totalMultiplier  = Mathf.Max(0f, 1f + bonusSum);
                totalRevenue = Mathf.RoundToInt(
                    (5000f + 200f * scaleMultiplier * Mathf.Pow(qualityScore / 100f, 2f)) * rand * totalMultiplier
                );
                if (RandomEventManager.Instance != null)
                    RandomEventManager.Instance.YoutuberSalesBonus = 1.0f; // 적용 후 초기화
            }

            revenuePerPeriod = new int[barCount];
            for (int i = 0; i < barCount; i++)
                revenuePerPeriod[i] = Mathf.RoundToInt(totalRevenue * distribution[i]);

            // 테크트리 '역주행(money_comeback)' — 첫 호출 시점의 해금 상태로 한 번 결정. 배열에 박혀 저장됨.
            bool comebackUnlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("money_comeback");
            if (comebackUnlocked && barCount > 0)
                revenuePerPeriod[barCount - 1] *= 3;
        }

        _cachedTotalRevenue       = totalRevenue;
        _cachedRevenuePerPeriod   = revenuePerPeriod;
        _completedBarIndex        = completedWeeks;

        SalesSaveManager.Instance?.SaveSales(
            completedWeeks, totalRevenue, revenuePerPeriod, _cachedQualityScore, scale, _cachedProjectName,
            _cachedScale, _cachedGenre, _cachedPlatform,
            _cachedPlanning, _cachedDevelop, _cachedArt, _cachedCreativity, _cachedBug
        );

        int maxRevenue = 0;
        foreach (var r in revenuePerPeriod) if (r > maxRevenue) maxRevenue = r;

        int adjustedTotalRevenue = 0;
        foreach (var r in revenuePerPeriod) adjustedTotalRevenue += r;
        totalRevenueText.text = $"총 매출: {adjustedTotalRevenue:N0}G";

        salesPanel.SetActive(true);
        StartCoroutine(ShowBarsSequentially(revenuePerPeriod, maxRevenue, completedWeeks));
    }
    IEnumerator ShowBarsSequentially(int[] revenuePerPeriod, int maxRevenue, int completedWeeks = 0)
    {
        yield return null; // ← 한 프레임 대기 후 높이 계산
        maxBarHeight = chartArea.rect.height * 0.9f;

        int cumulativeRevenue = 0;

        // 초심자의 행운 — 한 런 중 첫 SalesUI 세션에만 +pct% 가산 (이 세션 내 모든 주차에 일관 적용)
        int firstSaleBonusPct = TraitEffectApplier.ConsumeFirstSaleBonusPct();
        if (firstSaleBonusPct > 0)
            InfoUI.Instance?.Show("초심자의\n행운 발동!");

        for (int i = 0; i < barCount; i++)
        {
            float targetHeight = maxRevenue > 0 ? ((float)revenuePerPeriod[i] / maxRevenue) * maxBarHeight : 0f;
            int endRevenue = cumulativeRevenue + revenuePerPeriod[i];

            var barObj = Instantiate(barPrefab, chartArea);
            var barImage = barObj.transform.Find("BarImage").GetComponent<RectTransform>();
            var valueLabel = barObj.transform.Find("ValueLabel").GetComponent<TMPro.TextMeshProUGUI>();
            var periodLabel = barObj.transform.Find("PeriodLabel").GetComponent<TMPro.TextMeshProUGUI>();
            periodLabel.text = $"{i + 1}주";

            // 이미 완료된 주차는 즉시 표시 (돈 지급 없음)
            if (i < completedWeeks)
            {
                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);
                valueLabel.text = $"{revenuePerPeriod[i]:N0}G";
                cumulativeRevenue = endRevenue;
                totalRevenueText.text = $"총 매출: {cumulativeRevenue:N0}G";
                continue;
            }

            // 미완료 주차 - 애니메이션
            valueLabel.text = "";
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, 0f);

            int startRevenue = cumulativeRevenue;

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
                int currentRevenue = Mathf.RoundToInt(Mathf.Lerp(startRevenue, endRevenue, t));
                totalRevenueText.text = $"총 매출: {currentRevenue:N0}G";

                yield return null;
            }

            if (barObj == null) yield break;
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);

            int weeklyRevenue = revenuePerPeriod[i];
            if (firstSaleBonusPct > 0)
                weeklyRevenue = Mathf.RoundToInt(weeklyRevenue * (1f + firstSaleBonusPct / 100f));
            int rank = CalcRank(weeklyRevenue);
            valueLabel.text = $"{revenuePerPeriod[i]:N0}G";
            if (rankText != null)
                rankText.text = rank > 0 ? $"{rank}위" : "순위권 밖";
            totalRevenueText.text = $"총 매출: {endRevenue:N0}G";

            MoneyManager.Instance.AddGold(weeklyRevenue);
            QuestManager.Instance?.UpdateProgress(QuestType.TotalRevenue, weeklyRevenue);

            _completedBarIndex = i + 1;
            SalesSaveManager.Instance?.SaveSales(
                _completedBarIndex, _cachedTotalRevenue, _cachedRevenuePerPeriod, _cachedQualityScore, _cachedScale, _cachedProjectName,
                _cachedScale, _cachedGenre, _cachedPlatform,
                _cachedPlanning, _cachedDevelop, _cachedArt, _cachedCreativity, _cachedBug
            );
            GameTimeManager.Instance?.SaveGameTime();
            ProjectSaveManager.Instance?.SaveProject();

            cumulativeRevenue = endRevenue;

            float gap = weekDuration * 0.3f;
            float gapElapsed = 0f;
            while (gapElapsed < gap)
            {
                if (GameTimeManager.Instance.IsRunning) gapElapsed += Time.deltaTime;
                yield return null;
            }
        }
        float endWait = 0f;
        while (endWait < 0.5f)
        {
            if (GameTimeManager.Instance.IsRunning) endWait += Time.deltaTime;
            yield return null;
        }

        // 마이그레이션: AlertUI → InfoUI (슬라이드 아웃 종료 후 OnSalesComplete 호출)
        if (InfoUI.Instance != null)
            InfoUI.Instance.Show("판매 완료!", () => OnSalesComplete(cumulativeRevenue));
        else
            OnSalesComplete(cumulativeRevenue); // InfoUI 없으면 즉시 진행 (안전망)
    }

    void OnSalesComplete(int cumulativeRevenue)
    {
        salesPanel.SetActive(false);

        if (_currentSalesProject != null)
        {
            Debug.Log($"업데이트: scale={_currentSalesProject.scale} genre={_currentSalesProject.genre} revenue={cumulativeRevenue}");
            CompletedProjectManager.Instance.UpdateSalesResult(_currentSalesProject, cumulativeRevenue);
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