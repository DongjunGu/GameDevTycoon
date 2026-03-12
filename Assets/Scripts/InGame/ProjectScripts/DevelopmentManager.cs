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
    public float leaderTickDelay = 0.5f; // 인스펙터에서 수정 가능
    public bool IsStarted { get; private set; } = false;
    private float _elapsed;
    private bool _isRunning;

    private bool _triggered0;
    private bool _triggered25;
    private bool _triggered75;

    // 직원별 다음 tick 시간
    private Dictionary<string, float> _nextTickMap = new();
    private Dictionary<string, int> _tickCountMap = new();
    private Dictionary<string, int> _tickIndexMap = new();
    private Dictionary<string, int[]> _tickOrderMap = new(); // 순서 배열
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

        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;

        // 직원마다 interval 초기화
        InitTickMap();

        DevelopmentPanelUI.Instance.ResetValues();

        LeaderSelectUI.Instance.Open(LeaderType.Planner, () =>
        {
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

            // 가중치 6:2:2 → 0=능력치, 1=창의성, 2=버그
            var order = BuildTickOrder(tickCount, 0.6f, 0.2f, 0.2f);
            _tickOrderMap[employee.id] = order;

            Debug.Log($"[{employee.employeeName}] tickCount: {tickCount} / interval: {interval:F1}s / order: [{string.Join(",", order)}]");
        }
    }
    int[] BuildTickOrder(int total, float ratioA, float ratioB, float ratioC)
    {
        int countA = Mathf.RoundToInt(total * ratioA); // 능력치
        int countB = Mathf.RoundToInt(total * ratioB); // 창의성
        int countC = total - countA - countB;           // 버그

        var list = new System.Collections.Generic.List<int>();
        for (int i = 0; i < countA; i++) list.Add(0);
        for (int i = 0; i < countB; i++) list.Add(1);
        for (int i = 0; i < countC; i++) list.Add(2);

        // 셔플
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[rand]) = (list[rand], list[i]);
        }

        return list.ToArray();
    }
    IEnumerator DevelopmentCoroutine()
    {
        _isRunning = true;

        while (_elapsed < developmentDuration)
        {
            _elapsed += Time.deltaTime;
            float progress = _elapsed / developmentDuration;

            // 상시개발
            foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            {
                if (!_nextTickMap.ContainsKey(employee.id)) continue;

                if (_elapsed >= _nextTickMap[employee.id])
                {
                    int index = _tickIndexMap[employee.id];
                    int[] order = _tickOrderMap[employee.id];

                    if (index < order.Length)
                    {
                        int tickType = order[index];
                        AccumulateByType(employee, tickType);
                        _tickIndexMap[employee.id]++;
                    }

                    // 고정 interval 사용
                    float interval = developmentDuration / _tickCountMap[employee.id];
                    _nextTickMap[employee.id] += interval;
                }
            }

            if (_elapsed >= _nextGenreTick)
            {
                // 랜덤으로 장르 선택 (중복 허용)
                System.Array genres = System.Enum.GetValues(typeof(ProjectGenre));
                _currentGenreIndex = UnityEngine.Random.Range(0, genres.Length);
                _nextGenreTick += _genreInterval;

                Debug.Log($"인기 장르 변경: {GetCurrentPopularGenre()}");
                DevelopmentPanelUI.Instance.UpdateMarketFit(GetCurrentPopularGenre());
            }
            // 25% 팀장 선택
            if (!_triggered25 && progress >= 0.25f)
            {
                _triggered25 = true;
                _isRunning = false;
                LeaderSelectUI.Instance.Open(LeaderType.Programmer, () =>
                {
                    _isRunning = true;
                    StartCoroutine(DevelopmentCoroutine());
                });
                yield break;
            }

            // 75% 팀장 선택
            if (!_triggered75 && progress >= 0.75f)
            {
                _triggered75 = true;
                _isRunning = false;
                LeaderSelectUI.Instance.Open(LeaderType.Artist, () =>
                {
                    _isRunning = true;
                    StartCoroutine(DevelopmentCoroutine());
                });
                yield break;
            }

            yield return null;
        }

        OnDevelopmentComplete();
    }

    float CalcConstantDev(int skill)
    {
        double val = System.Math.Pow(
            System.Math.Log(1 + skill) / System.Math.Log(401),
            8.8
        ) * 100.0;

        return (float)val;
    }

    void OnDevelopmentComplete()
    {
        _isRunning = false;

        AlertUI.Instance.Show(
            "개발 완료!\n버그 제거 작업을 시작합니다.",
            () => _bugFixCoroutine = StartCoroutine(BugFixCoroutine())
        );
    }
    void ShowResult()
    {
        DevelopmentResultUI.Instance.Show(
            DevelopmentPanelUI.Instance.GetPlanning(),
            DevelopmentPanelUI.Instance.GetDevelop(),
            DevelopmentPanelUI.Instance.GetArt(),
            DevelopmentPanelUI.Instance.GetBug(),
            DevelopmentPanelUI.Instance.GetCreativity()
        );
    }
    IEnumerator BugFixCoroutine()
    {
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
        AlertUI.Instance.Show("버그 작업이 끝났습니다.", () => ShowResult());
    }

    // 출시 버튼에서 호출
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

        int n = employee.grade switch
        {
            EmployeeGrade.F => UnityEngine.Random.Range(1, 3),
            EmployeeGrade.D => UnityEngine.Random.Range(2, 4),
            EmployeeGrade.C => UnityEngine.Random.Range(2, 5),
            EmployeeGrade.B => UnityEngine.Random.Range(3, 5),
            EmployeeGrade.A => UnityEngine.Random.Range(3, 6),
            EmployeeGrade.S => UnityEngine.Random.Range(4, 6),
            _ => 1
        };

        float r = CalcConstantDev(skill);

        Debug.Log($"팀장: {employee.employeeName} / N: {n} / R: {r:F1}");

        LeaderScoreUI.Instance.Show(employee, type, n, r, leaderTickDelay, () =>
        {
            _isRunning = true;
            StartCoroutine(DevelopmentCoroutine());
        });
    }

    public float GetProgress() => developmentDuration > 0
        ? _elapsed / developmentDuration : 0f;

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
            case 0: // 능력치
                float planning = 0f, develop = 0f, art = 0f;
                switch (employee.role)
                {
                    case EmployeeRole.Planner: planning = CalcConstantDev(employee.planningSkill); break;
                    case EmployeeRole.Programmer: develop = CalcConstantDev(employee.developSkill); break;
                    case EmployeeRole.Artist: art = CalcConstantDev(employee.artSkill); break;
                }
                DevelopmentPanelUI.Instance.AddValues(planning, develop, art, 0f, 0f);
                break;

            case 1: // 창의성
                float creativity = 0f;
                switch (employee.role)
                {
                    case EmployeeRole.Planner:
                        creativity = CalcCreativityScore(employee.planningSkill, employee.developSkill);
                        break;
                    case EmployeeRole.Programmer:
                        creativity = CalcCreativityScore(employee.developSkill, employee.artSkill);
                        break;
                    case EmployeeRole.Artist:
                        creativity = CalcCreativityScore(employee.artSkill, employee.planningSkill);
                        break;
                }
                DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, 0f, creativity);
                break;

            case 2: // 버그
                float bug = CalcBug(employee.perfectionSkill);
                DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, bug, 0f);
                break;
        }
    }
    void InitGenrePool()
    {
        _genrePool.Clear();

        // 선택한 장르 포함 전체 장르로 초기화
        foreach (ProjectGenre genre in System.Enum.GetValues(typeof(ProjectGenre)))
            _genrePool.Add(genre);

        // 첫 번째는 선택 장르로 고정, 나머지 셔플
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

    // 테크트리에서 호출
    public void RemoveGenreFromPool(ProjectGenre genre)
    {
        if (genre == ProjectSetupUI.SelectedGenre) return; // 선택 장르는 제거 불가
        _genrePool.Remove(genre);

        // interval 재계산
        _genreInterval = developmentDuration / _genrePool.Count;
        Debug.Log($"장르 풀 제거: {genre} / 남은 풀: {string.Join(", ", _genrePool)}");
    }
}