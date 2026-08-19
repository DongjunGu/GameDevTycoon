using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    [Header("Chart - Rank Axis")]
    [Tooltip("툴팁 라벨용 폰트 — 미지정 시 TMP 기본 폰트 사용")]
    public TMPro.TMP_FontAsset chartFont;
    [Tooltip("순위점(RankDot) 스프라이트 — 12x12. 미지정 시 무늬 없는 단색 사각형으로 대체")]
    public Sprite rankDotSprite;
    // 순위점/연결선은 각 주차 Bar의 자식으로 붙는다 — chartArea의 HorizontalLayoutGroup이 매 주차 추가마다
    // 전체 행을 재정렬(가운데 정렬)해 기존 막대들의 X가 계속 바뀌는데, Bar의 자식으로 만들면 부모(Bar)가
    // 움직일 때 같이 따라 움직여서 별도 갱신 없이 항상 정렬 유지됨. 툴팁만 세션과 무관하게 1회 생성돼 유지.
    private RectTransform tooltipRT;
    private TextMeshProUGUI tooltipLine1;
    private TextMeshProUGUI tooltipLine2;
    private Transform _prevDot; // 직전 주차 순위점(다음 점과 이어줄 선의 시작점)

    [Header("UI")]
    public TextMeshProUGUI totalRevenueText;
    public TextMeshProUGUI rankText;
    [Tooltip("RevenuePanel/newrecordText — 과거 완료작(진행중 자기자신 제외) 최고 총매출 초과 시 표시, 기본 비활성")]
    public GameObject revenueNewRecordText;
    [Tooltip("RankPanel/newrecordText — 과거 완료작 최고 순위(bestRank, 낮을수록 좋음)보다 앞서면 표시, 기본 비활성")]
    public GameObject rankNewRecordText;
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
    // 온보딩 16-1 — 1주차 bar 애니메이션이 실제로 끝났는지(>=1) 튜토리얼 코루틴이 폴링할 수 있도록 공개.
    public int CompletedBarIndex => _completedBarIndex;
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
                isSequel         = ProjectSetupUI.SequelBaseProject != null,
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

        // 규모배율 1/2.9/8.3/13.8 (2026-08-14 재조정) — 대작은 직원 7명 이상이면 13.8, 그 외(6명 등)는 8.3
        float scaleMultiplier = scale switch
        {
            ProjectScale.Small  => 1f,
            ProjectScale.Medium => 2.9f,
            ProjectScale.Large  => (EmployeeManager.Instance != null && EmployeeManager.Instance.ownedEmployees.Count >= 7) ? 13.8f : 8.3f,
            _ => 1f
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
            // 튜토리얼(첫 프로젝트) 한정 — TutorialRankSequence(1/5/11위)와 짝을 맞춰 실제 공식 대신
            // 총 매출을 12,000G로 고정한다. 주차별 분배(jitter)는 아래 일반 로직 그대로 적용.
            else if (IsTutorialFirstSale())
            {
                totalRevenue = 12000;
                if (RandomEventManager.Instance != null)
                    RandomEventManager.Instance.YoutuberSalesBonus = 1.0f; // 적용 안 했지만 다음 판매에 새어나가지 않게 초기화
            }
            else
            {
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
                // 차기작(CompletedProjectsUI.NextProjectButton) — 원작 대비 기획/개발/아트/창의성 중 이번 작이 더
                // 높은 항목 수로 매출 보너스/패널티 결정: 0개 -25% / 1개 -10% / 2개 -5% / 3개 +10% / 4개 +25%.
                // 1회 소비(null로 되돌림) — 이 판매에만 적용되고 다음 프로젝트로 새어나가지 않게.
                float sequelBonus = 0f;
                if (ProjectSetupUI.SequelBaseProject != null)
                {
                    var baseProj = ProjectSetupUI.SequelBaseProject;
                    int higherCount = 0;
                    if (_cachedPlanning   > baseProj.planning)   higherCount++;
                    if (_cachedDevelop    > baseProj.develop)    higherCount++;
                    if (_cachedArt        > baseProj.art)        higherCount++;
                    if (_cachedCreativity > baseProj.creativity) higherCount++;
                    sequelBonus = higherCount switch
                    {
                        0 => -0.25f,
                        1 => -0.10f,
                        2 => -0.05f,
                        3 => 0.10f,
                        _ => 0.25f, // 4
                    };
                    Debug.Log($"[차기작] 원작 대비 상회 항목 {higherCount}/4 → 매출 보정 {sequelBonus * 100f:F0}%");
                }
                // 매출 보너스는 합연산 — 유튜버 +5% + 장인정신 +10% + 만점 +15% + 오타쿠 +20% + 신의축복 +10% + 규모 +5% + 평론가만점 +5% + 차기작 -25~+25% (곱셈 아님).
                float bonusSum         = (youtuberBonus - 1f) + (craftsmanBonus - 1f) + perfectBonus + otakuSalesBonus + godBlessingBonus + scaleSaleBonus + perfectTraitBonus + sequelBonus;
                float totalMultiplier  = Mathf.Max(0f, 1f + bonusSum);
                // 매출 = 규모배율 × 248 × (원천/100)^2  (원천 = 최종 변환 점수 qualityScore)
                // 2026-08-14 — 공식에서 Random(0.9~1.1) 삭제. 변동성은 이제 주차별 분배 지터(아래
                // jitterSum 클램프)가 대신 담당한다.
                totalRevenue = Mathf.RoundToInt(
                    scaleMultiplier * 248f * Mathf.Pow(Mathf.Max(0f, qualityScore) / 100f, 2f) * totalMultiplier
                );
                if (RandomEventManager.Instance != null)
                    RandomEventManager.Instance.YoutuberSalesBonus = 1.0f; // 적용 후 초기화
                ProjectSetupUI.SequelBaseProject = null; // 이번 매출 계산에 1회 소비 — 다음 프로젝트로 안 새어나가게 초기화
            }

            // 2026-08-14 — 각 주차 분배에 ±5% 변동을 주되, 예전처럼 합을 정확히 100%로 재정규화하지 않는다.
            // 공식에서 삭제한 Random(0.9~1.1) 총매출 변동성을, 주차별 지터의 합(jitterSum, distribution 합이
            // 1이므로 보통 1.0 근방) 자체가 대신하게 함 — 단 [0.9, 1.1] 밖으로 벗어나지 않게 클램프.
            // 각 주차의 "상대적" 비중(jittered[i]/jitterSum)은 그대로 유지해 주차 간 자연스러운 굴곡은 보존.
            float[] jittered = new float[barCount];
            float jitterSum = 0f;
            for (int i = 0; i < barCount; i++)
            {
                jittered[i] = distribution[i] * UnityEngine.Random.Range(0.95f, 1.05f);
                jitterSum += jittered[i];
            }
            if (jitterSum <= 0f) jitterSum = 1f;
            float clampedJitterSum = Mathf.Clamp(jitterSum, 0.9f, 1.1f);

            revenuePerPeriod = new int[barCount];
            for (int i = 0; i < barCount; i++)
                revenuePerPeriod[i] = Mathf.RoundToInt(totalRevenue * clampedJitterSum * (jittered[i] / jitterSum));

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

        // ⚠️ 패널이 열리자마자 최종 총 매출을 먼저 찍어버리면 안 됨 — bar가 하나도 안 오른 상태에서
        // 유저가 결과를 미리 봐버리는 스포일러. 초기엔 비워두고, ShowBarsSequentially가 완료 주차는
        // 즉시/미완료 주차는 bar가 오르는 만큼 값을 채워나간다(1프레임도 안 지나 정상 갱신됨).
        totalRevenueText.text = "";
        if (revenueNewRecordText != null) revenueNewRecordText.SetActive(false);
        if (rankNewRecordText != null)    rankNewRecordText.SetActive(false);

        // 폴드 잔재 복원 후 활성화
        panelRT.DOKill();
        panelRT.localPosition = _panelOpenLocalPos;
        panelRT.localScale = _panelOpenScale;
        salesPanel.SetActive(true);
        _salesInProgress = true;
        UpdateOpenBtn(); // 패널 열림 → OpenBtn 비활성
        if (_barsCoroutine != null) StopCoroutine(_barsCoroutine);
        _barsCoroutine = StartCoroutine(ShowBarsSequentially(revenuePerPeriod, completedWeeks));
    }
    IEnumerator ShowBarsSequentially(int[] revenuePerPeriod, int completedWeeks = 0)
    {
        yield return null; // ← 한 프레임 대기 후 높이 계산
        maxBarHeight = chartArea.rect.height * 0.8f;

        // 순위-매출 이중축(2026-08-13) — 1주차 순위(1~55위로 clamp)의 축 높이를 "100% 기준선"으로 삼고,
        // 이후 매출은 그 기준선 대비 비율로 막대 높이를 정한다(더 이상 이 세션 내 최댓값 상대비교 아님).
        EnsureTooltip();

        float week1Revenue = revenuePerPeriod.Length > 0 ? revenuePerPeriod[0] : 0f;
        int rank1 = GetRank(revenuePerPeriod.Length > 0 ? revenuePerPeriod[0] : 0, 0);
        int clampedRank1 = Mathf.Clamp(rank1 <= 0 ? 100 : rank1, 1, 55);
        float baselineHeight = (1f - AxisPositionFromTop01(clampedRank1)) * maxBarHeight;
        _prevDot = null; // 순위선 연결 상태 초기화(막대는 매 세션 재생성되므로 점도 함께 새로 생김)

        // bar 너비/간격 — 규모별로 관리(너비는 전부 26, 간격만 개수에 맞춰 좁아짐).
        float barWidth = 26f;
        float spacing = _cachedScale switch
        {
            ProjectScale.Small  => 95f,
            ProjectScale.Medium => 50f,
            ProjectScale.Large  => 28f,
            _ => 95f
        };
        var chartLayout = chartArea.GetComponent<HorizontalLayoutGroup>();
        if (chartLayout != null) chartLayout.spacing = spacing;

        int cumulativeRevenue = 0;

        // 초심자의 행운 — 한 런 중 첫 SalesUI 세션에만 +pct% 가산 (이 세션 내 모든 주차에 일관 적용)
        int firstSaleBonusPct = TraitEffectApplier.ConsumeFirstSaleBonusPct();
        if (firstSaleBonusPct > 0)
            InfoUI.Instance?.Show("초심자의\n행운 발동!");

        for (int i = 0; i < barCount; i++)
        {
            float targetHeight = week1Revenue > 0
                ? Mathf.Clamp(baselineHeight * (revenuePerPeriod[i] / week1Revenue), 0f, maxBarHeight)
                : 0f;
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
            periodLabel.text = $"{i + 1}";

            // 이미 완료된 주차는 즉시 표시 (돈 지급 없음)
            if (i < completedWeeks)
            {
                barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, targetHeight);

                cumulativeRevenue = endRevenue;
                totalRevenueText.text = $"{cumulativeRevenue:N0} G";
                int restoredRank = GetRank(revenuePerPeriod[i], i);
                UpdateBestRank(restoredRank); // 복원 시에도 최고순위 누적
                UpdateRecordBadges(cumulativeRevenue);

                float restoredRatio = week1Revenue > 0 ? revenuePerPeriod[i] / week1Revenue : 0f;
                // barImage 높이를 막 바꾼 직후라 Bar의 레이아웃(자기 높이 보고)이 아직 재계산 전(다음 프레임에
                // 지연 처리됨) 상태 — 이 시점에 점의 월드좌표를 읽으면 갱신 전 낡은 높이 기준으로 계산돼버려
                // 강제로 즉시 재계산시킨 뒤에 점을 찍는다.
                LayoutRebuilder.ForceRebuildLayoutImmediate(chartArea);
                PlaceRankPoint(barImage, restoredRank);
                AttachTooltip(barImage.gameObject,
                    $"매출 {Mathf.RoundToInt(restoredRatio * 100f)}% ({revenuePerPeriod[i]:N0}G)",
                    restoredRank > 0 ? $"{restoredRank}위" : "차트 밖");
                continue;
            }

            // 미완료 주차 - 애니메이션

            barImage.sizeDelta = new Vector2(barImage.sizeDelta.x, 0f);

            // 온보딩 튜토리얼 16-1 — 첫 프로젝트 한정, 1주차 bar 애니메이션이 막 시작될 때 병렬로
            // fire-and-forget 실행. TutorialController.PlayTutorial16_1()이 자체적으로 게임 시간을
            // 멈추므로(BeginDimTimeStop), 아래 루프들의 GameTimeManager.IsRunning 체크 덕분에 대사가
            // 떠 있는 동안엔 bar 애니메이션/판매 완료가 자동으로 같이 멈춘다.
            if (i == 0 && !OnboardingState.Tutorial16_1Done
                && IsTutorialFirstSale()
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
            UpdateRecordBadges(endRevenue);

            float weekRatio = week1Revenue > 0 ? revenuePerPeriod[i] / week1Revenue : 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(chartArea); // 위와 동일 — 방금 바뀐 높이를 즉시 반영시킨 뒤 점을 찍음
            PlaceRankPoint(barImage, rank);
            AttachTooltip(barImage.gameObject,
                $"매출 {Mathf.RoundToInt(weekRatio * 100f)}% ({revenuePerPeriod[i]:N0}G)",
                rank > 0 ? $"{rank}위" : "차트 밖");

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

        // 튜토리얼 퀘스트1(첫 번째 게임 개발하기) — 첫 판매(=완료된 프로젝트 정확히 1개)가 끝나면 완료
        // 처리. isMainQuest=1이라 완료 즉시 UnlockChainedMainQuests가 튜토리얼 퀘스트2를 자동 공개한다.
        if (CompletedProjectManager.Instance != null && CompletedProjectManager.Instance.completedProjects.Count == 1)
            QuestManager.Instance?.UpdateProgress(QuestType.CompleteFirstProject, 1);

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

    // 과거 완료작(진행중인 자기 자신은 totalRevenue=0이라 자동 제외) 대비 신기록 배지 갱신.
    // 매출은 누적값이 과거 최고 총매출을 넘으면, 순위는 이번 판매에서 도달한 최고순위(bestRank, 낮을수록
    // 좋음)가 과거 최고 순위보다 앞서면(또는 과거에 순위권 진입 기록 자체가 없으면) 표시 — 한 번 켜지면
    // 두 값 모두 단조 개선만 되므로 다시 꺼지지 않는다.
    void UpdateRecordBadges(int cumulativeRevenue)
    {
        if (CompletedProjectManager.Instance == null) return;

        if (revenueNewRecordText != null)
        {
            int bestPastRevenue = 0;
            foreach (var p in CompletedProjectManager.Instance.completedProjects)
                if (p.totalRevenue > bestPastRevenue) bestPastRevenue = p.totalRevenue;
            revenueNewRecordText.SetActive(cumulativeRevenue > bestPastRevenue);
        }

        if (rankNewRecordText != null && _currentSalesProject != null && _currentSalesProject.bestRank > 0)
        {
            int bestPastRank = 0; // 0 = 비교 대상 없음(과거작 없음/전부 순위권 밖) → 이번이 곧 신기록
            foreach (var p in CompletedProjectManager.Instance.completedProjects)
            {
                if (p.totalRevenue <= 0 || p.bestRank <= 0) continue; // 자기 자신/순위권 밖 기록 제외
                if (bestPastRank == 0 || p.bestRank < bestPastRank) bestPastRank = p.bestRank;
            }
            bool isRecord = bestPastRank == 0 || _currentSalesProject.bestRank < bestPastRank;
            rankNewRecordText.SetActive(isRecord);
        }
    }

    // 튜토리얼(첫 프로젝트, 소형=3주) 한정 — 실제 매출과 무관하게 1주차→2주차→3주차 순위를 이 값으로 고정.
    static readonly int[] TutorialRankSequence = { 1, 5, 11 };

    // ⚠️ completedProjects.Count==0 을 그대로 쓰면 안 됨 — 이번 판매 자신도
    // CompletedProjectManager.SaveCompletedProject 의 비동기 Insert 콜백이 도착하는 순간(보통 애니메이션이
    // 몇 주차 진행되기도 전, 네트워크 왕복 정도의 시간)부터 totalRevenue=0인 채로 이미 completedProjects에
    // 들어가 있다. 그래서 1주차 총매출(12,000G 고정)은 SaveCompletedProject 호출 직후 같은 프레임에 판정돼
    // 정상 동작하지만, 순위(GetRank)는 애니메이션 도중(수 초 뒤, 이미 콜백이 도착한 뒤) 판정되면서
    // Count==0이 깨져 매주 실제 공식(CalcRank)으로 새 버렸다 — "총 매출은 고정되는데 순위는 안 먹힘" 버그.
    // 실제로 판매까지 끝나 매출이 찍힌(totalRevenue>0) 프로젝트만 세고 진행 중인 자기 자신은 항상 제외한다.
    // 디버그 전용 — true면 실제로 첫 판매여도 튜토리얼 고정값(매출 12,000G/순위 1·5·11) 대신 실제 공식을
    // 그대로 태운다. 16-1 테스트에서 높은 매출/순위대의 차트 모습을 확인하고 싶을 때 CharacterEventTester가
    // 잠깐 켰다가 테스트 종료 후 원복.
    public static bool DebugForceRealFormula = false;

    bool IsTutorialFirstSale()
    {
        if (DebugForceRealFormula) return false;
        // 튜토리얼이 이미 끝났으면(정상 완주는 물론, 디버그 "튜토리얼 완전해제"로 스킵한 경우 포함) 첫 판매여도
        // 고정값을 적용하지 않는다 — 그렇지 않으면 completedProjects 카운트만 보므로, 메인메뉴를 나갔다 오거나
        // 튜토리얼을 벗어난 뒤에도 "아직 한 번도 안 팔았다"는 이유만으로 계속 튜토리얼 고정값이 적용됐다.
        if (TutorialController.IsFullyDone()) return false;
        return CompletedProjectManager.Instance != null
            && CompletedProjectManager.Instance.completedProjects.FindAll(p => p.totalRevenue > 0).Count == 0;
    }

    // weekIndex(0-based) 기준 순위 — 튜토리얼이면 실제 매출 무시하고 TutorialRankSequence 그대로,
    // 아니면 기존처럼 CalcRank(weeklyRevenue)로 계산.
    int GetRank(int weeklyRevenue, int weekIndex)
    {
        if (IsTutorialFirstSale() && weekIndex >= 0 && weekIndex < TutorialRankSequence.Length)
            return TutorialRankSequence[weekIndex];
        return CalcRank(weeklyRevenue);
    }

    int CalcRank(int weeklyRevenue)
    {
        if (weeklyRevenue >= 500000) return 1;
        if (weeklyRevenue >= 400000) return 2;
        if (weeklyRevenue >= 320000) return 3;
        if (weeklyRevenue >= 256000) return 4;
        if (weeklyRevenue >= 205000) return 5;
        if (weeklyRevenue >= 162000) return 6;
        if (weeklyRevenue >= 129000) return 7;
        if (weeklyRevenue >= 102000) return 8;
        if (weeklyRevenue >= 81000)  return 9;
        if (weeklyRevenue >= 64000)  return 10;
        // 11~30위: 64,000(11위) ~ 22,000(30위), r = 10 + (64,000-revenue)/2,100
        if (weeklyRevenue >= 22000)
            return Mathf.Clamp(Mathf.RoundToInt(10f + (64000f - weeklyRevenue) / 2100f), 11, 30);
        // 31~60위: 22,000(31위) ~ 8,500(60위), r = 30 + (22,000-revenue)/450
        if (weeklyRevenue >= 8500)
            return Mathf.Clamp(Mathf.RoundToInt(30f + (22000f - weeklyRevenue) / 450f), 31, 60);
        // 61~100위: 8,500(61위) ~ 2,100(100위), r = 60 + (8,500-revenue)/160
        if (weeklyRevenue >= 2100)
            return Mathf.Clamp(Mathf.RoundToInt(60f + (8500f - weeklyRevenue) / 160f), 61, 100);
        return 0; // 순위권 밖 (2,100 미만)
    }

    // 규모별 주차 분배 (2026-08-14 재조정 — 대작 9주차→8주차로 축소)
    float[] CalcDistribution(ProjectScale scale)
    {
        return scale switch
        {
            ProjectScale.Small  => new float[] { 0.50f, 0.30f, 0.20f },
            ProjectScale.Medium => new float[] { 0.31f, 0.26f, 0.15f, 0.12f, 0.09f, 0.07f },
            ProjectScale.Large  => new float[] { 0.24f, 0.18f, 0.15f, 0.13f, 0.11f, 0.08f, 0.07f, 0.04f },
            _ => new float[] { 0.50f, 0.30f, 0.20f }
        };
    }

    // ================= 순위-매출 이중축 차트 (2026-08-13) =================
    // 세로축 전체를 순위 하나로 표현: 위쪽 1/3=1~10위 균등, 아래쪽 2/3=10~100위 균등.
    // rank(1~100) → 0(맨 위,1위)~1(맨 아래,100위) 위치로 변환.
    float AxisPositionFromTop01(int rank)
    {
        int r = Mathf.Clamp(rank, 1, 100);
        if (r <= 10) return (r - 1) / 9f * (1f / 3f);
        return (1f / 3f) + (r - 10) / 90f * (2f / 3f);
    }

    // 그 주차의 실제 순위를 BarImage 위에 점으로 찍고, 직전 점과 이어 선을 그린다(순위권 밖=0위는 100위 취급, 붉은색).
    // 점/선을 Bar 루트가 아니라 "BarImage"(실제 막대 이미지)의 자식으로 붙이는 게 핵심.
    // Bar 루트는 VerticalLayoutGroup으로 BarImage+PeriodLabel(주차 라벨, 높이 30)을 4px 간격으로 쌓기 때문에
    // Bar 루트의 바닥은 PeriodLabel의 바닥이지 BarImage의 바닥이 아니다 — Bar 루트 기준으로 찍으면 점이
    // PeriodLabel 쪽으로 처박힌다. BarImage 자체의 바닥-중앙을 앵커로 잡으면 라벨 높이/간격을 몰라도 항상
    // 막대가 실제로 시작하는 지점 기준으로 찍힌다.
    // ChartArea의 HorizontalLayoutGroup은 막대가 하나씩 추가될 때마다 전체 행을 다시 가운데 정렬해 기존
    // 막대들의 X가 계속 밀리는데, BarImage의 자식으로 만들면 부모가 밀릴 때 점도 같이 밀려서 별도 갱신 로직
    // 없이 항상 자기 막대 위에 붙어 있는다. 이전 주차와의 상대 간격은 재정렬로 변하지 않으므로, 연결선도
    // "다음 막대의 자식"으로 한 번만 계산해 두면 이후 몇 번을 더 재정렬해도 계속 정확하다.
    void PlaceRankPoint(RectTransform barImage, int rank)
    {
        bool offChart = rank <= 0;
        int axisRank = offChart ? 100 : rank;

        Transform newDot = CreateRankDot(barImage, axisRank, offChart);

        if (_prevDot != null)
            CreateLineSegment(barImage, _prevDot.position, newDot.position);

        _prevDot = newDot;
    }

    // BarImage의 바닥-중앙(0.5, 0)을 앵커로 삼아, 그 지점에서 축 높이만큼 위로 띄운 위치에 점을 찍는다.
    // BarImage는 childControlHeight=false라 자기 sizeDelta.y가 얼마든 바닥 앵커 자체는 흔들리지 않는다
    // (Bar 루트의 VerticalLayoutGroup이 매 프레임 그 바닥을 다시 고정시켜줌).
    Transform CreateRankDot(RectTransform parentBar, int axisRank, bool offChart)
    {
        float localY = (1f - AxisPositionFromTop01(axisRank)) * maxBarHeight;
        var go = new GameObject("RankDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parentBar, false);
        // Bar 루트에 VerticalLayoutGroup(BarImage/PeriodLabel 배치용)이 붙어 있어, 이 플래그 없이 자식으로
        // 넣으면 그 레이아웃 그룹이 점을 세 번째 행으로 취급해 위치/폭을 멋대로 덮어써 버린다 — 반드시 무시시킴.
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(12f, 12f);
        rt.anchoredPosition = new Vector2(0f, localY);
        var img = go.GetComponent<Image>();
        if (rankDotSprite != null) img.sprite = rankDotSprite;
        img.color = Color.white;
        img.raycastTarget = false;
        return go.transform;
    }

    // 직전 점(다른 Bar의 자식일 수 있음)에서 이번 점까지, 이번 Bar를 부모로 삼아 선을 하나 만든다.
    // 두 월드좌표를 이번 Bar의 로컬좌표로 변환해 한 번만 굽는다 — 이후 레이아웃 재정렬은 모든 막대를
    // 동일하게 밀 뿐 막대 간 상대 간격은 바꾸지 않으므로, 이 상대 위치는 계속 유효하다.
    void CreateLineSegment(RectTransform parentBar, Vector3 fromWorld, Vector3 toWorld)
    {
        var go = new GameObject("RankSeg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parentBar, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true; // 위와 동일한 이유로 Bar의 VerticalLayoutGroup 무시
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);

        // InverseTransformPoint는 Bar의 피벗(중심) 기준 좌표를 주는데, 이 오브젝트의 앵커는 (0.5,0)(Bar 바닥-중앙)이라
        // 기준점이 다르다 — Bar 피벗 기준 Y에서 rect.y(=바닥 오프셋, 즉 -높이/2)를 빼줘야 앵커 기준 Y로 정확히 변환된다.
        // X는 피벗.x와 앵커.x가 둘 다 0.5로 같아서 보정이 필요 없다.
        Vector2 rawA = parentBar.InverseTransformPoint(fromWorld);
        Vector2 rawB = parentBar.InverseTransformPoint(toWorld);
        Vector2 a = new Vector2(rawA.x, rawA.y - parentBar.rect.y);
        Vector2 b = new Vector2(rawB.x, rawB.y - parentBar.rect.y);
        Vector2 diff = b - a;
        rt.sizeDelta = new Vector2(diff.magnitude, 3f);
        rt.anchoredPosition = a;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.5f, 0.55f, 0.9f);
        img.raycastTarget = false;
    }

    // ---- 툴팁: 막대="매출 ○○% (○○○G)" — 항상 원래 단위로 환산해 표시 ----
    void EnsureTooltip()
    {
        if (tooltipRT != null) return;
        var go = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(chartArea.parent, false); // ChartPanel 하위 — ChartArea의 HorizontalLayoutGroup 영향 안 받음
        tooltipRT = go.GetComponent<RectTransform>();
        tooltipRT.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRT.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRT.pivot = new Vector2(0f, 0f);
        tooltipRT.sizeDelta = new Vector2(160f, 46f);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.1f, 0.1f, 0.85f);
        bg.raycastTarget = false;

        tooltipLine1 = CreateTooltipLabel(tooltipRT, "line1", 25f, new Color(0.86f, 0.66f, 0.32f, 1f));
        tooltipLine2 = CreateTooltipLabel(tooltipRT, "line2", 5f, new Color(0.4f, 0.85f, 0.8f, 1f));

        go.SetActive(false);
    }

    TextMeshProUGUI CreateTooltipLabel(Transform parent, string name, float bottomOffset, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-14f, 20f);
        rt.anchoredPosition = new Vector2(0f, bottomOffset);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (chartFont != null) label.font = chartFont;
        label.fontSize = 15f;
        label.alignment = TextAlignmentOptions.Left;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    void AttachTooltip(GameObject target, string line1, string line2)
    {
        var trigger = target.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => ShowTooltip(target.GetComponent<RectTransform>(), line1, line2));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exit);
    }

    void ShowTooltip(RectTransform anchorTarget, string line1, string line2)
    {
        if (tooltipRT == null) return;
        Vector2 local = ((RectTransform)chartArea.parent).InverseTransformPoint(anchorTarget.position);
        tooltipRT.anchoredPosition = local + new Vector2(-tooltipRT.sizeDelta.x / 2f, 14f);
        tooltipLine1.text = line1;
        tooltipLine2.text = line2;
        tooltipRT.gameObject.SetActive(true);
        tooltipRT.SetAsLastSibling();
    }

    void HideTooltip()
    {
        if (tooltipRT != null) tooltipRT.gameObject.SetActive(false);
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