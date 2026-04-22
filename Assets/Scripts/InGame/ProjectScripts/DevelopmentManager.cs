using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LeaderType { Planner, Programmer, Artist }

public class DevelopmentManager : MonoBehaviour
{
    public static DevelopmentManager Instance { get; private set; }

    [Header("Settings")]
    public bool IsVoluntaryOvertimeActive { get; private set; } = false;
    public void SetVoluntaryOvertime(bool active) => IsVoluntaryOvertimeActive = active;


    public float developmentDuration = 180f;
    public float bugDurationRate = 0.2f;
    public EmployeeData plannerLeader;
    public EmployeeData programmerLeader;
    public EmployeeData artistLeader;
    public float GetElapsed() => _elapsed;

    [Header("Leader Settings")]
    public float leaderTickDelay = 0.5f;

    [Header("Mode")]
    public bool IsOvertimeMode = false;

    public float BugPenalty { get; private set; } = 0f;
    public float BugEventBonus { get; private set; } = 0f; // 버그 이벤트 (미개발)

    public bool IsStarted { get; private set; } = false;
    public bool IsTriggered25 => _triggered25;
    public bool IsTriggered75 => _triggered75;
    public bool IsPendingLeaderScore25 => _pendingLeaderScore25;
    public bool IsPendingLeaderScore75 => _pendingLeaderScore75;
    public float LeaderDevelopBonusTotal  => _leaderDevelopBonusTotal;
    public float LeaderPlanningBonusTotal => _leaderPlanningBonusTotal;
    public float LeaderArtBonusTotal      => _leaderArtBonusTotal;
    public void SetLeaderDevelopBonusTotal(float val)  => _leaderDevelopBonusTotal  = val;
    public void SetLeaderPlanningBonusTotal(float val) => _leaderPlanningBonusTotal = val;
    public void SetLeaderArtBonusTotal(float val)      => _leaderArtBonusTotal      = val;
    public float GetLeaderBonusByRole(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner    => _leaderPlanningBonusTotal,
        EmployeeRole.Programmer => _leaderDevelopBonusTotal,
        EmployeeRole.Artist     => _leaderArtBonusTotal,
        _ => 0f
    };
    public ProjectStage CurrentStage { get; set; } = ProjectStage.None;

    private float _elapsed;
    private bool _isRunning;
    private bool _triggered25;
    private bool _triggered75;
    private bool _patrolStarted;
    private bool _pendingLeaderScore25;
    private bool _pendingLeaderScore75;
    private bool _pendingDevelopmentComplete;
    private float _leaderDevelopBonusTotal;
    private float _leaderPlanningBonusTotal;
    private float _leaderArtBonusTotal;

    // 네트워크 이벤트 등 duration 연장 시 진행도 표시 보정
    private float _progressVisualOffset = 0f;      // 현재 남은 시각 보정값
    private float _progressOffsetElapsedAtEvent = 0f; // 이벤트 발동 시점 _elapsed
    private float _progressOffsetExtension = 0f;   // 연장된 초 (보정 감소 기준)

    private Dictionary<string, List<float>> _tickTimesMap = new();
    private Dictionary<string, int> _tickIndexMap = new();
    private Dictionary<string, int[]> _tickOrderMap = new();
    private Dictionary<string, CharacterState> _prevStateMap = new();
    private Dictionary<string, int> _midDevSeeds = new();
    private Dictionary<string, float> _midDevElapsed = new();
    private Coroutine _bugFixCoroutine;
    private bool _bugFixReleased = false;
    private int _tickSeed;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDevelopment()
    {
        GameTimeManager.Instance.SetProjectSpeed(ProjectSetupUI.SelectedScale);
        float baseDuration = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f,
            ProjectScale.Medium => 100.8f,
            ProjectScale.Large  => 124.8f,
            _ => 80f
        };

        // 인원 수에 따른 개발 기간 조정 (추천 인원 대비 1명 차이 = 1주 증감)
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended; // 양수: 초과(기간 감소), 음수: 부족(기간 증가)
        float secondsPerWeek = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 5f,
            ProjectScale.Medium => 4.2f,
            ProjectScale.Large  => 3.9f,
            _ => 5f
        };
        developmentDuration = baseDuration - diff * secondsPerWeek;

        IsStarted = true;
        _patrolStarted = false;
        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;

        DevelopmentPanelUI.Instance.ResetValues();
        RandomEventManager.Instance.InitEvents();
        RandomEventManager.Instance.ScheduleEvents();
        GameTimeManager.Instance.StopTime();

        void ProceedToInvestment()
        {
            InitTickMap(); // 야근 선택 완료 후 시드 확정
            RandomEventManager.Instance.TriggerInvestmentEvent(() => //투자 이벤트
            {
                LeaderSelectUI.Instance.Open(LeaderType.Planner, () =>
                {
                    GameTimeManager.Instance.ForceStartTime();
                    _isRunning = true;
                    StartCoroutine(DevelopmentCoroutine());
                });
            });
        }

        void ProceedToOvertimeSelect()
        {
            if (StageManager.Instance != null && StageManager.Instance.CurrentStage >= 2 && OvertimeSelectUI.Instance != null)
                OvertimeSelectUI.Instance.Open(ProceedToInvestment);
            else
                ProceedToInvestment();
        }

        if (diff != 0)
        {
            int absDiff = Mathf.Abs(diff);
            string sign   = diff > 0 ? "많아서" : "적어서";
            string effect = diff > 0 ? "줄었습니다" : "늘었습니다";
            AlertUI.Instance.Show(
                $"개발 인원이 추천보다 {absDiff}명 {sign}\n개발기간이 {absDiff}주 {effect}.",
                ProceedToOvertimeSelect
            );
        }
        else
        {
            ProceedToOvertimeSelect();
        }
    }

    void InitTickMap()
    {
        _tickSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        InitTickMapWithSeed(_tickSeed);
    }

    void InitTickMapWithSeed(int seed)
    {
        var rng = new System.Random(seed);
        _tickTimesMap.Clear();
        _tickIndexMap.Clear();
        _tickOrderMap.Clear();

        string[] tickTypeNames = { "잭팟", "성공", "창의성", "버그", "꽝" };

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            const int tickCount = 12;
            float segmentSize = developmentDuration / tickCount;

            var times = new List<float>();
            for (int i = 0; i < tickCount; i++)
                times.Add(i * segmentSize + (float)(rng.NextDouble() * segmentSize));

            _tickTimesMap[employee.id] = times;
            _tickIndexMap[employee.id] = 0;
            bool empOvertime = employee.isOvertimeWorker || IsOvertimeMode || IsVoluntaryOvertimeActive;
            int[] order = BuildTickOrder(tickCount, empOvertime, rng);
            _tickOrderMap[employee.id] = order;

            var sb = new System.Text.StringBuilder();
            string overtimeTag = empOvertime ? " [야근모드 적용됨]" : "";
            sb.Append($"[틱 시드={seed}] {employee.employeeName}({employee.id[..8]}){overtimeTag} : ");
            for (int i = 0; i < tickCount; i++)
            {
                sb.Append($"[{i + 1}] {times[i]:F2}s/{tickTypeNames[order[i]]}");
                if (i < tickCount - 1) sb.Append("  ");
            }
            Debug.Log(sb.ToString());
        }
    }

    public int GetTickSeed() => _tickSeed;

    void RestoreTickIndices(string tickIndices)
    {
        if (string.IsNullOrEmpty(tickIndices)) return;
        foreach (var entry in tickIndices.Split(','))
        {
            var parts = entry.Split(':');
            if (parts.Length != 2) continue;
            string empId = parts[0];
            if (int.TryParse(parts[1], out int idx) && _tickIndexMap.ContainsKey(empId))
                _tickIndexMap[empId] = idx;
        }
    }

    public string GetTickIndices()
    {
        var parts = new System.Text.StringBuilder();
        foreach (var kv in _tickIndexMap)
        {
            if (parts.Length > 0) parts.Append(',');
            parts.Append($"{kv.Key}:{kv.Value}");
        }
        return parts.ToString();
    }

    int[] BuildTickOrder(int total, bool overtime, System.Random rng = null)
    {
        // 0:잭팟, 1:성공, 2:창의성, 3:버그, 4:꽝
        // 일반: 10%, 35%, 15%, 10%, 30%
        // 야근: 15%, 30%, 15%, 15%, 25%
        float[] probs = overtime
            ? new float[] { 0.15f, 0.30f, 0.15f, 0.15f, 0f }
            : new float[] { 0.10f, 0.35f, 0.15f, 0.10f, 0f };

        int jackpot    = Mathf.Max(1, Mathf.RoundToInt(total * probs[0]));
        int success    = Mathf.RoundToInt(total * probs[1]);
        int creativity = Mathf.RoundToInt(total * probs[2]);
        int bug        = Mathf.Max(1, Mathf.RoundToInt(total * probs[3]));
        int blank      = total - jackpot - success - creativity - bug;

        var list = new List<int>();
        for (int i = 0; i < jackpot;    i++) list.Add(0);
        for (int i = 0; i < success;    i++) list.Add(1);
        for (int i = 0; i < creativity; i++) list.Add(2);
        for (int i = 0; i < bug;        i++) list.Add(3);
        for (int i = 0; i < blank;      i++) list.Add(4);

        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = rng != null ? rng.Next(0, i + 1) : UnityEngine.Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
        return list.ToArray();
    }

    IEnumerator DevelopmentCoroutine()
    {
        CurrentStage = ProjectStage.Developing;
        _isRunning = true;
        OfficeManager.Instance?.SetAllWorking();
        if (!_patrolStarted)
        {
            _patrolStarted = true;
            OfficeManager.Instance?.StartDevelopmentPatrol();
        }

        while (_elapsed < developmentDuration)
        {
            if (!_isRunning || !GameTimeManager.Instance.IsRunning)
            {
                yield return null;
                continue;
            }


            // _elapsed += Time.deltaTime * GetElapsedMultiplier(); // 인원 수 기반 elapsed 가속 (duration 직접 조정 방식으로 대체)
            _elapsed += Time.deltaTime;
            float progress = _elapsed / developmentDuration;

            RandomEventManager.Instance.CheckProgress(progress);

            foreach (var employee in EmployeeManager.Instance.ownedEmployees.ToList())
            {
                if (!_tickTimesMap.ContainsKey(employee.id)) continue;

                CharacterState curState = OfficeManager.Instance.GetState(employee.id);
                _prevStateMap.TryGetValue(employee.id, out CharacterState prevState);

                if (prevState == CharacterState.Patrolling && curState == CharacterState.Working)
                    ReschedulePendingTicks(employee.id);

                _prevStateMap[employee.id] = curState;

                int index = _tickIndexMap[employee.id];
                var times = _tickTimesMap[employee.id];
                int[] order = _tickOrderMap[employee.id];

                if (index < times.Count && _elapsed >= times[index])
                {
                    if (curState == CharacterState.Working)
                    {
                        AccumulateByType(employee, order[index]);
                        _tickIndexMap[employee.id]++;
                    }
                }
            }

            if (!_triggered25 && progress >= 0.25f)
            {
                _triggered25 = true;
                _isRunning = false;

                if (RandomEventManager.Instance.HasPendingEvent)
                {
                    // pending 중: 시간·캐릭터 이동은 유지, 진행도만 중단
                    _pendingLeaderScore25 = true;
                    yield break;
                }

                // pending 없음: 시간도 멈추고 팀장 선택 UI 표시
                GameTimeManager.Instance.StopTime();
                LeaderSelectUI.Instance.Open(LeaderType.Programmer, null);
                yield break;
            }

            if (!_triggered75 && progress >= 0.75f)
            {
                _triggered75 = true;
                _isRunning = false;

                if (RandomEventManager.Instance.HasPendingEvent)
                {
                    // pending 중: 시간·캐릭터 이동은 유지, 진행도만 중단
                    _pendingLeaderScore75 = true;
                    yield break;
                }

                // pending 없음: 시간도 멈추고 팀장 선택 UI 표시
                GameTimeManager.Instance.StopTime();
                LeaderSelectUI.Instance.Open(LeaderType.Artist, null);
                yield break;
            }

            yield return null;
        }

        if (RandomEventManager.Instance.HasPendingEvent)
        {
            // pending 이벤트가 끝난 뒤 ResumeFromEvent()에서 처리
            _pendingDevelopmentComplete = true;
            _isRunning = false;  // 진행도만 멈춤 (시간·캐릭터 이동은 계속)
            yield break;
        }

        OnDevelopmentComplete();
    }

    void FireRemainingTicks()
    {
        foreach (var employee in EmployeeManager.Instance.ownedEmployees.ToList())
        {
            if (!_tickTimesMap.ContainsKey(employee.id)) continue;

            var times  = _tickTimesMap[employee.id];
            int[] order = _tickOrderMap[employee.id];
            int index  = _tickIndexMap[employee.id];

            while (index < times.Count)
            {
                AccumulateByType(employee, order[index]);
                index++;
            }
            _tickIndexMap[employee.id] = index;
        }
    }

    void OnDevelopmentComplete()
    {
        FireRemainingTicks();
        _isRunning = false;
        OfficeManager.Instance?.StopDevelopmentPatrol();
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show(
            "개발 완료!\n디버깅 작업을 시작합니다.",
            () =>
            {
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                MoneyManager.Instance?.SaveMoney();
                _bugFixReleased = false;
                _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
            }
        );
    }

    IEnumerator BugFixCoroutine()
    {
        CurrentStage = ProjectStage.BugFixing;
        OfficeManager.Instance?.SetAllWorking();
        OfficeManager.Instance?.StopDevelopmentPatrol();
        GameTimeManager.Instance.StartTime();

        float initialBug = DevelopmentPanelUI.Instance.GetBug();
        if (initialBug <= 0f)
        {
            if (_bugFixReleased || CurrentStage != ProjectStage.BugFixing) { _bugFixReleased = false; yield break; }
            GameTimeManager.Instance.StopTime();
            AlertUI.Instance.Show("버그 작업이 끝났습니다.", () => ShowResult());
            yield break;
        }

        const float tickInterval = 3f;
        float elapsed = 0f;

        while (DevelopmentPanelUI.Instance.GetBug() > 0f)
        {
            if (!GameTimeManager.Instance.IsRunning)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            if (elapsed < tickInterval)
            {
                yield return null;
                continue;
            }
            elapsed = 0f;

            float bugFixChance = IsOvertimeMode ? 0.6f : 0.5f;
            if (UnityEngine.Random.value < bugFixChance)
            {
                var workers = EmployeeManager.Instance.ownedEmployees
                    .Where(e => OfficeManager.Instance.GetState(e.id) == CharacterState.Working)
                    .ToList();

                if (workers.Count == 0)
                {
                    yield return null;
                    continue;
                }

                float remainingBug = DevelopmentPanelUI.Instance.GetBug();
                float perfSum = 0f;
                foreach (var e in workers) perfSum += e.perfectionSkill;
                float avgPerfection = perfSum / workers.Count;

                float fixAmount = Mathf.Max(1f,
                    Mathf.Ceil((avgPerfection / 80f) * Mathf.Sqrt(remainingBug / initialBug)));

                DevelopmentPanelUI.Instance.SetBug(Mathf.Max(0f, remainingBug - fixAmount));

                var target = workers[UnityEngine.Random.Range(0, workers.Count)];
                OfficeManager.Instance?.ShowStatPopup(target.id, $"[버그수정] -{fixAmount:F0}", new Color(0.4f, 1f, 0.6f));
            }

            yield return null;
        }

        if (_bugFixReleased || CurrentStage != ProjectStage.BugFixing) { _bugFixReleased = false; yield break; }
        DevelopmentPanelUI.Instance.SetBug(0f);
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show("버그 작업이 끝났습니다.", () =>
        {
            if (_bugFixReleased || CurrentStage != ProjectStage.BugFixing) { _bugFixReleased = false; return; }
            ShowResult();
        });
    }

    void ShowResult()
    {
        CurrentStage = ProjectStage.Complete;
        GameTimeManager.Instance.ResetSpeed();
        GameTimeManager.Instance.StopTime();

        float planning = DevelopmentPanelUI.Instance.GetPlanning();
        float develop = DevelopmentPanelUI.Instance.GetDevelop();
        float art = DevelopmentPanelUI.Instance.GetArt();
        float bug = DevelopmentPanelUI.Instance.GetBug();
        float creativity = DevelopmentPanelUI.Instance.GetCreativity();

        RandomEventManager.Instance.CheckInvestmentResult(planning, develop, art, creativity, () =>
        {
            DevelopmentResultUI.Instance.Show(planning, develop, art, bug, creativity);
        });
    }

    public void OnClickRelease()
    {
        _bugFixReleased = true;
        if (_bugFixCoroutine != null)
        {
            StopCoroutine(_bugFixCoroutine);
            _bugFixCoroutine = null;
        }

        float remainingBug = DevelopmentPanelUI.Instance.GetBug();
        float triggerProb = Mathf.Min(1f, remainingBug * 0.02f);

        BugPenalty = 0f;
        if (UnityEngine.Random.value < triggerProb)
        {
            BugPenalty = remainingBug switch
            {
                < 3f  => 0.03f,
                < 6f  => 0.06f,
                < 10f => 0.09f,
                < 20f => 0.12f,
                _     => 0.15f
            };
        }

        string msg = BugPenalty > 0f
            ? $"디버깅을 중단합니다.\n{BugPenalty * 100f:F0}% 감점\n출시 시작"
            : "디버깅을 중단합니다.\n출시 시작";

        AlertUI.Instance.Show(msg, () =>
        {
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            MoneyManager.Instance?.SaveMoney();
            ShowResult();
        });
    }

    public void SetLeader(LeaderType type, EmployeeData employee)
    {
        switch (type)
        {
            case LeaderType.Planner: plannerLeader = employee; break;
            case LeaderType.Programmer: programmerLeader = employee; break;
            case LeaderType.Artist: artistLeader = employee; break;
        }

        // 같은 역할 다른 직원 연속 횟수 리셋, 선택 직원 카운트 증가
        EmployeeRole filterRole = type switch
        {
            LeaderType.Planner    => EmployeeRole.Planner,
            LeaderType.Programmer => EmployeeRole.Programmer,
            LeaderType.Artist     => EmployeeRole.Artist,
            _ => EmployeeRole.Planner
        };
        var jealousyCandidates = new System.Collections.Generic.List<EmployeeData>();
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            if (emp.role == filterRole && emp.id != employee.id)
            {
                emp.consecutiveLeaderCount = 0;
                emp.consecutiveNonLeaderCount++;
                if (emp.consecutiveNonLeaderCount >= 4)
                    jealousyCandidates.Add(emp);
            }
        }
        employee.consecutiveLeaderCount++;
        employee.consecutiveNonLeaderCount = 0;

        EmployeeData jealousyTarget = jealousyCandidates.Count > 0
            ? jealousyCandidates[UnityEngine.Random.Range(0, jealousyCandidates.Count)] : null;

        int skill = type switch
        {
            LeaderType.Planner => employee.planningSkill,
            LeaderType.Programmer => employee.developSkill,
            LeaderType.Artist => employee.artSkill,
            _ => 0
        };

        int n = CalcLeaderTickCount(skill);

        float total = CalcLeaderScore(skill, n);
        if (type == LeaderType.Programmer) _leaderDevelopBonusTotal  = total;
        if (type == LeaderType.Planner)    _leaderPlanningBonusTotal = total;
        if (type == LeaderType.Artist)     _leaderArtBonusTotal      = total;
        int weightSum = n * (n + 1) / 2;

        int[] weights = new int[n];
        for (int i = 0; i < n; i++) weights[i] = i + 1;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = weights[i]; weights[i] = weights[j]; weights[j] = tmp;
        }

        float[] scores = new float[n];
        for (int i = 0; i < n; i++)
            scores[i] = total * weights[i] / weightSum;

        int leaderCount = employee.consecutiveLeaderCount;
        GameTimeManager.Instance.StopTime();
        LeaderScoreUI.Instance.Show(employee, type, scores, leaderTickDelay, () =>
        {
            void StartDeveloping()
            {
                _isRunning = true;
                CurrentStage = ProjectStage.Developing;
                GameTimeManager.Instance.ForceStartTime();
                Debug.Log("팀장점수완료 저장");

                MoneyManager.Instance.SaveMoney();
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                EmployeeManager.Instance.SaveAllEmployees();
                GameTimeManager.Instance.ForceStartTime();
                StartCoroutine(DevelopmentCoroutine());
            }

            void AfterBurnout()
            {
                if (jealousyTarget != null && UnityEngine.Random.value < 0.7f)
                    RandomEvents_Condition.TriggerLeaderJealousyEvent(jealousyTarget, StartDeveloping);
                else
                    StartDeveloping();
            }

            if (leaderCount >= 3 && UnityEngine.Random.value < 0.5f)
                RandomEvents_Condition.TriggerLeaderBurnoutEvent(employee, leaderCount, AfterBurnout);
            else
                AfterBurnout();
        });
    }

    public void RestoreState(
        float elapsed, bool triggered25, bool triggered75,
        string plannerLeaderId, string programmerLeaderId, string artistLeaderId,
        float accumPlanning, float accumDevelop, float accumArt,
        float accumBug, float accumCreativity,
        ProjectStage stage,
        int tickSeed = 0, string tickIndices = "", string midDevData = "",
        float savedDuration = 0f, float networkSlowEndElapsed = 0f,
        float progOffsetElapsedAtEvent = 0f, float progOffsetExtension = 0f, float progVisualOffset = 0f,
        bool pendingLeaderScore25 = false, bool pendingLeaderScore75 = false)
    {
        float baseDuration = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f,
            ProjectScale.Medium => 100.8f,
            ProjectScale.Large  => 124.8f,
            _ => 80f
        };
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended;
        float secondsPerWeek = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 5f,
            ProjectScale.Medium => 4.2f,
            ProjectScale.Large  => 3.9f,
            _ => 5f
        };
        developmentDuration = savedDuration > 0f ? savedDuration : baseDuration - diff * secondsPerWeek;
        _elapsed = elapsed;

        // 네트워크 이슈 progress 보정 복원
        _progressOffsetElapsedAtEvent = progOffsetElapsedAtEvent;
        _progressOffsetExtension = progOffsetExtension;
        _progressVisualOffset = progVisualOffset;

        // 캐릭터 속도 감소 복원
        if (networkSlowEndElapsed > elapsed)
        {
            OfficeManager.Instance?.SetCharacterSpeedMultiplier(0.5f);
            StartCoroutine(RestoreCharacterSpeedAfter(networkSlowEndElapsed - elapsed));
        }
        _triggered25 = triggered25;
        _triggered75 = triggered75;
        CurrentStage = stage;
        IsStarted = true;
        RandomEventManager.Instance.InitEvents();

        plannerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == plannerLeaderId);
        programmerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == programmerLeaderId);
        artistLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == artistLeaderId);

        DevelopmentPanelUI.Instance.SetValues(accumPlanning, accumDevelop, accumArt, accumBug, accumCreativity);

        switch (stage)
        {
            case ProjectStage.Developing:
                _tickSeed = tickSeed != 0 ? tickSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);

                // 연장이 있으면 원본 duration으로 틱 생성 후 동일한 scaling 재적용
                if (progOffsetExtension > 0f && savedDuration > 0f)
                {
                    float preDuration = savedDuration - progOffsetExtension;
                    float oldRemaining = preDuration - progOffsetElapsedAtEvent;
                    float newRemaining = savedDuration - progOffsetElapsedAtEvent;
                    float scale = oldRemaining > 0f ? newRemaining / oldRemaining : 1f;

                    // 원본 직원: preDuration 기준 생성
                    developmentDuration = preDuration;
                    InitTickMapWithSeed(_tickSeed);

                    // mid-dev 직원: preDuration 기준 생성 (실시간과 동일 조건)
                    RestoreMidDevData(midDevData);
                    developmentDuration = savedDuration;

                    // 원본 + mid-dev 모두 동일한 scaling 적용
                    foreach (var empId in new System.Collections.Generic.List<string>(_tickIndexMap.Keys))
                    {
                        if (!_tickTimesMap.ContainsKey(empId)) continue;
                        var times = _tickTimesMap[empId];
                        for (int i = 0; i < times.Count; i++)
                            if (times[i] > progOffsetElapsedAtEvent)
                                times[i] = progOffsetElapsedAtEvent + (times[i] - progOffsetElapsedAtEvent) * scale;
                    }
                }
                else
                {
                    InitTickMapWithSeed(_tickSeed);
                    RestoreMidDevData(midDevData);
                }

                RestoreTickIndices(tickIndices);

                // 저장 시점에 팀장 점수가 미뤄진 상태였으면 LeaderSelectUI부터 표시
                if (pendingLeaderScore75)
                {
                    _pendingLeaderScore75 = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    LeaderSelectUI.Instance.Open(LeaderType.Artist, null);
                }
                else if (pendingLeaderScore25)
                {
                    _pendingLeaderScore25 = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    LeaderSelectUI.Instance.Open(LeaderType.Programmer, null);
                }
                else
                {
                    _isRunning = true;
                    GameTimeManager.Instance.ForceStartTime();
                    StartCoroutine(DevelopmentCoroutine());
                }
                break;

            case ProjectStage.BugFixing:
                _isRunning = true;
                _bugFixReleased = false;
                GameTimeManager.Instance.ForceStartTime();
                _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
                break;
        }

        Debug.Log($"프로젝트 복원 완료: {stage} / elapsed: {elapsed:F1}");
    }

    public float GetNetworkSlowEndElapsed() =>
        _progressOffsetExtension > 0f ? _progressOffsetElapsedAtEvent + _progressOffsetExtension : 0f;
    public float GetProgressVisualOffset()       => _progressVisualOffset;
    public float GetProgressOffsetElapsedAtEvent() => _progressOffsetElapsedAtEvent;
    public float GetProgressOffsetExtension()    => _progressOffsetExtension;

    public float GetProgress()
    {
        if (developmentDuration <= 0f) return 0f;
        float actual = _elapsed / developmentDuration;
        if (_progressVisualOffset <= 0f) return actual;

        float t = Mathf.Clamp01((_elapsed - _progressOffsetElapsedAtEvent) / _progressOffsetExtension);
        float offset = _progressVisualOffset * (1f - t);
        return actual + offset;
    }

    int CalcLeaderTickCount(int skill)
    {
        float rand = UnityEngine.Random.value;
        if (skill < 50)  return rand < 0.70f ? 1 : 2;
        if (skill < 100) return rand < 0.50f ? 1 : 2;
        if (skill < 150) return rand < 0.30f ? 1 : 2;
        if (skill < 200) return rand < 0.80f ? 2 : 3;
        if (skill < 250) return rand < 0.65f ? 2 : 3;
        if (skill < 300) return rand < 0.50f ? 2 : 3;
        if (skill < 350) return rand < 0.35f ? 2 : 3;
        if (skill < 400) return rand < 0.20f ? 2 : 3;
        if (skill < 450) return rand < 0.85f ? 3 : 4;
        if (skill < 500) return rand < 0.75f ? 3 : 4;
        if (skill < 550) return rand < 0.65f ? 3 : 4;
        if (skill < 600) return rand < 0.55f ? 3 : 4;
        if (skill < 650) return rand < 0.45f ? 3 : 4;
        if (skill < 700) return rand < 0.35f ? 3 : 4;
        if (skill < 750) return rand < 0.25f ? 3 : 4;
        return rand < 0.15f ? 3 : 4;
    }

    float CalcLeaderScore(int skill, int n)
    {
        double bonus = 0.1 + 0.4 * (skill / 800.0);
        double base_ = 20.0 * System.Math.Pow(1.0031, skill);
        double score = base_ * (1.0 + bonus * (n - 1));
        float rand = UnityEngine.Random.Range(0.85f, 1.15f);
        return (float)(score * rand);
    }

    public void OnEmployeeFired(string id)
    {
        _tickTimesMap.Remove(id);
        _tickIndexMap.Remove(id);
        _tickOrderMap.Remove(id);
        _prevStateMap.Remove(id);
        _midDevSeeds.Remove(id);
        _midDevElapsed.Remove(id);

        if (plannerLeader?.id == id)    plannerLeader = null;
        if (programmerLeader?.id == id) programmerLeader = null;
        if (artistLeader?.id == id)     artistLeader = null;
    }

    public void OnEmployeeHired(EmployeeData employee)
    {
        if (!IsStarted || CurrentStage != ProjectStage.Developing) return;

        float remaining = developmentDuration - _elapsed;
        float ratio = remaining / developmentDuration;
        int tickCount = Mathf.Max(1, Mathf.RoundToInt(12 * ratio));

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        _midDevSeeds[employee.id] = seed;
        _midDevElapsed[employee.id] = _elapsed;

        BuildMidDevTicks(employee.id, seed, _elapsed, tickCount);
    }

    void BuildMidDevTicks(string empId, int seed, float elapsedAtHire, int tickCount)
    {
        var rng = new System.Random(seed);
        float remaining = developmentDuration - elapsedAtHire;
        float segmentSize = remaining / tickCount;

        var times = new List<float>();
        for (int i = 0; i < tickCount; i++)
            times.Add(elapsedAtHire + i * segmentSize + (float)(rng.NextDouble() * segmentSize));

        _tickTimesMap[empId] = times;
        _tickIndexMap[empId] = 0;
        var empData = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == empId);
        bool empOvertime = (empData?.isOvertimeWorker ?? false) || IsOvertimeMode || IsVoluntaryOvertimeActive;
        _tickOrderMap[empId] = BuildTickOrder(tickCount, empOvertime, rng);
    }

    public string GetMidDevData()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in _midDevSeeds)
        {
            if (sb.Length > 0) sb.Append(',');
            float elapsed = _midDevElapsed.TryGetValue(kv.Key, out float e) ? e : 0f;
            sb.Append($"{kv.Key}:{kv.Value}:{elapsed.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
        }
        return sb.ToString();
    }

    void RestoreMidDevData(string midDevData)
    {
        if (string.IsNullOrEmpty(midDevData)) return;
        _midDevSeeds.Clear();
        _midDevElapsed.Clear();

        foreach (var entry in midDevData.Split(','))
        {
            var parts = entry.Split(':');
            if (parts.Length != 3) continue;
            string empId = parts[0];
            if (!int.TryParse(parts[1], out int seed)) continue;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out float elapsed)) continue;

            var employee = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == empId);
            if (employee == null) continue;

            float ratio = (developmentDuration - elapsed) / developmentDuration;
            int tickCount = Mathf.Max(1, Mathf.RoundToInt(12 * ratio));

            _midDevSeeds[empId] = seed;
            _midDevElapsed[empId] = elapsed;
            BuildMidDevTicks(empId, seed, elapsed, tickCount);
        }
    }

    void ReschedulePendingTicks(string id)
    {
        var times = _tickTimesMap[id];
        int index = _tickIndexMap[id];
        const float gap = 1.5f;

        float minTime = _elapsed;
        for (int i = index; i < times.Count; i++)
        {
            minTime += gap;
            if (times[i] < minTime)
                times[i] = minTime;
            else
                minTime = times[i]; // 이미 충분히 먼 틱은 그 시간을 기준으로
        }
    }

    float CalcConstantDev(int skill)
    {
        double val = System.Math.Pow(
            System.Math.Log(1 + skill) / System.Math.Log(401), 8.8) * 100.0;
        return (float)val;
    }

    float CalcBug(int perfection)
    {
        double val = 76.5 / System.Math.Pow(1 + perfection, 0.76);
        return (float)val;
    }

    float CalcCreativityScore(int main, int sub)
    {
        if (main <= 0) return 0f;
        float ratio = (float)sub / main;
        if (ratio < 0.5f) return 0f;
        return Mathf.Min(ratio * 10f, 10f);
    }

    void AccumulateByType(EmployeeData employee, int tickType)
    {
        float satisfactionMultiplier = GetSatisfactionMultiplier(employee);

        // 만족도 배율을 스킬 값 자체에 적용 (주스탯·부스탯 모두)
        int skill = (int)(employee.role switch
        {
            EmployeeRole.Planner    => employee.planningSkill,
            EmployeeRole.Programmer => employee.developSkill,
            EmployeeRole.Artist     => employee.artSkill,
            _ => 0
        } * satisfactionMultiplier);

        int effectivePerfection = (int)(employee.perfectionSkill * satisfactionMultiplier);

        float planning = 0f, develop = 0f, art = 0f, bug = 0f, creativity = 0f;

        switch (tickType)
        {
            case 0: // 잭팟
                int jackpotVal = UnityEngine.Random.Range(1, 4)
                    + (int)(skill / 50)
                    + (int)System.Math.Pow(skill / 300.0, 2);
                float jackpot = Mathf.Max(1, jackpotVal);
                switch (employee.role)
                {
                    case EmployeeRole.Planner:
                        planning = jackpot;
                        OfficeManager.Instance?.ShowStatPopup(employee.id, $"[잭팟] +기획 {planning:F0}", new Color(1f, 0.85f, 0f));
                        break;
                    case EmployeeRole.Programmer:
                        develop = jackpot;
                        OfficeManager.Instance?.ShowStatPopup(employee.id, $"[잭팟] +개발 {develop:F0}", new Color(1f, 0.85f, 0f));
                        break;
                    case EmployeeRole.Artist:
                        art = jackpot;
                        OfficeManager.Instance?.ShowStatPopup(employee.id, $"[잭팟] +아트 {art:F0}", new Color(1f, 0.85f, 0f));
                        break;
                }
                break;

            case 1: // 성공
                int successVal = UnityEngine.Random.Range(0, 2)
                    + (int)(skill / 100)
                    + (int)System.Math.Pow(skill / 400.0, 2);
                float success = Mathf.Max(0, successVal);
                switch (employee.role)
                {
                    case EmployeeRole.Planner:
                        planning = success;
                        if (planning > 0f) OfficeManager.Instance?.ShowStatPopup(employee.id, $"[성공] +기획 {planning:F0}", new Color(0.4f, 0.6f, 1f));
                        break;
                    case EmployeeRole.Programmer:
                        develop = success;
                        if (develop > 0f) OfficeManager.Instance?.ShowStatPopup(employee.id, $"[성공] +개발 {develop:F0}", new Color(0.4f, 1f, 0.5f));
                        break;
                    case EmployeeRole.Artist:
                        art = success;
                        if (art > 0f) OfficeManager.Instance?.ShowStatPopup(employee.id, $"[성공] +아트 {art:F0}", new Color(1f, 0.7f, 0.3f));
                        break;
                }
                break;

            case 2: // 창의성
                creativity = 10f;
                OfficeManager.Instance?.ShowStatPopup(employee.id, $"[창의성] +창의 {creativity:F1}", new Color(0.5f, 1f, 0.9f));
                break;

            case 3: // 버그
                int perfReduction = (int)(effectivePerfection / 100);
                int bugRaw = ProjectSetupUI.SelectedScale switch
                {
                    ProjectScale.Small  => UnityEngine.Random.Range(3, 7)  - perfReduction,
                    ProjectScale.Medium => UnityEngine.Random.Range(6, 10) - perfReduction,
                    ProjectScale.Large  => (IsOvertimeMode || employee.isOvertimeWorker)
                        ? UnityEngine.Random.Range(10, 21) - perfReduction
                        : UnityEngine.Random.Range(10, 16) - perfReduction,
                    _ => 1
                };
                bug = Mathf.Max(1, bugRaw);
                OfficeManager.Instance?.ShowStatPopup(employee.id, $"[버그] +버그 {bug:F0}", new Color(1f, 0.3f, 0.3f));
                break;

            case 4: // 꽝
                OfficeManager.Instance?.ShowStatPopup(employee.id, "[꽝]", new Color(0.6f, 0.6f, 0.6f));
                break;
        }

        DevelopmentPanelUI.Instance.AddValues(planning, develop, art, bug, creativity);
        UpdateInvestmentProgress();
    }
    public void ResetProject()
    {
        IsStarted = false;
        CurrentStage = ProjectStage.None;
        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;
        _pendingLeaderScore25 = false;
        _pendingLeaderScore75 = false;
        _pendingDevelopmentComplete = false;
        _leaderDevelopBonusTotal  = 0f;
        _leaderPlanningBonusTotal = 0f;
        _leaderArtBonusTotal      = 0f;
        BugPenalty = 0f;
        BugEventBonus = 0f;
        _patrolStarted = false;
        _progressVisualOffset = 0f;
        _progressOffsetElapsedAtEvent = 0f;
        _progressOffsetExtension = 0f;
        OfficeManager.Instance?.SetCharacterSpeedMultiplier(1f);
        OfficeManager.Instance?.StopDevelopmentPatrol();

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;

        _tickTimesMap.Clear();
        _tickIndexMap.Clear();
        _tickOrderMap.Clear();
        _midDevSeeds.Clear();
        _midDevElapsed.Clear();

        if (_bugFixCoroutine != null)
        {
            StopCoroutine(_bugFixCoroutine);
            _bugFixCoroutine = null;
        }

        DevelopmentPanelUI.Instance.ResetValues();
        DevelopmentTimerUI.Instance.ResetTimer();
        RandomEventManager.Instance.Reset();

        ProjectSetupUI.SelectedScale = default;
        ProjectSetupUI.SelectedGenre = default;
        ProjectSetupUI.SelectedPlatform = default;

        IsVoluntaryOvertimeActive = false;
        IsOvertimeMode            = false;
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
            emp.isOvertimeWorker = false;
        GameTimeManager.Instance.ForceStartTime();
        Debug.Log("프로젝트 초기화 완료");
    }
    public void ExtendDevelopmentDuration(float extensionSeconds)
    {
        float oldRemaining = developmentDuration - _elapsed;
        float actualProgressBefore = developmentDuration > 0f ? _elapsed / developmentDuration : 0f;

        developmentDuration += extensionSeconds;
        float newRemaining = developmentDuration - _elapsed;

        if (oldRemaining <= 0f) return;
        float scale = newRemaining / oldRemaining;

        // 아직 발동 안 된 틱들을 새 remaining 기준으로 비례 재분배
        foreach (var empId in new List<string>(_tickIndexMap.Keys))
        {
            if (!_tickTimesMap.ContainsKey(empId)) continue;
            var times = _tickTimesMap[empId];
            int startIdx = _tickIndexMap[empId];
            for (int i = startIdx; i < times.Count; i++)
                times[i] = _elapsed + (times[i] - _elapsed) * scale;
        }

        // 진행도 표시 보정: 연장 직후에도 시각적으로 이전 progress 유지, 연장분만큼 천천히 수렴
        float actualProgressAfter = _elapsed / developmentDuration;
        _progressVisualOffset = actualProgressBefore - actualProgressAfter;
        _progressOffsetElapsedAtEvent = _elapsed;
        _progressOffsetExtension = extensionSeconds;

        // 캐릭터 속도 50% 감소 → extensionSeconds 후 복귀
        OfficeManager.Instance?.SetCharacterSpeedMultiplier(0.5f);
        StartCoroutine(RestoreCharacterSpeedAfter(extensionSeconds));
    }

    IEnumerator RestoreCharacterSpeedAfter(float extensionSeconds)
    {
        float endElapsed = _elapsed + extensionSeconds;
        while (_elapsed < endElapsed)
        {
            if (!_isRunning) { yield return null; continue; }
            yield return null;
        }
        OfficeManager.Instance?.SetCharacterSpeedMultiplier(1f);
    }

    public void PauseForEvent()
    {
        _isRunning = false;
        GameTimeManager.Instance.StopTime();
    }
    public void ResumeFromEvent()
    {
        RandomEventManager.Instance.ClearEventInProgress();

        // 이벤트 종료 시점에 미뤄진 팀장 점수가 있으면 시간을 재개하지 않고 팀장 선택 UI 표시
        if (_pendingDevelopmentComplete)
        {
            _pendingDevelopmentComplete = false;
            // PauseForEvent()의 StopTime 카운트를 해소한 뒤 OnDevelopmentComplete 내부에서 다시 StopTime
            GameTimeManager.Instance.ForceStartTime();
            OnDevelopmentComplete();
            return;
        }

        if (_pendingLeaderScore75)
        {
            _pendingLeaderScore75 = false;
            // PauseForEvent()가 이미 시간을 멈췄으므로 그대로 팀장 선택 UI 표시
            LeaderSelectUI.Instance.Open(LeaderType.Artist, null);
            return;
        }

        if (_pendingLeaderScore25)
        {
            _pendingLeaderScore25 = false;
            // PauseForEvent()가 이미 시간을 멈췄으므로 그대로 팀장 선택 UI 표시
            LeaderSelectUI.Instance.Open(LeaderType.Programmer, null);
            return;
        }

        _isRunning = true;
        GameTimeManager.Instance.ForceStartTime();
    }
    /*
    float GetElapsedMultiplier()
    {
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended;
        return 1f + diff * 0.1f;
    }
    */

    float GetSatisfactionMultiplier(EmployeeData employee) => employee.GetSatisfactionMultiplier();
    void UpdateInvestmentProgress()
    {
        if (!RandomEventManager.Instance.InvestmentAccepted) return;

        float current = RandomEventManager.Instance.InvestmentStat switch
        {
            "planning" => DevelopmentPanelUI.Instance.GetPlanning(),
            "develop" => DevelopmentPanelUI.Instance.GetDevelop(),
            "art" => DevelopmentPanelUI.Instance.GetArt(),
            "creativity" => DevelopmentPanelUI.Instance.GetCreativity(),
            _ => 0f
        };

        InvestmentProgressUI.Instance?.UpdateProgress(
            current,
            RandomEventManager.Instance.InvestmentStatName,
            RandomEventManager.Instance.investmentThreshold
        );
    }
}