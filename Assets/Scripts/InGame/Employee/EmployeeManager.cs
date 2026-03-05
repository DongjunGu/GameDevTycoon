using System.Collections.Generic;
using UnityEngine;
using BackEnd;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    public List<EmployeeData> ownedEmployees = new();   // 채용된 직원
    public List<EmployeeData> poolEmployees = new();   // 전체 직원 풀
    private List<EmployeeData> _defaultEmployees;
    // 기본 직원 데이터 (최초 1회 서버에 저장)
void Awake()
{
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);

    _defaultEmployees = new List<EmployeeData>
    {
        new EmployeeData("emp_01", "김기획", EmployeeRole.Planner,    EmployeeGrade.A, 18,25, 80,90, 25,35),
        new EmployeeData("emp_02", "이코딩", EmployeeRole.Programmer, EmployeeGrade.S, 88,95, 20,30, 10,18),
        new EmployeeData("emp_03", "박아트", EmployeeRole.Artist,     EmployeeGrade.B,  8,15, 15,25, 82,92),
        new EmployeeData("emp_04", "최사운", EmployeeRole.Programmer, EmployeeGrade.C, 25,35, 35,45, 48,62),
        new EmployeeData("emp_05", "정기획", EmployeeRole.Planner,    EmployeeGrade.B, 12,20, 70,80, 20,30),
        new EmployeeData("emp_06", "한개발", EmployeeRole.Programmer, EmployeeGrade.A, 80,90, 30,40, 15,25),
        new EmployeeData("emp_07", "오아트", EmployeeRole.Artist,     EmployeeGrade.S,  8,15, 14,22, 90,99),
        new EmployeeData("emp_08", "윤기획", EmployeeRole.Planner,    EmployeeGrade.C,  8,14, 60,70, 15,25),
        new EmployeeData("emp_09", "장개발", EmployeeRole.Programmer, EmployeeGrade.B, 73,82, 25,35, 14,22),
        new EmployeeData("emp_10", "강아트", EmployeeRole.Artist,     EmployeeGrade.A, 12,20, 18,26, 78,88),
    };
}

    // ── 초기 로드 (LoadingScene에서 호출) ─────
    public void LoadAllData(System.Action onComplete = null)
    {
        LoadEmployeePool(() =>
        {
            LoadEmployees(onComplete);
        });
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
                // 신규 유저 → 기본 직원 10명 Insert
                InsertDefaultEmployees(onComplete);
                return;
            }

            poolEmployees.Clear();
            foreach (var row in rows)
                poolEmployees.Add(EmployeeData.FromServerRow((LitJson.JsonData)row));

            Debug.Log($"EmployeePool {poolEmployees.Count}명 로드 완료");
            onComplete?.Invoke();
        });
    }

    // 신규 유저 기본 직원 생성
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
        // 이미 로드된 poolEmployees에서 랜덤 추출
        if (poolEmployees.Count == 0)
        {
            Debug.LogError("EmployeePool 비어있음");
            return;
        }

        var shuffled = ShuffleList(poolEmployees);
        int take = Mathf.Min(count, shuffled.Count);
        onComplete?.Invoke(shuffled.GetRange(0, take));
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
            grade: poolEmployee.grade,
            developMin: poolEmployee.developMin,
            developMax: poolEmployee.developMax,
            planningMin: poolEmployee.planningMin,
            planningMax: poolEmployee.planningMax,
            artMin: poolEmployee.artMin,
            artMax: poolEmployee.artMax
        );

        // 확정 수치는 poolEmployee 값 그대로 복사 (랜덤 재생성 방지)
        inGameEmployee.developSkill = poolEmployee.developSkill;
        inGameEmployee.planningSkill = poolEmployee.planningSkill;
        inGameEmployee.artSkill = poolEmployee.artSkill;
        inGameEmployee.assignedProjectId = "";

        ownedEmployees.Add(inGameEmployee);

        Backend.GameData.Insert("Employee", inGameEmployee.ToParam(), bro =>
        {
            if (bro.IsSuccess())
            {
                inGameEmployee.rowInDate = bro.GetInDate();
                Debug.Log($"채용 완료: {inGameEmployee.employeeName}");
            }
            else
            {
                Debug.LogError($"채용 저장 실패: {bro}");
            }
        });
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
        });
    }

    // ── 직원 업그레이드 (아웃게임) ────────────
    public void UpdateEmployeePool(EmployeeData employee)
    {
        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        Backend.GameData.UpdateV2("EmployeePool", employee.rowInDate, Backend.UserInDate, employee.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"직원 업그레이드 실패: {bro}");
        });
    }

    // ── 상태 업데이트 ─────────────────────────
    public void UpdateEmployee(EmployeeData employee)
    {
        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        Backend.GameData.UpdateV2("Employee", employee.rowInDate, Backend.UserInDate, employee.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"직원 업데이트 실패: {bro}");
        });
    }

    // ── 해고 ──────────────────────────────────
    public void FireEmployee(EmployeeData employee)
    {
        ownedEmployees.Remove(employee);

        if (string.IsNullOrEmpty(employee.rowInDate)) return;

        Backend.GameData.DeleteV2("Employee", employee.rowInDate, Backend.UserInDate, bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"해고 저장 실패: {bro}");
        });
    }
}