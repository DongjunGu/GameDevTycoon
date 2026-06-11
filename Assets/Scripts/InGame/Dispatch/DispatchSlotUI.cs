using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 파견 패널 슬롯 — 초상화/이름/강화레벨 표시, 클릭 시 선택. 파견중이면 badge 표시 + 선택 불가.
public class DispatchSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    [Header("Role 아이콘 — roleIcons 는 enum 순서 [Planner, Programmer, Artist] 로 인스펙터에서 할당")]
    public Image roleIcon;
    public Sprite[] roleIcons;
    public TextMeshProUGUI enhancementText;
    public Button selectButton;
    public GameObject selectedIndicator; // 선택 하이라이트
    public GameObject dispatchedBadge;   // "파견중" badge

    private string _empId;
    public string EmployeeId => _empId;

    public void Setup(EmployeeData data, DispatchPanelUI panel, bool dispatched)
    {
        _empId = data.id;

        if (nameText != null) nameText.text = data.employeeName;
        if (roleIcon != null && roleIcons != null
            && (int)data.role >= 0 && (int)data.role < roleIcons.Length
            && roleIcons[(int)data.role] != null)
        {
            roleIcon.sprite = roleIcons[(int)data.role];
            roleIcon.enabled = true;
        }
        if (enhancementText != null) enhancementText.text = $"Lv {data.enhancementLevel}";

        if (portraitImage != null)
        {
            Sprite sp = !string.IsNullOrEmpty(data.portraitId)
                ? Resources.Load<Sprite>($"Portraits/{data.portraitId}") : null;
            portraitImage.sprite = sp;
            portraitImage.enabled = sp != null;
        }

        if (dispatchedBadge != null) dispatchedBadge.SetActive(dispatched);
        SetSelected(false);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.interactable = !dispatched;
            if (!dispatched)
                selectButton.onClick.AddListener(() => panel.OnSelect(_empId));
        }
    }

    public void SetSelected(bool on)
    {
        // selectButton 의 Image 알파만 토글 — 선택 시 120/255, 해제 시 0. RGB(색)는 인스펙터에서 설정한 값 유지.
        if (selectButton != null)
        {
            var img = selectButton.image != null ? selectButton.image : selectButton.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = on ? 120f / 255f : 0f;
                img.color = c;
            }
        }

        // selectedIndicator 가 연결돼 있으면 함께 토글 (옵션). 슬롯 루트 자신이면 무시(전체 꺼짐 방어).
        if (selectedIndicator != null && selectedIndicator != gameObject)
            selectedIndicator.SetActive(on);
    }
}
