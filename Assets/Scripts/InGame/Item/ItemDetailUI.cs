using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailUI : MonoBehaviour
{
    public static ItemDetailUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject detailPanel;

    [Header("UI")]
    public Image              itemImage;
    public TextMeshProUGUI    nameText;
    public TextMeshProUGUI    descriptionText;
    public TextMeshProUGUI    effectText;
    public TextMeshProUGUI    countText;
    public Button             useButton;

    private ItemChartRow _currentRow;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        detailPanel.SetActive(false);
    }

    public void Show(ItemChartRow row)
    {
        _currentRow = row;

        nameText.text        = row.name;
        descriptionText.text = row.description;
        effectText.text      = row.effectType switch
        {
            "satisfaction" => $"만족도 +{row.effectValue}",
            _ => ""
        };

        int count = ItemManager.Instance.GetCount(row.itemId);
        countText.text         = $"보유: {count}개";
        useButton.interactable = count > 0;

        var sprite = Resources.Load<Sprite>($"Items/{row.imageId}");
        if (sprite != null && itemImage != null)
            itemImage.sprite = sprite;

        ItemPanelUI.Instance.itemListPanel.SetActive(false);
        detailPanel.SetActive(true);
    }

    public void RefreshCount()
    {
        if (_currentRow == null) return;
        int count = ItemManager.Instance.GetCount(_currentRow.itemId);
        countText.text         = $"보유: {count}개";
        useButton.interactable = count > 0;
    }

    public void OnClickUse()
    {
        if (_currentRow == null) return;
        ItemEmployeeSelectUI.Instance.Open(_currentRow);
    }

    public void OnClickBack()
    {
        detailPanel.SetActive(false);
        ItemPanelUI.Instance.itemListPanel.SetActive(true);
        ItemPanelUI.Instance.Refresh();
    }
}
