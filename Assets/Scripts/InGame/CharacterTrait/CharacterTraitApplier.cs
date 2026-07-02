using UnityEngine;
using TMPro;

// 캐릭터별 등급 특성의 인게임 효과 디스패처 (CEO 특성 TraitEffectApplier 와 별개)
//
// 발동 기준: EmployeeData.grade >= Epic (채용 시 roll 된 현재 등급). 누적 — Unique/Legendary 도 특성 보유.
// 데이터: EmployeeData.epicTraitId → CharacterTraitChartLoader.Cache.
// 공유 상태: EmployeeData 의 otakuFixedGenre 등 런타임 필드 (특수이벤트 CharacterUniqueEvents 와 공유).
//
// 구조: traitId 별로 분기. 효과는 발동 "시점" 이 제각각이라 시스템별 hook 진입점을 둠.
//   - OnHire(emp)        : 채용 직후 1회        (오타쿠 = 선호 장르 고정 / 우기 = 초기 배율)  ← EmployeeManager.HireEmployee
//   - WeeklyTick(emp)    : 매주                 (우주의 기운 재추첨. 유리멘탈은 패시브)        ← EmployeeManager.OnWeekPassed
//   - 팀장 점수 계산 시   : 게으른 천재(×1.3) / 훈수쟁이(10%) / 오타쿠(×1.2)              ← DevelopmentManager.SetLeader
//   - 연봉/협상 시        : 금수저(절반 + 협상 제외)                                       ← EmployeeManager / SalaryNegotiationManager
//   - 매출 계산 시        : 오타쿠 장르 보너스(+20%)                                       ← SalesUI.bonusSum
//   효과 명세는 [[project_character_trait_event_spec]] 참조.
//
// 등급 게이팅 (확정): GetActiveTrait 는 grade>=Epic 요구 유지. CSV maxGrade 는 dead([[feedback_max_grade_source]]) —
//    실제 maxGrade 는 cardsJson derived 라 Normal/Rare 표기 캐릭터도 카드로 Epic 도달 가능 → maxGrade 무시.
public static class CharacterTraitApplier
{
    // masterEmployeeId → (특성ID, 전용이벤트ID) fallback.
    // 뒤끝 EmployeeMasterData 의 epicTraitId/uniqueEventType 컬럼이 비어 있어도(미업로드/구버전 저장)
    // masterEmployeeId 로 직접 매핑해 동작하게 함. 뒤끝 컬럼 값이 있으면 그 값을 우선.
    static readonly System.Collections.Generic.Dictionary<string, (string trait, string evt)> Directory = new()
    {
        ["kim_01"]       = ("ctrait_kim",       "KimUnique"),
        ["otaku_01"]     = ("ctrait_otaku",     "OtakuUnique"),
        ["goldspoon_01"] = ("ctrait_goldspoon", "GoldspoonUnique"),
        ["ugi_01"]       = ("ctrait_ugi",       "UgiUnique"),
        ["genius_01"]    = ("ctrait_genius",    "GeniusUnique"),
        ["hunsu_01"]     = ("ctrait_hunsu",     "HunsuUnique"),
    };

    // 직원의 실효 특성ID — epicTraitId 우선, 없으면 masterEmployeeId 로 fallback.
    public static string ResolveTraitId(EmployeeData emp)
    {
        if (emp == null) return "";
        if (!string.IsNullOrEmpty(emp.epicTraitId)) return emp.epicTraitId;
        return (emp.masterEmployeeId != null && Directory.TryGetValue(emp.masterEmployeeId, out var v)) ? v.trait : "";
    }

    // 직원의 실효 전용이벤트ID — uniqueEventType 우선, 없으면 masterEmployeeId 로 fallback.
    public static string ResolveEventType(EmployeeData emp)
    {
        if (emp == null) return "";
        if (!string.IsNullOrEmpty(emp.uniqueEventType)) return emp.uniqueEventType;
        return (emp.masterEmployeeId != null && Directory.TryGetValue(emp.masterEmployeeId, out var v)) ? v.evt : "";
    }

    // 파견중(사무실 부재) 직원인지 — 파견중엔 특성/전용이벤트 모두 비활성. CharacterUniqueEvents 도 공유.
    public static bool IsOnDispatch(EmployeeData emp)
        => emp != null && DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id);

    // grade >= Epic 이고 유효한 특성ID 를 가진 직원의 특성 row 반환. 아니면 null.
    public static CharacterTraitRow GetActiveTrait(EmployeeData emp)
    {
        if (emp == null) return null;
        if (IsOnDispatch(emp)) return null;                        // 파견중에는 특성 효과/표시 비활성
        if (emp.grade < EmployeeGrade.Epic) return null;          // ⚠️ 게이팅 미해결 (위 주석)
        string traitId = ResolveTraitId(emp);
        if (string.IsNullOrEmpty(traitId)) return null;

        var cache = CharacterTraitChartLoader.Cache;
        if (cache == null) return null;
        return cache.TryGetValue(traitId, out var row) ? row : null;
    }

    public static bool HasActiveTrait(EmployeeData emp) => GetActiveTrait(emp) != null;

    // 유리멘탈(김아무개) 특성 활성 여부 — 만족도 구간 배율이 일반 직원보다 극단적으로 적용됨.
    // EmployeeData.GetSatisfactionMultiplier 가 이 패시브 효과를 직접 분기 (별도 WeeklyTick 불필요).
    public static bool IsGlassMental(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_kim";

    // ──────────── 오타쿠(otaku_01) ────────────
    // 채용 시마다 랜덤 장르 1개를 emp.otakuFixedGenre 에 고정(재추첨). 그 장르 개발 시:
    //   - 오타쿠 본인 능력치 +20% (개발 틱 누적 + 팀장 점수 양쪽). DevelopmentManager 가 GetOtakuStatMultiplier 호출.
    //   - 프로젝트 매출 +20% (보유 오타쿠의 고정장르가 프로젝트 장르와 일치 시). SalesUI 가 GetOtakuSalesBonus 호출.
    public const float OTAKU_STAT_MULT  = 1.2f;  // 능력치 ×1.2
    public const float OTAKU_SALES_BONUS = 0.2f; // 매출 bonusSum 합연산 +0.20

    public static bool IsOtaku(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_otaku";

    // 이 직원이 오타쿠이고 고정 장르가 주어진 프로젝트 장르와 일치하는가.
    public static bool IsOtakuGenreMatch(EmployeeData emp, ProjectGenre genre)
        => IsOtaku(emp)
           && !string.IsNullOrEmpty(emp.otakuFixedGenre)
           && emp.otakuFixedGenre == genre.ToString();

    // 개발 틱/팀장 점수용 능력치 배율 — 오타쿠 본인 + 고정장르 매칭 시 1.2, 그 외 1.0.
    public static float GetOtakuStatMultiplier(EmployeeData emp, ProjectGenre genre)
        => IsOtakuGenreMatch(emp, genre) ? OTAKU_STAT_MULT : 1.0f;

    // 매출 보너스 — 보유 직원 중 고정장르가 프로젝트 장르와 일치하는 오타쿠가 한 명이라도 있으면 +0.20.
    public static float GetOtakuSalesBonus(ProjectGenre genre)
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return 0f;
        foreach (var emp in em.ownedEmployees)
            if (IsOtakuGenreMatch(emp, genre)) return OTAKU_SALES_BONUS;
        return 0f;
    }

    // ──────────── 금수저(goldspoon_01) ────────────
    // 기본 연봉·강화 연봉 상승량 50% 감소(채용/강화 시점에 salary 값 자체를 절반으로 확정 저장) + 연봉 협상 대상 제외.
    //   - 연봉 감소: EmployeeManager 의 후보 연봉 roll / 강화 연봉 가감 시 ApplyGoldspoonSalary 로 절반 처리.
    //   - 협상 제외: SalaryNegotiationManager.SelectNegotiationTarget 의 eligible 필터에서 IsGoldspoon 제외.
    public const float GOLDSPOON_SALARY_FACTOR = 0.5f;

    public static bool IsGoldspoon(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_goldspoon";

    // 금수저면 amount 의 절반(반올림), 아니면 amount 그대로. 기본 연봉·강화 상승량 양쪽에 공통 사용.
    public static int ApplyGoldspoonSalary(EmployeeData emp, int amount)
        => IsGoldspoon(emp) ? Mathf.RoundToInt(amount * GOLDSPOON_SALARY_FACTOR) : amount;

    // ──────────── 게으른 천재(genius_01) ────────────
    // 게으른 천재를 보유(Epic+)하면: 프로젝트 기간 +2주(고정, 다수여도 누적 X) + 개발(프로그래머) 팀장 최종 점수 ×1.3.
    //   - 기간: DevelopmentManager.StartDevelopment 의 developmentDuration 산정에 HasLazyGeniusOwned 시 +2주.
    //   - 팀장: DevelopmentManager.SetLeader 에서 type==Programmer 이고 보유 중이면 total ×1.3 (팀장 본인 여부 무관).
    public const int   LAZY_GENIUS_EXTRA_WEEKS = 2;
    public const float LAZY_GENIUS_LEADER_BONUS = 1.3f;

    public static bool IsLazyGenius(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_genius";

    // 게으른 천재를 한 명이라도 보유 중인가 (기간 +2주 / 개발 팀장 점수 +30% 발동 조건)
    public static bool HasLazyGeniusOwned()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return false;
        foreach (var emp in em.ownedEmployees)
            if (IsLazyGenius(emp)) return true;
        return false;
    }

    // ──────────── 훈수쟁이(hunsu_01) ────────────
    // 개발(프로그래머) 팀장일 때, 그 개발 팀장 점수의 10% 를 기획/아트 중 랜덤 1곳에 추가.
    // (훈수쟁이는 프로그래머라 LeaderSelect 역할 필터상 개발 팀장으로만 선정됨.)
    // 추가분은 LeaderScoreUI 마지막 tick 에 함께 카운트업 — DevelopmentManager.SetLeader 가 보너스/대상을 계산해 Show 에 전달.
    public const float HUNSU_BONUS_RATIO = 0.1f; // 개발 점수의 10%

    public static bool IsHunsu(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_hunsu";

    // 슬롯/카드 UI 표시용 — 활성 특성명 반환, 없으면 "" (grade < Epic 또는 미보유). UI 는 빈 문자열이면 숨김.
    public static string GetTraitName(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        return row != null ? row.name : "";
    }

    // 등급 게이팅을 무시하고 직원이 (잠재적으로) 가진 특성명 반환. CEO/미보유는 "".
    // 카드 UI 가 "등급 미충족이어도 이름은 표시 + lockedPanel" 하기 위해 사용.
    // ⚠️ 파견중이어도 이름은 표시한다 — "파견중 비활성화"는 효과 발동(GetActiveTrait/Is* 계열)만 막는 것이지
    //    표시 자체를 숨기라는 뜻이 아님(사용자 확인).
    public static string GetTraitNameAnyGrade(EmployeeData emp)
    {
        if (emp == null || emp.isCEO) return "";
        string traitId = ResolveTraitId(emp);
        if (string.IsNullOrEmpty(traitId)) return "";
        var cache = CharacterTraitChartLoader.Cache;
        return (cache != null && cache.TryGetValue(traitId, out var row)) ? row.name : "";
    }

    // 특성 발동 등급(Epic 이상) 충족 여부 — 표시/잠금오버레이/설명 클릭 가능 여부에 쓰임. CEO 제외.
    // ⚠️ 파견 여부는 반영 안 함 — 파견중 비활성화는 효과 발동(GetActiveTrait)만 막는 것이지 등급 충족 표시를 가리는 게 아님.
    public static bool IsTraitUnlocked(EmployeeData emp)
        => emp != null && !emp.isCEO && emp.grade >= EmployeeGrade.Epic;

    // 슬롯/카드 공통 — traitText 에 특성명 세팅 + 클릭 시 설명을 띄우는 버튼으로 만든다(런타임 컴포넌트 부착, 에디터 배선 불필요).
    // 특성 없음/CEO 면 빈 문자열 + raycastTarget off → 클릭이 슬롯 버튼으로 통과(가로채지 않음).
    // clickable=false 면 특성명은 표시하되 클릭→AlertUI 설명을 비활성(예: 팀장 선택 슬롯 — 클릭이 슬롯 선택으로 통과해야 함).
    public static void SetupTraitText(TMP_Text traitText, EmployeeData emp, bool clickable = true)
    {
        if (traitText == null) return;
        string traitName = (emp != null && !emp.isCEO) ? GetTraitName(emp) : "";
        traitText.text = string.IsNullOrEmpty(traitName) ? "" : $"특성 : {traitName}";

        var btn = traitText.GetComponent<TraitDescriptionButton>();
        if (string.IsNullOrEmpty(traitName) || !clickable)
        {
            if (btn != null) btn.Bind(null);
            traitText.raycastTarget = false; // 특성 없음/클릭 비활성 → 클릭이 슬롯 버튼으로 통과
            return;
        }
        if (btn == null) btn = traitText.gameObject.AddComponent<TraitDescriptionButton>();
        btn.Bind(emp);
        traitText.raycastTarget = true; // 특성 있으면 클릭 받아 설명 표시
    }

    // 특성명 텍스트 클릭 시 — 특성명 + 설명을 AlertUI 로 표시. 오타쿠는 고정 장르를 덧붙임.
    public static void ShowTraitDescription(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        if (row == null || AlertUI.Instance == null) return;

        string desc = row.description ?? "";
        if (ResolveTraitId(emp) == "ctrait_otaku" && !string.IsNullOrEmpty(emp.otakuFixedGenre))
            desc += $"\n\n고정 장르: {GenreKorName(emp.otakuFixedGenre)}";
        AlertUI.Instance.ShowPortrait(desc, emp.portraitId, row.name);
    }

    // 특성 설명 문자열만 반환(이름 없이) — 이력서 패널 등 자체 표시용. 특성 없으면 빈 문자열.
    public static string GetTraitDescription(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        if (row == null) return "";
        string desc = row.description;
        if (ResolveTraitId(emp) == "ctrait_otaku" && !string.IsNullOrEmpty(emp.otakuFixedGenre))
            desc += $"\n\n고정 장르: {GenreKorName(emp.otakuFixedGenre)}";
        return desc;
    }

    // 저장된 enum 이름(RPG, VisualNovel 등)을 한글 표시명으로 변환 (ProjectData 의 canonical 매핑 재사용)
    static string GenreKorName(string genreEnumName)
        => System.Enum.TryParse<ProjectGenre>(genreEnumName, out var g)
            ? new ProjectData { genre = g }.GenreToString()
            : genreEnumName;

    // ──────────── 시점별 hook 진입점 (각 시스템이 호출) ────────────

    // 채용 직후 1회. 오타쿠 = 선호 장르 고정 등.
    public static void OnHire(EmployeeData emp)
    {
        if (GetActiveTrait(emp) == null) return;
        switch (ResolveTraitId(emp))
        {
            case "ctrait_otaku":
                // 채용 시마다 랜덤 장르 1개 재추첨 고정 (해고 후 재채용 시 새 장르). enum 이름으로 저장.
                var genres = (ProjectGenre[])System.Enum.GetValues(typeof(ProjectGenre));
                emp.otakuFixedGenre = genres[UnityEngine.Random.Range(0, genres.Length)].ToString();
                Debug.Log($"[오타쿠] {emp.employeeName} 고정 장르 = {emp.otakuFixedGenre}");
                break;
            case "ctrait_ugi":
                // 채용 첫 주부터 변동되도록 초기 배율 1회 추첨 (이후 매주 WeeklyTick 재추첨)
                RerollCosmicEnergy(emp);
                break;
        }
    }

    // 우주의 기운 — 능력치 배율 70~150% 재추첨 (영속 필드 cosmicEnergyPercent, Effective*Skill 외곽 곱으로 소비)
    static void RerollCosmicEnergy(EmployeeData emp)
    {
        emp.cosmicEnergyPercent = UnityEngine.Random.Range(70, 151); // 70~150
        Debug.Log($"[우주의 기운] {emp.employeeName} 이번 배율 = {emp.cosmicEnergyPercent}%");
    }

    // 매주 1회. 유리멘탈(만족도 구간 배율) / 우주의 기운(주간 능력치 변동) 등.
    public static void WeeklyTick(EmployeeData emp)
    {
        if (GetActiveTrait(emp) == null) return;
        switch (ResolveTraitId(emp))
        {
            case "ctrait_kim":   // 유리멘탈 — 패시브로 EmployeeData.GetSatisfactionMultiplier 가 직접 분기 (IsGlassMental). 매주 처리 불필요.
                break;
            case "ctrait_ugi":   // 우주의 기운 — 매주 능력치 70~150% 변동 (단 신의 축복 주사위 2로 고정 중이면 재추첨 정지)
                if (!emp.cosmicFrozen) RerollCosmicEnergy(emp);
                break;
        }
    }

    // ⚠️ DEAD CODE — 호출처 없음 (NewRunInitializer 가 부르는 건 CEO용 OutGame/Trait/TraitEffectApplier.ApplyOnRunStart).
    // 호출하면 안 됨: WeeklyTick 이 우주의 기운을 재추첨해 저장된 cosmicEnergyPercent(및 신의 축복 cosmicFrozen 고정값)를 덮어씀.
    // 복원 시점 재적용이 필요해지면 패시브만 골라 적용하도록 새로 짤 것. 현재는 영속 필드 복원으로 충분해 미사용.
    public static void ApplyOnRunStart()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return;
        foreach (var emp in em.ownedEmployees)
            if (GetActiveTrait(emp) != null) WeeklyTick(emp);
    }
}
