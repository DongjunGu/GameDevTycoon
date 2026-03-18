using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmployeeListUI : MonoBehaviour
{
    public static EmployeeListUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject listPanel;
    public GameObject confirmPanel;

    [Header("List")]
    public Transform slotParent;
    public GameObject ownedEmployeeSlotPrefab;

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
    public TextMeshProUGUI confirmDescText;

    private EmployeeData _selectedEmployee;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void OpenList()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        confirmPanel.SetActive(false);
        ShowList();
    }

    void ShowList()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        listPanel.SetActive(true); // ← 슬롯 생성 전에 먼저 활성화

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            var slot = Instantiate(ownedEmployeeSlotPrefab, slotParent);
            slot.GetComponent<OwnedEmployeeSlotUI>().Setup(employee, this);
        }
    }

    // 해고 버튼 클릭
    public void OnClickFire(EmployeeData employee)
    {
        _selectedEmployee = employee;

        confirmNameText.text = employee.employeeName;
        confirmRoleText.text = employee.RoleToString();
        confirmGradeText.text = employee.GradeToString();
        confirmPotentialText.text = employee.PotentialToString();
        confirmDevelopText.text = employee.DevelopText();
        confirmPlanningText.text = employee.PlanningText();
        confirmArtText.text = employee.ArtText();
        confirmPerfectionText.text = employee.PerfectionText();
        enhancementText.text = $"+{employee.enhancementLevel}";
        listPanel.SetActive(false);
        confirmPanel.SetActive(true);
    }
    // 해고 확정
    public void OnClickConfirmFire()
    {
        EmployeeManager.Instance.FireEmployee(_selectedEmployee);
        HUDUI.Instance.RefreshAll();
        confirmPanel.SetActive(false);
        ShowList();
    }

    // 뒤로가기
    public void OnClickBack()
    {
        confirmPanel.SetActive(false);
        listPanel.SetActive(true);
    }

    // 닫기
    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        listPanel.SetActive(false);
    }
}