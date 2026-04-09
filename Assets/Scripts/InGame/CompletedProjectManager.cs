using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class CompletedProjectManager : MonoBehaviour
{
    public static CompletedProjectManager Instance { get; private set; }

    public List<CompletedProjectData> completedProjects = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 (Sales 시작 시 0으로 insert) ─────
    public void SaveCompletedProject(CompletedProjectData data)
    {
        Backend.GameData.Insert("CompletedProjects", data.ToParam(), bro =>
        {
            if (bro.IsSuccess())
            {
                data.rowInDate = bro.GetInDate();
                completedProjects.Add(data);
                GenreFatigueManager.Instance?.UpdateFatigue((ProjectGenre)data.genre);
                SalesSaveManager.Instance?.SaveCompletedProjectRowInDate(data.rowInDate);
                Debug.Log($"완료 프로젝트 저장: {data.projectName} rowInDate={data.rowInDate}");

                // Sales가 먼저 끝났다면 (totalUnits이 이미 기록됨) 즉시 업데이트
                if (data.totalUnits > 0)
                    ExecuteSalesResultUpdate(data);
            }
            else
            {
                Debug.LogError($"완료 프로젝트 저장 실패: {bro}");
            }
        });
    }

    // ── 판매 완료 시 매출/판매량 업데이트 ──────
    public void UpdateSalesResult(CompletedProjectData data, int totalUnits, int totalRevenue)
    {
        data.totalUnits   = totalUnits;
        data.totalRevenue = totalRevenue;

        // rowInDate가 아직 없으면 값만 저장 — Insert 콜백에서 처리
        if (string.IsNullOrEmpty(data.rowInDate)) return;

        ExecuteSalesResultUpdate(data);
    }

    void ExecuteSalesResultUpdate(CompletedProjectData data)
    {
        var param = new BackEnd.Param();
        param.Add("totalUnits",   data.totalUnits);
        param.Add("totalRevenue", data.totalRevenue);

        Backend.GameData.UpdateV2("CompletedProjects", data.rowInDate, Backend.UserInDate, param, bro =>
        {
            if (bro.IsSuccess())
                Debug.Log($"판매 결과 업데이트: {data.projectName} units={data.totalUnits}");
            else
                Debug.LogError($"판매 결과 업데이트 실패: {bro}");
        });
    }

    // ── 로드 ──────────────────────────────────
public void LoadCompletedProjects(System.Action onComplete = null)
{
    Backend.GameData.GetMyData("CompletedProjects", new Where(), bro =>
    {
        if (!bro.IsSuccess())
        {
            Debug.Log($"완료 프로젝트 없음 (정상): {bro}");
            onComplete?.Invoke();
            return;
        }

        var rows = bro.FlattenRows();
        if (rows == null || rows.Count == 0)
        {
            Debug.Log("완료 프로젝트 0개");
            onComplete?.Invoke();
            return;
        }

        completedProjects.Clear();
        foreach (var row in rows)
            completedProjects.Add(CompletedProjectData.FromServerRow((LitJson.JsonData)row));

        completedProjects.Sort((a, b) =>
        {
            if (a.year != b.year) return a.year.CompareTo(b.year);
            if (a.month != b.month) return a.month.CompareTo(b.month);
            return a.week.CompareTo(b.week);
        });

        foreach (var p in completedProjects)
            Debug.Log($"[정렬확인] {p.year}년 {p.month}월 {p.week}주 / 장르:{p.genre} ({(ProjectGenre)p.genre})");
        GenreFatigueManager.Instance?.RebuildFromHistory(completedProjects);
        Debug.Log($"완료 프로젝트 {completedProjects.Count}개 로드");
        onComplete?.Invoke();
    });
}
}