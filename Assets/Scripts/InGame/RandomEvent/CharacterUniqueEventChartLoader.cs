using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 유니크 등급 직원 전용 이벤트 차트 (일반 조건 이벤트 RandomEventCondition 와 별개)
// 뒤끝 콘솔 차트 이름: "CharacterUniqueEvent"
// 컬럼: eventType(string), title(string), description1~5(string), systemMessage(string), portraitId(string)
// key = eventType. EmployeeData.uniqueEventType (grade >= Unique 일 때 발동 후보) 가 이 key 를 가리킴.
// 발동 조건/확률은 RandomEventManager.CheckConditionEvents 의 TODO (현재 비활성).

public class CharacterUniqueEventRow
{
    public string   title;
    public string[] descriptions; // description1~5 중 비어있지 않은 것
    public string   systemMessage;
    public string   portraitId;
}

public static class CharacterUniqueEventChartLoader
{
    private const string CHART_NAME = "CharacterUniqueEvent";
    private const int    MAX_DESCS  = 5;

    private static Dictionary<string, CharacterUniqueEventRow> _cache;
    public  static Dictionary<string, CharacterUniqueEventRow> Cache => _cache; // 테스트: fallback 비활성 (원복: => _cache ?? GetFallback();)

    public static void Load()
    {
        _cache = LoadFromServer();
        // 테스트: fallback 비활성 — 뒤끝 "CharacterUniqueEvent" 차트 적용 여부 확인용. (원복 시 아래 2줄 주석 해제)
        // if (_cache == null || _cache.Count == 0)
        //     _cache = GetFallback();
        Debug.Log($"[CharacterUniqueEventChart] {(_cache?.Count ?? 0)}개 전용 이벤트 로드 / 키: {(_cache != null ? string.Join(", ", _cache.Keys) : "")} (fallback 비활성 — 0개면 뒤끝 차트 못 찾음)");
    }

    // 차트 미업로드 시 fallback — 6 캐릭터 전용 이벤트. 설명은 player-facing 문구(상세 로직은 CharacterUniqueEvents TODO).
    static Dictionary<string, CharacterUniqueEventRow> GetFallback() => new()
    {
        ["KimUnique"]       = E("유리 멘탈 회복", "만족도가 80 이하일 때 낮은 확률로 100까지 회복합니다", "portrait_kim_01"),
        ["OtakuUnique"]     = E("버튜버 데뷔",     "방송을 통해 좋아하는 장르를 더욱 인기있게 만들어줍니다", "portrait_otaku_01"),
        ["GoldspoonUnique"] = E("오다 주웠다",     "1년에 한 번씩 아이템을 랜덤하게 하나를 제공합니다", "portrait_goldspoon_01"),
        ["UgiUnique"]       = E("신의 축복",       "1년에 한 번 주사위를 던져서 랜덤한 버프를 본인에게 제공합니다", "portrait_ugi_01"),
        ["GeniusUnique"]    = E("잠 깨우기",       "커피 아이템을 사용할 경우 개발 점수가 소폭 상승합니다", "portrait_genius_01"),
        ["HunsuUnique"]     = E("약점 극복",       "출시 직전 게임의 가장 점수가 낮은 파트의 점수를 증가시킵니다", "portrait_hunsu_01"),
    };

    static CharacterUniqueEventRow E(string title, string desc, string portrait) => new()
    {
        title = title, descriptions = new[] { desc },
        systemMessage = "", portraitId = portrait
    };

    static Dictionary<string, CharacterUniqueEventRow> LoadFromServer()
    {
        var result = new Dictionary<string, CharacterUniqueEventRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[CharacterUniqueEventChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList  = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[CharacterUniqueEventChart] 차트 없음 — fallback 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess()) return result;

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId)) return result;

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var    row  = rows[i];
            string type = S(row, "eventType");
            if (string.IsNullOrEmpty(type)) continue;

            var descList = new List<string>();
            for (int d = 1; d <= MAX_DESCS; d++)
            {
                string desc = S(row, $"description{d}");
                if (!string.IsNullOrEmpty(desc)) descList.Add(desc);
            }

            result[type] = new CharacterUniqueEventRow
            {
                title         = S(row, "title"),
                descriptions  = descList.ToArray(),
                systemMessage = S(row, "systemMessage"),
                portraitId    = S(row, "portraitId"),
            };
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
}
