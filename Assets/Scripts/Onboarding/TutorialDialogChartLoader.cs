using System;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using UnityEngine;

// 뒤끝 콘솔 차트 이름: "TutorialDialog" (DialogNode/DialogChartLoader와 별개 — 튜토리얼 전용)
// 컬럼: dialogId(string, 고유), stepGroup(string, "1-1"/"1-2" 등), order(int),
//       speakerName(string, "비서"/"나"), portraitId(string, Resources/Portraits/ 파일명), text(string)
// text 안의 [[강조할 텍스트]] 는 TutorialPanelUI 가 색상 강조로 치환.

public class TutorialDialogLine
{
    public string dialogId;
    public string stepGroup;
    public int    order;
    public string speakerName;
    public string portraitId;
    public string text;
}

public static class TutorialDialogChartLoader
{
    private const string CHART_NAME = "TutorialDialog";
    private static Dictionary<string, List<TutorialDialogLine>> _cache;
    public static Dictionary<string, List<TutorialDialogLine>> Cache => _cache ?? GetFallback();

    public static void Load()
    {
        _cache = LoadFromServer();
        if (_cache == null || _cache.Count == 0)
            _cache = GetFallback();
        Debug.Log($"[TutorialDialogChart] {_cache.Count}개 stepGroup 로드 완료");
    }

    // 차트 미업로드 시 fallback — 1-1/1-2 기본 대사 (튜토리얼 차트 업로드 전 테스트용)
    static Dictionary<string, List<TutorialDialogLine>> GetFallback() => new()
    {
        ["1-1"] = new List<TutorialDialogLine>
        {
            new() { dialogId = "tut_1_1_0", stepGroup = "1-1", order = 1, speakerName = "비서", portraitId = "portrait_secretary", text = "현재 우리 회사의 직원은… [[대표님 한 명]]이네요." },
            new() { dialogId = "tut_1_1_1", stepGroup = "1-1", order = 2, speakerName = "나",   portraitId = "ceo_001",           text = "너는 누구지 그럼?" },
            new() { dialogId = "tut_1_1_2", stepGroup = "1-1", order = 3, speakerName = "비서", portraitId = "portrait_secretary", text = "저요? 저는 직원이 아니라 대표님의 [[만능 비서]]잖아요! 아직 잠이 덜깨신건가?" },
        },
        ["1-2"] = new List<TutorialDialogLine>
        {
            new() { dialogId = "tut_1_2_0", stepGroup = "1-2", order = 1, speakerName = "비서", portraitId = "portrait_secretary", text = "[채용]을 눌러 첫 직원을 뽑아볼까요?" },
        },
    };

    static Dictionary<string, List<TutorialDialogLine>> LoadFromServer()
    {
        var result = new Dictionary<string, List<TutorialDialogLine>>();

        var tableResult = BackEnd.Backend.CDN.Content.Table.Get();
        if (!tableResult.IsSuccess())
        {
            Debug.LogWarning($"[TutorialDialogChart] 테이블 조회 실패: {tableResult}");
            return result;
        }

        var tableList = tableResult.GetContentTableItemList();
        string chartId = null;
        foreach (var item in tableList)
            if (item.chartName == CHART_NAME) { chartId = item.chartId; break; }

        if (chartId == null)
        {
            Debug.LogWarning("[TutorialDialogChart] 차트 없음 — fallback 사용");
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
            string group = S(row, "stepGroup");
            if (string.IsNullOrEmpty(group)) continue;

            if (!result.ContainsKey(group)) result[group] = new List<TutorialDialogLine>();
            result[group].Add(new TutorialDialogLine
            {
                dialogId    = S(row, "dialogId"),
                stepGroup   = group,
                order       = N(row, "order"),
                speakerName = S(row, "speakerName"),
                portraitId  = S(row, "portraitId"),
                text        = S(row, "text"),
            });
        }

        foreach (var group in result.Keys.ToList())
            result[group] = result[group].OrderBy(l => l.order).ToList();

        return result;
    }

    static string S(JsonData r, string k) { try { return r[k].ToString(); } catch { return ""; } }
    static int    N(JsonData r, string k) { try { return int.Parse(r[k].ToString()); } catch { return 0; } }
}
