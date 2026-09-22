using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 유니크 등급 직원 전용 이벤트 로직 디스패처 (RandomEventManager 는 스케줄링/위임만 담당).
//
// 데이터: CharacterUniqueEventChartLoader.Cache (key = EmployeeData.uniqueEventType).
// 공유 상태: EmployeeData 의 otakuFixedGenre / lastUniqueEventYear / glassMentalCooldownWeeks 등
//            (특성 CharacterTraitApplier 와 공유).
//
// 구조: uniqueEventType 별 분기. 표시(EventChoicePanel)는 공통, 효과는 케이스별 ApplyEffect.
// 효과 명세는 [[project_character_trait_event_spec]] 참조.
public static class CharacterUniqueEvents
{
    const int   GLASS_MENTAL_COOLDOWN_WEEKS     = 24;    // 유리 멘탈 회복 재발동 금지 6개월(=24주)
    const int   GOD_BLESSING_STAT_PERCENT       = 20;    // 신의 축복 주사위 3: 랜덤 직원 능력치 +20%
    const int   GOD_BLESSING_ALL_STAT_PERCENT   = 8;     // 신의 축복 주사위 5: 모든 직원 능력치 +8%
    public const float GOD_BLESSING_SALES_BONUS = 0.12f; // 신의 축복 주사위 6: 매출 +12% (SalesUI bonusSum 합연산)

    // 전용 이벤트 강화 단계(0~2) — 특성과 같은 보유 카드 stage(Unique+N)를 공유.
    static int StageOf(EmployeeData emp) => Mathf.Clamp(CharacterTraitApplier.GetTraitStage(emp), 0, 2);
    public static int DebugForcedDice = 0; // 테스트용 — 1~6 지정 시 다음 신의 축복이 그 눈으로 발동(소비 후 0 리셋). 0=정상 랜덤.

    // 매주 1회 (RandomEventManager.CheckCharacterUniqueEvents → 유니크 직원별 호출).
    // 주간 발동형 전용 이벤트(유리 멘탈 회복 등) 의 조건/확률/쿨다운을 처리.
    public static void WeeklyCheck(EmployeeData emp)
    {
        if (emp == null) return;
        if (CharacterTraitApplier.IsOnDispatch(emp)) return; // 파견중 직원은 전용 이벤트 발동 안 함
        switch (CharacterTraitApplier.ResolveEventType(emp))
        {
            case "KimUnique":       CheckGlassMentalRecovery(emp); break;
            case "GoldspoonUnique": CheckGoldspoonGift(emp);       break;
            // 그 외(Ugi/Genius/Hunsu)는 각자 시점(개발 시작 / 진행도 60% / 창의성 게임 후)에서 처리
        }
    }

    // 오다 주웠다 — 매년 1월 1주에 1회, 상점 등장 아이템 중 랜덤 (단계+1)개 지급.
    static void CheckGoldspoonGift(EmployeeData emp)
    {
        var gt = GameTimeManager.Instance;
        if (gt == null) return;
        if (emp.lastUniqueEventYear == gt.Year) return; // 올해 이미 발동
        if (gt.Month != 1 || gt.Week != 1) return;      // 1월 1주 고정
        Trigger(emp); // 패널 + ApplyEffect(아이템 지급 + lastUniqueEventYear) + 4-set 저장
    }

    // 신의 축복 — 프로젝트 개발 시작 시 1회 주사위(d6) 발동 (DevelopmentManager.StartDevelopment 에서 호출).
    // 프로젝트당 1회는 _usedGameUpgrades("godBlessing") 마킹으로 보장(재접속 복원에도 유지).
    public static void CheckGodBlessingOnDevStart()
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null || dm.IsGameUpgradeUsed("godBlessing")) return;
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return;

        foreach (var emp in em.ownedEmployees)
        {
            if (emp.grade < EmployeeGrade.Unique) continue;
            if (CharacterTraitApplier.IsOnDispatch(emp)) continue;
            if (CharacterTraitApplier.ResolveEventType(emp) != "UgiUnique") continue;
            dm.MarkGameUpgradeUsed("godBlessing");
            Trigger(emp); // InfoFeedUI 토스트 + ApplyEffect(d6 효과) + 4-set 저장 (모달 패널 없음)
            return;
        }
    }

    // ──────────── 잠 깨우기 (GeniusUnique) — 진행도 60% 도달 시 1회 선택지 이벤트 ────────────
    // "....Zzzz" 대사 + 선택지 2개: 커피를 사준다(자금 -500G, 최종 개발 점수 +6/8/10%) / 그냥 자게 둔다(만족도 +10/15/20).
    // 자금 부족이면 커피 선택지는 비활성(회색). 프로젝트당 1회 — _usedGameUpgrades("geniusWakeup") 마킹.
    // 호출: DevelopmentManager.DevelopmentCoroutine 의 progress >= 0.60 분기. 시간 정지/재개는 이 함수가 처리.
    public const int GENIUS_COFFEE_COST = 500;

    public static void CheckGeniusWakeUp()
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null || dm.IsGameUpgradeUsed("geniusWakeup")) return;
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return;

        EmployeeData genius = null;
        foreach (var emp in em.ownedEmployees)
        {
            if (emp.grade < EmployeeGrade.Unique) continue;
            if (CharacterTraitApplier.IsOnDispatch(emp)) continue;
            if (CharacterTraitApplier.ResolveEventType(emp) != "GeniusUnique") continue;
            genius = emp; break;
        }
        if (genius == null || RandomEventChoiceUI.Instance == null) return;

        dm.MarkGameUpgradeUsed("geniusWakeup"); // 패널 표시 전 마킹 — 중도 종료해도 중복 발동 방지

        int stage      = StageOf(genius);
        float devPct   = new[] { 0.06f, 0.08f, 0.10f }[stage];
        int   satUp    = new[] { 10, 15, 20 }[stage];
        bool  canAfford = MoneyManager.Instance != null && MoneyManager.Instance.CanAfford(GENIUS_COFFEE_COST);

        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue("GeniusUnique", out row);
        string title = row != null ? $"{row.title} 발동" : "잠 깨우기 발동";

        var data = new RandomEventChoiceData
        {
            title       = title,
            description = "....Zzzz",
            portraitId  = genius.portraitId,
            choices     = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption
                {
                    buttonLabel   = "커피를 사준다",
                    conditionText = $"자금 -{GENIUS_COFFEE_COST:N0} G",
                    disabled      = !canAfford,
                    onChoose      = () => ApplyGeniusCoffee(genius, devPct),
                },
                new RandomEventChoiceOption
                {
                    buttonLabel = "그냥 자게 둔다",
                    onChoose    = () =>
                    {
                        genius.ChangeSatisfaction(satUp);
                        InfoFeedUI.Instance?.ShowSatisfaction(genius, satUp);
                        SaveAfterChoice();
                    },
                },
            },
            onConfirm = () => GameTimeManager.Instance?.StartTime(),
        };

        ModalGate.I.WhenFree(() =>
        {
            GameTimeManager.Instance?.StopTime();
            RandomEventChoiceUI.Instance.Show(data);
        });
    }

    // 커피 구매 — 자금 차감 후 현재 개발 점수의 devPct 만큼 개발 파트에 가산.
    static void ApplyGeniusCoffee(EmployeeData genius, float devPct)
    {
        if (MoneyManager.Instance == null || !MoneyManager.Instance.SpendGold(GENIUS_COFFEE_COST, false)) return;

        var ui = DevelopmentPanelUI.Instance;
        if (ui != null)
        {
            int add = Mathf.Max(1, Mathf.RoundToInt(ui.GetDevelop() * devPct));
            ui.AddValuesInstant(0f, add, 0f, 0f, 0f); // 개발 파트에만 가산
            InfoFeedUI.Instance?.ShowCustom(genius,
                $"{InfoFeedUI.Colorize(genius.employeeName, true)}이(가) 잠에서 깨어 개발 점수가 {InfoFeedUI.Colorize($"+{add}", true)} 올랐다.");
        }
        SaveAfterChoice();
    }

    // 선택 결과 즉시 영속화 — 전용 이벤트 Trigger 와 동일한 4-set.
    static void SaveAfterChoice()
    {
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance?.SaveProject();
        GameTimeManager.Instance?.SaveGameTime();
    }

    // 약점 극복(HunsuUnique) — 창의성 미니게임 후·디버깅 전 1회(DevelopmentManager.ShowCreativityGame 콜백에서 호출).
    // Unique+ 훈수쟁이 보유 시 기획/개발/아트 중 최저 파트에 개발 팀장점수의 25/35/45%(단계별) 추가.
    public static void CheckWeaknessOvercome()
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null || dm.IsGameUpgradeUsed("hunsuWeakness")) return; // 프로젝트당 1회(영속 가드)
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return;

        EmployeeData hunsu = null;
        foreach (var emp in em.ownedEmployees)
            if (emp.grade >= EmployeeGrade.Unique && !CharacterTraitApplier.IsOnDispatch(emp)
                && CharacterTraitApplier.ResolveEventType(emp) == "HunsuUnique")
            { hunsu = emp; break; }
        if (hunsu == null) return;

        Trigger(hunsu); // 패널 + ApplyEffect(최저 파트 상승 + hunsuWeakness 마킹) + 4-set
    }

    // 상점에서 나올 수 있는 아이템(현재 stage 의 appearStages 포함, 없으면 전체) 중 랜덤 1개 itemId. MerchantManager.RollItems 와 동일 규칙.
    static string PickRandomShopItem()
    {
        var cache = ItemChartLoader.Cache;
        if (cache == null || cache.Count == 0) return null;

        var ids = new List<string>();
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 0;
        if (stage > 0)
        {
            string stageStr = stage.ToString();
            foreach (var kv in cache)
            {
                // 2026-08-14 — 강화권/초심 회복기 등 강화 계열도 이제 실제 효과가 구현돼 있어 지급 대상 포함
                // (MerchantManager.RollItems와 동일 규칙).
                var stages = kv.Value.appearStages;
                if (string.IsNullOrEmpty(stages)) continue;
                foreach (var s in stages.Split(','))
                    if (s.Trim() == stageStr) { ids.Add(kv.Key); break; }
            }
        }
        if (ids.Count == 0)
            foreach (var kv in cache)
                ids.Add(kv.Key);
        return ids.Count > 0 ? ids[Random.Range(0, ids.Count)] : null;
    }

    // 유리 멘탈 회복 — 만족도 80 이하일 때 매주 단계별 확률(2/3/4%)로 만족도 100 회복.
    // 재발동은 최소 6개월(24주) 간격 — glassMentalCooldownWeeks 가 매주 1씩 감소.
    static void CheckGlassMentalRecovery(EmployeeData emp)
    {
        if (emp.glassMentalCooldownWeeks > 0) { emp.glassMentalCooldownWeeks--; return; } // 쿨다운 소진 중
        if (emp.satisfaction > 80) return;                             // 만족도 80 이하에서만
        if (Random.value >= GetGlassMentalChance(emp)) return;         // 단계별 매주 확률
        Trigger(emp); // 차트 문구 모달 + ApplyEffect(만족도 100 + 쿨다운 세팅)
    }

    // 유리 멘탈 회복 주간 확률 — 0단계 2% / 1단계 3% / 2단계 4%.
    static float GetGlassMentalChance(EmployeeData emp)
        => new[] { 0.02f, 0.03f, 0.04f }[StageOf(emp)];

    // ──────────── UI 표시 (eventText — traitText 와 동일 패턴) ────────────

    // 슬롯/카드 UI 표시용 — 전용 이벤트명(grade >= Unique 일 때만, 아니면 ""). CEO 제외.
    public static string GetEventName(EmployeeData emp)
    {
        if (emp == null || emp.isCEO) return "";
        if (emp.grade < EmployeeGrade.Unique) return "";
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        if (string.IsNullOrEmpty(eventType)) return "";
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        return row != null ? row.title : "";
    }

    // 등급 게이팅을 무시하고 직원이 (잠재적으로) 가진 전용 이벤트명 반환. CEO/미보유는 "".
    // 카드 UI 가 "등급 미충족이어도 이름은 표시 + lockedPanel" 하기 위해 사용.
    public static string GetEventNameAnyGrade(EmployeeData emp)
    {
        if (emp == null || emp.isCEO) return "";
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        if (string.IsNullOrEmpty(eventType)) return "";
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        return row != null ? row.title : "";
    }

    // 전용 이벤트 발동 등급(Unique 이상) 충족 여부. CEO 제외.
    public static bool IsEventUnlocked(EmployeeData emp)
        => emp != null && !emp.isCEO && emp.grade >= EmployeeGrade.Unique;

    // 약점 극복 — 개발 팀장 점수 대비 최저 파트 가산 비율. 0단계 25% / 1단계 35% / 2단계 45%.
    static float GetWeaknessRatio(EmployeeData emp) => new[] { 0.25f, 0.35f, 0.45f }[StageOf(emp)];

    // eventText 클릭 시 — 전용 이벤트명(+단계) + 설명을 AlertUI 로 표시.
    public static void ShowEventDescription(EmployeeData emp)
    {
        if (emp == null || AlertUI.Instance == null || emp.grade < EmployeeGrade.Unique) return;
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        if (row == null) return;
        int stage = StageOf(emp);
        string label = stage > 0 ? $"{row.title} +{stage}" : row.title;
        AlertUI.Instance.ShowPortrait(GetEventDescription(emp), emp.portraitId, label);
    }

    // 전용 이벤트 설명 문자열만 반환(제목 없이) — 이력서 패널 등 자체 표시용. 없으면 빈 문자열.
    // 차트 설명(정성 문구) + 현재 강화 단계의 실제 수치.
    public static string GetEventDescription(EmployeeData emp)
    {
        if (emp == null || emp.grade < EmployeeGrade.Unique) return "";
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        if (row == null) return "";
        string desc = (row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "";
        string effect = GetEventEffectText(emp);
        return string.IsNullOrEmpty(effect) ? desc : $"{desc}\n\n{effect}";
    }

    // 현재 강화 단계(0~2)의 실제 수치 문구. 수치 변경 시 각 효과 로직과 함께 여기도 갱신할 것.
    public static string GetEventEffectText(EmployeeData emp)
        => emp == null ? "" : GetEventEffectText(CharacterTraitApplier.ResolveEventType(emp), StageOf(emp), emp);

    // eventType + 단계 직접 지정 — 아웃게임 상세 패널처럼 emp.grade 가 Normal(마스터 데이터)인 경우용.
    public static string GetEventEffectText(string eventType, int stage, EmployeeData emp = null)
    {
        int i = Mathf.Clamp(stage, 0, 2);
        switch (eventType)
        {
            case "KimUnique":
                return $"만족도가 80 이하일 때 매주 {new[] { 2, 3, 4 }[i]}%의 확률로 100까지 회복 (최소 6개월 간격 발동)";
            case "OtakuUnique":
                return $"해당 장르의 인기도를 {3 + i}단계로 올려주는 이벤트 발생";
            case "GoldspoonUnique":
                return $"매년 1월에 아이템을 랜덤하게 {i + 1}개 제공";
            case "UgiUnique":
                return "게임 개발 중 주사위를 던져 랜덤한 버프 제공"
                     + $"\n1: {new[] { "꽝", "모든 직원 만족도 +5", "모든 직원 만족도 +15" }[i]}"
                     + $"\n2: 우기 능력치 {(i >= 1 ? "130~160" : "120~150")}% 사이 적용"
                     + $"\n3: 랜덤 직원 능력치 +{GOD_BLESSING_STAT_PERCENT}%"
                     + "\n4: 우기 만족도 100 고정"
                     + $"\n5: 모든 직원 능력치 +{GOD_BLESSING_ALL_STAT_PERCENT}%"
                     + $"\n6: 매출 +{Mathf.RoundToInt(GOD_BLESSING_SALES_BONUS * 100f)}%";
            case "GeniusUnique":
                return $"커피를 사주면 최종 개발 점수 +{new[] { 6, 8, 10 }[i]}% / 그냥 두면 만족도 +{new[] { 10, 15, 20 }[i]}";
            case "HunsuUnique":
                return $"기획·개발·아트 중 가장 점수가 낮은 파트에 개발 팀장 점수의 {new[] { 25, 35, 45 }[i]}% 추가";
            default:
                return "";
        }
    }

    // 슬롯 프리팹용 — traitText 의 형제 "eventText"(TMP)를 찾아 세팅 (직렬화 필드 없이 형제 탐색).
    public static void SetupEventText(TMP_Text traitText, EmployeeData emp)
    {
        if (traitText == null || traitText.transform.parent == null) return;
        var found = traitText.transform.parent.Find("eventText");
        if (found == null) return;
        SetupEventTextDirect(found.GetComponent<TMP_Text>(), emp);
    }

    // 직렬화된 eventText 를 직접 받아 세팅 (EmployeeCardUI 등 직접 배선용).
    // 전용 이벤트명 세팅 + 클릭 시 설명 버튼화(런타임 AddComponent). 이벤트 없으면 빈 문자열 + 클릭 통과.
    public static void SetupEventTextDirect(TMP_Text eventText, EmployeeData emp)
    {
        if (eventText == null) return;

        string eventName = GetEventName(emp);
        eventText.text = string.IsNullOrEmpty(eventName) ? "" : $"이벤트 : {eventName}";

        var btn = eventText.GetComponent<EventDescriptionButton>();
        if (string.IsNullOrEmpty(eventName))
        {
            if (btn != null) btn.Bind(null);
            eventText.raycastTarget = false; // 이벤트 없으면 클릭 통과
            return;
        }
        if (btn == null) btn = eventText.gameObject.AddComponent<EventDescriptionButton>();
        btn.Bind(emp);
        eventText.raycastTarget = true;
    }

    // 등급 게이팅을 무시한 차트 설명 원문만 반환(수치 문구 미포함) — 문장형/숫자형을 따로 표시하는 패널용.
    public static string GetEventDescriptionRawAnyGrade(EmployeeData emp)
    {
        if (emp == null || emp.isCEO) return "";
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        if (string.IsNullOrEmpty(eventType)) return "";
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        return (row != null && row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "";
    }

    // 등급 게이팅을 무시한 전용 이벤트 설명 — 아웃게임 상세 패널용. 차트 설명 + 지정 단계의 실제 수치.
    public static string GetEventDescriptionAnyGrade(EmployeeData emp, int stage)
    {
        if (emp == null || emp.isCEO) return "";
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        if (string.IsNullOrEmpty(eventType)) return "";
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        if (row == null) return "";

        string desc   = (row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "";
        string effect = GetEventEffectText(eventType, stage, emp);
        return string.IsNullOrEmpty(effect) ? desc : $"{desc}\n\n{effect}";
    }

    // 전용 이벤트 1건 발동 — 차트 문구 표시 후 케이스별 효과 적용.
    public static void Trigger(EmployeeData emp)
    {
        if (emp == null) return;
        if (CharacterTraitApplier.IsOnDispatch(emp)) return; // 파견중 직원은 전용 이벤트 발동 안 함 (백스톱)
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        if (string.IsNullOrEmpty(eventType)) return;

        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        if (row == null)
        {
            Debug.LogWarning($"[CharacterUniqueEvent] '{eventType}' 차트 row 없음 ({emp.employeeName})");
            return;
        }

        // 효과 + 4-set 즉시 적용/저장 — 패널 확인 전 종료해도 저장/복원 일관성 보장.
        // (그 주 OnWeekPassed 의 다른 직원 변동·시간·머니·프로젝트까지 함께 박음. SaveGameTime 이 SaveAllEmployees fan-out)
        // ApplyEffect 가 null 이 아닌 문구를 반환하면(신의 축복 주사위 결과 등) 차트 description 대신 그 문구를 패널에 표시.
        string customDesc = ApplyEffect(emp, row, eventType);
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance?.SaveProject();
        GameTimeManager.Instance?.SaveGameTime();

        // 신의 축복(UgiUnique)은 더 이상 확인 클릭이 필요한 모달 패널(ShowEventPanel)로 안 뜬다 —
        // ApplyGodBlessing 안에서 이미 InfoFeedUI 토스트로 결과를 안내했으므로 여기선 그냥 끝낸다
        // (시간정지/ModalGate 대기 없음).
        if (eventType == "UgiUnique") return;

        // 패널은 ModalGate 큐로 순차 표시 — 같은 주에 여러 전용 이벤트/사직 패널이 겹쳐도 하나씩 차례로.
        // 표시 동안 StopTime ↔ 확인 시 StartTime(카운터 균형) → 마지막 패널이 닫힐 때만 시간 재개.
        ModalGate.I.WhenFree(() =>
        {
            GameTimeManager.Instance?.StopTime();
            ShowEventPanel(emp, row, customDesc, () => GameTimeManager.Instance?.StartTime());
        });
    }

    // EventChoicePanel(RandomEventChoiceUI) 로 전용 이벤트 표시 — 선택지 없음(ChoiceButtonContainer 미사용),
    // 제목 "{이벤트명} 발동", 설명 = description1, 초상화 = 해당 직원. onConfirm 은 확인 시 콜백(시간 관리는 호출자 책임).
    static void ShowEventPanel(EmployeeData emp, CharacterUniqueEventRow row, string customDesc, System.Action onConfirm)
    {
        if (RandomEventChoiceUI.Instance == null) { onConfirm?.Invoke(); return; }
        string desc = !string.IsNullOrEmpty(customDesc)
            ? customDesc
            : ((row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "");
        RandomEventChoiceUI.Instance.Show(new RandomEventChoiceData
        {
            title       = $"{row.title} 발동",
            description = desc,
            portraitId  = emp.portraitId,
            choices     = new List<RandomEventChoiceOption>(),
            onConfirm   = onConfirm,
        });
    }

    // ──────────── 버튜버 데뷔 (OtakuUnique) — 디버깅 종료 후(결과 표시 직전) hook ────────────
    // Unique+ 오타쿠를 보유하고 이번 프로젝트 장르가 그 오타쿠의 고정 장르이며 인기도가 1·2단계면
    // 인기도를 3단계로 올리고 이벤트 패널을 표시한다. DevelopmentManager.ShowResult 에서 호출(결과/매출에 인기도 3 반영).
    // 시간은 ShowResult 가 이미 정지 → 확인 시 onDone(결과 표시)으로 진행(시간 재개 안 함).
    public static void CheckVtuberDebut(System.Action onDone)
    {
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) { onDone?.Invoke(); return; }

        string genreName = ProjectSetupUI.SelectedGenre.ToString();
        EmployeeData otaku = null;
        foreach (var emp in em.ownedEmployees)
        {
            if (emp.grade < EmployeeGrade.Unique) continue;
            if (CharacterTraitApplier.IsOnDispatch(emp)) continue; // 파견중 오타쿠는 버튜버 데뷔 발동 안 함
            if (CharacterTraitApplier.ResolveEventType(emp) != "OtakuUnique") continue;
            if (!string.IsNullOrEmpty(emp.otakuFixedGenre) && emp.otakuFixedGenre == genreName) { otaku = emp; break; }
        }
        if (otaku == null) { onDone?.Invoke(); return; }
        int targetPop = 3 + StageOf(otaku); // 0단계 3 / 1단계 4 / 2단계 5
        if (ProjectSetupUI.SelectedGenrePopularity >= targetPop) { onDone?.Invoke(); return; } // 이미 목표 이상이면 발동 안 함

        // 효과: 이번 프로젝트 인기도(스냅샷) + 표시 인기도를 목표 단계로
        ProjectSetupUI.SelectedGenrePopularity = targetPop;
        GenrePopularityManager.Instance?.SetPopularity(ProjectSetupUI.SelectedGenre, targetPop);

        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue("OtakuUnique", out row);
        if (row == null) { onDone?.Invoke(); return; }
        ShowEventPanel(otaku, row, null, onDone); // 시간은 이미 정지 — 확인 시 setup 계속 (차트 문구 사용)
    }

    // 효과 적용. 패널에 차트 description 대신 표시할 문구가 있으면 반환(없으면 null → 차트 description1 사용).
    static string ApplyEffect(EmployeeData emp, CharacterUniqueEventRow row, string eventType)
    {
        switch (eventType)
        {
            case "KimUnique":       // 유리 멘탈 회복 — 만족도 100 회복 + 6개월 쿨다운 세팅 (조건/확률은 WeeklyCheck 에서)
                emp.satisfaction = 100;
                emp.glassMentalCooldownWeeks = GLASS_MENTAL_COOLDOWN_WEEKS;
                // 저장은 Trigger 의 4-set(SaveAllEmployees 포함)에서 일괄 처리 — 여기서 별도 UpdateEmployee 안 함(중복/동시 쓰기 방지)
                break;
            case "OtakuUnique":     // 버튜버 데뷔 — 개발 시작 hook(CheckVtuberDebut)에서 처리. 이 경로(Trigger/ApplyEffect)로는 안 옴.
                break;
            case "GoldspoonUnique": // 오다 주웠다 — 상점 등장 아이템 중 랜덤 (단계+1)개 지급 + 올해 발동 기록
                int giftCount = StageOf(emp) + 1; // 0단계 1개 / 1단계 2개 / 2단계 3개
                for (int g = 0; g < giftCount; g++)
                {
                    string giftId = PickRandomShopItem(); // 중복 허용 — 매번 독립 추첨
                    if (string.IsNullOrEmpty(giftId)) continue;
                    ItemManager.Instance?.AddItem(giftId); // 인벤토리 추가 + UserItems 저장
                    Debug.Log($"[오다 주웠다] {emp.employeeName} → 아이템 '{giftId}' 지급 ({g + 1}/{giftCount})");
                }
                emp.lastUniqueEventYear = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : emp.lastUniqueEventYear;
                break;
            case "UgiUnique":       // 신의 축복 — d6 주사위. 결과 문구를 반환해 패널에 표시.
                return ApplyGodBlessing(emp);
            case "GeniusUnique":    // 잠 깨우기 — CheckGeniusWakeUp(진행도 60% 선택지 이벤트)에서 직접 처리. 이 경로로는 안 옴.
                break;
            case "HunsuUnique":     // 약점 극복 — 기획/개발/아트 중 최저 파트에 개발 팀장점수의 25/35/45% 추가. 프로젝트당 1회.
                {
                    var dm = DevelopmentManager.Instance;
                    var ui = DevelopmentPanelUI.Instance;
                    if (dm == null || ui == null) break;
                    int raise = Mathf.Max(1, Mathf.RoundToInt(dm.LeaderDevelopBonusTotal * GetWeaknessRatio(emp)));

                    // 3개 파트 [기획, 개발, 아트] 현재값 — 창의성은 대상 아님
                    float[] vals = { ui.GetPlanning(), ui.GetDevelop(), ui.GetArt() };
                    int minIdx = 0;
                    for (int i = 1; i < 3; i++) if (vals[i] < vals[minIdx]) minIdx = i;

                    ui.AddValuesInstant(
                        minIdx == 0 ? raise : 0f,  // 기획
                        minIdx == 1 ? raise : 0f,  // 개발
                        minIdx == 2 ? raise : 0f,  // 아트
                        0f, 0f);                   // 버그 / 창의성
                    dm.MarkGameUpgradeUsed("hunsuWeakness");
                }
                break;
            default:
                Debug.LogWarning($"[CharacterUniqueEvent] '{eventType}' 효과 미구현");
                break;
        }
        return null; // 위 break 경로는 차트 description1 사용
    }

    // ──────────── 신의 축복 (UgiUnique) — 1년 1회 d6 주사위 ────────────
    // 결과별 효과 적용 + InfoFeedUI 토스트 안내(더 이상 확인 클릭이 필요한 모달 패널 아님, Trigger 참고).
    // 2/3/6 은 "다음 축복까지" 지속(영속 필드), 4/5 는 즉시 1회.
    // 발동 시 작년 축복의 지속효과는 ClearGodBlessing 으로 먼저 해제(한 번에 하나만 활성).
    static string ApplyGodBlessing(EmployeeData ugi)
    {
        ClearGodBlessing(); // 이전 축복(지속형 2/3/5/6) 해제 후 새 축복 적용

        // 테스트용 강제 주사위(1~6) 가 지정돼 있으면 그 값 사용 후 리셋, 아니면 정상 랜덤.
        int dice = (DebugForcedDice >= 1 && DebugForcedDice <= 6) ? DebugForcedDice : Random.Range(1, 7); // 1~6
        DebugForcedDice = 0;
        Debug.Log($"[신의 축복] {ugi.employeeName} 주사위 = {dice}");
        int stage = StageOf(ugi);
        switch (dice)
        {
            case 1: // 0단계 꽝 / 1단계 모든 직원 만족도 +5 / 2단계 +15
            {
                int satUp = new[] { 0, 5, 15 }[stage];
                if (satUp <= 0)
                {
                    InfoFeedUI.Instance?.ShowCustom(ugi, $"{ugi.employeeName}에게 아무 일도 일어나지 않았다. (꽝)");
                    return "주사위 결과: 1\n\n...이번 해에는 아무 일도 일어나지 않았습니다. (꽝)";
                }
                ApplyAllSatisfaction(satUp);
                InfoFeedUI.Instance?.ShowGlobalSatisfaction(satUp);
                return $"주사위 결과: 1\n\n모든 직원의 만족도가 +{satUp} 상승했습니다!";
            }
            case 2: // 우기 능력치 배율 고정 (다음 축복까지, 우주의 기운 매주 재추첨 정지). 0단계 120~150% / 1·2단계 130~160%
            {
                int lo  = stage >= 1 ? 130 : 120;
                int pct = Random.Range(lo, lo + 31); // lo ~ lo+30
                ugi.cosmicEnergyPercent = pct;
                ugi.cosmicFrozen        = true;
                InfoFeedUI.Instance?.ShowCustom(ugi,
                    $"{InfoFeedUI.Colorize(ugi.employeeName, true)}의 능력치 배율이 {InfoFeedUI.Colorize($"{pct}%", true)}로 고정됐다.");
                return $"주사위 결과: 2\n\n{ugi.employeeName}의 능력치 배율이 <b>{pct}%</b>로 고정됩니다!\n(다음 축복 때까지 매주 변동 정지)";
            }
            case 3: // 랜덤 직원 능력치 +20% (다음 축복까지)
            {
                var target = PickRandomOwnedEmployee();
                if (target == null)
                {
                    InfoFeedUI.Instance?.ShowCustom(ugi, "대상 직원이 없어 축복이 흩어졌다.");
                    return "주사위 결과: 3\n\n...대상 직원이 없어 축복이 흩어졌습니다.";
                }
                target.godBlessingStatPercent = GOD_BLESSING_STAT_PERCENT;
                InfoFeedUI.Instance?.ShowCustom(target,
                    $"{InfoFeedUI.Colorize(target.employeeName, true)}의 능력치가 {InfoFeedUI.Colorize($"{GOD_BLESSING_STAT_PERCENT}%", true)} 상승했다.");
                return $"주사위 결과: 3\n\n<b>{target.employeeName}</b>의 능력치가 +{GOD_BLESSING_STAT_PERCENT}% 상승합니다!\n(다음 축복 때까지 유지)";
            }
            case 4: // 우기 만족도 100 고정(즉시 최대치)
            {
                int before = ugi.satisfaction;
                ugi.satisfaction = 100;
                InfoFeedUI.Instance?.ShowSatisfaction(ugi, ugi.satisfaction - before);
                return $"주사위 결과: 4\n\n{ugi.employeeName}의 만족도가 100으로 고정됐습니다!";
            }
            case 5: // 모든 직원 능력치 +8% (다음 축복까지)
            {
                var em = EmployeeManager.Instance;
                if (em?.ownedEmployees != null)
                    foreach (var e in em.ownedEmployees) e.godBlessingStatPercent = GOD_BLESSING_ALL_STAT_PERCENT;
                InfoFeedUI.Instance?.ShowCustom(ugi,
                    $"모든 직원의 능력치가 {InfoFeedUI.Colorize($"{GOD_BLESSING_ALL_STAT_PERCENT}%", true)} 상승했다.");
                return $"주사위 결과: 5\n\n모든 직원의 능력치가 +{GOD_BLESSING_ALL_STAT_PERCENT}% 상승합니다!\n(다음 축복 때까지 유지)";
            }
            case 6: // 매출 +12% (다음 축복까지)
                ugi.godBlessingSalesActive = true;
                InfoFeedUI.Instance?.ShowCustom(ugi, $"{InfoFeedUI.Colorize(ugi.employeeName, true)} 덕분에 매출이 {InfoFeedUI.Colorize("12%", true)} 상승했다.");
                return "주사위 결과: 6\n\n다음 축복 때까지 게임 매출이 +12% 상승합니다!";
        }
        return null;
    }

    // 신의 축복의 "근원" — grade>=Unique 우기 보유 여부. 우기가 없으면(미보유/해고/도주) 지속 효과(2/3/6)는 발동도 유지도 안 됨.
    // 효과 읽기 시점 게이팅에 사용 → 우기 부재 시 즉시 0 (어떤 경로로 사라져도 robust).
    public static bool HasUniqueUgi()
    {
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return false;
        foreach (var e in em.ownedEmployees)
            if (e.grade >= EmployeeGrade.Unique && !CharacterTraitApplier.IsOnDispatch(e)
                && CharacterTraitApplier.ResolveEventType(e) == "UgiUnique")
                return true;
        return false;
    }

    // 신의 축복 지속형 효과(2/3/6) 일괄 해제 — (a) 다음 축복 발동 시 작년 효과 교체, (b) 우기 퇴장 시 잔존 버프 정리.
    // 한 번에 하나의 축복만 활성 + 우기 재채용 시 옛 버프 부활 방지.
    public static void ClearGodBlessing()
    {
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return;
        foreach (var e in em.ownedEmployees)
        {
            e.godBlessingStatPercent = 0;
            e.godBlessingSalesActive = false;
            e.cosmicFrozen           = false; // 우주의 기운 매주 재추첨 재개 (이번이 주사위 2면 직후 다시 set)
        }
    }

    static EmployeeData PickRandomOwnedEmployee()
    {
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null || em.ownedEmployees.Count == 0) return null;
        return em.ownedEmployees[Random.Range(0, em.ownedEmployees.Count)];
    }

    static void ApplyAllSatisfaction(int amount)
    {
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return;
        foreach (var e in em.ownedEmployees) e.ChangeSatisfaction(amount);
    }

    // SalesUI bonusSum 합연산용 — 신의 축복 주사위 6(매출 +10%) 활성 직원이 있으면 0.10, 없으면 0.
    public static float GetGodBlessingSalesBonus()
    {
        if (!HasUniqueUgi()) return 0f; // 우기 없으면 매출 보너스 즉시 중단
        var em = EmployeeManager.Instance;
        if (em?.ownedEmployees == null) return 0f;
        foreach (var e in em.ownedEmployees)
            if (e.godBlessingSalesActive) return GOD_BLESSING_SALES_BONUS;
        return 0f;
    }
}
