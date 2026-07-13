using System.Collections.Generic;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔에 등록할 차트 이름: "RandomEventChoice"
// 컬럼 목록 (기존):
//   eventType              (string)
//   title                  (string)
//   description            (string)  — 대사1, box1에서 타이핑
//   description2           (string)  — 대사1의 role-alt(예: 아트직군 전용)
//   portraitId             (string)
//   choice1_label          (string)
//   choice1_resultTitle    (string)  — 비어있으면 원래 title 유지
//   choice1_resultDescription (string)
//   choice1_systemMessage  (string)
//   choice2_label ~ choice3_label (동일 구조 반복)
//
// 컬럼 목록 (신규 — 답변/질문/결과팝업 시스템):
//   dialogue2              (string)  — 대사2, 있으면 대사1 다음 box2로 화자 전환 후 타이핑
//   question               (string)  — 질문, 대사1(+대사2) 완료 후 QuestionText에 즉시 표시
//   choice1_reply1         (string)  — 선택 후 나오는 대사(1차)
//   choice1_reply2         (string)  — 선택 후 나오는 대사(2차, 있으면 이어서)
//   choice1_popupType      (int)     — 0=미지정(기존 resultSystemMessage→AlertUI1) / 1=AlertUI4 / 2=AlertUI5 / 3=AlertUI6
//   choice1_ment1~3        (string)  — 결과팝업멘트1~3 (타입별 매핑은 RandomEventChoiceUI.ShowResultPopup 참고)
//   choice1_popupType2/choice1_ment1_2~3_2 (선택) — 첫 팝업 확인 후 이어서 하나 더 띄울 2번째 팝업 (예: 승자효과→패자효과)
//   choice2_*/choice3_* 동일 구조 반복

public class RandomEventChoiceChartRow
{
    public string title;
    public string description;
    public string description2; // 역할/상태 분기용 2번째 설명 (e.g. Artist 전용) — 대사1의 role-alt, dialogue2와 별개
    public string dialogue2;    // 대사2 (신규) — 두 번째 화자의 도입 대사
    public string question;     // 질문 (신규) — QuestionText 출력용
    public string portraitId;
    public string portraitId2;
    public float  weight;
    public int    categoryMin;
    public int    categoryMax;
    public bool   requiresPatrol;
    public string requiredPatrolPointId;
    public List<RandomEventChoiceOptionRow> choices = new List<RandomEventChoiceOptionRow>();
}

public class RandomEventChoiceOptionRow
{
    public string label;
    public string resultTitle;
    public string resultDescription;
    public string resultDescription2;
    public string resultDescription3;
    public string resultSystemMessage;
    public string resultSystemMessage2;
    public string resultSystemMessage3;

    // ── 신규 (답변1/답변2 + 결과 팝업) ──────────────────────────
    public string reply1;
    public string reply2;
    public int    popupType; // 0=미지정(기존 방식) / 1=AlertUI4 / 2=AlertUI5 / 3=AlertUI6
    public string ment1;
    public string ment2;
    public string ment3;

    // 2번째 결과 팝업(선택) — 첫 팝업 확인 후 바로 이어서 하나 더
    public int    popupType2;
    public string ment1_2;
    public string ment2_2;
    public string ment3_2;
}

public static class RandomEventChoiceChartLoader
{
    private const string CHART_NAME  = "RandomEventChoice";
    private const int    MAX_CHOICES = 3;

    private static Dictionary<string, RandomEventChoiceChartRow> _cache = null;
    public  static Dictionary<string, RandomEventChoiceChartRow> Cache => _cache;

    public static void Load()
    {
        _cache = LoadFromServer();
        Debug.Log($"[RandomEventChoiceChart] 로드 결과: {_cache.Count}개 / 키: {string.Join(", ", _cache.Keys)}");
    }

    // RandomEventChoiceData에 차트 값 적용
    // onSetup 호출 전에 사용해 템플릿 값 복원
    public static void Apply(RandomEventChoiceData data, string eventTypeKey,
                             Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        if (chart == null || !chart.TryGetValue(eventTypeKey, out var row)) return;

        if (!string.IsNullOrEmpty(row.title))       data.title       = row.title;
        if (!string.IsNullOrEmpty(row.description)) data.description = row.description;
        if (!string.IsNullOrEmpty(row.dialogue2))   data.dialogue2   = row.dialogue2;
        if (!string.IsNullOrEmpty(row.question))    data.question    = row.question;
        if (!string.IsNullOrEmpty(row.portraitId))  data.portraitId  = row.portraitId;
        if (!string.IsNullOrEmpty(row.portraitId2)) data.portraitId2 = row.portraitId2;
        if (row.weight > 0)                         data.weight      = row.weight;
        if (row.categoryMin > 0)                    data.categoryMin = row.categoryMin;
        if (row.categoryMax > 0)                    data.categoryMax = row.categoryMax;
        data.requiresPatrol        = row.requiresPatrol;
        data.requiredPatrolPointId = row.requiredPatrolPointId;

        // 선택지 텍스트 복원 (choices 리스트 크기가 일치한다고 가정)
        for (int i = 0; i < row.choices.Count && i < data.choices.Count; i++)
        {
            var src = row.choices[i];
            var dst = data.choices[i];

            if (!string.IsNullOrEmpty(src.label))               dst.buttonLabel         = src.label;
            if (!string.IsNullOrEmpty(src.resultTitle))         dst.resultTitle         = src.resultTitle;
            if (!string.IsNullOrEmpty(src.resultSystemMessage)) dst.resultSystemMessage = src.resultSystemMessage;

            // resultDescriptions 리스트 구성
            dst.resultDescriptions.Clear();
            if (!string.IsNullOrEmpty(src.resultDescription))  dst.resultDescriptions.Add(src.resultDescription);
            if (!string.IsNullOrEmpty(src.resultDescription2)) dst.resultDescriptions.Add(src.resultDescription2);
            if (!string.IsNullOrEmpty(src.resultDescription3)) dst.resultDescriptions.Add(src.resultDescription3);
            // 단일 항목이면 resultDescription도 유지 (기존 코드 호환)
            if (dst.resultDescriptions.Count == 1) dst.resultDescription = dst.resultDescriptions[0];

            // ── 신규 (답변1/답변2 + 결과 팝업) ──────────────────
            if (!string.IsNullOrEmpty(src.reply1)) dst.reply1 = src.reply1;
            if (!string.IsNullOrEmpty(src.reply2)) dst.reply2 = src.reply2;
            if (src.popupType > 0)                 dst.resultPopupType = src.popupType;
            if (!string.IsNullOrEmpty(src.ment1))  dst.resultMent1 = src.ment1;
            if (!string.IsNullOrEmpty(src.ment2))  dst.resultMent2 = src.ment2;
            if (!string.IsNullOrEmpty(src.ment3))  dst.resultMent3 = src.ment3;

            if (src.popupType2 > 0)                 dst.resultPopupType2 = src.popupType2;
            if (!string.IsNullOrEmpty(src.ment1_2))  dst.resultMent1_2 = src.ment1_2;
            if (!string.IsNullOrEmpty(src.ment2_2))  dst.resultMent2_2 = src.ment2_2;
            if (!string.IsNullOrEmpty(src.ment3_2))  dst.resultMent3_2 = src.ment3_2;
        }
    }

    // ── 내부 로드 ──────────────────────────────────────────────────
    static Dictionary<string, RandomEventChoiceChartRow> LoadFromServer()
    {
        var result = new Dictionary<string, RandomEventChoiceChartRow>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventChoiceChart] 테이블 조회 실패 — 코드 기본값 사용: {tableResult}");
            return result;
        }

        var tableList  = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
        {
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }
        }

        if (chartId == null)
        {
            Debug.LogWarning($"[RandomEventChoiceChart] '{CHART_NAME}' 차트 없음 — 코드 기본값 사용");
            return result;
        }

        var contentResult = BackEnd.Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess())
        {
            Debug.LogWarning($"[RandomEventChoiceChart] 내용 로드 실패 — 코드 기본값 사용: {contentResult}");
            return result;
        }

        var dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(chartId))
        {
            Debug.LogWarning($"[RandomEventChoiceChart] chartId 없음: {chartId}");
            return result;
        }

        JsonData rows = dic[chartId].contentJson;
        for (int i = 0; i < rows.Count; i++)
        {
            var    row  = rows[i];
            string type = S(row, "eventType");
            if (string.IsNullOrEmpty(type)) continue;

            var chartRow = new RandomEventChoiceChartRow
            {
                title                 = S(row, "title"),
                description           = S(row, "description"),
                description2          = S(row, "description2"),
                dialogue2             = S(row, "dialogue2"),
                question              = S(row, "question"),
                portraitId            = S(row, "portraitId"),
                portraitId2           = S(row, "portraitId2"),
                weight                = F(row, "weight"),
                categoryMin           = N(row, "categoryMin"),
                categoryMax           = N(row, "categoryMax"),
                requiresPatrol        = B(row, "requiresPatrol"),
                requiredPatrolPointId = S(row, "requiredPatrolPointId"),
            };

            for (int c = 1; c <= MAX_CHOICES; c++)
            {
                string label = S(row, $"choice{c}_label");
                if (string.IsNullOrEmpty(label)) break;

                chartRow.choices.Add(new RandomEventChoiceOptionRow
                {
                    label                = label,
                    resultTitle          = S(row, $"choice{c}_resultTitle"),
                    resultDescription    = S(row, $"choice{c}_resultDescription"),
                    resultDescription2   = S(row, $"choice{c}_resultDescription2"),
                    resultDescription3   = S(row, $"choice{c}_resultDescription3"),
                    resultSystemMessage  = S(row, $"choice{c}_systemMessage"),
                    resultSystemMessage2 = S(row, $"choice{c}_systemMessage2"),
                    resultSystemMessage3 = S(row, $"choice{c}_systemMessage3"),
                    reply1               = S(row, $"choice{c}_reply1"),
                    reply2               = S(row, $"choice{c}_reply2"),
                    popupType            = N(row, $"choice{c}_popupType"),
                    ment1                = S(row, $"choice{c}_ment1"),
                    ment2                = S(row, $"choice{c}_ment2"),
                    ment3                = S(row, $"choice{c}_ment3"),
                    popupType2           = N(row, $"choice{c}_popupType2"),
                    ment1_2              = S(row, $"choice{c}_ment1_2"),
                    ment2_2              = S(row, $"choice{c}_ment2_2"),
                    ment3_2              = S(row, $"choice{c}_ment3_2"),
                });
            }

            result[type] = chartRow;
        }

        Debug.Log($"[RandomEventChoiceChart] {result.Count}개 이벤트 로드 완료");
        return result;
    }

    // ── 헬퍼 ───────────────────────────────────────────────────────
    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
    static float  F(JsonData r, string k) { try { return float.Parse(r[k].ToString(),
                                                   System.Globalization.CultureInfo.InvariantCulture); }
                                             catch { return 0f; } }
    static bool   B(JsonData r, string k) { try { return r[k].ToString().ToLower() == "true"; } catch { return false; } }
}
