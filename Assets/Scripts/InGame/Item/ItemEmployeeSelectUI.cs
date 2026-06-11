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
    private EmployeeRole? _roleFilter;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        employeeSelectPanel.SetActive(false);
    }

    public void Open(ItemChartRow row, EmployeeRole? roleFilter = null)
    {
        _currentRow  = row;
        _roleFilter  = roleFilter;

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id)) continue; // 파견중 직원은 아이템 대상 제외
            if (_roleFilter.HasValue && emp.role != _roleFilter.Value) continue;
            // 라꾸라꾸: 디버프 걸린 직원만 노출
            if (_currentRow != null && _currentRow.itemId == "relax" && !emp.HasAnyStatDebuff()) continue;

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
