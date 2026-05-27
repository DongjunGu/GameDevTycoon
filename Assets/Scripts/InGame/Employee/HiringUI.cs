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
    [Tooltip("후보 리스트 새로고침 버튼. 'hire_refresh' 해금 시 활성, 같은 세션 1회만 사용 가능.")]
    public Button refreshButton;

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

    [Header("Settings")]
    public int candidateCount = 4;
    const int INTERVIEW_WEEKS = 3; // 채용 클릭 → 후보 리스트 공개까지 대기 주차

    private EmployeeData _selectedEmployee;
    private EmployeeData _conflictingOwned;     // 동일 masterEmployeeId 보유 직원
    private List<EmployeeData> _currentCandidates = new();
    private int _hireCost;

    private int  _currentTierIndex = -1;        // hire_refresh 재로드용 — 마지막 OnClickTier 의 티어
    private bool _refreshUsed      = false;     // 같은 세션 1회 가드

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
        GameTimeManager.Instance?.StopTime(); // 리스트 표시 동안 정지(후보 선택 완료/취소 시 StartTime 으로 해소)
        ShowSecretaryEvent("최종 리스트 전달드리겠습니다.", () => OpenCandidateList(tierIndex));
    }

    void OpenCandidateList(int tierIndex)
    {
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

    // 테크트리 '한 번 더!(hire_refresh)' — 같은 세션 1회 한정 후보 전체 재추첨.
    // ConfirmUI 로 확인받은 뒤 진행.
    public void OnClickRefresh()
    {
        if (_refreshUsed) return;
        if (TechTreeManager.Instance == null || !TechTreeManager.Instance.IsUnlocked("hire_refresh")) return;
        if (_currentTierIndex < 0) return;

        ConfirmUI.Instance.Show(
            "후보 리스트를 새로고침 하시겠습니까?\n(1회만 가능합니다)",
            onConfirm: () =>
            {
                if (_refreshUsed) return; // ConfirmUI 대기 중 상태 변동 안전망
                _refreshUsed = true;
                if (loadingPanel != null) loadingPanel.SetActive(true);
                LoadAndShowCandidates(_currentTierIndex);
            },
            onCancel: () => { },
            confirmText: "예",
            cancelText: "아니오"
        );
    }

    void UpdateRefreshButton()
    {
        if (refreshButton == null) return;
        bool unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_refresh");
        refreshButton.interactable = unlocked && !_refreshUsed;
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

    void ShowCandidates(List<EmployeeData> candidates)
    {
        _currentCandidates = candidates;
        if (loadingPanel != null) loadingPanel.SetActive(false);

        confirmPanel.SetActive(false);

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var employee in candidates)
        {
            var slot = Instantiate(employeeSlotPrefab, slotParent);
            slot.GetComponent<EmployeeSlotUI>().Setup(employee, this);
        }

        hiringPanel.SetActive(true);
        UpdateRefreshButton();
    }

    public void OnSelectEmployee(EmployeeData employee)
    {
        _selectedEmployee = employee;

        int baseHireCost = EmployeeManager.GetExpectedEnhanceCost(employee.enhancementLevel);
        _hireCost = Mathf.RoundToInt(baseHireCost * UnityEngine.Random.Range(0.8f, 1.2f));
        // 테크트리 '채용 비용 할인(hire_discount)' 은 후보 목록을 뽑는 티어 진입 비용에만 적용 — 개별 직원 채용 비용에는 적용 안 함.

        confirmNameText.text = employee.employeeName;
        confirmRoleText.text = employee.RoleToString();
        confirmGradeText.text = employee.GradeToString();
        CharacterTraitApplier.SetupTraitText(confirmTraitText, employee);
        CharacterUniqueEvents.SetupEventText(confirmTraitText, employee);
        confirmPotentialText.text = employee.PotentialToString();
        confirmDevelopText.text = employee.DevelopDisplayText();
        confirmPlanningText.text = employee.PlanningDisplayText();
        confirmArtText.text = employee.ArtDisplayText();
        confirmCreativityText.text = employee.CreativityText();
        confirmSalaryText.text = employee.SalaryRangeText();
        enhancementText.text = $"+{employee.enhancementLevel}";
        if (confirmHireCostText != null)
            confirmHireCostText.text = _hireCost <= 0 ? "무료" : $"{_hireCost:N0}G";
        if (confirmSatisfactionText != null)
            confirmSatisfactionText.text = employee.SatisfactionText();
        else
            Debug.LogError("confirmSatisfactionText가 null입니다. 인스펙터 할당 확인");

        // 동일 직원 보유 여부 확인 (masterEmployeeId 공백이면 이름으로 대조)
        _conflictingOwned = EmployeeManager.Instance.ownedEmployees
            .Find(e => EmployeeSlotUI.IsSameEmployee(e, employee));

        bool hasConflict = _conflictingOwned != null;
        if (comparisonSection != null) comparisonSection.SetActive(hasConflict);
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

        hiringPanel.SetActive(false);
        confirmPanel.SetActive(true);
    }

    public void OnClickConfirmHire()
    {
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

        GameTimeManager.Instance?.StartTime();
        EmployeeManager.Instance.HireEmployee(_selectedEmployee);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);

        DialogManager.Instance.Resume();
    }

    // 보유 직원 유지하기 (비교 패널에서)
    public void OnClickKeep()
    {
        GameTimeManager.Instance?.StartTime();
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
        GameTimeManager.Instance?.StartTime();
        ConfirmUI.Instance.Show(
            "채용을 취소하시겠습니까?",
            onConfirm: () =>
            {
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