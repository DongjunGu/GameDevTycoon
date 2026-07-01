using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Condition
{
    static Dictionary<string, RandomEventConditionChartRow> Chart =>
        RandomEventConditionChartLoader.Cache;

    static string[] GetDescs(string key) =>
        Chart != null && Chart.TryGetValue(key, out var r) && r.descriptions.Length > 0
            ? r.descriptions : null;

    public static string GetTitle(string key) =>
        Chart != null && Chart.TryGetValue(key, out var r) ? r.title : null;

    public static string GetSystemMessage(string key) =>
        Chart != null && Chart.TryGetValue(key, out var r) ? r.systemMessage : null;

    // ── 도망 메시지 ───────────────────────────────────────────────
    // "책상 위에 놓인 사원증이 반으로 쪼개져 있습니다."
    // "프로필 상태가 '구직 중'으로 바뀌었습니다."
    // "책상 위에 포스트잇 한 장이 붙어 있습니다.\n'회사 탈출은 지능순'"
    public static string GetRunAwayMessage()
    {
        var descs = GetDescs("EmployeeRun");
        return descs != null ? descs[Random.Range(0, descs.Length)] : "";
    }

    // ── Register ─────────────────────────────────────────────────
    public static void Register(List<RandomEventData> pool, RandomEventManager mgr,
                                Dictionary<string, RandomEventChartRow> chart = null)
    {
    }

    // ── 불안한 회사 (wrapper) ─────────────────────────────────────
    public static bool CheckUnstableCompanyOnNewYear(RandomEventManager mgr, int newYear)
    {
        if (mgr.HiringPenaltyEndYear >= 0 && newYear >= mgr.HiringPenaltyEndYear)
        {
            mgr.HiringPenalty        = 0;
            mgr.HiringPenaltyEndYear = -1;
            Debug.Log("[RandomEvents_Condition] 채용 패널티 만료");
        }

        int exitCount = EmployeeManager.Instance?.YearlyExitCount ?? 0;
        if (exitCount >= 2 && Random.value < 0.5f)
        {
            mgr.ScheduleUnstableCompanyEvent();
            return true;
        }
        return false;
    }

    public static void TriggerUnstableCompanyEvent(RandomEventManager mgr, int currentYear)
    {
        if (Random.value < 0.5f)
            TriggerBadRumorEvent(mgr, currentYear);
        else
            TriggerAnxietyInducingEvent();
    }

    // ── 안좋은 소문 ───────────────────────────────────────────────
    // sys: "1년간 채용 지원 인원 1명 감소"
    public static void TriggerBadRumorEvent(RandomEventManager mgr, int currentYear)
    {
        mgr.HiringPenalty        = Mathf.Max(1, mgr.HiringPenalty + 1);
        mgr.HiringPenaltyEndYear = currentYear + 1;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("BadRumor", out row);

        string sysMsg = row?.systemMessage ?? "1년간 채용 지원 인원 1명 감소";

        RandomEventUI.Instance.Show(
            row?.title ?? "안좋은 소문",
            row?.portraitId ?? "",
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            sysMsg,
            null,
            () =>
            {
                GameTimeManager.Instance?.ForceStartTime();
                GameTimeManager.Instance?.SaveGameTime();
            }
        );
    }

    // ── 불안감 조성 ───────────────────────────────────────────────
    // sys:  "{직원이름} 만족도 -5"
    // sys2: "{직원이름} 능력치 n주 동안 -10%"  (1·2단계 5~15주 / 3·4단계 10~20주)
    public static void TriggerAnxietyInducingEvent()
    {
        var all = EmployeeManager.Instance?.ownedEmployees;
        if (all == null || all.Count == 0) return;

        var employees = new List<EmployeeData>();
        foreach (var e in all)
            if (DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id)) employees.Add(e);
        if (employees.Count == 0) return;

        var emp = employees[Random.Range(0, employees.Count)];

        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        int n = stage <= 2 ? Random.Range(5, 16) : Random.Range(10, 21);

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("AnxietyInducing", out row);

        string sysMsg = !string.IsNullOrEmpty(row?.systemMessage)
            ? row.systemMessage.Replace("{직원이름}", emp.employeeName)
            : $"{emp.employeeName} 만족도 -5";
        string sysMsg2 = !string.IsNullOrEmpty(row?.systemMessage2)
            ? row.systemMessage2.Replace("{직원이름}", emp.employeeName).Replace("n주", $"{n}주")
            : $"{emp.employeeName} 능력치 {n}주 동안 -10%";

        RandomEventUI.Instance.Show(
            row?.title ?? "불안감 조성",
            emp.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[Random.Range(0, row.descriptions.Length)] : "",
            sysMsg,
            sysMsg2,
            () =>
            {
                emp.ChangeSatisfaction(-5);
                emp.ApplyStatBuff(n, -10);
                EmployeeManager.Instance.UpdateEmployee(emp);
                OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -5", new Color(0.4f, 0.6f, 1f));
                OfficeManager.Instance?.ShowStatPopup(emp.id, $"능력치 {n}주 -10%", new Color(0.4f, 0.6f, 1f));
                GameTimeManager.Instance?.ForceStartTime();
                GameTimeManager.Instance?.SaveGameTime();
            }
        );
    }

    // ── 회사 평점 1점 ─────────────────────────────────────────────
    // sys: "최악의 리뷰로 인해 지원자들의 발길이 끊깁니다.."
    public static void TriggerCompanyBadReviewEvent(RandomEventManager mgr, int currentYear)
    {
        mgr.HiringPenalty        = Mathf.Max(1, mgr.HiringPenalty + 1);
        mgr.HiringPenaltyEndYear = currentYear + 1;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("CompanyBadReview", out row);

        string sysMsg = row?.systemMessage ?? "최악의 리뷰로 인해 지원자들의 발길이 끊깁니다..";

        RandomEventUI.Instance.Show(
            row?.title ?? "회사 평점 1점",
            row?.portraitId ?? "",
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            sysMsg,
            null,
            () => GameTimeManager.Instance?.SaveGameTime()
        );
    }

    // ── 사내 연애 ─────────────────────────────────────────────────
    // sys:  "{이름1}, {이름2} 만족도 +10"
    // sys2: "{이름1}, {이름2} 능력치 n주 동안 +10%"  (1·2단계 5~15주 / 3·4단계 10~20주)
    // desc1: 기존 직원이 남성일 때 ("사장님 저 {상대이름}과 사귀기로 했습니다! 만세!")
    // desc2: 기존 직원이 여성일 때 ("{상대이름}님이랑 오늘부터 1일이에요 축하해주세요!")
    public static void TriggerOfficeRomanceEvent(RandomEventManager mgr,
                                                  string newEmpId, string existingEmpId)
    {
        var newEmp      = EmployeeManager.Instance?.GetEmployee(newEmpId);
        var existingEmp = EmployeeManager.Instance?.GetEmployee(existingEmpId);
        if (newEmp == null || existingEmp == null) return;

        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        int n = stage <= 2 ? Random.Range(5, 16) : Random.Range(10, 21);

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("OfficeRomance", out row);

        string desc = (!existingEmp.isFemale
            ? (row?.descriptions?.Length > 0 ? row.descriptions[0] : "")
            : (row?.descriptions?.Length > 1 ? row.descriptions[1] : ""))
            .Replace("{상대이름}", newEmp.employeeName);

        string sysMsg = (row?.systemMessage ?? "")
            .Replace("{이름1}", existingEmp.employeeName)
            .Replace("{이름2}", newEmp.employeeName);
        string sysMsg2 = !string.IsNullOrEmpty(row?.systemMessage2)
            ? row.systemMessage2
                .Replace("{이름1}", existingEmp.employeeName)
                .Replace("{이름2}", newEmp.employeeName)
                .Replace("n주", $"{n}주")
            : $"{n}주간 능력치 +10%";

        mgr.SetActiveCouple(newEmpId, existingEmpId);

        RandomEventUI.Instance.Show(
            row?.title ?? "사내 연애",
            existingEmp.portraitId,
            desc,
            sysMsg,
            sysMsg2,
            () =>
            {
                existingEmp.ChangeSatisfaction(10);
                newEmp.ChangeSatisfaction(10);
                existingEmp.romanceBuffWeeksLeft = n;
                newEmp.romanceBuffWeeksLeft      = n;
                EmployeeManager.Instance.UpdateEmployee(existingEmp);
                EmployeeManager.Instance.UpdateEmployee(newEmp);
                GameTimeManager.Instance?.SaveGameTime();
            }
        );
    }

    // ── 동반 퇴사 ─────────────────────────────────────────────────
    // sys: "{이름}도 같이 퇴사합니다"
    public static void TriggerCoupleResignationEvent(string partnerEmpId)
    {
        var partner = EmployeeManager.Instance?.GetEmployee(partnerEmpId);
        if (partner == null) return;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("CoupleResignation", out row);

        string sysMsg = (row?.systemMessage ?? "").Replace("{이름}", partner.employeeName);

        RandomEventUI.Instance.Show(
            row?.title ?? "혼자선 못 살아요",
            partner.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            sysMsg,
            null,
            () =>
            {
                EmployeeManager.Instance.FireEmployee(partner, countAsExit: false);
                HUDUI.Instance?.RefreshAll();
                GameTimeManager.Instance?.SaveGameTime();
            }
        );
    }

    // ── 사내 연애 이별 ────────────────────────────────────────────
    // sys: "{이름1}, {이름2} 만족도 -20"
    public static void TriggerRomanceBrokeUpEvent(RandomEventManager mgr, string empId1, string empId2)
    {
        var emp1 = EmployeeManager.Instance?.GetEmployee(empId1);
        var emp2 = EmployeeManager.Instance?.GetEmployee(empId2);
        if (emp1 == null || emp2 == null) return;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("RomanceBrokeUp", out row);

        string sysMsg = (row?.systemMessage ?? "")
            .Replace("{이름1}", emp1.employeeName)
            .Replace("{이름2}", emp2.employeeName);

        mgr.ClearCoupleIfInvolved(empId1);

        RandomEventUI.Instance.Show(
            row?.title ?? "사내 연애의 결말…",
            emp1.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            sysMsg,
            null,
            () =>
            {
                emp1.ChangeSatisfaction(-20);
                emp2.ChangeSatisfaction(-20);
                EmployeeManager.Instance.UpdateEmployee(emp1);
                EmployeeManager.Instance.UpdateEmployee(emp2);
                GameTimeManager.Instance?.SaveGameTime();
            }
        );
    }

    // ── 자발적 야근 ──────────────────────────────────────────────
    // sys: "{직원이름}이 자발적으로 야근합니다. 만족도 하락 없이 야근 모드가 활성화됩니다"
    public static void TriggerVoluntaryOvertimeEvent(EmployeeData emp)
    {
        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("VoluntaryOvertime", out row);

        string[] descs = row?.descriptions;
        string desc   = descs != null && descs.Length > 0 ? descs[Random.Range(0, Mathf.Min(descs.Length, 2))] : "";
        string sysMsg = (row?.systemMessage ?? "").Replace("{직원이름}", emp.employeeName);

        DevelopmentManager.Instance.SetVoluntaryOvertime(true);

        RandomEventUI.Instance.Show(
            row?.title ?? "자발적 야근",
            emp.portraitId,
            desc,
            sysMsg,
            null,
            () =>
            {
                GameTimeManager.Instance?.ForceStartTime();
                MoneyManager.Instance?.SaveMoney();
                GameTimeManager.Instance?.SaveGameTime();
                ProjectSaveManager.Instance?.SaveProject();
            }
        );
    }

    // ── 팀장 번아웃 ──────────────────────────────────────────────
    // sys: "{직원이름} 능력치 n주간 -20%"  (1·2단계 5~15주 / 3·4단계 10~20주)
    public static void TriggerLeaderBurnoutEvent(EmployeeData emp, int consecutiveCount, System.Action onDone)
    {
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        int n = stage <= 2 ? Random.Range(5, 16) : Random.Range(10, 21);

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("LeaderBurnout", out row);

        string[] descs = row?.descriptions;
        string rawDesc = descs != null && descs.Length > 0
            ? descs[Random.Range(0, Mathf.Min(descs.Length, 2))]
            : "";
        string desc   = rawDesc.Replace("{n}", consecutiveCount.ToString());
        string sysMsg = (row?.systemMessage ?? "")
            .Replace("{직원이름}", emp.employeeName)
            .Replace("n주", $"{n}주");

        emp.ApplyStatDebuff(n);
        EmployeeManager.Instance.UpdateEmployee(emp);
        OfficeManager.Instance?.ShowStatPopup(emp.id, $"능력치 {n}주 -20%", new Color(0.4f, 0.6f, 1f));

        RandomEventUI.Instance.Show(
            row?.title ?? "팀장 멈춰!",
            emp.portraitId,
            desc,
            sysMsg,
            null,
            () => onDone?.Invoke()
        );
    }

    // ── 팀장 질투 ─────────────────────────────────────────────────
    // sys: "{직원이름} 만족도 -15"
    public static void TriggerLeaderJealousyEvent(EmployeeData emp, System.Action onDone)
    {
        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("LeaderJealousy", out row);

        string desc   = row?.descriptions?.Length > 0 ? row.descriptions[0] : "";
        string sysMsg = (row?.systemMessage ?? "").Replace("{직원이름}", emp.employeeName);

        emp.ChangeSatisfaction(-15);
        EmployeeManager.Instance.UpdateEmployee(emp);

        RandomEventUI.Instance.Show(
            row?.title ?? "나도 팀장...",
            emp.portraitId,
            desc,
            sysMsg,
            null,
            () => onDone?.Invoke()
        );
    }

    static void Add(List<RandomEventData> pool,
                    Dictionary<string, RandomEventChartRow> chart,
                    RandomEventData evt)
    {
        RandomEventChartLoader.Apply(evt, chart);
        pool.Add(evt);
    }
}
