using System.Collections.Generic;
using UnityEngine;
using BackEnd;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    public List<EmployeeData> ownedEmployees = new();
    public List<EmployeeData> poolEmployees = new();
    private List<EmployeeData> _defaultEmployees;

    private bool _satisfactionDroppedThisCycle = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _defaultEmployees = new List<EmployeeData>
        {
            //                                                             dev      plan     art      perfection   salary        maxGrade
            new EmployeeData("emp_01", "김기획", EmployeeRole.Planner,    40,50, 80,90, 25,35, 15,25, 1400,1600, EmployeeGrade.Rare) { portraitId = "portrait_emp_01" },
            new EmployeeData("emp_02", "이코딩", EmployeeRole.Programmer, 88,95, 20,30, 50,60, 20,30, 2300,2700, EmployeeGrade.Unique){ portraitId = "portrait_emp_01" },
            new EmployeeData("emp_03", "박아트", EmployeeRole.Artist,      8,15, 45,55, 82,92, 11,22,  900,1100, EmployeeGrade.Normal){ portraitId = "portrait_emp_01" },
            new EmployeeData("emp_04", "최사운", EmployeeRole.Programmer, 25,35, 35,45, 48,62, 10,20,  650, 750, EmployeeGrade.Normal){ portraitId = "portrait_emp_01" },
            new EmployeeData("emp_05", "정기획", EmployeeRole.Planner,    12,20, 70,80, 20,30, 11,22,  900,1000, EmployeeGrade.Rare){ portraitId = "portrait_emp_01" },
            new EmployeeData("emp_06", "한개발", EmployeeRole.Programmer, 80,90, 30,40, 42,50, 15,25, 1500,1700, EmployeeGrade.Epic){ portraitId = "portrait_emp_02" },
            new EmployeeData("emp_07", "오아트", EmployeeRole.Artist,      8,15, 48,58, 90,99, 20,30, 2000,2400, EmployeeGrade.Unique){ portraitId = "portrait_emp_02" },
            new EmployeeData("emp_08", "윤기획", EmployeeRole.Planner,     8,14, 60,70, 15,25, 10,20,  600, 700, EmployeeGrade.Normal){ portraitId = "portrait_emp_02" },
            new EmployeeData("emp_09", "장개발", EmployeeRole.Programmer, 73,82, 25,35, 38,48, 11,22, 1000,1100, EmployeeGrade.Rare){ portraitId = "portrait_emp_02" },
            new EmployeeData("emp_10", "강아트", EmployeeRole.Artist,     12,20, 40,50, 78,88, 15,25, 1350,1550, EmployeeGrade.Epic){ portraitId = "portrait_emp_02" },
        };
    }

    // ── 초기 로드 ─────────────────────────────
    public void LoadAllData(System.Action onComplete = null)
    {
        LoadEmployeePool(() => LoadEmployees(onComplete));
    }

    // ── EmployeePool 불러오기 ─────────────────
    public void LoadEmployeePool(System.Action onComplete = null)
    {
        Backend.GameData.GetMyData("EmployeePool", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"EmployeePool 불러오기 실패: {bro}");
                return;
            }

            var rows = bro.FlattenRows();

            if (rows.Count == 0)
            {
                InsertDefaultEmployees(onComplete);
                return;
            }

            poolEmployees.Clear();
            foreach (var row in rows)
            {
                var employee = EmployeeData.FromServerRow((LitJson.JsonData)row);
                poolEmployees.Add(employee);
            }

            Debug.Log($"EmployeePool {poolEmployees.Count}명 로드 완료");
            onComplete?.Invoke();
        });
    }

    void InsertDefaultEmployees(System.Action onComplete = null)
    {
        int savedCount = 0;

        foreach (var employee in _defaultEmployees)
        {
            Backend.GameData.Insert("EmployeePool", employee.ToParam(), bro =>
            {
                if (bro.IsSuccess())
                {
                    employee.rowInDate = bro.GetInDate();
                    poolEmployees.Add(employee);
                }
                else
                {
                    Debug.LogError($"기본 직원 저장 실패: {bro}");
                }

                savedCount++;
                if (savedCount == _defaultEmployees.Count)
                {
                    Debug.Log("기본 직원 10명 생성 완료");
                    onComplete?.Invoke();
                }
            });
        }
    }

    // ── 채용 후보 랜덤 추출 ───────────────────
    public void LoadRandomCandidates(int count, System.Action<List<EmployeeData>> onComplete)
    {
        if (poolEmployees.Count == 0)
        {
            Debug.LogError("EmployeePool 비어있음");
            return;
        }

        var shuffled = ShuffleList(poolEmployees);
        int take = Mathf.Min(count, shuffled.Count);
        var candidates = shuffled.GetRange(0, take);

        foreach (var employee in candidates)
        {
            // Grade / Potential 결정
            employee.grade = RollGrade(employee.maxGrade);
            employee.potential = RollPotential(employee.grade);

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
        inGameEmployee.portraitId      = poolEmployee.portraitId;
        inGameEmployee.assignedProjectId = "";
        inGameEmployee.satisfaction = 90;
        
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

    private EmployeePotential RollPotential(EmployeeGrade grade)
    {
        // 순서: C, B, A, S
        int[] weights = grade switch
        {
            EmployeeGrade.Normal => new[] { 6, 3, 1, 0 },
            EmployeeGrade.Rare   => new[] { 4, 4, 2, 0 },
            EmployeeGrade.Epic   => new[] { 2, 4, 3, 1 },
            EmployeeGrade.Unique => new[] { 0, 2, 4, 4 },
            _                    => new[] { 6, 3, 1, 0 }
        };

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
        Backend.GameData.GetMyData("Employee", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"직원 불러오기 실패: {bro}");
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

    public void UpdateEmployeePool(EmployeeData employee)
    {
        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        Backend.GameData.UpdateV2("EmployeePool", employee.rowInDate, Backend.UserInDate, employee.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"직원 업그레이드 실패: {bro}");
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
            {
                emp.satisfaction = Mathf.Clamp(emp.satisfaction - dropAmount, 0, 100);
                UpdateEmployee(emp);
            }
        }

        RandomEventManager.Instance?.CheckConditionEvents();
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

        ApplyStat(employee, mainStat, totalMainGain);
        ApplyStat(employee, subStats[0], subGain0);
        ApplyStat(employee, subStats[1], subGain1);

        // 연봉 증가 (강화 성공 시 — tableIndex = 새 레벨 - 1)
        employee.salary += EnhanceSalaryTable[tableIndex];
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