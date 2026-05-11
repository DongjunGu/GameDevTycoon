using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class TechTreeManager : MonoBehaviour
{
    public static TechTreeManager Instance { get; private set; }

    public List<TechNodeData> allNodes = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildNodesFromChart();
    }

    // 차트 캐시(`TechTreeChartLoader.Cache`)에서 노드 정의를 다시 만든다.
    // - Awake 시점: 아직 차트 로드 전이라 fallback(코드 내 기본값) 사용 가능
    // - LoadTechTree 진입 직전: BackendManager가 차트 로드 완료한 뒤이므로 서버 정의 반영
    public void BuildNodesFromChart()
    {
        var prevUnlocked = new HashSet<string>();
        foreach (var n in allNodes)
            if (n.isUnlocked) prevUnlocked.Add(n.id);

        allNodes = new List<TechNodeData>();
        foreach (var row in TechTreeChartLoader.Cache)
        {
            allNodes.Add(new TechNodeData
            {
                id             = row.id,
                name           = row.name,
                category       = row.category,
                order          = row.order,
                prerequisiteId = row.prerequisiteId,
                cost           = row.cost,
                isUnlocked     = prevUnlocked.Contains(row.id),
            });
        }
    }

    // ── 해금 가능 여부 ────────────────────────
    public bool CanUnlock(TechNodeData node)
    {
        if (node.isUnlocked) return false;
        if (!MoneyManager.Instance.CanAfford(node.cost)) return false;

        if (string.IsNullOrEmpty(node.prerequisiteId)) return true;

        var prereq = allNodes.Find(n => n.id == node.prerequisiteId);
        return prereq != null && prereq.isUnlocked;
    }

    // ── 해금 ─────────────────────────────────
    public void Unlock(TechNodeData node)
    {
        if (!CanUnlock(node)) return;

        MoneyManager.Instance.SpendGold(node.cost);
        node.isUnlocked = true;
        SaveTechTree();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        Debug.Log($"테크 해금: {node.name}");
    }

    // ── 저장 ─────────────────────────────────
    private string _rowInDate = null;

    void SaveTechTree(System.Action onComplete = null)
    {
        var unlockedIds = new System.Text.StringBuilder();
        foreach (var node in allNodes)
            if (node.isUnlocked)
                unlockedIds.Append(node.id + ",");

        var param = new Param();
        param.Add("unlockedIds", unlockedIds.ToString().TrimEnd(','));

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserTechTree", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess())
                    Debug.LogError($"테크트리 저장 실패: {bro}");
                onComplete?.Invoke();
            });
        }
        else
        {
            Backend.GameData.Insert("UserTechTree", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("테크트리 Insert 완료");
                }
                else
                {
                    Debug.LogError($"테크트리 Insert 실패: {bro}");
                }
                onComplete?.Invoke();
            });
        }
    }

    // ── 로드 ─────────────────────────────────
    public void LoadTechTree(System.Action onComplete = null)
    {
        // 서버 차트가 이번 로그인 시점에 갱신됐을 수 있으니 노드 정의 재빌드 후 unlock 적용
        BuildNodesFromChart();

        Backend.GameData.GetMyData("UserTechTree", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.Log("테크트리 데이터 없음");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            JsonData row = (JsonData)rows[rows.Count - 1];
            _rowInDate = row["inDate"]?.ToString();

            string unlockedIds = row["unlockedIds"]?.ToString() ?? "";
            var ids = new HashSet<string>(unlockedIds.Split(','));

            foreach (var node in allNodes)
                node.isUnlocked = ids.Contains(node.id);

            Debug.Log($"테크트리 로드 완료");
            onComplete?.Invoke();
        });
    }

    public bool IsUnlocked(string id)
    {
        var node = allNodes.Find(n => n.id == id);
        return node != null && node.isUnlocked;
    }

    // 새 런 시작 — 모든 노드 unlocked=false 로 되돌리고 row 덮어쓰기
    public void ResetForNewRun(System.Action onComplete = null)
    {
        foreach (var node in allNodes) node.isUnlocked = false;
        SaveTechTree(onComplete);
    }
}