//공통 메서드 모음

public static class GameUIHelper
{
    // onDecline:
    //   - 대출 활성으로 prompt 불가, 또는 사용자가 대출 prompt 거절 시 호출.
    //   - null 이면 기본 동작 (대출 활성 시 "재화가 부족합니다" AlertUI / prompt 거절 시 무동작)
    //   - "대출 못 받으면 파산" 호출자가 파산 처리 콜백 전달
    // onClose:
    //   - LoanUI 가 닫힐 때 호출 (true=대출 받음 / false=안 받고 닫음).
    //   - null 이면 패널 닫힘 후 별도 후속 처리 없음.
    //   - 임금 지급 같이 "대출 받으면 차감 재시도, 안 받고 닫으면 파산" 호출자가 흐름 분기 콜백 전달
    public static void ShowLoanPrompt(System.Action onDecline = null, System.Action<bool> onClose = null)
    {
        // 이미 활성 대출이 있으면 추가 대출 불가
        if (LoanManager.Instance != null && LoanManager.Instance.activeLoans.Count > 0)
        {
            if (onDecline != null) onDecline.Invoke();
            else                   AlertUI.Instance?.Show("재화가 부족합니다.");
            return;
        }

        ConfirmUI.Instance.Show(
            "재화가 부족합니다.\n대출하시겠습니까?",
            onConfirm: () => { LoanUI.Instance.OpenWithCloseCallback(onClose); },
            onCancel:  () => { onDecline?.Invoke(); },
            confirmText: "대출하기",
            cancelText:  "아니요"
        );
    }
}
