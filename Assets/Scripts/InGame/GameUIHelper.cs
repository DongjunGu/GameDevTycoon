//공통 메서드 모음

public static class GameUIHelper
{
    // 재화가 부족할 때 안내.
    // onDecline:
    //   - null 이 아니면 안내 대신 이 콜백을 호출 (예: "임금 못 내면 파산" 호출자가 파산 처리 전달)
    //   - null 이면 "재화가 부족합니다" AlertUI 표시
    // bypassGate:
    //   - 호출부 패널 자신이 ModalGate.Register(this)로 게이트를 쥔 채 열려있는 상태에서 부르는 경우 true.
    //     안 그러면 AlertUI.Show()의 기본 WhenFree 대기가 그 패널이 닫힐 때까지 표시를 미룬다
    //     (ProjectSetupUI "플랫폼과 장르를 선택해주세요" 와 동일한 원인의 버그).
    public static void ShowInsufficientFunds(System.Action onDecline = null, bool bypassGate = false)
    {
        if (onDecline != null) onDecline.Invoke();
        else                   AlertUI.Instance?.Show("재화가 부족합니다.", null, bypassGate);
    }
}
