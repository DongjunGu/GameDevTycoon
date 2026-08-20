using UnityEngine;

// 파견(Dispatch) 시스템 — 한 번에 한 명만, 5주(임시). 파견 시점에 성과(강화량/테크포인트/ran)를 미리 계산해
// 저장해두고, 5주 뒤 직원이 master_desk 로 복귀해 RandomEventUI 로 보고할 때 출력·반영한다.
// 영속화는 UserGameTime 테이블에 (dispatchEmployeeId / dispatchWeeksLeft / dispatchOutcome) 로 저장
// — GameTimeManager.SaveGameTime/LoadGameTime 이 이 매니저에서 값을 읽고 넣어준다.
public class DispatchManager : MonoBehaviour
{
    public static DispatchManager Instance { get; private set; }

    public const int DispatchDurationWeeks = 5; // 임시 — 나중에 규모별로 분기

    [System.Serializable]
    public class DispatchOutcome
    {
        public string employeeId;
        public int    oldLevel;   // 파견 시점 강화 레벨
        public int    newLevel;   // 강화 후 레벨 (oldLevel + 강화량, 0~10구간은 11 클램프)
        public int    techPoints; // floor((주능력치/20) * ranRoll)
        public float  ranRoll;    // 0.6~1.5 (1.0 이상 = 당첨)
    }

    public string DispatchEmployeeId   { get; private set; } = "";
    public string DispatchEmployeeName { get; private set; } = ""; // 뒤끝 저장용(확인 편의) — 진실은 ID
    public string DispatchDeskId       { get; private set; } = ""; // 파견 시점 원래 자리 — 복귀 전까지 항상 예약(비워둠)
    public int    DispatchWeeksLeft    { get; private set; } = 0;
    private DispatchOutcome _outcome;

    public bool IsActive => !string.IsNullOrEmpty(DispatchEmployeeId);
    public bool IsDispatched(string empId) => IsActive && DispatchEmployeeId == empId;

    // 파견 상태 변화 통지 (슬롯 badge 갱신용)
    public System.Action OnDispatchChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged += OnWeekPassed;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnWeekPassed;
    }

    // ── 파견 시작 ─────────────────────────────────────────────
    // DispatchPanelUI 가 호출. 이미 파견 중이면 AlertUI 로 안내하고 무시.
    public void RequestDispatch(string employeeId)
    {
        if (IsActive)
        {
            var cur = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(DispatchEmployeeId) : null;
            string curName = cur != null ? cur.employeeName : "직원";
            if (AlertUI.Instance != null) AlertUI.Instance.Show($"{curName}가 이미 파견중입니다.");
            return;
        }

        var emp = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(employeeId) : null;
        if (emp == null) return;

        _outcome            = ComputeOutcome(emp);
        DispatchEmployeeId   = employeeId;
        DispatchEmployeeName = emp.employeeName;
        DispatchDeskId       = emp.assignedDeskId; // 원래 자리 기억 — 복귀 전까지 예약 유지
        DispatchWeeksLeft    = DispatchDurationWeeks;
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.SaveGameTime();
        if (OnDispatchChanged != null) OnDispatchChanged.Invoke();

        // 출발 다이얼로그 → 클릭 시 사무실에서 spawnpoint 로 퇴장
        if (RandomEventUI.Instance != null)
            RandomEventUI.Instance.Show("", emp.portraitId, "파견 다녀오겠습니다!", () =>
            {
                if (OfficeManager.Instance != null) OfficeManager.Instance.SendOnDispatch(employeeId);
            });
        else if (OfficeManager.Instance != null)
            OfficeManager.Instance.SendOnDispatch(employeeId);
    }

    // 파견 시점 능력치 기준으로 성과 사전 계산
    DispatchOutcome ComputeOutcome(EmployeeData emp)
    {
        int oldLevel = emp.enhancementLevel;
        int add      = RollEnhanceAmount(oldLevel);
        int newLevel = oldLevel + add;
        // 0~10 구간만 결과 11 클램프 (11성 초과 불가).
        if (oldLevel <= 10 && newLevel > 11) newLevel = 11;
        // 최대 강화(전 등급 25) 초과 방지 — 테이블 상한이자 강화 상한.
        newLevel = Mathf.Min(newLevel, EmployeeData.MaxEnhancementForGrade(emp.grade));

        float ran        = Random.Range(0.6f, 1.5f);
        int   mainStat   = GetMainStat(emp);
        int   techPoints = Mathf.Max(0, Mathf.FloorToInt(mainStat / 20f * ran));

        var outcome = new DispatchOutcome();
        outcome.employeeId = emp.id;
        outcome.oldLevel   = oldLevel;
        outcome.newLevel   = newLevel;
        outcome.techPoints = techPoints;
        outcome.ranRoll    = ran;
        return outcome;
    }

    // 구간별 강화량 추첨
    int RollEnhanceAmount(int level)
    {
        if (level <= 10) return WeightedPick(new[] { 1, 2, 3, 4, 5 }, new[] { 20, 20, 30, 20, 10 });
        if (level <= 15) return WeightedPick(new[] { 1, 2, 3 },       new[] { 60, 35, 5 });
        if (level <= 20) return WeightedPick(new[] { 0, 1, 2 },       new[] { 75, 20, 5 });
        return WeightedPick(new[] { 0, 1 }, new[] { 95, 5 });
    }

    int WeightedPick(int[] values, int[] weights)
    {
        int total = 0;
        for (int i = 0; i < weights.Length; i++) total += weights[i];
        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < values.Length; i++)
        {
            acc += weights[i];
            if (roll < acc) return values[i];
        }
        return values[values.Length - 1];
    }

    int GetMainStat(EmployeeData e)
    {
        switch (e.role)
        {
            case EmployeeRole.Planner:    return e.planningSkill;
            case EmployeeRole.Programmer: return e.developSkill;
            case EmployeeRole.Artist:     return e.artSkill;
            default:                      return e.developSkill;
        }
    }

    // ── 주간 카운트다운 ───────────────────────────────────────
    void OnWeekPassed()
    {
        if (!IsActive || DispatchWeeksLeft <= 0) return;

        DispatchWeeksLeft--;
        if (DispatchWeeksLeft <= 0)
        {
            DispatchWeeksLeft = 0;
            if (GameTimeManager.Instance != null) GameTimeManager.Instance.SaveGameTime(); // weeks=0 영속 (직원ID 유지)
            // 같은 주 퇴사/조건 이벤트 모달과 겹치지 않게 모두 닫힌 뒤 복귀 시작
            ModalGate.I.WhenFree(BeginReturn);
        }
    }

    // 재접속 복원 — 주차 0 인 채로 종료했던 파견은 GameScene 진입 후 보고 재개
    public void CheckReturnOnReconnect()
    {
        if (IsActive && DispatchWeeksLeft <= 0)
            ModalGate.I.WhenFree(BeginReturn);
    }

    // ── 복귀 + 보고 ───────────────────────────────────────────
    // 복귀는 다른 진행(개발/이벤트/팀장점수)을 막지 않는다. 25%/75% 등 마일스톤은 그대로 진행되고,
    // 파견은 성과보고(ApplyOutcomeAndAlert → Clear)를 완료해야만 확정된다. 보고 전에 종료/재접속하면
    // weeks=0 + employeeId 가 영속돼 있어 GameSceneInitializer → CheckReturnOnReconnect 가 다시 복귀시킨다.
    void BeginReturn()
    {
        if (!IsActive) return;
        if (OfficeManager.Instance == null) { ShowReport(); return; }
        OfficeManager.Instance.ReturnFromDispatch(DispatchEmployeeId, ShowReport);
    }

    void ShowReport()
    {
        var emp = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(DispatchEmployeeId) : null;
        string portraitId = emp != null ? emp.portraitId : "";
        string message    = PickReportMessage();
        if (RandomEventUI.Instance != null)
            RandomEventUI.Instance.Show("", portraitId, message, ApplyOutcomeAndAlert);
        else
            ApplyOutcomeAndAlert();
    }

    string PickReportMessage()
    {
        bool win      = _outcome != null && _outcome.ranRoll >= 1.0f;
        bool enhanced = _outcome != null && _outcome.newLevel > _outcome.oldLevel;

        string[] pool;
        if (win && enhanced)
            pool = new[]
            {
                "사장님, 이번 출장 완전 대성공이에요! 저 진짜 엄청나게 많이 배워왔으니까 얼른 칭찬해 주세요!",
                "거기 천재들라는 애들 다 이기고 왔습니다. 저 제대로 업그레이드돼서 왔으니까 다음 프로젝트는 저만 믿으세요!"
            };
        else if (win && !enhanced)
            pool = new[]
            {
                "가볍게 요정도 가져왔습니다! 내년 연봉 상승 기대할게요",
                "저 이정도에요 항상 제가 직원으로 있는걸 고마워해주세요!",
                "회사 기술력은 제가 혼자서 낭낭하게 챙겨왔습니다!"
            };
        else if (!win && enhanced)
            pool = new[]
            {
                "기술 포인트는 조금 아쉽지만... 대신 제가 엄청 성장해서 왔잖아요!",
                "가져온 정보가 적어서 실망하셨죠? 대신 제가 성장했으니까 이제 캐리할게요!"
            };
        else
            pool = new[]
            {
                "이번 출장은 진짜 대실패였네요... 죄송합니다ㅠㅠ",
                "분명 성공 직전이었는데 너무 아쉽네요..."
            };

        return pool[Random.Range(0, pool.Length)];
    }

    // 보고 다이얼로그 클릭 후 — 성과 반영 + AlertUI 요약 + 데스크 복귀
    void ApplyOutcomeAndAlert()
    {
        if (_outcome == null) { Clear(); return; }

        var emp = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetEmployee(_outcome.employeeId) : null;
        int  oldLevel   = _outcome.oldLevel;
        int  newLevel   = _outcome.newLevel;
        int  techPoints = _outcome.techPoints;
        string empId    = _outcome.employeeId;
        string empName  = emp != null ? emp.employeeName : "직원";

        if (emp != null)
        {
            EmployeeManager.Instance.ApplyDispatchEnhancement(emp, newLevel);
            EmployeeManager.Instance.RecoverSatisfaction(emp, 80);
            EmployeeManager.Instance.UpdateEmployee(emp);
        }
        if (techPoints > 0 && MoneyManager.Instance != null)
            MoneyManager.Instance.AddPoint(techPoints, false);

        // 프로젝트 개발 진행(Developing) 중이면 복귀 직원도 개발 합류 — 일반 채용과 동일하게
        // 새 시드 생성 + 남은 시간만큼 상시개발 틱 구성. 성과 보고 후(이 시점)부터 틱이 잡히고,
        // master_desk 등장 시점이 아니다. (시간은 보고/AlertUI 동안 멈춰 있어 _elapsed 가 고정됨)
        if (emp != null && DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsStarted)
        {
            DevelopmentManager.Instance.OnEmployeeHired(emp);
            ProjectSaveManager.Instance?.SaveProject();
        }

        Clear(); // 파견 상태 해제 + 영속화

        string summary =
            $"테크트리 포인트 {techPoints}P 획득\n" +
            $"{empName} {oldLevel} -> {newLevel}\n" +
            "만족도 회복";

        if (AlertUI.Instance != null)
            AlertUI.Instance.Show(summary, () =>
            {
                if (OfficeManager.Instance != null) OfficeManager.Instance.AssignDeskAndReturn(empId);
            });
        else if (OfficeManager.Instance != null)
            OfficeManager.Instance.AssignDeskAndReturn(empId);
    }

    void Clear()
    {
        DispatchEmployeeId   = "";
        DispatchEmployeeName = "";
        DispatchDeskId       = "";
        DispatchWeeksLeft    = 0;
        _outcome             = null;
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.SaveGameTime();
        if (OnDispatchChanged != null) OnDispatchChanged.Invoke();
    }

    // ── 영속화 (GameTimeManager 가 호출) ──────────────────────
    public string GetOutcomeJson()
    {
        return _outcome != null ? JsonUtility.ToJson(_outcome) : "";
    }

    public void LoadDispatch(string employeeId, string employeeName, string deskId, int weeksLeft, string outcomeJson)
    {
        DispatchEmployeeId   = employeeId ?? "";
        DispatchEmployeeName = employeeName ?? "";
        DispatchDeskId       = deskId ?? "";
        DispatchWeeksLeft    = weeksLeft;
        _outcome = !string.IsNullOrEmpty(outcomeJson)
            ? JsonUtility.FromJson<DispatchOutcome>(outcomeJson)
            : null;
        if (OnDispatchChanged != null) OnDispatchChanged.Invoke();
    }

    public void ResetForNewRun()
    {
        DispatchEmployeeId   = "";
        DispatchEmployeeName = "";
        DispatchDeskId       = "";
        DispatchWeeksLeft    = 0;
        _outcome             = null;
        if (OnDispatchChanged != null) OnDispatchChanged.Invoke();
    }
}
