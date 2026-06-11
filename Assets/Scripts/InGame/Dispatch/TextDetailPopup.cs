using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// 라벨(TMP) 클릭 → 연결된 설명 패널을 최상단(Canvas overrideSorting)에 표시.
// 라벨/패널 바깥을 클릭하면 자동으로 닫힌다. (파견 패널 특성/이벤트 설명용)
// New Input System — Mouse/Touchscreen.wasPressedThisFrame + EventSystem.RaycastAll (MenuController 외부클릭 패턴 재사용).
// 라벨의 TextMeshProUGUI 에 런타임 부착되며, Setup() 으로 매번 (패널, 설명) 을 바인딩한다.
[RequireComponent(typeof(TMP_Text))]
public class TextDetailPopup : MonoBehaviour, IPointerClickHandler
{
    const int POPUP_SORTING_ORDER = 5000; // 다른 UI 위에 뜨도록 높은 정렬값

    GameObject _panel;   // 설명 패널 (보일 때만 active)
    TMP_Text   _text;    // 패널 자식 텍스트
    string     _desc;
    bool       _open;

    static readonly List<RaycastResult> _hits = new();

    // 패널/설명 중 하나라도 비면 비활성(클릭 무시) + 패널 숨김. 매 선택마다 호출돼 재바인딩.
    public void Setup(GameObject panel, string desc)
    {
        _panel = panel;
        _desc  = desc;
        _text  = panel != null ? panel.GetComponentInChildren<TMP_Text>(true) : null;

        bool clickable = panel != null && !string.IsNullOrEmpty(desc);
        var label = GetComponent<TMP_Text>();
        if (label != null) label.raycastTarget = clickable;

        if (clickable) EnsureTopmost(panel);
        Hide();
        enabled = clickable;
    }

    // 패널을 최상단에 렌더링하기 위한 Canvas(overrideSorting) + GraphicRaycaster 보장
    static void EnsureTopmost(GameObject panel)
    {
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = POPUP_SORTING_ORDER;
        if (panel.GetComponent<GraphicRaycaster>() == null) panel.AddComponent<GraphicRaycaster>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_open) Hide(); else Show();
    }

    void Show()
    {
        if (_panel == null) return;
        if (_text != null) _text.text = _desc;
        _panel.SetActive(true);
        _open = true;
    }

    void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        _open = false;
    }

    void OnDisable() => Hide();

    void Update()
    {
        if (!_open) return;
        if (!TryGetPress(out var pos) || EventSystem.current == null) return;

        var ped = new PointerEventData(EventSystem.current) { position = pos };
        _hits.Clear();
        EventSystem.current.RaycastAll(ped, _hits);

        foreach (var h in _hits)
        {
            var t = h.gameObject.transform;
            if (t == transform || t.IsChildOf(transform)) return;            // 라벨 클릭 → OnPointerClick 이 토글
            if (_panel != null && t.IsChildOf(_panel.transform)) return;     // 패널 내부 클릭 → 유지
        }
        Hide(); // 바깥 클릭 → 닫기
    }

    static bool TryGetPress(out Vector2 position)
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
