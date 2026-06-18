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
//   itemDiscount       → MerchantShopPanelUI 구매가 -value% (중고 거래 5% / 중고 거래+ 10%) ← 구현됨
//   devCostDiscount    → ProjectSetupUI 개발금 -value% (c1) ← 구현됨
//   resignChanceReduce → RandomEventManager 사직/도주 트리거 확률 -value%p (c2) ← 구현됨
//   fatigueReduce      → GenreFatigueManager 같은 장르 연속 제작 피로도 상승량 -value (c3) ← 구현됨
//   enhanceCostDiscount→ EmployeeEnhancement 강화비 -value% (b1=5 / a1=10, 합산) ← 구현됨
//   highSatStatBonus   → EmployeeData 만족도 81+ 시 스탯 배율 +value%p (b3=5 / a4=10, 합산) ← 구현됨
//   smallScaleSaleBonus→ SalesUI 소규모 프로젝트 매출 +value% (b6) ← 구현됨
//   mediumScaleSaleBonus→ SalesUI 중형 프로젝트 매출 +value% (a5) ← 구현됨
//   largeScaleSaleBonus→ SalesUI 대작 프로젝트 매출 +value% (s7) ← 구현됨
//   perfectScoreSaleBonus→ SalesUI 평론가 100점 시 매출 +value% (s8) ← 구현됨
//   yearlySatRecover   → RandomEventManager 새 연도마다 value% 굴려 성공 시 연중 랜덤 주차에 랜덤 직원 만족도 풀 회복 (a2) ← 구현됨
//   yearlyAllRecover   → RandomEventManager 새 연도마다 value% 굴려 성공 시 연중 랜덤 주차에 직원 전원 만족도 95 회복 (s4) ← 구현됨
//   bugTolerance       → DevelopmentManager 버그 value개 이하 출시 시 패널티 면제 (a6) ← 구현됨
//   fireNoMoraleEvent  → EmployeeListUI 직접 해고를 퇴사 카운트 제외 → 불안정 회사/만족도 이벤트 차단 (s1) ← 구현됨
//   highEnhanceSuccess → EmployeeEnhancement 15성+ 강화 성공 확률 +value%p (s3) ← 구현됨
//   monthlySatDropReduce→ EmployeeManager 주기 만족도 하락량 -value (s5) ← 구현됨
//   (b5 = startItem_blockRandom → 위 startItem_<itemId> 경로 재사용, 별도 코드 없음)
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

    // ──────────── 아이템 할인 (중고 거래 / 중고 거래+) ────────────
    // 장착된 itemDiscount 합 % 반환 (예: 중고거래 5 + 중고거래+ 10 동시 장착 = 15). 0~90 클램프.
    public static int GetItemDiscountPct() => Mathf.Clamp(SumEquipped("itemDiscount"), 0, 90);

    // 원가에 할인 적용한 최종 구매가 (최소 1G 보장). 할인 미장착이면 원가 그대로.
    public static int ApplyItemDiscount(int basePrice)
    {
        if (basePrice <= 0) return basePrice;
        int pct = GetItemDiscountPct();
        if (pct <= 0) return basePrice;
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * (1f - pct / 100f)));
    }

    // ──────────── 개발금 할인 (c1) ────────────
    // 장착된 devCostDiscount 합 % (0~90 클램프). 개발금에 적용.
    public static int GetDevCostDiscountPct() => Mathf.Clamp(SumEquipped("devCostDiscount"), 0, 90);

    // 원가에 할인 적용한 최종 개발금 (최소 0G). 할인 미장착이면 원가 그대로.
    public static int ApplyDevCostDiscount(int baseCost)
    {
        if (baseCost <= 0) return baseCost;
        int pct = GetDevCostDiscountPct();
        if (pct <= 0) return baseCost;
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * (1f - pct / 100f)));
    }

    // ──────────── 퇴사 확률 감소 (c2) ────────────
    // 장착된 resignChanceReduce 합을 확률(0~1)로 반환 (10 → 0.10). 사직/도주 트리거 확률에서 차감.
    public static float GetResignChanceReduce() => SumEquipped("resignChanceReduce") / 100f;

    // ──────────── 장르 피로도 상승 감소 (c3) ────────────
    // 같은 장르 연속 제작 시 피로도 상승량에서 차감할 값. 장착 합 반환.
    public static int GetSameGenreFatigueReduce() => SumEquipped("fatigueReduce");

    // ──────────── 강화비 할인 (b1) ────────────
    // 장착된 enhanceCostDiscount 합 % (0~90 클램프). 강화 비용에 적용.
    public static int GetEnhanceCostDiscountPct() => Mathf.Clamp(SumEquipped("enhanceCostDiscount"), 0, 90);

    // 원가에 할인 적용한 최종 강화비 (최소 1G 보장). 할인 미장착이면 원가 그대로.
    public static int ApplyEnhanceCostDiscount(int baseCost)
    {
        if (baseCost <= 0) return baseCost;
        int pct = GetEnhanceCostDiscountPct();
        if (pct <= 0) return baseCost;
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * (1f - pct / 100f)));
    }

    // ──────────── 고만족 스탯 보너스 (b3 / a4) ────────────
    // 만족도 81 이상일 때 스탯 배율에 가산할 값 합 (b3: +5% / a4: +10%, 동시 장착 +15%).
    // EmployeeData.GetSatisfactionMultiplier 에서 사용.
    public static float GetHighSatStatBonus(int satisfaction)
        => satisfaction >= 81 ? SumEquipped("highSatStatBonus") / 100f : 0f;

    // ──────────── 규모별 매출 보너스 (b6 / a5 / s7) ────────────
    // 소규모(ProjectScale.Small) 프로젝트 매출 bonusSum 에 가산할 값 (5 → 0.05).
    public static float GetSmallScaleSaleBonus() => SumEquipped("smallScaleSaleBonus") / 100f;
    // 중형(ProjectScale.Medium) 프로젝트 매출 bonusSum 에 가산할 값 (5 → 0.05).
    public static float GetMediumScaleSaleBonus() => SumEquipped("mediumScaleSaleBonus") / 100f;
    // 대작(ProjectScale.Large) 프로젝트 매출 bonusSum 에 가산할 값 (5 → 0.05).
    public static float GetLargeScaleSaleBonus() => SumEquipped("largeScaleSaleBonus") / 100f;

    // ──────────── 평론가 만점 매출 보너스 (s8) ────────────
    // 평론가 점수 100점일 때 매출 bonusSum 에 가산할 값 (5 → 0.05). SalesUI 에서 100점 여부 판정 후 사용.
    public static float GetPerfectScoreSaleBonus() => SumEquipped("perfectScoreSaleBonus") / 100f;

    // ──────────── 버그 허용치 (a6) ────────────
    // 출시 시 이 개수 이하의 버그면 패널티 무조건 면제. 장착 합 반환(미장착 0).
    public static int GetBugTolerance() => SumEquipped("bugTolerance");

    // ──────────── 해고 만족도 이벤트 면제 (s1) ────────────
    // true 면 플레이어의 직접 해고가 연간 퇴사 카운트(YearlyExitCount)에 잡히지 않음
    // → '불안정한 회사 → 불안감 조성'(남은 직원 만족도 -5) 이벤트로 이어지지 않음.
    public static bool HasFireMoraleImmunity() => HasEquipped("fireNoMoraleEvent");

    // ──────────── 15성+ 강화 성공 확률 (s3) ────────────
    // 강화레벨 15 이상에서 성공 확률에 가산할 %p (장착 합). EmployeeEnhancement.GetRates 에서 사용.
    public static int GetHighEnhanceSuccessBonus() => SumEquipped("highEnhanceSuccess");

    // ──────────── 월간 만족도 감소량 감소 (s5) ────────────
    // 주기 만족도 하락량에서 차감할 값 (장착 합). EmployeeManager.OnWeekPassed 에서 sat_mental 과 합산.
    public static int GetMonthlySatDropReduce() => SumEquipped("monthlySatDropReduce");

    // ──────────── 연 1회 만족도 풀 회복 (a2) ────────────
    // 발동 여부는 RandomEventManager 가 새 연도마다 이 확률(%)로 1회 굴려, 성공 시 연중 랜덤 주차에 예약.
    // 미장착이면 0.
    public static int GetYearlySatRecoverChancePct() => SumEquipped("yearlySatRecover");

    // 예약된 주차 도달 시 실제 적용 — 랜덤 직원 1명 만족도 풀 회복. 적용 시 true + 직원 반환.
    public static bool DoYearlySatRecover(out EmployeeData recovered)
    {
        recovered = null;
        var emp = PickRandomEmployee();
        if (emp == null) return false;

        int before = emp.satisfaction;
        emp.ChangeSatisfaction(100); // +100 → 1~100 클램프로 풀 회복
        EmployeeManager.Instance?.UpdateEmployee(emp);
        recovered = emp;
        Debug.Log($"[TraitEffect] yearlySatRecover 발동 - {emp.employeeName} 만족도 {before}→{emp.satisfaction}");
        return true;
    }

    // ──────────── 연 1회 직원 전원 만족도 95 회복 (s4) ────────────
    // a2 와 독립적으로 RandomEventManager 가 새 연도마다 이 확률(%)로 굴려 연중 랜덤 주차에 예약. 미장착 0.
    public static int GetYearlyAllRecoverChancePct() => SumEquipped("yearlyAllRecover");

    // 예약 주차 도달 시 적용 — 보유 직원 전원 만족도 95 이상으로 상향(이미 95+ 면 유지). 1명이라도 적용 시 true.
    public static bool DoYearlyAllRecover()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null || em.ownedEmployees.Count == 0) return false;

        bool any = false;
        foreach (var emp in em.ownedEmployees)
        {
            if (emp.satisfaction >= 95) continue;
            emp.ChangeSatisfaction(95 - emp.satisfaction); // 95 로 상향 (하향 없음)
            any = true;
        }
        if (any) Debug.Log("[TraitEffect] yearlyAllRecover 발동 - 직원 전원 만족도 95 회복");
        return any;
    }

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
