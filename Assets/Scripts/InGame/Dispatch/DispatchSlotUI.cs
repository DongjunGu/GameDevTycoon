using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 파견 패널 슬롯 — 초상화/이름/강화레벨 표시, 클릭 시 선택. 파견중이면 badge 표시 + 선택 불가.
public class DispatchSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    [Tooltip("CEO(주인공)일 때만 활성화되는 라벨")]
    public GameObject CEOText;
    static readonly Color CEONameColor = new Color32(0xFF, 0xF1, 0x94, 0xFF);
    const string CEODisplayName = "주인공";
    private Color _defaultNameColor = Color.white;
    private bool  _defaultNameColorCached;
    [Header("등급별 배경 (공용 GradeSpriteSet 에셋)")]
    public Image bgImage;
    public GradeSpriteSet bgGradeSet;
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

        GradeSpriteSet.Apply(bgImage, bgGradeSet, data.isCEO, data.grade);

        if (nameText != null && !_defaultNameColorCached)
        {
            _defaultNameColor = nameText.color;
            _defaultNameColorCached = true;
        }

        // CEO는 역할/강화레벨 개념이 없음 — roleIconPanel 자식 Image 비활성화 + 강화텍스트 공백.
        if (data.isCEO)
        {
            if (roleIcon != null) roleIcon.gameObject.SetActive(false);
            if (enhancementText != null) enhancementText.text = "";
            if (CEOText != null) CEOText.SetActive(true);
            if (nameText != null)
            {
                nameText.text  = CEODisplayName;
                nameText.color = CEONameColor;
            }
        }
        else
        {
            if (CEOText != null) CEOText.SetActive(false);
            if (nameText != null)
            {
                nameText.text  = data.employeeName;
                nameText.color = _defaultNameColor;
            }
            if (roleIcon != null)
            {
                roleIcon.gameObject.SetActive(true);
                if (roleIcons != null && (int)data.role >= 0 && (int)data.role < roleIcons.Length
                    && roleIcons[(int)data.role] != null)
                {
                    roleIcon.sprite = roleIcons[(int)data.role];
                    roleIcon.enabled = true;
                }
            }
            if (enhancementText != null) enhancementText.text = $"Lv {data.enhancementLevel}";
        }

        if (portraitImage != null)
        {
            Sprite sp = !string.IsNullOrEmpty(data.portraitId)
                ? Resources.Load<Sprite>($"Portraits/Mini/{data.portraitId}") : null;
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
        // selectedIndicator = SelectionBorder(#DF0F0F 3px 테두리 4바) — 슬롯 루트 자신이면 무시(전체 꺼짐 방어).
        if (selectedIndicator != null && selectedIndicator != gameObject)
            selectedIndicator.SetActive(on);
    }
}
