using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LeaderType { Planner, Programmer, Artist }
public enum LeaderScoreAim { Low, Mid, High } // 4회차 조준 선택 (약/중/강)

// 기여도 순위 한 항목 — 퇴사자도 캐시된 이름/초상화로 표시.
public struct ContributionEntry
{
    public EmployeeData emp;     // 재직 중이면 참조, 퇴사면 null
    public string name;          // 표시명 (퇴사면 "이름(퇴사)")
    public string portraitId;    // 초상화 로드용 (재직 emp 우선, 없으면 캐시)
    public float  percent;       // 0~100
    public bool   resigned;
}

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

    [Header("개발 완료 축하 patrol — CEO/비서 포함 랜덤 2명, 서로 다른 지점으로 이동")]
    public string developCompletePatrolPointA;
    public string developCompletePatrolPointB;

    // 직원별 프로젝트 기여도 (팀장점수 + 상시개발값 누적) — ResetProject 시 초기화
    private readonly Dictionary<string, float> _employeeContribution = new();
    // 퇴사 후에도 표시할 수 있도록 id별 이름/초상화 캐시 (재직 중일 때 최신값 갱신, 저장에도 포함)
    private readonly Dictionary<string, (string name, string portraitId)> _contributionInfo = new();

    public void AddEmployeeContribution(string empId, float amount)
    {
        if (string.IsNullOrEmpty(empId) || amount <= 0f) return;
        _employeeContribution.TryGetValue(empId, out float cur);
        _employeeContribution[empId] = cur + amount;

        // 재직 중일 때 이름/초상화 스냅샷 — 퇴사하면 ownedEmployees 에서 사라지므로 여기서 캐시.
        var e = FindEmployeeById(empId);
        if (e != null) _contributionInfo[empId] = (e.employeeName, e.portraitId);
    }

    EmployeeData FindEmployeeById(string id)
    {
        var em = EmployeeManager.Instance;
        if (em == null || string.IsNullOrEmpty(id)) return null;
        var e = em.ownedEmployees.Find(x => x.id == id);
        if (e == null && em.CEO != null && em.CEO.id == id) e = em.CEO; // CEO 는 ownedEmployees 에 없음
        return e;
    }

    // id → 표시용 엔트리. 재직 중이면 최신 이름/초상화, 퇴사면 캐시 + "(퇴사)" 접미.
    ContributionEntry BuildEntry(string id, float value, float total)
    {
        var emp = FindEmployeeById(id);
        _contributionInfo.TryGetValue(id, out var info);

        string baseName   = emp != null ? emp.employeeName
                                        : (!string.IsNullOrEmpty(info.name) ? info.name : "?");
        string portraitId = emp != null ? emp.portraitId : info.portraitId;
        bool   resigned   = emp == null;

        return new ContributionEntry
        {
            emp        = emp,
            name       = resigned ? $"{baseName}(퇴사)" : baseName,
            portraitId = portraitId,
            percent    = total > 0f ? value / total * 100f : 0f,
            resigned   = resigned
        };
    }

    // 기여도 1등 엔트리 반환. 기여 기록 없으면 default(ContributionEntry) (name == null).
    // CEO는 1등 표시에서 제외 — CEO가 최고 기여자여도 그 다음 순위 직원을 1등으로 표시(단, CEO 외 기여자가 아예 없으면 예외적으로 CEO 표시).
    public ContributionEntry GetTopContributor()
    {
        if (_employeeContribution.Count == 0) return default;
        float total = 0f;
        foreach (var v in _employeeContribution.Values) total += v;
        if (total <= 0f) return default;

        string ceoId = EmployeeManager.Instance?.CEO?.id;

        string topId = null; float topVal = -1f;
        foreach (var kv in _employeeContribution)
        {
            if (!string.IsNullOrEmpty(ceoId) && kv.Key == ceoId) continue;
            if (kv.Value > topVal) { topVal = kv.Value; topId = kv.Key; }
        }

        if (topId == null) // CEO 외 기여자 없음 — 부득이 CEO를 그대로 1등으로
            foreach (var kv in _employeeContribution)
                if (kv.Value > topVal) { topVal = kv.Value; topId = kv.Key; }

        return BuildEntry(topId, topVal, total);
    }

    // 기여도 내림차순 순위 리스트.
    // - 기여 기록이 있는 퇴사자 포함(분모 유지) → 표시명 "(퇴사)"
    // - 현재 보유 직원 전원 포함(기여 0이어도 0%로 표시) — 언제 채용됐든 항상 노출
    public List<ContributionEntry> GetContributionRanking()
    {
        var result = new List<ContributionEntry>();

        float total = 0f;
        foreach (var v in _employeeContribution.Values) total += v;

        // 기여 기록 + 보유 직원 전원 병합 (보유 직원은 없으면 0)
        var values = new Dictionary<string, float>(_employeeContribution);
        var em = EmployeeManager.Instance;
        if (em != null)
            foreach (var e in em.ownedEmployees)
                if (e != null && !values.ContainsKey(e.id))
                    values[e.id] = 0f;

        if (values.Count == 0) return result;

        var sorted = new List<KeyValuePair<string, float>>(values);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        foreach (var kv in sorted)
            result.Add(BuildEntry(kv.Key, kv.Value, total)); // total==0 이면 BuildEntry 가 0% 처리
        return result;
    }

    // 직원별 기여도 직렬화/복원 ("id:score:portraitId:name|..."). id/portraitId 는 콜론·파이프 없음.
    // name 은 마지막 필드라 콜론 포함 가능(Split limit 4). 구버전("id:score")도 복원 호환.
    public string GetContributionJson()
    {
        if (_employeeContribution.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var kv in _employeeContribution)
        {
            if (sb.Length > 0) sb.Append('|');
            _contributionInfo.TryGetValue(kv.Key, out var info);
            sb.Append(kv.Key).Append(':')
              .Append(kv.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(':')
              .Append(info.portraitId ?? "").Append(':')
              .Append(info.name ?? "");
        }
        return sb.ToString();
    }

    public void RestoreContribution(string data)
    {
        _employeeContribution.Clear();
        _contributionInfo.Clear();
        if (string.IsNullOrEmpty(data)) return;
        foreach (var part in data.Split('|'))
        {
            var f = part.Split(new[] { ':' }, 4);
            if (f.Length < 2 || string.IsNullOrEmpty(f[0])) continue;
            if (!float.TryParse(f[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float val)) continue;
            _employeeContribution[f[0]] = val;

            string portraitId = f.Length >= 3 ? f[2] : "";
            string name       = f.Length >= 4 ? f[3] : "";
            if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(portraitId))
                _contributionInfo[f[0]] = (name, portraitId);
        }
    }

    [Header("Leader Settings")]
    public float leaderTickDelay = 0.5f;

    [Header("Mode")]
    public bool IsOvertimeMode = false;

    public float BugPenalty { get; private set; } = 0f;
    public float BugEventBonus { get; private set; } = 0f; // 버그 이벤트 (미개발)

    public bool IsStarted { get; private set; } = false;
    public bool IsTriggered25 => _triggered25;
    public bool IsTriggered75 => _triggered75;
    public bool IsPendingLeaderScore25    => _pendingLeaderScore25;
    public bool IsPendingLeaderScore75    => _pendingLeaderScore75;
    public bool IsPendingLeaderSelect      { get; set; }
    public bool PendingInvestmentUIRestore { get; private set; }
    public bool PendingLeaderScoreResumeRestore { get; private set; }
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
    private bool _pendingCreativityGame;
    public bool IsPendingCreativityGame => _pendingCreativityGame;
    private bool _pendingDebuggingAlert;
    public bool IsPendingDebuggingAlert => _pendingDebuggingAlert;
    private float _leaderDevelopBonusTotal;
    private float _leaderPlanningBonusTotal;
    private float _leaderArtBonusTotal;

    // 게임 카테고리 아이템 (upgradeRandom/Develop/Art/Plan) 의 프로젝트당 1회 사용 추적.
    // ProjectSaveManager 가 CSV 로 직렬화 → 새 프로젝트(StartDevelopment) 진입 시 클리어.
    private readonly HashSet<string> _usedGameUpgrades = new();
    public bool IsGameUpgradeUsed(string itemId) => _usedGameUpgrades.Contains(itemId);
    public void MarkGameUpgradeUsed(string itemId) => _usedGameUpgrades.Add(itemId);
    public string GetUsedGameUpgradesString() => string.Join(",", _usedGameUpgrades);
    public void RestoreUsedGameUpgrades(string csv)
    {
        _usedGameUpgrades.Clear();
        if (string.IsNullOrEmpty(csv)) return;
        foreach (var id in csv.Split(','))
            if (!string.IsNullOrEmpty(id)) _usedGameUpgrades.Add(id);
    }

    // 게임 카테고리 아이템용 — 기존 팀장 점수 로직 그대로 통과시킨 후 1/4. n 추첨도 동일하게 가져감.
    public float CalcGameUpgradeScore(EmployeeData employee, EmployeeRole role)
    {
        int skill = role switch
        {
            EmployeeRole.Planner    => employee.planningSkill,
            EmployeeRole.Programmer => employee.developSkill,
            EmployeeRole.Artist     => employee.artSkill,
            _ => 0
        };
        int n = CalcLeaderTickCount(skill);
        float total = CalcLeaderScore(skill, n);
        return total * 0.25f;
    }

    // 네트워크 이벤트 등 duration 연장 시 진행도 표시 보정
    private float _progressVisualOffset = 0f;      // 현재 남은 시각 보정값
    private float _progressOffsetElapsedAtEvent = 0f; // 이벤트 발동 시점 _elapsed
    private float _progressOffsetExtension = 0f;   // 연장된 초 (보정 감소 기준)
    private float _characterSlowEndElapsed = 0f;   // 캐릭터 감속 종료 _elapsed (연장과 분리 — 게으른 천재는 연장의 2배 감속)

    private Dictionary<string, List<float>> _tickTimesMap = new();
    private Dictionary<string, int> _tickIndexMap = new();
    private Dictionary<string, int[]> _tickOrderMap = new();
    private Dictionary<string, CharacterState> _prevStateMap = new();
    private Dictionary<string, int> _midDevSeeds = new();
    private Dictionary<string, float> _midDevElapsed = new();
    private Coroutine _bugFixCoroutine;
    private Coroutine _characterSlowCoroutine;
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
        float baseDuration = 80f; // 전 규모 총 80초로 통일 (주수 16/24/32 차등은 secondsPerWeek가 담당)

        // 인원 수에 따른 개발 기간 조정 (추천 인원 대비 1명 차이 = 1주 증감)
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended; // 양수: 초과(기간 감소), 음수: 부족(기간 증가)
        float secondsPerWeek = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f / 16f, // 5.0초/주
            ProjectScale.Medium => 80f / 24f, // 3.33초/주
            ProjectScale.Large  => 80f / 32f, // 2.5초/주
            _ => 80f / 16f
        };
        developmentDuration = baseDuration - diff * secondsPerWeek;
        // 게으른 천재 +2주는 여기서 더하지 않고, 개발 시작(팀장 선택 직후)에 ExtendDevelopmentDuration 으로 적용
        // → 기간 연장 + 캐릭터 속도 감소 + progress 보정 + 저장/복원(networkSlow 필드)을 기존 기제로 일괄 처리

        IsStarted = true;
        _patrolStarted = false;
        _elapsed = 0f;
        _isRunning = false;
        _triggered25 = false;
        _triggered75 = false;
        _usedGameUpgrades.Clear();

        plannerLeader = null;
        programmerLeader = null;
        artistLeader = null;

        // 기여도는 프로젝트 단위 — ResetProject()는 "새 프로젝트가 이전 판매 도중 시작"된 경우
        // (SalesUI._newProjectStartedDuringSales) 이전 프로젝트가 끝나도 호출되지 않으므로,
        // 여기서 명시적으로 비워야 이전 프로젝트 기여자(퇴사자 포함)가 새 프로젝트로 새는 것을 막는다.
        _employeeContribution.Clear();
        _contributionInfo.Clear();

        DevelopmentPanelUI.Instance.ResetValues();
        // 이전 프로젝트에서 남은 개발틱 팝업 잔재 정리 — LeaderSelectUI 가 stale ActiveCount 를 기다리며
        // 딜레이/hang 되지 않도록 (개발 시작 직전엔 개발틱 팝업이 없는 게 정상).
        OfficeManager.Instance?.ClearAllPopups();
        RandomEventManager.Instance.InitEvents();
        RandomEventManager.Instance.ScheduleEvents();
        GameTimeManager.Instance.StopTime();

        void ProceedToInvestment()
        {
            InitTickMap(); // 야근 선택 완료 후 시드 확정
            int level = CreativityGameUI.Instance != null ? CreativityGameUI.Instance.CreativityLevel : 1;
            var pool = CreativityGameData.GetGridsForLevel(level);
            var grid = pool[UnityEngine.Random.Range(0, pool.Length)];
            CreativityGameUI.Instance?.SetFixedGrid(grid);
            RandomEventManager.Instance.TriggerInvestmentEvent(() => //투자 이벤트
            {
                IsPendingLeaderSelect = true;
                ProjectSaveManager.Instance.SaveProject();

                // onComplete=null — 개발 재개(ForceStartTime+DevelopmentCoroutine)는 팀장 선택 직후가 아니라
                // 팀장점수(LeaderScoreUI) 확정 후 ContinueAfterLeaderScore.StartDeveloping()에서 처리된다.
                // 여기서 콜백으로 미리 재개해버리면 점수 연출이 재생되는 동안 시간/개발진행도가 새는 버그가 생긴다.
                DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Planner, null);
            });
        }

        void ProceedToOvertimeSelect()
        {
            if (StageManager.Instance != null && StageManager.Instance.CurrentStage >= 2 && OvertimeSelectUI.Instance != null)
                OvertimeSelectUI.Instance.Open(ProceedToInvestment);
            else
                ProceedToInvestment();
        }

        void BeginSetup()
        {
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

        BeginSetup();
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
            int[] order = BuildTickOrder(tickCount, empOvertime, employee.EffectiveCreativitySkill, rng);
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

    int[] BuildTickOrder(int total, bool overtime, int stat, System.Random rng = null)
    {
        // 0:잭팟, 1:성공, 2:창의성, 3:버그, 4:꽝  (stat = 창의성 스탯)
        // 일반: 잭팟 10%, 성공 28.5%, 버그 25%, 창의성 = 0.15 + 0.05×C, 꽝 = 나머지(= 0.365 - 창의성)
        // 야근: 잭팟 15%, 성공 35%,   버그 25%, 창의성 = 0.15 + 0.05×C, 꽝 = 나머지(= 0.25  - 창의성)
        // C(창의성 계수) = (창의성스탯 - 17.5) / 707.5, [0,1] 클램프
        float jackpotP = overtime ? 0.15f : 0.10f;
        float successP = overtime ? 0.35f : 0.285f;
        float bugP     = 0.25f;
        float creativityC = Mathf.Clamp01((stat - 17.5f) / 707.5f);
        float creativityP = 0.15f + 0.05f * creativityC;

        // 테크트리 '각성 상태(money_awaken)' 미해금 시 잭팟 미발동, 확률은 성공으로 흡수
        bool awakenUnlocked = TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("money_awaken");
        if (!awakenUnlocked)
        {
            successP += jackpotP;
            jackpotP  = 0f;
        }

        int jackpot    = jackpotP > 0f ? Mathf.Max(1, Mathf.RoundToInt(total * jackpotP)) : 0;
        int success    = Mathf.RoundToInt(total * successP);
        int creativity = Mathf.RoundToInt(total * creativityP);
        int bug        = Mathf.Max(1, Mathf.RoundToInt(total * bugP));
        int blank      = total - jackpot - success - creativity - bug;
        if (blank < 0) { creativity = Mathf.Max(0, creativity + blank); blank = 0; }

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
        OfficeManager.Instance?.StopDevelopmentPatrol(); // patrol 중이던 직원 즉시 데스크 복귀
        if (!_patrolStarted)
        {
            _patrolStarted = true;
            // OfficeManager.Instance?.StartDevelopmentPatrol(); // 개발 중 자동 랜덤 patrol — 일단 비활성
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
                // 파견중 직원은 사무실 부재 — GetState 가 기본값 Working 을 줘서 틱이 누적되는 것 방지(복귀 시 OnEmployeeHired 가 새 틱 구성).
                if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(employee.id)) continue;

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

                // 시간이 아직 흐르는 상태에서 대기해야 상시개발값 팝업(카운트업+흡입+패널반영)이 자연 완료된다.
                yield return WaitForStatPopups();

                if (RandomEventManager.Instance.HasPendingEvent)
                {
                    // pending 중: 시간·캐릭터 이동은 유지, 진행도만 중단
                    _pendingLeaderScore25 = true;
                    yield break;
                }

                // pending 없음: 시간도 멈추고 팀장 선택 UI 표시
                GameTimeManager.Instance.StopTime();
                DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Programmer, null);
                yield break;
            }

            if (!_triggered75 && progress >= 0.75f)
            {
                _triggered75 = true;
                _isRunning = false;

                // 시간이 아직 흐르는 상태에서 대기해야 상시개발값 팝업(카운트업+흡입+패널반영)이 자연 완료된다.
                yield return WaitForStatPopups();

                if (RandomEventManager.Instance.HasPendingEvent)
                {
                    // pending 중: 시간·캐릭터 이동은 유지, 진행도만 중단
                    _pendingLeaderScore75 = true;
                    yield break;
                }

                // pending 없음: 시간도 멈추고 팀장 선택 UI 표시
                GameTimeManager.Instance.StopTime();
                DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Artist, null);
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

    // 25%/75%/100% 마일스톤 전환 시 상시개발값 팝업(카운트업+흡입+DevelopmentPanel 반영)이 끝날 때까지 대기.
    // GameTimeManager 를 멈추기 전에 호출해야 팝업이 자연스럽게 완료된다(멈추면 팝업도 같이 멈춤).
    IEnumerator WaitForStatPopups()
    {
        float waited = 0f;
        while (StatTickPopup.ActiveCount > 0 && waited < 10f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (StatTickPopup.ActiveCount > 0)
        {
            Debug.LogWarning($"[DevelopmentManager] 개발틱 팝업 대기 타임아웃(ActiveCount={StatTickPopup.ActiveCount}) — 강제 진행");
            StatTickPopup.ActiveCount = 0;
        }
    }

    // pending 이벤트 종료 후(ResumeFromEvent) 팀장 선택 UI를 열기 전 호출.
    // PauseForEvent()가 이미 시간을 멈춘 상태라 팝업이 자연 완료될 수 없으므로, 잠깐 풀어서
    // 완료시킨 뒤(WaitForStatPopups) 다시 멈춘다 — ForceStartTime 직후 StopTime 1회는 기존
    // OnDevelopmentComplete 경로(_pendingDevelopmentComplete)와 동일한 패턴.
    IEnumerator OpenLeaderSelectAfterPopups(LeaderType type)
    {
        if (StatTickPopup.ActiveCount > 0)
        {
            GameTimeManager.Instance.ForceStartTime();
            yield return WaitForStatPopups();
            GameTimeManager.Instance.StopTime();
        }
        DispatchPanelUI.Instance.OpenForLeaderSelect(type, null);
    }

    void FireRemainingTicks()
    {
        foreach (var employee in EmployeeManager.Instance.ownedEmployees.ToList())
        {
            if (!_tickTimesMap.ContainsKey(employee.id)) continue;
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(employee.id)) continue; // 파견중 직원은 잔여 틱도 누적 X

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
        // 개발 완료 시 캐릭터 감속·진행도 보정 즉시 해제 — SaveProject 전에 클리어해야 stale 복원 방지
        if (_characterSlowCoroutine != null) { StopCoroutine(_characterSlowCoroutine); _characterSlowCoroutine = null; }
        _characterSlowEndElapsed = 0f;
        _progressVisualOffset = 0f;
        _progressOffsetElapsedAtEvent = 0f;
        _progressOffsetExtension = 0f;
        OfficeManager.Instance?.SetCharacterSpeedMultiplier(1f);
        OfficeManager.Instance?.StopDevelopmentPatrol();
        StartCoroutine(FinishDevelopmentCompleteAfterPopups());
    }

    // FireRemainingTicks 로 한꺼번에 쏟아진 마지막 팝업들까지 전부 흡입·패널반영 된 뒤에
    // 시간을 멈추고 "개발 완료!" 알림을 띄운다.
    IEnumerator FinishDevelopmentCompleteAfterPopups()
    {
        yield return WaitForStatPopups();
        GameTimeManager.Instance.StopTime();
        AlertUI.Instance.Show("개발 완료!", () =>
        {
            _pendingCreativityGame = true;
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            MoneyManager.Instance?.SaveMoney();
            EmployeeManager.Instance?.SaveAllEmployees();
            ShowCreativityGame();
        });
    }

    public void ShowCreativityGame()
    {
        AlertUI.Instance.Show("창의성을 올리세요!", () =>
        {
            CreativityGameUI.Instance.Open(() =>
            {
                _pendingCreativityGame = false;
                _pendingDebuggingAlert = true;
                CurrentStage = ProjectStage.BugFixing;
                CreativityGameUI.Instance?.ClearEarnedBlocks();
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();
                MoneyManager.Instance?.SaveMoney();
                EmployeeManager.Instance?.SaveAllEmployees();

                // 약점 극복(HunsuUnique): 창의성 미니게임 후·디버깅 전. Unique+ 훈수쟁이 보유 시 최저 파트 상승 (CharacterUniqueEvents 위임).
                CharacterUniqueEvents.CheckWeaknessOvercome();

                AlertUI.Instance.Show("디버깅 작업을 시작합니다.", () =>
                {
                    _pendingDebuggingAlert = false;
                    ProjectSaveManager.Instance.SaveProject();
                    _bugFixReleased = false;
                    _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
                });
            });
        });
    }

    IEnumerator BugFixCoroutine()
    {
        CurrentStage = ProjectStage.BugFixing;
        OfficeManager.Instance?.SetAllWorking();
        OfficeManager.Instance?.StopDevelopmentPatrol();
        GameTimeManager.Instance.SetDebuggingSpeed(); // 디버깅 구간 4초/주 고정
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

            // 작업 가능한 직원(근무중·파견제외) — 팀 평균 창의성으로 확률·해결량 산출 (야근모드 동일)
            var workers = EmployeeManager.Instance.ownedEmployees
                .Where(e => OfficeManager.Instance.GetState(e.id) == CharacterState.Working
                            && !(DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(e.id))) // 파견중 제외
                .ToList();
            if (workers.Count == 0)
            {
                yield return null;
                continue;
            }

            float creativitySum = 0f;
            foreach (var e in workers) creativitySum += e.EffectiveCreativitySkill;
            float avgCreativity = creativitySum / workers.Count;
            float creSafe = Mathf.Max(1f, avgCreativity);

            // 해결 확률 = 0.60 + 0.30 × 창의성정규화,  창의성정규화 = (창의성 - 17.5) / 707.5 [0,1]
            float creNorm = Mathf.Clamp01((avgCreativity - 17.5f) / 707.5f);
            float bugFixChance = 0.60f + 0.30f * creNorm;
            // 테크트리 '버그 잡기 달인(money_bugmaster)' — 해결 확률 +10%
            if (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("money_bugmaster"))
                bugFixChance += 0.10f;
            bugFixChance = Mathf.Clamp01(bugFixChance);

            if (UnityEngine.Random.value < bugFixChance)
            {
                float remainingBug = DevelopmentPanelUI.Instance.GetBug();

                // 창의성처리보정 = 0.40 + 1.20 × 창의성로그정규화 [0.40~1.60],
                // 창의성로그정규화 = (ln(창의성) - ln(17.5)) / 3.7240 [0,1]
                float creLogNorm = Mathf.Clamp01((Mathf.Log(creSafe) - Mathf.Log(17.5f)) / 3.7240f);
                float creProcMul = 0.40f + 1.20f * creLogNorm;
                // 감속계수 = (잔여버그 / 초기버그)^0.35 — 버그가 줄수록 해결량 감소
                float slowFactor = Mathf.Pow(remainingBug / initialBug, 0.35f);

                // 해결량 = max(1, ceil(7.0 × 창의성처리보정 × 감속계수 × Random(0.5~1.5)))
                float fixAmount = Mathf.Max(1f,
                    Mathf.Ceil(7.0f * creProcMul * slowFactor * UnityEngine.Random.Range(0.5f, 1.5f)));

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

        // 버튜버 데뷔: 디버깅 끝난 뒤(결과 표시 직전) 조건 충족 시 인기도 3단계 + 이벤트 패널.
        // 시간은 위에서 StopTime 됨 → 패널 확인 후 결과(ShowResultInternal) 로. 조건 미충족이면 즉시 결과.
        CharacterUniqueEvents.CheckVtuberDebut(ShowResultInternal);
    }

    void ShowResultInternal()
    {
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

        BugPenalty = 0f;
        // 장착 특성 'a6'(bugTolerance) — 버그가 허용치 이하면 패널티 무조건 면제(확률 굴림 스킵).
        // 미장착(tolerance 0)이면 remainingBug>0 일 때만 굴림 → 기존 동작 그대로.
        int bugTolerance = TraitEffectApplier.GetBugTolerance();
        if (remainingBug > bugTolerance)
        {
            float triggerProb = Mathf.Min(1f, remainingBug * 0.02f);
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
        // CEO 는 랜덤이벤트 / 카운트 모두 자동 제외 — 연속 팀장 카운트도 증가 X (LeaderBurnout 트리거 조건 못 채움)
        if (!employee.isCEO)
        {
            employee.consecutiveLeaderCount++;
            employee.consecutiveNonLeaderCount = 0;
        }

        EmployeeData jealousyTarget = jealousyCandidates.Count > 0
            ? jealousyCandidates[UnityEngine.Random.Range(0, jealousyCandidates.Count)] : null;

        int leaderCount = employee.consecutiveLeaderCount;
        BuildAndShowLeaderScore(type, employee, jealousyTarget, leaderCount, null, true);
    }

    // ── 팀장점수 공식 상수 (고스탑 개편) ──
    static readonly float[] LeaderStageM          = { 1.35f, 1.5f, 1.65f, 1.8f }; // 단계(1~4) → 추천가중치 M
    static readonly int[]   LeaderDsMaxRoll       = { 12, 14, 14, 16 };           // 회차(1~4) → ds 랜덤 상한
    static readonly float[] LeaderRoundMultiplier = { 0.6f, 0.9f, 1f, 1.5f };     // 회차(1~4) → 회차 배율
    const float LeaderScoreC0 = 0.176f;
    // 성수(강화수치 0~25) → S(성수), U자형 곡선
    static readonly float[] LeaderGrowthS =
    {
        1f, 0.9077f, 0.856f, 0.8017f, 0.7708f, 0.7375f, 0.721f, 0.7013f, 0.6933f, 0.6807f,
        0.6765f, 0.6638f, 0.659f, 0.6598f, 0.6691f, 0.684f, 0.6889f, 0.6991f, 0.7111f, 0.7264f,
        0.7268f, 0.7659f, 0.8134f, 0.8737f, 0.9228f, 0.9601f
    };
    static float GetLeaderGrowthS(int enhanceLevel) => LeaderGrowthS[Mathf.Clamp(enhanceLevel, 0, LeaderGrowthS.Length - 1)];

    // 회차 점수 = 회차배율 × c0 × K(능력치) × S(성수) × ds^0.95 × Random(0.97~1.03)
    // 랜덤 진폭(0.97~1.03) 적용 전 기본값 — 실제 굴림과 범위 표시(min/max) 양쪽에서 재사용.
    static float CalcLeaderRoundScoreBase(int roundIndex, float K, float S, float ds)
        => LeaderRoundMultiplier[roundIndex] * LeaderScoreC0 * K * S * Mathf.Pow(ds, 0.95f);

    static float CalcLeaderRoundScore(int roundIndex, float K, float S, float ds)
        => Mathf.Round(CalcLeaderRoundScoreBase(roundIndex, K, S, ds) * UnityEngine.Random.Range(0.97f, 1.03f));

    // 4회차 조준(아직 U를 굴리기 전)의 예상 점수 범위 — 참고용 (현재 버튼 라벨은 GetAimDsRange 사용). 대기 중이 아니면 (0,0).
    public (int min, int max) GetAimScoreRange(LeaderScoreAim aim)
    {
        var ctx = _pendingRound4;
        if (ctx == null) return (0, 0);

        var (uMin, uMax) = GetAimURange(aim);
        float dsMin = 11f + uMin * ctx.M;
        float dsMax = 11f + uMax * ctx.M;
        float scoreMin = CalcLeaderRoundScoreBase(3, ctx.K, ctx.S, dsMin) * 0.97f;
        float scoreMax = CalcLeaderRoundScoreBase(3, ctx.K, ctx.S, dsMax) * 1.03f;
        return (Mathf.RoundToInt(scoreMin), Mathf.RoundToInt(scoreMax));
    }

    // 4회차 조준이 실제로 스트레스(ds)를 얼마나 올릴지의 범위 — 버튼 라벨 표시용. 대기 중이 아니면 (0,0).
    public (int min, int max) GetAimDsRange(LeaderScoreAim aim)
    {
        var ctx = _pendingRound4;
        if (ctx == null) return (0, 0);

        var (uMin, uMax) = GetAimURange(aim);
        float dsMin = 11f + uMin * ctx.M;
        float dsMax = 11f + uMax * ctx.M;
        return (Mathf.RoundToInt(dsMin), Mathf.RoundToInt(dsMax));
    }

    // ── 팀장점수 보너스 시스템 ──
    // 누적ds(스트레스)가 90/95/99를 최초 돌파하면 즉시 1회 지급, 중첩 가능(90+95+99 다 받을 수 있음).
    // 오버플로(>100) 회차는 보너스 지급 안 함 — 오버플로 확인 후에만 보너스를 붙이므로 자연히 걸러짐.
    // f 범위: [성수구간(0=0~10/1=11~20/2=21~25)][임계선(0=90/1=95/2=99)]
    static readonly (float min, float max)[,] LeaderBonusF = new (float, float)[3, 3]
    {
        { (1.99f, 2.985f),  (5.469f, 8.204f),   (22.143f, 33.215f) },
        { (1.634f, 2.452f), (4.43f, 6.645f),    (16.954f, 25.431f) },
        { (1.173f, 1.759f), (3.111f, 4.666f),   (10.86f, 16.291f) },
    };
    static readonly float[] LeaderBonusThresholds = { 90f, 95f, 99f };

    static int LeaderBonusGrowthBucket(int enhanceLevel)
    {
        if (enhanceLevel <= 10) return 0;
        if (enhanceLevel <= 20) return 1;
        return 2;
    }

    // 세션 진행 중 이미 지급된 임계선(90/95/99) — 중복 지급 방지. 새 팀장점수 시작 시 리셋.
    private bool[] _leaderBonusGranted = new bool[3];
    private float[] _leaderBonusAmounts = new float[3]; // 임계선별 실제 지급액 (UI "+n" 표시용)
    private float _leaderBonusTotal;

    // UI에서 임계선별(0=90/1=95/2=99) 지급 보너스 금액 조회 — 아직 안 넘었으면 0.
    public float GetLeaderBonusAmount(int thresholdIndex)
        => (thresholdIndex >= 0 && thresholdIndex < 3) ? _leaderBonusAmounts[thresholdIndex] : 0f;

    // 이번 세션에서 지급된 보너스 총합 (90+95+99 중첩 합산) — 4회차 팝콘 연출에 같이 얹을 때 사용.
    public float GetLeaderBonusTotal() => _leaderBonusTotal;

    // 3회차 끝난 시점(4회차 선택 대기 중)에 보여줄 "이 임계선까지 도달하면 받을 수 있는 총액" 미리보기.
    // 하위 임계선까지 중첩된 f를 누적으로 더한 범위 × K × S. 대기 중이 아니면 (0,0).
    public (float min, float max) GetLeaderBonusPotentialRange(int thresholdIndex)
    {
        var ctx = _pendingRound4;
        if (ctx == null || thresholdIndex < 0 || thresholdIndex > 2) return (0f, 0f);

        int bucket = LeaderBonusGrowthBucket(ctx.employee.enhancementLevel);
        float fMinSum = 0f, fMaxSum = 0f;
        for (int i = 0; i <= thresholdIndex; i++)
        {
            var (fMin, fMax) = LeaderBonusF[bucket, i];
            fMinSum += fMin;
            fMaxSum += fMax;
        }
        return (ctx.K * ctx.S * fMinSum, ctx.K * ctx.S * fMaxSum);
    }

    // cumDs가 새로 90/95/99를 넘겼으면 해당 임계선 보너스를 지급(누적). 오버플로 회차에서는 호출하지 않는다.
    void CheckLeaderBonusThresholds(float cumDs, int enhanceLevel, float K, float S)
    {
        int bucket = LeaderBonusGrowthBucket(enhanceLevel);
        for (int i = 0; i < 3; i++)
        {
            if (_leaderBonusGranted[i]) continue;
            if (cumDs <= LeaderBonusThresholds[i]) continue;
            _leaderBonusGranted[i] = true;

            var (fMin, fMax) = LeaderBonusF[bucket, i];
            float f = Mathf.Round(UnityEngine.Random.Range(fMin, fMax) * 100f) / 100f;
            float bonus = K * S * f;
            _leaderBonusAmounts[i] = bonus;
            _leaderBonusTotal += bonus;
        }
    }

    // 4회차 조준 선택 대기 컨텍스트 — 1~3회차가 오버플로 없이 끝나면 여기 저장하고
    // SelectRound4Aim() 호출(유저 UI)을 기다린다.
    private class PendingRound4Context
    {
        public LeaderType type;
        public EmployeeData employee;
        public EmployeeData jealousyTarget;
        public int leaderCount;
        public float K, S, M;
        public bool lazyGenius;
        public float cumDs3;
        public float[] fullRoundScores;
        public float[] roundScores;
        public float[] cumDsAfter;
        public bool testMode; // true면 저장/기여도/스탯반영 전부 스킵 (TestLeaderScore 전용)
    }
    private PendingRound4Context _pendingRound4;
    public bool IsPendingRound4Aim => _pendingRound4 != null;

    // ── 테스트 전용 진입점 ──────────────────────────────────────
    // 프로젝트 진행 상태(개발 단계/기여도/저장)에 전혀 영향 없이 팀장점수 연출만 실행해본다.
    // 4회차까지 끝나도 저장이나 DevelopmentPanelUI 반영이 일절 일어나지 않고 그냥 패널만 닫힌다.
    public void TestLeaderScore(EmployeeData employee, LeaderType type)
    {
        if (employee == null || LeaderScoreUI.Instance == null) return;

        // 정상 흐름(LeaderSelectUI.OnSelectLeader)을 거치지 않고 바로 점수 연출로 들어가므로,
        // 컨테이너 패널을 직접 켜줘야 함 — 안 하면 LeaderScoreUI 내부 패널만 active여도 부모가
        // 꺼져있어 화면에 아무것도 안 보임 (ResumeLeaderScore와 동일 패턴).
        if (LeaderSelectUI.Instance != null)
        {
            LeaderSelectUI.Instance.entireLeaderPanel.gameObject.SetActive(true);
            if (LeaderSelectUI.Instance.leaderPanel != null)
                LeaderSelectUI.Instance.leaderPanel.gameObject.SetActive(false);
        }

        int skill = type switch
        {
            LeaderType.Planner    => employee.EffectivePlanningSkill,
            LeaderType.Programmer => employee.EffectiveDevelopSkill,
            LeaderType.Artist     => employee.EffectiveArtSkill,
            _ => 0
        };

        int stage = RollLeaderStage(employee.enhancementLevel);
        float M = LeaderStageM[stage - 1];
        float K = 0.8738f + 0.026409f * Mathf.Pow(skill, 0.9081f);
        bool lazyGenius = type == LeaderType.Programmer && CharacterTraitApplier.HasLazyGeniusOwned();
        if (lazyGenius) K *= CharacterTraitApplier.LAZY_GENIUS_LEADER_BONUS;
        float S = GetLeaderGrowthS(employee.enhancementLevel);

        _leaderBonusGranted  = new bool[3];
        _leaderBonusAmounts  = new float[3];
        _leaderBonusTotal    = 0f;

        var fullRoundScores = new float[4];
        var roundScores     = new float[4];
        var cumDsAfter      = new float[4];
        float cumDs = 0f;
        int overflowRound = -1;
        float cutFactor = 0f;
        bool overflowedEarly = false;

        for (int r = 0; r < 3; r++)
        {
            int roll = UnityEngine.Random.Range(1, LeaderDsMaxRoll[r] + 1);
            float ds = 11f + roll * M;
            cumDs += ds;
            cumDsAfter[r] = cumDs;

            if (cumDs > 100f)
            {
                overflowRound = r;
                cutFactor = UnityEngine.Random.Range(0.10f, 0.20f);
                fullRoundScores[r] = 0f;
                roundScores[r] = 0f;
                for (int k = 0; k < r; k++)
                    roundScores[k] = Mathf.Round(fullRoundScores[k] * (1f - cutFactor));
                overflowedEarly = true;
                break;
            }

            fullRoundScores[r] = CalcLeaderRoundScore(r, K, S, ds);
            roundScores[r] = fullRoundScores[r];
            CheckLeaderBonusThresholds(cumDs, employee.enhancementLevel, K, S);
        }

        if (!overflowedEarly)
        {
            _pendingRound4 = new PendingRound4Context
            {
                type = type, employee = employee, jealousyTarget = null, leaderCount = 1,
                K = K, S = S, M = M, lazyGenius = lazyGenius, cumDs3 = cumDs,
                fullRoundScores = fullRoundScores, roundScores = roundScores, cumDsAfter = cumDsAfter,
                testMode = true
            };
            LeaderScoreUI.Instance.ShowPendingRound4(employee, type, fullRoundScores, roundScores, cumDsAfter, testMode: true);
            return;
        }

        float total = 0f;
        for (int r = 0; r < 4; r++) total += roundScores[r];
        total += _leaderBonusTotal;

        LeaderScoreUI.Instance.Show(employee, type, fullRoundScores, roundScores, cumDsAfter,
                                    total, overflowRound, cutFactor,
                                    () => { /* 테스트 확정 — 아무 것도 반영 안 함 */ },
                                    testMode: true);
    }

    // 팀장 점수 산출(신규 추첨) 또는 저장값 재생 → LeaderScoreUI 표시.
    // doSave=true 면 선택 시점 저장(돈/직원 항상, overflow 면 값까지 잠금). 복원 재생/재추첨은 doSave=false.
    void BuildAndShowLeaderScore(LeaderType type, EmployeeData employee,
                                 EmployeeData jealousyTarget, int leaderCount, LeaderScoreResume saved, bool doSave)
    {
        float[] fullRoundScores;
        float[] roundScores;
        float[] cumDsAfter;
        float total;
        int overflowRound;
        float cutFactor;
        int hunsuBonus;
        LeaderType hunsuBonusTarget;

        if (saved != null && saved.hasValues)
        {
            // overflow 확정본 — 저장된 값 그대로 재생 (재추첨 없음)
            fullRoundScores  = (float[])saved.fullRoundScores.Clone();
            roundScores      = (float[])saved.roundScores.Clone();
            cumDsAfter       = (float[])saved.cumDsAfter.Clone();
            total            = saved.total;
            overflowRound    = saved.overflowRound;
            cutFactor        = saved.cutFactor;
            hunsuBonus       = saved.hunsuBonus;
            hunsuBonusTarget = (LeaderType)saved.hunsuBonusTarget;
        }
        else
        {
            // 신규 추첨 (또는 non-overflow 재접속 재추첨)
            // 팀장 점수 = Effective 주스탯(만족도·버프/디버프·사내연애·오타쿠 전부 포함)
            int skill = type switch
            {
                LeaderType.Planner    => employee.EffectivePlanningSkill,
                LeaderType.Programmer => employee.EffectiveDevelopSkill,
                LeaderType.Artist     => employee.EffectiveArtSkill,
                _ => 0
            };

            // 강화도(성수) 기반 단계 추첨 → 추천가중치 M
            int stage = RollLeaderStage(employee.enhancementLevel);
            float M = LeaderStageM[stage - 1];

            // K(능력치) = 0.8738 + 0.026409 × 주스탯^0.9081
            float K = 0.8738f + 0.026409f * Mathf.Pow(skill, 0.9081f);
            bool lazyGenius = type == LeaderType.Programmer && CharacterTraitApplier.HasLazyGeniusOwned();
            if (lazyGenius)
                K *= CharacterTraitApplier.LAZY_GENIUS_LEADER_BONUS;

            float S = GetLeaderGrowthS(employee.enhancementLevel);

            // 보너스 임계선(90/95/99) 지급 상태 리셋 — 새 팀장점수 세션 시작
            _leaderBonusGranted = new bool[3];
            _leaderBonusAmounts = new float[3];
            _leaderBonusTotal = 0f;

            // 1~3회차 ds / 회차 점수 자동 진행 (누적 ds 100 초과 시 0점 + 전 회차 차감 후 종료).
            // 4회차는 여기서 굴리지 않는다 — 3회차까지 오버플로 없이 끝나야만 유저가 조준(약/중/강)을
            // 선택한 뒤 SelectRound4Aim()에서 계산한다.
            fullRoundScores = new float[4];
            roundScores     = new float[4];
            cumDsAfter      = new float[4];
            float cumDs = 0f;
            overflowRound = -1;
            cutFactor = 0f;
            bool overflowedEarly = false;

            for (int r = 0; r < 3; r++)
            {
                int roll = UnityEngine.Random.Range(1, LeaderDsMaxRoll[r] + 1); // 1~상한 정수
                float ds = 11f + roll * M;
                cumDs += ds;
                cumDsAfter[r] = cumDs;

                if (cumDs > 100f)
                {
                    overflowRound = r;
                    cutFactor = UnityEngine.Random.Range(0.10f, 0.20f);
                    fullRoundScores[r] = 0f;
                    roundScores[r] = 0f;
                    for (int k = 0; k < r; k++)
                        roundScores[k] = Mathf.Round(fullRoundScores[k] * (1f - cutFactor));
                    overflowedEarly = true;
                    break;
                }

                fullRoundScores[r] = CalcLeaderRoundScore(r, K, S, ds);
                roundScores[r] = fullRoundScores[r];

                // 오버플로가 아닌 회차에서만 보너스 임계선 체크 (오버플로면 위 if에서 이미 break)
                CheckLeaderBonusThresholds(cumDs, employee.enhancementLevel, K, S);
            }

            if (!overflowedEarly)
            {
                // 3회차까지 무사히 끝남 — 4회차는 유저 선택 대기. SelectRound4Aim()이 이어받는다.
                _pendingRound4 = new PendingRound4Context
                {
                    type = type, employee = employee, jealousyTarget = jealousyTarget, leaderCount = leaderCount,
                    K = K, S = S, M = M, lazyGenius = lazyGenius, cumDs3 = cumDs,
                    fullRoundScores = fullRoundScores, roundScores = roundScores, cumDsAfter = cumDsAfter
                };

                _leaderScoreResume = new LeaderScoreResume
                {
                    active = true, hasValues = false, leaderType = (int)type, employeeId = employee.id,
                    jealousyTargetId = jealousyTarget != null ? jealousyTarget.id : "", leaderCount = leaderCount
                };

                GameTimeManager.Instance.StopTime();
                if (doSave)
                {
                    if (CurrentStage == ProjectStage.None) CurrentStage = ProjectStage.Developing;
                    MoneyManager.Instance.SaveMoney();
                    ProjectSaveManager.Instance.SaveProject();
                    GameTimeManager.Instance.SaveGameTime();
                    EmployeeManager.Instance.SaveAllEmployees();
                }

                LeaderScoreUI.Instance.ShowPendingRound4(employee, type, fullRoundScores, roundScores, cumDsAfter);
                return;
            }

            total = 0f;
            for (int r = 0; r < 4; r++) total += roundScores[r];
            total += _leaderBonusTotal; // 90/95/99 임계선 보너스 (오버플로 회차는 지급 안 됐으므로 차감 대상 아님)

            // 훈수쟁이(개발팀장): 개발 점수의 10% 를 기획/아트 중 랜덤 1곳에 추가 (게으른 천재 ×1.3 이전 base 기준)
            hunsuBonus = 0;
            hunsuBonusTarget = LeaderType.Planner;
            if (type == LeaderType.Programmer && CharacterTraitApplier.IsHunsu(employee))
            {
                float baseTotal = lazyGenius ? total / CharacterTraitApplier.LAZY_GENIUS_LEADER_BONUS : total;
                hunsuBonus = Mathf.RoundToInt(baseTotal * CharacterTraitApplier.HUNSU_BONUS_RATIO);
                hunsuBonusTarget = UnityEngine.Random.value < 0.5f ? LeaderType.Planner : LeaderType.Artist;
            }
        }

        if (type == LeaderType.Programmer) _leaderDevelopBonusTotal  = total;
        if (type == LeaderType.Planner)    _leaderPlanningBonusTotal = total;
        if (type == LeaderType.Artist)     _leaderArtBonusTotal      = total;

        bool hasValues = overflowRound != -1; // overflow 면 값 잠금 저장, 아니면 직원만 저장(재추첨 허용)

        // 선택 시점 진행 컨텍스트 — 직원/역할 항상, overflow 면 값까지
        _leaderScoreResume = new LeaderScoreResume
        {
            active           = true,
            hasValues        = hasValues,
            leaderType       = (int)type,
            employeeId       = employee.id,
            fullRoundScores  = hasValues ? (float[])fullRoundScores.Clone() : new float[4],
            roundScores      = hasValues ? (float[])roundScores.Clone()     : new float[4],
            cumDsAfter       = hasValues ? (float[])cumDsAfter.Clone()       : new float[4],
            total            = hasValues ? total : 0f,
            overflowRound    = hasValues ? overflowRound : -1,
            cutFactor        = hasValues ? cutFactor : 0f,
            hunsuBonus       = hasValues ? hunsuBonus : 0,
            hunsuBonusTarget = (int)hunsuBonusTarget,
            jealousyTargetId = jealousyTarget != null ? jealousyTarget.id : "",
            leaderCount      = leaderCount
        };

        GameTimeManager.Instance.StopTime();

        if (doSave)
        {
            // 기획 팀장은 이 시점 CurrentStage 가 None → SaveProject 스킵/isInProgress=false 가 되어 복원 불가. Developing 승격.
            if (CurrentStage == ProjectStage.None) CurrentStage = ProjectStage.Developing;
            MoneyManager.Instance.SaveMoney();
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            EmployeeManager.Instance.SaveAllEmployees();
        }

        LeaderScoreUI.Instance.Show(employee, type, fullRoundScores, roundScores, cumDsAfter,
                                    total, overflowRound, cutFactor,
                                    () => ContinueAfterLeaderScore(type, employee, jealousyTarget, leaderCount, total, hunsuBonus, hunsuBonusTarget));
    }

    // 조준별 U 범위: 약 1~6 / 중 5~12 / 강 11~16 (기본 U 1~16을 3등분+1칸 겹침) — 버튼 라벨 표시 등에도 재사용.
    public static (int min, int max) GetAimURange(LeaderScoreAim aim) => aim switch
    {
        LeaderScoreAim.Low  => (1, 6),
        LeaderScoreAim.Mid  => (5, 12),
        LeaderScoreAim.High => (11, 16),
        _ => (1, 6)
    };

    // 4회차 조준 선택(유저 UI에서 호출) — U 범위 결정 → ds/회차점수 계산 → 정산/저장 → 4회차 연출 재생까지 이어감.
    public void SelectRound4Aim(LeaderScoreAim aim)
    {
        var ctx = _pendingRound4;
        if (ctx == null) return;
        _pendingRound4 = null;

        (int uMin, int uMax) = GetAimURange(aim);
        int U = UnityEngine.Random.Range(uMin, uMax + 1);
        float ds4 = 11f + U * ctx.M;
        float cumDs = ctx.cumDs3 + ds4;

        var fullRoundScores = ctx.fullRoundScores;
        var roundScores     = ctx.roundScores;
        var cumDsAfter      = ctx.cumDsAfter;
        cumDsAfter[3] = cumDs;

        int overflowRound = -1;
        float cutFactor = 0f;

        if (cumDs > 100f)
        {
            overflowRound = 3;
            cutFactor = UnityEngine.Random.Range(0.10f, 0.20f);
            fullRoundScores[3] = 0f;
            roundScores[3] = 0f;
            for (int k = 0; k < 3; k++)
                roundScores[k] = Mathf.Round(fullRoundScores[k] * (1f - cutFactor));
        }
        else
        {
            fullRoundScores[3] = CalcLeaderRoundScore(3, ctx.K, ctx.S, ds4);
            roundScores[3] = fullRoundScores[3];

            // 오버플로가 아닐 때만 보너스 임계선 체크 (1~3회차에서 이미 지급된 건 _leaderBonusGranted로 중복 방지됨)
            CheckLeaderBonusThresholds(cumDs, ctx.employee.enhancementLevel, ctx.K, ctx.S);
        }

        float total = 0f;
        for (int r = 0; r < 4; r++) total += roundScores[r];
        total += _leaderBonusTotal; // 90/95/99 임계선 보너스 (1~3회차분 포함 누적치)

        if (ctx.type == LeaderType.Programmer) _leaderDevelopBonusTotal  = total;
        if (ctx.type == LeaderType.Planner)    _leaderPlanningBonusTotal = total;
        if (ctx.type == LeaderType.Artist)     _leaderArtBonusTotal      = total;

        int hunsuBonus = 0;
        LeaderType hunsuBonusTarget = LeaderType.Planner;
        if (ctx.type == LeaderType.Programmer && CharacterTraitApplier.IsHunsu(ctx.employee))
        {
            float baseTotal = ctx.lazyGenius ? total / CharacterTraitApplier.LAZY_GENIUS_LEADER_BONUS : total;
            hunsuBonus = Mathf.RoundToInt(baseTotal * CharacterTraitApplier.HUNSU_BONUS_RATIO);
            hunsuBonusTarget = UnityEngine.Random.value < 0.5f ? LeaderType.Planner : LeaderType.Artist;
        }

        // 오버플로 확정 시에만 값 잠금 저장 (기존 설계와 동일 — non-overflow 는 재접속 시 재추첨 허용)
        // 테스트 모드는 저장 자체를 스킵(프로젝트 상태에 영향 없어야 함)
        if (overflowRound != -1 && !ctx.testMode)
        {
            _leaderScoreResume.hasValues        = true;
            _leaderScoreResume.fullRoundScores  = (float[])fullRoundScores.Clone();
            _leaderScoreResume.roundScores      = (float[])roundScores.Clone();
            _leaderScoreResume.cumDsAfter       = (float[])cumDsAfter.Clone();
            _leaderScoreResume.total            = total;
            _leaderScoreResume.overflowRound    = overflowRound;
            _leaderScoreResume.cutFactor        = cutFactor;
            _leaderScoreResume.hunsuBonus       = hunsuBonus;
            _leaderScoreResume.hunsuBonusTarget = (int)hunsuBonusTarget;

            MoneyManager.Instance.SaveMoney();
            ProjectSaveManager.Instance.SaveProject();
            GameTimeManager.Instance.SaveGameTime();
            EmployeeManager.Instance.SaveAllEmployees();
        }

        var type = ctx.type;
        var employee = ctx.employee;
        var jealousyTarget = ctx.jealousyTarget;
        var leaderCount = ctx.leaderCount;
        bool testMode = ctx.testMode;

        System.Action onComplete = testMode
            ? () => { /* 테스트 — 확정해도 프로젝트에 아무 영향 없음 */ }
            : () => ContinueAfterLeaderScore(type, employee, jealousyTarget, leaderCount, total, hunsuBonus, hunsuBonusTarget);

        LeaderScoreUI.Instance.PlayRound4AndFinish(fullRoundScores, roundScores, cumDsAfter,
                                    total, overflowRound, cutFactor, onComplete);
    }

    // 팀장점수 확정(또는 재접속 재개 후 확정) → 개발 진행으로 이어가는 공통 로직.
    // hunsuBonus>0 이면 팀장점수 연출/패널과 분리해 여기서 AlertUI3 로 안내 후 기획/아트에 반영한다.
    void ContinueAfterLeaderScore(LeaderType type, EmployeeData employee, EmployeeData jealousyTarget, int leaderCount, float total,
                                   int hunsuBonus = 0, LeaderType hunsuBonusTarget = LeaderType.Planner)
    {
        ClearLeaderScoreResume();
        // 기여도 가산은 확정 시점 1회만 (재추첨/재접속 시 이중 가산 방지)
        AddEmployeeContribution(employee.id, total);

        void StartDeveloping()
        {
            IsPendingLeaderSelect = false; // 저장 직전에 펜딩 해제 (재시작 시 재선택 방지)
            _isRunning = true;
            CurrentStage = ProjectStage.Developing;
            GameTimeManager.Instance.ForceStartTime();
            Debug.Log("팀장점수완료 저장");

            // 게으른 천재: 첫 팀장(기획) 선정 직후 1회 — 기간 +2주 / 캐릭터 감속 4주(연장의 2배)
            if (type == LeaderType.Planner && CharacterTraitApplier.HasLazyGeniusOwned())
            {
                float spw = ProjectSetupUI.SelectedScale switch
                {
                    ProjectScale.Small  => 80f / 16f,
                    ProjectScale.Medium => 80f / 24f,
                    ProjectScale.Large  => 80f / 32f,
                    _ => 80f / 16f
                };
                float ext = CharacterTraitApplier.LAZY_GENIUS_EXTRA_WEEKS * spw;
                ExtendDevelopmentDuration(ext, ext * 2f);
                InfoUI.Instance?.Show("게으른 천재 특성 발동!");
            }

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

        void ProceedAfterHunsu()
        {
            // CEO 는 랜덤이벤트 자체 제외 — burnout 트리거 명시적 가드
            if (!employee.isCEO && leaderCount >= 3 && UnityEngine.Random.value < 0.5f)
                RandomEvents_Condition.TriggerLeaderBurnoutEvent(employee, leaderCount, AfterBurnout);
            else
                AfterBurnout();
        }

        // 훈수쟁이 보너스가 있으면 AlertUI3로 안내하고, 유저가 확인을 눌러야(onConfirm) 이후 개발 재개
        // (ForceStartTime 포함) 로직으로 넘어간다. 안 그러면 OnClickConfirm 에서 이미 풀어둔 시간 위로
        // StartDeveloping 의 ForceStartTime 이 곧바로 다시 겹쳐 걸려, 팝업이 떠있는 동안에도 시간이 흐름.
        if (hunsuBonus > 0)
        {
            float pl = hunsuBonusTarget == LeaderType.Planner ? hunsuBonus : 0f;
            float ar = hunsuBonusTarget == LeaderType.Artist  ? hunsuBonus : 0f;
            DevelopmentPanelUI.Instance?.AddValuesInstant(pl, 0f, ar, 0f, 0f);

            string targetName = hunsuBonusTarget == LeaderType.Planner ? "기획" : "아트";
            AlertUI.Instance?.ShowPortrait(
                $"훈수쟁이 특성 발동!\n{targetName} 점수 +{hunsuBonus}",
                employee.portraitId, "훈수쟁이", ProceedAfterHunsu);
        }
        else
        {
            ProceedAfterHunsu();
        }
    }

    // ── 팀장점수 진행 저장/복원 ─────────────────
    [System.Serializable]
    private class LeaderScoreResume
    {
        public bool active;
        public bool hasValues;          // true=overflow 확정본(값 잠금) / false=직원만(재접속 시 재추첨)
        public int leaderType;
        public string employeeId = "";
        public float[] fullRoundScores = new float[4];
        public float[] roundScores = new float[4];
        public float[] cumDsAfter = new float[4];
        public float total;
        public int overflowRound = -1;
        public float cutFactor;
        public int hunsuBonus;
        public int hunsuBonusTarget;
        public string jealousyTargetId = "";
        public int leaderCount;
    }
    private LeaderScoreResume _leaderScoreResume = new LeaderScoreResume();

    public bool IsLeaderScoreResumeActive => _leaderScoreResume != null && _leaderScoreResume.active;

    // 뒤끝 저장은 원시 JSON 문자열을 안전히 못 다루므로(다른 복합 저장도 전부 구분자 방식),
    // '|' 필드 / ',' 배열 구분자 문자열로 직렬화. 부동소수는 InvariantCulture.
    public string GetLeaderScoreResumeJson()
    {
        var d = _leaderScoreResume;
        if (d == null || !d.active) return "";
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string Arr(float[] a)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++) { if (i > 0) sb.Append(','); sb.Append((i < a.Length ? a[i] : 0f).ToString("R", ci)); }
            return sb.ToString();
        }
        return string.Join("|", new string[]
        {
            d.active ? "1" : "0",
            d.hasValues ? "1" : "0",
            d.leaderType.ToString(ci),
            d.employeeId ?? "",
            Arr(d.fullRoundScores), Arr(d.roundScores), Arr(d.cumDsAfter),
            d.total.ToString("R", ci),
            d.overflowRound.ToString(ci),
            d.cutFactor.ToString("R", ci),
            d.hunsuBonus.ToString(ci),
            d.hunsuBonusTarget.ToString(ci),
            d.jealousyTargetId ?? "",
            d.leaderCount.ToString(ci)
        });
    }
    public void RestoreLeaderScoreResumeJson(string data)
    {
        _leaderScoreResume = new LeaderScoreResume();
        if (string.IsNullOrEmpty(data)) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var f = data.Split('|');
            if (f.Length < 14) return;
            float[] Arr(string s)
            {
                var p = s.Split(',');
                var a = new float[4];
                for (int i = 0; i < 4 && i < p.Length; i++) float.TryParse(p[i], System.Globalization.NumberStyles.Float, ci, out a[i]);
                return a;
            }
            var d = new LeaderScoreResume
            {
                active           = f[0] == "1",
                hasValues        = f[1] == "1",
                leaderType       = int.Parse(f[2], ci),
                employeeId       = f[3],
                fullRoundScores  = Arr(f[4]),
                roundScores      = Arr(f[5]),
                cumDsAfter       = Arr(f[6]),
                total            = float.Parse(f[7], System.Globalization.NumberStyles.Float, ci),
                overflowRound    = int.Parse(f[8], ci),
                cutFactor        = float.Parse(f[9], System.Globalization.NumberStyles.Float, ci),
                hunsuBonus       = int.Parse(f[10], ci),
                hunsuBonusTarget = int.Parse(f[11], ci),
                jealousyTargetId = f[12],
                leaderCount      = int.Parse(f[13], ci)
            };
            _leaderScoreResume = d;
        }
        catch { _leaderScoreResume = new LeaderScoreResume(); }
    }
    public void ClearLeaderScoreResume() { _leaderScoreResume = new LeaderScoreResume(); }

    // 재접속 복원 — 같은 직원으로 1회차부터 재생. overflow 확정본이면 저장값 그대로(잠금), 아니면 재추첨.
    public void ResumeLeaderScore()
    {
        PendingLeaderScoreResumeRestore = false;
        var d = _leaderScoreResume;
        if (d == null || !d.active) return;

        LeaderType type = (LeaderType)d.leaderType;
        EmployeeData emp = FindEmployeeById(d.employeeId);
        if (emp == null) { ClearLeaderScoreResume(); return; }

        // entireLeaderPanel(ModalLayer.UseBlur=true)을 켜는 순간 ModalBlocker가 그 자리에서 동기적으로
        // 화면을 캡처해 블러 RT를 1회 굽는다(다음 프레임의 DevelopmentPanelUI.Update()를 기다려주지 않음).
        // 정상 플레이 흐름(OnSelectLeader)은 IsPendingLeaderSelect가 true 된 지 한참(여러 프레임) 지난
        // 뒤라 DevelopmentPanelUI가 이미 자체 교정된 상태지만, 씬 로드 직후 복원은 RestoreState에서
        // CurrentStage/스탯 값을 막 세팅한 직후라 DevelopmentPanelUI가 아직 한 번도 갱신되지 않았을 수
        // 있음 — 그 상태로 캡처되면 defaultText가 활성화된 채 스탯 텍스트가 비어있는 화면이 블러에
        // 고정되어 버림(RenderBlur는 켜지는 순간 1회만 찍음). 캡처 전에 강제로 한 번 동기화한다.
        DevelopmentPanelUI.Instance?.UpdateDefaultText();

        // 점수창 컨테이너만 켜고 팀장 선택 슬롯 리스트는 숨김 (정상 흐름의 OnSelectLeader 대체)
        LeaderSelectUI.Instance.entireLeaderPanel.gameObject.SetActive(true);
        if (LeaderSelectUI.Instance.leaderPanel != null)
            LeaderSelectUI.Instance.leaderPanel.gameObject.SetActive(false);

        // 리더 필드 복원 (확정 전 종료라 CEO 등은 RestoreState 의 leaderId 조회로 비어있을 수 있음)
        switch (type)
        {
            case LeaderType.Planner:    plannerLeader = emp;    break;
            case LeaderType.Programmer: programmerLeader = emp; break;
            case LeaderType.Artist:     artistLeader = emp;     break;
        }

        if (d.hasValues)
        {
            EmployeeData jt = FindEmployeeById(d.jealousyTargetId);
            BuildAndShowLeaderScore(type, emp, jt, d.leaderCount, d, false);    // 저장값 재생(잠금)
        }
        else
        {
            // 재추첨 — doSave=true 로 결과를 다시 저장. 재추첨에서 overflow 나면 hasValues=1 로 잠겨
            // 다음 재접속부터는 그 값으로 고정(설계: overflow 확정 시 저장). non-overflow 면 계속 재추첨 가능.
            BuildAndShowLeaderScore(type, emp, null, d.leaderCount, null, true);
        }
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
        bool pendingLeaderScore25 = false, bool pendingLeaderScore75 = false,
        bool pendingLeaderSelect = false, bool pendingInvestmentUI = false,
        bool pendingCreativityGame = false, bool pendingDebuggingAlert = false)
    {
        float baseDuration = 80f; // 전 규모 총 80초로 통일 (주수 16/24/32 차등은 secondsPerWeek가 담당)
        int recommended = ProjectData.GetRecommendedStaff(ProjectSetupUI.SelectedScale);
        int actual = EmployeeManager.Instance.ownedEmployees.Count;
        int diff = actual - recommended;
        float secondsPerWeek = ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f / 16f, // 5.0초/주
            ProjectScale.Medium => 80f / 24f, // 3.33초/주
            ProjectScale.Large  => 80f / 32f, // 2.5초/주
            _ => 80f / 16f
        };
        // 게으른 천재 +2주는 savedDuration 에 이미 반영돼 복원됨(ExtendDevelopmentDuration 시점에 baked). 캐릭터 속도 감소도 networkSlowEndElapsed 로 복원.
        developmentDuration = savedDuration > 0f ? savedDuration : baseDuration - diff * secondsPerWeek;
        _elapsed = elapsed;

        // 네트워크 이슈 progress 보정 복원
        _progressOffsetElapsedAtEvent = progOffsetElapsedAtEvent;
        _progressOffsetExtension = progOffsetExtension;
        _progressVisualOffset = progVisualOffset;

        // 캐릭터 속도 감소 복원 (감속 종료 시점 = networkSlowEndElapsed, 연장과 분리 저장됨)
        _characterSlowEndElapsed = networkSlowEndElapsed;
        if (networkSlowEndElapsed > elapsed)
        {
            OfficeManager.Instance?.SetCharacterSpeedMultiplier(0.5f);
            if (_characterSlowCoroutine != null) StopCoroutine(_characterSlowCoroutine);
            _characterSlowCoroutine = StartCoroutine(RestoreCharacterSpeedAfter(networkSlowEndElapsed - elapsed));
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

                // 팀장점수 진행 중 종료였으면 → 팀장 선택 재오픈 대신 점수 재개 (최우선).
                // 재추첨 시 SaveProject 가 발생하므로 RandomEvent 스케줄/펜딩 복원이 끝난 뒤(RestoreIfNeeded)
                // 실행해야 빈 스케줄로 덮어쓰지 않음 → 여기선 플래그만 세우고 지연 (PendingInvestmentUIRestore 와 동일 패턴)
                if (IsLeaderScoreResumeActive)
                {
                    PendingLeaderScoreResumeRestore = true;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                }
                // 저장 시점에 투자 이벤트 UI가 표시 중이었으면 복원은 RestoreIfNeeded에서 처리
                else if (pendingInvestmentUI)
                {
                    PendingInvestmentUIRestore = true;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                }
                // 저장 시점에 팀장 점수가 미뤄진 상태였으면 LeaderSelectUI부터 표시
                else if (pendingLeaderSelect)
                {
                    IsPendingLeaderSelect = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    // onComplete=null — 개발 재개는 팀장점수 확정 후 ContinueAfterLeaderScore.StartDeveloping()이 처리.
                    DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Planner, null);
                }
                else if (pendingLeaderScore75)
                {
                    _pendingLeaderScore75 = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Artist, null);
                }
                else if (pendingLeaderScore25)
                {
                    _pendingLeaderScore25 = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    DispatchPanelUI.Instance.OpenForLeaderSelect(LeaderType.Programmer, null);
                }
                else if (pendingCreativityGame)
                {
                    _pendingCreativityGame = false;
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    ShowCreativityGame();
                }
                else
                {
                    _isRunning = true;
                    GameTimeManager.Instance.ForceStartTime();
                    StartCoroutine(DevelopmentCoroutine());
                }
                break;

            case ProjectStage.BugFixing:
                _bugFixReleased = false;
                if (pendingDebuggingAlert)
                {
                    GameTimeManager.Instance.StopTime(); // GameSceneInitializer.StartTime() 상쇄
                    AlertUI.Instance.Show("디버깅 작업을 시작합니다.", () =>
                    {
                        _pendingDebuggingAlert = false;
                        ProjectSaveManager.Instance.SaveProject();
                        _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
                    });
                }
                else
                {
                    _isRunning = true;
                    GameTimeManager.Instance.ForceStartTime();
                    _bugFixCoroutine = StartCoroutine(BugFixCoroutine());
                }
                break;
        }

        Debug.Log($"프로젝트 복원 완료: {stage} / elapsed: {elapsed:F1}");
    }

    public float GetNetworkSlowEndElapsed() => _characterSlowEndElapsed;
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
        return Mathf.Clamp01(actual + offset);
    }

    // 강화도(성수, 0~25) → 팀장 단계(1~4) 확률 추첨. 인접 두 단계 사이에서만 분포.
    int RollLeaderStage(int enhanceLevel)
    {
        int lv = Mathf.Clamp(enhanceLevel, 0, 25);
        float r = UnityEngine.Random.value * 100f;
        switch (lv)
        {
            case 9:  return r < 90f ? 1 : 2;
            case 10: return r < 80f ? 1 : 2;
            case 11: return r < 20f ? 1 : 2;
            case 12: return r < 10f ? 1 : 2;
            case 13: return r < 90f ? 2 : 3;
            case 14: return r < 80f ? 2 : 3;
            case 15: return r < 20f ? 2 : 3;
            case 16: return r < 10f ? 2 : 3;
            case 18: return r < 90f ? 3 : 4;
            case 19: return r < 80f ? 3 : 4;
            case 20: return r < 20f ? 3 : 4;
            case 21: return r < 10f ? 3 : 4;
        }
        if (lv <= 8)  return 1;
        if (lv == 17) return 3;
        return 4; // 22~25
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
        int empCreativity = empData?.EffectiveCreativitySkill ?? 0;
        _tickOrderMap[empId] = BuildTickOrder(tickCount, empOvertime, empCreativity, rng);
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

    float CalcBug(int creativity)
    {
        double val = 76.5 / System.Math.Pow(1 + creativity, 0.76);
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
        // 개발 틱 산출 = Effective 주스탯(만족도 배율 + 버프/디버프 스택 + 사내연애 + 오타쿠 포함)
        // → 슬롯/카드에 표시되는 Effective 능력치와 실제 개발 산출이 일치
        int skill = employee.GetEffectiveMainStat();

        // 성공 기준점수 S = 1.2982 + 0.040680 × 주스탯^0.9076  (잭팟 = 3×S×R, 성공 = S×R)
        float S = 1.2982f + 0.040680f * Mathf.Pow(Mathf.Max(0, skill), 0.9076f);

        float planning = 0f, develop = 0f, art = 0f, bug = 0f, creativity = 0f;

        switch (tickType)
        {
            case 0: // 잭팟 — 3 × S × Random(0.5~1.5)
                float jackpot = Mathf.Max(1, Mathf.RoundToInt(3f * S * UnityEngine.Random.Range(0.5f, 1.5f)));
                {
                    Color jackpotColor = new Color(1f, 0.85f, 0f);
                    switch (employee.role)
                    {
                        case EmployeeRole.Planner:
                            planning = jackpot;
                            OfficeManager.Instance?.ShowStatTickPopup(employee.id, "planning", (int)planning, jackpotColor, true);
                            break;
                        case EmployeeRole.Programmer:
                            develop = jackpot;
                            OfficeManager.Instance?.ShowStatTickPopup(employee.id, "develop", (int)develop, jackpotColor, true);
                            break;
                        case EmployeeRole.Artist:
                            art = jackpot;
                            OfficeManager.Instance?.ShowStatTickPopup(employee.id, "art", (int)art, jackpotColor, true);
                            break;
                    }
                }
                break;

            case 1: // 성공 — S × Random(0.8~1.2)
                float success = Mathf.Max(0, Mathf.RoundToInt(S * UnityEngine.Random.Range(0.8f, 1.2f)));
                switch (employee.role)
                {
                    case EmployeeRole.Planner:
                        planning = success;
                        if (planning > 0f) OfficeManager.Instance?.ShowStatTickPopup(employee.id, "planning", (int)planning, new Color(0.4f, 0.6f, 1f), false);
                        break;
                    case EmployeeRole.Programmer:
                        develop = success;
                        if (develop > 0f) OfficeManager.Instance?.ShowStatTickPopup(employee.id, "develop", (int)develop, new Color(0.4f, 1f, 0.5f), false);
                        break;
                    case EmployeeRole.Artist:
                        art = success;
                        if (art > 0f) OfficeManager.Instance?.ShowStatTickPopup(employee.id, "art", (int)art, new Color(1f, 0.7f, 0.3f), false);
                        break;
                }
                break;

            case 2: // 창의성
            {
                var earnedBlock = CreativityGameData.DrawRandomBlock();
                CreativityGameUI.Instance?.AddEarnedBlock(earnedBlock);
                OfficeManager.Instance?.ShowBlockPopup(employee.id, earnedBlock.cells, earnedBlock.color);
                // 기여도: 블록 획득 = 창의성 5점으로 간주
                AddEmployeeContribution(employee.id, 5f);
                break;
            }

            case 3: // 버그 — 규모별 RANDBETWEEN (소규모 1~4 / 중형 2~6 / 대작 4~8)
                int bugRaw = ProjectSetupUI.SelectedScale switch
                {
                    ProjectScale.Small  => UnityEngine.Random.Range(1, 5), // 1~4
                    ProjectScale.Medium => UnityEngine.Random.Range(2, 7), // 2~6
                    ProjectScale.Large  => UnityEngine.Random.Range(4, 9), // 4~8
                    _ => 1
                };
                bug = Mathf.Max(1, bugRaw);
                OfficeManager.Instance?.ShowStatTickPopup(employee.id, "bug", (int)bug, new Color(1f, 0.3f, 0.3f), false);
                break;

            case 4: // 꽝 — 스프라이트만 표시 (숫자/파티클 없음)
                OfficeManager.Instance?.ShowStatTickPopup(employee.id, "blank", 0, new Color(0.6f, 0.6f, 0.6f), false);
                break;
        }

        DevelopmentPanelUI.Instance.AddValues(planning, develop, art, bug, creativity);
        // 기여도: 양수 개발 산출(기획/개발/아트)을 직원별 누적 (버그/창의성 제외)
        AddEmployeeContribution(employee.id, planning + develop + art);
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
        _pendingLeaderScore25      = false;
        _pendingLeaderScore75      = false;
        IsPendingLeaderSelect      = false;
        ClearLeaderScoreResume();
        PendingInvestmentUIRestore = false;
        PendingLeaderScoreResumeRestore = false;
        _pendingDevelopmentComplete = false;
        _pendingCreativityGame = false;
        _pendingDebuggingAlert = false;
        _leaderDevelopBonusTotal  = 0f;
        _leaderPlanningBonusTotal = 0f;
        _leaderArtBonusTotal      = 0f;
        BugPenalty = 0f;
        BugEventBonus = 0f;
        _patrolStarted = false;
        _progressVisualOffset = 0f;
        _progressOffsetElapsedAtEvent = 0f;
        _progressOffsetExtension = 0f;
        _characterSlowEndElapsed = 0f;
        if (_characterSlowCoroutine != null) { StopCoroutine(_characterSlowCoroutine); _characterSlowCoroutine = null; }
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
        _employeeContribution.Clear();
        _contributionInfo.Clear();

        if (_bugFixCoroutine != null)
        {
            StopCoroutine(_bugFixCoroutine);
            _bugFixCoroutine = null;
        }

        DevelopmentPanelUI.Instance.ResetValues();
        DevelopmentTimerUI.Instance.ResetTimer();
        RandomEventManager.Instance.Reset();
        CreativityGameUI.Instance?.ClearEarnedBlocks();

        ProjectSetupUI.SelectedScale = default;
        ProjectSetupUI.SelectedGenre = default;
        ProjectSetupUI.SelectedPlatform = default;

        IsVoluntaryOvertimeActive = false;
        IsOvertimeMode            = false;
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
            emp.isOvertimeWorker = false;
        // ForceStartTime 은 stopCount 를 0으로 강제해 다른 모달(직원리스트 등)의 StopTime 등록까지
        // 무력화해버린다 — 이 시점(판매 완료 → OnSalesComplete → ResetProject)에 다른 모달이 떠서
        // 시간을 붙잡고 있는 중이면 손대지 않고, 그 모달이 닫힐 때 자기 StartTime()으로 자연 재개되게 둔다.
        if (!ModalGate.I.IsBlocked)
            GameTimeManager.Instance.ForceStartTime();
        Debug.Log("프로젝트 초기화 완료");
    }
    // extensionSeconds: 개발 기간 연장량. slowdownSeconds: 캐릭터 감속 지속 시간(미지정 시 연장량과 동일 = 네트워크 이벤트).
    // 게으른 천재는 연장 2주 / 감속 4주 처럼 분리해서 호출.
    public void ExtendDevelopmentDuration(float extensionSeconds, float slowdownSeconds = -1f)
    {
        if (slowdownSeconds < 0f) slowdownSeconds = extensionSeconds;

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

        // 캐릭터 속도 50% 감소 → slowdownSeconds 후 복귀 (연장과 분리 가능)
        _characterSlowEndElapsed = _elapsed + slowdownSeconds;
        OfficeManager.Instance?.SetCharacterSpeedMultiplier(0.5f);
        if (_characterSlowCoroutine != null) StopCoroutine(_characterSlowCoroutine);
        _characterSlowCoroutine = StartCoroutine(RestoreCharacterSpeedAfter(slowdownSeconds));
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
    public void BeginDevelopmentCoroutine()
    {
        _isRunning = true;
        StartCoroutine(DevelopmentCoroutine());
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
            StartCoroutine(OpenLeaderSelectAfterPopups(LeaderType.Artist));
            return;
        }

        if (_pendingLeaderScore25)
        {
            _pendingLeaderScore25 = false;
            StartCoroutine(OpenLeaderSelectAfterPopups(LeaderType.Programmer));
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
            RandomEventManager.Instance.InvestmentThreshold
        );
    }
}