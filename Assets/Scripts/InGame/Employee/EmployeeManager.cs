using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using BackEnd.Content;
using LitJson;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    public List<EmployeeData> ownedEmployees = new();

    // CEO — ownedEmployees 에는 안 넣음. CEOManager 가 메타 상태(강화 단계 등) 유지하고
    // 인게임 진입 시 ApplyCEOFromManager() 호출로 메모리에 EmployeeData 인스턴스 보관.
    // 팀장 후보로만 노출 (LeaderSelectUI). 만족도/아이템/이벤트/연봉/카운트/퀘스트 자동 제외.
    public EmployeeData CEO { get; private set; }

    // 런 식별자. ResetForNewRun 마다 ++. HireEmployee 비동기 Insert 콜백이 reset 이후 늦게 도착해
    // ownedEmployees 에 stale 직원을 박는 race 를 차단.
    private int _runEpoch = 0;

    public int GetTotalSalary()
    {
        int total = 0;
        foreach (var e in ownedEmployees) total += e.salary;
        return total;
    }
    public List<EmployeeData> poolEmployees = new();
    private readonly HashSet<string> _acquiredEmployeeIds = new();

    private bool _satisfactionDroppedThisCycle = false;

    public int YearlyExitCount { get; private set; } = 0;
    public int ExitCountYear   { get; private set; } = 0;

    // 채용 면접 대기 (HiringUI 가 등록, 매주 카운트다운 → 0 시 리스트 공개). UserGameTime 에 영속.
    public int HiringPendingTier  { get; private set; } = -1; // -1 = 진행 중 없음
    public int HiringPendingWeeks { get; private set; } = 0;
    public void SetHiringPending(int tier, int weeks)  { HiringPendingTier = tier; HiringPendingWeeks = weeks; }
    public void LoadHiringPending(int tier, int weeks) { HiringPendingTier = tier; HiringPendingWeeks = weeks; }

    // 공개 시점에 확정된 후보 리스트(초기 A / 리롤 B) — 재접속해도 동일 유지(악용 방지). 채용/끝내기 때만 해제.
    public string HiringListAJson { get; private set; } = "";
    public string HiringListBJson { get; private set; } = "";
    public bool HasHiringLists => !string.IsNullOrEmpty(HiringListAJson);

    [System.Serializable]
    class CandidateListDTO { public List<EmployeeData> items = new(); }

    public void SetHiringLists(List<EmployeeData> a, List<EmployeeData> b)
    {
        HiringListAJson = JsonUtility.ToJson(new CandidateListDTO { items = a ?? new() });
        HiringListBJson = JsonUtility.ToJson(new CandidateListDTO { items = b ?? new() });
    }

    public void LoadHiringLists(string aJson, string bJson)
    {
        HiringListAJson = aJson ?? "";
        HiringListBJson = bJson ?? "";
    }

    public List<EmployeeData> GetHiringListA() => DeserializeCandidates(HiringListAJson);
    public List<EmployeeData> GetHiringListB() => DeserializeCandidates(HiringListBJson);

    static List<EmployeeData> DeserializeCandidates(string json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        var dto = JsonUtility.FromJson<CandidateListDTO>(json);
        return dto?.items ?? new();
    }

    // 채용 완료/끝내기/취소 시 — pending + 확정 리스트 모두 해제.
    public void ClearHiring()
    {
        HiringPendingTier  = -1;
        HiringPendingWeeks = 0;
        HiringListAJson    = "";
        HiringListBJson    = "";
    }

    public void RecordEmployeeExit()
    {
        int currentYear = GameTimeManager.Instance?.Year ?? 0;
        if (ExitCountYear != currentYear)
        {
            YearlyExitCount = 0;
            ExitCountYear   = currentYear;
        }
        YearlyExitCount++;
        Debug.Log($"[EmployeeManager] 올해({currentYear}) 퇴직자: {YearlyExitCount}명");
    }

    public void ResetYearlyExitCount()
    {
        YearlyExitCount = 0;
        ExitCountYear   = GameTimeManager.Instance?.Year ?? 0;
    }

    public void LoadExitStats(int count, int year)
    {
        YearlyExitCount = count;
        ExitCountYear   = year;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    // ── 초기 로드 ─────────────────────────────
    public void LoadAllData(System.Action onComplete = null)
    {
        LoadMasterEmployees(() => LoadAcquiredEmployees(() => LoadEmployees(onComplete)));
    }

    // ── 마스터 직원 풀 로드 (뒤끝 차트) ──────
    public void LoadMasterEmployees(System.Action onComplete = null)
    {
        poolEmployees.Clear();

        // 1) 차트 테이블 목록 조회
        BackendContentTableReturnObject tableResult = Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogError($"마스터 직원 차트 테이블 조회 실패: {tableResult}");
            onComplete?.Invoke();
            return;
        }

        List<ContentTableItem> tableList = tableResult.GetContentTableItemList();

        // 2) chartName으로 chartId 탐색
        string targetChartId = null;
        foreach (ContentTableItem item in tableList)
        {
            if (item.chartName == "EmployeeMasterData")
            {
                targetChartId = item.chartId;
                break;
            }
        }

        if (targetChartId == null)
        {
            Debug.LogError("마스터 직원 차트를 찾을 수 없음: EmployeeMasterData");
            onComplete?.Invoke();
            return;
        }

        // 3) 차트 내용 로드
        BackendContentReturnObject contentResult = Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess())
        {
            Debug.LogError($"마스터 직원 차트 내용 로드 실패: {contentResult}");
            onComplete?.Invoke();
            return;
        }

        // 4) chartId로 해당 차트 꺼내기
        Dictionary<string, ContentItem> dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(targetChartId))
        {
            Debug.LogError("마스터 직원 차트 데이터 없음");
            onComplete?.Invoke();
            return;
        }

        LitJson.JsonData rows = dic[targetChartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            LitJson.JsonData row = rows[i];
            try
            {
                var data = new EmployeeData(
                    id:            row["employeeId"].ToString(),
                    name:          row["employeeName"].ToString(),
                    role:          (EmployeeRole)int.Parse(row["role"].ToString()),
                    developMin:    int.Parse(row["developMin"].ToString()),
                    developMax:    int.Parse(row["developMax"].ToString()),
                    planningMin:   int.Parse(row["planningMin"].ToString()),
                    planningMax:   int.Parse(row["planningMax"].ToString()),
                    artMin:        int.Parse(row["artMin"].ToString()),
                    artMax:        int.Parse(row["artMax"].ToString()),
                    creativityMin: int.Parse(row["creativityMin"].ToString()),
                    creativityMax: int.Parse(row["creativityMax"].ToString()),
                    salaryMin:     int.Parse(row["salaryMin"].ToString()),
                    salaryMax:     int.Parse(row["salaryMax"].ToString()),
                    maxGrade:      (EmployeeGrade)int.Parse(row["maxGrade"].ToString())
                );
                data.portraitId = row["portraitId"].ToString();
                data.isDefault  = row["isDefault"].ToString() == "1";
                var femaleVal   = row.ContainsKey("isFemale") ? row["isFemale"].ToString() : "0";
                data.isFemale   = femaleVal == "1" || femaleVal.ToLower() == "true";
                // 캐릭터별 등급 특징 (차트에 컬럼 없으면 빈 문자열 — 뒤끝 차트 미반영 환경 대비)
                data.epicTraitId     = row.ContainsKey("epicTraitId")     ? row["epicTraitId"].ToString()     : "";
                data.uniqueEventType = row.ContainsKey("uniqueEventType") ? row["uniqueEventType"].ToString() : "";
                data.introduction    = row.ContainsKey("introduction")    ? row["introduction"].ToString()    : "";
                poolEmployees.Add(data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"직원 데이터 파싱 실패 (row {i}): {e.Message}");
            }
        }

        Debug.Log($"마스터 직원 {poolEmployees.Count}명 로드 완료");
        onComplete?.Invoke();
    }

    // ── 획득 직원 로드 ────────────────────────
    public void LoadAcquiredEmployees(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("AcquiredEmployee", bro =>
        {
            _acquiredEmployeeIds.Clear();
            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                foreach (LitJson.JsonData row in rows)
                {
                    try
                    {
                        string empId = row["employeeId"].ToString();
                        if (!string.IsNullOrEmpty(empId))
                            _acquiredEmployeeIds.Add(empId);
                    }
                    catch { }
                }
            }
            Debug.Log($"획득 직원 {_acquiredEmployeeIds.Count}개 로드");
            onComplete?.Invoke();
        });
    }

    // ── 직원 획득 (아웃게임 등에서 호출) ──────
    public void AcquireEmployee(string masterEmployeeId)
    {
        if (_acquiredEmployeeIds.Contains(masterEmployeeId))
        {
            Debug.Log($"이미 획득한 직원: {masterEmployeeId}");
            return;
        }

        _acquiredEmployeeIds.Add(masterEmployeeId);

        var param = new Param();
        param.Add("employeeId", masterEmployeeId);
        Backend.GameData.Insert("AcquiredEmployee", param, bro =>
        {
            if (bro.IsSuccess())
                Debug.Log($"직원 획득 저장: {masterEmployeeId}");
            else
                Debug.LogError($"직원 획득 저장 실패: {bro}");
        });
    }

    public bool IsAcquired(string masterEmployeeId) =>
        _acquiredEmployeeIds.Contains(masterEmployeeId);

    // ── 채용 후보 랜덤 추출 ───────────────────
    public void LoadRandomCandidates(int count, int tierIndex, System.Action<List<EmployeeData>> onComplete)
    {
        // isDefault이거나 유저가 획득한 직원만 채용 풀로 사용
        var availablePool = poolEmployees.FindAll(e => e.isDefault || _acquiredEmployeeIds.Contains(e.id));

        if (availablePool.Count == 0)
        {
            Debug.LogError("채용 가능한 직원 없음");
            return;
        }

        // 복원 허용: count만큼 랜덤 추출 (같은 직원이 다른 등급으로 중복 등장 가능)
        var candidates = new List<EmployeeData>();
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, availablePool.Count);
            candidates.Add(availablePool[idx].Clone());
        }

        foreach (var employee in candidates)
        {
            // Grade / Potential 결정 — 등급 상한은 OutGameEmployeeManager의 도달 maxGrade 기준
            var maxGrade = OutGameEmployeeManager.Instance != null
                ? OutGameEmployeeManager.Instance.GetMaxGrade(employee.id)
                : EmployeeGrade.Normal;
            employee.grade = RollGrade(maxGrade);
            employee.potential = RollPotential(employee.grade, tierIndex);

            // 능력치는 마스터 min~max 범위로 재랜덤
            employee.developSkill = UnityEngine.Random.Range(employee.developMin, employee.developMax + 1);
            employee.planningSkill = UnityEngine.Random.Range(employee.planningMin, employee.planningMax + 1);
            employee.artSkill = UnityEngine.Random.Range(employee.artMin, employee.artMax + 1);
            employee.creativitySkill = UnityEngine.Random.Range(employee.creativityMin, employee.creativityMax + 1);

            // 연봉 재랜덤 — 금수저 특성(Epic+)이면 기본 연봉 50% 감소 적용
            int steps = (employee.salaryMax - employee.salaryMin) / 50;
            int rolledSalary = employee.salaryMin + (UnityEngine.Random.Range(0, steps + 1) * 50);
            employee.salary = CharacterTraitApplier.ApplyGoldspoonSalary(employee, rolledSalary);
        }

        // 최종 등급이 Rare 이상이면 주스탯(역할 일치 스탯) 실제 수치에도 인터벌 보너스 반영
        // (EmployeeData.GradeIntervalBonus·*DisplayText()의 표시 인터벌과 일치시킴). 등급 강제 보정 이후에 적용.
        foreach (var employee in candidates)
        {
            if (employee.grade < EmployeeGrade.Rare) continue;
            int bonus = employee.GradeIntervalBonus;
            switch (employee.role)
            {
                case EmployeeRole.Programmer: employee.developSkill  += bonus; break;
                case EmployeeRole.Planner:    employee.planningSkill += bonus; break;
                case EmployeeRole.Artist:     employee.artSkill      += bonus; break;
            }
        }

        onComplete?.Invoke(candidates);
    }

    // 튜토리얼 전용(1단계 첫 채용, OnboardingState.Tutorial3Done 이 false 인 동안) — 후보 3명을 무조건
    // 금수저(Rare/A) → 훈수쟁이(Normal/B) → 천재(Normal/C) 순서로 고정. RollGrade/RollPotential 미사용,
    // 능력치도 랜덤이 아니라 마스터 인터벌의 최대치로 고정 표시.
    class TutorialCandidateSpec
    {
        public string id;
        public EmployeeGrade grade;
        public EmployeePotential potential;
        public TutorialCandidateSpec(string id, EmployeeGrade grade, EmployeePotential potential)
        {
            this.id = id; this.grade = grade; this.potential = potential;
        }
    }

    static readonly List<TutorialCandidateSpec> TutorialFixedCandidateSpecs = new()
    {
        new TutorialCandidateSpec("goldspoon_01", EmployeeGrade.Rare,   EmployeePotential.A),
        new TutorialCandidateSpec("hunsu_01",     EmployeeGrade.Normal, EmployeePotential.B),
        new TutorialCandidateSpec("genius_01",    EmployeeGrade.Normal, EmployeePotential.C),
    };

    public void LoadTutorialFixedCandidates(System.Action<List<EmployeeData>> onComplete)
    {
        var candidates = new List<EmployeeData>();
        foreach (var spec in TutorialFixedCandidateSpecs)
        {
            var master = poolEmployees.Find(e => e.id == spec.id);
            if (master == null)
            {
                Debug.LogError($"[EmployeeManager] 튜토리얼 고정 채용 후보 마스터 데이터 없음: {spec.id}");
                continue;
            }

            var employee = master.Clone();
            employee.grade = spec.grade;
            employee.potential = spec.potential;

            employee.developSkill    = employee.developMax;
            employee.planningSkill   = employee.planningMax;
            employee.artSkill        = employee.artMax;
            employee.creativitySkill = employee.creativityMax;
            employee.salary = CharacterTraitApplier.ApplyGoldspoonSalary(employee, employee.salaryMax);

            candidates.Add(employee);
        }

        // 일반 롤과 동일하게 Rare 이상은 주스탯에 등급 인터벌 보너스 반영
        foreach (var employee in candidates)
        {
            if (employee.grade < EmployeeGrade.Rare) continue;
            int bonus = employee.GradeIntervalBonus;
            switch (employee.role)
            {
                case EmployeeRole.Programmer: employee.developSkill  += bonus; break;
                case EmployeeRole.Planner:    employee.planningSkill += bonus; break;
                case EmployeeRole.Artist:     employee.artSkill      += bonus; break;
            }
        }

        onComplete?.Invoke(candidates);
    }

    List<EmployeeData> ShuffleList(List<EmployeeData> list)
    {
        var result = new List<EmployeeData>(list);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (result[i], result[rand]) = (result[rand], result[i]);
        }
        return result;
    }

    // ── 채용 확정 ─────────────────────────────
    public void HireEmployee(EmployeeData poolEmployee)
    {
        var inGameEmployee = new EmployeeData(
            id: System.Guid.NewGuid().ToString(),
            name: poolEmployee.employeeName,
            role: poolEmployee.role,
            developMin: poolEmployee.developMin,
            developMax: poolEmployee.developMax,
            planningMin: poolEmployee.planningMin,
            planningMax: poolEmployee.planningMax,
            artMin: poolEmployee.artMin,
            artMax: poolEmployee.artMax,
            creativityMin: poolEmployee.creativityMin,
            creativityMax: poolEmployee.creativityMax,
            salaryMin: poolEmployee.salaryMin,
            salaryMax: poolEmployee.salaryMax,
            maxGrade: poolEmployee.maxGrade
        );

        // 채용 화면에서 확정된 수치 그대로 복사
        inGameEmployee.grade = poolEmployee.grade;
        inGameEmployee.potential = poolEmployee.potential;
        inGameEmployee.developSkill = poolEmployee.developSkill;
        inGameEmployee.planningSkill = poolEmployee.planningSkill;
        inGameEmployee.artSkill = poolEmployee.artSkill;
        inGameEmployee.creativitySkill = poolEmployee.creativitySkill;
        inGameEmployee.salary = poolEmployee.salary;
        inGameEmployee.enhancementLevel = poolEmployee.enhancementLevel;
        inGameEmployee.portraitId       = poolEmployee.portraitId;
        inGameEmployee.masterEmployeeId = poolEmployee.id;
        inGameEmployee.assignedProjectId = "";
        inGameEmployee.satisfaction = 80;
        inGameEmployee.hiredYear = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : 0;
        inGameEmployee.isFemale  = poolEmployee.isFemale;
        inGameEmployee.epicTraitId     = poolEmployee.epicTraitId;
        inGameEmployee.uniqueEventType = poolEmployee.uniqueEventType;
        inGameEmployee.introduction    = poolEmployee.introduction;
        
        int epochAtCall = _runEpoch;
        Backend.GameData.Insert("Employee", inGameEmployee.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"채용 저장 실패: {bro}");
                return;
            }

            string newRowInDate = bro.GetInDate();

            // 콜백 도착 시점이 이미 새 런이면 stale — 서버 row 즉시 삭제 후 메모리 변경 없음.
            if (epochAtCall != _runEpoch)
            {
                Debug.LogWarning($"[Stale] HireEmployee 콜백 무시 (런 변경됨) - 서버 row {newRowInDate} 삭제");
                Backend.GameData.DeleteV2("Employee", newRowInDate, Backend.UserInDate, _ => { });
                return;
            }

            inGameEmployee.rowInDate = newRowInDate;
            ownedEmployees.Add(inGameEmployee);

            // 캐릭터 특성 채용 hook (오타쿠 선호 장르 고정 등 — 현재 골격 스텁)
            CharacterTraitApplier.OnHire(inGameEmployee);

            // 테크트리 '신입 환영회(sat_welcome)' — 신규 입사 시 기존 직원 랜덤 1명 만족도 +20
            if (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("sat_welcome"))
            {
                var welcomeTargets = ownedEmployees.FindAll(e => e != inGameEmployee);
                if (welcomeTargets.Count > 0)
                    welcomeTargets[UnityEngine.Random.Range(0, welcomeTargets.Count)].ChangeSatisfaction(+20);
            }

            HUDUI.Instance.RefreshAll();
            RandomEventManager.Instance?.CheckOfficeRomanceOnHire(inGameEmployee);

            QuestManager.Instance.UpdateProgress(QuestType.HireEmployee, 1);
            OfficeManager.Instance?.OnEmployeeHired(inGameEmployee);
            if (DevelopmentManager.Instance.IsStarted)
                DevelopmentManager.Instance.OnEmployeeHired(inGameEmployee);

            MoneyManager.Instance?.SaveMoney();
            GameTimeManager.Instance?.SaveGameTime();
            ProjectSaveManager.Instance?.SaveProject();
            Debug.Log($"채용 완료: {inGameEmployee.employeeName} ({inGameEmployee.grade} / {inGameEmployee.potential})");
        });
    }

    // ── Grade / Potential 헬퍼 ────────────────
    private EmployeeGrade RollGrade(EmployeeGrade maxGrade)
    {
        int rolled = UnityEngine.Random.Range(0, (int)maxGrade + 1);
        return (EmployeeGrade)rolled;
    }

    // [tierIndex][gradeIndex] → C, B, A, S 가중치
    // gradeIndex: Normal=0, Rare=1, Epic=2, Unique=3
    private static readonly int[][][] PotentialWeightTable =
    {
        // 1단계
        new int[][]
        {
            new[] { 60, 40,  0,  0 },  // Normal
            new[] { 50, 30, 20,  0 },  // Rare
            new[] { 40, 30, 30,  0 },  // Epic
            new[] { 40, 30, 30,  0 },  // Unique
        },
        // 2단계
        new int[][]
        {
            new[] { 50, 30, 20,  0 },  // Normal
            new[] { 40, 40, 20,  0 },  // Rare
            new[] { 30, 40, 30,  0 },  // Epic
            new[] { 30, 40, 30, 10 },  // Unique
        },
        // 3단계
        new int[][]
        {
            new[] { 40, 30, 30,  0 },  // Normal
            new[] { 30, 40, 30,  0 },  // Rare
            new[] { 20, 50, 30, 10 },  // Epic
            new[] { 20, 40, 40, 20 },  // Unique
        },
    };

    private EmployeePotential RollPotential(EmployeeGrade grade, int tierIndex)
    {
        int ti = Mathf.Clamp(tierIndex, 0, PotentialWeightTable.Length - 1);
        int[] weights = PotentialWeightTable[ti][(int)grade];

        int total = 0;
        foreach (int w in weights) total += w;

        int roll = UnityEngine.Random.Range(0, total);
        int cum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cum += weights[i];
            if (roll < cum) return (EmployeePotential)i;
        }
        return EmployeePotential.C;
    }

    // ── 보유 직원 불러오기 ────────────────────
    public void LoadEmployees(System.Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("Employee", bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"직원 불러오기 실패: {bro}");
                onComplete?.Invoke();
                return;
            }

            ownedEmployees.Clear();
            var rows = bro.FlattenRows();

            foreach (var row in rows)
            {
                var emp = EmployeeData.FromServerRow((LitJson.JsonData)row);

                // isFemale은 마스터 데이터 기준으로 덮어씌움 (기존 레코드 자동 마이그레이션)
                var master = poolEmployees.Find(p => p.id == emp.masterEmployeeId);
                if (master != null) emp.isFemale = master.isFemale;

                ownedEmployees.Add(emp);
            }

            Debug.Log($"보유 직원 {ownedEmployees.Count}명 로드 완료");
            onComplete?.Invoke();
            HUDUI.Instance?.RefreshAll();
        });
    }

    // 새 런 시작 — 인게임 채용 직원 전부 wipe (Employee 테이블 row 삭제)
    // 메타 보존: _acquiredEmployeeIds(AcquiredEmployee 테이블) 와 poolEmployees(차트)는 유지
    public void ResetForNewRun(System.Action onComplete = null)
    {
        _runEpoch++;

        // 외부 캐싱된 EmployeeData 참조의 rowInDate 무효화 (UpdateEmployee 404 방지)
        foreach (var e in ownedEmployees)
            e.rowInDate = null;

        ownedEmployees.Clear();
        YearlyExitCount = 0;
        ExitCountYear   = 0;
        HiringPendingTier  = -1;
        HiringPendingWeeks = 0;
        HiringListAJson    = "";
        HiringListBJson    = "";
        _satisfactionDroppedThisCycle = false;

        // 메모리 List 대신 서버에서 직접 fetch → 모든 row 삭제. inFlight Insert 가 늦게 도착해도 epoch 가드가 자체 정리.
        Backend.GameData.GetMyData("Employee", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.Log($"[Reset] Employee fetch 실패/빈 - 삭제 스킵: {bro}");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var ids = new List<string>();
            foreach (var row in rows)
            {
                string inDate = TryGetInDate((JsonData)row);
                if (!string.IsNullOrEmpty(inDate)) ids.Add(inDate);
            }

            if (ids.Count == 0) { onComplete?.Invoke(); return; }

            int pending = ids.Count;
            foreach (var inDate in ids)
            {
                Backend.GameData.DeleteV2("Employee", inDate, Backend.UserInDate, broDel =>
                {
                    if (!broDel.IsSuccess()) Debug.LogError($"[Reset] Employee delete 실패: {broDel}");
                    pending--;
                    if (pending == 0) onComplete?.Invoke();
                });
            }
        });
    }

    static string TryGetInDate(JsonData row)
    {
        try { return row["inDate"].ToString(); } catch { return ""; }
    }

    void Start()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged += OnWeekPassed;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnWeekPassed;
    }

    void OnWeekPassed()
    {
        int week = GameTimeManager.Instance.Week;

        // 1주차: 사이클 리셋
        if (week == 1) _satisfactionDroppedThisCycle = false;

        // 2주차: 50% 확률로 하락
        // 3주차: 2주차에 안 떨어졌으면 반드시 하락
        bool shouldDrop = false;
        if (week == 2 && !_satisfactionDroppedThisCycle)
            shouldDrop = UnityEngine.Random.value < 0.5f;
        else if (week == 3 && !_satisfactionDroppedThisCycle)
            shouldDrop = true;

        if (shouldDrop)
        {
            _satisfactionDroppedThisCycle = true;
            if (DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsVoluntaryOvertimeActive) return;
            bool globalOvertime = DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsOvertimeMode;

            // 테크트리 '멘탈 케어 서비스(sat_mental)' — 주기 만족도 하락량 1 감소
            int mentalCare = (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("sat_mental")) ? 1 : 0;
            // 특성 's5'(monthlySatDropReduce) — 주기 만족도 하락량 추가 감소 (sat_mental 과 합산)
            int traitDropReduce = TraitEffectApplier.GetMonthlySatDropReduce();

            foreach (var emp in ownedEmployees)
            {
                if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id)) continue; // 파견중 제외
                int dropAmount = (globalOvertime || emp.isOvertimeWorker) ? 10 : 5;
                dropAmount = Mathf.Max(0, dropAmount - mentalCare - traitDropReduce);
                emp.satisfaction = Mathf.Clamp(emp.satisfaction - dropAmount, 0, 100);
            }
        }

        foreach (var emp in ownedEmployees)
        {
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id)) continue; // 파견중 제외
            // 디버프 스택: 각 entry 1주씩 감소, 만료(≤0) 시 제거
            for (int i = emp.statDebuffStacks.Count - 1; i >= 0; i--)
            {
                emp.statDebuffStacks[i]--;
                if (emp.statDebuffStacks[i] <= 0) emp.statDebuffStacks.RemoveAt(i);
            }
            // 버프 스택: 각 entry 1주씩 감소, 만료(≤0) 시 제거 (struct 라 인덱스 재할당)
            for (int i = emp.statBuffStacks.Count - 1; i >= 0; i--)
            {
                var s = emp.statBuffStacks[i];
                s.weeksLeft--;
                if (s.weeksLeft <= 0) emp.statBuffStacks.RemoveAt(i);
                else emp.statBuffStacks[i] = s;
            }
            if (emp.romanceBuffWeeksLeft > 0)
                emp.romanceBuffWeeksLeft--;

            // 캐릭터 특성 매주 hook (유리멘탈/우주의 기운 등 — 현재 골격 스텁)
            CharacterTraitApplier.WeeklyTick(emp);
        }

        CheckLowSatisfaction();
        RandomEventManager.Instance?.CheckConditionEvents();

        // 채용 면접 대기 카운트다운 — 0 되면 후보 리스트 공개 (HiringUI 가 비활성이어도 동작)
        // 면접 대기 카운트다운 — 0 되면 후보 리스트 공개. pending(tier)은 유지(채용/끝내기 때만 해제),
        // 공개 시 RevealHiring 이 리스트를 1회 확정·저장해 재접속에도 동일 리스트 유지.
        if (HiringPendingTier >= 0 && HiringPendingWeeks > 0)
        {
            HiringPendingWeeks--;
            if (HiringPendingWeeks <= 0)
            {
                HiringPendingWeeks = 0;
                GameTimeManager.Instance?.SaveGameTime(); // weeks=0 영속(tier 유지)
                int tier = HiringPendingTier;
                var hiring = FindObjectOfType<HiringUI>(true); // 비활성 포함
                // 같은 주 퇴사/조건 이벤트(위 CheckLowSatisfaction/CheckConditionEvents 가 먼저 실행)가 모달로 떠 있으면
                // 그 모달이 모두 닫힌 뒤 후보 리스트 공개 — 패널 겹침 + 시간 강제재개 충돌 방지.
                if (hiring != null) ModalGate.I.WhenFree(() => hiring.RevealHiring(tier));
            }
        }
    }

    void CheckLowSatisfaction()
    {
        foreach (var emp in ownedEmployees)
        {
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id)) continue; // 파견중 제외
            // 만족도 40 이상 회복 시 플래그 리셋 — 다음 진입 때 재적용 가능
            if (emp.satisfaction >= 40)
            {
                emp.lowSatisfactionPenaltyApplied = false;
                continue;
            }
            // 이번 진입 사이클에서 이미 페널티 적용됨 — 매주 누적 방지
            if (emp.lowSatisfactionPenaltyApplied) continue;

            // 스탯 ×0.8 (데이터 패널티만 처리, 이벤트 트리거는 RandomEventManager 담당)
            emp.developSkill    = Mathf.Max(1, Mathf.RoundToInt(emp.developSkill    * 0.8f));
            emp.planningSkill   = Mathf.Max(1, Mathf.RoundToInt(emp.planningSkill   * 0.8f));
            emp.artSkill        = Mathf.Max(1, Mathf.RoundToInt(emp.artSkill        * 0.8f));
            emp.creativitySkill = Mathf.Max(1, Mathf.RoundToInt(emp.creativitySkill * 0.8f));
            emp.lowSatisfactionPenaltyApplied = true;
        }
    }

    public void ChangeAllSatisfaction(int delta)
    {
        foreach (var emp in ownedEmployees)
            emp.ChangeSatisfaction(delta);
    }

    public void ReduceAllSatisfaction(int amount)
    {
        foreach (var emp in ownedEmployees)
            emp.satisfaction = Mathf.Clamp(emp.satisfaction - amount, 0, 100);
        SaveAllEmployees();
    }

    public void ReduceAllSatisfactionExcept(int amount, EmployeeData except)
    {
        foreach (var emp in ownedEmployees)
        {
            if (emp == except) continue;
            emp.satisfaction = Mathf.Clamp(emp.satisfaction - amount, 0, 100);
        }
        SaveAllEmployees();
    }

    // 프로젝트 완성 시 호출 — 전 직원 만족도 +40
    public void OnProjectCompleted()
    {
        // 테크트리 '완성의 기쁨(sat_completion)' — 완성 시 만족도 회복량 +5
        int recover = 40;
        if (TechTreeManager.Instance != null && TechTreeManager.Instance.IsUnlocked("sat_completion"))
            recover += 5;

        foreach (var emp in ownedEmployees)
        {
            emp.satisfaction = Mathf.Clamp(emp.satisfaction + recover, 0, 100);
            UpdateEmployee(emp);
        }
    }

    public EmployeeData GetEmployee(string id) =>
        ownedEmployees.Find(e => e.id == id);

    public void SaveAllEmployees()
    {
        foreach (var emp in ownedEmployees)
            UpdateEmployee(emp);
    }

    // 인게임 진입 시 CEOManager 메타 데이터로 CEO 인스턴스 셋업. ownedEmployees 에는 안 넣음.
    // GameSceneInitializer.Start 등에서 호출.
    public void ApplyCEOFromManager()
    {
        if (CEOManager.Instance == null) { CEO = null; return; }
        CEO = CEOManager.Instance.CreateCEOEmployee();
        Debug.Log($"[Employee] CEO 셋업: {CEO.employeeName} P{CEO.planningSkill}/D{CEO.developSkill}/A{CEO.artSkill}");
    }

    public void UpdateEmployee(EmployeeData employee)
    {
        if (employee == null) return;
        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        // 참조 자체가 현재 ownedEmployees 에 없으면 stale. 다른 매니저/코루틴이 옛 EmployeeData 캡처를 들고
        // 호출해도 여기서 차단. ResetForNewRun 후 잔여 콜백이 stale 참조로 update 치는 404 경로를 봉쇄.
        if (!ownedEmployees.Contains(employee))
        {
            Debug.LogWarning($"[Stale] UpdateEmployee 거부 - ownedEmployees 에 없는 참조: {employee.employeeName} (호출자 추적용)");
            employee.rowInDate = null;
            return;
        }

        Backend.GameData.UpdateV2("Employee", employee.rowInDate, Backend.UserInDate, employee.ToParam(), bro =>
        {
            if (bro.IsSuccess()) return;

            Debug.LogError($"직원 업데이트 실패: {bro}");
            // 404 = 서버 row 없음 (Reset 등으로 사라짐). 메모리 stale 참조 정리.
            if (bro.GetStatusCode() == "404")
            {
                Debug.LogWarning($"[Stale] 서버에 row 없음 - 메모리에서 분리: {employee.employeeName}");
                employee.rowInDate = null;
                ownedEmployees.Remove(employee);
                // 우기가 이 stale 정리로 사라졌다면 신의 축복 지속 효과도 즉시 정리 (재채용 부활 방지)
                if (!CharacterUniqueEvents.HasUniqueUgi()) CharacterUniqueEvents.ClearGodBlessing();
            }
        });
    }

    public void FireEmployee(EmployeeData employee, bool countAsExit = true)
    {
        if (employee != null && DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(employee.id))
        {
            AlertUI.Instance?.Show("파견중인 직원은 해고할 수 없습니다.");
            return;
        }
        if (countAsExit) RecordEmployeeExit();
        ownedEmployees.Remove(employee);
        // 신의 축복(우기 전용) 지속 효과(2/3/6)는 grade>=Unique 우기가 있어야만 유지 — 우기 퇴장 시 즉시 해제 +
        // 잔존 버프 정리(재채용 시 부활 방지). 우기가 남아 있으면(다수 보유) 유지. 아래 4-set 저장에 반영됨.
        if (!CharacterUniqueEvents.HasUniqueUgi()) CharacterUniqueEvents.ClearGodBlessing();
        RandomEventManager.Instance?.CheckCoupleOnFire(employee.id);
        RandomEventManager.Instance?.ClearCoupleIfInvolved(employee.id);
        OfficeManager.Instance?.OnEmployeeFired(employee);
        if (DevelopmentManager.Instance.IsStarted)
            DevelopmentManager.Instance.OnEmployeeFired(employee.id);

        // 총 연봉 HUD 갱신 — 자발적 퇴사/도주 이벤트 등 호출자 측 갱신 누락 케이스 일괄 처리.
        HUDUI.Instance?.RefreshAll();
        // 직원 리스트 패널이 열려있으면 자동 재빌드 — 커플 동반퇴사, 사직, 도주 이벤트로 외부 해고 시 슬롯 잔존 방지.
        EmployeeListUI.Instance?.RefreshIfOpen();

        if (string.IsNullOrEmpty(employee.rowInDate))
        {
            Debug.LogWarning($"rowInDate 없음, 서버 삭제 스킵: {employee.employeeName}");
            return;
        }

        Backend.GameData.DeleteV2("Employee", employee.rowInDate, Backend.UserInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                MoneyManager.Instance?.SaveMoney();
                GameTimeManager.Instance?.SaveGameTime();
                ProjectSaveManager.Instance?.SaveProject();
                Debug.Log($"해고 완료: {employee.employeeName}");
            }
            else
                Debug.LogError($"해고 저장 실패: {bro}");
        });
    }
    // ── 강화 적용 ─────────────────────────────
    // 강화 단계별 연봉 증가량 [강화 단계(0→1 ~ 24→25)]
    private static readonly int[] EnhanceSalaryTable =
    {
              0,  // 0→1
              0,  // 1→2
              0,  // 2→3
              0,  // 3→4
              0,  // 4→5
              0,  // 5→6
              0,  // 6→7
              0,  // 7→8
              0,  // 8→9
              0,  // 9→10
          1_500,  // 10→11
          3_000,  // 11→12
          4_500,  // 12→13
          6_000,  // 13→14
         15_000,  // 14→15
         20_000,  // 15→16
         25_000,  // 16→17
         30_000,  // 17→18
         35_000,  // 18→19
        100_000,  // 19→20
        130_000,  // 20→21
        160_000,  // 21→22
        200_000,  // 22→23
        250_000,  // 23→24
        300_000,  // 24→25
    };

    // 주스탯 증가량 테이블 [강화 단계(0→1 ~ 24→25)] = (min, max)
    private static readonly (int min, int max)[] MainStatGainTable =
    {
        ( 20,  20),  // 0→1
        ( 20,  20),  // 1→2
        ( 25,  25),  // 2→3
        ( 25,  25),  // 3→4
        ( 30,  30),  // 4→5
        ( 30,  30),  // 5→6
        ( 35,  35),  // 6→7
        ( 35,  35),  // 7→8
        ( 40,  40),  // 8→9
        ( 40,  40),  // 9→10
        ( 40,  40),  // 10→11
        ( 50,  50),  // 11→12
        ( 50,  50),  // 12→13
        ( 50,  50),  // 13→14
        ( 50,  50),  // 14→15
        ( 65,  65),  // 15→16
        ( 65,  65),  // 16→17
        ( 65,  65),  // 17→18
        ( 65,  65),  // 18→19
        ( 65,  65),  // 19→20
        ( 90,  90),  // 20→21
        ( 90,  90),  // 21→22
        ( 90,  90),  // 22→23
        (120, 120),  // 23→24
        (160, 160),  // 24→25
    };

    // 부스탯/창의성 증가 배율 — 주스탯 증가량(잠재력 보너스 포함) 대비. 0.5 고정.
    // 고정값이라 하락 시 테이블+잠재력으로 재계산 가능 → 강화 기록(enhancementRecordsJson) 저장 불필요.
    const float SubStatRatio = 0.5f;

    public void ApplyEnhancement(EmployeeData employee)
    {
        // 주스탯: 강화 레벨 기반 테이블 (++후 호출되므로 -1로 인덱스)
        int tableIndex = Mathf.Clamp(employee.enhancementLevel - 1, 0, MainStatGainTable.Length - 1);
        var (mainMin, mainMax) = MainStatGainTable[tableIndex];

        int mainGain = UnityEngine.Random.Range(mainMin, mainMax + 1);

        // Potential 보너스 적용 후 부스탯 계산 기준으로 사용
        int potentialBonus = employee.potential switch
        {
            EmployeePotential.C => 0,
            EmployeePotential.B => 1,
            EmployeePotential.A => 3,
            EmployeePotential.S => 8,
            _ => 0
        };
        int totalMainGain = mainGain + potentialBonus;

        // 부스탯: 주스탯 증가량의 0.5 고정 — 3개(비주스탯 2 + 창의성) 동일 적용
        int subGain = Mathf.RoundToInt(totalMainGain * SubStatRatio);

        ApplyStat(employee, GetMainStatKey(employee.role), totalMainGain);
        foreach (var key in GetSubStatKeys(employee.role))
            ApplyStat(employee, key, subGain);

        // 금수저: 강화 연봉 상승량도 50% 감소
        employee.salary += CharacterTraitApplier.ApplyGoldspoonSalary(employee, EnhanceSalaryTable[tableIndex]);
    }

    // 강화 하락 시 해당 레벨의 스탯/연봉을 되돌림.
    // 부스탯 0.5 고정이라 기록 없이 테이블+잠재력으로 재계산 — 주스탯/부스탯/연봉 모두 동일 기준으로 롤백.
    public void ReverseEnhancement(EmployeeData employee, int levelToReverse)
    {
        int tableIndex = Mathf.Clamp(levelToReverse - 1, 0, MainStatGainTable.Length - 1);
        var (mainMin, mainMax) = MainStatGainTable[tableIndex];

        int potentialBonus = employee.potential switch
        {
            EmployeePotential.C => 0,
            EmployeePotential.B => 1,
            EmployeePotential.A => 3,
            EmployeePotential.S => 8,
            _ => 0
        };
        int totalMainGain = (mainMin + mainMax) / 2 + potentialBonus; // 0~10강은 min==max
        int subGain = Mathf.RoundToInt(totalMainGain * SubStatRatio);

        ApplyStat(employee, GetMainStatKey(employee.role), -totalMainGain);
        foreach (var key in GetSubStatKeys(employee.role))
            ApplyStat(employee, key, -subGain);

        // 금수저: 강화 시 절반만 더했으므로 롤백도 절반만 차감 (가감 대칭)
        employee.salary = Mathf.Max(0, employee.salary - CharacterTraitApplier.ApplyGoldspoonSalary(employee, EnhanceSalaryTable[tableIndex]));
    }

    // 강화된 직원 채용 비용 = 연봉 누적 증가량 (EnhanceSalaryTable 의 레벨별 누적합).
    // 강화 레벨 N 후보를 뽑을 때 이 값(±20%)을 비용으로 차감. (11강부터 비용 발생)
    private static readonly int[] CumulativeExpectedEnhanceCost =
    {
                0,  // 0강
                0,  // 1강
                0,  // 2강
                0,  // 3강
                0,  // 4강
                0,  // 5강
                0,  // 6강
                0,  // 7강
                0,  // 8강
                0,  // 9강
                0,  // 10강
            1_500,  // 11강
            4_500,  // 12강
            9_000,  // 13강
           15_000,  // 14강
           30_000,  // 15강
           50_000,  // 16강
           75_000,  // 17강
          105_000,  // 18강
          140_000,  // 19강
          240_000,  // 20강
          370_000,  // 21강
          530_000,  // 22강
          730_000,  // 23강
          980_000,  // 24강
        1_280_000,  // 25강
    };

    public static int GetExpectedEnhanceCost(int enhancementLevel)
    {
        if (enhancementLevel <= 0) return 0;
        int idx = Mathf.Clamp(enhancementLevel, 0, CumulativeExpectedEnhanceCost.Length - 1);
        return CumulativeExpectedEnhanceCost[idx];
    }

    // 채용 시 기댓값 기반 강화 적용 (랜덤 없이 각 단계 기댓값을 확정 적용)
    public void ApplyEnhancementExpected(EmployeeData employee, int targetLevel)
    {
        employee.enhancementLevel = targetLevel;
        if (targetLevel <= 0) return;

        int potentialBonus = employee.potential switch
        {
            EmployeePotential.C => 0,
            EmployeePotential.B => 1,
            EmployeePotential.A => 3,
            EmployeePotential.S => 8,
            _ => 0
        };

        int mainGainTotal = 0;
        int subGainTotal  = 0;
        int salaryGain    = 0;

        for (int lv = 1; lv <= targetLevel; lv++)
        {
            int ti = Mathf.Clamp(lv - 1, 0, MainStatGainTable.Length - 1);
            var (minG, maxG) = MainStatGainTable[ti];
            int stepMain = (minG + maxG) / 2 + potentialBonus;
            int stepSub  = Mathf.RoundToInt(stepMain * SubStatRatio);

            mainGainTotal += stepMain;
            subGainTotal  += stepSub;
            salaryGain    += EnhanceSalaryTable[ti];
        }

        employee.mainStatEnhanceGain = mainGainTotal;
        employee.subStatEnhanceMin = subGainTotal; // 0.5 고정이라 min==max
        employee.subStatEnhanceMax = subGainTotal;

        string mainStat  = GetMainStatKey(employee.role);
        var    subStats  = GetSubStatKeys(employee.role);

        ApplyStat(employee, mainStat,    mainGainTotal);
        ApplyStat(employee, subStats[0], subGainTotal);
        ApplyStat(employee, subStats[1], subGainTotal);
        ApplyStat(employee, subStats[2], subGainTotal);

        // 금수저: 채용 시 일괄강화 연봉 상승량도 50% 감소
        employee.salary += CharacterTraitApplier.ApplyGoldspoonSalary(employee, salaryGain);
    }

    // 잠재력 보너스 (강화 주스탯 증가량에 가산)
    private static int PotentialBonus(EmployeePotential p) => p switch
    {
        EmployeePotential.C => 0,
        EmployeePotential.B => 1,
        EmployeePotential.A => 3,
        EmployeePotential.S => 8,
        _ => 0
    };

    // 다음 강화(현재 레벨 → +1) 주스탯 증가 범위 (잠재력 보너스 포함). 예상수치 미리보기용.
    public (int min, int max) GetNextMainStatGain(EmployeeData emp)
    {
        int ti = Mathf.Clamp(emp.enhancementLevel, 0, MainStatGainTable.Length - 1);
        var (mn, mx) = MainStatGainTable[ti];
        int pot = PotentialBonus(emp.potential);
        return (mn + pot, mx + pot);
    }

    // 다음 강화 부스탯 증가 범위 (주스탯 증가량 × 0.5). 예상수치 미리보기용.
    public (int min, int max) GetNextSubStatGain(EmployeeData emp)
    {
        var (mn, mx) = GetNextMainStatGain(emp);
        return (Mathf.RoundToInt(mn * SubStatRatio), Mathf.RoundToInt(mx * SubStatRatio));
    }

    // 다음 강화 부스탯 평균(단일) 증가량 — 주스탯 범위 중앙값 × 0.5. 부스탯 단일 표시용.
    public int GetNextSubStatGainAvg(EmployeeData emp)
    {
        var (mn, mx) = GetNextMainStatGain(emp);
        return Mathf.RoundToInt((mn + mx) / 2f * SubStatRatio);
    }

    // 다음 강화 1회의 연봉 상승량 (금수저 반영). 예상 연봉 미리보기용. (ApplyEnhancement 과 동일 인덱스 규칙)
    public int GetNextSalaryGain(EmployeeData emp)
    {
        if (emp == null) return 0;
        int ti = Mathf.Clamp(emp.enhancementLevel, 0, EnhanceSalaryTable.Length - 1);
        return CharacterTraitApplier.ApplyGoldspoonSalary(emp, EnhanceSalaryTable[ti]);
    }

    private string GetMainStatKey(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner => "planning",
        EmployeeRole.Programmer => "develop",
        EmployeeRole.Artist => "art",
        _ => "develop"
    };

    private List<string> GetSubStatKeys(EmployeeRole role)
    {
        var all = new List<string> { "develop", "planning", "art" };
        all.Remove(GetMainStatKey(role));
        all.Add("creativity");   // 창의성도 부스탯
        return all;
    }

    private void ApplyStat(EmployeeData e, string key, int gain)
    {
        switch (key)
        {
            case "develop": e.developSkill += gain; break;
            case "planning": e.planningSkill += gain; break;
            case "art": e.artSkill += gain; break;
            case "creativity": e.creativitySkill += gain; break;
        }
    }

    // ── 파견(Dispatch) 연동 ───────────────────────────────────
    // 파견 강화 적용 — 다운그레이드/유지 없이 oldLevel→targetLevel 까지 확정 강화.
    // 구간별 강화량은 DispatchManager 가 파견 시점에 사전 계산(targetLevel)해 둔다.
    public void ApplyDispatchEnhancement(EmployeeData emp, int targetLevel)
    {
        if (emp == null) return;
        while (emp.enhancementLevel < targetLevel)
        {
            emp.enhancementLevel++;
            ApplyEnhancement(emp);
        }
    }

    // 만족도를 특정 값으로 회복 (파견 복귀 등). 40 이상이면 저만족 페널티 플래그도 리셋.
    public void RecoverSatisfaction(EmployeeData emp, int value)
    {
        if (emp == null) return;
        emp.satisfaction = Mathf.Clamp(value, 0, 100);
        if (emp.satisfaction >= 40) emp.lowSatisfactionPenaltyApplied = false;
    }
}