using System;
using BackEnd;
using LitJson;
using UnityEngine;

// CEO 시스템 매니저 (메타, 영구 보존 — 런 무관)
//
// 상태 single source: StoneManager.ActiveStone (Stones 리스트의 한 항목).
// CEOManager 는 stage/progress 를 ActiveStone 에서 위임 조회. 자체 저장 없음.
// LoadAsync 는 구 UserCEO row 가 있으면 마이그레이션 시드용으로만 읽어 둠 → StoneManager.LoadAsync 가 활용.
//
// 강화/리셋은 ActiveStone 을 직접 mutate 하고 StoneManager.MarkDirty 로 저장 트리거.
public class CEOManager : MonoBehaviour
{
    public static CEOManager Instance { get; private set; }

    [Header("Defaults")]
    [Tooltip("강화 단계 0 일 때 기본 능력치 (기획/개발/아트 동일)")]
    public int baseStat = 50;
    [Tooltip("1~6 단계 도달 시 단계당 누적되는 능력치 보너스. 6단계면 +60.")]
    public int statPerStage = 10;
    [Tooltip("7단계 도달 시 추가되는 1회성 보너스 (1~6 누적 위에 한 번 더 가산). 기본 +100.")]
    public int stage7Bonus = 100;

    [Header("Upgrade")]
    [Tooltip("강화 가중치. 인덱스 = 현재 단계. Lv.0~Lv.10 (11개). 합이 maxTotalStage 도달 시 강화 불가.")]
    public float[] upgradeWeights = new float[] { 100f, 73f, 53.3f, 38.9f, 28.4f, 20.7f, 15.1f, 11f, 8.1f, 5.9f, 0f };
    [Tooltip("강화 합계 최대치 (planning+develop+art). 이 합 도달 시 강화 버튼 비활성.")]
    public int maxTotalStage = 20;

    [Header("CEO Identity")]
    public string ceoEmployeeId = "ceo_001";
    public string ceoName = "CEO";
    [Tooltip("OfficeCharacter 컴포넌트가 부착된 캐릭터 prefab. 인스펙터에서 직접 드래그 드롭.")]
    public GameObject ceoPrefab;
    [Tooltip("CEO 가 항상 앉을 데스크 ID. 인게임 진입 시 자동 점유되어 일반 직원이 못 앉음.")]
    public string ceoDeskId = "desk_04";

    // ── 활성 돌 위임 조회 ─────────────────────
    public int PlanningStage    => StoneManager.Instance?.ActiveStone?.planningStage    ?? 0;
    public int DevelopStage     => StoneManager.Instance?.ActiveStone?.developStage     ?? 0;
    public int ArtStage         => StoneManager.Instance?.ActiveStone?.artStage         ?? 0;
    public int PlanningProgress => StoneManager.Instance?.ActiveStone?.planningProgress ?? 0;
    public int DevelopProgress  => StoneManager.Instance?.ActiveStone?.developProgress  ?? 0;
    public int ArtProgress      => StoneManager.Instance?.ActiveStone?.artProgress      ?? 0;
    public bool HasActive       => StoneManager.Instance?.ActiveStone != null;

    // ── 마이그레이션 시드 (구 UserCEO row → StoneManager.LoadAsync 가 첫 active 돌 생성용으로 사용) ──
    public bool HasMigrationSeed { get; private set; }
    public int MigPlanningStage { get; private set; }
    public int MigDevelopStage { get; private set; }
    public int MigArtStage { get; private set; }
    public int MigPlanningProgress { get; private set; }
    public int MigDevelopProgress { get; private set; }
    public int MigArtProgress { get; private set; }

    public event Action OnChanged;

    // StoneManager 가 ActiveStone 갱신 시 호출 → PiecePanelUI 등 구독자 갱신.
    public void RaiseChanged() => OnChanged?.Invoke();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 더 이상 자체 저장 없음. StoneManager.FlushPendingSave 가 모든 상태 저장.
    public void FlushPendingSave() { }

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserCEO", bro =>
        {
            HasMigrationSeed = false;
            MigPlanningStage = MigDevelopStage = MigArtStage = 0;
            MigPlanningProgress = MigDevelopProgress = MigArtProgress = 0;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    MigPlanningStage    = SafeInt(row, "planningStage",    0);
                    MigDevelopStage     = SafeInt(row, "developStage",     0);
                    MigArtStage         = SafeInt(row, "artStage",         0);
                    MigPlanningProgress = SafeInt(row, "planningProgress", 0);
                    MigDevelopProgress  = SafeInt(row, "developProgress",  0);
                    MigArtProgress      = SafeInt(row, "artProgress",      0);
                    bool hadActive = SafeInt(row, "hasActive", 1) != 0;
                    // 마이그레이션 트리거: 구 UserCEO 가 hasActive=true 였고 어떤 stage 든 진행이 있었을 때만.
                    HasMigrationSeed = hadActive && (MigPlanningStage > 0 || MigDevelopStage > 0 || MigArtStage > 0);
                    Debug.Log($"[CEO] UserCEO 읽음 (마이그레이션용): seed={HasMigrationSeed} P{MigPlanningStage}/D{MigDevelopStage}/A{MigArtStage}");
                }
            }
            onComplete?.Invoke();
        });
    }

    // 능력치 공식: base + 1~6단계 누적(statPerStage 씩) + 7단계 도달 시 stage7Bonus 1회 가산.
    // 8/9/10 단계는 카테고리 보너스라 능력치에 가산 안 함 (StoneBonusListUI 가 row 로 표시).
    public int GetPlanning() => baseStat + Mathf.Min(PlanningStage, 6) * statPerStage + (PlanningStage >= 7 ? stage7Bonus : 0);
    public int GetDevelop()  => baseStat + Mathf.Min(DevelopStage,  6) * statPerStage + (DevelopStage  >= 7 ? stage7Bonus : 0);
    public int GetArt()      => baseStat + Mathf.Min(ArtStage,      6) * statPerStage + (ArtStage      >= 7 ? stage7Bonus : 0);

    public void ResetAll()
    {
        var s = StoneManager.Instance?.ActiveStone;
        if (s == null) return;
        s.planningStage    = 0;
        s.developStage     = 0;
        s.artStage         = 0;
        s.planningProgress = 0;
        s.developProgress  = 0;
        s.artProgress      = 0;
        StoneManager.Instance.MarkDirty();
        OnChanged?.Invoke();
        Debug.Log("[CEO] 리셋 완료");
    }

    // ──────── 강화 ────────
    public enum UpgradePart { Planning, Develop, Art }

    public struct UpgradeResult
    {
        public UpgradePart part;
        public int newStage;
    }

    float GetWeight(int level)
    {
        if (upgradeWeights == null || upgradeWeights.Length == 0) return 0f;
        int idx = Mathf.Clamp(level, 0, upgradeWeights.Length - 1);
        return upgradeWeights[idx];
    }

    public int TotalStage => PlanningStage + DevelopStage + ArtStage;

    public bool CanUpgrade()
    {
        if (!HasActive) return false;
        if (TotalStage >= maxTotalStage) return false;
        float sum = GetWeight(PlanningStage) + GetWeight(DevelopStage) + GetWeight(ArtStage);
        return sum > 0f;
    }

    public float[] GetProbabilities()
    {
        float wP = GetWeight(PlanningStage);
        float wD = GetWeight(DevelopStage);
        float wA = GetWeight(ArtStage);
        float sum = wP + wD + wA;
        if (sum <= 0f) return new[] { 0f, 0f, 0f };
        return new[] { wP / sum, wD / sum, wA / sum };
    }

    public bool TryUpgrade(out UpgradeResult result)
    {
        result = default;
        if (!CanUpgrade()) return false;
        var s = StoneManager.Instance?.ActiveStone;
        if (s == null) return false;

        float wP = GetWeight(s.planningStage);
        float wD = GetWeight(s.developStage);
        float wA = GetWeight(s.artStage);
        float sum  = wP + wD + wA;
        float roll = UnityEngine.Random.Range(0f, sum);

        if (roll < wP)
        {
            s.planningStage++;
            result.part = UpgradePart.Planning;
            result.newStage = s.planningStage;
        }
        else if (roll < wP + wD)
        {
            s.developStage++;
            result.part = UpgradePart.Develop;
            result.newStage = s.developStage;
        }
        else
        {
            s.artStage++;
            result.part = UpgradePart.Art;
            result.newStage = s.artStage;
        }

        StoneManager.Instance.MarkDirty();
        OnChanged?.Invoke();
        Debug.Log($"[CEO] 강화 → {result.part} Lv.{result.newStage} (Total {TotalStage}/{maxTotalStage})");
        return true;
    }

    public EmployeeData CreateCEOEmployee()
    {
        int p = GetPlanning();
        int d = GetDevelop();
        int a = GetArt();

        var ceo = new EmployeeData(
            id: ceoEmployeeId,
            name: ceoName,
            role: EmployeeRole.Planner,
            developMin: d, developMax: d,
            planningMin: p, planningMax: p,
            artMin: a, artMax: a,
            creativityMin: 0, creativityMax: 0,
            salaryMin: 0, salaryMax: 0,
            maxGrade: EmployeeGrade.Normal
        );
        ceo.employeeName     = ceoName;
        ceo.developSkill     = d;
        ceo.planningSkill    = p;
        ceo.artSkill         = a;
        ceo.creativitySkill  = 0;
        ceo.salary           = 0;
        ceo.satisfaction     = 100;
        ceo.portraitId       = "";
        ceo.isCEO            = true;
        ceo.isDefault        = false;
        ceo.assignedDeskId   = ceoDeskId;
        ceo.masterEmployeeId = ceoEmployeeId;
        ceo.hiredYear        = 0;
        ceo.grade            = EmployeeGrade.Normal;
        return ceo;
    }

    static int SafeInt(JsonData row, string key, int fallback)
    {
        if (row == null || !row.ContainsKey(key)) return fallback;
        if (int.TryParse(row[key]?.ToString(), out int val)) return val;
        return fallback;
    }
}
