using UnityEngine;

public class ItemPanelUI : MonoBehaviour
{
    public static ItemPanelUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject itemListPanel;

    [Header("List")]
    public Transform  slotParent;
    public GameObject itemSlotPrefab;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        itemListPanel.SetActive(false);
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        itemListPanel.SetActive(true);
        Refresh();
    }

    public void Refresh()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var kv in ItemChartLoader.Cache)
        {
            int count = ItemManager.Instance.GetCount(kv.Key);
            if (count <= 0) continue;

            var go   = Instantiate(itemSlotPrefab, slotParent);
            var slot = go.GetComponent<ItemSlotUI>();
            slot.Setup(kv.Value, count);
        }
    }

    public void OnClickClose()
    {
        itemListPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
    }
    public void OnClickAddTestItems()
  {
      ItemManager.Instance.AddItem("coffee", 1);
      ItemManager.Instance.AddItem("energyDrink", 1);
      Debug.Log("아이템 지급");
  }
}
