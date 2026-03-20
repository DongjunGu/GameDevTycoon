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
        InitNodes();
    }

    void InitNodes()
    {
        allNodes = new List<TechNodeData>
        {
            // ── 직원관리 - 만족도 유지 ──────────────
            new TechNodeData { id="emp_sat_1", name="협상의 기술",       category=TechCategory.EmployeeSatisfaction, order=1, prerequisiteId="",          cost=100 },
            new TechNodeData { id="emp_sat_2", name="기초 복지 인프라",   category=TechCategory.EmployeeSatisfaction, order=2, prerequisiteId="emp_sat_1", cost=100 },
            new TechNodeData { id="emp_sat_3", name="전문 멘탈 케어",     category=TechCategory.EmployeeSatisfaction, order=3, prerequisiteId="emp_sat_2", cost=100 },
            new TechNodeData { id="emp_sat_4", name="고급 테마 라운지",   category=TechCategory.EmployeeSatisfaction, order=4, prerequisiteId="emp_sat_3", cost=100 },
            new TechNodeData { id="emp_sat_5", name="성과급 체계 도입",   category=TechCategory.EmployeeSatisfaction, order=5, prerequisiteId="emp_sat_4", cost=100 },

            // ── 직원관리 - 효율성 극대화 ────────────
            new TechNodeData { id="emp_eff_1", name="야근 및 주말 근무",     category=TechCategory.EmployeeEfficiency, order=1, prerequisiteId="",          cost=100 },
            new TechNodeData { id="emp_eff_2", name="직군 이동 최적화",      category=TechCategory.EmployeeEfficiency, order=2, prerequisiteId="emp_eff_1", cost=100 },
            new TechNodeData { id="emp_eff_3", name="전략적 인재 영입",      category=TechCategory.EmployeeEfficiency, order=3, prerequisiteId="emp_eff_2", cost=100 },
            new TechNodeData { id="emp_eff_4", name="한계 돌파 훈련",        category=TechCategory.EmployeeEfficiency, order=4, prerequisiteId="emp_eff_3", cost=100 },
            new TechNodeData { id="emp_eff_5", name="성장 확률 증가",        category=TechCategory.EmployeeEfficiency, order=5, prerequisiteId="emp_eff_4", cost=100 },

            // ── 기술연구 - 장르/플랫폼 ──────────────
            new TechNodeData { id="tech_gp_1", name="장르 마스터리 1단계",  category=TechCategory.GenrePlatform, order=1, prerequisiteId="",          cost=100 },
            new TechNodeData { id="tech_gp_2", name="장르 마스터리 2단계",  category=TechCategory.GenrePlatform, order=2, prerequisiteId="tech_gp_1", cost=100 },
            new TechNodeData { id="tech_gp_3", name="플랫폼 라이선스",      category=TechCategory.GenrePlatform, order=3, prerequisiteId="tech_gp_2", cost=100 },
            new TechNodeData { id="tech_gp_4", name="플랫폼 최적화",        category=TechCategory.GenrePlatform, order=4, prerequisiteId="tech_gp_3", cost=100 },

            // ── 기술연구 - 참신함 ────────────────────
            new TechNodeData { id="tech_nov_1", name="참신함 연구 1단계",   category=TechCategory.Novelty, order=1, prerequisiteId="",            cost=100 },
            new TechNodeData { id="tech_nov_2", name="참신함 연구 2단계",   category=TechCategory.Novelty, order=2, prerequisiteId="tech_nov_1",  cost=100 },
            new TechNodeData { id="tech_nov_3", name="이스터에그 설계",     category=TechCategory.Novelty, order=3, prerequisiteId="tech_nov_2",  cost=100 },
            new TechNodeData { id="tech_nov_4", name="실시간 상호작용",     category=TechCategory.Novelty, order=4, prerequisiteId="tech_nov_3",  cost=100 },

            // ── 유틸리티 ────────────────────────────
            new TechNodeData { id="util_1", name="트렌드 레이더",          category=TechCategory.Utility, order=1, prerequisiteId="",        cost=100 },
            new TechNodeData { id="util_2", name="장르 블렌더",            category=TechCategory.Utility, order=2, prerequisiteId="util_1",  cost=100 },
            new TechNodeData { id="util_3", name="프랜차이즈 빌더",        category=TechCategory.Utility, order=3, prerequisiteId="util_2",  cost=100 },
            new TechNodeData { id="util_4", name="하이퍼 프로세스",        category=TechCategory.Utility, order=4, prerequisiteId="util_3",  cost=100 },
            new TechNodeData { id="util_5", name="신용 분석 알고리즘",     category=TechCategory.Utility, order=5, prerequisiteId="util_4",  cost=100 },
            new TechNodeData { id="util_6", name="금융권 네트워킹",        category=TechCategory.Utility, order=6, prerequisiteId="util_5",  cost=100 },
        };
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

        Debug.Log($"테크 해금: {node.name}");
    }

    // ── 저장 ─────────────────────────────────
    private string _rowInDate = null;

    void SaveTechTree()
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
            });
        }
    }

    // ── 로드 ─────────────────────────────────
    public void LoadTechTree(System.Action onComplete = null)
    {
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
}