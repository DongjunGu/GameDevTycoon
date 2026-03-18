//공통 메서드 모음

public static class GameUIHelper
{
    public static void ShowLoanPrompt()
    {
        ConfirmUI.Instance.Show(
            "재화가 부족합니다.\n대출하시겠습니까?",
            onConfirm: () => { LoanUI.Instance.Open(); },
            onCancel:  () => { },
            confirmText: "대출하기",
            cancelText:  "아니요"
        );
    }
}