using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    public Image itemImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI countText;
    public Button slotButton;

    private ItemChartRow _row;

    public void Setup(ItemChartRow row, int count)
    {
        _row = row;
        nameText.text  = row.name;
        countText.text = $"x{count}";

        var sprite = Resources.Load<Sprite>($"Items/{row.imageId}");
        if (sprite != null && itemImage != null)
            itemImage.sprite = sprite;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => ItemDetailUI.Instance.Show(_row));
    }
}
