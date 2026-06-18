using System;
using System.Collections.Generic;
using LitJson;
using UnityEngine;

public enum EmployeeRole { Planner, Programmer, Artist }

[Serializable]
public struct StatBuffStack
{
    public int weeksLeft;
    public int percent; // 정수 퍼센트 (10 = 10%, 20 = 20%)
}

public enum EmployeePotential { C, B, A, S }
public enum EmployeeGrade { Normal, Rare, Epic, Unique, Legendary }
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
    public int creativitySkill;
    public int salary;
    public int enhancementLevel;
    public int hireCost;   // 채용 후보 전용 — 공개 시 확정된 채용 비용(재접속에도 고정). 보유 직원엔 미사용.
    public int satisfaction = 80;
    // 임시 능력치 디버프 — 발동마다 entry 추가, 각 entry 가 남은 주차. Count 가 곧 활성 디버프 개수.
    // 예: 감기(5주) + 번아웃(8주) → [5, 8] → 매주 -20% × 2 누적 적용.
    public List<int> statDebuffStacks = new();
    // 임시 능력치 버프 스택 — 발동마다 (남은 주차, 퍼센트) 추가. 합산해 한 번에 적용.
    // 예: 랜덤이벤트 20%(6주) + 각성의 물약 10%(4주) → 4주간 +30%, 이후 2주간 +20%.
    public List<StatBuffStack> statBuffStacks = new();
    public int romanceBuffWeeksLeft = 0; // 사내 연애 능력치 +10% 버프 남은 주차
    public int consecutiveLeaderCount    = 0; // 연속 팀장 선정 횟수
    public int consecutiveNonLeaderCount = 0; // 연속 팀장 미선정 횟수
    public bool isOvertimeWorker = false;     // 이번 프로젝트 야근 여부 (프로젝트 단위 리셋)
    // 만족도 < 40 진입 시 base skill ×0.8 영구 감산을 1회만 적용하기 위한 플래그.
    // 만족도가 40 이상으로 회복되면 false 로 리셋되어 다음 진입 때 재적용 가능.
    public bool lowSatisfactionPenaltyApplied = false;

    // ── 범위 수치 ─────────────────────────────
    public int developMin, developMax;
    public int planningMin, planningMax;
    public int artMin, artMax;
    public int creativityMin, creativityMax;
    public int salaryMin, salaryMax;

    // 강화로 인한 주스탯/부스탯 증가 (표시용). 부스탯 0.4 고정이라 min==max.
    public int mainStatEnhanceGain;
    public int subStatEnhanceMin;
    public int subStatEnhanceMax;

    public string assignedDeskId = "";
    public string masterEmployeeId = ""; // 마스터 풀의 원본 ID (emp_01 등)
    public bool isDefault; // true = 항상 채용 풀 등장, false = 획득 필요
    public int hiredYear; // 채용된 게임 연도 (연봉협상 첫해 제외 조건에 사용)
    public bool isFemale; // 여직원 여부 (연봉협상 특수 대사 조건, EmployeeMasterData 차트 isFemale 컬럼 필요)

    // ── 캐릭터별 등급 특징 (CEO 특성과 별개, 마스터 데이터 귀속) ──
    // 발동 기준은 채용 시 roll 된 현재 grade. 누적: Epic 이상 → 특성, Unique 이상 → 특성 + 전용 이벤트.
    public string epicTraitId    = ""; // 캐릭터 고유 특성 ID (grade >= Epic 일 때 활성). CharacterTrait_Chart 참조.
    public string uniqueEventType = ""; // 캐릭터 전용 이벤트 타입 (grade >= Unique 일 때 발동 후보). CharacterUniqueEvent 차트 참조.

    // ── 캐릭터 특성/이벤트 런타임 상태 (특성 스크립트와 이벤트 스크립트가 공유, 영속화) ──
    // 캐릭터별 효과 구현 시 사용. 필요한 캐릭터가 생기면 여기에 필드 추가.
    public string otakuFixedGenre        = "";  // 오타쿠: 채용 시 1회 고정된 선호 장르 (특성+버튜버 이벤트 공유)
    public int    lastUniqueEventYear    = -1;  // 연 1회 전용 이벤트(금수저/우기 등) 마지막 발동 연도
    public int    glassMentalCooldownWeeks = 0; // 김아무개 유리멘탈 회복: 발동 후 재발동 금지 남은 주차 (>0 = 대기중)
    public int    cosmicEnergyPercent      = 100; // 우기 우주의 기운: 이번 주 능력치 배율(%) 70~150, 매주 WeeklyTick 재추첨. 비-우기/비활성은 100(영향 없음)
    // ── 신의 축복(우기 전용 이벤트, UgiUnique) — 발동 시 효과가 "다음 축복까지" 지속(연 1회). CharacterUniqueEvents 가 set/clear ──
    public bool   cosmicFrozen           = false; // 주사위 2: 우주의 기운 매주 재추첨을 정지하고 cosmicEnergyPercent 를 고정값(100~130)으로 유지
    public int    godBlessingStatPercent = 0;     // 주사위 3: 주스탯 % 버프(다음 축복까지 유지 — statBuffStacks 와 달리 주차 감소 없음)
    public bool   godBlessingSalesActive = false; // 주사위 6: 매출 +10%(다음 축복까지). SalesUI 가 GetGodBlessingSalesBonus 로 소비

    public EmployeeState state;
    public string assignedProjectId;
    public string portraitId;

    // CEO 표시 플래그 — 메모리 only (ToParam/FromServerRow 직렬화 안 함)
    // ownedEmployees 에 안 들어가서 만족도/아이템/이벤트/연봉/카운트 자동 제외, 팀장 후보로만 노출.
    [NonSerialized] public bool isCEO;

    // 마스터 데이터 복사본 생성 (채용 후보 표시용)
    public EmployeeData Clone() => new EmployeeData(
        id, employeeName, role,
        developMin, developMax,
        planningMin, planningMax,
        artMin, artMax,
        creativityMin, creativityMax,
        salaryMin, salaryMax,
        maxGrade
    ) { portraitId = this.portraitId, isDefault = this.isDefault, isFemale = this.isFemale,
        epicTraitId = this.epicTraitId, uniqueEventType = this.uniqueEventType };

    // ── 생성자 (EmployeePool 마스터 데이터용) ──
    public EmployeeData(string id, string name, EmployeeRole role,
        int developMin, int developMax,
        int planningMin, int planningMax,
        int artMin, int artMax,
        int creativityMin, int creativityMax,
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
        this.creativityMin = creativityMin; this.creativityMax = creativityMax;
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
            creativityMin: SafeInt(row, "creativityMin", 0),
            creativityMax: SafeInt(row, "creativityMax", 0),
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
        data.creativitySkill = SafeInt(row, "creativitySkill", 0);
        data.salary = SafeInt(row, "salary", 0);
        data.state = (EmployeeState)SafeInt(row, "state", 0);
        data.assignedProjectId = SafeString(row, "assignedProjectId", "");
        data.portraitId = SafeString(row, "portraitId", "portrait_secretary");
        data.satisfaction = SafeInt(row, "satisfaction", 80);
        data.assignedDeskId = SafeString(row, "assignedDeskId", "");
        data.masterEmployeeId = SafeString(row, "masterEmployeeId", "");
        data.hiredYear = SafeInt(row, "hiredYear", 0);
        data.isFemale  = SafeBool(row, "isFemale", false);
        data.epicTraitId     = SafeString(row, "epicTraitId", "");
        data.uniqueEventType = SafeString(row, "uniqueEventType", "");
        data.otakuFixedGenre          = SafeString(row, "otakuFixedGenre", "");
        data.lastUniqueEventYear      = SafeInt(row, "lastUniqueEventYear", -1);
        data.glassMentalCooldownWeeks = SafeInt(row, "glassMentalCooldownWeeks", 0);
        data.cosmicEnergyPercent      = SafeInt(row, "cosmicEnergyPercent", 100);
        data.cosmicFrozen             = SafeBool(row, "cosmicFrozen", false);
        data.godBlessingStatPercent   = SafeInt(row, "godBlessingStatPercent", 0);
        data.godBlessingSalesActive   = SafeBool(row, "godBlessingSalesActive", false);
        ParseStatDebuffStacks(data, row);
        ParseStatBuffStacks(data, row);
        data.romanceBuffWeeksLeft      = SafeInt(row, "romanceBuffWeeksLeft",      0);
        data.consecutiveLeaderCount    = SafeInt(row, "consecutiveLeaderCount",    0);
        data.consecutiveNonLeaderCount = SafeInt(row, "consecutiveNonLeaderCount", 0);
        data.isOvertimeWorker          = SafeBool(row, "isOvertimeWorker",          false);
        data.lowSatisfactionPenaltyApplied = SafeBool(row, "lowSatisfactionPenaltyApplied", false);
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
        param.Add("creativitySkill", creativitySkill);
        param.Add("salary", salary);
        param.Add("developMin", developMin);
        param.Add("developMax", developMax);
        param.Add("planningMin", planningMin);
        param.Add("planningMax", planningMax);
        param.Add("artMin", artMin);
        param.Add("artMax", artMax);
        param.Add("creativityMin", creativityMin);
        param.Add("creativityMax", creativityMax);
        param.Add("salaryMin", salaryMin);
        param.Add("salaryMax", salaryMax);
        param.Add("state", (int)state);
        param.Add("assignedProjectId", assignedProjectId);
        param.Add("portraitId", portraitId);
        param.Add("satisfaction", satisfaction);
        param.Add("assignedDeskId", assignedDeskId);
        param.Add("masterEmployeeId", masterEmployeeId);
        param.Add("hiredYear", hiredYear);
        param.Add("isFemale",  isFemale);
        param.Add("epicTraitId",     epicTraitId);
        param.Add("uniqueEventType", uniqueEventType);
        param.Add("otakuFixedGenre",          otakuFixedGenre);
        param.Add("lastUniqueEventYear",      lastUniqueEventYear);
        param.Add("glassMentalCooldownWeeks", glassMentalCooldownWeeks);
        param.Add("cosmicEnergyPercent",      cosmicEnergyPercent);
        param.Add("cosmicFrozen",             cosmicFrozen);
        param.Add("godBlessingStatPercent",   godBlessingStatPercent);
        param.Add("godBlessingSalesActive",   godBlessingSalesActive);
        param.Add("statDebuffStacks",       string.Join(",", statDebuffStacks));
        param.Add("statBuffStacks",         SerializeStatBuffStacks(statBuffStacks));
        param.Add("romanceBuffWeeksLeft",   romanceBuffWeeksLeft);
        param.Add("consecutiveLeaderCount",    consecutiveLeaderCount);
        param.Add("consecutiveNonLeaderCount", consecutiveNonLeaderCount);
        param.Add("isOvertimeWorker",          isOvertimeWorker);
        param.Add("lowSatisfactionPenaltyApplied", lowSatisfactionPenaltyApplied);
        return param;
    }

    // ── 텍스트 헬퍼 ──────────────────────────
    public string DevelopRangeText() => $"개발: {developMin}~{developMax}";
    public string PlanningRangeText() => $"기획: {planningMin}~{planningMax}";
    public string ArtRangeText() => $"아트: {artMin}~{artMax}";
    public string SalaryRangeText() => $"연봉: {salary:N0}G";
    public string SalaryText() => $"연봉: {salary:N0}G";

    public float GetSatisfactionMultiplier()
    {
        if (isCEO) return 1.0f; // CEO 는 만족도 시스템 적용 안 함

        float mult;
        // 유리멘탈(김아무개) 특성: 만족도 구간 배율이 일반 직원보다 극단적.
        // 91~100 +30% / 81~90 +15% / 61~80 중립 / 60 이하 -20%.
        if (CharacterTraitApplier.IsGlassMental(this))
        {
            if      (satisfaction >= 91) mult = 1.3f;
            else if (satisfaction >= 81) mult = 1.15f;
            else if (satisfaction >= 61) mult = 1.0f;
            else                         mult = 0.8f; // 60 이하 (41~60 및 40 이하 모두)
        }
        else
        {
            if      (satisfaction >= 81) mult = 1.1f;
            else if (satisfaction >= 61) mult = 1.0f;
            else if (satisfaction >= 41) mult = 0.9f;
            else                         mult = 0.8f;
        }

        // 장착 특성 'b3'(81+, +5%) / 'a4'(80+, +10%) — 고만족 시 스탯 배율 추가 가산 (임계값별 합산).
        mult += TraitEffectApplier.GetHighSatStatBonus(satisfaction);

        return mult;
    }

    public void ApplyStatDebuff(int weeks)
    {
        if (weeks <= 0) return;
        statDebuffStacks.Add(weeks); // 누적 — 발동마다 새 entry
    }

    public void ApplyStatBuff(int weeks, int percent)
    {
        if (weeks <= 0 || percent <= 0) return;
        statBuffStacks.Add(new StatBuffStack { weeksLeft = weeks, percent = percent });
    }

    // 라꾸라꾸 등으로 모든 디버프 스택 즉시 회복
    public void ClearAllStatDebuffs() => statDebuffStacks.Clear();
    public bool HasAnyStatDebuff() => statDebuffStacks.Count > 0;

    // 주 스탯 기준 합연산 디버프/버프량 — 디버프는 활성 스택 개수만큼 누적 감산, 버프는 스택 percent 합
    public int GetStatDebuffAmount()    => statDebuffStacks.Count > 0 ? (int)(GetMainStat() * 0.2f) * statDebuffStacks.Count : 0;
    public int GetStatBuffAmount()
    {
        if (statBuffStacks.Count == 0) return 0;
        int totalPercent = 0;
        for (int i = 0; i < statBuffStacks.Count; i++) totalPercent += statBuffStacks[i].percent;
        return (int)(GetMainStat() * totalPercent / 100f);
    }
    public int GetRomanceBuffAmount()   => romanceBuffWeeksLeft  > 0 ? (int)(GetMainStat() * 0.1f) : 0;
    // 신의 축복 주사위 3 — 주스탯 % 버프(다음 축복까지). 주차 감소 없는 영속 버프라 statBuff 와 별도.
    // grade>=Unique 우기가 있어야만 유효 — 우기 부재(해고/도주/미보유) 시 즉시 0 (HasUniqueUgi 게이팅).
    public int GetGodBlessingBuffAmount()
        => (godBlessingStatPercent > 0 && CharacterUniqueEvents.HasUniqueUgi())
           ? (int)(GetMainStat() * godBlessingStatPercent / 100f) : 0;

    // 우기 우주의 기운: 이번 주 능력치 배율 (비-우기/비활성은 100 → ×1.0, 영향 없음).
    // Effective*Skill 의 외곽 곱으로 적용 → (raw → 만족도 → ±버프/디버프) × 우주배율 순서.
    public float GetCosmicMultiplier() => cosmicEnergyPercent / 100f;

    public int EffectivePlanningSkill   => Mathf.RoundToInt(((int)(planningSkill   * GetSatisfactionMultiplier()) - (role == EmployeeRole.Planner    ? GetStatDebuffAmount() : 0) + (role == EmployeeRole.Planner    ? GetStatBuffAmount() : 0) + (role == EmployeeRole.Planner    ? GetRomanceBuffAmount() : 0) + (role == EmployeeRole.Planner    ? GetGodBlessingBuffAmount() : 0)) * GetCosmicMultiplier());
    public int EffectiveDevelopSkill    => Mathf.RoundToInt(((int)(developSkill    * GetSatisfactionMultiplier()) - (role == EmployeeRole.Programmer ? GetStatDebuffAmount() : 0) + (role == EmployeeRole.Programmer ? GetStatBuffAmount() : 0) + (role == EmployeeRole.Programmer ? GetRomanceBuffAmount() : 0) + (role == EmployeeRole.Programmer ? GetGodBlessingBuffAmount() : 0)) * GetCosmicMultiplier());
    public int EffectiveArtSkill        => Mathf.RoundToInt(((int)(artSkill        * GetSatisfactionMultiplier()) - (role == EmployeeRole.Artist     ? GetStatDebuffAmount() : 0) + (role == EmployeeRole.Artist     ? GetStatBuffAmount() : 0) + (role == EmployeeRole.Artist     ? GetRomanceBuffAmount() : 0) + (role == EmployeeRole.Artist     ? GetGodBlessingBuffAmount() : 0)) * GetCosmicMultiplier());
    public int EffectiveCreativitySkill => Mathf.RoundToInt((int)(creativitySkill * GetSatisfactionMultiplier()) * GetCosmicMultiplier());

    public string DevelopText()    => $"개발: {EffectiveDevelopSkill}";
    public string PlanningText()   => $"기획: {EffectivePlanningSkill}";
    public string ArtText()        => $"아트: {EffectiveArtSkill}";
    public string CreativityText() => $"창의성: {EffectiveCreativitySkill}";

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

    // 등급별 최대 강화 레벨 — EmployeeEnhancement.GetMaxLevel 와 단일 소스 공유.
    public static int MaxEnhancementForGrade(EmployeeGrade g) => g switch
    {
        EmployeeGrade.Normal    => 15,
        EmployeeGrade.Rare      => 15,
        EmployeeGrade.Epic      => 20,
        EmployeeGrade.Unique    => 25,
        EmployeeGrade.Legendary => 30,
        _                       => 15
    };
    public int MaxEnhancementLevel => MaxEnhancementForGrade(grade);

    public string RoleToString()
    {
        if (isCEO) return "CEO";
        return role switch
        {
            EmployeeRole.Planner => "기획자",
            EmployeeRole.Programmer => "프로그래머",
            EmployeeRole.Artist => "아티스트",
            _ => ""
        };
    }

    public string GradeToString()
    {
        if (isCEO) return ""; // CEO 는 등급 표시 없음
        return grade switch
        {
            EmployeeGrade.Normal => "Normal",
            EmployeeGrade.Rare => "Rare",
            EmployeeGrade.Epic => "Epic",
            EmployeeGrade.Unique => "Unique",
            EmployeeGrade.Legendary => "Legendary",
            _ => ""
        };
    }


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
    // 신규 컬럼 statDebuffStacks (CSV) 우선, 없으면 구버전 statDebuffWeeksLeft (단일 int) 를 단일 스택으로 변환
    static void ParseStatDebuffStacks(EmployeeData data, JsonData row)
    {
        data.statDebuffStacks.Clear();
        string csv = SafeString(row, "statDebuffStacks", "");
        if (!string.IsNullOrEmpty(csv))
        {
            foreach (var s in csv.Split(','))
                if (int.TryParse(s, out int w) && w > 0) data.statDebuffStacks.Add(w);
            return;
        }
        int legacy = SafeInt(row, "statDebuffWeeksLeft", 0);
        if (legacy > 0) data.statDebuffStacks.Add(legacy);
    }

    // 신규 컬럼 statBuffStacks ("weeks:percent,weeks:percent") 우선,
    // 없으면 구버전 statBuffWeeksLeft (단일 int, 20% 고정) 를 단일 스택으로 변환.
    static void ParseStatBuffStacks(EmployeeData data, JsonData row)
    {
        data.statBuffStacks.Clear();
        string csv = SafeString(row, "statBuffStacks", "");
        if (!string.IsNullOrEmpty(csv))
        {
            foreach (var entry in csv.Split(','))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;
                if (int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int p) && w > 0 && p > 0)
                    data.statBuffStacks.Add(new StatBuffStack { weeksLeft = w, percent = p });
            }
            return;
        }
        int legacyWeeks = SafeInt(row, "statBuffWeeksLeft", 0);
        if (legacyWeeks > 0)
            data.statBuffStacks.Add(new StatBuffStack { weeksLeft = legacyWeeks, percent = 20 });
    }

    static string SerializeStatBuffStacks(List<StatBuffStack> stacks)
    {
        if (stacks == null || stacks.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < stacks.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(stacks[i].weeksLeft).Append(':').Append(stacks[i].percent);
        }
        return sb.ToString();
    }

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

    // 역할별 주스탯 반환 (연봉협상 대상 선정에 사용) — raw 값
    public int GetMainStat() => role switch
    {
        EmployeeRole.Planner    => planningSkill,
        EmployeeRole.Programmer => developSkill,
        EmployeeRole.Artist     => artSkill,
        _ => 0
    };

    // 역할별 주스탯의 Effective 값 (만족도 배율 + 임시 버프/디버프 스택 + 사내연애 반영).
    // 개발 틱 누적·팀장 점수 산출이 이 값을 사용해 표시 능력치와 실제 산출을 일치시킨다. (오타쿠 ×1.2 는 호출부에서 별도 적용)
    public int GetEffectiveMainStat() => role switch
    {
        EmployeeRole.Planner    => EffectivePlanningSkill,
        EmployeeRole.Programmer => EffectiveDevelopSkill,
        EmployeeRole.Artist     => EffectiveArtSkill,
        _ => 0
    };

    // 원래 수치 대비 현재 수치로 색상 결정 (버프=#E63356 / 디버프=#517FFF / 같으면 흰색)
    static readonly Color StatBuffColor   = new Color(0.902f, 0.200f, 0.337f); // #E63356
    static readonly Color StatDebuffColor = new Color(0.318f, 0.498f, 1.000f); // #517FFF
    public static Color GetStatColor(int baseSkill, int effectiveSkill)
    {
        if (effectiveSkill > baseSkill) return StatBuffColor;
        if (effectiveSkill < baseSkill) return StatDebuffColor;
        return Color.white;
    }

    // 만족도 변경 (1~100 클램프)
    public void ChangeSatisfaction(int amount)
    {
        // 파견중(사무실 부재) 직원은 이벤트성 만족도 변화 동결 — 파견 출발 시점 값 유지.
        // (주기 만족도 하락은 EmployeeManager 가 직접 대입 + 자체 IsDispatched 가드 / 복귀 보상 RecoverSatisfaction 은 직접 대입이라 영향 없음)
        if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(id)) return;
        satisfaction = Mathf.Clamp(satisfaction + amount, 1, 100);
    }
}