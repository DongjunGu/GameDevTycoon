using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "Item"
// 컬럼: itemId, appearStages, category, name, grade, price, effect, description, effectType, effectValue, imageId
//   - appearStages: 등장 시기 ("1,2", "3,4", "1,2,3,4" 등 CSV)
//   - category:     "강화"/"게임"/"만족도"/"능력치"/"테크트리 포인트"/"창의성 블록"/"이벤트 대비"
//   - grade:        아이템 등급 1~4 — 2026-08-14부터 가격 등급(하/중/상/최상)과 1:1 매핑
//   - price:        실제 골드 가격 (하=100 / 중=300 / 상=800 / 최상=2000, grade와 동일 기준)
//   - effect:       사람이 읽는 효과 텍스트 (인게임 효과 로직은 별도, 일부만 effectType/effectValue 활용)
//   - effectType / effectValue / imageId: 기존 컬럼 (만족도 회복 등 코드 효과 트리거용 — 인게임 구현은 점진 적용 TODO)

public class ItemChartRow
{
    public string itemId;
    public string appearStages;  // "1,2" 등 CSV
    public string category;
    public string name;
    public int    grade;         // 아이템 등급 1~4
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
        // 2026-08-14 가격/등급 재조정 — 가격 등급(하/중/상/최상) = grade(1~4)와 1:1 매핑,
        // 하=100G/중=300G/상=800G/최상=2000G. 기존엔 강화류가 전부 1등급이었으나 아이템별로 갈라짐.
        // 2026-08-14 이름/효과 전면 개편 — 실제 기능이 처음 구현됨(이전엔 설명 텍스트만 있고 effectType이
        // 비어있어 죽은 효과였음). enhanceProtect는 "사용" 버튼이 없는 아이템(ItemDetailUI에서 항상 비활성) —
        // 하락 판정이 났을 때 EmployeeEnhancement.EnhanceOnce가 자동으로 소모한다.
        // 2026-08-14 itemId를 이름에 맞게 재정렬(하락방어권=enhanceProtect, 하급=enhanceLow, 중급=enhanceMid,
        // 상급=enhanceHigh — 이전엔 enhanceLow가 하락방어권을 가리키는 등 이름과 안 맞았음). imageId는 기존
        // 그대로 유지(에셋 경로 변경 없음).
        d["enhanceProtect"] = new ItemChartRow {
            itemId = "enhanceProtect", appearStages = "1,2", category = "강화",
            name = "하락 방어권", grade = 1, price = 100,
            description = "강화에 실패하여 단계가 하락하는걸 자동으로 방어한다 (11~24성 사이 일때 사용 가능)",
            imageId = "item_enhanceLow"
        };
        // 하급/중급/상급 강화권 — effectType="enhanceSuccessBoost", effectValue = 성공확률 가산(%p).
        // 사용 시 즉시 소모(대상 직원 선택) 후 TrainingPanelUI가 그 직원의 "다음 강화 1회"에만 적용.
        d["enhanceLow"] = new ItemChartRow {
            itemId = "enhanceLow", appearStages = "2,3", category = "강화",
            name = "하급 강화권", grade = 2, price = 300,
            description = "다음 강화 성공 확률 + 5%",
            effectType = "enhanceSuccessBoost", effectValue = 5, imageId = "item_enhanceMid"
        };
        d["enhanceMid"] = new ItemChartRow {
            itemId = "enhanceMid", appearStages = "3,4", category = "강화",
            name = "중급 강화권", grade = 3, price = 800,
            description = "다음 강화 성공 확률 + 10%",
            effectType = "enhanceSuccessBoost", effectValue = 10, imageId = "item_enhanceMidPlus"
        };
        d["enhanceHigh"] = new ItemChartRow {
            itemId = "enhanceHigh", appearStages = "4", category = "강화",
            name = "상급 강화권", grade = 4, price = 2000,
            description = "다음 강화 성공 확률을 100%로 바꾼다.",
            effectType = "enhanceSuccessBoost", effectValue = 100, imageId = "item_enhanceHigh"
        };
        // 초심 회복기 — 하급/중급/상급 강화권과 동일한 흐름(사용→대상 직원 선택→즉시 소모→TrainingPanel로
        // 직행)이지만 효과는 다름: "다음 강화 1회"가 하락 판정이 나면 하락 대신 만족도 100 회복으로 대체.
        // 실제로 발동했을 때(하락이 났을 때만)는 AlertUI3(ShowPortrait)로 안내(EmployeeEnhancement.EnhanceOnce 참고).
        d["resetSpirit"] = new ItemChartRow {
            itemId = "resetSpirit", appearStages = "1,2,3,4", category = "강화",
            name = "초심 회복기", grade = 2, price = 300,
            description = "강화를 단계를 하락시키는 대신 해당 직원 만족도를 100으로 회복한다",
            effectType = "enhanceResetSpirit", imageId = "item_resetSpirit"
        };

        // ── 게임 ────────────────────────────────────────────────
        d["upgradeRandom"] = new ItemChartRow {
            itemId = "upgradeRandom", appearStages = "2,3,4", category = "게임",
            name = "랜덤 업그레이드", grade = 2, price = 300,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "랜덤한 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeRandom"
        };
        d["upgradeDevelop"] = new ItemChartRow {
            itemId = "upgradeDevelop", appearStages = "3,4", category = "게임",
            name = "개발 업그레이드권", grade = 3, price = 800,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "개발 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeDevelop"
        };
        d["upgradeArt"] = new ItemChartRow {
            itemId = "upgradeArt", appearStages = "3,4", category = "게임",
            name = "아트 업그레이드권", grade = 3, price = 800,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "아트 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradeArt"
        };
        d["upgradePlan"] = new ItemChartRow {
            itemId = "upgradePlan", appearStages = "3,4", category = "게임",
            name = "기획 업그레이드권", grade = 3, price = 800,
            effect = "기존 팀장 점수 로직의 1/4가 적용",
            description = "기획 직군 직원이 게임성 업그레이드를 시도한다 (게임 하나에 한 번만 사용 가능)",
            imageId = "item_upgradePlan"
        };

        // ── 만족도 ──────────────────────────────────────────────
        d["coffee"] = new ItemChartRow {
            itemId = "coffee", appearStages = "1,2,3,4", category = "만족도",
            name = "커피", grade = 2, price = 300,
            description = "만족도 15 회복",
            effectType = "satisfaction", effectValue = 15, imageId = "item_coffee"
        };
        // 신규 — 만족도를 1~25 사이에서 랜덤으로 회복(effectType="satisfactionRandom", ItemManager.cs 참고).
        d["mysteryPotion"] = new ItemChartRow {
            itemId = "mysteryPotion", appearStages = "1,2,3,4", category = "만족도",
            name = "수상한 물약", grade = 3, price = 800,
            effect = "만족도 1~25 랜덤하게 회복",
            description = "만족도가 랜덤하게 회복됩니다.",
            effectType = "satisfactionRandom", effectValue = 25, imageId = "item_mysteryPotion"
        };
        d["energyDrink"] = new ItemChartRow {
            itemId = "energyDrink", appearStages = "1,2,3,4", category = "만족도",
            name = "에너지드링크", grade = 4, price = 2000,
            description = "만족도 25 회복",
            effectType = "satisfaction", effectValue = 25, imageId = "item_energyDrink"
        };

        // ── 능력치 ──────────────────────────────────────────────
        d["relax"] = new ItemChartRow {
            itemId = "relax", appearStages = "1,2,3,4", category = "능력치",
            name = "라꾸라꾸", grade = 1, price = 100,
            effect = "능력치 감소 상태 회복, 디버프 상태가 아니면 사용 불가능",
            description = "모든 안 좋은 디버프 상태를 회복",
            imageId = "item_relax"
        };
        d["awaken"] = new ItemChartRow {
            itemId = "awaken", appearStages = "1,2,3,4", category = "능력치",
            name = "각성의 물약", grade = 2, price = 300,
            effect = "4~8주간 랜덤 / 현재 버프된 능력치는 빼고 오리지널 능력치의 10% 적용",
            description = "잠시동안 능력치 10% 상승",
            imageId = "item_awaken"
        };

        // ── 테크트리 포인트 ─────────────────────────────────────
        d["techNote"] = new ItemChartRow {
            itemId = "techNote", appearStages = "3,4", category = "테크트리 포인트",
            name = "오래된 연구노트", grade = 4, price = 2000,
            description = "테크트리 포인트 1 획득",
            imageId = "item_techNote"
        };

        // ── 창의성 블록 ─────────────────────────────────────────
        d["blockRandom"] = new ItemChartRow {
            itemId = "blockRandom", appearStages = "1,2,3,4", category = "창의성 블록",
            name = "랜덤 블록", grade = 1, price = 100,
            effect = "3칸 2칸 전부 랜덤",
            description = "1회성으로 언제든지 사용가능 랜덤한 창의성 블록 생성",
            imageId = "item_blockRandom"
        };
        d["blockLegendary"] = new ItemChartRow {
            itemId = "blockLegendary", appearStages = "3,4", category = "창의성 블록",
            name = "전설의 블록", grade = 2, price = 300,
            effect = "아예 정사각형 한 칸짜리",
            description = "1회성으로 언제든지 사용가능 1칸짜리 창의성 블록 생성",
            imageId = "item_blockLegendary"
        };

        // ── 이벤트 대비 ─────────────────────────────────────────
        d["hypnotizer"] = new ItemChartRow {
            itemId = "hypnotizer", appearStages = "1,2,3,4", category = "이벤트 대비",
            name = "최면술사의 시계", grade = 4, price = 2000,
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
                grade        = N(row, "grade"),
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
