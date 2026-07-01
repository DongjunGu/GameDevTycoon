using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔에 등록할 차트 이름: "RandomEventCondition"
// 컬럼 목록:
//   eventType   (string)
//   title       (string)
//   description1~3 (string) — 복수 문구 랜덤 선택용, 비어있는 항목은 무시
//   systemMessage  (string) — ResultBG에 표시
//   systemMessage2 (string) — ResultBG2에 표시
//   titleType   (int)      — 0=부정, 1=중립, 2=긍정

public class RandomEventConditionChartRow
{
    public string   title;
    public string[] descriptions; // description1~3 중 비어있지 않은 것만
    public string   systemMessage;
    public string   systemMessage2;
    public string   portraitId;
    public bool     requiresPatrol;
    public string   requiredPatrolPointId;
    public int      titleType; // 0=부정, 1=중립, 2=긍정
}

public static class RandomEventConditionChartLoader
{
    private const string CHART_NAME = "RandomEventCondition";
    private const int    MAX_DESCS  = 5;

    private static Dictionary<string, RandomEventConditionChartRow> _cache = null;
    public  static Dictionary<string, RandomEventConditionChartRow> Cache => _cache;

    public static void Load()
    {
        _cache = LoadFromServer();
        Debug.Log($"[RandomEventConditionChart] 로드 결과: {_cache.Count}개 / 키: {string.Join(", ", _cache.Keys)}");
    }

    // ── 내부 로드 ──────────────────────────────────────────────────
    static Dictionary<string, RandomEventConditionChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, RandomEventConditionChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventConditionChart] 테이블 조회 실패 — 코드 기본값 사용: {tableResult}");
            return result;
        }

        var tableList  = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
        {
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }
        }

        if (chartId == null)
        {
            Debug.LogWarning($"[RandomEventConditionChart] '{CHART_NAME}' 차트 없음 — 코드 기본값 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventConditionChart] 내용 로드 실패 — 코드 기본값 사용: {contentResult}");
            return result;
        }

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId))
        {
            Debug.LogWarning($"[RandomEventConditionChart] chartId 없음: {chartId}");
            return result;
        }

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

            result[type] = new RandomEventConditionChartRow
            {
                title                 = S(row, "title"),
                descriptions          = descList.ToArray(),
                systemMessage         = S(row, "systemMessage"),
                systemMessage2        = S(row, "systemMessage2"),
                portraitId            = S(row, "portraitId"),
                requiresPatrol        = B(row, "requiresPatrol"),
                requiredPatrolPointId = S(row, "requiredPatrolPointId"),
                titleType             = N(row, "titleType"),
            };
        }

        Debug.Log($"[RandomEventConditionChart] {result.Count}개 이벤트 로드 완료");
        return result;
    }

    // ── 헬퍼 ───────────────────────────────────────────────────────
    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
    static bool   B(JsonData r, string k) { try { return r[k].ToString().ToLower() == "true"; } catch { return false; } }
}
