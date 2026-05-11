using UnityEngine;

// 장착 특성의 인게임 효과 적용 자리 (현재 TODO — 아웃게임만 작동)
//
// 호출 시점 (인게임 진입/특정 이벤트에서):
//   - GameScene 진입 직후: ApplyOnRunStart()
//   - 매출 정산: ApplyFirstSaleBonus()
//   - 채용 공고: ApplyRecruitApplicantsBonus()
//   - 아이템 구매: ApplyItemDiscount()
//   - 회사 자금 0 도달: ApplyBrokeRescue()
//
// effectType 분기 예시 (TODO):
//   startGold          → MoneyManager.AddMoney(value)
//   startItem_<itemId> → ItemManager.AddItem("<itemId>", value)
//   itemDiscount       → ItemManager 가격 % 할인
//   firstSaleBonus     → SalesManager 첫 매출 % 가산
//   recruitApplicants  → RecruitUI 지원자 수 +value
//   marketingFree      → MarketingManager 비용 0 override
//   brokeRescue        → MoneyManager 0 도달 트리거 → 랜덤 직원 연봉만큼 지급
public static class TraitEffectApplier
{
    // 장착된 모든 특성에 대해 run-start 효과 적용 (게임씬 진입 시 호출 예정)
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

            // TODO: effectType 별 분기 — 인게임 매니저들 연결 필요
            Debug.Log($"[TraitEffect] (TODO) {row.name} → {row.effectType}={row.effectValue}");
        }
    }
}
