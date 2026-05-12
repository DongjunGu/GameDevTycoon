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
        public string alertMessage;
        public int    weeksLeft;
    }
    private List<RunEventPayload> _pendingRunAlerts = new();

    private int _unstableCompanyWeeksLeft  = -1; // -1이면 예약 없음
    private int _coffeeRequestWeeksLeft      = -1; // -1이면 예약 없음
    private int _energyDrinkRequestWeeksLeft = -1; // -1이면 예약 없음

    private struct PendingRomancePayload
    {
        public string newEmpId;
        public string existingEmpId;
        public int    weeksLeft;
    }
    private List<PendingRomancePayload> _pendingRomanceEvents = new();

    // 패트롤 도착 대기 중인 사내 연애 이벤트
    private struct PendingRomanceEventPayload
    {
        public string newEmpId;
        public string existingEmpId;
    }
    private PendingRomanceEventPayload? _pendingRomanceEvent = null;

    private struct ActiveCouple
    {
        public string empId1;
        public string empId2;
        public int    breakUpWeeksLeft; // -1이면 이별 예정 없음
    }
    private ActiveCouple? _activeCouple = null;

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

    // ── 투자 이벤트 ───────────────────────────────────────────
    // (개발 이벤트 가중치는 뒤끝 RandomEventChoice/RandomEvent 차트의 weight 컬럼이 적용됨 — 인스펙터 필드 사용 안 함)
    // (조건 이벤트 employeeRun/employeeFight/badCompany 확률도 dead 였어서 제거됨 — 각 이벤트가 자체 트리거 로직 사용)
    [Header("투자 이벤트")]
    [Range(0f, 1f)] public float investmentTriggerChance = 0.5f;

    public int    HiringPenalty         { get; set; } = 0;    // 채용 지원 인원 감소량
    public int    HiringPenaltyEndYear  { get; set; } = -1;   // 패널티 만료 연도 (-1이면 없음)

    public void LoadHiringPenalty(int penalty, int endYear)
    {
        HiringPenalty        = penalty;
        HiringPenaltyEndYear = endYear;
    }

    public float  YoutuberSalesBonus    { get; set; } = 1.0f; // 유튜버 선공개 이벤트 매출 배율
    public bool   InvestmentAccepted    { get; set; } = false;
    public string InvestmentStat        { get; set; } = "";
    public string InvestmentStatName    { get; set; } = "";
    public float  InvestmentThreshold   { get; set; } = 0f;
    public int    InvestmentReward      { get; set; } = 0;
    public bool   PendingInvestmentUI   { get; set; } = false;
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

    // 새 런 시작 — 런타임 이벤트 상태 전부 초기화 (차트 로드된 풀은 유지)
    // GameTime 저장 시 이 값들이 row에 함께 반영됨
    public void ResetForNewRun()
    {
        _nextWeekPopups.Clear();
        _pendingRunAlerts.Clear();
        _unstableCompanyWeeksLeft     = -1;
        _coffeeRequestWeeksLeft       = -1;
        _energyDrinkRequestWeeksLeft  = -1;
        _pendingRomanceEvents.Clear();
        _pendingRomanceEvent = null;
        _activeCouple = null;
        _scheduledEvents.Clear();
        _nextScheduledIndex = 0;
        _pendingEvent = null;
        _pendingChoiceEvent = null;
        _eventInProgress = false;

        HiringPenalty        = 0;
        HiringPenaltyEndYear = -1;
        YoutuberSalesBonus   = 1.0f;
        InvestmentAccepted   = false;
        InvestmentStat       = "";
        InvestmentStatName   = "";
        InvestmentThreshold  = 0f;
        InvestmentReward     = 0;
        PendingInvestmentUI  = false;
        PendingHackyCodePenalty    = 0f;
        PendingHackyCodePortraitId = "";
        PendingHackyCodeWeeksLeft  = 0;
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

        if (_unstableCompanyWeeksLeft > 0)
        {
            _unstableCompanyWeeksLeft--;
            if (_unstableCompanyWeeksLeft == 0)
            {
                _unstableCompanyWeeksLeft = -1;
                RandomEvents_Condition.TriggerUnstableCompanyEvent(this, GameTimeManager.Instance?.Year ?? 2000);
            }
        }

        if (_coffeeRequestWeeksLeft > 0)
        {
            _coffeeRequestWeeksLeft--;
            if (_coffeeRequestWeeksLeft == 0)
            {
                _coffeeRequestWeeksLeft = -1;
                RandomEvents_Condition_Choice.TriggerCoffeeRequestEvent();
            }
        }

        if (_energyDrinkRequestWeeksLeft > 0)
        {
            _energyDrinkRequestWeeksLeft--;
            if (_energyDrinkRequestWeeksLeft == 0)
            {
                _energyDrinkRequestWeeksLeft = -1;
                RandomEvents_Condition_Choice.TriggerEnergyDrinkRequestEvent();
            }
        }

        if (_activeCouple.HasValue && _activeCouple.Value.breakUpWeeksLeft > 0)
        {
            var couple = _activeCouple.Value;
            couple.breakUpWeeksLeft--;
            if (couple.breakUpWeeksLeft <= 0)
            {
                string id1 = couple.empId1;
                string id2 = couple.empId2;
                _activeCouple = null;
                RandomEvents_Condition.TriggerRomanceBrokeUpEvent(this, id1, id2);
            }
            else
            {
                _activeCouple = couple;
            }
        }

        for (int i = _pendingRomanceEvents.Count - 1; i >= 0; i--)
        {
            var r = _pendingRomanceEvents[i];
            r.weeksLeft--;
            if (r.weeksLeft <= 0)
            {
                _pendingRomanceEvents.RemoveAt(i);
                if (!_activeCouple.HasValue && !_pendingRomanceEvent.HasValue)
                {
                    _pendingRomanceEvent = new PendingRomanceEventPayload
                    {
                        newEmpId      = r.newEmpId,
                        existingEmpId = r.existingEmpId
                    };
                    OfficeManager.Instance?.ForceCharacterToPatrolPoint(
                        r.existingEmpId, "master_desk", stayDuration: 1f);
                }
            }
            else
            {
                _pendingRomanceEvents[i] = r;
            }
        }

        for (int i = _pendingRunAlerts.Count - 1; i >= 0; i--)
        {
            var alert = _pendingRunAlerts[i];
            alert.weeksLeft--;
            if (alert.weeksLeft <= 0)
            {
                _pendingRunAlerts.RemoveAt(i);
                EmployeeManager.Instance.ReduceAllSatisfaction(10);
                string capturedMsg = alert.alertMessage;
                AlertUI.Instance.Show(capturedMsg, () =>
                {
                    if (UnityEngine.Random.value < 0.3f)
                        RandomEvents_Condition.TriggerCompanyBadReviewEvent(this, GameTimeManager.Instance?.Year ?? 2000);
                });
            }
            else
            {
                _pendingRunAlerts[i] = alert;
            }
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
        InvestmentAccepted  = false;
        InvestmentStat      = "";
        InvestmentStatName  = "";
        InvestmentReward    = 0;
        PendingInvestmentUI = false;
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
        // ── 사내 연애 (개발 이벤트와 독립적으로 체크) ──────────
        if (_pendingRomanceEvent.HasValue &&
            pointId == "master_desk" &&
            _pendingRomanceEvent.Value.existingEmpId == employeeId)
        {
            var romance = _pendingRomanceEvent.Value;
            _pendingRomanceEvent = null;
            StartCoroutine(ShowRomanceEventAfterDelay(romance.newEmpId, romance.existingEmpId, 1f));
            return;
        }

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
    // 형식: "weeksLeft:escaped_message|weeksLeft:escaped_message"
    public string GetPendingRunAlertsString()
    {
        if (_pendingRunAlerts.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var a in _pendingRunAlerts)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(a.weeksLeft);
            sb.Append(':');
            sb.Append(a.alertMessage.Replace("\n", "{NL}"));
        }
        return sb.ToString();
    }

    // ── 사내 연애 ─────────────────────────────────────────────
    public void CheckOfficeRomanceOnHire(EmployeeData newEmp)
    {
        if (_activeCouple.HasValue) return;
        if (UnityEngine.Random.value >= 0.2f) return;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null) return;

        var candidates = employees.FindAll(e => e.id != newEmp.id && e.isFemale != newEmp.isFemale);
        if (candidates.Count == 0) return;

        var partner = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _pendingRomanceEvents.Add(new PendingRomancePayload
        {
            newEmpId      = newEmp.id,
            existingEmpId = partner.id,
            weeksLeft     = 2
        });
        Debug.Log($"[사내연애] 예약: {newEmp.employeeName} ↔ {partner.employeeName}, 2주 후 발동");
    }

    public void SetActiveCouple(string empId1, string empId2)
    {
        int breakUp = UnityEngine.Random.value < 0.6f
            ? UnityEngine.Random.Range(8, 17)
            : -1;
        _activeCouple = new ActiveCouple { empId1 = empId1, empId2 = empId2, breakUpWeeksLeft = breakUp };
    }

    public void CheckCoupleOnFire(string empId)
    {
        if (!_activeCouple.HasValue) return;
        var couple = _activeCouple.Value;
        if (couple.empId1 != empId && couple.empId2 != empId) return;

        string partnerId = couple.empId1 == empId ? couple.empId2 : couple.empId1;
        _activeCouple = null;
        RandomEvents_Condition.TriggerCoupleResignationEvent(partnerId);
    }

    public void ClearCoupleIfInvolved(string empId)
    {
        if (_activeCouple.HasValue &&
            (_activeCouple.Value.empId1 == empId || _activeCouple.Value.empId2 == empId))
        {
            _activeCouple = null;
            Debug.Log("[사내연애] 커플 해소");
        }
        _pendingRomanceEvents.RemoveAll(r => r.newEmpId == empId || r.existingEmpId == empId);
        if (_pendingRomanceEvent.HasValue &&
            (_pendingRomanceEvent.Value.newEmpId == empId ||
             _pendingRomanceEvent.Value.existingEmpId == empId))
        {
            _pendingRomanceEvent = null;
        }
    }

    public string GetActiveCoupleString()
    {
        if (!_activeCouple.HasValue) return "";
        var emp1 = EmployeeManager.Instance?.GetEmployee(_activeCouple.Value.empId1);
        var emp2 = EmployeeManager.Instance?.GetEmployee(_activeCouple.Value.empId2);
        string id1 = emp1?.masterEmployeeId ?? _activeCouple.Value.empId1;
        string id2 = emp2?.masterEmployeeId ?? _activeCouple.Value.empId2;
        return $"{id1}|{id2}|{_activeCouple.Value.breakUpWeeksLeft}";
    }

    // 형식: "weeksLeft:newEmpId:existingEmpId,..."
    public string GetPendingRomanceString()
    {
        if (_pendingRomanceEvents.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var r in _pendingRomanceEvents)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(r.weeksLeft);
            sb.Append(':');
            sb.Append(r.newEmpId);
            sb.Append(':');
            sb.Append(r.existingEmpId);
        }
        return sb.ToString();
    }

    public void LoadRomanceState(string coupleStr, string pendingStr)
    {
        _activeCouple = null;
        if (!string.IsNullOrEmpty(coupleStr))
        {
            var parts = coupleStr.Split('|');
            if (parts.Length >= 2)
            {
                var employees = EmployeeManager.Instance?.ownedEmployees;
                string empId1 = employees?.Find(e => e.masterEmployeeId == parts[0])?.id ?? parts[0];
                string empId2 = employees?.Find(e => e.masterEmployeeId == parts[1])?.id ?? parts[1];
                int breakUp = parts.Length >= 3 && int.TryParse(parts[2], out int w) ? w : -1;
                _activeCouple = new ActiveCouple { empId1 = empId1, empId2 = empId2, breakUpWeeksLeft = breakUp };
            }
        }

        _pendingRomanceEvents.Clear();
        if (string.IsNullOrEmpty(pendingStr)) return;
        foreach (var entry in pendingStr.Split(','))
        {
            var parts = entry.Split(':');
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[0], out int weeks)) continue;
            _pendingRomanceEvents.Add(new PendingRomancePayload
            {
                weeksLeft     = weeks,
                newEmpId      = parts[1],
                existingEmpId = parts[2]
            });
        }
    }

    public int  GetUnstableCompanyWeeksLeft() => _unstableCompanyWeeksLeft;
    public void LoadUnstableCompanyWeeksLeft(int weeks) => _unstableCompanyWeeksLeft = weeks;

    public void ScheduleUnstableCompanyEvent()
    {
        _unstableCompanyWeeksLeft = UnityEngine.Random.Range(1, 49); // 1~48주 랜덤
        Debug.Log($"[UnstableCompany] {_unstableCompanyWeeksLeft}주 후 발동 예약");
    }

    public int  GetCoffeeRequestWeeksLeft()           => _coffeeRequestWeeksLeft;
    public void LoadCoffeeRequestWeeksLeft(int w)     => _coffeeRequestWeeksLeft = w;

    public int  GetEnergyDrinkRequestWeeksLeft()      => _energyDrinkRequestWeeksLeft;
    public void LoadEnergyDrinkRequestWeeksLeft(int w)=> _energyDrinkRequestWeeksLeft = w;

    public void ScheduleEnergyDrinkRequestEvent()
    {
        if (_energyDrinkRequestWeeksLeft > 0) return;
        _energyDrinkRequestWeeksLeft = UnityEngine.Random.Range(2, 5);
        Debug.Log($"[EnergyDrinkRequest] {_energyDrinkRequestWeeksLeft}주 후 발동 예약");
    }

    // 조건 선택지 이벤트 트리거 (patrol 지원)
    public void TriggerConditionChoiceEvent(RandomEventChoiceData choiceData)
    {
        if (HasPendingEvent) return;

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
            GameTimeManager.Instance?.StopTime();
            RandomEventChoiceUI.Instance?.Show(choiceData);
        }
    }

    public void ScheduleCoffeeRequestEvent()
    {
        // 커피 획득마다 1회 예약 (이미 예약 중이면 덮어쓰지 않음)
        if (_coffeeRequestWeeksLeft > 0) return;
        _coffeeRequestWeeksLeft = UnityEngine.Random.Range(2, 5); // 2~4주
        Debug.Log($"[CoffeeRequest] {_coffeeRequestWeeksLeft}주 후 발동 예약");
    }

    public void RestorePendingRunAlerts(string data)
    {
        _pendingRunAlerts.Clear();
        if (string.IsNullOrEmpty(data)) return;
        foreach (var entry in data.Split('|'))
        {
            int colonIdx = entry.IndexOf(':');
            if (colonIdx < 0) continue;
            if (!int.TryParse(entry.Substring(0, colonIdx), out int weeks)) continue;
            string msg = entry.Substring(colonIdx + 1).Replace("{NL}", "\n");
            _pendingRunAlerts.Add(new RunEventPayload { alertMessage = msg, weeksLeft = weeks });
        }
    }

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
        YoutuberSalesBonus       = 1.0f;
        InvestmentAccepted       = false;
        InvestmentStat           = "";
        InvestmentStatName       = "";
        InvestmentReward         = 0;
        PendingInvestmentUI      = false;
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
        RandomEvents_Condition_Choice.TriggerInvestmentEvent(onComplete);
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

        int reward  = InvestmentReward;
        int penalty = UnityEngine.Mathf.RoundToInt(reward * 1.5f);

        if (value >= InvestmentThreshold)
        {
            MoneyManager.Instance.AddGold(reward);
            AlertUI.Instance.Show(
                $"투자 이벤트 성공!\n리워드 {reward:N0}G를 얻었습니다",
                () => onComplete?.Invoke());
        }
        else
        {
            MoneyManager.Instance.ForceSpendGold(penalty);
            AlertUI.Instance.Show(
                $"투자 이벤트 실패\n위약금 {penalty:N0}G를 잃었습니다",
                () => onComplete?.Invoke());
        }
    }

    // ── 조건 이벤트 ───────────────────────────────────────────
    public void CheckConditionEvents()
    {
        // 만족도 90이상: 10% 확률로 자발적 야근 (개발 중에만)
        if (DevelopmentManager.Instance?.CurrentStage == ProjectStage.Developing &&
            !DevelopmentManager.Instance.IsVoluntaryOvertimeActive)
        {
            var highSatCandidates = new List<EmployeeData>();
            foreach (var emp in EmployeeManager.Instance.ownedEmployees)
                if (emp.satisfaction >= 90) highSatCandidates.Add(emp);

            if (highSatCandidates.Count > 0 && UnityEngine.Random.value < 0.1f)
            {
                var target = highSatCandidates[UnityEngine.Random.Range(0, highSatCandidates.Count)];
                RandomEvents_Condition.TriggerVoluntaryOvertimeEvent(target);
                return;
            }
        }

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

    public bool CheckUnstableCompanyOnNewYear(int newYear) =>
        RandomEvents_Condition.CheckUnstableCompanyOnNewYear(this, newYear);

    // ── stub ──────────────────────────────────────────────────

    public void TriggerEmployeeResignationEvent(EmployeeData emp)
    {
        bool   isOvertime = false;
        string message    = RandomEvents_Condition.GetResignationMessage(isOvertime);
        string title      = RandomEvents_Condition.GetTitle("EmployeeResignation") ?? "사직서 제출";

        EventUI.Instance.Show(title, emp.portraitId, $"{emp.employeeName}\n\n{message}", () =>
        {
            EmployeeManager.Instance.FireEmployee(emp);
            EmployeeManager.Instance.ReduceAllSatisfactionExcept(10, emp);
            AlertUI.Instance.Show(RandomEvents_Condition.GetResignationSystemMessage(emp.employeeName), () =>
            {
                if (UnityEngine.Random.value < 0.3f)
                    RandomEvents_Condition.TriggerCompanyBadReviewEvent(this, GameTimeManager.Instance?.Year ?? 2000);
            });
        });
    }

    public void TriggerEmployeeRunEvent(EmployeeData emp)
    {
        EmployeeManager.Instance.FireEmployee(emp);

        // 즉시 EventUI — 제목 없음, 직원 portrait, 랜덤 도망 메시지
        EventUI.Instance.Show("", emp.portraitId, RandomEvents_Condition.GetRunAwayMessage());

        // 2주 후 AlertUI 예약
        RandomEventConditionChartRow runRow = null;
        RandomEventConditionChartLoader.Cache?.TryGetValue("EmployeeRun", out runRow);
        string alertMsg = !string.IsNullOrEmpty(runRow?.systemMessage)
            ? runRow.systemMessage.Replace("{해당직원이름}", emp.employeeName)
            : $"{emp.employeeName}이 도망쳤습니다!\n남은 팀원들의 만족도가 10 하락합니다.";

        _pendingRunAlerts.Add(new RunEventPayload { alertMessage = alertMsg, weeksLeft = 2 });
    }

    // ── 테스트용 즉시 발동 ────────────────────────────────────
    public void TriggerEventTest(RandomEventType type)
    {
        // ── 조건 이벤트 ─────────────────────────────────────────
        if (type == RandomEventType.UnstableCompany)
        {
            int year = GameTimeManager.Instance?.Year ?? 2000;
            if (UnityEngine.Random.value < 0.5f)
                RandomEvents_Condition.TriggerBadRumorEvent(this, year);
            else
                RandomEvents_Condition.TriggerAnxietyInducingEvent();
            return;
        }
        if (type == RandomEventType.BadRumor)
        {
            RandomEvents_Condition.TriggerBadRumorEvent(this, GameTimeManager.Instance?.Year ?? 2000);
            return;
        }
        if (type == RandomEventType.AnxietyInducing)
        {
            RandomEvents_Condition.TriggerAnxietyInducingEvent();
            return;
        }
        if (type == RandomEventType.CompanyBadReview)
        {
            RandomEvents_Condition.TriggerCompanyBadReviewEvent(this, GameTimeManager.Instance?.Year ?? 2000);
            return;
        }
        if (type == RandomEventType.RomanceBrokeUp)
        {
            if (!_activeCouple.HasValue)
            {
                Debug.LogWarning("[EventTest] RomanceBrokeUp 테스트 실패 — 활성 커플 없음");
                return;
            }
            string id1 = _activeCouple.Value.empId1;
            string id2 = _activeCouple.Value.empId2;
            _activeCouple = null;
            RandomEvents_Condition.TriggerRomanceBrokeUpEvent(this, id1, id2);
            return;
        }
        if (type == RandomEventType.OfficeRomance)
        {
            var employees = EmployeeManager.Instance?.ownedEmployees;
            if (employees == null || employees.Count < 2)
            {
                Debug.LogWarning("[EventTest] 사내연애 테스트 실패 — 직원 2명 이상 필요");
                return;
            }
            // 이성 쌍 탐색
            EmployeeData emp1 = null, emp2 = null;
            foreach (var e in employees)
            {
                var partner = employees.Find(p => p.id != e.id && p.isFemale != e.isFemale);
                if (partner != null) { emp1 = e; emp2 = partner; break; }
            }
            if (emp1 == null)
            {
                Debug.LogWarning("[EventTest] 사내연애 테스트 실패 — 이성 직원 없음");
                return;
            }
            _pendingRomanceEvent = new PendingRomanceEventPayload
            {
                newEmpId      = emp1.id,
                existingEmpId = emp2.id
            };
            OfficeManager.Instance?.ForceCharacterToPatrolPoint(emp2.id, "master_desk", stayDuration: 1f);
            return;
        }
        if (type == RandomEventType.EmployeeResignation || type == RandomEventType.EmployeeRun)
        {
            var employees = EmployeeManager.Instance?.ownedEmployees;
            if (employees == null || employees.Count == 0)
            {
                Debug.LogWarning($"[EventTest] 조건 이벤트 테스트 실패 — 보유 직원 없음");
                return;
            }
            var emp = employees[UnityEngine.Random.Range(0, employees.Count)];
            if (type == RandomEventType.EmployeeResignation)
                TriggerEmployeeResignationEvent(emp);
            else
                TriggerEmployeeRunEvent(emp);
            return;
        }

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

    System.Collections.IEnumerator ShowRomanceEventAfterDelay(string newEmpId, string existingEmpId, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        RandomEvents_Condition.TriggerOfficeRomanceEvent(this, newEmpId, existingEmpId);
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
