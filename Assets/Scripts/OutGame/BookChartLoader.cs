using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 CDN 차트 이름: "Book"  (원본 CSV: Assets/Scripts/OutGame/Book_Chart.csv)
// 컬럼: level, needBook, needGold
//   - level    : 현재 레벨 (1~39). 이 행의 비용 = level → level+1 강화 비용
//   - needBook : 필요 책
//   - needGold : 필요 골드 (아웃게임 골드)
//
// 차트 없거나 로드 실패 시 아래 GetFallback() 값 사용.
public static class BookChartLoader
{
    private const string CHART_NAME = "Book";
    private static Dictionary<int, (int book, int gold)> _cache;
    public static Dictionary<int, (int book, int gold)> Cache => _cache ??= GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[BookChart] {_cache.Count}개 로드 완료");
    }

    // level → level+1 비용. 행이 없으면(=MAX) false.
    public static bool TryGetCost(int level, out int book, out int gold)
    {
        bool found = Cache.TryGetValue(level, out var cost);
        book = cost.book;
        gold = cost.gold;
        return found;
    }

    // CDN 로드 실패 시 기본값. 골드 = max(2000, 책 × 60).
    static Dictionary<int, (int book, int gold)> GetFallback()
    {
        int[] books =
        {
            5, 14, 26, 40, 56, 73, 93, 115, 135, 160,
            180, 210, 235, 260, 290, 320, 350, 380, 415, 445,
            480, 515, 550, 590, 625, 665, 700, 740, 780, 820,
            865, 905, 950, 990, 1040, 1080, 1130, 1170, 1220,
        };
        var d = new Dictionary<int, (int book, int gold)>();
        for (int i = 0; i < books.Length; i++)
            d[i + 1] = (books[i], Mathf.Max(2000, books[i] * 60));
        return d;
    }

    static Dictionary<int, (int book, int gold)> LoadFromServer()
    {
        var result = new Dictionary<int, (int book, int gold)>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[BookChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[BookChart] 차트 없음 — 기본값 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess()) return result;

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId)) return result;

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var row   = rows[i];
            int level = N(row, "level");
            if (level <= 0) continue;
            result[level] = (N(row, "needBook"), N(row, "needGold"));
        }
        return result;
    }

    static int N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
