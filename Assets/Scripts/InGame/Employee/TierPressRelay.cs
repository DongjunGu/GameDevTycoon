using UnityEngine;
using UnityEngine.EventSystems;

// tier 버튼(tier1/2/3)에 붙여 포인터 down/up을 HiringUI 로 중계한다 — 눌려 있는 동안은
// tierNOutlinePanel 의 Image/Outline 을 선택 여부와 무관하게 강제로 끄고, 떼면 원래 선택 상태로
// 되돌리기 위함(HiringUI.SetTierPressed 참고).
public class TierPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int tierIndex;

    public void OnPointerDown(PointerEventData eventData) => HiringUI.Instance?.SetTierPressed(tierIndex, true);
    public void OnPointerUp(PointerEventData eventData)   => HiringUI.Instance?.SetTierPressed(tierIndex, false);
}
