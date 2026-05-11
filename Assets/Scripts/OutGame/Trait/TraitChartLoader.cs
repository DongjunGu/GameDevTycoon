using System;
using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "Trait"
// 컬럼: traitId(string), name(string), description(string),
//       grade(string "S"/"A"/"B"/"C"), effectType(string), effectValue(int)
// 주의: "id"는 뒤끝 예약 키 → traitId 사용 (feedback_backend_reserved_keys.md)

public class TraitChartRow
{
    public string     traitId;
    public string     name;
    public string     description;
    public TraitGrade grade;
    public string     effectType;
    public int        effectValue;
}

public static class TraitChartLoader
{
    private const string CHART_NAME = "Trait";
    private static Dictionary<string, TraitChartRow> _cache;
    public static Dictionary<string, TraitChartRow> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[TraitChart] {_cache.Count}개 특성 로드 완료");
    }

    // 차트 미업로드 시 fallback — 현재 명단(C 5/B 2/A 2/S 0)
    static Dictionary<string, TraitChartRow> GetFallback() => new()
    {
        // // ===== C =====
        // ["trait_silver_spoon"] = new TraitChartRow {
        //     traitId = "trait_silver_spoon", name = "은수저",
        //     description = "시작 시 3000G 추가 보유",
        //     grade = TraitGrade.C, effectType = "startGold", effectValue = 3000 },
        // ["trait_caffeine"] = new TraitChartRow {
        //     traitId = "trait_caffeine", name = "카페인 중독",
        //     description = "시작 시 커피 1개 보유",
        //     grade = TraitGrade.C, effectType = "startItem_coffee", effectValue = 1 },
        // ["trait_haggler"] = new TraitChartRow {
        //     traitId = "trait_haggler", name = "중고 거래",
        //     description = "아이템 5% 할인가로 구매",
        //     grade = TraitGrade.C, effectType = "itemDiscount", effectValue = 5 },
        // ["trait_beginner_luck"] = new TraitChartRow {
        //     traitId = "trait_beginner_luck", name = "초심자의 행운",
        //     description = "첫 게임 매출 +10%",
        //     grade = TraitGrade.C, effectType = "firstSaleBonus", effectValue = 10 },
        // ["trait_poor_rescue"] = new TraitChartRow {
        //     traitId = "trait_poor_rescue", name = "가난한 회사",
        //     description = "회사 돈이 처음 0원이 되면 랜덤 직원의 연봉만큼 지급",
        //     grade = TraitGrade.C, effectType = "brokeRescue", effectValue = 0 },

        // // ===== B =====
        // ["trait_gold_spoon"] = new TraitChartRow {
        //     traitId = "trait_gold_spoon", name = "금수저",
        //     description = "시작 시 6000G 추가 보유",
        //     grade = TraitGrade.B, effectType = "startGold", effectValue = 6000 },
        // ["trait_haggler_plus"] = new TraitChartRow {
        //     traitId = "trait_haggler_plus", name = "중고 거래+",
        //     description = "아이템 10% 할인가로 구매",
        //     grade = TraitGrade.B, effectType = "itemDiscount", effectValue = 10 },

        // // ===== A =====
        // ["trait_marketing_god"] = new TraitChartRow {
        //     traitId = "trait_marketing_god", name = "마케팅의 신",
        //     description = "마케팅비 0원으로 최적화 적용",
        //     grade = TraitGrade.A, effectType = "marketingFree", effectValue = 0 },
        // ["trait_recruiter"] = new TraitChartRow {
        //     traitId = "trait_recruiter", name = "호객행위",
        //     description = "채용 공고 시 지원하는 인원 +1",
        //     grade = TraitGrade.A, effectType = "recruitApplicants", effectValue = 1 },

        // // ===== S ===== (현재 비어있음)
    };

    static Dictionary<string, TraitChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, TraitChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[TraitChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[TraitChart] 차트 없음 — fallback 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess()) return result;

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId)) return result;

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            string id = S(row, "traitId");
            if (string.IsNullOrEmpty(id)) continue;

            result[id] = new TraitChartRow
            {
                traitId     = id,
                name        = S(row, "name"),
                description = S(row, "description"),
                grade       = ParseGrade(S(row, "grade")),
                effectType  = S(row, "effectType"),
                effectValue = N(row, "effectValue"),
            };
        }
        return result;
    }

    static TraitGrade ParseGrade(string s)
    {
        if (string.IsNullOrEmpty(s)) return TraitGrade.C;
        if (Enum.TryParse<TraitGrade>(s.Trim(), true, out var g)) return g;
        return TraitGrade.C;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
