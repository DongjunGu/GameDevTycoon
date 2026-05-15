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
    public void Save(System.Action onComplete = null)
    {
        if (!_isLoaded) { onComplete?.Invoke(); return; }

        var param = new Param();
        param.Add("itemsJson", SerializeInventory());

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserItems", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[ItemManager] 저장 실패: {bro}");
                onComplete?.Invoke();
            });
        }
        else
        {
            Backend.GameData.Insert("UserItems", param, bro =>
            {
                if (bro.IsSuccess()) _rowInDate = bro.GetInDate();
                else Debug.LogError($"[ItemManager] Insert 실패: {bro}");
                onComplete?.Invoke();
            });
        }
    }

    // 새 런 시작 — 인벤토리 비우고 row 덮어쓰기
    public void ResetForNewRun(System.Action onComplete = null)
    {
        _inventory.Clear();
        _isLoaded = true;
        Save(onComplete);
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

    // 메모리만 변경 — 백엔드 저장 안 함. 호출부에서 batch save 책임 짐.
    // 상인 구매처럼 여러 개를 한 번에 사고 닫힐 때 한 번만 저장하는 패턴에 사용.
    public void AddItemNoSave(string itemId, int count = 1)
    {
        _inventory.TryGetValue(itemId, out int cur);
        _inventory[itemId] = cur + count;
        ItemPanelUI.Instance?.Refresh();

        if (itemId == "coffee")
            RandomEventManager.Instance?.ScheduleCoffeeRequestEvent();
        else if (itemId == "energyDrink")
            RandomEventManager.Instance?.ScheduleEnergyDrinkRequestEvent();
    }

    // ── 사용 가능 여부 ────────────────────────────────────────
    // 카테고리별 사용 조건: 강화/만족도/이벤트 대비는 항상 사용 가능,
    // 그 외(게임/능력치/테크트리 포인트/창의성 블록)는 프로젝트 진행중에만.
    public static bool IsAlwaysUsableCategory(string category)
        => category == "강화" || category == "만족도" || category == "이벤트 대비";

    public static bool IsProjectActive()
    {
        if (DevelopmentManager.Instance == null) return false;
        var s = DevelopmentManager.Instance.CurrentStage;
        return s == ProjectStage.Developing || s == ProjectStage.BugFixing;
    }

    public static bool IsUsableNow(ItemChartRow row)
    {
        if (row == null) return false;
        if (IsAlwaysUsableCategory(row.category)) return true;
        return IsProjectActive();
    }

    // ── 사용 ──────────────────────────────────────────────────

    // 아이템만 차감 (효과는 호출부에서 직접 처리)
    public bool UseItemDirect(string itemId)
    {
        if (GetCount(itemId) <= 0) return false;
        var chart = ItemChartLoader.Cache;
        if (chart.TryGetValue(itemId, out var row) && !IsUsableNow(row))
        {
            Debug.Log($"[Item] '{row.name}' 은 프로젝트 진행중에만 사용 가능");
            return false;
        }
        _inventory[itemId]--;
        Save();
        return true;
    }

    // 대상 직원 없이 사용하는 아이템 (창의성 블록 등). 효과 적용 성공 시에만 차감.
    public bool UseItemNoTarget(string itemId)
    {
        if (GetCount(itemId) <= 0) return false;

        var chart = ItemChartLoader.Cache;
        if (!chart.TryGetValue(itemId, out var row)) return false;

        if (!IsUsableNow(row))
        {
            Debug.Log($"[Item] '{row.name}' 은 프로젝트 진행중에만 사용 가능");
            return false;
        }

        if (!ApplyNoTargetEffect(itemId)) return false;

        _inventory[itemId]--;

        // 4-set save: 인벤토리 + 프로젝트(earnedBlocks 등) + GameTime + Money
        // 창의성 블록 효과가 ProjectSaveManager.earnedBlocks 컬럼을 갱신해야 종료 후 복원됨
        Save();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
        MoneyManager.Instance?.SaveMoney();
        return true;
    }

    bool ApplyNoTargetEffect(string itemId)
    {
        switch (itemId)
        {
            case "upgradeRandom":
            {
                if (DevelopmentManager.Instance == null) return false;
                if (DevelopmentManager.Instance.IsGameUpgradeUsed("upgradeRandom"))
                {
                    Debug.Log("[Item] '랜덤 업그레이드' 는 이 프로젝트에 이미 사용됨");
                    return false;
                }
                var pool = EmployeeManager.Instance?.ownedEmployees;
                if (pool == null || pool.Count == 0)
                {
                    Debug.LogWarning("[Item] 직원이 없어 랜덤 업그레이드 적용 불가");
                    return false;
                }
                var emp = pool[Random.Range(0, pool.Count)];
                ApplyGameUpgrade(emp, emp.role, "upgradeRandom");
                return true;
            }
            case "blockRandom":
            {
                // 30% 2칸 / 70% 3칸
                var pool = Random.value < 0.30f
                    ? CreativityGameData.Blocks2
                    : CreativityGameData.Blocks3;
                if (pool == null || pool.Length == 0) return false;
                return AddBlockToCreativity(pool[Random.Range(0, pool.Length)]);
            }
            case "blockLegendary":
            {
                var dot = System.Array.Find(CreativityGameData.Blocks, b => b.name == "Dot");
                if (dot == null) { Debug.LogWarning("[Item] Dot 블록 정의 누락"); return false; }
                return AddBlockToCreativity(dot);
            }
        }
        Debug.LogWarning($"[Item] '{itemId}' 무대상 효과 미구현");
        return false;
    }

    bool AddBlockToCreativity(CreativityGameData.BlockShape shape)
    {
        var ui = CreativityGameUI.Instance;
        if (ui == null) { Debug.LogWarning("[Item] CreativityGameUI 없음"); return false; }
        ui.GrantItemBlock(shape); // 패널 열려있으면 트레이에 즉시 스폰, 닫혀있으면 _earnedBlocks 큐잉
        return true;
    }

    // 게임 카테고리 (upgradeRandom/Develop/Art/Plan) 공통 적용:
    // CalcGameUpgradeScore → AddValuesInstant → MarkGameUpgradeUsed → AlertUI
    void ApplyGameUpgrade(EmployeeData emp, EmployeeRole role, string itemId)
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null) return;

        float score = dm.CalcGameUpgradeScore(emp, role);
        int rounded = Mathf.Max(1, Mathf.RoundToInt(score));

        DevelopmentPanelUI.Instance?.AddValuesInstant(
            role == EmployeeRole.Planner    ? rounded : 0f,
            role == EmployeeRole.Programmer ? rounded : 0f,
            role == EmployeeRole.Artist     ? rounded : 0f,
            0f, 0f);

        dm.MarkGameUpgradeUsed(itemId);

        string roleName = role switch
        {
            EmployeeRole.Planner    => "기획",
            EmployeeRole.Programmer => "개발",
            EmployeeRole.Artist     => "아트",
            _ => ""
        };
        AlertUI.Instance?.Show($"{emp.employeeName}가 게임성을 업그레이드 했습니다.\n{roleName}점수 +{rounded}");
    }

    public bool UseItem(string itemId, EmployeeData target)
    {
        if (GetCount(itemId) <= 0) return false;

        var chart = ItemChartLoader.Cache;
        if (!chart.TryGetValue(itemId, out var row)) return false;

        if (!IsUsableNow(row))
        {
            Debug.Log($"[Item] '{row.name}' 은 프로젝트 진행중에만 사용 가능");
            return false;
        }

        // 게임 카테고리: 프로젝트당 1회 가드
        if (row.category == "게임" && DevelopmentManager.Instance != null
            && DevelopmentManager.Instance.IsGameUpgradeUsed(itemId))
        {
            Debug.Log($"[Item] '{row.name}' 은 이 프로젝트에 이미 사용됨");
            return false;
        }

        // 라꾸라꾸: 대상 직원에게 활성 디버프가 없으면 사용 불가 (UI 가드 통과 못한 안전망)
        if (itemId == "relax" && !target.HasAnyStatDebuff())
        {
            Debug.Log($"[Item] '{row.name}' — 대상 직원에게 활성 디버프 없음, 사용 무효");
            return false;
        }

        _inventory[itemId]--;

        switch (row.effectType)
        {
            case "satisfaction":
                target.ChangeSatisfaction(row.effectValue);
                OfficeManager.Instance?.ShowStatPopup(
                    target.id, $"만족도 +{row.effectValue}", new Color(1f, 0.4f, 0.4f));
                break;
        }

        // 라꾸라꾸: 디버프 스택 모두 회복 (만족도 multiplier 는 손대지 않음)
        if (itemId == "relax")
        {
            target.ClearAllStatDebuffs();
            OfficeManager.Instance?.ShowStatPopup(
                target.id, "디버프 회복", new Color(0.5f, 1f, 0.5f));
        }

        // 게임 카테고리 직군 아이템 → role 매핑 후 업그레이드 적용
        EmployeeRole? upgradeRole = itemId switch
        {
            "upgradeDevelop" => EmployeeRole.Programmer,
            "upgradeArt"     => EmployeeRole.Artist,
            "upgradePlan"    => EmployeeRole.Planner,
            _ => (EmployeeRole?)null
        };
        if (upgradeRole.HasValue)
            ApplyGameUpgrade(target, upgradeRole.Value, itemId);

        // 저장
        Save();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
        MoneyManager.Instance?.SaveMoney();
        EmployeeManager.Instance?.UpdateEmployee(target);

        return true;
    }

    // 디버그: 차트의 모든 아이템을 N개씩 인벤토리에 채워 넣고 한 번만 저장
    public void GiveAllItemsForTest(int countEach = 5)
    {
        foreach (var kv in ItemChartLoader.Cache)
        {
            _inventory.TryGetValue(kv.Key, out int cur);
            _inventory[kv.Key] = cur + countEach;
        }
        Save();
        ItemPanelUI.Instance?.Refresh();
        CreativityGameUI.Instance?.RefreshItemControls();
        Debug.Log($"[ItemManager] 테스트용 전체 아이템 +{countEach} 지급");
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
