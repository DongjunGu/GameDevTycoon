using UnityEngine;
using TMPro;

public class HUDUI : MonoBehaviour
{
    public static HUDUI Instance { get; private set; }

    [Header("HUD Items")]
    public TextMeshProUGUI totalSalaryText;
    [Header("Money")]
    public TextMeshProUGUI moneyText;
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
    }

    void RefreshSalary()
    {
        int total = EmployeeManager.Instance.GetTotalSalary();
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