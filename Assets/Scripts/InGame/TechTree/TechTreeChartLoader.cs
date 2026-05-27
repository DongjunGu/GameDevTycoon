using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "TechTree"
// 컬럼: nodeId(string), category(string 한글), name(string),
//       description(string), logic(string), requiredPoints(int)
// 카테고리는 한글 라벨로 입력하고 로더에서 enum 매핑.
// 선후 관계는 별도 컬럼 없이 동일 카테고리 내 row 순서로 자동 부여.
// 주의: "id"는 뒤끝 예약 키라 사용 불가 → nodeId로 명명.

public class TechNodeChartRow
{
    public string       id;
    public string       name;
    public TechCategory category;
    public string       description;
    public string       logic;
    public int          requiredPoints;
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

    // CDN 로드 실패/차트 미업로드 시 사용 — CSV 와 동일한 정의를 코드에도 박아둠.
    // 카테고리별 row 순서 = 해금 순서 (위 row 가 prerequisite).
    static List<TechNodeChartRow> GetFallback() => new()
    {
        // ── 돈 ──────────────────────────────────────
        new() { id="money_awaken",    category=TechCategory.Money, name="각성 상태",
                description="직원이 가끔씩 개발 도중 각성상태가 되어 게임 점수가 크게 증가합니다",
                logic="기존 틱에서 상승하던 잭팟 시스템이 추가 ( 찍기전에는 잭팟 없이 기본만 )",
                requiredPoints=5 },
        new() { id="money_comeback",  category=TechCategory.Money, name="역주행",
                description="마지막 달의 매출이 3배 증가합니다",
                logic="로직상 매출이 나오는 마지막 달의 매출 3배로 증가",
                requiredPoints=10 },
        new() { id="money_craftsman", category=TechCategory.Money, name="장인 정신",
                description="연구 완료 이후 출시한 게임이 증가할때마다 매출 0.5% 상승합니다 (최대 10%)",
                logic="연구 완료 이후 낸 게임이 1개가 증가할때마다 매출 0.5% 상승 최대 15개",
                requiredPoints=15 },
        new() { id="money_bugmaster", category=TechCategory.Money, name="버그 잡기 달인",
                description="버그 해결속도가 증가합니다",
                logic="버그 해결속도 증가",
                requiredPoints=20 },
        new() { id="money_perfect",   category=TechCategory.Money, name="만점 신화",
                description="게임 점수가 100점이 나오면 해당 게임의 매출이 15% 증가합니다",
                logic="100점이면 매출 증가 15%",
                requiredPoints=25 },
        new() { id="money_sequel",    category=TechCategory.Money, name="익숙한 맛",
                description="후속작을 출시 했을 경우 해당 게임의 매출이 20% 증가합니다",
                logic="후속작 개발시 매출 + 20%",
                requiredPoints=30 },

        // ── 채용 ────────────────────────────────────
        new() { id="hire_discount", category=TechCategory.Hiring, name="채용 비용 할인",
                description="채용 후보 목록을 뽑는 비용이 20% 감소합니다",
                logic="채용 후보 목록 뽑기(티어 진입) 비용 20% 할인",
                requiredPoints=5 },
        new() { id="hire_tier2",    category=TechCategory.Hiring, name="채용 2단계",
                description="채용 등급이 2단계로 상승하여 더 좋은 직원이 등장할 확률이 높아집니다",
                logic="채용 2단계",
                requiredPoints=10 },
        new() { id="hire_more",     category=TechCategory.Hiring, name="한 명 더!",
                description="채용 리스트에 등장하는 직원이 한 명 더 추가됩니다",
                logic="채용 인원 +1",
                requiredPoints=15 },
        new() { id="hire_refresh",  category=TechCategory.Hiring, name="한 번 더!",
                description="채용 리스트에서 원하는 사람이 없는 경우 새로고침을 1회 할 수 있게됩니다",
                logic="채용 리스트 전원 새로 고침 1회 가능 (이러면 여기서 못뽑게) 뽑으면 새로고침 버튼 사라짐",
                requiredPoints=20 },
        new() { id="hire_tier3",    category=TechCategory.Hiring, name="채용 3단계",
                description="채용 등급이 3단계로 상승하여 더 좋은 직원이 등장할 확률이 높아집니다",
                logic="채용 3단계",
                requiredPoints=25 },

        // ── 만족도 ──────────────────────────────────
        new() { id="sat_mental",     category=TechCategory.Satisfaction, name="멘탈 케어 서비스",
                description="매달 하락하는 만족도가 1 감소합니다",
                logic="기본 만족도 하락양 감소",
                requiredPoints=5 },
        new() { id="sat_loyalty",    category=TechCategory.Satisfaction, name="평생 직장",
                description="직원이 자진 퇴사할 확률이 하락합니다",
                logic="직원 자진 퇴사 확률 10% 하락 (도망포함된 전체 확률, 음수는 0% 처리)",
                requiredPoints=10 },
        new() { id="sat_welcome",    category=TechCategory.Satisfaction, name="신입 환영회",
                description="신규 직원이 입사하면 랜덤한 직원의 만족도가 20 회복됩니다",
                logic="신규 직원 입사시 랜덤 직원 만족도 + 20",
                requiredPoints=15 },
        new() { id="sat_completion", category=TechCategory.Satisfaction, name="완성의 기쁨",
                description="게임을 출시하면 모든직원의 만족도가 5 회복됩니다",
                logic="게임 완성시 만족도 전원 추가 5 상승",
                requiredPoints=20 },
        new() { id="sat_elite",      category=TechCategory.Satisfaction, name="고급 인력",
                description="11성 이상일 때 강화를 성공하면 해당 직원의 만족도가 5 회복됩니다",
                logic="11성 이상 강화 성공시 해당 직원 만족도 +5",
                requiredPoints=25 },

        // ── 창의성 ──────────────────────────────────
        new() { id="creat_value1", category=TechCategory.Creativity, name="블록 가치 상승 Ⅰ",
                description="창의성 블록이 차지한 한 칸당 점수가 10점으로 증가합니다",
                logic="칸당 점수 5점 > 10점",
                requiredPoints=5 },
        new() { id="creat_box2",   category=TechCategory.Creativity, name="창의성 2단계",
                description="창의성 블록이 들어갈 박스를 한 단계 넓게 만들어줍니다",
                logic="창의성 박스 크기를 한단계 업그레이드",
                requiredPoints=10 },
        new() { id="creat_value2", category=TechCategory.Creativity, name="블록 가치 상승 Ⅱ",
                description="창의성 블록이 차지한 한 칸당 점수가 15점으로 증가합니다",
                logic="칸당 점수 10점 > 15점",
                requiredPoints=15 },
        new() { id="creat_box3",   category=TechCategory.Creativity, name="창의성 3단계",
                description="창의성 블록이 들어갈 박스를 한 단계 넓게 만들어줍니다",
                logic="창의성 박스 크기를 한단계 업그레이드",
                requiredPoints=20 },
        new() { id="creat_value3", category=TechCategory.Creativity, name="블록 가치 상승 Ⅲ",
                description="창의성 블록이 차지한 한 칸당 점수가 20점으로 증가합니다",
                logic="칸당 점수 15점 > 20점",
                requiredPoints=25 },

        // ── 광고 ──────────────────────────────────── (row 0개, UI 탭만 유지)
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
                category       = ParseCategory(S(row, "category")),
                description    = S(row, "description"),
                logic          = S(row, "logic"),
                requiredPoints = N(row, "requiredPoints"),
            });
        }
        return result;
    }

    // 한글 라벨 → enum. 미매칭 시 Money 폴백 + 경고.
    static TechCategory ParseCategory(string label) => label switch
    {
        "돈"     => TechCategory.Money,
        "채용"   => TechCategory.Hiring,
        "만족도" => TechCategory.Satisfaction,
        "창의성" => TechCategory.Creativity,
        "광고"   => TechCategory.Ad,
        _ => LogAndFallback(label),
    };

    static TechCategory LogAndFallback(string label)
    {
        Debug.LogWarning($"[TechTreeChart] 알 수 없는 카테고리 '{label}' → Money 로 폴백");
        return TechCategory.Money;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
