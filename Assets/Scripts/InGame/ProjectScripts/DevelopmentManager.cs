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

    private float _elapsed;
    private bool _isRunning;

    private bool _triggered0;
    private bool _triggered25;
    private bool _triggered75;

    // 직원별 다음 tick 시간
    private Dictionary<string, float> _nextTickMap = new();
    private Dictionary<string, float> _nextBugTickMap = new();
    private Coroutine _bugFixCoroutine;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDevelopment()
    {
        _elapsed = 0f;
        _isRunning = false;
        _triggered0 = false;
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
        _nextBugTickMap.Clear();

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            // 상시개발 interval
            int tickCount = UnityEngine.Random.Range(8, 11);
            float interval = developmentDuration / tickCount;
            _nextTickMap[employee.id] = interval;

            // 버그 interval
            int bugTickCount = UnityEngine.Random.Range(2, 6); // 2~5
            float bugInterval = developmentDuration / bugTickCount;
            _nextBugTickMap[employee.id] = bugInterval;

            Debug.Log($"{employee.employeeName} / 상시개발 tickCount: {tickCount} / interval: {interval:F1}초 / 버그 tickCount: {bugTickCount} / bugInterval: {bugInterval:F1}초");
        }
    }

    IEnumerator DevelopmentCoroutine()
    {
        _isRunning = true;

        while (_elapsed < developmentDuration)
        {
            _elapsed += Time.deltaTime;
            float progress = _elapsed / developmentDuration;

            // 직원별 tick 체크
            foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            {
                // 상시개발
                if (_nextTickMap.ContainsKey(employee.id) &&
                    _elapsed >= _nextTickMap[employee.id])
                {
                    AccumulateDevelopment(employee);

                    int tickCount = UnityEngine.Random.Range(8, 11);
                    float interval = developmentDuration / tickCount;
                    _nextTickMap[employee.id] += interval;
                }

                // 버그
                if (_nextBugTickMap.ContainsKey(employee.id) &&
                    _elapsed >= _nextBugTickMap[employee.id])
                {
                    AccumulateBug(employee);

                    int bugTickCount = UnityEngine.Random.Range(2, 6);
                    float bugInterval = developmentDuration / bugTickCount;
                    _nextBugTickMap[employee.id] += bugInterval;
                }
            }

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
    AlertUI.Instance.Show("버그 작업이 끝났습니다.");
}

// 출시 버튼에서 호출
public void OnClickRelease()
{
    if (_bugFixCoroutine != null)
    {
        StopCoroutine(_bugFixCoroutine);
        _bugFixCoroutine = null;
    }

    AlertUI.Instance.Show("버그 작업을 중단했습니다\n출시 시작");
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
    IEnumerator ApplyLeaderScore(LeaderType type, EmployeeData employee)
    {
        int skill = type switch
        {
            LeaderType.Planner => employee.planningSkill,
            LeaderType.Programmer => employee.developSkill,
            LeaderType.Artist => employee.artSkill,
            _ => 0
        };

        // N: 등급에 따라 랜덤
        int n = employee.grade switch
        {
            EmployeeGrade.F => UnityEngine.Random.Range(1, 3), // 1~2
            EmployeeGrade.D => UnityEngine.Random.Range(2, 4), // 2~3
            EmployeeGrade.C => UnityEngine.Random.Range(2, 5), // 2~4
            EmployeeGrade.B => UnityEngine.Random.Range(3, 5), // 3~4
            EmployeeGrade.A => UnityEngine.Random.Range(3, 6), // 3~5
            EmployeeGrade.S => UnityEngine.Random.Range(4, 6), // 4~5
            _ => 1
        };

        float r = CalcConstantDev(skill);

        Debug.Log($"팀장: {employee.employeeName} / N: {n} / R: {r:F1}");

        for (int i = 0; i < n; i++)
        {
            yield return new WaitForSeconds(leaderTickDelay);

            switch (type)
            {
                case LeaderType.Planner:
                    DevelopmentPanelUI.Instance.AddValues(r, 0f, 0f);
                    break;
                case LeaderType.Programmer:
                    DevelopmentPanelUI.Instance.AddValues(0f, r, 0f);
                    break;
                case LeaderType.Artist:
                    DevelopmentPanelUI.Instance.AddValues(0f, 0f, r);
                    break;
            }
        }

        _isRunning = true;
        StartCoroutine(DevelopmentCoroutine());
    }
    void AccumulateDevelopment(EmployeeData employee)
    {
        float planning = 0f;
        float develop = 0f;
        float art = 0f;

        switch (employee.role)
        {
            case EmployeeRole.Planner:
                planning = CalcConstantDev(employee.planningSkill);
                break;
            case EmployeeRole.Programmer:
                develop = CalcConstantDev(employee.developSkill);
                break;
            case EmployeeRole.Artist:
                art = CalcConstantDev(employee.artSkill);
                break;
        }

        DevelopmentPanelUI.Instance.AddValues(planning, develop, art, 0f);
    }

    void AccumulateBug(EmployeeData employee)
    {
        float bug = CalcBug(employee.perfectionSkill);
        DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, bug);
    }


    float CalcBug(int perfection)
    {
        double val = 76.5 / System.Math.Pow(1 + perfection, 0.76);
        return (float)val;
    }
}