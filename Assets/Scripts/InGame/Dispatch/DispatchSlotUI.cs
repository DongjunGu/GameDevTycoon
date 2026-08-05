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
    static readonly Color CEONameColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    const string CEODisplayName = "주인공";
    private Color _defaultNameColor = Color.white;
    private bool  _defaultNameColorCached;
    [Header("등급별 배경 (공용 GradeSpriteSet 에셋)")]
    public Image bgImage;
    public GradeSpriteSet bgGradeSet;
    [Header("Role 아이콘 — roleIcons 는 enum 순서 [Planner, Programmer, Artist] 로 인스펙터에서 할당")]
    public GameObject roleIconPanel; // roleIcon 의 부모 — CEO 일 때 통째로 비활성화
    public Image roleIcon;
    public Sprite[] roleIcons;
    public TextMeshProUGUI enhancementText;
    [Header("연속 팀장 횟수 — 팀장 선택 모드(isLeaderMode)에서만 표시, CEO는 항상 숨김")]
    public GameObject countPanel;
    public TextMeshProUGUI countText;
    public Button selectButton;
    public GameObject selectedIndicator; // SelectionImage — ImageBlink 컴포넌트로 선택 시 스스로 깜빡임(SetActive만 토글하면 됨)
    public GameObject dispatchedBadge;   // "파견중" badge

    private string _empId;
    public string EmployeeId => _empId;

    public void Setup(EmployeeData data, DispatchPanelUI panel, bool dispatched, bool isLeaderMode = false)
    {
        _empId = data.id;

        GradeSpriteSet.Apply(bgImage, bgGradeSet, data.isCEO, data.grade);

        if (nameText != null && !_defaultNameColorCached)
        {
            _defaultNameColor = nameText.color;
            _defaultNameColorCached = true;
        }

        // CEO는 역할/강화레벨/연속 팀장 횟수 개념이 없음 — roleIconPanel/강화텍스트 비활성화 + CEOText 활성화.
        if (data.isCEO)
        {
            if (roleIconPanel != null) roleIconPanel.SetActive(false);
            if (enhancementText != null) enhancementText.gameObject.SetActive(false);
            if (CEOText != null) CEOText.SetActive(true);
            if (countPanel != null) countPanel.SetActive(false);
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
            if (roleIconPanel != null) roleIconPanel.SetActive(true);
            if (roleIcon != null)
            {
                if (roleIcons != null && (int)data.role >= 0 && (int)data.role < roleIcons.Length
                    && roleIcons[(int)data.role] != null)
                {
                    roleIcon.sprite = roleIcons[(int)data.role];
                    roleIcon.enabled = true;
                }
            }
            if (enhancementText != null)
            {
                enhancementText.gameObject.SetActive(true);
                enhancementText.text = $"Lv {data.enhancementLevel}";
            }

            // 팀장 선택 모드에서만 연속 팀장 횟수(EmployeeData.consecutiveLeaderCount) 표시.
            if (countPanel != null) countPanel.SetActive(isLeaderMode);
            if (isLeaderMode && countText != null) countText.text = $"{data.consecutiveLeaderCount}";
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
        // selectedIndicator = SelectionImage — 슬롯 루트 자신이면 무시(전체 꺼짐 방어).
        if (selectedIndicator != null && selectedIndicator != gameObject)
            selectedIndicator.SetActive(on);
    }
}
