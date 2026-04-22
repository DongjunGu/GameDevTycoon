using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    private Dictionary<string, int> _inventory = new();
    private string _rowInDate = null;
    private bool   _isLoaded  = false;

    public IReadOnlyDictionary<string, int> Inventory => _inventory;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 로드 ──────────────────────────────────────────────────
    public void Load(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserItems", bro =>
        {
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    var row = rows[rows.Count - 1];
                    _rowInDate = SafeString(row, "inDate", "");
                    ParseInventory(SafeString(row, "itemsJson", "{}"));
                    _isLoaded = true;
                    Save(); // 신규 컬럼 자동 반영
                    Debug.Log($"[ItemManager] 로드 완료: {SerializeInventory()}");
                }
                else
                {
                    GiveTestItems();
                    _isLoaded = true;
                    Save();
                    Debug.Log("[ItemManager] 신규 유저 — 테스트 아이템 지급");
                }
            }
            else
            {
                Debug.LogWarning($"[ItemManager] 로드 실패: {bro}");
            }
            onComplete?.Invoke();
        });
    }

    // ── 저장 ──────────────────────────────────────────────────
    public void Save()
    {
        if (!_isLoaded) return;

        var param = new Param();
        param.Add("itemsJson", SerializeInventory());

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserItems", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[ItemManager] 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserItems", param, bro =>
            {
                if (bro.IsSuccess()) _rowInDate = bro.GetInDate();
                else Debug.LogError($"[ItemManager] Insert 실패: {bro}");
            });
        }
    }

    // ── 조회 ──────────────────────────────────────────────────
    public int GetCount(string itemId) =>
        _inventory.TryGetValue(itemId, out int count) ? count : 0;

    public void AddItem(string itemId, int count = 1)
    {
        _inventory.TryGetValue(itemId, out int cur);
        _inventory[itemId] = cur + count;
        Save();
        ItemPanelUI.Instance?.Refresh();

        if (itemId == "coffee")
            RandomEventManager.Instance?.ScheduleCoffeeRequestEvent();
        else if (itemId == "energyDrink")
            RandomEventManager.Instance?.ScheduleEnergyDrinkRequestEvent();
    }

    // ── 사용 ──────────────────────────────────────────────────

    // 아이템만 차감 (효과는 호출부에서 직접 처리)
    public bool UseItemDirect(string itemId)
    {
        if (GetCount(itemId) <= 0) return false;
        _inventory[itemId]--;
        Save();
        return true;
    }

    public bool UseItem(string itemId, EmployeeData target)
    {
        if (GetCount(itemId) <= 0) return false;

        var chart = ItemChartLoader.Cache;
        if (!chart.TryGetValue(itemId, out var row)) return false;

        _inventory[itemId]--;

        switch (row.effectType)
        {
            case "satisfaction":
                target.ChangeSatisfaction(row.effectValue);
                OfficeManager.Instance?.ShowStatPopup(
                    target.id, $"만족도 +{row.effectValue}", new Color(1f, 0.4f, 0.4f));
                break;
        }

        // 저장
        Save();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
        MoneyManager.Instance?.SaveMoney();
        EmployeeManager.Instance?.UpdateEmployee(target);

        return true;
    }

    // ── 내부 ──────────────────────────────────────────────────
    void GiveTestItems()
    {
        _inventory["coffee"]      = 3;
        _inventory["energyDrink"] = 2;
    }

    void ParseInventory(string json)
    {
        _inventory.Clear();
        try
        {
            var data = JsonMapper.ToObject(json);
            foreach (string key in data.Keys)
                if (int.TryParse(data[key].ToString(), out int val))
                    _inventory[key] = val;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ItemManager] 인벤토리 파싱 실패: {e.Message}");
        }
    }

    string SerializeInventory()
    {
        var sb = new System.Text.StringBuilder("{");
        bool first = true;
        foreach (var kv in _inventory)
        {
            if (!first) sb.Append(',');
            sb.Append($"\"{kv.Key}\":{kv.Value}");
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); } catch { return fallback; }
    }
}
