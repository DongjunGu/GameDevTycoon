using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 CDN 차트 이름: "MailReward"
// 컬럼: mailRewardId, type, amount, description
//   - mailRewardId : 뒤끝 우편(관리자 우편) 아이템의 "아이템 이름"에 넣는 값 = 이 차트의 키
//   - type         : "gold" / "diamond" (아웃게임 재화)
//   - amount       : 지급 수량 (우편 chargeAmount 가 2 이상이면 amount × chargeAmount 로 배수 지급)
//   - description  : 사람이 읽는 설명 (수령 요약 표시용, 선택)
//
// [운영]
//  콘솔 → 우편 발송 시 아이템 이름에 mailRewardId(예: "gold_5000") 입력.
//  아이템 커스텀 JSON은 필요 없음 — 실제 지급 내용은 전부 이 차트가 정의한다.
//  차트 없거나 로드 실패 시 아래 GetFallback() 값 사용.

public class MailRewardChartRow
{
    public string mailRewardId;
    public string type;    // "gold" / "diamond"
    public int    amount;
    public string description;
}

public static class MailRewardChartLoader
{
    private const string CHART_NAME = "MailReward";
    private static Dictionary<string, MailRewardChartRow> _cache;
    public static Dictionary<string, MailRewardChartRow> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[MailRewardChart] {_cache.Count}개 로드 완료 / 키: {string.Join(", ", _cache.Keys)}");
    }

    // CDN 로드 실패 시 기본값.
    static Dictionary<string, MailRewardChartRow> GetFallback()
    {
        var d = new Dictionary<string, MailRewardChartRow>();
        void Add(string id, string type, int amount, string desc)
            => d[id] = new MailRewardChartRow { mailRewardId = id, type = type, amount = amount, description = desc };

        Add("gold_1000",   "gold",    1000,  "골드 1,000");
        Add("gold_5000",   "gold",    5000,  "골드 5,000");
        Add("gold_10000",  "gold",   10000,  "골드 10,000");
        Add("diamond_10",  "diamond",   10,  "다이아 10");
        Add("diamond_50",  "diamond",   50,  "다이아 50");
        Add("diamond_100", "diamond",  100,  "다이아 100");
        return d;
    }

    static Dictionary<string, MailRewardChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, MailRewardChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[MailRewardChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[MailRewardChart] 차트 없음 — 기본값 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess()) return result;

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId)) return result;

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var    row = rows[i];
            string id  = S(row, "mailRewardId");
            if (string.IsNullOrEmpty(id)) continue;

            result[id] = new MailRewardChartRow
            {
                mailRewardId = id,
                type         = S(row, "type"),
                amount       = N(row, "amount"),
                description  = S(row, "description"),
            };
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
