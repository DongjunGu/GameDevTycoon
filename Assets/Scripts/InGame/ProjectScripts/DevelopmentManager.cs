using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LeaderType { Planner, Programmer, Artist }

public class DevelopmentManager : MonoBehaviour
{
    public static DevelopmentManager Instance { get; private set; }

    [Header("Settings")]
    public bool IsOvertimeActive { get; private set; } = false;
    public void SetOvertime(bool active) => IsOvertimeActive = active;

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
    public ProjectStage CurrentStage { get; set; } = ProjectStage.None;

    private float _elapsed;
    private bool _isRunning;
    private bool _triggered25;
    private bool _triggered75;
    private bool _patrolStarted;

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
        developmentDuration = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f,
            ProjectScale.Medium => 100.8f,
            ProjectScale.Large  => 124.8f,
            _ => 80f
        };
        IsStarted = true;
        _patrolStarted = false;
        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;


        InitTickMap();
        DevelopmentPanelUI.Instance.ResetValues();
        RandomEventManager.Instance.InitEvents();
        GameTimeManager.Instance.StopTime();
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
            int[] order = BuildTickOrder(tickCount, IsOvertimeMode, rng);
            _tickOrderMap[employee.id] = order;

            var sb = new System.Text.StringBuilder();
            sb.Append($"[틱 시드={seed}] {employee.employeeName}({employee.id[..8]}) : ");
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


            _elapsed += Time.deltaTime * GetElapsedMultiplier();
            float progress = _elapsed / developmentDuration;

            RandomEventManager.Instance.CheckTrigger(progress);

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
                GameTimeManager.Instance.StopTime();
                LeaderSelectUI.Instance.Open(LeaderType.Programmer, () =>
                {
                    GameTimeManager.Instance.ForceStartTime();
                    _isRunning = true;
                    StartCoroutine(DevelopmentCoroutine());
                });
                yield break;
            }

            if (!_triggered75 && progress >= 0.75f)
            {
                _triggered75 = true;
                _isRunning = false;
                GameTimeManager.Instance.StopTime();
                LeaderSelectUI.Instance.Open(LeaderType.Artist, () =>
                {
                    _isRunning = true;
                    GameTimeManager.Instance.ForceStartTime();
                    StartCoroutine(DevelopmentCoroutine());
                });
                yield break;
            }

            yield return null;
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

            if (UnityEngine.Random.value < 0.5f)
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

        int skill = type switch
        {
            LeaderType.Planner => employee.planningSkill,
            LeaderType.Programmer => employee.developSkill,
            LeaderType.Artist => employee.artSkill,
            _ => 0
        };

        int n = CalcLeaderTickCount(skill);

        float total = CalcLeaderScore(skill, n);
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
        GameTimeManager.Instance.StopTime();
        LeaderScoreUI.Instance.Show(employee, type, scores, leaderTickDelay, () =>
        {
            _isRunning = true;
            CurrentStage = ProjectStage.Developing;
            GameTimeManager.Instance.ForceStartTime();
            Debug.Log("팀장점수완료 저장");

            MoneyManager.Instance.SaveMoney();
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            GameTimeManager.Instance.ForceStartTime();
            StartCoroutine(DevelopmentCoroutine());
        });
    }

    public void RestoreState(
        float elapsed, bool triggered25, bool triggered75,
        string plannerLeaderId, string programmerLeaderId, string artistLeaderId,
        float accumPlanning, float accumDevelop, float accumArt,
        float accumBug, float accumCreativity,
        ProjectStage stage,
        int tickSeed = 0, string tickIndices = "", string midDevData = "")
    {
        developmentDuration = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f,
            ProjectScale.Medium => 100.8f,
            ProjectScale.Large  => 124.8f,
            _ => 80f
        };
        _elapsed = elapsed;
        _triggered25 = triggered25;
        _triggered75 = triggered75;
        CurrentStage = stage;
        IsStarted = true;
        RandomEventManager.Instance.InitEvents();
        if (elapsed / developmentDuration >= 0.5f)
            RandomEventManager.Instance.SetTriggered50(true);

        plannerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == plannerLeaderId);
        programmerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == programmerLeaderId);
        artistLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == artistLeaderId);

        DevelopmentPanelUI.Instance.SetValues(accumPlanning, accumDevelop, accumArt, accumBug, accumCreativity);

        switch (stage)
        {
            case ProjectStage.Developing:
                _tickSeed = tickSeed != 0 ? tickSeed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                InitTickMapWithSeed(_tickSeed);
                RestoreMidDevData(midDevData);
                RestoreTickIndices(tickIndices);

                _isRunning = true;
                GameTimeManager.Instance.ForceStartTime();
                StartCoroutine(DevelopmentCoroutine());
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

    public float GetProgress() => developmentDuration > 0 ? _elapsed / developmentDuration : 0f;

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
        _tickOrderMap[empId] = BuildTickOrder(tickCount, IsOvertimeMode, rng);
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
        float totalMultiplier = satisfactionMultiplier;

        int skill = employee.role switch
        {
            EmployeeRole.Planner    => employee.planningSkill,
            EmployeeRole.Programmer => employee.developSkill,
            EmployeeRole.Artist     => employee.artSkill,
            _ => 0
        };

        float planning = 0f, develop = 0f, art = 0f, bug = 0f, creativity = 0f;

        switch (tickType)
        {
            case 0: // 잭팟
                int jackpotVal = UnityEngine.Random.Range(1, 4)
                    + (int)(skill / 50)
                    + (int)System.Math.Pow(skill / 300.0, 2);
                float jackpot = Mathf.Max(1, jackpotVal) * totalMultiplier;
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
                float success = Mathf.Max(0, successVal) * totalMultiplier;
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
                creativity = 10f * totalMultiplier;
                OfficeManager.Instance?.ShowStatPopup(employee.id, $"[창의성] +창의 {creativity:F1}", new Color(0.5f, 1f, 0.9f));
                break;

            case 3: // 버그
                int perfReduction = (int)(employee.perfectionSkill / 100);
                int bugRaw = ProjectSetupUI.SelectedScale switch
                {
                    ProjectScale.Small  => UnityEngine.Random.Range(3, 7)  - perfReduction,
                    ProjectScale.Medium => UnityEngine.Random.Range(6, 10) - perfReduction,
                    ProjectScale.Large  => IsOvertimeMode
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
        BugPenalty = 0f;
        BugEventBonus = 0f;
        _patrolStarted = false;
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

        IsOvertimeActive = false;
        GameTimeManager.Instance.ForceStartTime();
        Debug.Log("프로젝트 초기화 완료");
    }
    public void PauseForEvent()
    {
        _isRunning = false;
        GameTimeManager.Instance.StopTime();
    }
    public void ResumeFromEvent()
    {
        _isRunning = true;
        GameTimeManager.Instance.ForceStartTime();
    }
    float GetElapsedMultiplier()
    {
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended;
        return 1f + diff * 0.1f;
    }

    float GetSatisfactionMultiplier(EmployeeData employee)
    {
        int sat = employee.satisfaction;
        if (sat >= 80) return 1.1f;
        if (sat >= 60) return 1.0f;
        return 0.9f; // 40~60, ~40 모두 -10%
    }
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