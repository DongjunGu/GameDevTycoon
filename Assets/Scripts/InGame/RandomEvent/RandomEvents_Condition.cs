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

    // ── 사직 메시지 ───────────────────────────────────────────────
    // description1~2 = 일반 사직, description3 = 야근 사직
    // [CDN fallback]
    // "안녕히 계세요 여러분\n전 이 세상의 모든 굴레와 속박을 벗어 던지고\n제 행복을 찾아 떠납니다."
    // "건강상의 사유로 그만두겠습니다.\n진단명은... 사장님 알레르기!"
    // [야근] "야근 도저히 못해먹겠네\n난 퇴사할껍니다!"
    public static string GetResignationMessage(bool isOvertime)
    {
        var descs = GetDescs("EmployeeResignation");
        if (descs == null) return "";
        if (isOvertime && descs.Length >= 3) return descs[2];
        int count = Mathf.Min(descs.Length, isOvertime ? descs.Length - 1 : 2);
        return count > 0 ? descs[Random.Range(0, count)] : "";
    }

    // [CDN fallback] "{이름}이(가) 사직서를 제출하고 퇴사했습니다.\n남은 직원들의 만족도가 10 하락합니다."
    public static string GetResignationSystemMessage(string empName)
    {
        string msg = GetSystemMessage("EmployeeResignation");
        return !string.IsNullOrEmpty(msg) ? msg.Replace("{이름}", empName) : "";
    }

    // ── 도망 메시지 ───────────────────────────────────────────────
    // [CDN fallback]
    // "책상 위에 놓인 사원증이 반으로 쪼개져 있습니다."
    // "프로필 상태가 '구직 중'으로 바뀌었습니다."
    // "책상 위에 포스트잇 한 장이 붙어 있습니다.\n'회사 탈출은 지능순'"
    public static string GetRunAwayMessage()
    {
        var descs = GetDescs("EmployeeRun");
        return descs != null ? descs[Random.Range(0, descs.Length)] : "";
    }

    // ── Register ─────────────────────────────────────────────────
    // BadCompany는 빈 stub 호출이라 dead였음 — 등록 제거
    // (실제 BadCompany 발동은 TriggerCompanyBadReviewEvent 등 별도 경로)
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

    // 예약된 UnstableCompany 이벤트 실제 발동 (OnWeekChanged 카운트다운 만료 시)
    public static void TriggerUnstableCompanyEvent(RandomEventManager mgr, int currentYear)
    {
        if (Random.value < 0.5f)
            TriggerBadRumorEvent(mgr, currentYear);
        else
            TriggerAnxietyInducingEvent();
    }

    // ── 안좋은 소문 ───────────────────────────────────────────────
    // [CDN fallback]
    // title: "안좋은 소문"
    // desc:  "회사에서 나간 직원들이 사장님에 대한 안 좋은 소문을 내서\n지원자들의 경쟁률이 계속 줄고 있어요!"
    // sys:   "안 좋은 소문으로 인해 지원 인원이 감소하고 있습니다\n채용 지원 인원 1명 감소"
    // portrait: "portrait_secretary"
    public static void TriggerBadRumorEvent(RandomEventManager mgr, int currentYear)
    {
        mgr.HiringPenalty        = Mathf.Max(1, mgr.HiringPenalty + 1);
        mgr.HiringPenaltyEndYear = currentYear + 1;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("BadRumor", out row);

        EventUI.Instance.Show(
            row?.title ?? "",
            row?.portraitId ?? "",
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            () => AlertUI.Instance.Show(
                row?.systemMessage ?? "",
                () =>
                {
                    GameTimeManager.Instance?.ForceStartTime();
                    GameTimeManager.Instance?.SaveGameTime();
                }
            )
        );
    }

    // ── 불안감 조성 ───────────────────────────────────────────────
    // [CDN fallback]
    // title: "불안감 조성"
    // desc:  "사장님 이번에는 제가 나갈 차롄가요…? 너무 많이 잘리니까 잘릴까봐 불안해서 일을 못하겠어요!"
    // sys:   "직원이 불안에 떨고 있습니다\n만족도 -5 / {직원이름} 능력치 -20%"
    public static void TriggerAnxietyInducingEvent()
    {
        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return;

        var emp = employees[Random.Range(0, employees.Count)];

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("AnxietyInducing", out row);
        string sysMsg = !string.IsNullOrEmpty(row?.systemMessage)
            ? row.systemMessage.Replace("{직원이름}", emp.employeeName) : "";

        EventUI.Instance.Show(
            row?.title ?? "",
            emp.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            () =>
            {
                emp.ChangeSatisfaction(-5);
                emp.ApplyStatDebuff(Random.Range(4, 9));
                EmployeeManager.Instance.UpdateEmployee(emp);
                AlertUI.Instance.Show(sysMsg, () =>
                {
                    GameTimeManager.Instance?.ForceStartTime();
                    GameTimeManager.Instance?.SaveGameTime();
                });
            }
        );
    }

    // ── 회사 평점 1점 ─────────────────────────────────────────────
    // [CDN fallback]
    // title:   "회사 평점 1점"
    // desc:    "이전에 만족도가 낮아서 퇴사한 직원이 회사에 욕설이 가득한 리뷰를 남겨서 평판이 안좋아졌네요…"
    // sys:     "최악의 리뷰로 인해 지원자들의 발길이 끊깁니다.."
    // portrait: "portrait_secretary"
    public static void TriggerCompanyBadReviewEvent(RandomEventManager mgr, int currentYear)
    {
        mgr.HiringPenalty        = Mathf.Max(1, mgr.HiringPenalty + 1);
        mgr.HiringPenaltyEndYear = currentYear + 1;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("CompanyBadReview", out row);

        EventUI.Instance.Show(
            row?.title ?? "",
            row?.portraitId ?? "",
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            () => AlertUI.Instance.Show(
                row?.systemMessage ?? "",
                () => GameTimeManager.Instance?.SaveGameTime()
            )
        );
    }

    // ── 사내 연애 ─────────────────────────────────────────────────
    // [CDN fallback]
    // title:  "사내 연애"
    // desc1 (남자 보고): "사장님 저 {상대이름}과 사귀기로 했습니다! 만세!"
    // desc2 (여자 보고): "{상대이름}님이랑 오늘부터 1일이에요 축하해주세요!"
    // sys:    "{이름1}, {이름2} 만족도 +10, 능력치 +10%"
    public static void TriggerOfficeRomanceEvent(RandomEventManager mgr,
                                                  string newEmpId, string existingEmpId)
    {
        var newEmp      = EmployeeManager.Instance?.GetEmployee(newEmpId);
        var existingEmp = EmployeeManager.Instance?.GetEmployee(existingEmpId);
        if (newEmp == null || existingEmp == null) return;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("OfficeRomance", out row);

        string desc = (!existingEmp.isFemale
            ? (row?.descriptions?.Length > 0 ? row.descriptions[0] : "")
            : (row?.descriptions?.Length > 1 ? row.descriptions[1] : ""))
            .Replace("{상대이름}", newEmp.employeeName);

        string sysMsg = (row?.systemMessage ?? "")
            .Replace("{이름1}", existingEmp.employeeName)
            .Replace("{이름2}", newEmp.employeeName);

        mgr.SetActiveCouple(newEmpId, existingEmpId);

        EventUI.Instance.Show(row?.title ?? "", existingEmp.portraitId, desc, () =>
        {
            existingEmp.ChangeSatisfaction(10);
            newEmp.ChangeSatisfaction(10);
            existingEmp.romanceBuffWeeksLeft = Mathf.Max(existingEmp.romanceBuffWeeksLeft, 8);
            newEmp.romanceBuffWeeksLeft      = Mathf.Max(newEmp.romanceBuffWeeksLeft, 8);
            EmployeeManager.Instance.UpdateEmployee(existingEmp);
            EmployeeManager.Instance.UpdateEmployee(newEmp);
            AlertUI.Instance.Show(sysMsg, () => GameTimeManager.Instance?.SaveGameTime());
        });
    }

    // ── 동반 퇴사 ─────────────────────────────────────────────
    // [CDN fallback]
    // title: "혼자선 못 살아요"
    // desc:  "우린 원래 1+1이에요! 저도 같이 퇴사할껍니다!"
    // sys:   "{이름}도 같이 퇴사합니다"
    public static void TriggerCoupleResignationEvent(string partnerEmpId)
    {
        var partner = EmployeeManager.Instance?.GetEmployee(partnerEmpId);
        if (partner == null) return;

        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("CoupleResignation", out row);

        string sysMsg = (row?.systemMessage ?? "").Replace("{이름}", partner.employeeName);

        EventUI.Instance.Show(
            row?.title ?? "",
            partner.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            () =>
            {
                EmployeeManager.Instance.FireEmployee(partner, countAsExit: false);
                HUDUI.Instance?.RefreshAll();
                AlertUI.Instance.Show(sysMsg, () => GameTimeManager.Instance?.SaveGameTime());
            }
        );
    }

    // ── 사내 연애 이별 ────────────────────────────────────────
    // [CDN fallback]
    // title: "사내 연애의 결말…"
    // desc:  "사장님… 최대한 마주칠일 없게 부탁드립니다… 헤어졌어요"
    // sys:   "{이름1}, {이름2} 만족도 -20"
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

        EventUI.Instance.Show(
            row?.title ?? "",
            emp1.portraitId,
            row?.descriptions?.Length > 0 ? row.descriptions[0] : "",
            () =>
            {
                emp1.ChangeSatisfaction(-20);
                emp2.ChangeSatisfaction(-20);
                EmployeeManager.Instance.UpdateEmployee(emp1);
                EmployeeManager.Instance.UpdateEmployee(emp2);
                AlertUI.Instance.Show(sysMsg, () => GameTimeManager.Instance?.SaveGameTime());
            }
        );
    }

    // ── 자발적 야근 ──────────────────────────────────────────────
    // [CDN fallback]
    // title: "자발적 야근"
    // desc1: "이렇게 좋은 회사는 살면서 처음이네요! 목숨을 바쳐서 일하겠습니다"
    // desc2: "이대로는 잠이 안올 것 같아요 전 오늘 야근하겠습니다"
    // sys:   "{직원이름}이 자발적으로 야근합니다. 만족도 하락 없이 야근 모드가 활성화됩니다"
    public static void TriggerVoluntaryOvertimeEvent(EmployeeData emp)
    {
        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("VoluntaryOvertime", out row);

        string[] descs = row?.descriptions;
        string desc   = descs != null && descs.Length > 0 ? descs[Random.Range(0, Mathf.Min(descs.Length, 2))] : "";
        string sysMsg = (row?.systemMessage ?? "").Replace("{직원이름}", emp.employeeName);

        DevelopmentManager.Instance.SetVoluntaryOvertime(true);

        EventUI.Instance.Show(
            row?.title ?? "자발적 야근",
            emp.portraitId,
            desc,
            () => AlertUI.Instance.Show(sysMsg, () =>
            {
                GameTimeManager.Instance?.ForceStartTime();
                GameTimeManager.Instance?.SaveGameTime();
            })
        );
    }

    // ── 팀장 번아웃 ──────────────────────────────────────────────
    // [CDN fallback]
    // title: "팀장 멈춰!"
    // desc1: "저 지금 팀장 {n}번 연속 하고 있어요…\n저 이제 그만 좀 시켜주세요…"
    // desc2: "저희 팀은 왜 맨날 저만 일하나요…\n저 말고 다른 사람 시켜주세요..."
    // sys:   "{직원이름} 능력치가 20% 하락합니다"
    public static void TriggerLeaderBurnoutEvent(EmployeeData emp, int consecutiveCount, System.Action onDone)
    {
        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("LeaderBurnout", out row);

        string[] descs = row?.descriptions;
        string rawDesc = descs != null && descs.Length > 0
            ? descs[Random.Range(0, Mathf.Min(descs.Length, 2))]
            : "";
        string desc = rawDesc.Replace("{n}", consecutiveCount.ToString());
        string sysMsg = (row?.systemMessage ?? "").Replace("{직원이름}", emp.employeeName);

        emp.ApplyStatDebuff(8);
        EmployeeManager.Instance.UpdateEmployee(emp);

        EventUI.Instance.Show(
            row?.title ?? "팀장 멈춰!",
            emp.portraitId,
            desc,
            () => AlertUI.Instance.Show(sysMsg, () => onDone?.Invoke())
        );
    }

    // ── 팀장 질투 ─────────────────────────────────────────────────
    // [CDN fallback]
    // title: "나도 팀장..."
    // desc:  "뭐 이번에도 팀장은 아니네요. 이제는 기대도 안 하고 그냥 시키는 일이나 하다가 조용히 퇴근하렵니다."
    // sys:   "{직원이름} 만족도 -20"
    public static void TriggerLeaderJealousyEvent(EmployeeData emp, System.Action onDone)
    {
        RandomEventConditionChartRow row = null;
        Chart?.TryGetValue("LeaderJealousy", out row);

        string desc   = row?.descriptions?.Length > 0 ? row.descriptions[0] : "";
        string sysMsg = (row?.systemMessage ?? "").Replace("{직원이름}", emp.employeeName);

        emp.ChangeSatisfaction(-20);
        EmployeeManager.Instance.UpdateEmployee(emp);

        EventUI.Instance.Show(
            row?.title ?? "나도 팀장...",
            emp.portraitId,
            desc,
            () => AlertUI.Instance.Show(sysMsg, () => onDone?.Invoke())
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
