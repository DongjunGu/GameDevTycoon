using UnityEngine;
using UnityEngine.UI;

// 단일 탭의 정체성/시각/상태 책임 (SRP)
// - tabId, button, panel 묶음
// - Show/Hide 시 인디케이터·라벨 색 토글
// 네비게이션 흐름·다른 탭은 알지 못함 — NavigationController가 사용
public class OutGameTabController : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("코드에서 SwitchTo(string)로 선택할 ID — '직원', '특성' 등")]
    public string tabId;

    [Header("Refs")]
    [Tooltip("이 탭으로 진입시킬 BottomNavBar 안의 버튼")]
    public Button tabButton;
    [Tooltip("표시될 패널 GameObject")]
    public GameObject panel;

    [Header("(Optional) Visual State")]
    [Tooltip("선택 시 켜질 인디케이터 자식 GameObject")]
    public GameObject activeIndicator;
    [Tooltip("선택 시 색이 바뀔 라벨 Graphic")]
    public Graphic labelGraphic;
    public Color labelActiveColor   = Color.white;
    public Color labelInactiveColor = new Color(1f, 1f, 1f, 0.45f);

    public string TabId    => tabId;
    public Button TabButton => tabButton;

    public void Show()
    {
        if (panel != null)            panel.SetActive(true);
        if (activeIndicator != null)  activeIndicator.SetActive(true);
        if (labelGraphic != null)     labelGraphic.color = labelActiveColor;
    }

    public void Hide()
    {
        if (panel != null)            panel.SetActive(false);
        if (activeIndicator != null)  activeIndicator.SetActive(false);
        if (labelGraphic != null)     labelGraphic.color = labelInactiveColor;
    }
}
