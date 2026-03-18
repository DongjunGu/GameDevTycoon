using System;
using LitJson;

public enum EmployeeRole      { Planner, Programmer, Artist }
public enum EmployeePotential { F, D, C, B, A }
public enum EmployeeGrade     { Normal, Rare, Epic, Unique }
public enum EmployeeState     { Idle, Working }

[Serializable]
public class EmployeeData
{
    public string id;
    public string rowInDate;

    public string        employeeName;
    public EmployeeRole  role;
    public EmployeeGrade maxGrade;   // 마스터 데이터 기준 최고 등급
    public EmployeeGrade grade;      // 채용 시 결정된 등급
    public EmployeePotential potential; // 채용 시 결정된 잠재력

    // ── 확정 수치 ─────────────────────────────
    public int developSkill;
    public int planningSkill;
    public int artSkill;
    public int perfectionSkill;
    public int salary;
    public int enhancementLevel;

    // ── 범위 수치 ─────────────────────────────
    public int developMin,    developMax;
    public int planningMin,   planningMax;
    public int artMin,        artMax;
    public int perfectionMin, perfectionMax;
    public int salaryMin,     salaryMax;

    public EmployeeState state;
    public string assignedProjectId;
    public string portraitId;

    // ── 생성자 (EmployeePool 마스터 데이터용) ──
    public EmployeeData(string id, string name, EmployeeRole role,
        int developMin,    int developMax,
        int planningMin,   int planningMax,
        int artMin,        int artMax,
        int perfectionMin, int perfectionMax,
        int salaryMin,     int salaryMax,
        EmployeeGrade maxGrade)
    {
        this.id           = id;
        this.employeeName = name;
        this.role         = role;
        this.maxGrade     = maxGrade;

        this.developMin    = developMin;    this.developMax    = developMax;
        this.planningMin   = planningMin;   this.planningMax   = planningMax;
        this.artMin        = artMin;        this.artMax        = artMax;
        this.perfectionMin = perfectionMin; this.perfectionMax = perfectionMax;
        this.salaryMin     = salaryMin;     this.salaryMax     = salaryMax;

        this.state             = EmployeeState.Idle;
        this.assignedProjectId = "";
    }

    public static EmployeeData FromServerRow(JsonData row)
    {
        var data = new EmployeeData(
            id:             row["id"].ToString(),
            name:           row["employeeName"].ToString(),
            role:           (EmployeeRole)SafeInt(row, "role", 0),
            developMin:     SafeInt(row, "developMin",    0),
            developMax:     SafeInt(row, "developMax",    0),
            planningMin:    SafeInt(row, "planningMin",   0),
            planningMax:    SafeInt(row, "planningMax",   0),
            artMin:         SafeInt(row, "artMin",        0),
            artMax:         SafeInt(row, "artMax",        0),
            perfectionMin:  SafeInt(row, "perfectionMin", 0),
            perfectionMax:  SafeInt(row, "perfectionMax", 0),
            salaryMin:      SafeInt(row, "salaryMin",     400),
            salaryMax:      SafeInt(row, "salaryMax",     500),
            maxGrade:       (EmployeeGrade)SafeInt(row, "maxGrade", 0)
        );

        data.rowInDate       = SafeString(row, "inDate", "");
        data.enhancementLevel = SafeInt(row, "enhancementLevel", 0);
        data.grade           = (EmployeeGrade)SafeInt(row, "grade", 0);
        data.potential       = (EmployeePotential)SafeInt(row, "potential", 0);
        data.developSkill    = SafeInt(row, "developSkill",    0);
        data.planningSkill   = SafeInt(row, "planningSkill",   0);
        data.artSkill        = SafeInt(row, "artSkill",        0);
        data.perfectionSkill = SafeInt(row, "perfectionSkill", 0);
        data.salary          = SafeInt(row, "salary",          0);
        data.state           = (EmployeeState)SafeInt(row, "state", 0);
        data.assignedProjectId = SafeString(row, "assignedProjectId", "");
        data.portraitId = SafeString(row, "portraitId", "portrait_secretary");

        return data;
    }

    public BackEnd.Param ToParam()
    {
        var param = new BackEnd.Param();
        param.Add("id",              id);
        param.Add("employeeName",    employeeName);
        param.Add("enhancementLevel", enhancementLevel);
        param.Add("role",            (int)role);
        param.Add("maxGrade",        (int)maxGrade);
        param.Add("grade",           (int)grade);
        param.Add("potential",       (int)potential);
        param.Add("developSkill",    developSkill);
        param.Add("planningSkill",   planningSkill);
        param.Add("artSkill",        artSkill);
        param.Add("perfectionSkill", perfectionSkill);
        param.Add("salary",          salary);
        param.Add("developMin",      developMin);
        param.Add("developMax",      developMax);
        param.Add("planningMin",     planningMin);
        param.Add("planningMax",     planningMax);
        param.Add("artMin",          artMin);
        param.Add("artMax",          artMax);
        param.Add("perfectionMin",   perfectionMin);
        param.Add("perfectionMax",   perfectionMax);
        param.Add("salaryMin",       salaryMin);
        param.Add("salaryMax",       salaryMax);
        param.Add("state",           (int)state);
        param.Add("assignedProjectId", assignedProjectId);
        param.Add("portraitId", portraitId);
        return param;
    }

    // ── 텍스트 헬퍼 ──────────────────────────
    public string DevelopRangeText()    => $"개발: {developMin}~{developMax}";
    public string PlanningRangeText()   => $"기획: {planningMin}~{planningMax}";
    public string ArtRangeText()        => $"아트: {artMin}~{artMax}";
    public string PerfectionRangeText() => $"완성도: {perfectionMin}~{perfectionMax}";
    public string SalaryRangeText()     => $"연봉: {salary:N0}G";
    public string SalaryText()          => $"연봉: {salary:N0}G";

    public string DevelopText()    => $"개발: {developSkill}";
    public string PlanningText()   => $"기획: {planningSkill}";
    public string ArtText()        => $"아트: {artSkill}";
    public string PerfectionText() => $"완성도: {perfectionSkill}";

    public string RoleToString() => role switch
    {
        EmployeeRole.Planner    => "기획자",
        EmployeeRole.Programmer => "프로그래머",
        EmployeeRole.Artist     => "아티스트",
        _ => ""
    };

    public string GradeToString() => grade switch
    {
        EmployeeGrade.Normal => "Normal",
        EmployeeGrade.Rare   => "Rare",
        EmployeeGrade.Epic   => "Epic",
        EmployeeGrade.Unique => "Unique",
        _ => ""
    };

    public string PotentialToString() => potential switch
    {
        EmployeePotential.F => "F",
        EmployeePotential.D => "D",
        EmployeePotential.C => "C",
        EmployeePotential.B => "B",
        EmployeePotential.A => "A",
        _ => ""
    };

    public string StateToString() => state switch
    {
        EmployeeState.Idle    => "대기중",
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