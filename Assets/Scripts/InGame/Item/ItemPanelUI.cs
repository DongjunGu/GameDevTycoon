using UnityEngine;

public class ItemPanelUI : MonoBehaviour
{
    public static ItemPanelUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject itemListPanel;

    [Header("List")]
    public Transform  slotParent;
    public GameObject itemSlotPrefab;

    // 카드 컨텍스트로 열렸을 때 사용 대상 직원 (null이면 일반 플로우)
    public string TargetEmployeeId { get; private set; }
    private System.Action _onClosedCallback;

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

    // EmployeeCardUI 등에서 특정 직원에게 사용할 목적으로 호출 — 직원 선택 단계 자동 스킵
    public void OpenForEmployee(string employeeId, System.Action onClosed = null)
    {
        TargetEmployeeId = employeeId;
        _onClosedCallback = onClosed;
        Open();
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

        // 카드 컨텍스트 정리 + 콜백 호출 (있으면)
        TargetEmployeeId = null;
        var cb = _onClosedCallback;
        _onClosedCallback = null;
        cb?.Invoke();
    }
    public void OnClickAddTestItems()
  {
      ItemManager.Instance.AddItem("coffee", 1);
      ItemManager.Instance.AddItem("energyDrink", 1);
      Debug.Log("아이템 지급");
  }
}
