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
        if (enhancementText != null) enhancementText.text = $"+{data.enhancementLevel}";

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
        // selectedIndicator 가 슬롯 루트 자신으로 잘못 연결돼 있으면 토글 시 슬롯 전체가 꺼짐 → 방어
        if (selectedIndicator != null && selectedIndicator != gameObject)
            selectedIndicator.SetActive(on);
    }
}
