using System;
using System.Collections;
using BackEnd;
using LitJson;
using UnityEngine;

// CEO 시스템 매니저 (메타, 영구 보존 — 런 무관)
// 뒤끝 테이블: UserCEO (단일 row)
//   planningStage / developStage / artStage         (int, 강화 단계, 디폴트 0)
//   planningProgress / developProgress / artProgress (int, 강화 시도 누적 progress — 강화 시스템 구현 시 사용)
//
// 인게임 진입 시 CreateCEOEmployee() 로 EmployeeData 인스턴스 생성 → EmployeeManager.CEO 로 보관.
// ownedEmployees 에는 안 들어가므로 만족도/아이템/이벤트/연봉/카운트/퀘스트 자동 제외, 팀장 후보로만 노출.
//
// 강화 API (PiecePanel 작업 시): TryUpgrade(part, cost, successPct, ...) — TODO
public class CEOManager : MonoBehaviour
{
    public static CEOManager Instance { get; private set; }

    [Header("Defaults")]
    [Tooltip("강화 단계 0 일 때 기본 능력치 (기획/개발/아트 동일)")]
    public int baseStat = 50;
    [Tooltip("단계당 능력치 증가량")]
    public int statPerStage = 5;

    [Header("Upgrade")]
    [Tooltip("강화 가중치. 인덱스 = 현재 단계. Lv.0~Lv.10 (11개). 합이 maxTotalStage 도달 시 강화 불가.")]
    public float[] upgradeWeights = new float[] { 100f, 73f, 53.3f, 38.9f, 28.4f, 20.7f, 15.1f, 11f, 8.1f, 5.9f, 0f };
    [Tooltip("강화 합계 최대치 (planning+develop+art). 이 합 도달 시 강화 버튼 비활성.")]
    public int maxTotalStage = 20;

    [Header("Save Debounce")]
    [Tooltip("변경 후 이 시간(초) 동안 추가 변경이 없으면 서버 저장. 연쇄 강화/리셋 묶음.")]
    public float saveDebounceSeconds = 0.7f;

    private bool _dirty;
    private Coroutine _saveCo;

    [Header("CEO Identity")]
    public string ceoEmployeeId = "ceo_001";
    public string ceoName = "CEO";
    [Tooltip("OfficeCharacter 컴포넌트가 부착된 캐릭터 prefab. 인스펙터에서 직접 드래그 드롭.")]
    public GameObject ceoPrefab;
    [Tooltip("CEO 가 항상 앉을 데스크 ID. 인게임 진입 시 자동 점유되어 일반 직원이 못 앉음.")]
    public string ceoDeskId = "desk_04";

    public int PlanningStage    { get; private set; }
    public int DevelopStage     { get; private set; }
    public int ArtStage         { get; private set; }
    public int PlanningProgress { get; private set; }
    public int DevelopProgress  { get; private set; }
    public int ArtProgress      { get; private set; }

    private string _rowInDate;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()                       => FlushPendingSave();
    void OnApplicationQuit()               => FlushPendingSave();
    void OnApplicationPause(bool paused) { if (paused) FlushPendingSave(); }

    // 변경 발생 시 호출 — debounce 타이머 시작/리셋. 연쇄 변경은 마지막만 서버 호출.
    void ScheduleSave()
    {
        _dirty = true;
        if (_saveCo != null) StopCoroutine(_saveCo);
        _saveCo = StartCoroutine(SaveAfterDelay());
    }

    IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(saveDebounceSeconds);
        _saveCo = null;
        FlushPendingSave();
    }

    // 외부에서 즉시 저장 강제 (예: 인게임 진입 직전)
    public void FlushPendingSave()
    {
        if (_saveCo != null) { StopCoroutine(_saveCo); _saveCo = null; }
        if (!_dirty) return;
        _dirty = false;
        Save();
    }

    public void LoadAsync(Action onComplete = null)
    {
        BackendRetry.Instance.GetMyData("UserCEO", bro =>
        {
            PlanningStage = DevelopStage = ArtStage = 0;
            PlanningProgress = DevelopProgress = ArtProgress = 0;
            _rowInDate = null;

            if (bro.IsSuccess())
            {
                var rows = bro.FlattenRows();
                if (rows.Count > 0)
                {
                    JsonData row = rows[0];
                    _rowInDate       = row["inDate"]?.ToString();
                    PlanningStage    = SafeInt(row, "planningStage",    0);
                    DevelopStage     = SafeInt(row, "developStage",     0);
                    ArtStage         = SafeInt(row, "artStage",         0);
                    PlanningProgress = SafeInt(row, "planningProgress", 0);
                    DevelopProgress  = SafeInt(row, "developProgress",  0);
                    ArtProgress      = SafeInt(row, "artProgress",      0);
                    Debug.Log($"[CEO] 로드: 단계 P{PlanningStage}/D{DevelopStage}/A{ArtStage}");
                }
                else
                {
                    Save(); // 신규 row insert (디폴트 0)
                    Debug.Log("[CEO] 신규 유저 — 빈 상태로 초기화");
                }
            }
            else
            {
                Debug.LogError($"[CEO] 로드 실패: {bro}");
            }
            onComplete?.Invoke();
        });
    }

    public int GetPlanning() => baseStat + PlanningStage * statPerStage;
    public int GetDevelop()  => baseStat + DevelopStage  * statPerStage;
    public int GetArt()      => baseStat + ArtStage      * statPerStage;

    // 모든 단계/진척도 초기화. PiecePanel 의 리셋 버튼에서 호출.
    public void ResetAll()
    {
        PlanningStage = 0;
        DevelopStage  = 0;
        ArtStage      = 0;
        PlanningProgress = 0;
        DevelopProgress  = 0;
        ArtProgress      = 0;
        ScheduleSave();
        OnChanged?.Invoke();
        Debug.Log("[CEO] 리셋 완료");
    }

    public event Action OnChanged;

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
        if (TotalStage >= maxTotalStage) return false;
        float sum = GetWeight(PlanningStage) + GetWeight(DevelopStage) + GetWeight(ArtStage);
        return sum > 0f;
    }

    // UI 노출용 — 각 스탯이 다음에 오를 확률 (0~1). 합 0 이면 모두 0.
    public float[] GetProbabilities()
    {
        float wP = GetWeight(PlanningStage);
        float wD = GetWeight(DevelopStage);
        float wA = GetWeight(ArtStage);
        float sum = wP + wD + wA;
        if (sum <= 0f) return new[] { 0f, 0f, 0f };
        return new[] { wP / sum, wD / sum, wA / sum };
    }

    // 강화 시도 (비용 없음). 가중치 기반 랜덤으로 한 스탯 +1. 결과 반환.
    public bool TryUpgrade(out UpgradeResult result)
    {
        result = default;
        if (!CanUpgrade()) return false;

        float wP = GetWeight(PlanningStage);
        float wD = GetWeight(DevelopStage);
        float wA = GetWeight(ArtStage);
        float sum  = wP + wD + wA;
        float roll = UnityEngine.Random.Range(0f, sum);

        if (roll < wP)
        {
            PlanningStage++;
            result.part = UpgradePart.Planning;
            result.newStage = PlanningStage;
        }
        else if (roll < wP + wD)
        {
            DevelopStage++;
            result.part = UpgradePart.Develop;
            result.newStage = DevelopStage;
        }
        else
        {
            ArtStage++;
            result.part = UpgradePart.Art;
            result.newStage = ArtStage;
        }

        ScheduleSave();
        OnChanged?.Invoke();
        Debug.Log($"[CEO] 강화 → {result.part} Lv.{result.newStage} (Total {TotalStage}/{maxTotalStage})");
        return true;
    }

    // 인게임 진입 시 EmployeeData 인스턴스 생성 — EmployeeManager.CEO 로 보관
    public EmployeeData CreateCEOEmployee()
    {
        int p = GetPlanning();
        int d = GetDevelop();
        int a = GetArt();

        var ceo = new EmployeeData(
            id: ceoEmployeeId,
            name: ceoName,
            role: EmployeeRole.Planner,   // 단일 enum 필드 채우기용 — isCEO 가드로 모든 role 통과 처리
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
        ceo.portraitId       = ""; // prefab 직접 참조라 portraitId 불필요
        ceo.isCEO            = true;
        ceo.isDefault        = false;
        ceo.assignedDeskId   = ceoDeskId;
        ceo.masterEmployeeId = ceoEmployeeId;
        ceo.hiredYear        = 0;
        ceo.grade            = EmployeeGrade.Normal; // 등급 표시/배경 셔머 비활성
        return ceo;
    }

    void Save()
    {
        var param = new Param();
        param.Add("planningStage",    PlanningStage);
        param.Add("developStage",     DevelopStage);
        param.Add("artStage",         ArtStage);
        param.Add("planningProgress", PlanningProgress);
        param.Add("developProgress",  DevelopProgress);
        param.Add("artProgress",      ArtProgress);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserCEO", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess()) Debug.LogError($"[CEO] Update 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserCEO", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("[CEO] Insert 완료");
                }
                else
                {
                    Debug.LogError($"[CEO] Insert 실패: {bro}");
                }
            });
        }
    }

    static int SafeInt(JsonData row, string key, int fallback)
    {
        if (row == null || !row.ContainsKey(key)) return fallback;
        if (int.TryParse(row[key]?.ToString(), out int val)) return val;
        return fallback;
    }
}
