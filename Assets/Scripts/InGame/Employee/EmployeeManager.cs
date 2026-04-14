using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using BackEnd.Content;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    public List<EmployeeData> ownedEmployees = new();
    public List<EmployeeData> poolEmployees = new();
    private readonly HashSet<string> _acquiredEmployeeIds = new();

    private bool _satisfactionDroppedThisCycle = false;

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
                    perfectionMin: int.Parse(row["perfectionMin"].ToString()),
                    perfectionMax: int.Parse(row["perfectionMax"].ToString()),
                    salaryMin:     int.Parse(row["salaryMin"].ToString()),
                    salaryMax:     int.Parse(row["salaryMax"].ToString()),
                    maxGrade:      (EmployeeGrade)int.Parse(row["maxGrade"].ToString())
                );
                data.portraitId = row["portraitId"].ToString();
                data.isDefault  = row["isDefault"].ToString() == "1";
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
            // Grade / Potential 결정
            employee.grade = RollGrade(employee.maxGrade);
            employee.potential = RollPotential(employee.grade, tierIndex);

            // 능력치는 마스터 min~max 범위로 재랜덤
            employee.developSkill = UnityEngine.Random.Range(employee.developMin, employee.developMax + 1);
            employee.planningSkill = UnityEngine.Random.Range(employee.planningMin, employee.planningMax + 1);
            employee.artSkill = UnityEngine.Random.Range(employee.artMin, employee.artMax + 1);
            employee.perfectionSkill = UnityEngine.Random.Range(employee.perfectionMin, employee.perfectionMax + 1);

            // 연봉 재랜덤
            int steps = (employee.salaryMax - employee.salaryMin) / 50;
            employee.salary = employee.salaryMin + (UnityEngine.Random.Range(0, steps + 1) * 50);
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
            perfectionMin: poolEmployee.perfectionMin,
            perfectionMax: poolEmployee.perfectionMax,
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
        inGameEmployee.perfectionSkill = poolEmployee.perfectionSkill;
        inGameEmployee.salary = poolEmployee.salary;
        inGameEmployee.enhancementLevel = poolEmployee.enhancementLevel;
        inGameEmployee.portraitId       = poolEmployee.portraitId;
        inGameEmployee.masterEmployeeId = poolEmployee.id;
        inGameEmployee.assignedProjectId = "";
        inGameEmployee.satisfaction = 80;
        
        Backend.GameData.Insert("Employee", inGameEmployee.ToParam(), bro =>
        {
            if (bro.IsSuccess())
            {
                inGameEmployee.rowInDate = bro.GetInDate();
                ownedEmployees.Add(inGameEmployee);
                HUDUI.Instance.RefreshAll();

                QuestManager.Instance.UpdateProgress(QuestType.HireEmployee, 1);
                OfficeManager.Instance?.OnEmployeeHired(inGameEmployee);
                if (DevelopmentManager.Instance.IsStarted)
                    DevelopmentManager.Instance.OnEmployeeHired(inGameEmployee);

                if (ownedEmployees.Count >= 2)
                    QuestManager.Instance.UnlockQuest("quest_003");
                if (ownedEmployees.Count >= 4)
                    QuestManager.Instance.UnlockQuest("quest_004");

                MoneyManager.Instance?.SaveMoney();
                GameTimeManager.Instance?.SaveGameTime();
                ProjectSaveManager.Instance?.SaveProject();
                Debug.Log($"채용 완료: {inGameEmployee.employeeName} ({inGameEmployee.grade} / {inGameEmployee.potential})");
            }
            else
            {
                Debug.LogError($"채용 저장 실패: {bro}");
            }
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
                ownedEmployees.Add(EmployeeData.FromServerRow((LitJson.JsonData)row));

            Debug.Log($"보유 직원 {ownedEmployees.Count}명 로드 완료");
            onComplete?.Invoke();
            HUDUI.Instance?.RefreshAll();
        });
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
            bool isOvertime = DevelopmentManager.Instance != null && DevelopmentManager.Instance.IsOvertimeActive;
            int dropAmount = isOvertime ? 10 : 5;

            foreach (var emp in ownedEmployees)
                emp.satisfaction = Mathf.Clamp(emp.satisfaction - dropAmount, 0, 100);
        }

        CheckLowSatisfaction();
        RandomEventManager.Instance?.CheckConditionEvents();
    }

    void CheckLowSatisfaction()
    {
        foreach (var emp in ownedEmployees)
        {
            if (emp.satisfaction >= 40) continue;

            // 스탯 ×0.8 (데이터 패널티만 처리, 이벤트 트리거는 RandomEventManager 담당)
            emp.developSkill    = Mathf.Max(1, Mathf.RoundToInt(emp.developSkill    * 0.8f));
            emp.planningSkill   = Mathf.Max(1, Mathf.RoundToInt(emp.planningSkill   * 0.8f));
            emp.artSkill        = Mathf.Max(1, Mathf.RoundToInt(emp.artSkill        * 0.8f));
            emp.perfectionSkill = Mathf.Max(1, Mathf.RoundToInt(emp.perfectionSkill * 0.8f));
        }
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
        foreach (var emp in ownedEmployees)
        {
            emp.satisfaction = Mathf.Clamp(emp.satisfaction + 40, 0, 100);
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

    public void UpdateEmployee(EmployeeData employee)
    {
        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        Backend.GameData.UpdateV2("Employee", employee.rowInDate, Backend.UserInDate, employee.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"직원 업데이트 실패: {bro}");
        });
    }

    public void FireEmployee(EmployeeData employee)
    {
        ownedEmployees.Remove(employee);
        OfficeManager.Instance?.OnEmployeeFired(employee);
        if (DevelopmentManager.Instance.IsStarted)
            DevelopmentManager.Instance.OnEmployeeFired(employee.id);

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
              0,  // 10→11
          5_000,  // 11→12
          8_000,  // 12→13
         11_000,  // 13→14
         14_000,  // 14→15
         20_000,  // 15→16
         25_000,  // 16→17
         30_000,  // 17→18
         35_000,  // 18→19
         50_000,  // 19→20
         60_000,  // 20→21
         70_000,  // 21→22
         80_000,  // 22→23
         90_000,  // 23→24
        100_000,  // 24→25
    };

    // 주스탯 증가량 테이블 [강화 단계(0→1 ~ 24→25)] = (min, max)
    private static readonly (int min, int max)[] MainStatGainTable =
    {
        ( 5,  5),  // 0→1
        (10, 10),  // 1→2
        (10, 10),  // 2→3
        (10, 10),  // 3→4
        (15, 15),  // 4→5
        (15, 15),  // 5→6
        (15, 15),  // 6→7
        (20, 20),  // 7→8
        (20, 20),  // 8→9
        (20, 20),  // 9→10
        (25, 25),  // 10→11
        (25, 35),  // 11→12
        (25, 35),  // 12→13
        (25, 35),  // 13→14
        (25, 35),  // 14→15
        (35, 45),  // 15→16
        (35, 45),  // 16→17
        (35, 45),  // 17→18
        (35, 45),  // 18→19
        (35, 45),  // 19→20
        (40, 60),  // 20→21
        (40, 60),  // 21→22
        (40, 60),  // 22→23
        (40, 80),  // 23→24
        (50,100),  // 24→25
    };

    [System.Serializable]
    private class EnhancementRecord
    {
        public int level;
        public string sub0Stat;
        public int sub0Gain;
        public string sub1Stat;
        public int sub1Gain;
    }

    [System.Serializable]
    private class EnhancementRecordListWrapper
    {
        public List<EnhancementRecord> records = new();
    }

    private List<EnhancementRecord> ParseEnhancementRecords(EmployeeData employee)
    {
        if (string.IsNullOrEmpty(employee.enhancementRecordsJson) || employee.enhancementRecordsJson == "[]")
            return new List<EnhancementRecord>();
        try
        {
            var wrapper = JsonUtility.FromJson<EnhancementRecordListWrapper>(
                "{\"records\":" + employee.enhancementRecordsJson + "}");
            return wrapper?.records ?? new List<EnhancementRecord>();
        }
        catch { return new List<EnhancementRecord>(); }
    }

    private string SerializeRecords(List<EnhancementRecord> records)
    {
        var wrapper = new EnhancementRecordListWrapper { records = records };
        string json = JsonUtility.ToJson(wrapper);
        int start = json.IndexOf('[');
        int end   = json.LastIndexOf(']');
        return (start >= 0 && end >= 0) ? json.Substring(start, end - start + 1) : "[]";
    }

    public void ApplyEnhancement(EmployeeData employee)
    {
        // 주스탯: 강화 레벨 기반 테이블 (++후 호출되므로 -1로 인덱스)
        int tableIndex = Mathf.Clamp(employee.enhancementLevel - 1, 0, MainStatGainTable.Length - 1);
        var (mainMin, mainMax) = MainStatGainTable[tableIndex];

        string mainStat = GetMainStatKey(employee.role);
        int mainGain = UnityEngine.Random.Range(mainMin, mainMax + 1);

        // Potential 보너스 적용 후 부스탯 계산 기준으로 사용
        int potentialBonus = employee.potential switch
        {
            EmployeePotential.C => 0,
            EmployeePotential.B => 1,
            EmployeePotential.A => 3,
            EmployeePotential.S => 5,
            _ => 0
        };
        int totalMainGain = mainGain + potentialBonus;

        var subStats = GetSubStatKeys(employee.role);
        Shuffle(subStats);
        int subGain0 = Mathf.RoundToInt(totalMainGain * UnityEngine.Random.Range(0.3f, 0.5f));
        int subGain1 = Mathf.RoundToInt(totalMainGain * UnityEngine.Random.Range(0.3f, 0.5f));
        int salaryGain = EnhanceSalaryTable[tableIndex];

        ApplyStat(employee, mainStat, totalMainGain);
        ApplyStat(employee, subStats[0], subGain0);
        ApplyStat(employee, subStats[1], subGain1);
        employee.salary += EnhanceSalaryTable[tableIndex];

        // 부스탯 배정 기록 저장 (하락 시 롤백용, 주스탯/연봉은 테이블로 계산 가능)
        var records = ParseEnhancementRecords(employee);
        records.RemoveAll(r => r.level == employee.enhancementLevel); // 같은 레벨 재강화 시 덮어쓰기
        records.Add(new EnhancementRecord
        {
            level    = employee.enhancementLevel,
            sub0Stat = subStats[0],
            sub0Gain = subGain0,
            sub1Stat = subStats[1],
            sub1Gain = subGain1,
        });
        employee.enhancementRecordsJson = SerializeRecords(records);
    }

    // 강화 하락 시 해당 레벨의 스탯/연봉을 되돌림
    // 주스탯/연봉: 테이블 고정값으로 계산, 부스탯: 기록에서 조회
    public void ReverseEnhancement(EmployeeData employee, int levelToReverse)
    {
        int tableIndex = Mathf.Clamp(levelToReverse - 1, 0, MainStatGainTable.Length - 1);
        var (mainMin, mainMax) = MainStatGainTable[tableIndex];

        int potentialBonus = employee.potential switch
        {
            EmployeePotential.C => 0,
            EmployeePotential.B => 1,
            EmployeePotential.A => 3,
            EmployeePotential.S => 5,
            _ => 0
        };
        int mainGain = (mainMin + mainMax) / 2 + potentialBonus; // 0~10강은 min==max

        ApplyStat(employee, GetMainStatKey(employee.role), -mainGain);
        employee.salary = Mathf.Max(0, employee.salary - EnhanceSalaryTable[tableIndex]);

        var records = ParseEnhancementRecords(employee);
        var record  = records.Find(r => r.level == levelToReverse);
        if (record != null)
        {
            ApplyStat(employee, record.sub0Stat, -record.sub0Gain);
            ApplyStat(employee, record.sub1Stat, -record.sub1Gain);
            records.Remove(record);
            employee.enhancementRecordsJson = SerializeRecords(records);
        }
        else
        {
            Debug.LogWarning($"부스탯 강화 기록 없음 (lv {levelToReverse}), 부스탯 롤백 스킵");
        }
    }

    // 강화 레벨별 누적 기댓값 비용 (0강=0원, 11강=4696원, ...)
    // EnhanceCostTable + EnhanceTable 확률로 계산한 값
    private static readonly int[] CumulativeExpectedEnhanceCost =
    {
            0,  // 0강
           51,  // 1강
          158,  // 2강
          282,  // 3강
          421,  // 4강
          643,  // 5강
          917,  // 6강
         1249,  // 7강
         1735,  // 8강
         2393,  // 9강
         3295,  // 10강
         4696,  // 11강
        11363,  // 12강
        24696,  // 13강
        44696,  // 14강
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
            EmployeePotential.S => 5,
            _ => 0
        };

        int mainGainTotal  = 0;
        int subGainTotal   = 0;
        int subGainMinTotal = 0;
        int subGainMaxTotal = 0;
        int salaryGain     = 0;

        for (int lv = 1; lv <= targetLevel; lv++)
        {
            int ti = Mathf.Clamp(lv - 1, 0, MainStatGainTable.Length - 1);
            var (minG, maxG) = MainStatGainTable[ti];
            int stepMain = (minG + maxG) / 2 + potentialBonus;
            int stepSub  = Mathf.RoundToInt(stepMain * UnityEngine.Random.Range(0.3f, 0.5f));

            mainGainTotal   += stepMain;
            subGainTotal    += stepSub;
            subGainMinTotal += Mathf.RoundToInt(stepMain * 0.3f);
            subGainMaxTotal += Mathf.RoundToInt(stepMain * 0.5f);
            salaryGain      += EnhanceSalaryTable[ti];
        }

        employee.mainStatEnhanceGain = mainGainTotal;
        employee.subStatEnhanceMin = subGainMinTotal;
        employee.subStatEnhanceMax = subGainMaxTotal;

        string mainStat  = GetMainStatKey(employee.role);
        var    subStats  = GetSubStatKeys(employee.role);

        ApplyStat(employee, mainStat,    mainGainTotal);
        ApplyStat(employee, subStats[0], subGainTotal);
        ApplyStat(employee, subStats[1], subGainTotal);

        employee.salary += salaryGain;
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
        return all;
    }

    private void ApplyStat(EmployeeData e, string key, int gain)
    {
        switch (key)
        {
            case "develop": e.developSkill += gain; break;
            case "planning": e.planningSkill += gain; break;
            case "art": e.artSkill += gain; break;
            case "perfection": e.perfectionSkill += gain; break;
        }
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }


}