using System.Collections.Generic;
using BackEnd.Content;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔에 등록할 차트 이름: "RandomEvent"
// 컬럼 목록:
//   eventType (string)  — RandomEventType enum 이름과 일치
//   title     (string)
//   description  (string)  — 동적으로 onSetup에서 세팅하는 경우 빈칸
//   systemMessage (string) — 동적이면 빈칸
//   weight    (float)
//   categoryMin (int)
//   categoryMax (int)
//   portraitId  (string)   — 고정 초상화 없으면 빈칸
//   requiresPatrol (bool)
//   requiredPatrolPointId (string)

public class RandomEventChartRow
{
    public string title;
    public string description;
    public string systemMessage;
    public float  weight;
    public int    categoryMin;
    public int    categoryMax;
    public string portraitId;
    public bool   requiresPatrol;
    public string requiredPatrolPointId;
}

public static class RandomEventChartLoader
{
    private const string CHART_NAME = "RandomEvent";

    // BackendManager.LoadAllAndEnterGame()에서 로그인 후 한 번만 호출
    // 이후에는 Cache 프로퍼티로 참조
    private static Dictionary<string, RandomEventChartRow> _cache = null;
    public static Dictionary<string, RandomEventChartRow> Cache => _cache;

    public static void Load()
    {
        _cache = LoadFromServer();
    }

    // 차트 데이터를 이벤트에 덮어씌움 (비어있는 값은 코드 기본값 유지)
    public static void Apply(RandomEventData evt, Dictionary<string, RandomEventChartRow> chart)
    {
        if (chart == null || !chart.TryGetValue(evt.type.ToString(), out var row)) return;

        if (!string.IsNullOrEmpty(row.title)) evt.title = row.title;
        evt.weight  = row.weight;
        evt.categoryMin = row.categoryMin;
        evt.categoryMax = row.categoryMax;
        evt.requiresPatrol        = row.requiresPatrol;
        evt.requiredPatrolPointId = row.requiredPatrolPointId;

        // 동적으로 onSetup에서 세팅하는 값은 빈칸이면 코드 기본값 유지
        if (!string.IsNullOrEmpty(row.description))   evt.description   = row.description;
        if (!string.IsNullOrEmpty(row.systemMessage))  evt.systemMessage = row.systemMessage;
        if (!string.IsNullOrEmpty(row.portraitId))     evt.portraitId    = row.portraitId;
    }

    // ── 내부 로드 ──────────────────────────────────────────────────
    static Dictionary<string, RandomEventChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, RandomEventChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventChart] 테이블 조회 실패 — 코드 기본값 사용: {tableResult}");
            return result;
        }

        var tableList   = tableResult.GetContentTableItemList();
        string chartId  = null;
        foreach (var item in tableList)
        {
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }
        }

        if (chartId == null)
        {
            Debug.LogWarning($"[RandomEventChart] '{CHART_NAME}' 차트 없음 — 코드 기본값 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventChart] 내용 로드 실패 — 코드 기본값 사용: {contentResult}");
            return result;
        }

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId))
        {
            Debug.LogWarning($"[RandomEventChart] chartId 없음: {chartId}");
            return result;
        }

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var row      = rows[i];
            string type  = S(row, "eventType");
            if (string.IsNullOrEmpty(type)) continue;

            result[type] = new RandomEventChartRow
            {
                title                 = S(row, "title"),
                description           = S(row, "description"),
                systemMessage         = S(row, "systemMessage"),
                weight                = F(row, "weight"),
                categoryMin           = N(row, "categoryMin"),
                categoryMax           = N(row, "categoryMax"),
                portraitId            = S(row, "portraitId"),
                requiresPatrol        = B(row, "requiresPatrol"),
                requiredPatrolPointId = S(row, "requiredPatrolPointId"),
            };
        }

        Debug.Log($"[RandomEventChart] {result.Count}개 이벤트 로드 완료");
        return result;
    }

    // ── 헬퍼 ───────────────────────────────────────────────────────
    static string S(JsonData r, string k) { try { return r[k].ToString(); }  catch { return ""; }   }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
    static float  F(JsonData r, string k) { try { return float.Parse(r[k].ToString(),
                                                   System.Globalization.CultureInfo.InvariantCulture); }
                                             catch { return 0f; } }
    static bool   B(JsonData r, string k) { try { return r[k].ToString().ToLower() == "true"; } catch { return false; } }
}
