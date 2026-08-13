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

    // 보유 아이템 중 랜덤 1개 ID를 골라서 반환. 인벤토리가 비어있으면 null. 차감하지 않음.
    public string PickRandomItemId()
    {
        var owned = new List<string>();
        foreach (var kv in _inventory)
            if (kv.Value > 0) owned.Add(kv.Key);
        return owned.Count == 0 ? null : owned[UnityEngine.Random.Range(0, owned.Count)];
    }

    // 특정 아이템 1개를 강제 차감하고 저장. 도난 등 사용 조건 무시.
    public void StealSpecificItem(string itemId)
    {
        if (!_inventory.TryGetValue(itemId, out int count) || count <= 0) return;
        _inventory[itemId]--;
        if (_inventory[itemId] <= 0) _inventory.Remove(itemId);
        Save();
        if (ItemPanelUI.Instance != null) ItemPanelUI.Instance.Refresh();
    }

    public void AddItem(string itemId, int count = 1)
    {
        _inventory.TryGetValue(itemId, out int cur);
        _inventory[itemId] = cur + count;
        Save();
        // Unity 널 검사(!=) — 파괴된 stale Instance 면 ?. 가 통과해 크래시하므로 명시 검사.
        if (ItemPanelUI.Instance != null) ItemPanelUI.Instance.Refresh();

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
        if (ItemPanelUI.Instance != null) ItemPanelUI.Instance.Refresh();

        if (itemId == "coffee")
            RandomEventManager.Instance?.ScheduleCoffeeRequestEvent();
        else if (itemId == "energyDrink")
            RandomEventManager.Instance?.ScheduleEnergyDrinkRequestEvent();
    }

    // ── 사용 가능 여부 ────────────────────────────────────────
    // 카테고리별 사용 조건: 강화/만족도/이벤트 대비/테크트리 포인트는 항상 사용 가능,
    // 그 외(게임/능력치/창의성 블록)는 프로젝트 진행중에만.
    public static bool IsAlwaysUsableCategory(string category)
        => category == "강화" || category == "만족도" || category == "이벤트 대비" || category == "테크트리 포인트";

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
            case "techNote":
            {
                // 오래된 연구노트 — 테크트리 포인트 1 획득 (인게임 TechPoint = UserMoney.point)
                if (TechTreeManager.Instance == null) return false;
                TechTreeManager.Instance.AddPoints(1);
                // ItemPanelUI가 열려있는 동안 ModalGate를 쥐고 있어 Show()의 기본 WhenFree 대기가 패널을 닫을
                // 때까지 표시를 미룬다. bypassGate:true로 게이트 대기 없이 즉시 표시.
                AlertUI.Instance?.Show("오래된 연구노트를 해독했습니다.\n테크트리 포인트 +1", null, bypassGate: true);
                return true;
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
        AlertUI.Instance?.ShowPortrait($"{emp.employeeName}가 게임성을 업그레이드 했습니다.\n{roleName}점수 +{rounded}", emp.portraitId, emp.employeeName);
    }

    // 잠 깨우기 (천재 GeniusUnique 전용 이벤트): 개발 진행 중 Unique+ 천재에게 커피 사용 시
    // 개발 업그레이드권과 동일하게 팀장 점수의 1/4 추가. 프로젝트당 1회(geniusWakeup 키).
    // 조건 충족 시 CharacterUniqueEvents.Trigger 로 위임 → EventChoicePanel 표시 + ApplyEffect(점수+마킹) + 4-set.
    void TryGeniusWakeUp(EmployeeData emp)
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null || !dm.IsStarted) return;                                  // 개발 진행 중에만
        if (emp.grade < EmployeeGrade.Unique) return;                             // 전용 이벤트 = Unique+
        if (CharacterTraitApplier.ResolveEventType(emp) != "GeniusUnique") return;
        if (dm.IsGameUpgradeUsed("geniusWakeup")) return;                         // 프로젝트당 1회 (마킹은 ApplyEffect 에서)

        CharacterUniqueEvents.Trigger(emp);
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
                AlertUI.Instance?.ShowPortrait($"{target.employeeName}에게 {row.name}을 사용했습니다.\n만족도 +{row.effectValue}", target.portraitId, target.employeeName);
                break;
            // 수상한 물약 — effectValue를 상한으로 1~effectValue 사이 랜덤 회복.
            case "satisfactionRandom":
                int randomGain = Random.Range(1, row.effectValue + 1);
                target.ChangeSatisfaction(randomGain);
                OfficeManager.Instance?.ShowStatPopup(
                    target.id, $"만족도 +{randomGain}", new Color(1f, 0.4f, 0.4f));
                AlertUI.Instance?.ShowPortrait($"{target.employeeName}에게 {row.name}을 사용했습니다.\n만족도 +{randomGain}", target.portraitId, target.employeeName);
                break;
            // 초심 회복기 — 강화 굴림과 무관한 즉시효과. 강화 버튼을 누를 필요 없이 사용 즉시 강화 단계를
            // 1 낮추고(일반 하락과 동일하게 EmployeeManager.ReverseEnhancement로 그 레벨의 스탯 증가분도
            // 되돌림) 만족도를 100으로 회복한다(+100 델타는 클램프[1,100]에 의해 항상 정확히 100이 됨).
            case "enhanceResetSpirit":
            {
                int oldLevel = target.enhancementLevel;
                if (oldLevel > 0)
                {
                    EmployeeManager.Instance.ReverseEnhancement(target, oldLevel);
                    target.enhancementLevel = oldLevel - 1;
                }
                target.ChangeSatisfaction(100);
                OfficeManager.Instance?.ShowStatPopup(
                    target.id, "만족도 100 회복", new Color(1f, 0.4f, 0.4f));
                AlertUI.Instance?.ShowPortrait(
                    $"{target.employeeName}에게 {row.name}을 사용했습니다.\n강화 단계가 1 하락하고 만족도가 100으로 회복되었습니다.",
                    target.portraitId, target.employeeName);
                EmployeeListUI.Instance?.RefreshSlotLevelText(target.id);
                break;
            }
        }

        // 라꾸라꾸: 디버프 스택 모두 회복 (만족도 multiplier 는 손대지 않음)
        if (itemId == "relax")
        {
            target.ClearAllStatDebuffs();
            OfficeManager.Instance?.ShowStatPopup(
                target.id, "디버프 회복", new Color(0.5f, 1f, 0.5f));
            AlertUI.Instance?.ShowPortrait($"{target.employeeName}의 능력치 디버프가\n모두 회복됐습니다.", target.portraitId, target.employeeName);
        }

        // 각성의 물약: 4~8주간 +10% 버프 스택 추가. 기존 버프와 합연산 (20% + 10% → 30%).
        if (itemId == "awaken")
        {
            int weeks = Random.Range(4, 9);
            target.ApplyStatBuff(weeks, 10);
            OfficeManager.Instance?.ShowStatPopup(
                target.id, $"능력치 +10% ({weeks}주)", new Color(1f, 0.9f, 0.3f));
            AlertUI.Instance?.ShowPortrait($"{target.employeeName}에게 각성의 물약을 사용했습니다.\n능력치 +10% ({weeks}주간)", target.portraitId, target.employeeName);
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

        // 잠 깨우기 (천재 GeniusUnique): 개발 중 Unique+ 천재에게 커피 사용 시 팀장점수 1/4 적용 (프로젝트당 1회)
        if (itemId == "coffee")
            TryGeniusWakeUp(target);

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
            // ⚠️ 예전엔 "grade==1은 스킵"으로 강화권 4종(전부 죽은 효과였을 당시 다 같이 1등급)을 걸렀는데,
            // 2026-08-14 가격 등급 재조정으로 grade가 등급별 가격(하=1~최상=4)을 뜻하게 되면서 라꾸라꾸/랜덤
            // 블록처럼 무관한 아이템까지 같이 걸러지는 부작용이 생겼다. 강화권도 이제 전부 실제 효과가
            // 구현돼 있으니 grade 기준 스킵 자체를 없애고 전 아이템을 지급한다.
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
