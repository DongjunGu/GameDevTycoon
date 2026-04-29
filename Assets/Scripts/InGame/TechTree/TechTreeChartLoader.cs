using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "TechTree"
// 컬럼: nodeId(string), name(string), category(int = TechCategory),
//       order(int), prerequisiteId(string, 없으면 빈값), cost(int)
// 주의: "id"는 뒤끝 예약 키라 사용 불가 → nodeId로 명명

public class TechNodeChartRow
{
    public string       id;
    public string       name;
    public TechCategory category;
    public int          order;
    public string       prerequisiteId;
    public int          cost;
}

public static class TechTreeChartLoader
{
    private const string CHART_NAME = "TechTree";
    private static List<TechNodeChartRow> _cache;
    public static List<TechNodeChartRow> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[TechTreeChart] {_cache.Count}개 노드 로드 완료");
    }

    // CDN 로드 실패/차트 미업로드 시 사용 — 콘솔 업로드 전 마지막으로 코드에 박혀있던 정의와 동일
    static List<TechNodeChartRow> GetFallback() => new()
    {
        // 직원관리 - 만족도 유지
        new() { id="emp_sat_1", name="협상의 기술",     category=TechCategory.EmployeeSatisfaction, order=1, prerequisiteId="",          cost=100 },
        new() { id="emp_sat_2", name="기초 복지 인프라", category=TechCategory.EmployeeSatisfaction, order=2, prerequisiteId="emp_sat_1", cost=100 },
        new() { id="emp_sat_3", name="전문 멘탈 케어",   category=TechCategory.EmployeeSatisfaction, order=3, prerequisiteId="emp_sat_2", cost=100 },
        new() { id="emp_sat_4", name="고급 테마 라운지", category=TechCategory.EmployeeSatisfaction, order=4, prerequisiteId="emp_sat_3", cost=100 },
        new() { id="emp_sat_5", name="성과급 체계 도입", category=TechCategory.EmployeeSatisfaction, order=5, prerequisiteId="emp_sat_4", cost=100 },

        // 직원관리 - 효율성 극대화
        new() { id="emp_eff_1", name="야근 및 주말 근무",  category=TechCategory.EmployeeEfficiency, order=1, prerequisiteId="",          cost=100 },
        new() { id="emp_eff_2", name="직군 이동 최적화",   category=TechCategory.EmployeeEfficiency, order=2, prerequisiteId="emp_eff_1", cost=100 },
        new() { id="emp_eff_3", name="전략적 인재 영입",   category=TechCategory.EmployeeEfficiency, order=3, prerequisiteId="emp_eff_2", cost=100 },
        new() { id="emp_eff_4", name="한계 돌파 훈련",     category=TechCategory.EmployeeEfficiency, order=4, prerequisiteId="emp_eff_3", cost=100 },
        new() { id="emp_eff_5", name="성장 확률 증가",     category=TechCategory.EmployeeEfficiency, order=5, prerequisiteId="emp_eff_4", cost=100 },

        // 기술연구 - 장르/플랫폼
        new() { id="tech_gp_1", name="장르 마스터리 1단계", category=TechCategory.GenrePlatform, order=1, prerequisiteId="",          cost=100 },
        new() { id="tech_gp_2", name="장르 마스터리 2단계", category=TechCategory.GenrePlatform, order=2, prerequisiteId="tech_gp_1", cost=100 },
        new() { id="tech_gp_3", name="플랫폼 라이선스",     category=TechCategory.GenrePlatform, order=3, prerequisiteId="tech_gp_2", cost=100 },
        new() { id="tech_gp_4", name="플랫폼 최적화",       category=TechCategory.GenrePlatform, order=4, prerequisiteId="tech_gp_3", cost=100 },

        // 기술연구 - 참신함
        new() { id="tech_nov_1", name="참신함 연구 1단계", category=TechCategory.Novelty, order=1, prerequisiteId="",           cost=100 },
        new() { id="tech_nov_2", name="참신함 연구 2단계", category=TechCategory.Novelty, order=2, prerequisiteId="tech_nov_1", cost=100 },
        new() { id="tech_nov_3", name="이스터에그 설계",   category=TechCategory.Novelty, order=3, prerequisiteId="tech_nov_2", cost=100 },
        new() { id="tech_nov_4", name="실시간 상호작용",   category=TechCategory.Novelty, order=4, prerequisiteId="tech_nov_3", cost=100 },

        // 유틸리티
        new() { id="util_1", name="트렌드 레이더",      category=TechCategory.Utility, order=1, prerequisiteId="",        cost=100 },
        new() { id="util_2", name="장르 블렌더",        category=TechCategory.Utility, order=2, prerequisiteId="util_1",  cost=100 },
        new() { id="util_3", name="프랜차이즈 빌더",    category=TechCategory.Utility, order=3, prerequisiteId="util_2",  cost=100 },
        new() { id="util_4", name="하이퍼 프로세스",    category=TechCategory.Utility, order=4, prerequisiteId="util_3",  cost=100 },
        new() { id="util_5", name="신용 분석 알고리즘", category=TechCategory.Utility, order=5, prerequisiteId="util_4",  cost=100 },
        new() { id="util_6", name="금융권 네트워킹",    category=TechCategory.Utility, order=6, prerequisiteId="util_5",  cost=100 },

        // 창의성 미니게임 강화
        new() { id="creativity_score_1", name="창의성 점수 강화 1단계", category=TechCategory.Utility, order=7,  prerequisiteId="",                   cost=100 },
        new() { id="creativity_score_2", name="창의성 점수 강화 2단계", category=TechCategory.Utility, order=8,  prerequisiteId="creativity_score_1", cost=100 },
        new() { id="creativity_grid_1",  name="그리드 확장 1단계",      category=TechCategory.Utility, order=9,  prerequisiteId="",                   cost=100 },
        new() { id="creativity_grid_2",  name="그리드 확장 2단계",      category=TechCategory.Utility, order=10, prerequisiteId="creativity_grid_1",  cost=100 },
    };

    static List<TechNodeChartRow> LoadFromServer()
    {
        var result = new List<TechNodeChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[TechTreeChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[TechTreeChart] 차트 없음 — 기본값 사용");
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
            string id  = S(row, "nodeId");
            if (string.IsNullOrEmpty(id)) continue;

            result.Add(new TechNodeChartRow
            {
                id             = id,
                name           = S(row, "name"),
                category       = (TechCategory)N(row, "category"),
                order          = N(row, "order"),
                prerequisiteId = S(row, "prerequisiteId"),
                cost           = N(row, "cost"),
            });
        }
        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
