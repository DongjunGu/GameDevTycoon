using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "Item"
// 컬럼: itemId, appearStages, category, name, priceTier, price, effect, description, effectType, effectValue, imageId
//   - appearStages: 등장 시기 ("1,2", "3,4", "1,2,3,4" 등 CSV)
//   - category:     "강화"/"게임"/"만족도"/"능력치"/"테크트리 포인트"/"창의성 블록"/"이벤트 대비"
//   - priceTier:    "하"/"중"/"상"/"최상" (가격 등급 표시용 라벨)
//   - price:        실제 골드 가격 (현재 전부 1G — 추후 튜닝)
//   - effect:       사람이 읽는 효과 텍스트 (인게임 효과 로직은 별도, 일부만 effectType/effectValue 활용)
//   - effectType / effectValue / imageId: 기존 컬럼 (만족도 회복 등 코드 효과 트리거용 — 인게임 구현은 점진 적용 TODO)

public class ItemChartRow
{
    public string itemId;
    public string appearStages;  // "1,2" 등 CSV
    public string category;
    public string name;
    public string priceTier;     // 하/중/상/최상
    public int    price;
    public string effect;
    public string description;
    public string effectType;
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

    // CDN 로드 실패 시 기본값. 사용자 정의 차트(2026-05) 반영. 가격은 전부 1G 임시.
    static Dictionary<string, ItemChartRow> GetFallback()
    {
        var d = new Dictionary<string, ItemChartRow>();

        // ── 강화 ────────────────────────────────────────────────
        d["enhanceLow"] = new ItemChartRow {
            itemId = "enhanceLow", appearStages = "1,2", category = "강화",
            name = "하급 강화권", priceTier = "하", price = 1,
            description = "강화에 실패해도 하락하지 않는다 (1~10성 사이 일때 사용 가능)",
            imageId = "item_enhanceLow"
        };
        d["enhanceMid"] = new ItemChartRow {
            itemId = "enhanceMid", appearStages = "2,3", category = "강화",
            name = "중급 강화권", priceTier = "하", price = 1,
            description = "강화에 실패하면 등급이 하락하는 대신 강화확률이 20% 상승한다",
            imageId = "item_enhanceMid"
        };
        d["enhanceMidPlus"] = new ItemChartRow {
            itemId = "enhanceMidPlus", appearStages = "3,4", category = "강화",
            name = "중급 강화권+", priceTier = "하", price = 1,
            description = "강화에 실패하면 등급이 하락하는 대신 강화확률이 30% 상승한다",
            imageId = "item_enhanceMidPlus"
        };
        d["enhanceHigh"] = new ItemChartRow {
            itemId = "enhanceHigh", appearStages = "4", category = "강화",
            name = "상급 강화권", priceTier = "하", price = 1,
            description = "강화에 실패하면 등급이 하락하는 대신 강화확률이 40% 상승한다",
            imageId = "item_enhanceHigh"
        };
        d["resetSpirit"] = new ItemChartRow {
            itemId = "resetSpirit", appearStages = "1,2,3,4", category = "강화",
            name = "초심 회복기", priceTier = "하", price = 1,
            description = "강화를 단계를 하락시키는 대신 해당 직원 만족도를 100으로 회복한다",
            imageId = "item_resetSpirit"
        };

        // ── 게임 ────────────────────────────────────────────────
        d["upgradeRandom"] = new ItemChartRow {
            itemId = "upgradeRandom", appearStages = "2,3,4", category = "게임",
            name = "랜덤 업그레이드", priceTier = "중", price = 1,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "랜덤한 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeRandom"
        };
        d["upgradeDevelop"] = new ItemChartRow {
            itemId = "upgradeDevelop", appearStages = "3,4", category = "게임",
            name = "개발 업그레이드권", priceTier = "상", price = 1,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "개발 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeDevelop"
        };
        d["upgradeArt"] = new ItemChartRow {
            itemId = "upgradeArt", appearStages = "3,4", category = "게임",
            name = "아트 업그레이드권", priceTier = "상", price = 1,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "아트 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeArt"
        };
        d["upgradePlan"] = new ItemChartRow {
            itemId = "upgradePlan", appearStages = "3,4", category = "게임",
            name = "기획 업그레이드권", priceTier = "상", price = 1,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "기획 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradePlan"
        };

        // ── 만족도 ──────────────────────────────────────────────
        d["coffee"] = new ItemChartRow {
            itemId = "coffee", appearStages = "1,2,3,4", category = "만족도",
            name = "커피", priceTier = "중", price = 1,
            description = "만족도 15 회복",
            effectType = "satisfaction", effectValue = 15, imageId = "item_coffee"
        };
        d["energyDrink"] = new ItemChartRow {
            itemId = "energyDrink", appearStages = "1,2,3,4", category = "만족도",
            name = "에너지드링크", priceTier = "상", price = 1,
            description = "만족도 25 회복",
            effectType = "satisfaction", effectValue = 25, imageId = "item_energyDrink"
        };

        // ── 능력치 ──────────────────────────────────────────────
        d["relax"] = new ItemChartRow {
            itemId = "relax", appearStages = "1,2,3,4", category = "능력치",
            name = "라꾸라꾸", priceTier = "중", price = 1,
            effect = "능력치 감소 상태 회복, 디버프 상태가 아니면 사용 불가능",
            description = "모든 안 좋은 디버프 상태를 회복",
            imageId = "item_relax"
        };
        d["awaken"] = new ItemChartRow {
            itemId = "awaken", appearStages = "1,2,3,4", category = "능력치",
            name = "각성의 물약", priceTier = "중", price = 1,
            effect = "4~8주간 랜덤 / 현재 버프된 능력치는 빼고 오리지널 능력치의 10% 적용",
            description = "잠시동안 능력치 10% 상승",
            imageId = "item_awaken"
        };

        // ── 테크트리 포인트 ─────────────────────────────────────
        d["techNote"] = new ItemChartRow {
            itemId = "techNote", appearStages = "3,4", category = "테크트리 포인트",
            name = "오래된 연구노트", priceTier = "최상", price = 1,
            description = "테크트리 포인트 1 획득",
            imageId = "item_techNote"
        };

        // ── 창의성 블록 ─────────────────────────────────────────
        d["blockRandom"] = new ItemChartRow {
            itemId = "blockRandom", appearStages = "1,2,3,4", category = "창의성 블록",
            name = "랜덤 블록", priceTier = "중", price = 1,
            effect = "3칸 2칸 전부 랜덤",
            description = "1회성으로 언제든지 사용가능 랜덤한 창의성 블록 생성",
            imageId = "item_blockRandom"
        };
        d["blockLegendary"] = new ItemChartRow {
            itemId = "blockLegendary", appearStages = "3,4", category = "창의성 블록",
            name = "전설의 블록", priceTier = "중", price = 1,
            effect = "아예 정사각형 한 칸짜리",
            description = "1회성으로 언제든지 사용가능 1칸짜리 창의성 블록 생성",
            imageId = "item_blockLegendary"
        };

        // ── 이벤트 대비 ─────────────────────────────────────────
        d["hypnotizer"] = new ItemChartRow {
            itemId = "hypnotizer", appearStages = "1,2,3,4", category = "이벤트 대비",
            name = "최면술사의 시계", priceTier = "상", price = 1,
            effect = "직원 사직서 제출시 사용 가능, 아이템이 있으면 사용하기를 누르면 퇴직 취소 되고 만족도 +20",
            description = "직원이 사직서 제출을 취소하고 만족도가 일부 상승한다",
            imageId = "item_hypnotizer"
        };

        return d;
    }

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
                itemId       = id,
                appearStages = S(row, "appearStages"),
                category     = S(row, "category"),
                name         = S(row, "name"),
                priceTier    = S(row, "priceTier"),
                price        = N(row, "price"),
                effect       = S(row, "effect"),
                description  = S(row, "description"),
                effectType   = S(row, "effectType"),
                effectValue  = N(row, "effectValue"),
                imageId      = S(row, "imageId"),
            };
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
