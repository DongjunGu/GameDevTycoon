using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private List<RandomEventData>       _eventPool       = new();
    private List<RandomEventChoiceData> _choiceEventPool = new();
    private List<RandomEventData>       _conditionEventPool = new();
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
        public bool            isChoiceEvent;
    }

    private struct SchedulableEntry
    {
        public RandomEventType type;
        public float           weight;
        public bool            isChoiceEvent;
    }
    private List<ScheduledEvent> _scheduledEvents = new();
    private int _nextScheduledIndex = 0;

    // 진행도 도달 후 패트롤 도착을 기다리는 대기 이벤트
    private RandomEventData       _pendingEvent       = null;
    private RandomEventChoiceData _pendingChoiceEvent = null;

    // 패트롤 도착 후 1초 딜레이 + 이벤트 UI 표시 중 구간도 포함한 "이벤트 진행 중" 플래그
    private bool _eventInProgress = false;

    // _pendingEvent 이동 중 OR 도착 후 UI가 닫힐 때까지 true
    public bool HasPendingEvent => _pendingEvent != null || _pendingChoiceEvent != null || _eventInProgress;

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
    public float networkIssueWeight      = 1.3f; // 네트워크 끊김
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
    public float  PendingHackyCodePenalty    { get; set; } = 0f;
    public string PendingHackyCodePortraitId { get; set; } = "";
    public int    PendingHackyCodeWeeksLeft  { get; set; } = 0;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 조건 이벤트 등록은 차트 로드 후 BackendManager에서 InitConditionEvents() 호출
        // (Awake 시점엔 Cache가 null이므로 여기서 등록하지 않음)
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

        if (PendingHackyCodePenalty > 0f)
        {
            PendingHackyCodeWeeksLeft--;
            if (PendingHackyCodeWeeksLeft <= 0)
            {
                float penalty   = PendingHackyCodePenalty;
                string portraitId = PendingHackyCodePortraitId;
                PendingHackyCodePenalty    = 0f;
                PendingHackyCodePortraitId = "";
                PendingHackyCodeWeeksLeft  = 0;
                DevelopmentPanelUI.Instance.AddValues(0f, -penalty, 0f, 0f, 0f);
                EventUI.Instance.Show(
                    "야매 코드 문제 발생",
                    portraitId,
                    $"임시 처리 해둔 코드가 문제가 생겼습니다!\n개발 점수 -{Mathf.RoundToInt(penalty)}"
                );
            }
        }

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

    // ── 조건 이벤트 등록 (BackendManager에서 차트 로드 후 호출) ──
    public void InitConditionEvents()
    {
        _conditionEventPool.Clear();
        RandomEvents_Condition.Register(_conditionEventPool, this, RandomEventChartLoader.Cache);
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
        RandomEvents_Dev.Register(_eventPool, this, RandomEventChartLoader.Cache);
        _choiceEventPool.Clear();
        RandomEvents_Choice.Register(_choiceEventPool, this, RandomEventChoiceChartLoader.Cache);
    }

    // ── 신규 프로젝트 시작 시: 스케줄 결정 ──────────────────
    public void ScheduleEvents()
    {
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;

        // 디버그 모드: 지정 이벤트를 진행도 1%에 강제 배치
        if (debugMode)
        {
            bool inChoice = _choiceEventPool.Exists(e => e.type == debugEventType);
            bool inNormal = _eventPool.Exists(e => e.type == debugEventType);

            if (inChoice || inNormal)
            {
                _scheduledEvents.Add(new ScheduledEvent
                {
                    triggerProgress = 0.01f,
                    eventType       = debugEventType,
                    isChoiceEvent   = inChoice
                });
                Debug.Log($"[Debug] 이벤트 강제 스케줄: {debugEventType} @ 1% (choice={inChoice})");
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
            if (_scheduledEvents.Count >= 2) break;
            if (UnityEngine.Random.value > chances[cat - 1]) continue;

            // 두 풀을 합쳐 카테고리 범위와 중복 여부 확인
            var combined = new List<SchedulableEntry>();
            foreach (var e in _eventPool)
                if (cat >= e.categoryMin && cat <= e.categoryMax && !usedTypes.Contains(e.type))
                    combined.Add(new SchedulableEntry { type = e.type, weight = e.weight, isChoiceEvent = false });
            foreach (var e in _choiceEventPool)
                if (cat >= e.categoryMin && cat <= e.categoryMax && !usedTypes.Contains(e.type))
                    combined.Add(new SchedulableEntry { type = e.type, weight = e.weight, isChoiceEvent = true });

            if (combined.Count == 0) continue;

            var (min, max) = CategoryRanges[cat - 1];
            float progress = UnityEngine.Random.Range(min, max);

            var selected = PickWeightedEntry(combined);
            usedTypes.Add(selected.type);   // 이후 카테고리에서 영구 제외

            _scheduledEvents.Add(new ScheduledEvent
            {
                triggerProgress = progress,
                eventType       = selected.type,
                isChoiceEvent   = selected.isChoiceEvent
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

        var scheduled = _scheduledEvents[_nextScheduledIndex];
        _nextScheduledIndex++;

        // ── 선택지 이벤트 ───────────────────────────────────────
        if (scheduled.isChoiceEvent)
        {
            var choiceData = _choiceEventPool.Find(e => e.type == scheduled.eventType);
            if (choiceData == null) return;
            RandomEventChoiceChartLoader.Apply(choiceData, scheduled.eventType.ToString(), RandomEventChoiceChartLoader.Cache);
            choiceData.cancelled = false;
            choiceData.onSetup?.Invoke();
            if (choiceData.cancelled) return;

            if (choiceData.requiresPatrol)
            {
                _pendingChoiceEvent = choiceData;
                if (!string.IsNullOrEmpty(choiceData.targetEmployeeId) &&
                    !string.IsNullOrEmpty(choiceData.requiredPatrolPointId))
                {
                    OfficeManager.Instance?.ForceCharacterToPatrolPoint(
                        choiceData.targetEmployeeId, choiceData.requiredPatrolPointId, stayDuration: 1f);
                }
            }
            else
            {
                DevelopmentManager.Instance.PauseForEvent();
                RandomEventChoiceUI.Instance.Show(choiceData);
            }
            return;
        }

        // ── 일반 이벤트 ─────────────────────────────────────────
        var evt = _eventPool.Find(e => e.type == scheduled.eventType);
        if (evt == null) return;

        if (evt.requiresPatrol)
        {
            // 차트 데이터 복원 후 onSetup 호출 (systemMessage 등 템플릿 초기화)
            RandomEventChartLoader.Apply(evt, RandomEventChartLoader.Cache);
            evt.cancelled = false;
            evt.onSetup?.Invoke();
            if (evt.cancelled) return;
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
            // 차트 데이터 복원 후 onSetup 호출
            RandomEventChartLoader.Apply(evt, RandomEventChartLoader.Cache);
            evt.cancelled = false;
            evt.onSetup?.Invoke();
            if (evt.cancelled) return;
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventUI.Instance.Show(evt);
        }
    }

    // ── 패트롤 도착 시 OfficeCharacter에서 호출 ──────────────
    public void OnPatrolArrived(string pointId = "", string employeeId = "")
    {
        // ── 일반 이벤트 ─────────────────────────────────────
        if (_pendingEvent != null)
        {
            string requiredPoint = _pendingEvent.requiredPatrolPointId;
            if (!string.IsNullOrEmpty(requiredPoint) && requiredPoint != pointId) goto checkChoice;

            string requiredEmp = _pendingEvent.targetEmployeeId;
            if (!string.IsNullOrEmpty(requiredEmp) && requiredEmp != employeeId) goto checkChoice;

            var evt = _pendingEvent;
            _pendingEvent = null;

            if (!string.IsNullOrEmpty(employeeId))
            {
                evt.targetEmployeeId = employeeId;
                var arrivedEmp = EmployeeManager.Instance.GetEmployee(employeeId);
                if (arrivedEmp != null) evt.portraitId = arrivedEmp.portraitId;
            }

            StartCoroutine(ShowEventAfterDelay(evt, 1f));
            return;
        }

        checkChoice:
        // ── 선택지 이벤트 ───────────────────────────────────
        if (_pendingChoiceEvent != null)
        {
            string requiredPoint = _pendingChoiceEvent.requiredPatrolPointId;
            if (!string.IsNullOrEmpty(requiredPoint) && requiredPoint != pointId) return;

            string requiredEmp = _pendingChoiceEvent.targetEmployeeId;
            if (!string.IsNullOrEmpty(requiredEmp) && requiredEmp != employeeId) return;

            var choiceData = _pendingChoiceEvent;
            _pendingChoiceEvent = null;

            if (!string.IsNullOrEmpty(employeeId))
            {
                choiceData.targetEmployeeId = employeeId;
                var arrivedEmp = EmployeeManager.Instance.GetEmployee(employeeId);
                if (arrivedEmp != null) choiceData.portraitId = arrivedEmp.portraitId;
            }

            StartCoroutine(ShowChoiceEventAfterDelay(choiceData, 1f));
        }
    }

    // ── 저장 ─────────────────────────────────────────────────
    // 형식: "0.1234:Blackout:0|0.4321:Birthday:1"  (마지막 필드: isChoiceEvent 0/1)
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
            sb.Append(':');
            sb.Append(s.isChoiceEvent ? '1' : '0');
        }
        return sb.ToString();
    }

    public int GetNextScheduledIndex() => _nextScheduledIndex;

    // 형식: "EventType:0" (일반) or "EventType:1" (선택지), 없으면 ""
    public string GetPendingEventData()
    {
        if (_pendingEvent != null)       return $"{_pendingEvent.type}:0";
        if (_pendingChoiceEvent != null) return $"{_pendingChoiceEvent.type}:1";
        return "";
    }

    public void RestorePendingEventFromSave(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var parts = data.Split(':');
        if (parts.Length < 2) return;
        if (!System.Enum.TryParse(parts[0], out RandomEventType type)) return;
        bool isChoice = parts[1] == "1";

        if (isChoice)
        {
            var choiceData = _choiceEventPool.Find(e => e.type == type);
            if (choiceData == null) return;
            RandomEventChoiceChartLoader.Apply(choiceData, type.ToString(), RandomEventChoiceChartLoader.Cache);
            choiceData.onSetup?.Invoke();
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventChoiceUI.Instance.Show(choiceData);
        }
        else
        {
            var evt = _eventPool.Find(e => e.type == type);
            if (evt == null) return;
            RandomEventChartLoader.Apply(evt, RandomEventChartLoader.Cache);
            evt.onSetup?.Invoke();
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventUI.Instance.Show(evt);
        }
    }

    // ── 복원 ─────────────────────────────────────────────────
    public void RestoreSchedule(string data, int nextIndex)
    {
        _scheduledEvents.Clear();
        _nextScheduledIndex = nextIndex;
        if (string.IsNullOrEmpty(data)) return;

        foreach (var entry in data.Split('|'))
        {
            var parts = entry.Split(':');
            if (parts.Length < 2) continue;
            if (!float.TryParse(parts[0],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float prog)) continue;
            if (!System.Enum.TryParse(parts[1], out RandomEventType type)) continue;
            bool isChoice = parts.Length >= 3 && parts[2] == "1";
            _scheduledEvents.Add(new ScheduledEvent
            {
                triggerProgress = prog,
                eventType       = type,
                isChoiceEvent   = isChoice
            });
        }
    }

    // ─────────────────────────────────────────────────────────
    public void Reset()
    {
        InvestmentAccepted       = false;
        InvestmentStat           = "";
        InvestmentStatName       = "";
        PendingHackyCodePenalty    = 0f;
        PendingHackyCodePortraitId = "";
        PendingHackyCodeWeeksLeft  = 0;
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;
        _pendingEvent       = null;
        _pendingChoiceEvent = null;
        _eventInProgress    = false;
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
            RandomEvents_Dev.Register(_eventPool, this, RandomEventChartLoader.Cache);
            RandomEvents_Choice.Register(_choiceEventPool, this, RandomEventChoiceChartLoader.Cache);
        }

        // ── 선택지 이벤트 우선 탐색 ─────────────────────────────
        var choiceData = _choiceEventPool.Find(e => e.type == type);
        if (choiceData != null)
        {
            RandomEventChoiceChartLoader.Apply(choiceData, type.ToString(), RandomEventChoiceChartLoader.Cache);
            choiceData.cancelled = false;
            choiceData.onSetup?.Invoke();
            if (choiceData.cancelled) return;

            if (choiceData.requiresPatrol)
            {
                _pendingEvent       = null;
                _pendingChoiceEvent = choiceData;
                if (!string.IsNullOrEmpty(choiceData.targetEmployeeId) &&
                    !string.IsNullOrEmpty(choiceData.requiredPatrolPointId))
                {
                    OfficeManager.Instance?.ForceCharacterToPatrolPoint(
                        choiceData.targetEmployeeId, choiceData.requiredPatrolPointId, stayDuration: 1f);
                }
            }
            else
            {
                DevelopmentManager.Instance.PauseForEvent();
                RandomEventChoiceUI.Instance.Show(choiceData);
            }
            return;
        }

        // ── 일반 이벤트 ─────────────────────────────────────────
        var evt = _eventPool.Find(e => e.type == type);
        if (evt == null)
        {
            Debug.LogWarning($"[EventTest] {type} 를 풀에서 찾을 수 없음");
            return;
        }

        if (evt.requiresPatrol)
        {
            RandomEventChartLoader.Apply(evt, RandomEventChartLoader.Cache);
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
            RandomEventChartLoader.Apply(evt, RandomEventChartLoader.Cache);
            evt.onSetup?.Invoke();
            DevelopmentManager.Instance.PauseForEvent();
            RandomEventUI.Instance.Show(evt);
        }
    }

    System.Collections.IEnumerator ShowEventAfterDelay(RandomEventData evt, float delay)
    {
        _eventInProgress = true;
        yield return new UnityEngine.WaitForSeconds(delay);
        DevelopmentManager.Instance.PauseForEvent();
        RandomEventUI.Instance.Show(evt);
    }

    System.Collections.IEnumerator ShowChoiceEventAfterDelay(RandomEventChoiceData choiceData, float delay)
    {
        _eventInProgress = true;
        yield return new UnityEngine.WaitForSeconds(delay);
        DevelopmentManager.Instance.PauseForEvent();
        RandomEventChoiceUI.Instance.Show(choiceData);
    }

    public void ClearEventInProgress() => _eventInProgress = false;

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

    SchedulableEntry PickWeightedEntry(List<SchedulableEntry> pool)
    {
        float total = 0f;
        foreach (var e in pool) total += e.weight;

        float roll       = UnityEngine.Random.value * total;
        float cumulative = 0f;
        SchedulableEntry selected = pool[pool.Count - 1];
        foreach (var e in pool)
        {
            cumulative += e.weight;
            if (roll <= cumulative) { selected = e; break; }
        }
        return selected;
    }
}
