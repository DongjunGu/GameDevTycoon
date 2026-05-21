using UnityEngine;

// 캐릭터별 등급 특성의 인게임 효과 디스패처 (CEO 특성 TraitEffectApplier 와 별개)
//
// 발동 기준: EmployeeData.grade >= Epic (채용 시 roll 된 현재 등급). 누적 — Unique/Legendary 도 특성 보유.
// 데이터: EmployeeData.epicTraitId → CharacterTraitChartLoader.Cache.
// 공유 상태: EmployeeData 의 otakuFixedGenre 등 런타임 필드 (특수이벤트 CharacterUniqueEvents 와 공유).
//
// 구조: traitId 별로 분기. 효과는 발동 "시점" 이 제각각이라 시스템별 hook 진입점을 둠.
//   - OnHire(emp)        : 채용 직후 1회        (오타쿠 = 선호 장르 고정)            ← EmployeeManager.HireEmployee
//   - WeeklyTick(emp)    : 매주                 (유리멘탈 / 우주의 기운)             ← EmployeeManager.OnWeekPassed
//   - [TODO 미연결 hook] : 팀장 점수 계산 시    (게으른 천재 / 훈수쟁이 / 오타쿠)    ← DevelopmentManager
//                          연봉/협상 시          (금수저)                             ← SalaryNegotiationManager
//                          매출 계산 시          (오타쿠 장르 보너스)                 ← SalesUI
//   효과 명세는 [[project_character_trait_event_spec]] 참조. 현재 전부 TODO 스텁.
//
// ⚠️ 등급 게이팅 미해결: 스펙상 maxGrade Normal/Rare 캐릭터(김아무개·천재·금수저·훈수쟁이)도 특성을 갖는데
//    현재 GetActiveTrait 는 grade>=Epic 요구 → 영원히 미발동. 활성 규칙 확정 시 GetActiveTrait 수정.
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

    // grade >= Epic 이고 유효한 특성ID 를 가진 직원의 특성 row 반환. 아니면 null.
    public static CharacterTraitRow GetActiveTrait(EmployeeData emp)
    {
        if (emp == null) return null;
        if (emp.grade < EmployeeGrade.Epic) return null;          // ⚠️ 게이팅 미해결 (위 주석)
        string traitId = ResolveTraitId(emp);
        if (string.IsNullOrEmpty(traitId)) return null;

        var cache = CharacterTraitChartLoader.Cache;
        if (cache == null) return null;
        return cache.TryGetValue(traitId, out var row) ? row : null;
    }

    public static bool HasActiveTrait(EmployeeData emp) => GetActiveTrait(emp) != null;

    // 슬롯/카드 UI 표시용 — 활성 특성명 반환, 없으면 "" (grade < Epic 또는 미보유). UI 는 빈 문자열이면 숨김.
    public static string GetTraitName(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        return row != null ? row.name : "";
    }

    // ──────────── 시점별 hook 진입점 (각 시스템이 호출) ────────────

    // 채용 직후 1회. 오타쿠 = 선호 장르 고정 등.
    public static void OnHire(EmployeeData emp)
    {
        if (GetActiveTrait(emp) == null) return;
        switch (ResolveTraitId(emp))
        {
            case "ctrait_otaku":
                // TODO: 채용 시 랜덤 장르 1개를 emp.otakuFixedGenre 에 고정
                break;
        }
    }

    // 매주 1회. 유리멘탈(만족도 구간 배율) / 우주의 기운(주간 능력치 변동) 등.
    public static void WeeklyTick(EmployeeData emp)
    {
        if (GetActiveTrait(emp) == null) return;
        switch (ResolveTraitId(emp))
        {
            case "ctrait_kim":   // 유리멘탈 — 만족도 구간별 능력치 가감
                // TODO: 만족도 81~90 +15% / 91~100 +30% / 41~60 -20% 적용
                break;
            case "ctrait_ugi":   // 우주의 기운 — 매주 능력치 70~150% 변동
                // TODO: 주간 배율 갱신
                break;
        }
    }

    // 런 시작 시 일괄 (저장된 직원 복원 후 패시브 특성 재적용용). 현재 골격만.
    public static void ApplyOnRunStart()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return;
        foreach (var emp in em.ownedEmployees)
            if (GetActiveTrait(emp) != null) WeeklyTick(emp); // TODO: 시점 정합성 검토
    }
}
