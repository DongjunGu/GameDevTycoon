using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

public class TechTreeManager : MonoBehaviour
{
    public static TechTreeManager Instance { get; private set; }

    public List<TechNodeData> allNodes = new();

    // 테크 포인트는 MoneyManager.Point 에서 관리 — 여기서는 forwarding 만.
    // OnPointsChanged 이벤트는 MoneyManager.OnPointChanged 를 그대로 전달.
    public int CurrentPoints => MoneyManager.Instance != null ? MoneyManager.Instance.Point : 0;

    // 장인 정신(money_craftsman) — 해금 후 출시한 게임 누적 카운트. 20 cap, 1 당 매출 +0.5%.
    public int CraftsmanGameCount { get; private set; }
    public const int CraftsmanMaxCount = 20;
    public const float CraftsmanBonusPerCount = 0.005f; // 0.5%

    public event System.Action OnPointsChanged;
    public event System.Action OnUnlockChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildNodesFromChart();
    }

    void Start()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnPointChanged += ForwardPointChanged;
    }

    void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnPointChanged -= ForwardPointChanged;
    }

    void ForwardPointChanged() => OnPointsChanged?.Invoke();

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
                description    = row.description,
                logic          = row.logic,
                requiredPoints = row.requiredPoints,
                isUnlocked     = prevUnlocked.Contains(row.id),
            });
        }
    }

    // 카테고리 내 row 순서로 직전 노드를 자동 prerequisite 으로 사용.
    TechNodeData FindPrerequisite(TechNodeData node)
    {
        TechNodeData prev = null;
        foreach (var n in allNodes)
        {
            if (n == node) return prev;
            if (n.category == node.category) prev = n;
        }
        return null;
    }

    public bool CanUnlock(TechNodeData node)
    {
        if (node == null || node.isUnlocked) return false;
        if (MoneyManager.Instance == null || !MoneyManager.Instance.CanAffordPoint(node.requiredPoints)) return false;

        var prereq = FindPrerequisite(node);
        return prereq == null || prereq.isUnlocked;
    }

    public void Unlock(TechNodeData node)
    {
        if (!CanUnlock(node)) return;

        if (!MoneyManager.Instance.SpendPoint(node.requiredPoints)) return; // SaveMoney 자동
        node.isUnlocked = true;
        OnUnlockChanged?.Invoke();
        // 4-set 저장 — UserTechTree + (SpendPoint 가 호출한) UserMoney + UserGameTime + UserProject
        SaveTechTree();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        Debug.Log($"[TechTree] 해금: {node.name} (-{node.requiredPoints}P, 잔여 {CurrentPoints}P)");
    }

    // 디버그/획득 — MoneyManager 로 forward
    public void AddPoints(int delta)
    {
        if (delta == 0 || MoneyManager.Instance == null) return;
        if (delta > 0) MoneyManager.Instance.AddPoint(delta);
        else           MoneyManager.Instance.SpendPoint(-delta);
    }

    // 장인 정신 — 출시 1회마다 +1 (cap 20). 매출 보너스 (CraftsmanBonusMultiplier) 곱.
    // SalesUI.ShowInternal 신규 분기 진입 시 호출. 복원 분기는 호출하지 않음.
    public void IncrementCraftsmanCount()
    {
        if (!IsUnlocked("money_craftsman")) return;
        if (CraftsmanGameCount >= CraftsmanMaxCount) return;
        CraftsmanGameCount++;
        SaveTechTree();
        Debug.Log($"[TechTree] 장인 정신 카운트 +1 → {CraftsmanGameCount}/{CraftsmanMaxCount}");
    }

    // 현재 매출 보너스 배율 (1.0 ~ 1.10). 미해금 시 1.0.
    public float CraftsmanBonusMultiplier()
    {
        if (!IsUnlocked("money_craftsman")) return 1f;
        int count = Mathf.Min(CraftsmanMaxCount, CraftsmanGameCount);
        return 1f + count * CraftsmanBonusPerCount;
    }

    // ── 저장 (unlockedIds 만 — 포인트는 UserMoney.point 에서) ───────
    private string _rowInDate = null;

    void SaveTechTree(System.Action onComplete = null)
    {
        var unlockedIds = new System.Text.StringBuilder();
        foreach (var node in allNodes)
            if (node.isUnlocked)
                unlockedIds.Append(node.id + ",");

        var param = new Param();
        param.Add("unlockedIds",    unlockedIds.ToString().TrimEnd(','));
        param.Add("craftsmanCount", CraftsmanGameCount);

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

    public void LoadTechTree(System.Action onComplete = null)
    {
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

            string unlockedIds = SafeString(row, "unlockedIds", "");
            var ids = new HashSet<string>(unlockedIds.Split(','));
            foreach (var node in allNodes)
                node.isUnlocked = ids.Contains(node.id);

            CraftsmanGameCount = SafeInt(row, "craftsmanCount", 0);

            OnUnlockChanged?.Invoke();
            Debug.Log($"[TechTree] 로드 완료 (craftsmanCount={CraftsmanGameCount})");
            onComplete?.Invoke();
        });
    }

    public bool IsUnlocked(string id)
    {
        var node = allNodes.Find(n => n.id == id);
        return node != null && node.isUnlocked;
    }

    // 새 런 시작 — 노드 unlock 전부 false + 카운터 0 후 row 덮어쓰기. 포인트는 MoneyManager.ResetForNewRun 에서 처리.
    public void ResetForNewRun(System.Action onComplete = null)
    {
        foreach (var node in allNodes) node.isUnlocked = false;
        CraftsmanGameCount = 0;
        OnUnlockChanged?.Invoke();
        SaveTechTree(onComplete);
    }

    static int    SafeInt   (JsonData row, string key, int    defaultValue) { try { return int.Parse(row[key].ToString()); }  catch { return defaultValue; } }
    static string SafeString(JsonData row, string key, string defaultValue) { try { return row[key].ToString(); } catch { return defaultValue; } }
}
