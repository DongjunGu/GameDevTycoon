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
        ModalGate.I.Register(this);
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

        EmployeeData targetEmp = null;
        if (!string.IsNullOrEmpty(TargetEmployeeId))
            targetEmp = EmployeeManager.Instance?.GetEmployee(TargetEmployeeId);

        var usableItems   = new System.Collections.Generic.List<(ItemChartRow row, int count)>();
        var unusableItems = new System.Collections.Generic.List<(ItemChartRow row, int count)>();

        foreach (var kv in ItemChartLoader.Cache)
        {
            int count = ItemManager.Instance.GetCount(kv.Key);
            if (count <= 0) continue;
            if (IsSlotUsable(kv.Value, targetEmp))
                usableItems.Add((kv.Value, count));
            else
                unusableItems.Add((kv.Value, count));
        }

        foreach (var (row, count) in usableItems)
            CreateSlot(row, count, true);
        foreach (var (row, count) in unusableItems)
            CreateSlot(row, count, false);
    }

    void CreateSlot(ItemChartRow row, int count, bool usable)
    {
        var go   = Instantiate(itemSlotPrefab, slotParent);
        var slot = go.GetComponent<ItemSlotUI>();
        slot.Setup(row, count, usable);
    }

    static bool IsSlotUsable(ItemChartRow row, EmployeeData emp)
    {
        if (!ItemManager.IsUsableNow(row)) return false;
        if (IsEventReadyCategory(row)) return false;
        if (IsGameUpgradeAlreadyUsed(row)) return false;
        if (emp != null && IsWrongRoleUpgrade(row, emp)) return false;
        if (IsRelaxButNoDebuffedEmployee(row)) return false;
        if (IsUpgradeButNoRoleEmployee(row)) return false;
        if (IsCreativityBlockCategory(row)) return false;
        return true;
    }

    static bool IsEventReadyCategory(ItemChartRow row)
        => row != null && row.category == "이벤트 대비";

    // 창의성 블록 카테고리(랜덤/전설의 블록): 아이템창에서는 항상 비활성. 실제 사용은 창의성 미니게임의 BlockItemPanel에서만.
    static bool IsCreativityBlockCategory(ItemChartRow row)
        => row != null && row.category == "창의성 블록";

    static bool IsGameUpgradeAlreadyUsed(ItemChartRow row)
    {
        if (row == null || row.category != "게임") return false;
        return DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsGameUpgradeUsed(row.itemId);
    }

    static bool IsRelaxButNoDebuffedEmployee(ItemChartRow row)
    {
        if (row == null || row.itemId != "relax") return false;
        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return true;
        foreach (var emp in employees)
            if (emp.HasAnyStatDebuff()) return false;
        return true;
    }

    static bool IsWrongRoleUpgrade(ItemChartRow row, EmployeeData emp)
        => row.itemId switch
        {
            "upgradeDevelop" => emp.role != EmployeeRole.Programmer,
            "upgradeArt"     => emp.role != EmployeeRole.Artist,
            "upgradePlan"    => emp.role != EmployeeRole.Planner,
            _ => false
        };

    // 직군 업그레이드권: 회사에 해당 직군 직원이 한 명도 없으면 (카드 컨텍스트가 아니어도) 애초에 사용 불가
    static bool IsUpgradeButNoRoleEmployee(ItemChartRow row)
    {
        if (row == null) return false;
        EmployeeRole? role = row.itemId switch
        {
            "upgradeDevelop" => EmployeeRole.Programmer,
            "upgradeArt"     => EmployeeRole.Artist,
            "upgradePlan"    => EmployeeRole.Planner,
            _ => (EmployeeRole?)null
        };
        if (!role.HasValue) return false;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return true;
        foreach (var emp in employees)
            if (emp.role == role.Value) return false;
        return true;
    }

    public void OnClickClose()
    {
        itemListPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);

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
