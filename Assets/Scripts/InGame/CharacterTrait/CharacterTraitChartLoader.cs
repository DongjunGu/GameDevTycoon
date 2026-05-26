using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 캐릭터별 등급 특성 차트 (CEO 특성 Trait 차트와 별개 — 이건 인게임 직원 귀속)
// 뒤끝 콘솔 차트 이름: "CharacterTrait"
// 컬럼: traitId(string), name(string), description(string), effectType(string), effectValue(int)
// 주의: "id"는 뒤끝 예약 키 → traitId 사용 (feedback_backend_reserved_keys.md)
// 발동 기준은 EmployeeData.grade >= Epic. 적용 로직은 CharacterTraitApplier (effectType 미정 = TODO).

public class CharacterTraitRow
{
    public string traitId;
    public string name;
    public string description;
    public string effectType;
    public int    effectValue;
}

public static class CharacterTraitChartLoader
{
    private const string CHART_NAME = "CharacterTrait";
    private static Dictionary<string, CharacterTraitRow> _cache;
    public static Dictionary<string, CharacterTraitRow> Cache => _cache; // 테스트: fallback 비활성 (원복: => _cache ?? GetFallback();)

    public static void Load()
    {
        _cache = LoadFromServer();
        // 테스트: fallback 비활성 — 뒤끝 "CharacterTrait" 차트 적용 여부 확인용. (원복 시 아래 2줄 주석 해제)
        // if (_cache == null || _cache.Count == 0)
        //     _cache = GetFallback();
        Debug.Log($"[CharacterTraitChart] {(_cache?.Count ?? 0)}개 캐릭터 특성 로드 (fallback 비활성 — 0개면 뒤끝 차트 못 찾음)");
    }

    // 차트 미업로드 시 fallback — 6 캐릭터 특성. 효과 로직은 CharacterTraitApplier 에서 traitId 별 분기(현재 TODO).
    static Dictionary<string, CharacterTraitRow> GetFallback() => new()
    {
        ["ctrait_kim"]       = new CharacterTraitRow { traitId = "ctrait_kim",       name = "유리멘탈",
            description = "만족도가 높을 때 스탯이 크게 상승하고 낮을 때 크게 하락합니다", effectType = "", effectValue = 0 },
        ["ctrait_otaku"]     = new CharacterTraitRow { traitId = "ctrait_otaku",     name = "오타쿠",
            description = "특정 장르를 만들 때 능력치가 20% 상승하고 해당 게임 매출이 20% 증가합니다", effectType = "", effectValue = 0 },
        ["ctrait_goldspoon"] = new CharacterTraitRow { traitId = "ctrait_goldspoon", name = "금수저",
            description = "연봉을 다른 직원의 50%만 받고 연봉 협상을 요구하지 않습니다", effectType = "", effectValue = 0 },
        ["ctrait_ugi"]       = new CharacterTraitRow { traitId = "ctrait_ugi",       name = "우주의 기운",
            description = "능력치가 매주 변동합니다", effectType = "", effectValue = 0 },
        ["ctrait_genius"]    = new CharacterTraitRow { traitId = "ctrait_genius",    name = "게으른 천재",
            description = "프로젝트가 2주 지연되는 대신 개발 팀장 점수가 30% 증가합니다", effectType = "", effectValue = 0 },
        ["ctrait_hunsu"]     = new CharacterTraitRow { traitId = "ctrait_hunsu",     name = "훈수쟁이",
            description = "개발 도중 다른 파트의 수치가 일부 증가합니다", effectType = "", effectValue = 0 },
    };

    static Dictionary<string, CharacterTraitRow> LoadFromServer()
    {
        var result = new Dictionary<string, CharacterTraitRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[CharacterTraitChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[CharacterTraitChart] 차트 없음 — fallback 사용");
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

            result[id] = new CharacterTraitRow
            {
                traitId     = id,
                name        = S(row, "name"),
                description = S(row, "description"),
                effectType  = S(row, "effectType"),
                effectValue = N(row, "effectValue"),
            };
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
