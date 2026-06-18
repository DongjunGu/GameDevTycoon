using UnityEngine;

// 직원 강화의 단일 소스 — 비용/확률 테이블 + 강화 롤 실행.
// TrainingUI(리스트→강화→결과 3분할 화면)와 TrainingPanelUI(단일 화면)가 함께 사용한다.
// 주스탯/부스탯 증가 계산은 EmployeeManager.ApplyEnhancement/ReverseEnhancement 가 담당.
public enum EnhanceOutcome { Success, Maintain, Downgrade }

public static class EmployeeEnhancement
{
    // 강화 비용 테이블 [현재 레벨] = (현재 레벨 → +1 비용)
    public static readonly int[] CostTable =
    {
            50,  // 0→1
           100,  // 1→2
           100,  // 2→3
           100,  // 3→4
           150,  // 4→5
           150,  // 5→6
           150,  // 6→7
           200,  // 7→8
           200,  // 8→9
           200,  // 9→10
           250,  // 10→11
         2_000,  // 11→12
         4_000,  // 12→13
         6_000,  // 13→14
         8_000,  // 14→15
        10_000,  // 15→16
        20_000,  // 16→17
        30_000,  // 17→18
        40_000,  // 18→19
        50_000,  // 19→20
        70_000,  // 20→21
        90_000,  // 21→22
       110_000,  // 22→23
       150_000,  // 23→24
       200_000,  // 24→25
    };

    // 강화 확률 테이블 [현재 레벨] = (성공%, 유지%, 하락%)
    // 0~10: 실패 시 하락 가능, 11~24: 하락 없음(유지)
    public static readonly (int success, int maintain, int downgrade)[] RateTable =
    {
        (99,  0,  0),  // 0
        (95,  0,  5),  // 1
        (90,  0, 10),  // 2
        (85,  0, 15),  // 3
        (80,  0, 20),  // 4
        (75,  0, 25),  // 5
        (70,  0, 30),  // 6
        (65,  0, 35),  // 7
        (60,  0, 40),  // 8
        (55,  0, 45),  // 9
        (50,  0, 50),  // 10
        (30, 70,  0),  // 11
        (30, 70,  0),  // 12
        (30, 70,  0),  // 13
        (30, 70,  0),  // 14
        (20, 80,  0),  // 15
        (20, 80,  0),  // 16
        (20, 80,  0),  // 17
        (20, 80,  0),  // 18
        (15, 85,  0),  // 19
        (15, 85,  0),  // 20
        (15, 85,  0),  // 21
        (15, 85,  0),  // 22
        (10, 90,  0),  // 23
        ( 5, 95,  0),  // 24
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

    public static (int success, int maintain, int downgrade) GetRates(EmployeeData emp)
    {
        int lv = Mathf.Clamp(emp.enhancementLevel, 0, RateTable.Length - 1);
        var r = RateTable[lv];

        // 특성 's3'(highEnhanceSuccess) — 강화레벨 15 이상에서 성공 확률 +N%p (유지/하락에서 차감).
        // 표시·실제 롤이 GetRates 단일 소스라 한 곳만 보정.
        if (emp.enhancementLevel >= 15)
        {
            int bonus = TraitEffectApplier.GetHighEnhanceSuccessBonus();
            if (bonus > 0)
            {
                int success  = Mathf.Min(100, r.success + bonus);
                int absorbed = success - r.success;            // 실제 증가분
                int downgrade = Mathf.Max(0, r.downgrade - absorbed);
                int maintain  = Mathf.Max(0, 100 - success - downgrade);
                return (success, maintain, downgrade);
            }
        }
        return r;
    }

    // 성공확률 / 실패확률 2분류 (실패 = 유지 + 하락 = 100 - 성공)
    public static int SuccessRate(EmployeeData emp) => GetRates(emp).success;
    public static int FailRate(EmployeeData emp)    => 100 - GetRates(emp).success;

    // 강화 1회 실행 — 비용 차감/저장은 호출자 책임.
    // 레벨/주스탯/부스탯/연봉/만족도를 변경한 뒤 결과(성공/유지/하락)를 반환한다.
    public static EnhanceOutcome EnhanceOnce(EmployeeData emp)
    {
        int level = emp.enhancementLevel;
        var (success, maintain, downgrade) = GetRates(emp);
        int roll = Random.Range(0, 100);

        if (roll < success)
        {
            emp.enhancementLevel++;
            EmployeeManager.Instance.ApplyEnhancement(emp);
            // 테크트리 '고급 인력(sat_elite)' — 11성 이상 강화 성공 시 해당 직원 만족도 +5
            if (emp.enhancementLevel >= 11 &&
                TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("sat_elite"))
                emp.ChangeSatisfaction(+5);
            return EnhanceOutcome.Success;
        }

        if (downgrade > 0 && roll >= success + maintain)
        {
            EmployeeManager.Instance.ReverseEnhancement(emp, level);
            emp.enhancementLevel = Mathf.Max(0, level - 1);
            return EnhanceOutcome.Downgrade;
        }

        return EnhanceOutcome.Maintain;
    }
}
