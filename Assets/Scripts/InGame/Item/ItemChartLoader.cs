using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "Item"
// 컬럼: itemId, name, description, effectType, effectValue, imageId

public class ItemChartRow
{
    public string itemId;
    public string name;
    public string description;
    public string effectType;   // "satisfaction"
    public int    effectValue;
    public string imageId;
}

public static class ItemChartLoader
{
    private const string CHART_NAME = "Item";
    private static Dictionary<string, ItemChartRow> _cache;
    public static Dictionary<string, ItemChartRow> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[ItemChart] {_cache.Count}개 아이템 로드 완료");
    }

    // CDN 로드 실패 시 기본값 (Item_Chart.csv 기준)
    //   itemId=coffee,       name="커피",         effectType=satisfaction, effectValue=15, imageId=item_coffee
    //   itemId=energyDrink,  name="에너지드링크",  effectType=satisfaction, effectValue=30, imageId=item_energydrink
    static Dictionary<string, ItemChartRow> GetFallback() => new()
    {
        ["coffee"] = new ItemChartRow
        {
            itemId = "coffee", name = "커피",
            description = "직원의 만족도를 회복시킵니다.",
            effectType = "satisfaction", effectValue = 15, imageId = "item_coffee"
        },
        ["energyDrink"] = new ItemChartRow
        {
            itemId = "energyDrink", name = "에너지드링크",
            description = "직원의 만족도를 크게 회복시킵니다.",
            effectType = "satisfaction", effectValue = 30, imageId = "item_energydrink"
        }
    };

    static Dictionary<string, ItemChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, ItemChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[ItemChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[ItemChart] 차트 없음 — 기본값 사용");
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
            string id  = S(row, "itemId");
            if (string.IsNullOrEmpty(id)) continue;

            result[id] = new ItemChartRow
            {
                itemId      = id,
                name        = S(row, "name"),
                description = S(row, "description"),
                effectType  = S(row, "effectType"),
                effectValue = N(row, "effectValue"),
                imageId     = S(row, "imageId"),
            };
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
