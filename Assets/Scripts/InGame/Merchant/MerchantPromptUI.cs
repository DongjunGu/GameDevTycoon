using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 상인 도착 시 표시되는 작은 popup — "아이템 사세요!" 텍스트.
// 패널 전체를 클릭하면 onClick 콜백 호출. 1회용 — Show 후 클릭 시 자동 SetActive(false).
public class MerchantPromptUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Optional")]
    public TMP_Text labelText;
    [Tooltip("표시 텍스트. 비워두면 그대로 사용.")]
    public string defaultMessage = "아이템 사세요!";

    System.Action _onClick;

    void Awake()
    {
        // 주의: 여기서 gameObject.SetActive(false) 하면 Show 가 SetActive(true) 한 직후 Awake 가 다시 false 로 토글되는
        // 무한 재비활성 버그 발생. 시작 비활성은 씬에서 이미 inactive 로 두는 것으로 처리.
        if (labelText != null && !string.IsNullOrEmpty(defaultMessage))
            labelText.text = defaultMessage;
    }

    public void Show(System.Action onClick)
    {
        _onClick = onClick;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _onClick = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var cb = _onClick;
        _onClick = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }
}
