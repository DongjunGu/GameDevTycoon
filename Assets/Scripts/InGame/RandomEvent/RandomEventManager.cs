using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private List<RandomEventData> _eventPool = new();
    private List<RandomEventData> _conditionEventPool = new();
    private Queue<string> _nextWeekPopups = new();

    private struct RunEventPayload
    {
        public string employeeName;
        public string portraitId;
        public string message;
    }
    private Queue<RunEventPayload> _nextWeekRunEvents = new();

    // ── 이벤트 스케줄 (프로젝트 시작 시 전부 결정) ──────────
    private struct ScheduledEvent
    {
        public float           triggerProgress; // 발동 진행도 (0~1)
        public RandomEventType eventType;
    }
    private List<ScheduledEvent> _scheduledEvents = new();
    private int _nextScheduledIndex = 0;

    // 진행도 도달 후 패트롤 도착을 기다리는 대기 이벤트
    private RandomEventData _pendingEvent = null;

    // ── 디버그 ────────────────────────────────────────────────
    [Header("Debug - 테스트 이벤트")]
    public bool            debugMode      = false;
    public RandomEventType debugEventType = RandomEventType.NetworkIssue;

    // ── 카테고리 발동 확률 ────────────────────────────────────
    [Header("카테고리 발동 확률")]
    [Range(0f, 1f)] public float category1Chance = 0.5f;  //  1~24%
    [Range(0f, 1f)] public float category2Chance = 0.7f;  // 26~50%
    [Range(0f, 1f)] public float category3Chance = 0.7f;  // 51~74%
    [Range(0f, 1f)] public float category4Chance = 0.5f;  // 76~99%

    // 카테고리별 진행도 범위 (0~1)
    private static readonly (float min, float max)[] CategoryRanges =
    {
        (0.01f, 0.24f),
        (0.26f, 0.49f),  // category 2: 26~49%
        (0.51f, 0.74f),
        (0.76f, 0.99f),
    };

    // ── 개발 이벤트 가중치 ────────────────────────────────────
    [Header("개발 이벤트 가중치")]
    // 1~99%
    public float competitorGameWeight    = 1f; // 대형 게임사의 경쟁작 출시
    public float tangsuYukFightWeight    = 1f; // 탕수육 부먹 찍먹 싸움
    public float avoidingEmployeeWeight  = 1f; // 나를 피하는 직원
    public float coldWeight              = 1f; // 감기
    public float badReviewWeight         = 1f; // 이유 없는 별점 1점
    public float birthdayWeight          = 1f; // 생일
    public float earlyLeaveRequestWeight = 1f; // 퇴근 요청
    public float equipmentUpgradeWeight  = 1f; // 장비 업그레이드 요청
    public float gameUpgradeRequestWeight= 1f; // 게임 업그레이드 요청
    public float companyDinnerWeight     = 1f; // 오늘은 회식이다!
    public float bossGossipWeight        = 1f; // 사장님 뒷담까기
    // 1~74%
    public float networkIssueWeight      = 1f; // 네트워크 끊김
    // 26~99%
    public float hackyCodeWeight         = 1f; // 야매코드
    // 51~99%
    public float youtuberRequestWeight   = 1f; // 유튜버 선공개 요청

    // ── 조건 이벤트 ───────────────────────────────────────────
    [Header("조건 이벤트")]
    [Range(0f, 1f)] public float employeeRunChance    = 0.5f;
    [Range(0f, 1f)] public float employeeFightChance  = 0.5f;
    [Range(0f, 1f)] public float badCompanyChance     = 0.3f;

    // ── 투자 이벤트 ───────────────────────────────────────────
    [Header("투자 이벤트")]
    [Range(0f, 1f)] public float investmentTriggerChance = 0.5f;
    public float investmentThreshold = 80f;
    public int   investmentReward    = 1000;

    public bool   InvestmentAccepted    { get; set; } = false;
    public string InvestmentStat        { get; set; } = "";
    public string InvestmentStatName    { get; set; } = "";

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        RandomEvents_Condition.Register(_conditionEventPool, this);
    }

    void Start()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged += OnWeekChanged;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnWeekChanged;
    }

    void OnWeekChanged()
    {
        while (_nextWeekPopups.Count > 0)
            AlertUI.Instance.Show(_nextWeekPopups.Dequeue());

        while (_nextWeekRunEvents.Count > 0)
        {
            var payload = _nextWeekRunEvents.Dequeue();
            EmployeeManager.Instance.ReduceAllSatisfaction(10);
            EventUI.Instance.Show(
                "직원 도망",
                payload.portraitId,
                $"{payload.employeeName}\n\n{payload.message}",
                null
            );
        }
    }

    // ── 풀 구성만 (StartDevelopment/RestoreState 양쪽에서 호출) ──
    public void InitEvents()
    {
        InvestmentAccepted = false;
        InvestmentStat     = "";
        InvestmentStatName = "";
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;
        _eventPool.Clear();
        RandomEvents_Dev.Register(_eventPool, this);
    }

    // ── 신규 프로젝트 시작 시: 스케줄 결정 ──────────────────
    public void ScheduleEvents()
    {
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;

        // 디버그 모드: 지정 이벤트를 진행도 1%에 강제 배치
        if (debugMode)
        {
            var debugEvt = _eventPool.Find(e => e.type == debugEventType);
            if (debugEvt != null)
            {
                _scheduledEvents.Add(new ScheduledEvent
                {
                    triggerProgress = 0.01f,
                    eventType       = debugEventType
                });
                Debug.Log($"[Debug] 이벤트 강제 스케줄: {debugEventType} @ 1%");
            }
            else
            {
                Debug.LogWarning($"[Debug] {debugEventType} 를 풀에서 찾을 수 없음");
            }
            return;
        }

        float[] chances = { category1Chance, category2Chance, category3Chance, category4Chance };

        // 이미 다른 카테고리에서 뽑힌 이벤트 타입 추적
        var usedTypes = new HashSet<RandomEventType>();

        for (int cat = 1; cat <= 4; cat++)
        {
            if (UnityEngine.Random.value > chances[cat - 1]) continue;

            // categoryMin~categoryMax 범위에 이 카테고리가 포함되고,
            // 아직 다른 카테고리에서 선택되지 않은 이벤트만 풀에 포함
            var pool = _eventPool
                .Where(e => cat >= e.categoryMin && cat <= e.categoryMax
                            && !usedTypes.Contains(e.type))
                .ToList();
            if (pool.Count == 0) continue;

            var (min, max) = CategoryRanges[cat - 1];
            float progress = UnityEngine.Random.Range(min, max);

            var selected = PickWeighted(pool);
            usedTypes.Add(selected.type);   // 이후 카테고리에서 영구 제외

            _scheduledEvents.Add(new ScheduledEvent
            {
                triggerProgress = progress,
                eventType       = selected.type
            });
        }

        // 진행도 오름차순 정렬 (앞 카테고리가 먼저 발동 보장)
        _scheduledEvents.Sort((a, b) => a.triggerProgress.CompareTo(b.triggerProgress));

        foreach (var s in _scheduledEvents)
            Debug.Log($"[이벤트 스케줄] {s.triggerProgress:P1} → {s.eventType}");
    }

    // ── DevelopmentCoroutine에서 매 프레임 호출 ─────────────────
    public void CheckProgress(float progress)
    {
        if (_pendingEvent != null) return;  // 이미 대기 중
        if (_nextScheduledIndex >= _scheduledEvents.Count) return;
        if (progress < _scheduledEvents[_nextScheduledIndex].triggerProgress) return;

        var evt = _eventPool.Find(e => e.type == _scheduledEvents[_nextScheduledIndex].eventType);
        _nextScheduledIndex++;
        if (evt == null) return;

        if (evt.requiresPatrol)
        {
            // onSetup 먼저 호출 → 직원 결정(targetEmployeeId 세팅)
            evt.onSetup?.Invoke();
            _pendingEvent = evt;

            // 특정 직원 + 특정 지점이 설정된 경우 즉시 강제 이동
            if (!string.IsNullOrEmpty(evt.targetEmployeeId) &&
                !string.IsNullOrEmpty(evt.requiredPatrolPointId))
            {
                OfficeManager.Instance?.ForceCharacterToPatrolPoint(
                    evt.targetEmployeeId, evt.requiredPatrolPointId, stayDuration: 1f);
            }
        }
        else
        {
            // 패트롤 불필요 → 즉시 발동
            evt.onSetup?.Invoke();
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventUI.Instance.Show(evt);
        }
    }

    // ── 패트롤 도착 시 OfficeCharacter에서 호출 ──────────────
    public void OnPatrolArrived(string pointId = "", string employeeId = "")
    {
        if (_pendingEvent == null) return;

        // 지점 ID 검사 (설정된 경우)
        string requiredPoint = _pendingEvent.requiredPatrolPointId;
        if (!string.IsNullOrEmpty(requiredPoint) && requiredPoint != pointId) return;

        // 직원 ID 검사 (설정된 경우)
        string requiredEmp = _pendingEvent.targetEmployeeId;
        if (!string.IsNullOrEmpty(requiredEmp) && requiredEmp != employeeId) return;

        var evt = _pendingEvent;
        _pendingEvent = null;

        // onSetup은 requiresPatrol=true일 때 이미 호출됨 (progress 시점에 호출)
        StartCoroutine(ShowEventAfterDelay(evt, 1f));
    }

    // ── 저장 ─────────────────────────────────────────────────
    // 형식: "0.1234:Blackout|0.4321:TeamDinner"
    public string GetScheduledEventsString()
    {
        if (_scheduledEvents.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var s in _scheduledEvents)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(s.triggerProgress.ToString("F4",
                System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(':');
            sb.Append(s.eventType.ToString());
        }
        return sb.ToString();
    }

    public int GetNextScheduledIndex() => _nextScheduledIndex;

    // ── 복원 ─────────────────────────────────────────────────
    public void RestoreSchedule(string data, int nextIndex)
    {
        _scheduledEvents.Clear();
        _nextScheduledIndex = nextIndex;
        if (string.IsNullOrEmpty(data)) return;

        foreach (var entry in data.Split('|'))
        {
            var parts = entry.Split(':');
            if (parts.Length != 2) continue;
            if (!float.TryParse(parts[0],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float prog)) continue;
            if (!System.Enum.TryParse(parts[1], out RandomEventType type)) continue;
            _scheduledEvents.Add(new ScheduledEvent { triggerProgress = prog, eventType = type });
        }
    }

    // ─────────────────────────────────────────────────────────
    public void Reset()
    {
        InvestmentAccepted  = false;
        InvestmentStat      = "";
        InvestmentStatName  = "";
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;
        _pendingEvent       = null;
        InvestmentProgressUI.Instance?.Hide();
    }

    // ── 투자 이벤트 ───────────────────────────────────────────
    public void TriggerInvestmentEvent(System.Action onComplete)
    {
        if (UnityEngine.Random.value > investmentTriggerChance)
        {
            onComplete?.Invoke();
            return;
        }

        string[] stats     = { "planning", "develop", "art", "creativity" };
        string[] statNames = { "기획", "개발", "아트", "창의성" };
        int idx = UnityEngine.Random.Range(0, stats.Length);

        InvestmentStat     = stats[idx];
        InvestmentStatName = statNames[idx];

        ConfirmUI.Instance.Show(
            $"투자자가 찾아왔습니다!\n{statNames[idx]} 수치가 {investmentThreshold}점 이상이면\n{investmentReward:N0}G 지급\n달성 실패 시 {investmentReward:N0}G 차감",
            onConfirm: () =>
            {
                InvestmentAccepted = true;
                InvestmentProgressUI.Instance?.Show(InvestmentStatName, investmentThreshold);
                onComplete?.Invoke();
            },
            onCancel: () => onComplete?.Invoke(),
            confirmText: "수락",
            cancelText:  "거절"
        );
    }

    public void CheckInvestmentResult(float planning, float develop, float art, float creativity,
        System.Action onComplete = null)
    {
        if (!InvestmentAccepted || string.IsNullOrEmpty(InvestmentStat))
        {
            onComplete?.Invoke();
            return;
        }

        float value = InvestmentStat switch
        {
            "planning"   => planning,
            "develop"    => develop,
            "art"        => art,
            "creativity" => creativity,
            _ => 0f
        };

        InvestmentAccepted = false;
        InvestmentProgressUI.Instance?.Hide();

        if (value >= investmentThreshold)
        {
            MoneyManager.Instance.AddGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 성공!\n{InvestmentStatName} 수치: {value:F0}점\n{investmentReward:N0}G를 받았습니다!",
                () => onComplete?.Invoke());
        }
        else
        {
            MoneyManager.Instance.ForceSpendGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 실패...\n{InvestmentStatName} 수치: {value:F0}점\n{investmentReward:N0}G를 잃었습니다.",
                () => onComplete?.Invoke());
        }
    }

    // ── 조건 이벤트 ───────────────────────────────────────────
    public void CheckConditionEvents()
    {
        foreach (var emp in new List<EmployeeData>(EmployeeManager.Instance.ownedEmployees))
        {
            if (emp.satisfaction >= 41) continue;

            float triggerChance = (50 - emp.satisfaction) / 100f;
            if (UnityEngine.Random.value >= triggerChance) continue;

            var captured = emp;
            if (UnityEngine.Random.value < 0.7f)
                TriggerEmployeeResignationEvent(captured);
            else
                TriggerEmployeeRunEvent(captured);
        }
    }

    // ── stub ──────────────────────────────────────────────────
    public void TriggerScoutEvent()      { }
    public void TriggerBetaTestEvent()   { }
    public void TriggerAlgorithmEvent()  { }
    public void TriggerEmployeeFightEvent() { }
    public void TriggerBadCompanyEvent() { }

    public void TriggerEmployeeResignationEvent(EmployeeData emp)
    {
        bool isOvertime = false;
        string message = isOvertime
            ? RandomEvents_Condition.ResignationOvertimeMessage
            : RandomEvents_Condition.ResignationMessages[
                UnityEngine.Random.Range(0, RandomEvents_Condition.ResignationMessages.Length)];

        EventUI.Instance.Show("사직서 제출", emp.portraitId, $"{emp.employeeName}\n\n{message}", () =>
        {
            EmployeeManager.Instance.FireEmployee(emp);
            EmployeeManager.Instance.ReduceAllSatisfactionExcept(10, emp);
            AlertUI.Instance.Show(
                $"{emp.employeeName}이(가) 사직서를 제출하고 퇴사했습니다.\n남은 직원들의 만족도가 10 하락합니다."
            );
        });
    }

    public void TriggerEmployeeRunEvent(EmployeeData emp)
    {
        EmployeeManager.Instance.FireEmployee(emp);

        string message = RandomEvents_Condition.RunAwayMessages[
            UnityEngine.Random.Range(0, RandomEvents_Condition.RunAwayMessages.Length)];

        _nextWeekRunEvents.Enqueue(new RunEventPayload
        {
            employeeName = emp.employeeName,
            portraitId   = emp.portraitId,
            message      = message
        });
    }

    // ── 테스트용 즉시 발동 ────────────────────────────────────
    public void TriggerEventTest(RandomEventType type)
    {
        // 풀이 비어있으면 먼저 구성
        if (_eventPool.Count == 0)
        {
            _eventPool.Clear();
            RandomEvents_Dev.Register(_eventPool, this);
        }

        var evt = _eventPool.Find(e => e.type == type);
        if (evt == null)
        {
            Debug.LogWarning($"[EventTest] {type} 를 풀에서 찾을 수 없음");
            return;
        }

        if (evt.requiresPatrol)
        {
            evt.onSetup?.Invoke();
            _pendingEvent = evt;

            if (!string.IsNullOrEmpty(evt.targetEmployeeId) &&
                !string.IsNullOrEmpty(evt.requiredPatrolPointId))
            {
                OfficeManager.Instance?.ForceCharacterToPatrolPoint(
                    evt.targetEmployeeId, evt.requiredPatrolPointId, stayDuration: 1f);
            }
        }
        else
        {
            evt.onSetup?.Invoke();
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventUI.Instance.Show(evt);
        }
    }

    System.Collections.IEnumerator ShowEventAfterDelay(RandomEventData evt, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        DevelopmentManager.Instance.PauseForEvent();
        RandomEventUI.Instance.Show(evt);
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────
    RandomEventData PickWeighted(List<RandomEventData> pool)
    {
        float total = 0f;
        foreach (var e in pool) total += e.weight;

        float roll       = UnityEngine.Random.value * total;
        float cumulative = 0f;
        RandomEventData selected = pool[pool.Count - 1];
        foreach (var e in pool)
        {
            cumulative += e.weight;
            if (roll <= cumulative) { selected = e; break; }
        }
        return selected;
    }
}
