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
    [Header("Annual Fee (연세)")]
    public TextMeshProUGUI feeText;            // AnnualFeePanel-feeText
    [Tooltip("시작 연도 기준 기본 연세 (G)")]
    public int yearFee = 1000;
    [Tooltip("해마다 증가하는 연세 (G)")]
    public int yearFeeIncreasePerYear = 500;

    // 현재 연세 = 기본 + 500 × 경과연수 (Year 에서 파생 → 저장/로드 자동 반영, 차감은 추후)
    public int CurrentYearFee
    {
        get
        {
            int years = GameTimeManager.Instance != null
                ? Mathf.Max(0, GameTimeManager.Instance.Year - GameTimeManager.StartYear)
                : 0;
            return yearFee + yearFeeIncreasePerYear * years;
        }
    }
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
        RefreshYearFee();
    }

    void RefreshSalary()
    {
        int total = EmployeeManager.Instance.GetTotalSalary();
        totalSalaryText.text = $"{total:N0}G";
    }
    public void RefreshMoney()
    {
        if (moneyText != null)
            moneyText.text = $"{MoneyManager.Instance.Gold:N0}G";
    }
    public void RefreshTime()
    {
        if (timeText == null) return;
        var t = GameTimeManager.Instance;
        // TimeText 만 연도를 2자리로 표시 (2000→00, 2026→26). 다른 곳은 GetTimeString 그대로(4자리)
        timeText.text = $"{t.Year % 100:D2}년 {t.Month}월 {t.Week}주차";
    }
    // 연세(Yearfee) 표시 — 현재는 기본값만 출력. 차감/동적 계산은 추후 구현.
    public void RefreshYearFee()
    {
        if (feeText != null)
            feeText.text = $"{CurrentYearFee:N0}G";
    }

}