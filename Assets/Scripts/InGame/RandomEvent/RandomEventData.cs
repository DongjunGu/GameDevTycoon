using System;

public enum RandomEventType
{
    // ── 기존 (내부 시스템용으로 유지) ─────────────────────────
    Blackout,
    TeamDinner,
    DevBoost,
    PlanBoost,
    ArtBoost,
    CreativityBoost,
    Investment,
    EmployeeScout,
    BetaTestIssue,

    // ── 조건 이벤트 ───────────────────────────────────────────
    EmployeeRun,
    EmployeeResignation,
    EmployeeFight,
    BadCompany,
    CompetitorRelease,
    PerfectTiming,
    AlgorithmChoice,

    // ── 개발 중 랜덤 이벤트 (신규) ────────────────────────────
    NetworkIssue,           // 네트워크 끊김          1~74%
    CompetitorGame,         // 대형 게임사의 경쟁작 출시   1~99%
    TangsuYukFight,         // 탕수육 부먹 찍먹 싸움      76~99%
    AntiMintchoc,           // 반민초파의 공격             76~99%
    AcWar,                  // 에어컨 전쟁                 76~99%
    AvoidingEmployee,       // 나를 피하는 직원            1~99%
    Cold,                   // 감기                        1~99%
    BadReview,              // 이유 없는 별점 1점           1~99%
    Birthday,               // 생일                        1~99%
    EarlyLeaveRequest,      // 퇴근 요청                   1~99%
    EquipmentUpgrade,       // 장비 업그레이드 요청         1~99%
    GameUpgradeRequest,     // 게임 업그레이드 요청         1~99%
    CompanyDinner,          // 오늘은 회식이다!             1~99%
    BossGossip,             // 사장님 뒷담까기              1~99%
    HackyCode,              // 야매코드                    26~99%
    YoutuberRequest,        // 유튜버 선공개 요청          51~99%
    DrillEvent,             // 옆 사무실 공사               1~99%
    ThiefEvent,             // 도둑이야!                    1~99%
}

[Serializable]
public class RandomEventData
{
    public RandomEventType type;
    public string title;
    public string description;
    public float  weight;
    public float  scoreBonus;

    // 이 이벤트가 등장할 수 있는 카테고리 범위 (1~4)
    // 예) categoryMin=1, categoryMax=3 → 카테고리 1·2·3 풀에 포함
    //     categoryMin=2, categoryMax=2 → 카테고리 2 전용
    public int    categoryMin;
    public int    categoryMax;
    public string portraitId;       // 특정 직원 이벤트일 때 설정, 없으면 ""
    public string systemMessage;   // 시스템 효과 안내 문구, 없으면 ""

    // 패트롤 도착 트리거 설정
    public bool   requiresPatrol        = false; // true면 진행도 도달 후 패트롤 도착 시 발동
    public string requiredPatrolPointId = "";    // 비어있으면 모든 지점에서 발동, 설정 시 해당 pointId에서만 발동
    public string targetEmployeeId      = "";    // onSetup에서 세팅 — 해당 직원이 도착해야 발동, 비어있으면 누구든 OK

    // onSetup에서 true로 설정하면 이벤트 전체 스킵
    public bool cancelled = false;

    // Show() 직전에 호출 — portraitId·description 등 동적 내용 세팅용
    public System.Action onSetup;
    public System.Action onApply;
}
