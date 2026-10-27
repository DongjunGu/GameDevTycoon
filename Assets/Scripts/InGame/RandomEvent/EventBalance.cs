using System.Collections.Generic;
using UnityEngine;

// 이벤트_밸런싱_가이드.md 공용 계산 — R_ref(기준 매출) / 사무실 단계 / 버프 블록 기간 L / μ_part / 지연.
// 사무실 단계 = StageManager.CurrentStage (1~4 클램프). 단계별 값은 ByStage(1단계, 2단계, 3단계, 4단계) 순.
public static class EventBalance
{
    public static int Stage => Mathf.Clamp(StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1, 1, 4);

    public static T ByStage<T>(T s1, T s2, T s3, T s4) => Stage switch { 1 => s1, 2 => s2, 3 => s3, _ => s4 };

    // 규모배율 — 사무실 단계 1/2/3/4 → 1 / 2.9 / 8.3 / 13.8 (가이드 §2)
    public static float ScaleMultOfStage(int stage) => Mathf.Clamp(stage, 1, 4) switch { 1 => 1f, 2 => 2.9f, 3 => 8.3f, _ => 13.8f };

    // 출시 당시 사무실 단계 — officeStage 저장 전(구버전) 행은 게임 규모로 추정 (소1 / 중2 / 대3)
    static int StageOfProject(CompletedProjectData p) =>
        p.officeStage > 0 ? p.officeStage : (ProjectScale)p.scale switch { ProjectScale.Medium => 2, ProjectScale.Large => 3, _ => 1 };

    // R_ref = 최근 판매완료작 매출 × (현재 규모배율 ÷ 그 작품 규모배율). 판매완료작 없으면 총 연봉 × 1.5.
    // 판매 중에 다음 개발을 시작하면 최신작 매출이 아직 0 이라 매출 > 0 인 가장 최근 작품을 쓴다.
    public static float RRef()
    {
        var list = CompletedProjectManager.Instance?.completedProjects;
        if (list != null)
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].totalRevenue > 0)
                    return list[i].totalRevenue * ScaleMultOfStage(Stage) / ScaleMultOfStage(StageOfProject(list[i]));
        return (EmployeeManager.Instance != null ? EmployeeManager.Instance.GetTotalSalary() : 0) * 1.5f;
    }

    // 돈 = R_ref × pct%
    public static int Money(float pct) => Mathf.Max(1, Mathf.RoundToInt(RRef() * pct / 100f));

    // N = 직원 수 (CEO 는 ownedEmployees 에 없음)
    public static int N => EmployeeManager.Instance != null ? EmployeeManager.Instance.ownedEmployees.Count : 0;

    // T = 사무실 단계 기준 개발 기간 (1단계 16 / 2단계 24 / 3·4단계 32주)
    public static int T => ByStage(16, 24, 32, 32);

    // 개인 버프 1블록 기간 L = max(0.6, 0.2N) × T. blocks = 0.5(½블록) / 1 / 2 등
    public static int PersonalWeeks(float blocks = 1f) =>
        Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.6f, 0.2f * N) * T * blocks));

    // 전 직원 버프 1블록 기간 = 0.4T (직원 2명 이하 0.6T)
    public static int AllWeeks(float blocks = 1f) =>
        Mathf.Max(1, Mathf.RoundToInt((N <= 2 ? 0.6f : 0.4f) * T * blocks));

    public const int BuffPercent = 20; // 개인 버프/디버프 표준 ±20% (디버프는 ApplyStatDebuff = -20% 고정)

    // 개인 버프 +20% / 디버프 -20% — blocks 만큼의 기간
    public static int Buff(EmployeeData emp, float blocks = 1f)
    {
        if (emp == null) return 0;
        int w = PersonalWeeks(blocks);
        emp.ApplyStatBuff(w, BuffPercent);
        InfoFeedUI.Instance?.ShowStatBuff(emp, w, BuffPercent, true);
        return w;
    }

    public static int Debuff(EmployeeData emp, float blocks = 1f)
    {
        if (emp == null) return 0;
        int w = PersonalWeeks(blocks);
        emp.ApplyStatDebuff(w);
        InfoFeedUI.Instance?.ShowStatBuff(emp, w, BuffPercent, false);
        return w;
    }

    public static int ChangeSat(EmployeeData emp, int delta)
    {
        if (emp == null) return 0;
        int before = emp.satisfaction;
        emp.ChangeSatisfaction(delta);
        InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
        return emp.satisfaction - before;
    }

    public static void ChangeAllSat(int delta)
    {
        EmployeeManager.Instance.ChangeAllSatisfaction(delta);
        InfoFeedUI.Instance?.ShowGlobalSatisfaction(delta);
    }

    // ── 지연 ──
    // 규모별 지연 주수 (소 1 / 중 2 / 대 3)
    public static int ScaleDelayWeeks => ProjectSetupUI.SelectedScale switch
    {
        ProjectScale.Medium => 2,
        ProjectScale.Large  => 3,
        _ => 1
    };

    public static float SecondsPerWeek => ProjectSetupUI.SelectedScale switch
    {
        ProjectScale.Medium => GameTimeManager.BaseDevDurationSeconds / 24f,
        ProjectScale.Large  => GameTimeManager.BaseDevDurationSeconds / 32f,
        _ => GameTimeManager.BaseDevDurationSeconds / 16f
    };

    // 개발 기간 연장 N주 / 캐릭터 감속 2N주 (기존 이벤트와 동일 규칙)
    public static void Delay(EmployeeData emp, int weeks)
    {
        if (weeks <= 0 || DevelopmentManager.Instance == null) return;
        DevelopmentManager.Instance.ExtendDevelopmentDuration(weeks * SecondsPerWeek, weeks * 2f * SecondsPerWeek);
        InfoFeedUI.Instance?.ShowDevelopmentDelay(emp, weeks);
    }

    // ── 파트 점수 ──
    public static LeaderType ToPart(EmployeeRole r) => r switch
    {
        EmployeeRole.Programmer => LeaderType.Programmer,
        EmployeeRole.Artist     => LeaderType.Artist,
        _ => LeaderType.Planner
    };

    public static EmployeeRole ToRole(LeaderType t) => t switch
    {
        LeaderType.Programmer => EmployeeRole.Programmer,
        LeaderType.Artist     => EmployeeRole.Artist,
        _ => EmployeeRole.Planner
    };

    public static string PartName(EmployeeRole r) => ChallengeManager.PartDisplayName(ToPart(r));

    // μ_part = 그 파트의 기대 최종 총점 (도전과제 CalcMuSigma 와 동일)
    public static float PartMu(EmployeeRole r) => ChallengeManager.ExpectedPartTotal(ToPart(r));

    // 파트 점수 ±(μ_part × pct%) — 실제 반영량 반환. 이미 달성한 도전과제는 Resolve 후 재판정 안 하므로 되돌려지지 않는다.
    // 직원 이탈 Q (가이드 §3-8) = 공백 손실(면접 대기 3주 × 그 직원의 주간 기여) + 대체 비용(같은 성수 노말 계약금)
    // 주간 기여 = 파트 비중 × 파트 점수 100%의 가치(0.52 × R) ÷ T,  노말 계약금 = 강화 기댓값 × 0.6 (EmployeeManager.HireContractGradeMultiplier[0])
    public static int EmployeeQ(EmployeeData emp)
    {
        if (emp == null) return 0;
        float gap = RRef() * 0.52f * ChallengeManager.ContributionShare(emp) * 3f / T;
        float replace = EmployeeManager.GetExpectedEnhanceCost(emp.enhancementLevel) * 0.6f;
        return Mathf.RoundToInt(gap + replace);
    }

    public static float AddPart(EmployeeRole r, float pct) => AddPartAbs(r, PartMu(r) * pct / 100f);

    public static float AddPartAbs(EmployeeRole r, float d)
    {
        if (Mathf.Approximately(d, 0f) || DevelopmentPanelUI.Instance == null) return 0f;
        DevelopmentPanelUI.Instance.AddValues(
            r == EmployeeRole.Planner    ? d : 0f,
            r == EmployeeRole.Programmer ? d : 0f,
            r == EmployeeRole.Artist     ? d : 0f,
            0f, 0f);
        return d;
    }

    // ── 대상 선정 ──
    static bool InOffice(EmployeeData e) => DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id);

    public static List<EmployeeData> Present(System.Predicate<EmployeeData> filter = null)
    {
        var list = new List<EmployeeData>();
        if (EmployeeManager.Instance == null) return list;
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
            if (InOffice(e) && (filter == null || filter(e))) list.Add(e);
        return list;
    }

    public static EmployeeData PickPresent(System.Predicate<EmployeeData> filter = null)
    {
        var list = Present(filter);
        return list.Count == 0 ? null : list[Random.Range(0, list.Count)];
    }

    // 사무실에 직원이 있는 파트 중 랜덤 1개 (없으면 null)
    public static EmployeeRole? PickPartWithEmployee()
    {
        var roles = new List<EmployeeRole>();
        foreach (var r in new[] { EmployeeRole.Planner, EmployeeRole.Programmer, EmployeeRole.Artist })
            if (Present(e => e.role == r).Count > 0) roles.Add(r);
        return roles.Count == 0 ? null : roles[Random.Range(0, roles.Count)];
    }

    // 결과 팝업용 돈 태그 (코인 아이콘 + 노란색)
    public static string CoinTag(int signedAmount) =>
        $"<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>{(signedAmount >= 0 ? "+" : "")}{signedAmount:N0} G</color>";

    // 돈 지출 선택지 — 잔액 부족이면 비활성 (가이드 §4-2). conditionText 는 "자금 -N G".
    public static void SetCostChoice(RandomEventChoiceOption c, int cost)
    {
        c.conditionText = $"자금 -{cost:N0} G";
        c.disabled = MoneyManager.Instance == null || MoneyManager.Instance.Gold < cost;
    }

    // 잠재력 +1 (S 는 불가 → false)
    public static bool RaisePotential(EmployeeData emp)
    {
        if (emp == null || emp.potential >= EmployeePotential.S) return false;
        emp.potential = (EmployeePotential)((int)emp.potential + 1);
        EmployeeManager.Instance.UpdateEmployee(emp);
        return true;
    }
}
