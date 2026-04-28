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

        // 카드 컨텍스트(특정 직원 대상으로 열림): 직원 선택 단계 건너뛰고 즉시 사용
        string targetId = ItemPanelUI.Instance != null ? ItemPanelUI.Instance.TargetEmployeeId : null;
        if (!string.IsNullOrEmpty(targetId))
        {
            var emp = EmployeeManager.Instance?.GetEmployee(targetId);
            if (emp != null && ItemManager.Instance.UseItem(_currentRow.itemId, emp))
            {
                detailPanel.SetActive(false);
                ItemPanelUI.Instance.OnClickClose(); // 패널 닫기 + StartTime + 카드 콜백 호출
            }
            return;
        }

        // 일반 플로우: 직원 선택 패널로 이동
        ItemEmployeeSelectUI.Instance.Open(_currentRow);
    }

    public void OnClickBack()
    {
        detailPanel.SetActive(false);
        ItemPanelUI.Instance.itemListPanel.SetActive(true);
        ItemPanelUI.Instance.Refresh();
    }
}
