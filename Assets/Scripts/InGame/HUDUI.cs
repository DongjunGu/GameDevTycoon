using UnityEngine;
using TMPro;

public class HUDUI : MonoBehaviour
{
    public static HUDUI Instance { get; private set; }

    [Header("HUD Items")]
    public TextMeshProUGUI totalSalaryText;
    [Header("Money")]
    public TextMeshProUGUI moneyText;
    // public TextMeshProUGUI reputationText;
    // public TextMeshProUGUI officeText;
    [Header("Time")]
    public TextMeshProUGUI timeText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RefreshAll()
    {
        RefreshSalary();
        RefreshMoney();
        RefreshTime();
        // 추후 추가
        
        // RefreshReputation();
    }

    void RefreshSalary()
    {
        int total = 0;
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            total += employee.salary;

        totalSalaryText.text = $"총 연봉: {total:N0}G";
    }
    public void RefreshMoney()
    {
        if (moneyText != null)
            moneyText.text = $"{MoneyManager.Instance.Gold:N0}G";
    }
    public void RefreshTime()
    {
        if (timeText != null)
            timeText.text = GameTimeManager.Instance.GetTimeString();
    }
}