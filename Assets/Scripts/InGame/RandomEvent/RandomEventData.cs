using System;

public enum RandomEventType
{
    Blackout,       // 정전         모든수치    -50%
    TeamDinner,     // 회식         모든수치    +50%
    DevBoost,       // 개발         개발수치    +50
    PlanBoost,      // 기획         기획수치    +50
    ArtBoost,       // 아트         아트수치    +50
    CreativityBoost,// 창의성       창의성수치  +30
    Investment,
    EmployeeScout,
    BetaTestIssue,
    NetworkIssue,
    
    // 출시 이벤트
    CompetitorRelease,
    PerfectTiming,
    AlgorithmChoice,
    EmployeeRun,
    EmployeeResignation,
    EmployeeFight,
    BadCompany,
}

[Serializable]
public class RandomEventData
{
    public RandomEventType type;
    public string title;
    public string description;
    public float  weight;        // 가중치 (클수록 뽑힐 확률 높음)
    public float    scoreBonus;   // ← 출시 이벤트용 점수 보정값
    public System.Action onApply;       // 실제 효과
}