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
           100,  // 0→1
           150,  // 1→2
           200,  // 2→3
           250,  // 3→4
           300,  // 4→5
           400,  // 5→6
           500,  // 6→7
           600,  // 7→8
           700,  // 8→9
           800,  // 9→10
           900,  // 10→11
         2_000,  // 11→12
         3_000,  // 12→13
         4_000,  // 13→14
         5_000,  // 14→15
        12_000,  // 15→16
        14_000,  // 16→17
        16_000,  // 17→18
        18_000,  // 18→19
        18_000,  // 19→20
        40_000,  // 20→21
        45_000,  // 21→22
        50_000,  // 22→23
       100_000,  // 23→24
       150_000,  // 24→25
    };

    // 강화 확률 테이블 [현재 레벨] = (성공%, 유지%, 하락%)
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
        (45, 54f,    1f),    // 11
        (45, 53.7f,  1.3f),  // 12
        (40, 58.4f,  1.6f),  // 13
        (40, 58.1f,  1.9f),  // 14
        (35, 62.8f,  2.2f),  // 15
        (35, 62.5f,  2.5f),  // 16
        (30, 67.2f,  2.8f),  // 17
        (30, 66.8f,  3.2f),  // 18
        (30, 66.5f,  3.5f),  // 19
        (20, 76.2f,  3.8f),  // 20
        (20, 75.9f,  4.1f),  // 21
        (20, 75.6f,  4.4f),  // 22
        (10, 85.3f,  4.7f),  // 23
        ( 5, 90f,    5f),    // 24
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
                float success  = Mathf.Min(100f, r.success + bonus);
                float absorbed = success - r.success;            // 실제 증가분
                float downgrade = Mathf.Max(0f, r.downgrade - absorbed);
                float maintain  = Mathf.Max(0f, 100f - success - downgrade);
                return (success, maintain, downgrade);
            }
        }
        return r;
    }

    // 성공확률 / 실패확률 2분류 (실패 = 유지 + 하락 = 100 - 성공). 성공%는 항상 정수.
    public static int SuccessRate(EmployeeData emp) => Mathf.RoundToInt(GetRates(emp).success);
    public static int FailRate(EmployeeData emp)    => 100 - SuccessRate(emp);

    // 강화 1회 실행 — 비용 차감/저장은 호출자 책임.
    // 레벨/주스탯/부스탯/연봉/만족도를 변경한 뒤 결과(성공/유지/하락)를 반환한다.
    public static EnhanceOutcome EnhanceOnce(EmployeeData emp)
    {
        int level = emp.enhancementLevel;
        var (success, maintain, downgrade) = GetRates(emp);
        float roll = Random.Range(0f, 100f);

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
