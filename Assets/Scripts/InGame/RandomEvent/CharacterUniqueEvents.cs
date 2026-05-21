using UnityEngine;

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

        ShowModal(emp, row);
        ApplyEffect(emp, row, eventType);
    }

    static void ShowModal(EmployeeData emp, CharacterUniqueEventRow row)
    {
        string desc = (row.descriptions != null && row.descriptions.Length > 0)
            ? row.descriptions[Random.Range(0, row.descriptions.Length)]
            : "";
        string portrait = !string.IsNullOrEmpty(row.portraitId) ? row.portraitId : emp.portraitId;
        EventUI.Instance?.Show(row.title, portrait, desc);
    }

    static void ApplyEffect(EmployeeData emp, CharacterUniqueEventRow row, string eventType)
    {
        switch (eventType)
        {
            case "KimUnique":       // 유리 멘탈 회복 — 만족도 80↓ 매주 3% 확률 100 회복, 발동 시 1년 쿨다운
                // TODO: 만족도 100 회복 + emp.glassMentalCooldownWeeks = 52
                break;
            case "OtakuUnique":     // 버튜버 데뷔 — 고정 장르 인기도 1·2단계 → 3단계 상승
                // TODO: emp.otakuFixedGenre 인기도 3단계로
                break;
            case "GoldspoonUnique": // 오다 주웠다 — 매년 1월 상점 아이템 중 랜덤 1개 지급
                // TODO: 랜덤 아이템 지급
                break;
            case "UgiUnique":       // 신의 축복 — 연 1회 주사위(d6) 랜덤 버프
                // TODO: d6 분기 (꽝/능력치고정/랜덤직원+10%/만족도+20/전원+10/매출+10%)
                break;
            case "GeniusUnique":    // 잠 깨우기 — 커피 사용 시 팀장 점수 1/4 적용 (이벤트라기보다 아이템 hook)
                // TODO: ItemManager 커피 사용 경로에서 호출
                break;
            case "HunsuUnique":     // 약점 극복 — 99% 시점 최저 파트 점수 상승
                // TODO: DevelopmentManager 99% hook 에서 호출
                break;
            default:
                Debug.LogWarning($"[CharacterUniqueEvent] '{eventType}' 효과 미구현");
                break;
        }
    }
}
