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
    }

    // 1단계는 항상 표시. 2/3단계는 hire_tier2 / hire_tier3 해금 시에만 노출.
    void RefreshTierButtonVisibility()
    {
        if (tierButtons == null) return;
        bool tier2Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier2");
        bool tier3Unlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_tier3");

        if (tierButtons.Length > 0 && tierButtons[0] != null) tierButtons[0].SetActive(true);
        if (tierButtons.Length > 1 && tierButtons[1] != null) tierButtons[1].SetActive(tier2Unlocked);
        if (tierButtons.Length > 2 && tierButtons[2] != null) tierButtons[2].SetActive(tier3Unlocked);
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

        var (label, baseCost, range, weights) = Tiers[tierIndex];

        // 테크트리 '채용 비용 할인(hire_discount)' — 티어 진입 비용도 20% 감소
        bool discounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        int cost = discounted ? Mathf.RoundToInt(baseCost * 0.8f) : baseCost;
        string costDisplay = discounted
            ? $"<s><color=#888888>{baseCost:N0}G</color></s>  <color=#FFD24A>{cost:N0}G</color>  <size=70%>(-20%)</size>"
            : $"{cost:N0}G";

        ConfirmUI.Instance.Show(
            $"{label}\n비용: {costDisplay}",
            onConfirm: () =>
            {
                if (!MoneyManager.Instance.CanAfford(cost))
                {
                    GameUIHelper.ShowLoanPrompt();
                    return;
                }

                MoneyManager.Instance.SpendGold(cost);

                tierPanel.SetActive(false);
                if (loadingPanel != null) loadingPanel.SetActive(true);

                _currentTierIndex = tierIndex;
                _refreshUsed = false; // 새 티어 진입 시 새로고침 권리 부여
                LoadAndShowCandidates(tierIndex);
            },
            onCancel: () => { },
            confirmText: "채용하기",
            cancelText: "취소"
        );
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
        // 테크트리 '채용 비용 할인(hire_discount)' — 채용 비용 20% 감소 (취소선+할인가 표시)
        bool hireDiscounted = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("hire_discount");
        int prediscountHireCost = _hireCost;
        if (hireDiscounted) _hireCost = Mathf.RoundToInt(_hireCost * 0.8f);

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
        {
            if (_hireCost <= 0)
                confirmHireCostText.text = "무료";
            else if (hireDiscounted)
                confirmHireCostText.text = $"<s><color=#888888>{prediscountHireCost:N0}G</color></s>  <color=#FFD24A>{_hireCost:N0}G</color>  <size=70%>(-20%)</size>";
            else
                confirmHireCostText.text = $"{_hireCost:N0}G";
        }
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