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

    public void Setup(ItemChartRow row, int count, bool usable = true)
    {
        _row = row;
        nameText.text  = row.name;
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
