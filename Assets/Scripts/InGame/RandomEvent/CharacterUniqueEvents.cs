using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 유니크 등급 직원 전용 이벤트 로직 디스패처 (RandomEventManager 는 스케줄링/위임만 담당).
//
// 데이터: CharacterUniqueEventChartLoader.Cache (key = EmployeeData.uniqueEventType).
// 공유 상태: EmployeeData 의 otakuFixedGenre / lastUniqueEventYear / glassMentalCooldownWeeks 등
//            (특성 CharacterTraitApplier 와 공유).
//
// 구조: uniqueEventType 별 분기. 표시(EventUI)는 공통, 효과는 케이스별 TODO.
// 효과 명세는 [[project_character_trait_event_spec]] 참조. 현재 전부 TODO 스텁.
public static class CharacterUniqueEvents
{
    const float GLASS_MENTAL_RECOVERY_CHANCE = 0.03f; // 매주 3%

    // 매주 1회 (RandomEventManager.CheckCharacterUniqueEvents → 유니크 직원별 호출).
    // 주간 발동형 전용 이벤트(유리 멘탈 회복 등) 의 조건/확률/쿨다운을 처리.
    public static void WeeklyCheck(EmployeeData emp)
    {
        if (emp == null) return;
        switch (CharacterTraitApplier.ResolveEventType(emp))
        {
            case "KimUnique":       CheckGlassMentalRecovery(emp); break;
            case "GoldspoonUnique": CheckGoldspoonGift(emp);       break;
            // 그 외(Ugi/Genius/Hunsu)는 각자 시점(주사위/아이템/99% hook)에서 처리
        }
    }

    // 오다 주웠다 — 매년 1월 중 랜덤한 한 주에 1회, 상점 등장 아이템 중 랜덤 1개 지급.
    static void CheckGoldspoonGift(EmployeeData emp)
    {
        var gt = GameTimeManager.Instance;
        if (gt == null) return;
        if (gt.Month != 1) return;                      // 1월에만
        if (emp.lastUniqueEventYear == gt.Year) return; // 올해 이미 받음
        // 1월(1~4주) 중 정확히 한 주에 발동: 남은 주차 기준 1/(5-week) → 4주차엔 확정 발동
        int weeksLeft = Mathf.Max(1, 5 - gt.Week);
        if (Random.value >= 1f / weeksLeft) return;
        Trigger(emp); // 패널 + ApplyEffect(아이템 지급 + lastUniqueEventYear) + 4-set 저장
    }

    // 약점 극복(HunsuUnique) — 창의성 미니게임 후·디버깅 전 1회(DevelopmentManager.ShowCreativityGame 콜백에서 호출).
    // Unique+ 훈수쟁이 보유 시 기획/개발/아트/창의성 중 최저 파트에 개발 팀장점수의 20% 추가(상한=두 번째로 낮은 값).
    public static void CheckWeaknessOvercome()
    {
        var dm = DevelopmentManager.Instance;
        if (dm == null || dm.IsGameUpgradeUsed("hunsuWeakness")) return; // 프로젝트당 1회(영속 가드)
        var em = EmployeeManager.Instance;
        if (em == null || em.ownedEmployees == null) return;

        EmployeeData hunsu = null;
        foreach (var emp in em.ownedEmployees)
            if (emp.grade >= EmployeeGrade.Unique && CharacterTraitApplier.ResolveEventType(emp) == "HunsuUnique")
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
                var stages = kv.Value.appearStages;
                if (string.IsNullOrEmpty(stages)) continue;
                foreach (var s in stages.Split(','))
                    if (s.Trim() == stageStr) { ids.Add(kv.Key); break; }
            }
        }
        if (ids.Count == 0) ids.AddRange(cache.Keys);
        return ids.Count > 0 ? ids[Random.Range(0, ids.Count)] : null;
    }

    // 유리 멘탈 회복 — 만족도 80 이하일 때 매주 3% 확률로 만족도 100 회복. 달력 연도당 1회(매년 1월 리셋).
    static void CheckGlassMentalRecovery(EmployeeData emp)
    {
        int year = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : 0;
        if (emp.lastUniqueEventYear == year) return;              // 올해 이미 발동 (연 1회, 연도 바뀌면 리셋)
        if (emp.satisfaction > 80) return;                        // 만족도 80 이하에서만
        if (Random.value >= GLASS_MENTAL_RECOVERY_CHANCE) return; // 매주 3%
        Trigger(emp); // 차트 문구 모달 + ApplyEffect(만족도 100 + lastUniqueEventYear=올해)
    }

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

    // eventText 클릭 시 — 전용 이벤트명 + 설명을 AlertUI 로 표시.
    public static void ShowEventDescription(EmployeeData emp)
    {
        if (emp == null || AlertUI.Instance == null || emp.grade < EmployeeGrade.Unique) return;
        string eventType = CharacterTraitApplier.ResolveEventType(emp);
        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue(eventType, out row);
        if (row == null) return;
        string desc = (row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "";
        AlertUI.Instance.Show($"[{row.title}]\n{desc}");
    }

    // 슬롯/카드 공통 — traitText 의 형제 "eventText"(TMP)에 전용 이벤트명 세팅 + 클릭 시 설명 버튼화.
    // eventText 는 프리팹에서 traitText 형제로 생성하므로 traitText 참조로 찾는다 → 별도 직렬화 필드/배선 불필요.
    public static void SetupEventText(TMP_Text traitText, EmployeeData emp)
    {
        if (traitText == null || traitText.transform.parent == null) return;
        var found = traitText.transform.parent.Find("eventText");
        if (found == null) return;
        var eventText = found.GetComponent<TMP_Text>();
        if (eventText == null) return;

        string eventName = GetEventName(emp);
        eventText.text = eventName;

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

    // 전용 이벤트 1건 발동 — 차트 문구 표시 후 케이스별 효과 적용.
    public static void Trigger(EmployeeData emp)
    {
        if (emp == null) return;
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
        ApplyEffect(emp, row, eventType);
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance?.SaveProject();
        GameTimeManager.Instance?.SaveGameTime();

        // 패널은 ModalGate 큐로 순차 표시 — 같은 주에 여러 전용 이벤트/사직 패널이 겹쳐도 하나씩 차례로.
        // 표시 동안 StopTime ↔ 확인 시 StartTime(카운터 균형) → 마지막 패널이 닫힐 때만 시간 재개.
        ModalGate.I.WhenFree(() =>
        {
            GameTimeManager.Instance?.StopTime();
            ShowEventPanel(emp, row, () => GameTimeManager.Instance?.StartTime());
        });
    }

    // EventChoicePanel(RandomEventChoiceUI) 로 전용 이벤트 표시 — 선택지 없음(ChoiceButtonContainer 미사용),
    // 제목 "{이벤트명} 발동", 설명 = description1, 초상화 = 해당 직원. onConfirm 은 확인 시 콜백(시간 관리는 호출자 책임).
    static void ShowEventPanel(EmployeeData emp, CharacterUniqueEventRow row, System.Action onConfirm)
    {
        if (RandomEventChoiceUI.Instance == null) { onConfirm?.Invoke(); return; }
        string desc = (row.descriptions != null && row.descriptions.Length > 0) ? row.descriptions[0] : "";
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
            if (CharacterTraitApplier.ResolveEventType(emp) != "OtakuUnique") continue;
            if (!string.IsNullOrEmpty(emp.otakuFixedGenre) && emp.otakuFixedGenre == genreName) { otaku = emp; break; }
        }
        if (otaku == null) { onDone?.Invoke(); return; }
        if (ProjectSetupUI.SelectedGenrePopularity >= 3) { onDone?.Invoke(); return; } // 이미 3단계면 발동 안 함

        // 효과: 이번 프로젝트 인기도(스냅샷) + 표시 인기도를 3단계로
        ProjectSetupUI.SelectedGenrePopularity = 3;
        GenrePopularityManager.Instance?.SetPopularity(ProjectSetupUI.SelectedGenre, 3);

        CharacterUniqueEventRow row = null;
        CharacterUniqueEventChartLoader.Cache?.TryGetValue("OtakuUnique", out row);
        if (row == null) { onDone?.Invoke(); return; }
        ShowEventPanel(otaku, row, onDone); // 시간은 이미 정지 — 확인 시 setup 계속
    }

    static void ApplyEffect(EmployeeData emp, CharacterUniqueEventRow row, string eventType)
    {
        switch (eventType)
        {
            case "KimUnique":       // 유리 멘탈 회복 — 만족도 100 회복 + 올해 발동 기록 (연 1회, 발동 조건/확률은 WeeklyCheck 에서)
                emp.satisfaction = 100;
                emp.lastUniqueEventYear = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : emp.lastUniqueEventYear;
                // 저장은 Trigger 의 4-set(SaveAllEmployees 포함)에서 일괄 처리 — 여기서 별도 UpdateEmployee 안 함(중복/동시 쓰기 방지)
                break;
            case "OtakuUnique":     // 버튜버 데뷔 — 개발 시작 hook(CheckVtuberDebut)에서 처리. 이 경로(Trigger/ApplyEffect)로는 안 옴.
                break;
            case "GoldspoonUnique": // 오다 주웠다 — 상점 등장 아이템 중 랜덤 1개 지급 + 올해 발동 기록
                string giftId = PickRandomShopItem();
                if (!string.IsNullOrEmpty(giftId))
                {
                    ItemManager.Instance?.AddItem(giftId); // 인벤토리 추가 + UserItems 저장
                    Debug.Log($"[오다 주웠다] {emp.employeeName} → 아이템 '{giftId}' 지급");
                }
                emp.lastUniqueEventYear = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : emp.lastUniqueEventYear;
                break;
            case "UgiUnique":       // 신의 축복
                // TODO: 1년에 한 번 주사위(d6):
                //   1=꽝 / 2=우기 능력치 100~130% 중 특정값 고정 / 3=랜덤 직원 능력치 +10% /
                //   4=우기 만족도 +20 / 5=모든 직원 만족도 +10 / 6=매출 +10%
                //   (연 1회, lastUniqueEventYear 로 중복 방지)
                break;
            case "GeniusUnique":    // 잠 깨우기 — 커피 사용 hook(ItemManager.TryGeniusWakeUp)이 조건 확인 후 Trigger 호출.
                // 효과: 개발 업그레이드권과 동일하게 팀장 점수의 1/4 를 개발 점수로 추가 + 프로젝트당 1회 마킹.
                {
                    var dm = DevelopmentManager.Instance;
                    if (dm != null)
                    {
                        int rounded = Mathf.Max(1, Mathf.RoundToInt(dm.CalcGameUpgradeScore(emp, emp.role)));
                        DevelopmentPanelUI.Instance?.AddValuesInstant(
                            emp.role == EmployeeRole.Planner    ? rounded : 0f,
                            emp.role == EmployeeRole.Programmer ? rounded : 0f,
                            emp.role == EmployeeRole.Artist     ? rounded : 0f,
                            0f, 0f);
                        dm.MarkGameUpgradeUsed("geniusWakeup");
                    }
                }
                break;
            case "HunsuUnique":     // 약점 극복 — 기획/개발/아트/창의성 중 최저 파트에 개발 팀장점수의 20% 추가.
                                    // 단 두 번째로 낮은 값을 상한으로(최저를 그 값까지만). 프로젝트당 1회.
                {
                    var dm = DevelopmentManager.Instance;
                    var ui = DevelopmentPanelUI.Instance;
                    if (dm == null || ui == null) break;
                    int raise = Mathf.Max(1, Mathf.RoundToInt(dm.LeaderDevelopBonusTotal * 0.2f));

                    // 4개 파트 [기획, 개발, 아트, 창의성] 현재값
                    float[] vals = { ui.GetPlanning(), ui.GetDevelop(), ui.GetArt(), ui.GetCreativity() };
                    int minIdx = 0;
                    for (int i = 1; i < 4; i++) if (vals[i] < vals[minIdx]) minIdx = i;
                    // 두 번째로 낮은 값(= 최저 제외 나머지 중 최소) 을 상한으로
                    float secondLowest = float.MaxValue;
                    for (int i = 0; i < 4; i++) if (i != minIdx && vals[i] < secondLowest) secondLowest = vals[i];
                    int actual = Mathf.Max(0, Mathf.Min(raise, Mathf.RoundToInt(secondLowest - vals[minIdx])));

                    ui.AddValuesInstant(
                        minIdx == 0 ? actual : 0f,  // 기획
                        minIdx == 1 ? actual : 0f,  // 개발
                        minIdx == 2 ? actual : 0f,  // 아트
                        0f,                         // 버그
                        minIdx == 3 ? actual : 0f); // 창의성
                    dm.MarkGameUpgradeUsed("hunsuWeakness");
                }
                break;
            default:
                Debug.LogWarning($"[CharacterUniqueEvent] '{eventType}' 효과 미구현");
                break;
        }
    }
}
