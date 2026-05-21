using UnityEngine;

// 장착 특성의 인게임 효과 적용
//
// 호출 시점:
//   - NewRunInitializer.FinalizeRun: ApplyOnRunStart()                          ← startGold / startItem_*
//   - NewRunInitializer.RunResets:   ResetForNewRun()                           ← 1회성 플래그(_firstSaleConsumed/_brokeRescueFired) 리셋
//   - HiringUI.OnClickTier:           GetRecruitApplicantsBonus()                ← recruitApplicants
//   - SalesUI 매출 코루틴 진입부:    ConsumeFirstSaleBonusPct()                 ← firstSaleBonus (1회만)
//   - MoneyManager.SpendGold 부족:   TryConsumeBrokeRescue(out salary, out name) ← brokeRescue (1회만)
//   - MarketingUI / DevelopmentResultUI: HasMarketingFree()                       ← marketingFree
//
// effectType 키:
//   startGold          → MoneyManager.AddGold(value)                  ← 구현됨
//   startItem_<itemId> → ItemManager.AddItem("<itemId>", value)       ← 구현됨
//   recruitApplicants  → HiringUI 지원자 수 +value                    ← 구현됨
//   firstSaleBonus     → SalesUI 첫 게임 매출 +value%                 ← 구현됨
//   brokeRescue        → MoneyManager 0 도달 시 랜덤 직원 연봉 지급   ← 구현됨 (effectValue 무시)
//   marketingFree      → MarketingUI 비용 0G + DevelopmentResultUI 1.3 보정 ← 구현됨
//   itemDiscount       → TODO (중고 거래 5% / 중고 거래+ 10%) — 상인 가격 시스템 선행 필요. 미구현
public static class TraitEffectApplier
{
    const string STARTITEM_PREFIX = "startItem_";

    // 1회성 효과 발동 플래그는 RunStateManager 의 FirstSaleConsumed / BrokeRescueFired 가 source of truth.
    // 새 런 시작/종료 시 RunStateManager.StartRun / EndRun 이 둘 다 false 로 리셋함.
    // ResetForNewRun() 은 미래의 메모리 only 효과 확장을 위해 빈 메서드로 유지.
    public static void ResetForNewRun()
    {
        // 현재 모든 1회성 플래그가 RunState 로 위임됨. 메모리 only 플래그가 생기면 여기 추가.
    }

    // ──────────── Run Start ────────────
    public static void ApplyOnRunStart()
    {
        if (OwnedTraitManager.Instance == null) return;
        var cache = TraitChartLoader.Cache;
        if (cache == null) return;

        for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
        {
            var id = OwnedTraitManager.Instance.GetEquipped(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!cache.TryGetValue(id, out var row)) continue;
            ApplyOne(row);
        }
    }

    static void ApplyOne(TraitChartRow row)
    {
        if (string.IsNullOrEmpty(row.effectType)) return;

        if (row.effectType == "startGold")              { ApplyStartGold(row);                return; }
        if (row.effectType.StartsWith(STARTITEM_PREFIX)) { ApplyStartItem(row, row.effectType.Substring(STARTITEM_PREFIX.Length)); return; }

        Debug.Log($"[TraitEffect] (RunStart 외 효과) {row.name} → {row.effectType}={row.effectValue}");
    }

    static void ApplyStartGold(TraitChartRow row)
    {
        if (row.effectValue <= 0) return;
        if (MoneyManager.Instance == null) { Debug.LogWarning($"[TraitEffect] {row.name} startGold 실패 - MoneyManager 없음"); return; }
        MoneyManager.Instance.AddGold(row.effectValue);
        Debug.Log($"[TraitEffect] {row.name} → startGold +{row.effectValue:N0}G 적용");
    }

    static void ApplyStartItem(TraitChartRow row, string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || row.effectValue <= 0) return;
        if (ItemManager.Instance == null) { Debug.LogWarning($"[TraitEffect] {row.name} startItem 실패 - ItemManager 없음"); return; }
        ItemManager.Instance.AddItem(itemId, row.effectValue);
        Debug.Log($"[TraitEffect] {row.name} → startItem_{itemId} x{row.effectValue} 적용");
    }

    // ──────────── 채용 (호객행위) ────────────
    public static int GetRecruitApplicantsBonus() => SumEquipped("recruitApplicants");

    // ──────────── 마케팅의 신 (marketingFree) ────────────
    // true 면 MarketingUI 모든 비용 0G + DevelopmentResultUI 마케팅 보정 무조건 1.3 적용
    public static bool HasMarketingFree() => HasEquipped("marketingFree");

    // ──────────── 첫 매출 (초심자의 행운) ────────────
    // 첫 SalesUI 세션 진입 시 1회 호출. 이후 같은 런에서는 0 반환.
    // 반환값 = 가산할 % (예: 10 → +10%). RunState 에 영구 저장하므로 앱 재실행 후에도 재발동 안 됨.
    public static int ConsumeFirstSaleBonusPct()
    {
        var rs = RunStateManager.Instance;
        if (rs == null) return 0;
        if (rs.FirstSaleConsumed) return 0;

        int pct = SumEquipped("firstSaleBonus");
        if (pct > 0)
        {
            rs.SetFirstSaleConsumed(true);
            Debug.Log($"[TraitEffect] firstSaleBonus +{pct}% 소비 (이번 런 1회)");
        }
        return pct;
    }

    // ──────────── 파산 직전 구제 (가난한 회사) ────────────
    // MoneyManager.SpendGold 잔액 부족 시점에 호출. 발동 가능하면 true + salary/empName 채워서 반환.
    // 미발동 조건: RunStateManager 없음 / 이번 런 이미 발동 / trait 미장착 / 직원 0명.
    // RunState 에 영구 저장하므로 앱 재실행 후에도 재발동 안 됨.
    public static bool TryConsumeBrokeRescue(out int salary, out string empName)
    {
        salary = 0; empName = "";
        var rs = RunStateManager.Instance;
        if (rs == null) return false;
        if (rs.BrokeRescueFired) return false;
        if (OwnedTraitManager.Instance == null) return false;
        var cache = TraitChartLoader.Cache;
        if (cache == null) return false;

        bool hasRescue = false;
        for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
        {
            var id = OwnedTraitManager.Instance.GetEquipped(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!cache.TryGetValue(id, out var row)) continue;
            if (row.effectType == "brokeRescue") { hasRescue = true; break; }
        }
        if (!hasRescue) return false;

        var emp = PickRandomEmployee();
        if (emp == null) return false;

        salary  = Mathf.Max(0, emp.salary);
        empName = emp.employeeName;
        rs.SetBrokeRescueFired(true);
        Debug.Log($"[TraitEffect] brokeRescue 발동 - {empName} 연봉 {salary:N0}G 지급");
        return true;
    }

    static EmployeeData PickRandomEmployee()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null || em.ownedEmployees.Count == 0) return null;
        int idx = Random.Range(0, em.ownedEmployees.Count);
        return em.ownedEmployees[idx];
    }

    // ──────────── 공통 ────────────
    static int SumEquipped(string effectType)
    {
        if (OwnedTraitManager.Instance == null) return 0;
        var cache = TraitChartLoader.Cache;
        if (cache == null) return 0;

        int sum = 0;
        for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
        {
            var id = OwnedTraitManager.Instance.GetEquipped(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!cache.TryGetValue(id, out var row)) continue;
            if (row.effectType == effectType) sum += row.effectValue;
        }
        return sum;
    }

    static bool HasEquipped(string effectType)
    {
        if (OwnedTraitManager.Instance == null) return false;
        var cache = TraitChartLoader.Cache;
        if (cache == null) return false;

        for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
        {
            var id = OwnedTraitManager.Instance.GetEquipped(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!cache.TryGetValue(id, out var row)) continue;
            if (row.effectType == effectType) return true;
        }
        return false;
    }
}
