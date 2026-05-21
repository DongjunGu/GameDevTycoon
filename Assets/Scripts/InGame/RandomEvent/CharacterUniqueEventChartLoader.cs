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
    public  static Dictionary<string, CharacterUniqueEventRow> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[CharacterUniqueEventChart] {_cache.Count}개 전용 이벤트 로드 / 키: {string.Join(", ", _cache.Keys)}");
    }

    // 차트 미업로드 시 fallback — 6 캐릭터 전용 이벤트. 문구·효과는 미정(TODO).
    static Dictionary<string, CharacterUniqueEventRow> GetFallback() => new()
    {
        ["KimUnique"]       = E("유리 멘탈 회복", "portrait_kim_01"),
        ["OtakuUnique"]     = E("버튜버 데뷔",     "portrait_otaku_01"),
        ["GoldspoonUnique"] = E("오다 주웠다",     "portrait_goldspoon_01"),
        ["UgiUnique"]       = E("신의 축복",       "portrait_ugi_01"),
        ["GeniusUnique"]    = E("잠 깨우기",       "portrait_genius_01"),
        ["HunsuUnique"]     = E("약점 극복",       "portrait_hunsu_01"),
    };

    static CharacterUniqueEventRow E(string title, string portrait) => new()
    {
        title = title, descriptions = new[] { "(문구 미정 — TODO)" },
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
