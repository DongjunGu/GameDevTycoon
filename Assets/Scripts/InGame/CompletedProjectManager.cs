using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class CompletedProjectManager : MonoBehaviour
{
    public static CompletedProjectManager Instance { get; private set; }

    public List<CompletedProjectData> completedProjects = new();

    // 런 식별자. ResetForNewRun 마다 ++. 비동기 Insert 콜백이 reset 이후 늦게 도착해
    // 메모리/fatigue 에 stale 데이터를 박는 race 를 차단하기 위한 epoch.
    private int _runEpoch = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 (Sales 시작 시 0으로 insert) ─────
    public void SaveCompletedProject(CompletedProjectData data)
    {
        int epochAtCall = _runEpoch;
        Backend.GameData.Insert("CompletedProjects", data.ToParam(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"완료 프로젝트 저장 실패: {bro}");
                return;
            }

            string newRowInDate = bro.GetInDate();

            // 콜백 도착 시점이 이미 새 런이면 stale — 서버 row 즉시 삭제 후 메모리/fatigue 손 안 댐
            if (epochAtCall != _runEpoch)
            {
                Debug.LogWarning($"[Stale] SaveCompletedProject 콜백 무시 (런 변경됨) - 서버 row {newRowInDate} 삭제");
                Backend.GameData.DeleteV2("CompletedProjects", newRowInDate, Backend.UserInDate, _ => { });
                return;
            }

            data.rowInDate = newRowInDate;
            completedProjects.Add(data);
            GenreFatigueManager.Instance?.UpdateFatigue((ProjectGenre)data.genre);
            SalesSaveManager.Instance?.SaveCompletedProjectRowInDate(data.rowInDate);
            Debug.Log($"완료 프로젝트 저장: {data.projectName} rowInDate={data.rowInDate}");

            // Sales가 먼저 끝났다면 (totalRevenue 이 이미 기록됨) 즉시 업데이트
            if (data.totalRevenue > 0)
                ExecuteSalesResultUpdate(data);
        });
    }

    // ── 판매 완료 시 매출 업데이트 ──────
    public void UpdateSalesResult(CompletedProjectData data, int totalRevenue)
    {
        data.totalRevenue = totalRevenue;

        // rowInDate가 아직 없으면 값만 저장 — Insert 콜백에서 처리
        if (string.IsNullOrEmpty(data.rowInDate)) return;

        ExecuteSalesResultUpdate(data);
    }

    void ExecuteSalesResultUpdate(CompletedProjectData data)
    {
        var param = new BackEnd.Param();
        param.Add("totalRevenue", data.totalRevenue);
        param.Add("bestRank",     data.bestRank);

        Backend.GameData.UpdateV2("CompletedProjects", data.rowInDate, Backend.UserInDate, param, bro =>
        {
            if (bro.IsSuccess())
                Debug.Log($"판매 결과 업데이트: {data.projectName} revenue={data.totalRevenue}");
            else
                Debug.LogError($"판매 결과 업데이트 실패: {bro}");
        });
    }

    // 뒤끝 GetMyData 는 firstKey 없이 호출하면 기본 limit(10) 행만 반환한다 — LastEvaluatedKeyString 이
    // 비어있지 않은 동안 이어서 요청해 테이블의 모든 row 를 모은다.
    // ⚠ 이 캡 때문에 완료작이 10개를 넘어가면 항상 딱 10개까지만 불러와졌었다 — DevelopmentResultUI 의
    // 기본 프로젝트명("프로젝트명{완료작수+1}")이 계속 "프로젝트명11"에 고정되던 원인. ResetForNewRun 의
    // 정리 삭제도 같은 한도로 10개까지만 지워 서버에 잔재 row 가 계속 누적됐다(다음 런의 로드도 그 잔재
    // 를 포함해 여전히 10개에서 잘림 → 실제 진행과 무관하게 영구 고착).
    const int PageLimit = 100; // GetMyData 1회 호출 최대치

    static void FetchAllRows(string tableName, System.Action<List<JsonData>> onComplete)
        => FetchPage(tableName, "", new List<JsonData>(), onComplete);

    static void FetchPage(string tableName, string firstKey, List<JsonData> acc, System.Action<List<JsonData>> onComplete)
    {
        Backend.GameData.GetMyData(tableName, new Where(), PageLimit, firstKey, bro =>
        {
            if (!bro.IsSuccess())
            {
                onComplete?.Invoke(acc);
                return;
            }

            var rows = bro.FlattenRows();
            if (rows != null)
                foreach (var row in rows) acc.Add((JsonData)row);

            string next = bro.LastEvaluatedKeyString();
            if (!string.IsNullOrEmpty(next))
                FetchPage(tableName, next, acc, onComplete); // 더 남았으면 이어서 요청
            else
                onComplete?.Invoke(acc);
        });
    }

    // 새 런 시작 — 메모리 클리어 + 서버 직접 fetch 로 모든 row 일괄 삭제
    // 메모리 List 가 아니라 서버 GetMyData 기준으로 삭제 — Sales 직후 Insert 콜백 미도착 race 로 메모리 add 가 안 된
    // 잔재 row 까지 잡아 제거. (메모리만 의존하면 inFlight Insert 가 reset 이후 늦게 도착해 서버에 외로운 row 만듦.)
    public void ResetForNewRun(System.Action onComplete = null)
    {
        _runEpoch++;
        completedProjects.Clear();

        FetchAllRows("CompletedProjects", rows =>
        {
            if (rows == null || rows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var ids = new List<string>();
            foreach (var row in rows)
            {
                string inDate = TryGetInDate(row);
                if (!string.IsNullOrEmpty(inDate)) ids.Add(inDate);
            }

            if (ids.Count == 0) { onComplete?.Invoke(); return; }

            int pending = ids.Count;
            foreach (var inDate in ids)
            {
                Backend.GameData.DeleteV2("CompletedProjects", inDate, Backend.UserInDate, broDel =>
                {
                    if (!broDel.IsSuccess()) Debug.LogError($"[Reset] CompletedProjects delete 실패: {broDel}");
                    pending--;
                    if (pending == 0) onComplete?.Invoke();
                });
            }
        });
    }

    static string TryGetInDate(JsonData row)
    {
        try { return row["inDate"].ToString(); } catch { return ""; }
    }

    // ── 로드 ──────────────────────────────────
    public void LoadCompletedProjects(System.Action onComplete = null)
    {
        FetchAllRows("CompletedProjects", rows =>
        {
            if (rows == null || rows.Count == 0)
            {
                Debug.Log("완료 프로젝트 0개");
                onComplete?.Invoke();
                return;
            }

            completedProjects.Clear();
            foreach (var row in rows)
                completedProjects.Add(CompletedProjectData.FromServerRow(row));

            completedProjects.Sort((a, b) =>
            {
                if (a.year != b.year) return a.year.CompareTo(b.year);
                if (a.month != b.month) return a.month.CompareTo(b.month);
                return a.week.CompareTo(b.week);
            });

            GenreFatigueManager.Instance?.RebuildFromHistory(completedProjects);
            onComplete?.Invoke();
        });
    }
}