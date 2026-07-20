using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 자기 자신(+ 자식) 밖 아무 곳이나 클릭하면 자동으로 비활성화되는 팝업. EmployeeCardUI.Update() 의
// "빈 공간/다른 UI 클릭 시 닫기" 판정을 그대로 재사용 — 백드롭이나 별도 매니저 없이 팝업 자신에게만
// 붙이면 끝. 재사용은 이 컴포넌트가 붙은 오브젝트(예: TierHelpPopup1)를 통째로 복제해 배치하고,
// 아이콘의 onClick 에서 Show() 만 호출하면 됨.
[RequireComponent(typeof(RectTransform))]
public class PopupClickAway : MonoBehaviour
{
    static readonly List<RaycastResult> _raycastResults = new();

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 형제 UI 위에 렌더
    }

    public void Hide() => gameObject.SetActive(false);

    void Update()
    {
        if (!gameObject.activeSelf) return;
        if (!TryGetPressedPointerPosition(out Vector2 pos)) return;

        // 자기 자신(또는 그 자식) 위 클릭만 닫지 않음. 그 외 어디든 클릭하면 닫힘.
        if (EventSystem.current != null)
        {
            var ped = new PointerEventData(EventSystem.current) { position = pos };
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(ped, _raycastResults);
            foreach (var r in _raycastResults)
                if (r.gameObject.transform.IsChildOf(transform)) return;
        }

        Hide();
    }

    static bool TryGetPressedPointerPosition(out Vector2 position)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            position = touch.primaryTouch.position.ReadValue();
            return true;
        }
        position = default;
        return false;
    }
}
