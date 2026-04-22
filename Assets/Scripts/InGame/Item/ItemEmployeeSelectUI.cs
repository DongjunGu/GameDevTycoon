using UnityEngine;

public class ItemEmployeeSelectUI : MonoBehaviour
{
    public static ItemEmployeeSelectUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject employeeSelectPanel;

    [Header("List")]
    public Transform  slotParent;
    public GameObject itemEmployeeSlotPrefab;

    private ItemChartRow _currentRow;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        employeeSelectPanel.SetActive(false);
    }

    public void Open(ItemChartRow row)
    {
        _currentRow = row;

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            var go   = Instantiate(itemEmployeeSlotPrefab, slotParent);
            var slot = go.GetComponent<ItemEmployeeSlotUI>();
            slot.Setup(emp, OnSelectEmployee);
        }

        ItemDetailUI.Instance.detailPanel.SetActive(false);
        employeeSelectPanel.SetActive(true);
    }

    void OnSelectEmployee(EmployeeData emp)
    {
        bool success = ItemManager.Instance.UseItem(_currentRow.itemId, emp);
        if (!success) return;

        employeeSelectPanel.SetActive(false);
        ItemPanelUI.Instance.itemListPanel.SetActive(true);
        ItemPanelUI.Instance.Refresh();
    }

    public void OnClickBack()
    {
        employeeSelectPanel.SetActive(false);
        ItemDetailUI.Instance.detailPanel.SetActive(true);
    }
}
