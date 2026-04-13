using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HiringUI : MonoBehaviour
{
    public static HiringUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject tierPanel;       // 티어 선택 패널 (새로 추가)
    public GameObject hiringPanel;
    public GameObject confirmPanel;
    public GameObject loadingPanel;

    [Header("Slots")]
    public Transform slotParent;
    public GameObject employeeSlotPrefab;

    [Header("Confirm")]
    public TextMeshProUGUI confirmNameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI confirmRoleText;
    public TextMeshProUGUI confirmGradeText;
    public TextMeshProUGUI confirmPotentialText;
    public TextMeshProUGUI confirmDevelopText;
    public TextMeshProUGUI confirmPlanningText;
    public TextMeshProUGUI confirmArtText;
    public TextMeshProUGUI confirmPerfectionText;
    public TextMeshProUGUI confirmSalaryText;
    public TextMeshProUGUI confirmSatisfactionText;
    public TextMeshProUGUI confirmHireCostText;
    [Header("Settings")]
    public int candidateCount = 4;

    private EmployeeData _selectedEmployee;
    private List<EmployeeData> _currentCandidates = new();
    private int _hireCost;

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

    // ── 티어 선택 패널 열기 ───────────────────
    public void OpenHiring()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        tierPanel.SetActive(true);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
    }

    // 티어 버튼 클릭 (0~4)
    public void OnClickTier(int tierIndex)
    {
        if (EmployeeManager.Instance.ownedEmployees.Count >= StageManager.Instance.MaxEmployeeCount)
        {
            AlertUI.Instance.Show("최대 직원수 입니다.");
            return;
        }

        var (label, cost, range, weights) = Tiers[tierIndex];

        ConfirmUI.Instance.Show(
            $"{label}\n비용: {cost:N0}G",
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

                _currentCandidates.Clear();
                EmployeeManager.Instance.LoadRandomCandidates(candidateCount, tierIndex, candidates =>
                {
                    // 티어별 강화 수치 적용
                    foreach (var employee in candidates)
                    {
                        int enhLevel = RollWeighted(range, weights);
                        ApplyEnhancementLevel(employee, enhLevel);
                    }
                    ShowCandidates(candidates);
                });
            },
            onCancel: () => { },
            confirmText: "채용하기",
            cancelText: "취소"
        );
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
    }

    public void OnSelectEmployee(EmployeeData employee)
    {
        _selectedEmployee = employee;

        int baseHireCost = EmployeeManager.GetExpectedEnhanceCost(employee.enhancementLevel);
        _hireCost = Mathf.RoundToInt(baseHireCost * UnityEngine.Random.Range(0.8f, 1.2f));

        confirmNameText.text = employee.employeeName;
        confirmRoleText.text = employee.RoleToString();
        confirmGradeText.text = employee.GradeToString();
        confirmPotentialText.text = employee.PotentialToString();
        confirmDevelopText.text = employee.DevelopDisplayText();
        confirmPlanningText.text = employee.PlanningDisplayText();
        confirmArtText.text = employee.ArtDisplayText();
        confirmPerfectionText.text = employee.PerfectionRangeText();
        confirmSalaryText.text = employee.SalaryRangeText();
        enhancementText.text = $"+{employee.enhancementLevel}";
        if (confirmHireCostText != null)
            confirmHireCostText.text = _hireCost > 0 ? $"{_hireCost:N0}G" : "무료";
        
        if (confirmSatisfactionText != null)
        confirmSatisfactionText.text = employee.SatisfactionText();
    else
        Debug.LogError("confirmSatisfactionText가 null입니다. 인스펙터 할당 확인");


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

        GameTimeManager.Instance?.StartTime();
        EmployeeManager.Instance.HireEmployee(_selectedEmployee);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);

        DialogManager.Instance.Resume();//다이얼로그 다시재생
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