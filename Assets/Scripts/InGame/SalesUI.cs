using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class SalesUI : MonoBehaviour
{
    public static SalesUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject salesPanel;

    [Header("Fold Animation")]
    public GameObject salesPanelOpenBtn;     // 우측하단 펼치기 버튼 (판매중 & 패널 닫힘일 때만 활성)
    public float foldDuration = 0.3f;        // 접기 시간
    public float unfoldDuration = 0.35f;     // 펼치기 시간
    private RectTransform panelRT;
    private Vector3 _panelOpenLocalPos;
    private Vector3 _panelOpenScale;
    private bool _salesInProgress = false;

    [Header("Chart")]
    public RectTransform chartArea;   // HorizontalLayoutGroup 오브젝트
    public GameObject barPrefab;      // Bar 프리팹
    private float maxBarHeight;

    [Header("UI")]
    public TextMeshProUGUI totalRevenueText;
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
    // ShowInternal 이 (복원 경로 등에서) 중복 호출되면 이전 코루틴이 살아있는 채로 chartArea 를 비워버려
    // 그 코루틴이 들고 있던 barObj 참조가 null 이 되고, barObj==null 가드가 yield break 로 조용히 죽어버려
    // OnSalesComplete() 가 영영 호출되지 않는(패널이 안 닫히는) 버그가 있었음 — 새로 시작하기 전 이전 코루틴을
    // 명시적으로 정지시켜 "고아 코루틴"이 남지 않게 한다.
    private Coroutine _barsCoroutine;

    public void NotifyNewProjectStarted()
    {
        _newProjectStartedDuringSales = true;
        SalesSaveManager.Instance?.SaveNewProjectStarted();
    }
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        panelRT = salesPanel.GetComponent<RectTransform>();
        _panelOpenLocalPos = panelRT.localPosition;
        _panelOpenScale = panelRT.localScale;

        if (salesPanelOpenBtn != null)
        {
            var btn = salesPanelOpenBtn.GetComponent<Button>();
            if (btn != null) { btn.onClick.RemoveAllListeners(); btn.onClick.AddListener(OnClickOpenPanel); }
            salesPanelOpenBtn.SetActive(false);
        }

        salesPanel.SetActive(false);
    }

    // 판매중 && 패널 닫힘일 때만 OpenBtn 노출 (패널/버튼 상호 배타)
    void UpdateOpenBtn()
    {
        if (salesPanelOpenBtn != null)
            salesPanelOpenBtn.SetActive(_salesInProgress && !salesPanel.activeSelf);
    }

    Vector3 FoldTargetPos()
        => salesPanelOpenBtn != null ? salesPanelOpenBtn.transform.localPosition : _panelOpenLocalPos;

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
        // 복원 시점에 시간을 멈춘 차단 모달(AlertUI·새해 결제·랜덤이벤트 등 — 모두 ModalGate 에 Register)이
        // 떠 있으면 시간을 강제로 켜지 않는다. ForceStartTime 은 stopCount=0 으로 모든 정지를 무력화하므로
        // "알림 떠있는데 판매 그래프가 진행되는" 버그가 생긴다. (SalesUI 복원은 RestoreNextFrame 으로 1프레임 지연 →
        // 그 시점 ModalGate/시간정지 상태가 활성 모달을 정확히 반영). 모달이 닫혀 시간이 재개되면 판매도 이어서 진행.
        // 막는 모달이 없을 때만(잔여 복원 정지 해소 목적) 강제 재개.
        if (GameTimeManager.Instance != null && !ModalGate.I.IsBlocked)
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
        {
            if (child.name == "LineImage") continue; // 기준선 이미지는 재생성 대상 아님 — 유지
            Destroy(child.gameObject);
        }

        // 규모배율 1/2/4/8 — 대작은 직원 7명 이상이면 8, 그 외(6명 등)는 4
        int scaleMultiplier = scale switch
        {
            ProjectScale.Small  => 1,
            ProjectScale.Medium => 2,
            ProjectScale.Large  => (EmployeeManager.Instance != null && EmployeeManager.Instance.ownedEmployees.Count >= 7) ? 8 : 4,
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
                // 신의 축복(우기 전용 이벤트) 주사위 6: 다음 축복까지 매출 +10%.
                float godBlessingBonus = CharacterUniqueEvents.GetGodBlessingSalesBonus();
                // 장착 특성 'b6'(소형) / 'a5'(중형) / 's7'(대작) — 규모별 매출 보너스.
                float scaleSaleBonus   = scale switch
                {
                    ProjectScale.Small  => TraitEffectApplier.GetSmallScaleSaleBonus(),
                    ProjectScale.Medium => TraitEffectApplier.GetMediumScaleSaleBonus(),
                    ProjectScale.Large  => TraitEffectApplier.GetLargeScaleSaleBonus(),
                    _                   => 0f
                };
                // 장착 특성 's8'(perfectScoreSaleBonus) — 평론가 100점 시 매출 보너스 (테크트리 만점신화와 합산).
                float perfectTraitBonus = (CriticReviewUI.Instance != null && CriticReviewUI.Instance.LastCriticTotal == 100)
                    ? TraitEffectApplier.GetPerfectScoreSaleBonus() : 0f;
                // 매출 보너스는 합연산 — 유튜버 +5% + 장인정신 +10% + 만점 +15% + 오타쿠 +20% + 신의축복 +10% + 규모 +5% + 평론가만점 +5% (곱셈 아님).
                // rand 는 별개의 자연스러운 변동성이라 곱셈 유지.
                float bonusSum         = (youtuberBonus - 1f) + (craftsmanBonus - 1f) + perfectBonus + otakuSalesBonus + godBlessingBonus + scaleSaleBonus + perfectTraitBonus;
                float totalMultiplier  = Mathf.Max(0f, 1f + bonusSum);
                // 매출 = 규모배율 × 332.3 × (원천/100)^2.177 × Random(0.9~1.1)  (원천 = 최종 변환 점수 qualityScore)
                totalRevenue = Mathf.RoundToInt(
                    scaleMultiplier * 332.3f * Mathf.Pow(Mathf.Max(0f, qualityScore) / 100f, 2.177f) * rand * totalMultiplier
                );
                if (RandomEventManager.Instance != null)
                    RandomEventManager.Instance.YoutuberSalesBonus = 1.0f; // 적용 후 초기화
            }

            // 각 주차 분배에 ±5% 변동을 주되 합이 100% 되도록 재정규화 (총 매출은 유지)
            float[] jittered = new float[barCount];
            float jitterSum = 0f;
            for (int i = 0; i < barCount; i++)
            {
                jittered[i] = distribution[i] * UnityEngine.Random.Range(0.95f, 1.05f);
                jitterSum += jittered[i];
            }
            if (jitterSum <= 0f) jitterSum = 1f;

            revenuePerPeriod = new int[barCount];
            for (int i = 0; i < barCount; i++)
                revenuePerPeriod[i] = Mathf.RoundToInt(totalRevenue * (jittered[i] / jitterSum));

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
        totalRevenueText.text = $"{adjustedTotalRevenue:N0} G";

        // 폴드 잔재 복원 후 활성화
        panelRT.DOKill();
        panelRT.localPosition = _panelOpenLocalPos;
        panelRT.localScale = _panelOpenScale;
        salesPanel.SetActive(true);
        _salesInProgress = true;
        UpdateOpenBtn(); // 패널 열림 → OpenBtn 비활성
        if (_barsCoroutine != null) StopCoroutine(_barsCoroutine);
        _barsCoroutine = StartCoroutine(ShowBarsSequentially(revenuePerPeriod, maxRevenue, completedWeeks));
    }
    IEnumerator ShowBarsSequentially(int[] revenuePerPeriod, int maxRevenue, int completedWeeks = 0)
    {
        yield return null; // ← 한 프레임 대기 후 높이 계산
        maxBarHeight = chartArea.rect.height * 0.8f;

        // bar 너비/간격 — Large(9개)는 좁고 촘촘하게, Small/Medium은 넓고 여유있게.
        bool isLarge = barCount >= 9;
        float barWidth = isLarge ? 11f : 26f;
        var chartLayout = chartArea.GetComponent<HorizontalLayoutGroup>();
        if (chartLayout != null) chartLayout.spacing = isLarge ? 40f : 95f;

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
            var barRT = barObj.GetComponent<RectTransform>();
            barRT.sizeDelta = new Vector2(barWidth, barRT.sizeDelta.y);
            // ChartArea의 HorizontalLayoutGroup이 childControlWidth=true라 sizeDelta만으론 폭이 안 먹힘 —
            // min/preferred/flexible을 모두 고정값으로 명시하고, Bar 루트의 내부 VerticalLayoutGroup(우선순위 낮음)보다
            // 확실히 이기도록 layoutPriority도 높여서 폭을 고정.
            var barLayoutElement = barObj.GetComponent<LayoutElement>();
            if (barLayoutElement == null) barLayoutElement = barObj.AddComponent<LayoutElement>();
            barLayoutElement.minWidth       = barWidth;
            barLayoutElement.preferredWidth = barWidth;
            barLayoutElement.flexibleWidth  = 0f;
            barLayoutElement.layoutPriority = 10;
            LayoutRebuilder.ForceRebuildLayoutImmediate(chartArea); // 폭 반영을 다음 프레임까지 안 미루고 즉시 확정
            var barImage = barObj.transform.Find("BarImage").GetComponent<RectTransform>();
            
            var periodLabel = barObj.transform.Find("PeriodLabel").GetComponent<TMPro.TextMeshProUGUI>();
            periodLabel.text = $"{i + 1}주";

            // 이미 완료된 주차는 즉시 표시 (돈 지급 없음)
            if (i < completedWeeks)
            {
                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);
                
                cumulativeRevenue = endRevenue;
                totalRevenueText.text = $"{cumulativeRevenue:N0} G";
                UpdateBestRank(GetRank(revenuePerPeriod[i], i)); // 복원 시에도 최고순위 누적
                continue;
            }

            // 미완료 주차 - 애니메이션

            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, 0f);

            // 온보딩 튜토리얼 16-1 — 첫 프로젝트 한정, 1주차 bar 애니메이션이 막 시작될 때. 시간을 멈추지
            // 않고(대사 진행 중에도 bar가 계속 오르는 걸 보여줘야 함) 그냥 병렬로 fire-and-forget 실행.
            if (i == 0 && !OnboardingState.Tutorial16_1Done
                && CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0
                && TutorialController.Instance != null)
            {
                TutorialController.Instance.TriggerTutorial16_1();
            }

            int startRevenue = cumulativeRevenue;

            float weekDuration = 4f; // 규모 무관 4초/주 통일
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
                totalRevenueText.text = $"{currentRevenue:N0} G";

                yield return null;
            }

            if (barObj == null) yield break;
            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);

            int weeklyRevenue = revenuePerPeriod[i];
            if (firstSaleBonusPct > 0)
                weeklyRevenue = Mathf.RoundToInt(weeklyRevenue * (1f + firstSaleBonusPct / 100f));
            int rank = GetRank(weeklyRevenue, i);
            UpdateBestRank(rank);
            
            if (rankText != null)
                rankText.text = rank > 0 ? $"{rank}위" : "순위권 밖";
            totalRevenueText.text = $"{endRevenue:N0} G";

            MoneyManager.Instance.AddGold(weeklyRevenue);
            QuestManager.Instance?.UpdateProgress(QuestType.TotalRevenue, weeklyRevenue);

            _completedBarIndex = i + 1;
            // 마지막 주차면 isActive=false 로 저장 → 완료 상태가 "마지막 write" 가 되어 재접속 복원 race 제거.
            bool isLastWeek = _completedBarIndex >= barCount;
            SalesSaveManager.Instance?.SaveSales(
                _completedBarIndex, _cachedTotalRevenue, _cachedRevenuePerPeriod, _cachedQualityScore, _cachedScale, _cachedProjectName,
                _cachedScale, _cachedGenre, _cachedPlatform,
                _cachedPlanning, _cachedDevelop, _cachedArt, _cachedCreativity, _cachedBug,
                isActive: !isLastWeek
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
        // 16-1이 시간 안 멈추고 진행 중일 수 있음 — 판매가 먼저 끝나 패널이 닫히면 하이라이트/대사도 같이 정리.
        TutorialController.Instance?.CancelTutorial16_1IfRunning();

        _barsCoroutine = null;
        panelRT.DOKill();
        panelRT.localPosition = _panelOpenLocalPos;
        panelRT.localScale = _panelOpenScale;
        salesPanel.SetActive(false);
        _salesInProgress = false;
        UpdateOpenBtn(); // 판매 종료 → OpenBtn 비활성

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

        // 온보딩 튜토리얼 17-1 — 첫 판매(=완료된 프로젝트 정확히 1개)가 끝난 직후, 직원 강화 메뉴 유도.
        if (!OnboardingState.Tutorial17_1Done
            && CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 1
            && TutorialController.Instance != null)
        {
            TutorialController.Instance.StartCoroutine(TutorialController.Instance.PlayTutorial17_1());
        }
    }

    // 판매 기간 중 도달한 가장 높은(작은 숫자) 순위를 현재 프로젝트에 기록
    void UpdateBestRank(int rank)
    {
        if (rank <= 0 || _currentSalesProject == null) return;
        if (_currentSalesProject.bestRank == 0 || rank < _currentSalesProject.bestRank)
            _currentSalesProject.bestRank = rank;
    }

    // 튜토리얼(첫 프로젝트, 소형=3주) 한정 — 실제 매출과 무관하게 1주차→2주차→3주차 순위를 이 값으로 고정.
    static readonly int[] TutorialRankSequence = { 1, 5, 11 };

    // weekIndex(0-based) 기준 순위 — 튜토리얼이면 실제 매출 무시하고 TutorialRankSequence 그대로,
    // 아니면 기존처럼 CalcRank(weeklyRevenue)로 계산.
    int GetRank(int weeklyRevenue, int weekIndex)
    {
        if (CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 0
            && weekIndex >= 0 && weekIndex < TutorialRankSequence.Length)
            return TutorialRankSequence[weekIndex];
        return CalcRank(weeklyRevenue);
    }

    int CalcRank(int weeklyRevenue)
    {
        if (weeklyRevenue >= 430000) return 1;
        if (weeklyRevenue >= 380000) return 2;
        if (weeklyRevenue >= 320000) return 3;
        if (weeklyRevenue >= 270000) return 4;
        if (weeklyRevenue >= 225000) return 5;
        if (weeklyRevenue >= 175000) return 6;
        if (weeklyRevenue >= 130000) return 7;
        if (weeklyRevenue >= 95000)  return 8;
        if (weeklyRevenue >= 70000)  return 9;
        if (weeklyRevenue >= 53000)  return 10;
        // 11~30위: 53,000(11위) ~ 27,000(30위)
        if (weeklyRevenue >= 27000)
            return Mathf.Clamp(Mathf.RoundToInt(30f - ((weeklyRevenue - 27000f) / 26000f * 19f)), 11, 30);
        // 31~60위: 27,000(31위) ~ 12,000(60위)
        if (weeklyRevenue >= 12000)
            return Mathf.Clamp(Mathf.RoundToInt(60f - ((weeklyRevenue - 12000f) / 15000f * 29f)), 31, 60);
        // 61~100위: 12,000(61위) ~ 5,000(100위)
        if (weeklyRevenue >= 5000)
            return Mathf.Clamp(Mathf.RoundToInt(100f - ((weeklyRevenue - 5000f) / 7000f * 39f)), 61, 100);
        return 0; // 순위권 밖 (5,000 미만)
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

    // 확인(닫기) — 우측하단으로 압축되며 접힘
    public void OnClickClose()
    {
        panelRT.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(panelRT.DOScale(0.05f, foldDuration).SetEase(Ease.InBack));
        seq.Join(panelRT.DOLocalMove(FoldTargetPos(), foldDuration).SetEase(Ease.InBack));
        seq.OnComplete(() =>
        {
            salesPanel.SetActive(false);
            panelRT.localPosition = _panelOpenLocalPos;
            panelRT.localScale = _panelOpenScale;
            UpdateOpenBtn(); // 닫힘 → (판매중이면) OpenBtn 활성
        });
    }

    // 우측하단 버튼 — 압축이 풀리듯 촤라락 펼쳐짐
    public void OnClickOpenPanel()
    {
        panelRT.DOKill();
        panelRT.localPosition = FoldTargetPos();
        panelRT.localScale = Vector3.one * 0.05f;
        salesPanel.SetActive(true);
        UpdateOpenBtn(); // 열림 → OpenBtn 비활성

        Sequence seq = DOTween.Sequence();
        seq.Append(panelRT.DOScale(_panelOpenScale, unfoldDuration).SetEase(Ease.OutBack));
        seq.Join(panelRT.DOLocalMove(_panelOpenLocalPos, unfoldDuration).SetEase(Ease.OutBack));
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