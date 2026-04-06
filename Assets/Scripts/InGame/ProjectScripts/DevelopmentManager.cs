using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LeaderType { Planner, Programmer, Artist }

public class DevelopmentManager : MonoBehaviour
{
    public static DevelopmentManager Instance { get; private set; }

    [Header("Settings")]
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
    private Coroutine _bugFixCoroutine;

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
        _tickTimesMap.Clear();
        _tickIndexMap.Clear();
        _tickOrderMap.Clear();

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            const int tickCount = 12;
            float segmentSize = developmentDuration / tickCount;

            var times = new List<float>();
            for (int i = 0; i < tickCount; i++)
                times.Add(i * segmentSize + UnityEngine.Random.Range(0f, segmentSize));

            _tickTimesMap[employee.id] = times;
            _tickIndexMap[employee.id] = 0;
            _tickOrderMap[employee.id] = BuildTickOrder(tickCount, IsOvertimeMode);
        }
    }

    int[] BuildTickOrder(int total, bool overtime)
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
            int r = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
        return list.ToArray();
    }

    IEnumerator DevelopmentCoroutine()
    {
        CurrentStage = ProjectStage.Developing; // ← 스테이지 설정
        _isRunning = true;
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
            RandomEventManager.Instance.CheckNetworkIssueExpiry(progress);

            foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            {
                if (!_tickTimesMap.ContainsKey(employee.id)) continue;

                int index = _tickIndexMap[employee.id];
                var times = _tickTimesMap[employee.id];
                int[] order = _tickOrderMap[employee.id];

                if (index < times.Count && _elapsed >= times[index])
                {
                    // patrol 중인 캐릭터는 틱 스킵
                    if (!OfficeManager.Instance.IsPatrolling(employee.id))
                        AccumulateByType(employee, order[index]);
                    _tickIndexMap[employee.id]++;
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

    void OnDevelopmentComplete()
    {
        _isRunning = false;
        OfficeManager.Instance?.StopDevelopmentPatrol();
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show(
            "개발 완료!\n디버깅 작업을 시작합니다.",
            () =>
            {
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
            }
        );
    }

    IEnumerator BugFixCoroutine()
    {
        CurrentStage = ProjectStage.BugFixing;
        GameTimeManager.Instance.StartTime();

        float initialBug = DevelopmentPanelUI.Instance.GetBug();
        if (initialBug <= 0f)
        {
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
                float remainingBug = DevelopmentPanelUI.Instance.GetBug();

                var employees = EmployeeManager.Instance.ownedEmployees;
                float perfSum = 0f;
                foreach (var e in employees) perfSum += e.perfectionSkill;
                float avgPerfection = employees.Count > 0 ? perfSum / employees.Count : 0f;

                float fixAmount = Mathf.Max(1f,
                    Mathf.Ceil((avgPerfection / 80f) * Mathf.Sqrt(remainingBug / initialBug)));

                DevelopmentPanelUI.Instance.SetBug(Mathf.Max(0f, remainingBug - fixAmount));

                if (employees.Count > 0)
                {
                    var target = employees[UnityEngine.Random.Range(0, employees.Count)];
                    OfficeManager.Instance?.ShowStatPopup(target.id, $"[버그수정] -{fixAmount:F0}", new Color(0.4f, 1f, 0.6f));
                }
            }

            yield return null;
        }

        DevelopmentPanelUI.Instance.SetBug(0f);
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show("버그 작업이 끝났습니다.", () => ShowResult());
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
            ? $"버그 작업을 중단합니다.\n[버그 페널티] 최종점수 {BugPenalty * 100f:F0}% 감점\n출시 시작"
            : "버그 작업을 중단합니다.\n출시 시작";

        AlertUI.Instance.Show(msg, () =>
        {
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
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

            // 25% 개발팀장이면 네트워크 이슈 체크
            if (type == LeaderType.Programmer && _triggered25)
            {
                float currentProgress = _elapsed / developmentDuration;
                RandomEventManager.Instance.TryTriggerNetworkIssue(currentProgress, () => //네트워크이슈 이벤트 발생
                {
                    ProjectSaveManager.Instance.SaveProject();
                    GameTimeManager.Instance.SaveGameTime();
                    GameTimeManager.Instance.ForceStartTime();
                    StartCoroutine(DevelopmentCoroutine());
                });
            }
            else
            {
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                GameTimeManager.Instance.ForceStartTime();
                StartCoroutine(DevelopmentCoroutine());
            }
        });
    }

    public void RestoreState(
        float elapsed, bool triggered25, bool triggered75,
        string plannerLeaderId, string programmerLeaderId, string artistLeaderId,
        float accumPlanning, float accumDevelop, float accumArt,
        float accumBug, float accumCreativity,
        ProjectStage stage)
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
                InitTickMap();

                foreach (var key in new List<string>(_tickTimesMap.Keys))
                {
                    var times = _tickTimesMap[key];
                    int skipped = 0;
                    while (_tickIndexMap[key] < times.Count && times[_tickIndexMap[key]] <= _elapsed)
                    {
                        _tickIndexMap[key]++;
                        skipped++;
                    }
                    float nextTick = _tickIndexMap[key] < times.Count ? times[_tickIndexMap[key]] : -1f;
                    Debug.Log($"[보정] {key} skipped: {skipped} / nextTick: {nextTick:F1}");
                }

                _isRunning = true; // ← 추가
                GameTimeManager.Instance.ForceStartTime(); // ← 추가
                StartCoroutine(DevelopmentCoroutine());
                break;

            case ProjectStage.BugFixing:
                _isRunning = true; // ← 추가
                GameTimeManager.Instance.ForceStartTime(); // ← 추가
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
        float networkMultiplier = RandomEventManager.Instance.NetworkSpeedMultiplier;
        float totalMultiplier = satisfactionMultiplier * networkMultiplier;

        if (networkMultiplier < 1f)
            Debug.Log($"[네트워크이슈] {employee.employeeName} 틱 적용 / 만족도배율: {satisfactionMultiplier:F2} / 네트워크배율: {networkMultiplier:F2} / 최종: {totalMultiplier:F2}");

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
        if (diff == 0) return 1f;
        float factor = diff > 0 ? 1.1f : 0.9f;
        return Mathf.Pow(factor, Mathf.Abs(diff));
    }

    float GetSatisfactionMultiplier(EmployeeData employee)
    {
        var state = employee.GetSatisfactionState();
        return state switch
        {
            SatisfactionState.VeryHappy => 1.2f,
            SatisfactionState.Unhappy => 0.8f,
            SatisfactionState.VeryUnhappy => 0.8f,
            _ => 1.0f
        };
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