using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    public Image itemImage;
    public Image frameImage;
    public ItemGradeSet gradeSet;
    public Image veilImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI countText;
    public Button slotButton;

    static readonly Color VeiledNameColor = new Color32(0x79, 0x79, 0x79, 0xFF);
    private Color _defaultNameColor = Color.white;
    private bool  _defaultNameColorCached;

    private ItemChartRow _row;

    // 슬롯은 좁아서 2줄 표시가 필요한 아이템이 있다. Item_Chart의 name은 앞으로 줄바꿈 없는 게
    // 기본값이 될 예정이라(ItemDetailUI 등 다른 곳은 그냥 row.name 그대로 표시), 슬롯 전용 줄바꿈은
    // 여기서 별도로 유지한다 — 지금(2026-08-14) Item_Chart.csv에 실제로 들어있던 값 그대로.
    static readonly System.Collections.Generic.Dictionary<string, string> SlotNameOverride = new()
    {
        { "enhanceProtect",  "하락\n방어권" },
        { "enhanceLow",       "하급\n강화권" },
        { "enhanceMid",       "중급\n강화권" },
        { "enhanceHigh",      "상급\n강화권" },
        { "resetSpirit",      "초심\n회복기" },
        { "upgradeRandom",    "랜덤\n업그레이드" },
        { "upgradeDevelop",   "개발\n업그레이드권" },
        { "upgradeArt",       "아트\n업그레이드권" },
        { "upgradePlan",      "기획\n업그레이드권" },
        { "mysteryPotion",    "수상한\n물약" },
        { "awaken",           "각성의\n물약" },
        { "techNote",         "오래된\n연구노트" },
        { "hypnotizer",       "최면술사의\n시계" },
    };

    public void Setup(ItemChartRow row, int count, bool usable = true)
    {
        _row = row;
        nameText.text  = SlotNameOverride.TryGetValue(row.itemId, out var slotName) ? slotName : row.name;
        countText.text = $"{count}";

        var sprite = Resources.Load<Sprite>($"Items/{row.imageId}");
        if (sprite != null && itemImage != null)
            itemImage.sprite = sprite;

        ItemGradeSet.Apply(frameImage, gradeSet, row.grade);

        if (veilImage != null) veilImage.gameObject.SetActive(!usable);

        if (nameText != null)
        {
            if (!_defaultNameColorCached)
            {
                _defaultNameColor = nameText.color;
                _defaultNameColorCached = true;
            }
            nameText.color = usable ? _defaultNameColor : VeiledNameColor;
        }

        slotButton.interactable = usable;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => ItemDetailUI.Instance.Show(_row));
    }
}
