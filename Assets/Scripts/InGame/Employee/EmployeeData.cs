using System;
using LitJson;

public enum EmployeeRole  { Planner, Programmer, Artist }
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

    // ── 확정 수치 (인게임) ────────────────────
    public int developSkill;
    public int planningSkill;
    public int artSkill;

    // ── 범위 수치 (채용 UI 표시용) ────────────
    public int developMin;  public int developMax;
    public int planningMin; public int planningMax;
    public int artMin;      public int artMax;

    public EmployeeState state;
    public string assignedProjectId;

    // ── 생성자 (범위 포함) ────────────────────
    public EmployeeData(string id, string name, EmployeeRole role, EmployeeGrade grade,
        int developMin, int developMax,
        int planningMin, int planningMax,
        int artMin, int artMax)
    {
        this.id           = id;
        this.employeeName = name;
        this.role         = role;
        this.grade        = grade;

        this.developMin  = developMin;  this.developMax  = developMax;
        this.planningMin = planningMin; this.planningMax = planningMax;
        this.artMin      = artMin;      this.artMax      = artMax;

        // 확정 수치는 범위 내 랜덤
        this.developSkill  = UnityEngine.Random.Range(developMin,  developMax  + 1);
        this.planningSkill = UnityEngine.Random.Range(planningMin, planningMax + 1);
        this.artSkill      = UnityEngine.Random.Range(artMin,      artMax      + 1);

        this.state              = EmployeeState.Idle;
        this.assignedProjectId  = "";
    }

    // ── 서버 → EmployeeData 변환 ──────────────
public static EmployeeData FromServerRow(JsonData row)
{
    var data = new EmployeeData(
        id:          row["id"].ToString(),
        name:        row["employeeName"].ToString(),
        role:        (EmployeeRole) int.Parse(row["role"].ToString()),
        grade:       (EmployeeGrade)int.Parse(row["grade"].ToString()),
        developMin:  int.Parse(row["developMin"].ToString()),
        developMax:  int.Parse(row["developMax"].ToString()),
        planningMin: int.Parse(row["planningMin"].ToString()),
        planningMax: int.Parse(row["planningMax"].ToString()),
        artMin:      int.Parse(row["artMin"].ToString()),
        artMax:      int.Parse(row["artMax"].ToString())
    );

    // 생성자에서 랜덤으로 만든 수치를 서버 저장값으로 덮어쓰기
    data.developSkill  = int.Parse(row["developSkill"].ToString());
    data.planningSkill = int.Parse(row["planningSkill"].ToString());
    data.artSkill      = int.Parse(row["artSkill"].ToString());

    data.rowInDate         = row["inDate"].ToString();
    data.state             = (EmployeeState)int.Parse(row["state"].ToString());
    data.assignedProjectId = row["assignedProjectId"].ToString();

    return data;
}

    // ── 서버 저장용 Param ─────────────────────
    public BackEnd.Param ToParam()
    {
        var param = new BackEnd.Param();
        param.Add("id",                id);
        param.Add("employeeName",      employeeName);
        param.Add("role",              (int)role);
        param.Add("grade",             (int)grade);
        param.Add("developSkill",      developSkill);
        param.Add("planningSkill",     planningSkill);
        param.Add("artSkill",          artSkill);
        param.Add("developMin",        developMin);
        param.Add("developMax",        developMax);
        param.Add("planningMin",       planningMin);
        param.Add("planningMax",       planningMax);
        param.Add("artMin",            artMin);
        param.Add("artMax",            artMax);
        param.Add("state",             (int)state);
        param.Add("assignedProjectId", assignedProjectId);
        return param;
    }

    // ── 범위 표시용 ───────────────────────────
    public string DevelopRangeText()  => $"개발: {developMin}~{developMax}";
    public string PlanningRangeText() => $"기획: {planningMin}~{planningMax}";
    public string ArtRangeText()      => $"아트: {artMin}~{artMax}";

    // ── 확정 수치 표시용 ──────────────────────
    public string DevelopText()  => $"개발: {developSkill}";
    public string PlanningText() => $"기획: {planningSkill}";
    public string ArtText()      => $"아트: {artSkill}";

    public string RoleToString() => role switch
    {
        EmployeeRole.Planner    => "기획자",
        EmployeeRole.Programmer => "프로그래머",
        EmployeeRole.Artist     => "아티스트",
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
        EmployeeState.Idle    => "대기중",
        EmployeeState.Working => "근무중",
        _ => ""
    };
}