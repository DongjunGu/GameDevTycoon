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

        int tierIndex = loanLevel - 1; // ← loanLevel 기준
        int amount = LoanManager.LoanAmounts[tierIndex];
        int dueYear = GameTimeManager.Instance.Year + 1;

        ConfirmUI.Instance.Show(
            $"{amount:N0}G 대출\n만기: {dueYear}년 {GameTimeManager.Instance.Month}월 {GameTimeManager.Instance.Week}주\n1년 후 전액 상환",
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
            activeLoanText.text = $"현재 대출 없음\n현재 {availableAmount:N0}G 대출 가능합니다.";
            return;
        }

        string text = "현재 대출 현황\n";
        foreach (var loan in loans)
            text += $"{loan.amount:N0}G (만기: {loan.year}년 {loan.month}월 {loan.week}주)\n";

        activeLoanText.text = text;
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        loanPanel.SetActive(false);
    }
}