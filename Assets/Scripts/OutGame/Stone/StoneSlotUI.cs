using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보관함 돌 슬롯 1개 prefab.
// 인스펙터에 사각형 Image + 3 TMP 텍스트(기획/개발/아트) + selected outline Image + root Button 연결.
// 부모(StoneListPanelUI)가 Bind 시점에 콜백 주입.
public class StoneSlotUI : MonoBehaviour
{
    [Header("Stat Texts")]
    public TMP_Text planningText;
    public TMP_Text developText;
    public TMP_Text artText;

    [Header("Selection")]
    [Tooltip("선택됐을 때 켜지는 outline/하이라이트 Image. 인스펙터에서 enabled=false 로 시작.")]
    public Image selectedOutline;

    [Header("Applied (적용중) Indicator")]
    [Tooltip("이 돌이 현재 PieceLeft 에 적용중일 때 켜지는 GameObject (예: \"적용중\" 배지). 시작은 비활성.")]
    public GameObject activeIndicator;

    [Header("Click")]
    public Button rootButton;

    Stone _stone;
    System.Action<Stone> _onClick;

    public Stone Stone => _stone;

    public void Bind(Stone stone, System.Action<Stone> onClick)
    {
        _stone = stone;
        _onClick = onClick;

        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
            rootButton.onClick.AddListener(() => _onClick?.Invoke(_stone));
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_stone == null) return;
        if (planningText != null) planningText.text = _stone.planningStage.ToString();
        if (developText  != null) developText.text  = _stone.developStage.ToString();
        if (artText      != null) artText.text      = _stone.artStage.ToString();
    }

    public void SetSelected(bool on)
    {
        if (selectedOutline != null) selectedOutline.enabled = on;
    }

    public void SetApplied(bool on)
    {
        if (activeIndicator != null) activeIndicator.SetActive(on);
    }
}
