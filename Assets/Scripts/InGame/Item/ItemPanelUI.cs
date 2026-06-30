using UnityEngine;

public class ItemPanelUI : MonoBehaviour
{
    public static ItemPanelUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject itemListPanel;

    [Header("List")]
    public Transform  slotParent;           // 아이템 슬롯 부모 (GridLayoutGroup)
    public GameObject itemSlotPrefab;
    [Tooltip("배경 전용 부모 — slotParent 뒤에 동일 크기·GridLayoutGroup으로 겹쳐 배치. 비우면 slotParent 에 함께 생성")]
    public Transform  slotBackgroundParent;
    [Tooltip("아이템 슬롯 아래 깔아둘 배경 프리팹")]
    public GameObject slotBackgroundPrefab;
    [Tooltip("배경 프리팹 생성 개수")]
    public int        slotBackgroundCount = 6;

    // 카드 컨텍스트로 열렸을 때 사용 대상 직원 (null이면 일반 플로우)
    public string TargetEmployeeId { get; private set; }
    private System.Action _onClosedCallback;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        itemListPanel.SetActive(false);
    }

    // 씬 전환으로 파괴될 때 static Instance 에 stale(파괴된) 참조가 남지 않도록 해제.
    // (남으면 런 시작 등 다른 씬에서 ItemPanelUI.Instance?.Refresh() 가 파괴된 객체를 건드려 MissingReferenceException)
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        ItemDetailUI.Instance?.HideDetail();
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
        // slotParent 자식 청소 — BgContent(slotBackgroundParent)는 유지
        foreach (Transform child in slotParent)
        {
            if (slotBackgroundParent != null && child == slotBackgroundParent) continue;
            Destroy(child.gameObject);
        }

        // 배경 슬롯 생성 (BgContent 안)
        var bgParent = slotBackgroundParent != null ? slotBackgroundParent : slotParent;
        if (slotBackgroundPrefab != null)
        {
            foreach (Transform child in bgParent) Destroy(child.gameObject);
            for (int i = 0; i < slotBackgroundCount; i++)
                Instantiate(slotBackgroundPrefab, bgParent);
        }

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

  // 디버그: 차트의 모든 아이템을 5개씩 한 번에 지급 (Save 1회)
  public void OnClickAddAllItems()
  {
      ItemManager.Instance.GiveAllItemsForTest(5);
  }
}
