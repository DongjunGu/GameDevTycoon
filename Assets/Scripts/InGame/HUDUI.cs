using UnityEngine;
using TMPro;

public class HUDUI : MonoBehaviour
{
    public static HUDUI Instance { get; private set; }

    [Header("HUD Items")]
    public TextMeshProUGUI totalSalaryText;
    // 추후 추가 예정
    // public TextMeshProUGUI moneyText;
    // public TextMeshProUGUI reputationText;
    // public TextMeshProUGUI officeText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RefreshAll()
    {
        RefreshSalary();
        // 추후 추가
        // RefreshMoney();
        // RefreshReputation();
    }

    void RefreshSalary()
    {
        int total = 0;
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            total += employee.salary;

        totalSalaryText.text = $"총 연봉: {total:N0}G";
    }
}