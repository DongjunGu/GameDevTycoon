// =====================================================
// DialogChartLoader.cs
// 뒤끝 신규 Chart API 사용
// Backend.CDN.Content.Table.Get → Backend.CDN.Content.Get
// =====================================================

using System.Collections.Generic;
using BackEnd;
using BackEnd.Content;
using LitJson;
using UnityEngine;

public static class DialogChartLoader
{
    // ⚠️ 뒤끝 콘솔에 등록한 Chart 이름과 반드시 일치해야 함
    private const string CHART_DIALOG_NODE   = "DialogNode";
    private const string CHART_DIALOG_CHOICE = "DialogChoice";

    // ─── DialogNode ───────────────────────────────────────────────
    public static Dictionary<string, DialogNodeData> LoadNodes()
    {
        var result = new Dictionary<string, DialogNodeData>();

        JsonData rows = GetChartJson(CHART_DIALOG_NODE);
        if (rows == null) return result;

        for (int i = 0; i < rows.Count; i++)
        {
            var node = ParseNode(rows[i]);
            result[node.dialogId] = node;
        }

        return result;
    }

    // ─── DialogChoice ─────────────────────────────────────────────
    public static Dictionary<string, List<ChoiceData>> LoadChoices()
    {
        var result = new Dictionary<string, List<ChoiceData>>();

        JsonData rows = GetChartJson(CHART_DIALOG_CHOICE);
        if (rows == null) return result;

        for (int i = 0; i < rows.Count; i++)
        {
            var choice = ParseChoice(rows[i]);
            if (!result.ContainsKey(choice.parentDialogId))
                result[choice.parentDialogId] = new List<ChoiceData>();
            result[choice.parentDialogId].Add(choice);
        }

        return result;
    }

    // ─── 핵심: chartName → contentJson 변환 ───────────────────────
    // Table.Get()으로 목록 조회 → Content.Get()으로 내용 일괄 로드
    // → chartName으로 필터해서 해당 차트의 rows 반환
    private static JsonData GetChartJson(string chartName)
    {
        // 1) 차트 테이블 목록 조회
        BackendContentTableReturnObject tableResult = Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogError($"[DialogChartLoader] Chart 테이블 조회 실패: {tableResult}");
            return null;
        }

        List<ContentTableItem> tableList = tableResult.GetContentTableItemList();

        // 2) chartName으로 chartId 탐색
        string targetChartId = null;
        foreach (ContentTableItem item in tableList)
        {
            if (item.chartName == chartName)
            {
                targetChartId = item.chartId;
                break;
            }
        }

        if (targetChartId == null)
        {
            Debug.LogError($"[DialogChartLoader] Chart를 찾을 수 없음: {chartName}");
            return null;
        }

        // 3) 차트 내용 일괄 로드
        BackendContentReturnObject contentResult = Backend.CDN.Content.Get(tableList);
        if (!contentResult.IsSuccess())
        {
            Debug.LogError($"[DialogChartLoader] Chart 내용 로드 실패 ({chartName}): {contentResult}");
            return null;
        }

        // 4) chartId로 해당 차트 꺼내기
        Dictionary<string, ContentItem> dic = contentResult.GetContentDictionarySortByChartId();
        if (!dic.ContainsKey(targetChartId))
        {
            Debug.LogError($"[DialogChartLoader] ContentDic에 chartId 없음: {targetChartId}");
            return null;
        }

        // contentJson은 이미 JsonData (배열 형태)
        // 신규 API는 S/N/BOOL 래퍼 없이 값이 바로 들어있음
        return dic[targetChartId].contentJson;
    }

    // ─── 파서 ────────────────────────────────────────────────────
    private static DialogNodeData ParseNode(JsonData row)
    {
        return new DialogNodeData
        {
            dialogId          = S(row, "dialogId"),
            dialogGroupId     = S(row, "dialogGroupId"),
            order             = N(row, "order"),
            speakerType       = Enum<SpeakerType>(row, "speakerType"),
            speakerId         = S(row, "speakerId"),
            speakerName       = S(row, "speakerName"),
            speakerPortraitId = S(row, "speakerPortraitId"),
            dialogText        = S(row, "dialogText"),
            textSpeed         = Enum<TextSpeed>(row, "textSpeed"),
            hasChoice         = B(row, "hasChoice"),
            nextDialogId      = S(row, "nextDialogId"),
            portraitEmotion   = S(row, "portraitEmotion"),
            shakeEffect       = B(row, "shakeEffect"),
            bgmChangeId       = S(row, "bgmChangeId"),
            sfxId             = S(row, "sfxId"),
            autoAdvance       = B(row, "autoAdvance"),
            autoDelay         = F(row, "autoDelay"),
            skippable         = B(row, "skippable"),
        };
    }

    private static ChoiceData ParseChoice(JsonData row)
    {
        return new ChoiceData
        {
            choiceId       = S(row, "choiceId"),
            parentDialogId = S(row, "parentDialogId"),
            choiceText     = S(row, "choiceText"),
            nextDialogId   = S(row, "nextDialogId"),
            conditionType  = Enum<ConditionType>(row, "conditionType"),
            conditionValue = N(row, "conditionValue"),
            resultType     = Enum<ResultType>(row, "resultType"),
            resultValue    = N(row, "resultValue"),
        };
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────
    // ✅ 신규 API: S/N/BOOL 래퍼 없이 값이 바로 들어있음
    private static string S(JsonData r, string k) => r[k].ToString();
    private static int    N(JsonData r, string k) => int.Parse(r[k].ToString());
    private static float  F(JsonData r, string k) => float.Parse(r[k].ToString());
    private static bool   B(JsonData r, string k) => r[k].ToString().ToLower() == "true";
    private static T Enum<T>(JsonData r, string k) where T : struct
        => (T)System.Enum.Parse(typeof(T), r[k].ToString());
}