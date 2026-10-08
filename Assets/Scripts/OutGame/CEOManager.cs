using System;
using BackEnd;
using LitJson;
using UnityEngine;
using UnityEngine.Serialization;

// CEO 시스템 매니저 (메타, 영구 보존 — 런 무관)
//
// 책(Book) 강화: 기획/개발/아트 3트랙이 각각 Lv1~MaxLevel 독립. 책 + 아웃게임 골드를 소모하는 확정 강화.
//   - 비용: BookChartLoader (level → level+1)
//   - 재화: OutGameCurrencyManager (book / gold)
//   - 특수효과: 10/20/30/40 레벨 마일스톤 — HasMilestone(part, level) 로 조회
//
// 뒤끝 테이블: UserCEO (단일 row) — planningLevel / developLevel / artLevel (int)
public class CEOManager : MonoBehaviour
{
    public static CEOManager Instance { get; private set; }

    public enum Part { Planning, Develop, Art }

    public const int MaxLevel = 40;

    [Header("Defaults")]
    [Tooltip("Lv1 기본 능력치 (기획/개발/아트 동일)")]
    public int baseStat = 50;
    [Tooltip("레벨당 능력치 증가량. Lv N = baseStat + (N-1) * statPerLevel.")]
    [FormerlySerializedAs("statPerStage")]
    public int statPerLevel = 10;
    [Tooltip("Lv10 마일스톤 도달 시 1회 가산되는 능력치 보너스. 기본 +100.")]
    [FormerlySerializedAs("stage7Bonus")]
    public int level10Bonus = 100;

    [Header("CEO Identity")]
    public string ceoEmployeeId = "ceo_001";
    public string ceoName = "CEO";
    [Tooltip("OfficeCharacter 컴포넌트가 부착된 캐릭터 prefab. 인스펙터에서 직접 드래그 드롭.")]
    public GameObject ceoPrefab;
    [Tooltip("CEO 가 항상 앉을 데스크 ID. 인게임 진입 시 자동 점유되어 일반 직원이 못 앉음.")]
    public string ceoDeskId = "desk_04";

    static readonly string[] LevelColumns = { "planningLevel", "developLevel", "artLevel" };

    private readonly int[] _levels = { 1, 1, 1 };
    private string _rowInDate;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int GetLevel(Part part) => _levels[(int)part];

    // 마일스톤 특수효과 조회 (10/20/30/40). 인스턴스 없으면 false.
    public static bool HasMilestone(Part part, int level) => Instance != null && Instance.GetLevel(part) >= level;

    // 능력치 공식: base + (Lv-1) * statPerLevel + Lv10 도달 시 level10Bonus 1회 가산.
    public int GetStat(Part part)
    {
        int lv = GetLevel(part);
        return baseStat + (lv - 1) * statPerLevel + (lv >= 10 ? level10Bonus : 0);
    }

    public int GetPlanning() => GetStat(Part.Planning);
    public int GetDevelop()  => GetStat(Part.Develop);
    public int GetArt()      => GetStat(Part.Art);

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserCEO", bro =>
        {
            for (int i = 0; i < _levels.Length; i++) _levels[i] = 1;
            _rowInDate = null;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate = row["inDate"]?.ToString();
                    for (int i = 0; i < _levels.Length; i++)
                        _levels[i] = Mathf.Clamp(SafeInt(row, LevelColumns[i], 1), 1, MaxLevel);
                }
                Debug.Log($"[CEO] 로드: 기획 Lv{_levels[0]} / 개발 Lv{_levels[1]} / 아트 Lv{_levels[2]}");
            }
            else
            {
                Debug.LogError($"[CEO] 로드 실패: {bro}");
            }

            OnChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    // ──────── 강화 ────────

    public bool CanUpgrade(Part part)
    {
        var currency = OutGameCurrencyManager.Instance;
        if (currency == null) return false;
        if (!BookChartLoader.TryGetCost(GetLevel(part), out int book, out int gold)) return false; // MAX
        return currency.Book >= book && currency.Gold >= gold;
    }

    public bool TryUpgrade(Part part)
    {
        if (!CanUpgrade(part)) return false;
        BookChartLoader.TryGetCost(GetLevel(part), out int book, out int gold);

        var currency = OutGameCurrencyManager.Instance;
        currency.SpendGold(gold, saveImmediately: false);
        currency.SpendBook(book); // 골드+책 한 번에 저장

        _levels[(int)part]++;
        Save();
        OnChanged?.Invoke();
        Debug.Log($"[CEO] 강화 → {part} Lv.{GetLevel(part)} (책 -{book} / 골드 -{gold})");
        return true;
    }

    void Save()
    {
        var param = new Param();
        for (int i = 0; i < _levels.Length; i++) param.Add(LevelColumns[i], _levels[i]);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserCEO", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[CEO] 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserCEO", param, bro =>
            {
                if (bro.IsSuccess()) _rowInDate = bro.GetInDate();
                else Debug.LogError($"[CEO] Insert 실패: {bro}");
            });
        }
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
        ceo.portraitId       = "ceo_001"; // Resources/Portraits/ + Portraits/Mini/ 에 ceo_001 이미지 필요
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
