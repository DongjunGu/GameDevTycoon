using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HiringUI : MonoBehaviour
{
    public static HiringUI Instance { get; private set; }

    [Header("Panels")]
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
    //public TextMeshProUGUI confirmStateText;

    [Header("Settings")]
    public int candidateCount = 4;

    private EmployeeData _selectedEmployee;
    private List<EmployeeData> _currentCandidates = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void OpenHiring()
    {
        int hiringCost = 500;

        ConfirmUI.Instance.Show(
            $"직원을 채용하시겠습니까?\n비용: {hiringCost:N0}G",
            onConfirm: () =>
            {
                if (!MoneyManager.Instance.CanAfford(hiringCost))
                {
                    AlertUI.Instance.Show("재화가 부족합니다!");
                    return;
                }

                MoneyManager.Instance.SpendGold(hiringCost);

                hiringPanel.SetActive(false);
                confirmPanel.SetActive(false);
                if (loadingPanel != null) loadingPanel.SetActive(true);

                _currentCandidates.Clear();
                EmployeeManager.Instance.LoadRandomCandidates(candidateCount, ShowCandidates);
            },
            onCancel: () => { }
        );
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

        confirmNameText.text = employee.employeeName;
        confirmRoleText.text = employee.RoleToString();
        confirmGradeText.text = employee.GradeToString();
        confirmPotentialText.text = employee.PotentialToString();
        confirmDevelopText.text = employee.DevelopRangeText();
        confirmPlanningText.text = employee.PlanningRangeText();
        confirmArtText.text = employee.ArtRangeText();
        confirmPerfectionText.text = employee.PerfectionRangeText();
        confirmSalaryText.text = employee.SalaryRangeText();
        enhancementText.text     = $"+{employee.enhancementLevel}";
        //confirmStateText.text      = employee.StateToString();

        hiringPanel.SetActive(false);
        confirmPanel.SetActive(true);
    }

    public void OnClickConfirmHire()
    {
        EmployeeManager.Instance.HireEmployee(_selectedEmployee);
        hiringPanel.SetActive(false);
        confirmPanel.SetActive(false);
    }

    public void OnClickBack()
    {
        ShowCandidates(_currentCandidates);
    }

    public void OnClickClose()
    {
        ConfirmUI.Instance.Show(
            "채용을 취소하시겠습니까?",
            onConfirm: () =>
            {
                hiringPanel.SetActive(false);
            },
            onCancel: () =>
            {
                ConfirmUI.Instance.confirmPanel.SetActive(false);
            }
        );
    }
}