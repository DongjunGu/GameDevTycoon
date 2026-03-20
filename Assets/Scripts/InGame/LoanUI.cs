using UnityEngine;
using TMPro;

public class LoanUI : MonoBehaviour
{
    public static LoanUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject loanPanel;

    [Header("UI")]
    public TextMeshProUGUI activeLoanText; // 현재 대출 현황
    [Header("Settings")]
    [Range(1, 5)]
    public int loanLevel = 1;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        loanPanel.SetActive(false);
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        RefreshUI();
        loanPanel.SetActive(true);
    }

    public void OnClickLoan()
    {
        if (LoanManager.Instance.activeLoans.Count > 0)
        {
            AlertUI.Instance.Show("기존 대출을 상환한 후 대출이 가능합니다.");
            return;
        }

        int tierIndex = loanLevel - 1;
        int amount = LoanManager.LoanAmounts[tierIndex];
        int repayAmount = LoanManager.Instance.CalcRepayAmount(amount);
        float rate = LoanManager.Instance.interestRate * 100f;
        int dueYear = GameTimeManager.Instance.Year + 1;

        ConfirmUI.Instance.Show(
            $"{amount:N0}G 대출\n이자율: {rate:F0}% → 상환금액: {repayAmount:N0}G\n만기: {dueYear}년 {GameTimeManager.Instance.Month}월 {GameTimeManager.Instance.Week}주",
            onConfirm: () =>
            {
                LoanManager.Instance.TakeLoan(tierIndex);
                RefreshUI();
            },
            onCancel: () => { },
            confirmText: "대출하기",
            cancelText: "취소"
        );
    }

    public void RefreshUI()
    {
        if (activeLoanText == null) return;

        var loans = LoanManager.Instance.activeLoans;
        if (loans.Count == 0)
        {
            int availableAmount = LoanManager.LoanAmounts[loanLevel - 1];
            int repayAmount = LoanManager.Instance.CalcRepayAmount(availableAmount);
            float rate = LoanManager.Instance.interestRate * 100f;
            activeLoanText.text = $"현재 대출 없음\n현재 {availableAmount:N0}G 대출 가능합니다.\n(이자율 {rate:F0}% / 상환 {repayAmount:N0}G)";
            return;
        }

        string text = "현재 대출 현황\n";
        foreach (var loan in loans)
            text += $"{loan.amount:N0}G → 상환 {loan.repayAmount:N0}G\n(만기: {loan.year}년 {loan.month}월 {loan.week}주)\n";

        activeLoanText.text = text;
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        loanPanel.SetActive(false);
    }
}