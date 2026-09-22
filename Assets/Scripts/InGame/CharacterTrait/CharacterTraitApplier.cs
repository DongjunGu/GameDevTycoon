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

    // 특성 강화 단계(0~2) — 아웃게임 보유 카드의 Epic+N / Unique+N 합성 단계.
    // 저장하지 않고 OwnedCardManager 에서 실시간 조회 (진실 source 는 cardsJson 한 곳).
    // 채용 후보는 masterEmployeeId 가 비어 있어 id 로 fallback (채용 시 id 가 GUID 로 바뀜).
    public static int GetTraitStage(EmployeeData emp)
    {
        if (emp == null || OwnedCardManager.Instance == null) return 0;
        string masterId = !string.IsNullOrEmpty(emp.masterEmployeeId) ? emp.masterEmployeeId : emp.id;
        if (string.IsNullOrEmpty(masterId)) return 0;
        return OwnedCardManager.Instance.GetHighestStage(masterId, emp.grade);
    }

    // ──────────── 오타쿠(otaku_01) ────────────
    // 채용 시마다 랜덤 장르 1개를 emp.otakuFixedGenre 에 고정(재추첨). 그 장르 개발 시:
    //   - 오타쿠 본인 능력치 버프 (EmployeeData.GetOtakuBuffPercent 가 Effective*Skill 합연산 % 에 포함해서 계산 — 다른 버프와 동일 취급).
    //   - 그 장르의 숙련도 승급 확률 배수 (MasteryManager.TryPromote 의 확률 구간 / 거장 3% 양쪽에 곱).
    // 수치는 특성 강화 단계(GetTraitStage)별: 0단계 +10%/×1.5, 1단계 +15%/×2.0, 2단계 +20%/×2.5.

    // 능력치 버프 % — 비오타쿠/장르 불일치면 0.
    public static float GetOtakuStatPercent(EmployeeData emp, ProjectGenre genre)
    {
        if (!IsOtakuGenreMatch(emp, genre)) return 0f;
        return GetTraitStage(emp) switch { >= 2 => 20f, 1 => 15f, _ => 10f };
    }

    public static bool IsOtaku(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_otaku";

    // 이 직원이 오타쿠이고 고정 장르가 주어진 프로젝트 장르와 일치하는가.
    public static bool IsOtakuGenreMatch(EmployeeData emp, ProjectGenre genre)
        => IsOtaku(emp)
           && !string.IsNullOrEmpty(emp.otakuFixedGenre)
           && emp.otakuFixedGenre == genre.ToString();

    // 숙련도 승급 확률 배수 — 보유 오타쿠 중 고정장르가 일치하는 최고 단계 기준. 없으면 1.0(영향 없음).
    public static float GetOtakuMasteryChanceMult(ProjectGenre genre)
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return 1f;
        float best = 1f;
        foreach (var emp in em.ownedEmployees)
        {
            if (!IsOtakuGenreMatch(emp, genre)) continue;
            float m = GetTraitStage(emp) switch { >= 2 => 2.5f, 1 => 2.0f, _ => 1.5f };
            if (m > best) best = m;
        }
        return best;
    }

    // ──────────── 금수저(goldspoon_01) ────────────
    // 기본 연봉·강화 연봉 상승량 감소(채용/강화 시점에 salary 값 자체를 감산해 확정 저장) + 연봉 협상 대상 제외.
    //   - 연봉 감소: EmployeeManager 의 후보 연봉 roll / 강화 연봉 가감 시 ApplyGoldspoonSalary 로 처리.
    //     감소율은 특성 강화 단계(GetTraitStage)별 — 0단계 50% / 1단계 65% / 2단계 80%.
    //   - 협상 제외: SalaryNegotiationManager.SelectNegotiationTarget 의 eligible 필터에서 IsGoldspoon 제외.

    public static bool IsGoldspoon(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_goldspoon";

    // 금수저 연봉 배율 — 0단계 ×0.50 / 1단계 ×0.35 / 2단계 ×0.20. 비금수저는 ×1.
    public static float GetGoldspoonSalaryFactor(EmployeeData emp)
        => !IsGoldspoon(emp) ? 1f
           : GetTraitStage(emp) switch { >= 2 => 0.2f, 1 => 0.35f, _ => 0.5f };

    // 금수저면 amount 에 감소 배율 적용(반올림), 아니면 amount 그대로. 기본 연봉·강화 상승량 양쪽에 공통 사용.
    public static int ApplyGoldspoonSalary(EmployeeData emp, int amount)
        => IsGoldspoon(emp) ? Mathf.RoundToInt(amount * GetGoldspoonSalaryFactor(emp)) : amount;

    // ──────────── 게으른 천재(genius_01) ────────────
    // 게으른 천재를 보유(Epic+)하면: 프로젝트 기간 +2주(고정, 다수여도 누적 X) + 개발(프로그래머) 팀장 최종 점수 ×1.3.
    //   - 기간: DevelopmentManager.StartDevelopment 의 developmentDuration 산정에 HasLazyGeniusOwned 시 +2주.
    //   - 팀장: DevelopmentManager.SetLeader 에서 type==Programmer 이고 보유 중이면 total ×1.3 (팀장 본인 여부 무관).
    public const int LAZY_GENIUS_EXTRA_WEEKS = 2;

    public static bool IsLazyGenius(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_genius";

    // 게으른 천재를 한 명이라도 보유 중인가 (기간 +2주 / 개발 팀장 점수 증가 발동 조건)
    public static bool HasLazyGeniusOwned()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return false;
        foreach (var emp in em.ownedEmployees)
            if (IsLazyGenius(emp)) return true;
        return false;
    }

    // 개발 팀장 최종 점수 배율 — 보유 게으른 천재 중 최고 단계 기준(0단계 ×1.3 / 1단계 ×1.4 / 2단계 ×1.5).
    // 미보유면 1.0. 훈수쟁이 base 역산도 같은 값을 쓰므로 반드시 이 함수 하나만 참조할 것.
    public static float GetLazyGeniusLeaderBonus()
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return 1f;
        float best = 1f;
        foreach (var emp in em.ownedEmployees)
        {
            if (!IsLazyGenius(emp)) continue;
            float m = GetTraitStage(emp) switch { >= 2 => 1.5f, 1 => 1.4f, _ => 1.3f };
            if (m > best) best = m;
        }
        return best;
    }

    // ──────────── 훈수쟁이(hunsu_01) ────────────
    // 개발(프로그래머) 팀장일 때, 그 개발 팀장 점수의 일정 비율을 기획·아트 양쪽에 추가.
    // (훈수쟁이는 프로그래머라 LeaderSelect 역할 필터상 개발 팀장으로만 선정됨.)
    // 추가분은 DevelopmentManager.ContinueAfterLeaderScore 가 AlertUI 안내 후 양쪽에 반영.
    public static bool IsHunsu(EmployeeData emp)
        => GetActiveTrait(emp) != null && ResolveTraitId(emp) == "ctrait_hunsu";

    // 개발 점수 대비 기획·아트 반영 비율 — 0단계 10% / 1단계 15% / 2단계 20%. 비훈수쟁이는 0.
    public static float GetHunsuBonusRatio(EmployeeData emp)
        => !IsHunsu(emp) ? 0f
           : GetTraitStage(emp) switch { >= 2 => 0.20f, 1 => 0.15f, _ => 0.10f };

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

    // 특성명 텍스트 클릭 시 — 특성명(+단계) + 설명을 AlertUI 로 표시.
    public static void ShowTraitDescription(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        if (row == null || AlertUI.Instance == null) return;
        int stage = GetTraitStage(emp);
        string label = stage > 0 ? $"{row.name} +{stage}" : row.name;
        AlertUI.Instance.ShowPortrait(GetTraitDescription(emp), emp.portraitId, label);
    }

    // 특성 설명 문자열만 반환(이름 없이) — 이력서 패널 등 자체 표시용. 특성 없으면 빈 문자열.
    // 차트 설명(정성 문구) + 현재 강화 단계의 실제 수치 + (오타쿠) 고정 장르.
    public static string GetTraitDescription(EmployeeData emp)
    {
        var row = GetActiveTrait(emp);
        if (row == null) return "";
        string desc = row.description;

        string effect = GetTraitEffectText(emp);
        if (!string.IsNullOrEmpty(effect)) desc += $"\n\n{effect}";

        if (ResolveTraitId(emp) == "ctrait_otaku" && !string.IsNullOrEmpty(emp.otakuFixedGenre))
            desc += $"\n\n고정 장르: {GenreKorName(emp.otakuFixedGenre)}";
        return desc;
    }

    // 현재 강화 단계(0~2)에 해당하는 실제 수치 문구. 수치 변경 시 각 효과 로직과 함께 여기도 갱신할 것.
    //   유리멘탈 EmployeeData.GetSatisfactionMultiplier / 오타쿠 GetOtakuStatPercent·GetOtakuMasteryChanceMult /
    //   금수저 GetGoldspoonSalaryFactor / 우기 RerollCosmicEnergy / 천재 GetLazyGeniusLeaderBonus / 훈수 GetHunsuBonusRatio
    public static string GetTraitEffectText(EmployeeData emp)
        => GetTraitEffectText(ResolveTraitId(emp), GetTraitStage(emp));

    // traitId + 단계 직접 지정 — 아웃게임 상세 패널처럼 emp.grade 가 Normal(마스터 데이터)인 경우용.
    public static string GetTraitEffectText(string traitId, int stage)
    {
        int i = Mathf.Clamp(stage, 0, 2);
        switch (traitId)
        {
            case "ctrait_kim":
                return $"만족도 81~100일 때 능력치 +{new[] { 20, 25, 30 }[i]}%, 41~60일 때 능력치 -15%";
            case "ctrait_otaku":
                return $"해당 장르를 개발 시 능력치 +{new[] { 10, 15, 20 }[i]}% 상승, 숙련도 승급 확률 {new[] { "1.5", "2", "2.5" }[i]}배 증가";
            case "ctrait_goldspoon":
                return $"연봉 {new[] { 50, 65, 80 }[i]}% 감소, 연봉 협상 이벤트 대상에서 제외";
            case "ctrait_ugi":
                return $"게임 제작 시 능력치 {new[] { "70~150", "75~155", "80~160" }[i]}% 사이 랜덤 변동";
            case "ctrait_genius":
                return $"개발 기간 {LAZY_GENIUS_EXTRA_WEEKS}주 지연, 개발 팀장 최종 점수 {new[] { 30, 40, 50 }[i]}% 증가 적용";
            case "ctrait_hunsu":
                return $"개발 점수 상승량의 {new[] { 10, 15, 20 }[i]}%가 기획, 아트 점수에도 반영";
            default:
                return "";
        }
    }

    // 등급 게이팅을 무시한 차트 설명 원문만 반환(수치 문구 미포함) — 문장형/숫자형을 따로 표시하는 패널용.
    public static string GetTraitDescriptionRawAnyGrade(EmployeeData emp)
    {
        if (emp == null || emp.isCEO) return "";
        string traitId = ResolveTraitId(emp);
        if (string.IsNullOrEmpty(traitId)) return "";
        var cache = CharacterTraitChartLoader.Cache;
        return (cache != null && cache.TryGetValue(traitId, out var row)) ? row.description : "";
    }

    // 등급 게이팅을 무시한 특성 설명 — 아웃게임 상세 패널용(갤러리 직원은 grade 가 Normal 이라 GetTraitDescription 이 빈 문자열).
    // 차트 설명 + 지정 단계의 실제 수치. 특성 없으면 빈 문자열.
    public static string GetTraitDescriptionAnyGrade(EmployeeData emp, int stage)
    {
        if (emp == null || emp.isCEO) return "";
        string traitId = ResolveTraitId(emp);
        if (string.IsNullOrEmpty(traitId)) return "";
        var cache = CharacterTraitChartLoader.Cache;
        if (cache == null || !cache.TryGetValue(traitId, out var row)) return "";

        string effect = GetTraitEffectText(traitId, stage);
        return string.IsNullOrEmpty(effect) ? row.description : $"{row.description}\n\n{effect}";
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

    // 우주의 기운 — 능력치 배율 재추첨 (영속 필드 cosmicEnergyPercent, Effective*Skill 외곽 곱으로 소비)
    // 범위는 특성 강화 단계별 — 0단계 70~150% / 1단계 75~155% / 2단계 80~160%.
    static void RerollCosmicEnergy(EmployeeData emp)
    {
        int min = GetTraitStage(emp) switch { >= 2 => 80, 1 => 75, _ => 70 };
        emp.cosmicEnergyPercent = UnityEngine.Random.Range(min, min + 81); // min ~ min+80
        Debug.Log($"[우주의 기운] {emp.employeeName} 이번 배율 = {emp.cosmicEnergyPercent}% (범위 {min}~{min + 80})");
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
