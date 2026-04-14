using System;
using LitJson;
using UnityEngine;

public enum EmployeeRole { Planner, Programmer, Artist }
public enum EmployeePotential { C, B, A, S }
public enum EmployeeGrade { Normal, Rare, Epic, Unique }
public enum EmployeeState { Idle, Working }
// enum 추가
public enum SatisfactionState
{
    VeryHappy,  // 90이상 - 매우 만족중
    Happy,      // 80~90 - 만족중
    Neutral,    // 70~80 - 보통
    Unhappy,    // 60~70 - 불만
    VeryUnhappy // 50이하 - 매우불만
}

[Serializable]
public class EmployeeData
{
    public string id;
    public string rowInDate;

    public string employeeName;
    public EmployeeRole role;
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
    public int satisfaction = 90;

    // ── 범위 수치 ─────────────────────────────
    public int developMin, developMax;
    public int planningMin, planningMax;
    public int artMin, artMax;
    public int perfectionMin, perfectionMax;
    public int salaryMin, salaryMax;

    // 강화로 인한 주스탯/부스탯 증가 (표시용)
    public int mainStatEnhanceGain;
    public int subStatEnhanceMin;
    public int subStatEnhanceMax;

    // 강화 단계별 실제 적용 수치 기록 (하락 시 정확한 롤백용)
    public string enhancementRecordsJson = "[]";

    public string assignedDeskId = "";
    public string masterEmployeeId = ""; // 마스터 풀의 원본 ID (emp_01 등)
    public bool isDefault; // true = 항상 채용 풀 등장, false = 획득 필요

    public EmployeeState state;
    public string assignedProjectId;
    public string portraitId;

    // 마스터 데이터 복사본 생성 (채용 후보 표시용)
    public EmployeeData Clone() => new EmployeeData(
        id, employeeName, role,
        developMin, developMax,
        planningMin, planningMax,
        artMin, artMax,
        perfectionMin, perfectionMax,
        salaryMin, salaryMax,
        maxGrade
    ) { portraitId = this.portraitId, isDefault = this.isDefault };

    // ── 생성자 (EmployeePool 마스터 데이터용) ──
    public EmployeeData(string id, string name, EmployeeRole role,
        int developMin, int developMax,
        int planningMin, int planningMax,
        int artMin, int artMax,
        int perfectionMin, int perfectionMax,
        int salaryMin, int salaryMax,
        EmployeeGrade maxGrade)
    {
        this.id = id;
        this.employeeName = name;
        this.role = role;
        this.maxGrade = maxGrade;

        this.developMin = developMin; this.developMax = developMax;
        this.planningMin = planningMin; this.planningMax = planningMax;
        this.artMin = artMin; this.artMax = artMax;
        this.perfectionMin = perfectionMin; this.perfectionMax = perfectionMax;
        this.salaryMin = salaryMin; this.salaryMax = salaryMax;

        this.state = EmployeeState.Idle;
        this.assignedProjectId = "";
    }

    public static EmployeeData FromServerRow(JsonData row)
    {
        var data = new EmployeeData(
            id: row["id"].ToString(),
            name: row["employeeName"].ToString(),
            role: (EmployeeRole)SafeInt(row, "role", 0),
            developMin: SafeInt(row, "developMin", 0),
            developMax: SafeInt(row, "developMax", 0),
            planningMin: SafeInt(row, "planningMin", 0),
            planningMax: SafeInt(row, "planningMax", 0),
            artMin: SafeInt(row, "artMin", 0),
            artMax: SafeInt(row, "artMax", 0),
            perfectionMin: SafeInt(row, "perfectionMin", 0),
            perfectionMax: SafeInt(row, "perfectionMax", 0),
            salaryMin: SafeInt(row, "salaryMin", 400),
            salaryMax: SafeInt(row, "salaryMax", 500),
            maxGrade: (EmployeeGrade)SafeInt(row, "maxGrade", 0)

        );

        data.rowInDate = SafeString(row, "inDate", "");
        data.enhancementLevel = SafeInt(row, "enhancementLevel", 0);
        data.grade = (EmployeeGrade)SafeInt(row, "grade", 0);
        data.potential = (EmployeePotential)SafeInt(row, "potential", 0);
        data.developSkill = SafeInt(row, "developSkill", 0);
        data.planningSkill = SafeInt(row, "planningSkill", 0);
        data.artSkill = SafeInt(row, "artSkill", 0);
        data.perfectionSkill = SafeInt(row, "perfectionSkill", 0);
        data.salary = SafeInt(row, "salary", 0);
        data.state = (EmployeeState)SafeInt(row, "state", 0);
        data.assignedProjectId = SafeString(row, "assignedProjectId", "");
        data.portraitId = SafeString(row, "portraitId", "portrait_secretary");
        data.satisfaction = SafeInt(row, "satisfaction", 90);
        data.assignedDeskId = SafeString(row, "assignedDeskId", "");
        data.masterEmployeeId = SafeString(row, "masterEmployeeId", "");
        data.enhancementRecordsJson = SafeString(row, "enhancementRecordsJson", "[]");
        return data;
    }

    public BackEnd.Param ToParam()
    {
        var param = new BackEnd.Param();
        param.Add("id", id);
        param.Add("employeeName", employeeName);
        param.Add("enhancementLevel", enhancementLevel);
        param.Add("role", (int)role);
        param.Add("maxGrade", (int)maxGrade);
        param.Add("grade", (int)grade);
        param.Add("potential", (int)potential);
        param.Add("developSkill", developSkill);
        param.Add("planningSkill", planningSkill);
        param.Add("artSkill", artSkill);
        param.Add("perfectionSkill", perfectionSkill);
        param.Add("salary", salary);
        param.Add("developMin", developMin);
        param.Add("developMax", developMax);
        param.Add("planningMin", planningMin);
        param.Add("planningMax", planningMax);
        param.Add("artMin", artMin);
        param.Add("artMax", artMax);
        param.Add("perfectionMin", perfectionMin);
        param.Add("perfectionMax", perfectionMax);
        param.Add("salaryMin", salaryMin);
        param.Add("salaryMax", salaryMax);
        param.Add("state", (int)state);
        param.Add("assignedProjectId", assignedProjectId);
        param.Add("portraitId", portraitId);
        param.Add("satisfaction", satisfaction);
        param.Add("assignedDeskId", assignedDeskId);
        param.Add("masterEmployeeId", masterEmployeeId);
        param.Add("enhancementRecordsJson", enhancementRecordsJson);
        return param;
    }

    // ── 텍스트 헬퍼 ──────────────────────────
    public string DevelopRangeText() => $"개발: {developMin}~{developMax}";
    public string PlanningRangeText() => $"기획: {planningMin}~{planningMax}";
    public string ArtRangeText() => $"아트: {artMin}~{artMax}";
    public string PerfectionRangeText() => $"완성도: {perfectionMin}~{perfectionMax}";
    public string SalaryRangeText() => $"연봉: {salary:N0}G";
    public string SalaryText() => $"연봉: {salary:N0}G";

    public string DevelopText() => $"개발: {developSkill}";
    public string PlanningText() => $"기획: {planningSkill}";
    public string ArtText() => $"아트: {artSkill}";
    public string PerfectionText() => $"완성도: {perfectionSkill}";

    // 주스탯은 강화 반영 범위, 부스탯은 확정 수치로 표시
    public string DevelopDisplayText()  => role == EmployeeRole.Programmer
        ? $"개발: {developMin + mainStatEnhanceGain}~{developMax + mainStatEnhanceGain}"
        : DevelopText();
    public string PlanningDisplayText() => role == EmployeeRole.Planner
        ? $"기획: {planningMin + mainStatEnhanceGain}~{planningMax + mainStatEnhanceGain}"
        : PlanningText();
    public string ArtDisplayText()      => role == EmployeeRole.Artist
        ? $"아트: {artMin + mainStatEnhanceGain}~{artMax + mainStatEnhanceGain}"
        : ArtText();
    public string SatisfactionText() => $"만족도: {satisfaction}";

    public string RoleToString() => role switch
    {
        EmployeeRole.Planner => "기획자",
        EmployeeRole.Programmer => "프로그래머",
        EmployeeRole.Artist => "아티스트",
        _ => ""
    };

    public string GradeToString() => grade switch
    {
        EmployeeGrade.Normal => "Normal",
        EmployeeGrade.Rare => "Rare",
        EmployeeGrade.Epic => "Epic",
        EmployeeGrade.Unique => "Unique",
        _ => ""
    };

    public string PotentialToString() => potential switch
    {
        EmployeePotential.C => "C",
        EmployeePotential.B => "B",
        EmployeePotential.A => "A",
        EmployeePotential.S => "S",
        _ => ""
    };

    public string StateToString() => state switch
    {
        EmployeeState.Idle => "대기중",
        EmployeeState.Working => "근무중",
        _ => ""
    };
    public SatisfactionState GetSatisfactionState()
    {
        if (satisfaction >= 90) return SatisfactionState.VeryHappy;
        if (satisfaction >= 80) return SatisfactionState.Happy;
        if (satisfaction >= 70) return SatisfactionState.Neutral;
        if (satisfaction >= 60) return SatisfactionState.Unhappy;
        return SatisfactionState.VeryUnhappy;
    }
    public string SatisfactionToString() => GetSatisfactionState() switch
    {
        SatisfactionState.VeryHappy => "매우 만족중",
        SatisfactionState.Happy => "만족중",
        SatisfactionState.Neutral => "보통",
        SatisfactionState.Unhappy => "불만",
        SatisfactionState.VeryUnhappy => "매우 불만",
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
    static bool SafeBool(JsonData row, string key, bool defaultValue)
    {
        try { return bool.Parse(row[key].ToString()); }
        catch { return defaultValue; }
    }

    // 만족도 변경 (1~100 클램프)
    public void ChangeSatisfaction(int amount)
    {
        satisfaction = Mathf.Clamp(satisfaction + amount, 1, 100);
    }
}