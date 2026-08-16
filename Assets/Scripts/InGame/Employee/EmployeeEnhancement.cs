using UnityEngine;

// 직원 강화의 단일 소스 — 비용/확률 테이블 + 강화 롤 실행.
// TrainingUI(리스트→강화→결과 3분할 화면)와 TrainingPanelUI(단일 화면)가 함께 사용한다.
// 주스탯/부스탯 증가 계산은 EmployeeManager.ApplyEnhancement/ReverseEnhancement 가 담당.
// Protected  = 하락 판정이 나왔지만 '하락 방어권'(enhanceProtect) 자동 소모로 막힌 경우 (2026-08-14 신설).
// (초심 회복기는 강화 굴림과 무관한 즉시효과 아이템으로 재설계됨 — ItemManager.UseItem 참고, EnhanceOnce와 무관)
public enum EnhanceOutcome { Success, Maintain, Downgrade, Protected }

public static class EmployeeEnhancement
{
    // 강화 비용 테이블 [현재 레벨] = (현재 레벨 → +1 비용) — 2026-08-13 재조정
    public static readonly int[] CostTable =
    {
           200,  // 0→1
           200,  // 1→2
           400,  // 2→3
           400,  // 3→4
           600,  // 4→5
           600,  // 5→6
           800,  // 6→7
         1_000,  // 7→8
         1_300,  // 8→9
         1_600,  // 9→10
         2_000,  // 10→11
         3_400,  // 11→12
         3_800,  // 12→13
         4_300,  // 13→14
         5_000,  // 14→15
         5_800,  // 15→16
        11_000,  // 16→17
        12_000,  // 17→18
        15_000,  // 18→19
        18_000,  // 19→20
        21_000,  // 20→21
        32_000,  // 21→22
        39_000,  // 22→23
        72_000,  // 23→24
        94_000,  // 24→25
    };

    // 강화 확률 테이블 [현재 레벨] = (성공%, 유지%, 하락%) — 2026-08-13 재조정(11강 이후 하락률 상향)
    // 0~10: 하락 없음(유지), 11~24: 소폭 하락 발생. 유지/하락은 소수점 포함.
    public static readonly (float success, float maintain, float downgrade)[] RateTable =
    {
        (80, 20,     0),     // 0
        (75, 25,     0),     // 1
        (70, 30,     0),     // 2
        (65, 35,     0),     // 3
        (60, 40,     0),     // 4
        (60, 40,     0),     // 5
        (55, 45,     0),     // 6
        (55, 45,     0),     // 7
        (50, 50,     0),     // 8
        (50, 50,     0),     // 9
        (50, 50,     0),     // 10
        (45, 53f,    2f),    // 11
        (45, 52.4f,  2.6f),  // 12
        (40, 56.8f,  3.2f),  // 13
        (40, 56.2f,  3.8f),  // 14
        (40, 55.6f,  4.4f),  // 15
        (35, 60f,    5f),    // 16
        (35, 59.4f,  5.6f),  // 17
        (30, 63.6f,  6.4f),  // 18
        (30, 63f,    7f),    // 19
        (30, 62.4f,  7.6f),  // 20
        (20, 71.8f,  8.2f),  // 21
        (20, 71.2f,  8.8f),  // 22
        (10, 80.6f,  9.4f),  // 23
        ( 5, 85f,   10f),    // 24
    };

    public static int GetMaxLevel(EmployeeGrade grade) => EmployeeData.MaxEnhancementForGrade(grade);

    public static bool IsMax(EmployeeData emp) => emp.enhancementLevel >= GetMaxLevel(emp.grade);

    // 현재 레벨 → +1 강화 비용. 최대치/범위 초과면 -1.
    // 장착 특성 'b1'(enhanceCostDiscount) 적용 — 표시·차감 공통 소스라 한 곳만 할인.
    public static int GetCost(EmployeeData emp)
    {
        int lv = emp.enhancementLevel;
        if (IsMax(emp) || lv < 0 || lv >= CostTable.Length) return -1;
        return TraitEffectApplier.ApplyEnhanceCostDiscount(CostTable[lv]);
    }

    public static (float success, float maintain, float downgrade) GetRates(EmployeeData emp)
        => GetRates(emp, 0);

    // extraSuccessBoost — 하급/중급/상급 강화권 사용 시 "다음 강화 1회"에 걸리는 성공확률 가산(%p, 5/10/100).
    // 표시(미리보기)·실제 롤 양쪽에서 이 오버로드를 공유 — TrainingPanelUI가 보류 중인 강화권이 있으면
    // 그 값을 그대로 넘겨서 미리보기와 실제 결과가 항상 일치하게 한다.
    public static (float success, float maintain, float downgrade) GetRates(EmployeeData emp, int extraSuccessBoost)
    {
        int lv = Mathf.Clamp(emp.enhancementLevel, 0, RateTable.Length - 1);
        var r = RateTable[lv];

        // 특성 's3'(highEnhanceSuccess, 강화레벨 15 이상)과 강화권 보너스를 합산 — 표시·실제 롤이
        // GetRates 단일 소스라 한 곳만 보정.
        int bonus = Mathf.Max(0, extraSuccessBoost);
        if (emp.enhancementLevel >= 15)
            bonus += TraitEffectApplier.GetHighEnhanceSuccessBonus();

        if (bonus > 0)
        {
            float success  = Mathf.Min(100f, r.success + bonus);
            float absorbed = success - r.success;            // 실제 증가분
            float downgrade = Mathf.Max(0f, r.downgrade - absorbed);
            float maintain  = Mathf.Max(0f, 100f - success - downgrade);
            return (success, maintain, downgrade);
        }
        return r;
    }

    // 성공확률 / 실패확률 2분류 (실패 = 유지 + 하락 = 100 - 성공). 성공%는 항상 정수.
    public static int SuccessRate(EmployeeData emp) => Mathf.RoundToInt(GetRates(emp).success);
    public static int FailRate(EmployeeData emp)    => 100 - SuccessRate(emp);

    // 온보딩 튜토리얼 17-3 전용 — 남은 횟수만큼 실제 롤을 무조건 성공으로 우회(GetRates()로 표시되는
    // 확률 UI는 그대로 진짜 값을 보여주고, EnhanceOnce의 실제 판정만 우회한다).
    public static int ForceSuccessRemaining = 0;

    // 하락 방어권(enhanceProtect) 자동 방어가 적용되는 강화레벨 범위 — "11~24성 사이일때 사용 가능".
    const int ProtectMinLevel = 11;
    const int ProtectMaxLevel = 24;

    // 강화 1회 실행 — 비용 차감/저장은 호출자 책임.
    // successBoostPercent — 하급/중급/상급 강화권 사용 시 이번 1회에만 적용할 성공확률 가산(%p).
    // 아이템 소모는 호출자(TrainingPanelUI/EmployeeListUI)가 담당 — 여기선 순수하게 굴림에만 반영.
    // 레벨/주스탯/부스탯/연봉/만족도를 변경한 뒤 결과(성공/유지/하락/방어됨)를 반환한다.
    public static EnhanceOutcome EnhanceOnce(EmployeeData emp, int successBoostPercent = 0)
    {
        int level = emp.enhancementLevel;
        var (success, maintain, downgrade) = GetRates(emp, successBoostPercent);

        bool forceSuccess = ForceSuccessRemaining > 0;
        if (forceSuccess) ForceSuccessRemaining--;
        float roll = forceSuccess ? -1f : Random.Range(0f, 100f);

        if (roll < success)
        {
            emp.enhancementLevel++;
            EmployeeManager.Instance.ApplyEnhancement(emp);
            // 테크트리 '고급 인력(sat_elite)' — 11성 이상 강화 성공 시 해당 직원 만족도 +5
            if (emp.enhancementLevel >= 11 &&
                TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("sat_elite"))
            {
                int before = emp.satisfaction;
                emp.ChangeSatisfaction(+5);
                InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
            }
            return EnhanceOutcome.Success;
        }

        if (downgrade > 0 && roll >= success + maintain)
        {
            // 하락 방어권 — 사용 버튼이 없는 아이템(항상 비활성, ItemDetailUI 참고), 11~24성에서
            // 재고가 있으면 하락 판정이 나온 순간 자동으로 1개 소모하고 하락 자체를 무효화한다.
            if (level >= ProtectMinLevel && level <= ProtectMaxLevel
                && ItemManager.Instance != null && ItemManager.Instance.GetCount("enhanceProtect") > 0)
            {
                ItemManager.Instance.UseItemDirect("enhanceProtect");
                return EnhanceOutcome.Protected;
            }

            EmployeeManager.Instance.ReverseEnhancement(emp, level);
            emp.enhancementLevel = Mathf.Max(0, level - 1);
            return EnhanceOutcome.Downgrade;
        }

        return EnhanceOutcome.Maintain;
    }
}
