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

    public bool IsStarted { get; private set; } = false;
    public bool IsTriggered25 => _triggered25;
    public bool IsTriggered75 => _triggered75;
    public int CurrentGenreIndex => _currentGenreIndex;
    public float NextGenreTick => _nextGenreTick;
    public ProjectStage CurrentStage { get; private set; } = ProjectStage.None;

    private float _elapsed;
    private bool _isRunning;
    private bool _triggered25;
    private bool _triggered75;

    private Dictionary<string, float> _nextTickMap = new();
    private Dictionary<string, int> _tickCountMap = new();
    private Dictionary<string, int> _tickIndexMap = new();
    private Dictionary<string, int[]> _tickOrderMap = new();
    private Coroutine _bugFixCoroutine;
    private int _currentGenreIndex = 0;
    private float _genreInterval;
    private float _nextGenreTick;
    private List<ProjectGenre> _genrePool = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDevelopment()
    {
        IsStarted = true;
        InitGenrePool();
        int genreCount = _genrePool.Count;
        _genreInterval = developmentDuration / genreCount;
        _nextGenreTick = _genreInterval;
        _currentGenreIndex = 0;
        DevelopmentPanelUI.Instance.UpdateMarketFit(GetCurrentPopularGenre());
        DevelopmentPanelUI.Instance.UpdateUI();
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
        LeaderSelectUI.Instance.Open(LeaderType.Planner, () =>
        {
            GameTimeManager.Instance.StartTime();
            StartCoroutine(DevelopmentCoroutine());
        });
    }

    void InitTickMap()
    {
        _nextTickMap.Clear();
        _tickCountMap.Clear();
        _tickIndexMap.Clear();
        _tickOrderMap.Clear();

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            int tickCount = UnityEngine.Random.Range(8, 11);
            float interval = developmentDuration / tickCount;
            _nextTickMap[employee.id] = interval;
            _tickCountMap[employee.id] = tickCount;
            _tickIndexMap[employee.id] = 0;

            var order = BuildTickOrder(tickCount, 0.6f, 0.2f, 0.2f);
            _tickOrderMap[employee.id] = order;
        }
    }

    int[] BuildTickOrder(int total, float ratioA, float ratioB, float ratioC)
    {
        int countA = Mathf.RoundToInt(total * ratioA);
        int countB = Mathf.RoundToInt(total * ratioB);
        int countC = total - countA - countB;

        var list = new List<int>();
        for (int i = 0; i < countA; i++) list.Add(0);
        for (int i = 0; i < countB; i++) list.Add(1);
        for (int i = 0; i < countC; i++) list.Add(2);

        for (int i = list.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
        return list.ToArray();
    }

    IEnumerator DevelopmentCoroutine()
    {
        CurrentStage = ProjectStage.Developing; // ← 스테이지 설정
        _isRunning = true;

        while (_elapsed < developmentDuration)
        {
            if (!_isRunning)
            {
                yield return null;
                continue;
            }


            _elapsed += Time.deltaTime;
            float progress = _elapsed / developmentDuration;

            RandomEventManager.Instance.CheckTrigger(progress);

            foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            {
                if (!_nextTickMap.ContainsKey(employee.id)) continue;

                if (_elapsed >= _nextTickMap[employee.id])
                {
                    int index = _tickIndexMap[employee.id];
                    int[] order = _tickOrderMap[employee.id];

                    if (index < order.Length)
                    {
                        AccumulateByType(employee, order[index]);
                        _tickIndexMap[employee.id]++;
                    }

                    float interval = developmentDuration / _tickCountMap[employee.id];
                    _nextTickMap[employee.id] += interval;
                }
            }

            if (_elapsed >= _nextGenreTick)
            {
                System.Array genres = System.Enum.GetValues(typeof(ProjectGenre));
                _currentGenreIndex = UnityEngine.Random.Range(0, genres.Length);
                _nextGenreTick += _genreInterval;
                DevelopmentPanelUI.Instance.UpdateMarketFit(GetCurrentPopularGenre());
            }

            if (!_triggered25 && progress >= 0.25f)
            {
                _triggered25 = true;
                _isRunning = false;
                GameTimeManager.Instance.StopTime();
                LeaderSelectUI.Instance.Open(LeaderType.Programmer, () =>
                {
                    GameTimeManager.Instance.StartTime();
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
                    GameTimeManager.Instance.StartTime();
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
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show(
            "개발 완료!\n버그 제거 작업을 시작합니다.",
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
        CurrentStage = ProjectStage.BugFixing; // ← 스테이지 설정
        GameTimeManager.Instance.StartTime();
        float bugFixDuration = developmentDuration * bugDurationRate;
        float elapsed = 0f;
        float initialBug = DevelopmentPanelUI.Instance.GetBug();

        while (elapsed < bugFixDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / bugFixDuration;
            float ratio = 1f - Mathf.Pow(1f - progress, 2f);
            float currentBug = initialBug * (1f - ratio);
            DevelopmentPanelUI.Instance.SetBug(currentBug);
            yield return null;
        }

        DevelopmentPanelUI.Instance.SetBug(0f);
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show("버그 작업이 끝났습니다.", () => ShowResult());
    }

    void ShowResult()
    {
        CurrentStage = ProjectStage.Complete; // ← 완료 스테이지
        GameTimeManager.Instance.StopTime();
        DevelopmentResultUI.Instance.Show(
            DevelopmentPanelUI.Instance.GetPlanning(),
            DevelopmentPanelUI.Instance.GetDevelop(),
            DevelopmentPanelUI.Instance.GetArt(),
            DevelopmentPanelUI.Instance.GetBug(),
            DevelopmentPanelUI.Instance.GetCreativity()
        );
    }

    public void OnClickRelease()
    {
        if (_bugFixCoroutine != null)
        {
            StopCoroutine(_bugFixCoroutine);
            _bugFixCoroutine = null;
        }
        AlertUI.Instance.Show("버그 작업을 중단합니다.\n출시 시작", () => ShowResult());
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

        int n = employee.potential switch
        {
            EmployeePotential.F => UnityEngine.Random.Range(1, 3),
            EmployeePotential.D => UnityEngine.Random.Range(2, 4),
            EmployeePotential.C => UnityEngine.Random.Range(2, 5),
            EmployeePotential.B => UnityEngine.Random.Range(3, 5),
            EmployeePotential.A => UnityEngine.Random.Range(3, 6),
            _ => 1
        };

        float r = CalcConstantDev(skill);
        GameTimeManager.Instance.StopTime();
        LeaderScoreUI.Instance.Show(employee, type, n, r, leaderTickDelay, () =>
        {
            _isRunning = true;
            ProjectSaveManager.Instance.SaveProject(); // ← 추가
            GameTimeManager.Instance.SaveGameTime();
            GameTimeManager.Instance.StartTime();
            StartCoroutine(DevelopmentCoroutine());
        });
    }

    public void RestoreState(
        float elapsed, bool triggered25, bool triggered75,
        string plannerLeaderId, string programmerLeaderId, string artistLeaderId,
        float accumPlanning, float accumDevelop, float accumArt,
        float accumBug, float accumCreativity,
        int currentGenreIndex, float nextGenreTick,
        ProjectStage stage)
    {
        _elapsed = elapsed;
        _triggered25 = triggered25;
        _triggered75 = triggered75;
        _currentGenreIndex = currentGenreIndex;
        _nextGenreTick = nextGenreTick;
        CurrentStage = stage;
        IsStarted = true;
        RandomEventManager.Instance.InitEvents();
        if (elapsed / developmentDuration >= 0.5f)
            RandomEventManager.Instance.SetTriggered50(true);
        InitGenrePool();
        _genreInterval = developmentDuration / _genrePool.Count;
        DevelopmentPanelUI.Instance.UpdateMarketFit(GetCurrentPopularGenre()); // ← 추가

        plannerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == plannerLeaderId);
        programmerLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == programmerLeaderId);
        artistLeader = EmployeeManager.Instance.ownedEmployees.Find(e => e.id == artistLeaderId);

        DevelopmentPanelUI.Instance.SetValues(accumPlanning, accumDevelop, accumArt, accumBug, accumCreativity);

        switch (stage)
        {
            case ProjectStage.Developing:
                InitTickMap();

                // 디버그: 보정 전 확인
                foreach (var key in new List<string>(_nextTickMap.Keys))
                {
                    float interval = developmentDuration / _tickCountMap[key];
                    int skipped = 0;

                    while (_nextTickMap[key] <= _elapsed)
                    {
                        _nextTickMap[key] += interval;
                        _tickIndexMap[key]++;
                        skipped++;
                        // ← AccumulateByType 절대 호출 안함
                    }
                    Debug.Log($"[보정] {key} skipped: {skipped} / nextTick: {_nextTickMap[key]:F1}");
                }

                StartCoroutine(DevelopmentCoroutine());
                break;
            case ProjectStage.BugFixing:
                _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
                break;
        }

        Debug.Log($"프로젝트 복원 완료: {stage} / elapsed: {elapsed:F1}");
    }

    public float GetProgress() => developmentDuration > 0 ? _elapsed / developmentDuration : 0f;

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
        switch (tickType)
        {
            case 0:
                float planning = 0f, develop = 0f, art = 0f;
                switch (employee.role)
                {
                    case EmployeeRole.Planner: planning = CalcConstantDev(employee.planningSkill); break;
                    case EmployeeRole.Programmer: develop = CalcConstantDev(employee.developSkill); break;
                    case EmployeeRole.Artist: art = CalcConstantDev(employee.artSkill); break;
                }
                DevelopmentPanelUI.Instance.AddValues(planning, develop, art, 0f, 0f);
                break;

            case 1:
                float creativity = 0f;
                switch (employee.role)
                {
                    case EmployeeRole.Planner:
                        creativity = CalcCreativityScore(employee.planningSkill, employee.developSkill); break;
                    case EmployeeRole.Programmer:
                        creativity = CalcCreativityScore(employee.developSkill, employee.artSkill); break;
                    case EmployeeRole.Artist:
                        creativity = CalcCreativityScore(employee.artSkill, employee.planningSkill); break;
                }
                DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, 0f, creativity);
                break;

            case 2:
                float bug = CalcBug(employee.perfectionSkill);
                DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, bug, 0f);
                break;
        }
    }

    void InitGenrePool()
    {
        _genrePool.Clear();
        foreach (ProjectGenre genre in System.Enum.GetValues(typeof(ProjectGenre)))
            _genrePool.Add(genre);

        _genrePool.Remove(ProjectSetupUI.SelectedGenre);
        ShuffleGenrePool();
        _genrePool.Insert(0, ProjectSetupUI.SelectedGenre);
    }

    void ShuffleGenrePool()
    {
        for (int i = _genrePool.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (_genrePool[i], _genrePool[rand]) = (_genrePool[rand], _genrePool[i]);
        }
    }

    public ProjectGenre GetCurrentPopularGenre()
    {
        System.Array genres = System.Enum.GetValues(typeof(ProjectGenre));
        return (ProjectGenre)genres.GetValue(_currentGenreIndex % genres.Length);
    }

    public void RemoveGenreFromPool(ProjectGenre genre)
    {
        if (genre == ProjectSetupUI.SelectedGenre) return;
        _genrePool.Remove(genre);
        _genreInterval = developmentDuration / _genrePool.Count;
    }
    public void ResetProject()
    {
        IsStarted = false;
        CurrentStage = ProjectStage.None;
        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;

        _nextTickMap.Clear();
        _tickCountMap.Clear();
        _tickIndexMap.Clear();
        _tickOrderMap.Clear();

        _currentGenreIndex = 0;
        _nextGenreTick = 0f;
        _genrePool.Clear();

        if (_bugFixCoroutine != null)
        {
            StopCoroutine(_bugFixCoroutine);
            _bugFixCoroutine = null;
        }

        DevelopmentPanelUI.Instance.ResetValues();
        DevelopmentPanelUI.Instance.ResetMarketFit();
        DevelopmentTimerUI.Instance.ResetTimer();
        RandomEventManager.Instance.Reset();

        ProjectSetupUI.SelectedScale = default;
        ProjectSetupUI.SelectedGenre = default;
        ProjectSetupUI.SelectedPlatform = default;

        GameTimeManager.Instance.StartTime();
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
        GameTimeManager.Instance.StartTime();
    }
}