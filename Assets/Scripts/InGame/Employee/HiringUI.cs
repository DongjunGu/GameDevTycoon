using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class HiringUI : MonoBehaviour
{
    public static HiringUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject tierPanel;       // 티어 선택 패널 (새로 추가)
    public GameObject hiringPanel;
    public GameObject confirmPanel;
    public GameObject loadingPanel;

    [Header("Tier Buttons")]
    [Tooltip("티어 버튼 GameObject 3개를 순서대로 인스펙터에 할당 (1단계/2단계/3단계). 2/3단계는 hire_tier2/3 해금 시 노출.")]
    public GameObject[] tierButtons;

    [Header("Refresh")]
    [Tooltip("후보 리스트(HiringPanel) 새로고침 버튼. 'hire_refresh' 해금 시 활성, 같은 세션 1회만 사용 가능.")]
    public Button refreshButton;
    [Tooltip("ConfirmHirePanel 새로고침 버튼. refreshButton 과 같은 1회 가드를 공유. 누르면 이력서 셔플 후 후보 재추첨(확정 화면 유지).")]
    public Button confirmRefreshButton;

    [Header("Slots")]
    public Transform slotParent;
    public GameObject employeeSlotPrefab;

    [Header("Confirm")]
    public TextMeshProUGUI confirmNameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI confirmRoleText;
    public TextMeshProUGUI confirmGradeText;
    public TextMeshProUGUI confirmTraitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI confirmPotentialText;
    public TextMeshProUGUI confirmDevelopText;
    public TextMeshProUGUI confirmPlanningText;
    public TextMeshProUGUI confirmArtText;
    public TextMeshProUGUI confirmCreativityText;
    public TextMeshProUGUI confirmSalaryText;
    public TextMeshProUGUI confirmSatisfactionText;
    public TextMeshProUGUI confirmHireCostText;
    [Header("Comparison")]
    public GameObject comparisonSection;        // 보유 직원 비교 섹션 (없으면 숨김)
    public TextMeshProUGUI ownedNameText;
    public TextMeshProUGUI ownedRoleText;
    public TextMeshProUGUI ownedGradeText;
    public TextMeshProUGUI ownedTraitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI ownedPotentialText;
    public TextMeshProUGUI ownedDevelopText;
    public TextMeshProUGUI ownedPlanningText;
    public TextMeshProUGUI ownedArtText;
    public TextMeshProUGUI ownedCreativityText;
    public TextMeshProUGUI ownedSalaryText;
    public TextMeshProUGUI ownedEnhancementText;
    public Button confirmButton;                // 채용하기 버튼
    public Button keepButton;                   // 유지하기 버튼

    [Header("Resume Browse (NewEmployeeSlot)")]
    [Tooltip("이력서(NewEmployeeSlot)에 부착된 페이지 넘김 컴포넌트")]
    public ResumeFlipper resumeFlipper;
    public Button prevCandidateButton;          // 왼쪽 화살표 (이전 후보)
    public Button nextCandidateButton;          // 오른쪽 화살표 (다음 후보)

    [Header("Resume Panels (EmployeeResumePanel x3 — 캐러셀 슬롯)")]
    [Tooltip("가운데(현재 후보) 패널 — ResumeFlipper 가 붙은 그 패널")]
    public EmployeeResumePanel resumeCenterPanel;
    [Tooltip("왼쪽(이전 후보) 패널")]
    public EmployeeResumePanel resumeLeftPanel;
    [Tooltip("오른쪽(다음 후보) 패널")]
    public EmployeeResumePanel resumeRightPanel;

    [Header("Settings")]
    public int candidateCount = 4;
    const int INTERVIEW_WEEKS = 3; // 채용 클릭 → 후보 리스트 공개까지 대기 주차

    private EmployeeData _selectedEmployee;
    private EmployeeData _conflictingOwned;     // 동일 masterEmployeeId 보유 직원
    private List<EmployeeData> _currentCandidates = new();
    private int _hireCost;
    private int _currentIndex = -1;                 // _currentCandidates 내 현재 표시 중인 후보
    private readonly List<int> _hireCosts = new();  // 후보별 채용 비용 캐시 — 화살표로 넘길 때 재추첨 방지

    private int  _currentTierIndex = -1;        // hire_refresh 재로드용 — 마지막 OnClickTier 의 티어
    private bool _refreshUsed      = false;     // 같은 세션 1회 가드
    private bool _candidateFlowActive = false;  // 채용 공개~리스트 종료 동안 시간 정지 유지 + ModalGate 점유

    // 티어 데이터 (강화 레벨 범위/가중치, 잠재력 확률은 EmployeeManager.PotentialWeightTable 참조)
    private static readonly (string label, int cost, int[] range, int[] weights)[] Tiers =
    {
        ("채용 1단계", 2000,  new[] { 0 }, new[] { 1 }),
        ("채용 2단계", 7000,  new[] { 0, 11 }, new[] { 1, 1 }),
        ("채용 3단계", 20000, new[] { 12, 14 }, new[] { 1, 1 }),
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnClickRefresh);
        if (confirmRefreshButton != null)
            confirmRefreshButton.onClick.AddListener(OnClickRefreshFromConfirm);
        if (prevCandidateButton != null)
            prevCandidateButton.onClick.AddListener(OnClickPrevCandidate);
        if (nextCandidateButton != null)
            nextCandidateButton.onClick.AddListener(OnClickNextCandidate);
    }

    // ── 티어 선택 패널 열기 ───────────────────
    public void OpenHiring()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        tierPanel.SetActive(true);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        RefreshTierButtonVisibility();
        RefreshTierPrices();
    }

    // 각 티어 버튼의 자식 "priceText"(TMP)에 가격 표시. hire_discount 해금 시 할인가(20% 감소) 반영.
    void RefreshTierPrices()
    {
        if (tierButtons == null) return;
        bool discounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        for (int i = 0; i < tierButtons.Length && i < Tiers.Length; i++)
        {
            if (tierButtons[i] == null) continue;
            var priceText = FindChildText(tierButtons[i].transform, "priceText");
            if (priceText == null) continue;
            int cost = discounted ? Mathf.RoundToInt(Tiers[i].cost * 0.8f) : Tiers[i].cost;
            priceText.text = $"{cost:N0}G";
        }
    }

    static TextMeshProUGUI FindChildText(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t.GetComponent<TextMeshProUGUI>();
        return null;
    }

    // 모든 티어 버튼을 항상 표시. 해금 안 된 티어(2/3단계)는 어둡게 표시하되 클릭은 가능
    // (클릭 시 OnClickTier 가 "해금되지 않았습니다" 안내). 1단계는 항상 해금.
    void RefreshTierButtonVisibility()
    {
        if (tierButtons == null) return;
        bool tier2Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier2");
        bool tier3Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier3");

        SetTierButtonState(0, true);
        SetTierButtonState(1, tier2Unlocked);
        SetTierButtonState(2, tier3Unlocked);
    }

    // 티어 버튼: 항상 활성 + 잠금 시 어둡게(alpha) 표시. 클릭은 항상 가능(잠금 안내는 OnClickTier).
    void SetTierButtonState(int index, bool unlocked)
    {
        if (tierButtons == null || index >= tierButtons.Length || tierButtons[index] == null) return;
        var go = tierButtons[index];
        go.SetActive(true);
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = unlocked ? 1f : 0.45f;  // 잠금 시 어둡게
        cg.interactable = true;            // 클릭 유지
        cg.blocksRaycasts = true;
    }

    // 티어 버튼 클릭 (0~2)
    public void OnClickTier(int tierIndex)
    {
        // 안전망 — 해금 안 된 티어가 다른 경로로 호출되는 케이스 차단
        if (tierIndex == 1 && (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_tier2")))
        {
            AlertUI.Instance.Show("채용 2단계가 해금되지 않았습니다.");
            return;
        }
        if (tierIndex == 2 && (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_tier3")))
        {
            AlertUI.Instance.Show("채용 3단계가 해금되지 않았습니다.");
            return;
        }

        if (EmployeeManager.Instance.ownedEmployees.Count >= StageManager.Instance.MaxEmployeeCount)
        {
            AlertUI.Instance.Show("최대 직원수 입니다.");
            return;
        }

        // 이미 진행 중인 모집공고가 있으면 차단
        if (EmployeeManager.Instance.HiringPendingTier >= 0)
        {
            AlertUI.Instance.Show("이미 모집공고가 등록되었습니다.");
            return;
        }

        // 비용 계산 (hire_discount 반영) — 클릭 즉시 차감
        bool discounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        int cost = discounted ? Mathf.RoundToInt(Tiers[tierIndex].cost * 0.8f) : Tiers[tierIndex].cost;
        if (!MoneyManager.Instance.CanAfford(cost))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }
        MoneyManager.Instance.SpendGold(cost);

        // pending 등록(INTERVIEW_WEEKS 주 후 리스트) + 즉시 저장 — 돈만 쓰고 리스트 못 받는 일 방지(복원 가능)
        EmployeeManager.Instance.SetHiringPending(tierIndex, INTERVIEW_WEEKS);
        GameTimeManager.Instance?.SaveGameTime();

        // 채용 UI 닫고 "면접 확정" 비서 안내 → 확인 시 시간 재개(OpenHiring 의 StopTime 해소). 이후 게임 진행하며 INTERVIEW_WEEKS 주 경과.
        tierPanel.SetActive(false);
        gameObject.SetActive(false);
        ShowSecretaryEvent("면접 확정 후 알려드리겠습니다.", () => GameTimeManager.Instance?.StartTime());
    }

    // 면접 대기 만료(EmployeeManager.OnWeekPassed) 시 호출 — "최종 리스트" 안내 후 후보 리스트 표시.
    public void RevealHiring(int tierIndex)
    {
        BeginCandidateFlow(); // 시간 정지 + 흐름 중 외부 시간재개 방어 가드(퇴사 ForceStartTime 등)
        ShowSecretaryEvent("최종 리스트 전달드리겠습니다.", () => OpenCandidateList(tierIndex));
    }

    // 채용 공개 흐름 시작 — 시간 정지 + "흐름 중 외부가 시간을 재개하면 즉시 재정지" 가드 구독.
    // (같은 주 퇴사 이벤트 확인의 ForceStartTime 등이 후보 리스트 도중 시간을 풀어버리는 것을 차단)
    void BeginCandidateFlow()
    {
        if (_candidateFlowActive) return;
        _candidateFlowActive = true;
        GameTimeManager.Instance?.StopTime();
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged += OnTimeChangedDuringFlow;
    }

    // 채용 공개 흐름 종료 — 가드 해제 + 시간 재개 + ModalGate 점유 해제. 모든 종료 경로(채용/유지/취소)에서 호출.
    void EndCandidateFlow()
    {
        if (!_candidateFlowActive) return;
        _candidateFlowActive = false;
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged -= OnTimeChangedDuringFlow;
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
    }

    // 흐름 도중 외부가 시간을 재개하면(StartTime/ForceStartTime) 즉시 다시 멈춤.
    void OnTimeChangedDuringFlow(bool stopped)
    {
        if (_candidateFlowActive && !stopped)
            GameTimeManager.Instance?.StopTime();
    }

    void OpenCandidateList(int tierIndex)
    {
        ModalGate.I.Register(this); // 후보 리스트 표시 동안 다른 모달(퇴사 등) 차단·큐잉 (닫힐 때 EndCandidateFlow 에서 해제)
        gameObject.SetActive(true);
        tierPanel.SetActive(false);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        _currentTierIndex = tierIndex;
        _refreshUsed = false; // 새 진입 시 새로고침 권리 부여
        LoadAndShowCandidates(tierIndex);
    }

    // 비서 초상화 RandomEventUI 안내(EventPanel). 확인 시 onConfirm. type=Recruit 라 시간 강제재개(ResumeFromEvent) 안 함.
    void ShowSecretaryEvent(string description, System.Action onConfirm)
    {
        if (RandomEventUI.Instance == null) { onConfirm?.Invoke(); return; }
        RandomEventUI.Instance.Show(new RandomEventData
        {
            type                    = RandomEventType.Recruit,
            title                   = "직원 채용",
            description             = description,
            portraitId              = "portrait_secretary",
            systemMessage           = "",     // 공백이지만
            keepSystemMessageActive = true,   // systemMessageText GameObject 는 활성 유지
            onApply                 = onConfirm,
        });
    }

    // OnClickTier / OnClickRefresh 공용 — 마지막 진입 티어 기준으로 후보 재로드 후 ShowCandidates.
    void LoadAndShowCandidates(int tierIndex)
    {
        var (label, baseCost, range, weights) = Tiers[tierIndex];

        _currentCandidates.Clear();
        int recruitBonus  = TraitEffectApplier.GetRecruitApplicantsBonus();
        int hiringPenalty = RandomEventManager.Instance?.HiringPenalty ?? 0;
        // 테크트리 '한 명 더!(hire_more)' — 후보 +1
        int hireMoreBonus = (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_more")) ? 1 : 0;
        int effectiveCount = Mathf.Max(1, candidateCount + recruitBonus + hireMoreBonus - hiringPenalty);

        EmployeeManager.Instance.LoadRandomCandidates(effectiveCount, tierIndex, candidates =>
        {
            foreach (var employee in candidates)
            {
                int enhLevel = RollWeighted(range, weights);
                ApplyEnhancementLevel(employee, enhLevel);
            }
            ShowCandidates(candidates);
        });
    }

    // 테크트리 '한 번 더!(hire_refresh)' — 같은 세션 1회 한정 후보 전체 재추첨. 확인창 없이 즉시 실행.
    public void OnClickRefresh()
    {
        if (_refreshUsed) return;
        if (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_refresh")) return;
        if (_currentTierIndex < 0) return;

        _refreshUsed = true;
        if (loadingPanel != null) loadingPanel.SetActive(true);
        LoadAndShowCandidates(_currentTierIndex);
    }

    // ConfirmHirePanel 의 새로고침 버튼 — refreshButton 과 같은 1회 가드(_refreshUsed)·해금(hire_refresh)을 공유.
    // HiringPanel 새로고침과 달리 슬롯 리스트로 돌아가지 않고 확정 화면을 유지하며 이력서 셔플 연출을 보여준다. 확인창 없이 즉시 실행.
    public void OnClickRefreshFromConfirm()
    {
        if (_refreshUsed) return;
        if (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_refresh")) return;
        if (_currentTierIndex < 0) return;

        _refreshUsed = true;
        RefreshCandidatesWithShuffle();
    }

    // 확정 화면에서 후보 재추첨 + 이력서 셔플. 데이터 교체는 셔플 가운데(스택된 순간)에 숨겨 자연스럽게 보이게 한다.
    void RefreshCandidatesWithShuffle()
    {
        var (label, baseCost, range, weights) = Tiers[_currentTierIndex];
        int recruitBonus  = TraitEffectApplier.GetRecruitApplicantsBonus();
        int hiringPenalty = RandomEventManager.Instance?.HiringPenalty ?? 0;
        int hireMoreBonus = (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_more")) ? 1 : 0;
        int effectiveCount = Mathf.Max(1, candidateCount + recruitBonus + hireMoreBonus - hiringPenalty);

        EmployeeManager.Instance.LoadRandomCandidates(effectiveCount, _currentTierIndex, candidates =>
        {
            foreach (var employee in candidates)
                ApplyEnhancementLevel(employee, RollWeighted(range, weights));

            SetCandidates(candidates);

            // 셔플 동안 좌/우 이력서도 함께 움직이도록, 후보가 多면 미리 활성화(없던 경우 대비).
            bool multi = candidates.Count > 1;
            if (resumeLeftPanel  != null) resumeLeftPanel.gameObject.SetActive(multi);
            if (resumeRightPanel != null) resumeRightPanel.gameObject.SetActive(multi);

            UpdateRefreshButton(); // _refreshUsed=true → 두 새로고침 버튼 모두 비활성

            void SwapToNewCandidates()
            {
                _currentIndex = 0;
                PopulateConfirm();
                UpdateArrowButtons();
            }

            if (resumeFlipper != null)
                resumeFlipper.Shuffle(onMidpoint: SwapToNewCandidates);
            else
                SwapToNewCandidates();
        });
    }

    void UpdateRefreshButton()
    {
        bool unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_refresh");
        bool usable = unlocked && !_refreshUsed;
        if (refreshButton != null)        refreshButton.interactable        = usable;
        if (confirmRefreshButton != null) confirmRefreshButton.interactable = usable;
    }

    // 가중치 랜덤 롤
    int RollWeighted(int[] range, int[] weights)
    {
        int total = 0;
        foreach (int w in weights) total += w;

        int roll = UnityEngine.Random.Range(0, total);
        int cum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cum += weights[i];
            if (roll < cum) return range[i];
        }
        return range[0];
    }

    void ApplyEnhancementLevel(EmployeeData employee, int targetLevel)
    {
        EmployeeManager.Instance.ApplyEnhancementExpected(employee, targetLevel);
    }

    // 후보 리스트 + 후보별 채용 비용 캐시 세팅 (슬롯 UI 갱신 없음 — ShowCandidates/RefreshCandidatesWithShuffle 공용).
    // 비용은 한 번만 추첨해 캐시 — 화살표로 이력서를 넘길 때마다 비용이 흔들리지 않게 고정.
    void SetCandidates(List<EmployeeData> candidates)
    {
        _currentCandidates = candidates;
        _hireCosts.Clear();
        foreach (var e in candidates)
        {
            int baseCost = EmployeeManager.GetExpectedEnhanceCost(e.enhancementLevel);
            _hireCosts.Add(Mathf.RoundToInt(baseCost * UnityEngine.Random.Range(0.8f, 1.2f)));
        }
    }

    void ShowCandidates(List<EmployeeData> candidates)
    {
        SetCandidates(candidates);

        if (loadingPanel != null) loadingPanel.SetActive(false);

        confirmPanel.SetActive(false);

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var employee in candidates)
        {
            var slot = Instantiate(employeeSlotPrefab, slotParent);
            slot.GetComponent<EmployeeSlotUI>().Setup(employee, OnSelectEmployee);
        }

        hiringPanel.SetActive(true);
        UpdateRefreshButton();
    }

    public void OnSelectEmployee(EmployeeData employee)
    {
        int idx = _currentCandidates.IndexOf(employee);
        _currentIndex = idx >= 0 ? idx : 0;

        // 패널을 먼저 활성화한 뒤 Populate — 비활성 상태에서 등급 셰머 코루틴 StartCoroutine 시 에러 방지.
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(true);
        PopulateConfirm();
        UpdateArrowButtons();
        UpdateRefreshButton();   // ConfirmHirePanel 새로고침 버튼 상태 반영
    }

    // 오른쪽 화살표 — 다음 후보 이력서로 넘김
    public void OnClickNextCandidate() => FlipTo(+1);
    // 왼쪽 화살표 — 이전 후보 이력서로 넘김
    public void OnClickPrevCandidate() => FlipTo(-1);

    // 현재 채용 리스트에서 dir(+1 다음 / -1 이전)만큼 이동(양끝 순환).
    // ResumeFlipper 가 있으면 페이지 넘김 연출 중간(엣지온)에 데이터를 교체한다.
    void FlipTo(int dir)
    {
        if (_currentCandidates == null || _currentCandidates.Count <= 1) return;
        if (resumeFlipper != null && resumeFlipper.IsFlipping) return;

        int next = (_currentIndex + dir + _currentCandidates.Count) % _currentCandidates.Count;

        if (resumeFlipper != null)
            resumeFlipper.Flip(dir, () =>
            {
                _currentIndex = next;
                PopulateConfirm();
            });
        else
        {
            _currentIndex = next;
            PopulateConfirm();
        }
    }

    // 후보가 2명 이상일 때만 화살표 + 좌/우 이력서 노출 (1명 이하면 가운데만).
    void UpdateArrowButtons()
    {
        bool multi = _currentCandidates != null && _currentCandidates.Count > 1;
        if (prevCandidateButton != null) prevCandidateButton.gameObject.SetActive(multi);
        if (nextCandidateButton != null) nextCandidateButton.gameObject.SetActive(multi);
        if (resumeLeftPanel  != null) resumeLeftPanel.gameObject.SetActive(multi);
        if (resumeRightPanel != null) resumeRightPanel.gameObject.SetActive(multi);
    }

    // 캐러셀 3슬롯을 _currentIndex 기준으로 채운다 — 좌=이전 / 가운데=현재 / 우=다음 (양끝 순환).
    // 미리 옆 패널에 이전/다음 후보를 띄워두므로, 넘기면 옆 패널이 그대로 가운데로 온다.
    // 넘김 종료 콜백(ResumeFlipper onSwap)·최초 선택(OnSelectEmployee)에서 호출.
    void PopulateConfirm()
    {
        if (_currentCandidates == null || _currentCandidates.Count == 0) return;
        int n  = _currentCandidates.Count;
        int ci = _currentIndex;
        var center = _currentCandidates[ci];

        _selectedEmployee = center;

        // 후보별 캐시 비용 사용(넘겨도 고정). 캐시 미스 시에만 즉석 추첨.
        // 테크트리 '채용 비용 할인(hire_discount)' 은 티어 진입 비용에만 적용 — 개별 직원 채용 비용에는 적용 안 함.
        _hireCost = (ci >= 0 && ci < _hireCosts.Count)
            ? _hireCosts[ci]
            : Mathf.RoundToInt(EmployeeManager.GetExpectedEnhanceCost(center.enhancementLevel) * UnityEngine.Random.Range(0.8f, 1.2f));

        // 좌:이전 / 중앙:현재 / 우:다음 후보 미리 표시
        if (resumeCenterPanel != null) resumeCenterPanel.Setup(center);
        if (resumeLeftPanel  != null) resumeLeftPanel.Setup(_currentCandidates[(ci - 1 + n) % n]);
        if (resumeRightPanel != null) resumeRightPanel.Setup(_currentCandidates[(ci + 1) % n]);

        if (confirmHireCostText != null)
            confirmHireCostText.text = _hireCost <= 0 ? "무료" : $"{_hireCost:N0}G";

        // 동일 직원 보유 여부 확인 (중앙 후보 기준, masterEmployeeId 공백이면 이름으로 대조)
        _conflictingOwned = EmployeeManager.Instance.ownedEmployees
            .Find(e => EmployeeSlotUI.IsSameEmployee(e, center));

        bool hasConflict = _conflictingOwned != null;
        // 일단 보유 직원 비교 패널(OwnedEmployeeSlot) 활성화 주석 처리 — EmployeeResumePanel 개편 중 (지우지 말 것, 추후 복구)
        // if (comparisonSection != null) comparisonSection.SetActive(hasConflict);
        if (keepButton != null) keepButton.gameObject.SetActive(hasConflict);

        if (hasConflict)
        {
            var owned = _conflictingOwned;
            if (ownedNameText != null)       ownedNameText.text       = owned.employeeName;
            if (ownedRoleText != null)       ownedRoleText.text       = owned.RoleToString();
            if (ownedGradeText != null)      ownedGradeText.text      = owned.GradeToString();
            CharacterTraitApplier.SetupTraitText(ownedTraitText, owned);
            CharacterUniqueEvents.SetupEventText(ownedTraitText, owned);
            if (ownedPotentialText != null)  ownedPotentialText.text  = owned.PotentialToString();
            if (ownedDevelopText != null)    ownedDevelopText.text    = owned.DevelopText();
            if (ownedPlanningText != null)   ownedPlanningText.text   = owned.PlanningText();
            if (ownedArtText != null)        ownedArtText.text        = owned.ArtText();
            if (ownedCreativityText != null) ownedCreativityText.text = owned.CreativityText();
            if (ownedSalaryText != null)     ownedSalaryText.text     = owned.SalaryText();
            if (ownedEnhancementText != null)ownedEnhancementText.text= $"+{owned.enhancementLevel}";
        }
    }

    // 채용 버튼 — 즉시 채용하지 않고 확인 다이얼로그(ConfirmUI)를 띄운다.
    public void OnClickConfirmHire()
    {
        if (_selectedEmployee == null) return;
        ConfirmUI.Instance.Show(
            $"{_selectedEmployee.employeeName}을(를) 채용하시겠습니까?",
            onConfirm: DoHire,
            onCancel: () => { },          // 아니오 — ConfirmUI 만 닫고 이력서 화면 유지
            confirmText: "네",
            cancelText: "아니오"
        );
    }

    // 실제 채용 처리 (ConfirmUI 에서 '네' 선택 시)
    void DoHire()
    {
        if (_selectedEmployee == null) return;
        if (_hireCost > 0 && !MoneyManager.Instance.CanAfford(_hireCost))
        {
            GameUIHelper.ShowLoanPrompt();
            return;
        }
        if (_hireCost > 0)
        {
            MoneyManager.Instance.SpendGold(_hireCost);
            Debug.Log($"[채용] {_selectedEmployee.employeeName} 채용 비용 차감: {_hireCost:N0}G (강화 +{_selectedEmployee.enhancementLevel})");
        }

        // 동일 직원이 있으면 해고 후 채용
        if (_conflictingOwned != null)
        {
            EmployeeManager.Instance.FireEmployee(_conflictingOwned, countAsExit: false);
            _conflictingOwned = null;
        }

        EndCandidateFlow();
        EmployeeManager.Instance.HireEmployee(_selectedEmployee);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);     // ConfirmHirePanel 비활성화

        DialogManager.Instance.Resume();
    }

    // 보유 직원 유지하기 (비교 패널에서)
    public void OnClickKeep()
    {
        EndCandidateFlow();
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
        tierPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        DialogManager.Instance.Resume();
    }

    public void OnClickBack()
    {
        ShowCandidates(_currentCandidates);
    }

    public void OnClickClose()
    {
        // 취소 확정 시에만 흐름 종료(시간 재개). "채용진행" 선택 시엔 리스트 유지 + 시간 정지 유지.
        ConfirmUI.Instance.Show(
            "채용을 취소하시겠습니까?",
            onConfirm: () =>
            {
                EndCandidateFlow();
                tierPanel.SetActive(false);
                hiringPanel.SetActive(false);
                confirmPanel.SetActive(false);
                if (loadingPanel != null) loadingPanel.SetActive(false);
                DialogManager.Instance.EndDialog(); //다이얼로그 멈춤
            },
            onCancel: () => { },
            confirmText: "채용취소",
            cancelText: "채용진행"
        );
    }
    public void OnClickTierPanelClose()
    {
        tierPanel.SetActive(false);
        GameTimeManager.Instance.StartTime();
    }
}