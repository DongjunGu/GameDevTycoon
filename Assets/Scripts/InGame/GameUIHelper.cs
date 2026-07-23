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
    // bypassGate:
    //   - 호출부 패널 자신이 ModalGate.Register(this)로 게이트를 쥔 채로 열려있는 상태에서 부르는 경우 true.
    //     안 그러면 AlertUI.Show()의 기본 WhenFree 대기가 그 패널이 닫힐 때까지 표시를 미룬다
    //     (ProjectSetupUI "플랫폼과 장르를 선택해주세요" 와 동일한 원인의 버그).
    public static void ShowLoanPrompt(System.Action onDecline = null, System.Action<bool> onClose = null, bool bypassGate = false)
    {
        // [대출 시스템 비활성화] 대출 prompt 없이 "재화 부족" 안내 + onDecline 폴백만.
        // 대출 복구 시 아래 블록 주석 해제하고 이 두 줄 제거.
        if (onDecline != null) onDecline.Invoke();
        else                   AlertUI.Instance?.Show("재화가 부족합니다.", null, bypassGate);
        return;
        /*
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
        */
    }
}
