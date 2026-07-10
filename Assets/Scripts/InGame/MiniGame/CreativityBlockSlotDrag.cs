using UnityEngine;
using UnityEngine.EventSystems;

// 슬롯 전체 영역을 드래그 핸들로 삼아, 안에 든 (작을 수 있는) 블록을 대신 잡아 끌 수 있게 한다.
// 슬롯에 투명 raycast Image(DragArea)와 함께 부착되어, 드래그 이벤트를 대상 블록에 위임한다.
// → 1칸짜리처럼 작은 블록도 슬롯 어디를 눌러도 잡히므로 모바일에서 조작이 쉬워진다.
[RequireComponent(typeof(RectTransform))]
public class CreativityBlockSlotDrag : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CreativityGameBlockUI _target;

    public void SetTarget(CreativityGameBlockUI target) => _target = target;

    public void OnBeginDrag(PointerEventData e)
    {
        if (_target != null) _target.OnBeginDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_target != null) _target.OnDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_target != null) _target.OnEndDrag(e);
    }
}
