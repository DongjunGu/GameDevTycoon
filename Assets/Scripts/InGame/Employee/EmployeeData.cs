using System;
using LitJson;

public enum EmployeeRole { Planner, Programmer, Artist }
public enum EmployeeGrade { F, D, C, B, A, S }
public enum EmployeeState { Idle, Working }

[Serializable]
public class EmployeeData
{
    public string id;
    public string rowInDate;

    public string employeeName;
    public EmployeeRole role;
    public EmployeeGrade grade;

    // ── 확정 수치 ─────────────────────────────
    public int developSkill;
    public int planningSkill;
    public int artSkill;
    public int perfectionSkill;

    // ── 범위 수치 ─────────────────────────────
    public int developMin; public int developMax;
    public int planningMin; public int planningMax;
    public int artMin; public int artMax;
    public int perfectionMin; public int perfectionMax;

    public EmployeeState state;
    public string assignedProjectId;

    public EmployeeData(string id, string name, EmployeeRole role, EmployeeGrade grade,
        int developMin, int developMax,
        int planningMin, int planningMax,
        int artMin, int artMax,
        int perfectionMin, int perfectionMax)
    {
        this.id = id;
        this.employeeName = name;
        this.role = role;
        this.grade = grade;

        this.developMin = developMin; this.developMax = developMax;
        this.planningMin = planningMin; this.planningMax = planningMax;
        this.artMin = artMin; this.artMax = artMax;
        this.perfectionMin = perfectionMin; this.perfectionMax = perfectionMax;

        this.developSkill = UnityEngine.Random.Range(developMin, developMax + 1);
        this.planningSkill = UnityEngine.Random.Range(planningMin, planningMax + 1);
        this.artSkill = UnityEngine.Random.Range(artMin, artMax + 1);
        this.perfectionSkill = UnityEngine.Random.Range(perfectionMin, perfectionMax + 1);

        this.state = EmployeeState.Idle;
        this.assignedProjectId = "";
    }

    public static EmployeeData FromServerRow(JsonData row)
    {
        var data = new EmployeeData(
            id: row["id"].ToString(),
            name: row["employeeName"].ToString(),
            role: (EmployeeRole)int.Parse(row["role"].ToString()),
            grade: (EmployeeGrade)int.Parse(row["grade"].ToString()),
            developMin: SafeInt(row, "developMin", 0),
            developMax: SafeInt(row, "developMax", 0),
            planningMin: SafeInt(row, "planningMin", 0),
            planningMax: SafeInt(row, "planningMax", 0),
            artMin: SafeInt(row, "artMin", 0),
            artMax: SafeInt(row, "artMax", 0),
            perfectionMin: SafeInt(row, "perfectionMin", 0),
            perfectionMax: SafeInt(row, "perfectionMax", 0)
        );

        data.developSkill = SafeInt(row, "developSkill", 0);
        data.planningSkill = SafeInt(row, "planningSkill", 0);
        data.artSkill = SafeInt(row, "artSkill", 0);
        data.perfectionSkill = SafeInt(row, "perfectionSkill", 0);

        data.rowInDate = row["inDate"].ToString();
        data.state = (EmployeeState)SafeInt(row, "state", 0);
        data.assignedProjectId = SafeString(row, "assignedProjectId", "");

        return data;
    }

    public BackEnd.Param ToParam()
    {
        var param = new BackEnd.Param();
        param.Add("id", id);
        param.Add("employeeName", employeeName);
        param.Add("role", (int)role);
        param.Add("grade", (int)grade);
        param.Add("developSkill", developSkill);
        param.Add("planningSkill", planningSkill);
        param.Add("artSkill", artSkill);
        param.Add("perfectionSkill", perfectionSkill);
        param.Add("developMin", developMin);
        param.Add("developMax", developMax);
        param.Add("planningMin", planningMin);
        param.Add("planningMax", planningMax);
        param.Add("artMin", artMin);
        param.Add("artMax", artMax);
        param.Add("perfectionMin", perfectionMin);
        param.Add("perfectionMax", perfectionMax);
        param.Add("state", (int)state);
        param.Add("assignedProjectId", assignedProjectId);
        return param;
    }

    // ── 범위 표시 ─────────────────────────────
    public string DevelopRangeText() => $"개발: {developMin}~{developMax}";
    public string PlanningRangeText() => $"기획: {planningMin}~{planningMax}";
    public string ArtRangeText() => $"아트: {artMin}~{artMax}";
    public string PerfectionRangeText() => $"완성도: {perfectionMin}~{perfectionMax}";

    // ── 확정 수치 표시 ────────────────────────
    public string DevelopText() => $"개발: {developSkill}";
    public string PlanningText() => $"기획: {planningSkill}";
    public string ArtText() => $"아트: {artSkill}";
    public string PerfectionText() => $"완성도: {perfectionSkill}";

    public string RoleToString() => role switch
    {
        EmployeeRole.Planner => "기획자",
        EmployeeRole.Programmer => "프로그래머",
        EmployeeRole.Artist => "아티스트",
        _ => ""
    };

    public string GradeToString() => grade switch
    {
        EmployeeGrade.F => "F",
        EmployeeGrade.D => "D",
        EmployeeGrade.C => "C",
        EmployeeGrade.B => "B",
        EmployeeGrade.A => "A",
        EmployeeGrade.S => "S",
        _ => ""
    };

    public string StateToString() => state switch
    {
        EmployeeState.Idle => "대기중",
        EmployeeState.Working => "근무중",
        _ => ""
    };
    static int SafeInt(JsonData row, string key, int defaultValue)
    {
        try { return int.Parse(row[key].ToString()); }
        catch { return defaultValue; }
    }

    static string SafeString(JsonData row, string key, string defaultValue)
    {
        try { return row[key].ToString(); }
        catch { return defaultValue; }
    }
}