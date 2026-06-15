using UnityEngine;
using BackEnd;
using LitJson;

public class SalesSaveManager : MonoBehaviour
{
    public static SalesSaveManager Instance { get; private set; }

    private string _rowInDate = null;
    private bool _hasPendingRestore = false;
    private bool _insertInProgress = false;
    private Param _pendingUpdate = null;

    // ── 로드된 데이터 ─────────────────────────
    private bool _loadedIsActive;
    private int _loadedCompletedWeeks;
    private int _loadedTotalRevenue;
    private int[] _loadedRevenuePerPeriod = null; // 세션 시작 시 동결된 주차별 매출 배열
    private float _loadedQualityScore;
    private ProjectScale _loadedSalesScale;
    private string _loadedProjectName;
    private ProjectScale _loadedCachedScale;
    private ProjectGenre _loadedCachedGenre;
    private ProjectPlatform _loadedCachedPlatform;
    private float _loadedPlanning;
    private float _loadedDevelop;
    private float _loadedArt;
    private float _loadedCreativity;
    private float _loadedBug;
    private bool _loadedNewProjectStarted;
    private string _loadedCompletedProjectRowInDate;

    public bool LoadedNewProjectStarted => _loadedNewProjectStarted;
    public string LoadedCompletedProjectRowInDate => _loadedCompletedProjectRowInDate;

    public bool HasPendingRestore => _hasPendingRestore;
    public bool WasRestored { get; private set; } = false;

    // 로드된 판매가 "이미 완료된 상태"인지. stage=Marketing 잔존(SaveProject 가 판매 중 스킵돼 발생)으로
    // ProjectSaveManager 가 "판매 시작!" 으로 복원하는 것을 막는 데 사용.
    private bool _loadedSalesAlreadyComplete;
    public bool SalesAlreadyComplete => _loadedSalesAlreadyComplete;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 ─────────────────────────────────
    // isActive: 진행 중=true / 마지막 주차(완료)=false. 완료를 별도 UpdateV2(CompleteSales)로 분리하지 않고
    // 마지막 저장에 함께 실으면 "마지막에 쓰는 값"이 항상 isActive=false 가 되어 복원 race 가 사라진다.
    public void SaveSales(int completedWeeks, int totalRevenue, int[] revenuePerPeriod,
        float qualityScore, ProjectScale salesScale, string projectName,
        ProjectScale cachedScale, ProjectGenre cachedGenre, ProjectPlatform cachedPlatform,
        float planning, float develop, float art, float creativity, float bug,
        bool isActive = true)
    {
        var param = new Param();
        param.Add("isActive", isActive);
        param.Add("completedWeeks", completedWeeks);
        param.Add("totalRevenue", totalRevenue);
        // 세션 시작 시 동결된 주차별 매출 배열 — 복원 시 distribution/comebackUnlocked 재계산 안 함
        param.Add("revenuePerPeriod", revenuePerPeriod != null ? string.Join(",", revenuePerPeriod) : "");
        param.Add("qualityScore", qualityScore);
        param.Add("salesScale", (int)salesScale);
        param.Add("projectName", projectName);
        param.Add("cachedScale", (int)cachedScale);
        param.Add("cachedGenre", (int)cachedGenre);
        param.Add("cachedPlatform", (int)cachedPlatform);
        param.Add("planning", planning);
        param.Add("develop", develop);
        param.Add("art", art);
        param.Add("creativity", creativity);
        param.Add("bug", bug);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (!bro.IsSuccess())
                    Debug.LogError($"판매 저장 실패: {bro}");
            });
        }
        else if (_insertInProgress)
        {
            // Insert 진행 중 — 완료 후 적용될 업데이트로 캐시
            _pendingUpdate = param;
        }
        else
        {
            _insertInProgress = true;
            _pendingUpdate = null;
            Backend.GameData.Insert("UserSales", param, bro =>
            {
                _insertInProgress = false;
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("판매 Insert 완료");
                    if (_pendingUpdate != null)
                    {
                        Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, _pendingUpdate, bro2 =>
                        {
                            if (!bro2.IsSuccess()) Debug.LogError($"판매 pendingUpdate 실패: {bro2}");
                        });
                        _pendingUpdate = null;
                    }
                }
                else
                {
                    Debug.LogError($"판매 Insert 실패: {bro}");
                }
            });
        }
    }

    // ── 완료 프로젝트 rowInDate 저장 ──────────
    public void SaveCompletedProjectRowInDate(string rowInDate)
    {
        if (string.IsNullOrEmpty(_rowInDate)) return;
        var param = new Param();
        param.Add("completedProjectRowInDate", rowInDate);
        Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, param, bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"completedProjectRowInDate 저장 실패: {bro}");
        });
    }

    // ── 새 프로젝트 시작 플래그 저장 ─────────
    public void SaveNewProjectStarted()
    {
        if (string.IsNullOrEmpty(_rowInDate)) return;
        var param = new Param();
        param.Add("newProjectStarted", true);
        Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, param, bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogError($"newProjectStarted 저장 실패: {bro}");
        });
    }

    // ── 판매 완료 ─────────────────────────────
    // 보조 안전망: 마지막 주차 SaveSales 가 이미 isActive=false 를 썼으므로 사실상 중복(둘 다 false → race 무해).
    // 마지막 SaveSales 가 어떤 이유로 누락돼도 여기서 한 번 더 isActive=false 를 보장한다.
    public void CompleteSales()
    {
        if (string.IsNullOrEmpty(_rowInDate)) return;

        var param = new Param();
        param.Add("isActive", false);

        Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, param, bro =>
        {
            if (bro.IsSuccess())
                Debug.Log("판매 완료 처리");
            else
                Debug.LogError($"판매 완료 저장 실패: {bro}");
        });
    }

    // 새 런 시작 — UserSales row 삭제 + 복원 대기 상태 클리어
    public void ResetForNewRun(System.Action onComplete = null)
    {
        _hasPendingRestore = false;
        WasRestored = false;
        _insertInProgress = false;
        _pendingUpdate = null;

        if (string.IsNullOrEmpty(_rowInDate)) { onComplete?.Invoke(); return; }

        var oldRow = _rowInDate;
        _rowInDate = null;
        Backend.GameData.DeleteV2("UserSales", oldRow, Backend.UserInDate, bro =>
        {
            if (!bro.IsSuccess()) Debug.LogError($"[Reset] UserSales delete 실패: {bro}");
            onComplete?.Invoke();
        });
    }

    // ── 로드 ─────────────────────────────────
    public void LoadSales(System.Action onComplete = null)
    {
        _loadedSalesAlreadyComplete = false; // 매 로드마다 재판정 (이전 런/로드 값 stale 방지)
        Backend.GameData.GetMyData("UserSales", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.Log("판매 데이터 없음");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                // 첫 플레이 — 빈 행 미리 생성해 _rowInDate 확보 (이후 모든 저장이 UpdateV2 사용)
                var initParam = new Param();
                initParam.Add("isActive", false);
                Backend.GameData.Insert("UserSales", initParam, initBro =>
                {
                    if (initBro.IsSuccess())
                    {
                        _rowInDate = initBro.GetInDate();
                        Debug.Log($"[SalesSaveManager] 초기 행 생성: {_rowInDate}");
                    }
                    else
                    {
                        Debug.LogError($"[SalesSaveManager] 초기 행 생성 실패: {initBro}");
                    }
                    onComplete?.Invoke();
                });
                return;
            }

            JsonData row = rows[rows.Count - 1];
            _rowInDate = SafeString(row, "inDate", "");

            _loadedIsActive         = SafeBool(row, "isActive", false);
            _loadedCompletedWeeks   = SafeInt(row, "completedWeeks", 0);
            _loadedTotalRevenue     = SafeInt(row, "totalRevenue", 0);
            _loadedRevenuePerPeriod = ParseRevenuePerPeriod(SafeString(row, "revenuePerPeriod", ""));

            // 완료 판정 — 두 신호(isActive, completedWeeks)를 일관되게 해석.
            //  · allWeeksDone: 모든 주차가 끝남 (isActive 가 race 로 true 로 남아도 완료로 간주)
            //  · 비활성(isActive=false)인데 판매 이력(completedWeeks>0)이 있으면 정상 완료
            // → 완료된 판매는 복원하지 않고, ProjectSaveManager 의 "판매 시작!"(stage=Marketing 잔존) 도 막는다.
            bool allWeeksDone = _loadedRevenuePerPeriod != null && _loadedCompletedWeeks >= _loadedRevenuePerPeriod.Length;
            _loadedSalesAlreadyComplete = allWeeksDone || (!_loadedIsActive && _loadedCompletedWeeks > 0);

            if (!_loadedIsActive || allWeeksDone)
            {
                Debug.Log($"진행 중인 판매 없음 (isActive={_loadedIsActive}, completedWeeks={_loadedCompletedWeeks}, complete={_loadedSalesAlreadyComplete})");
                onComplete?.Invoke();
                return;
            }

            _loadedQualityScore   = SafeFloat(row, "qualityScore", 0f);
            _loadedSalesScale     = (ProjectScale)SafeInt(row, "salesScale", 0);
            _loadedProjectName    = SafeString(row, "projectName", "프로젝트명");
            _loadedCachedScale    = (ProjectScale)SafeInt(row, "cachedScale", 0);
            _loadedCachedGenre    = (ProjectGenre)SafeInt(row, "cachedGenre", 0);
            _loadedCachedPlatform = (ProjectPlatform)SafeInt(row, "cachedPlatform", 0);
            _loadedPlanning       = SafeFloat(row, "planning", 0f);
            _loadedDevelop        = SafeFloat(row, "develop", 0f);
            _loadedArt            = SafeFloat(row, "art", 0f);
            _loadedCreativity     = SafeFloat(row, "creativity", 0f);
            _loadedBug                = SafeFloat(row, "bug", 0f);
            _loadedNewProjectStarted           = SafeBool(row,   "newProjectStarted",         false);
            _loadedCompletedProjectRowInDate   = SafeString(row, "completedProjectRowInDate",  "");

            _hasPendingRestore = true;
            Debug.Log($"판매 로드 완료: completedWeeks={_loadedCompletedWeeks}");
            onComplete?.Invoke();
        });
    }

    // ── 복원 ─────────────────────────────────
    public void RestoreIfNeeded()
    {
        if (!_hasPendingRestore) return;
        _hasPendingRestore = false;
        WasRestored = true;

        void OpenSales() => SalesUI.Instance.ShowWithProjectName(
            _loadedQualityScore, _loadedSalesScale, _loadedProjectName,
            _loadedCachedScale, _loadedCachedGenre, _loadedCachedPlatform,
            _loadedPlanning, _loadedDevelop, _loadedArt, _loadedCreativity, _loadedBug,
            _loadedCompletedWeeks, _loadedTotalRevenue, _loadedRevenuePerPeriod, applyCompletion: false
        );

        Debug.Log($"[SalesSaveManager] RestoreIfNeeded: completedWeeks={_loadedCompletedWeeks}");
        StartCoroutine(RestoreNextFrame(OpenSales));
    }

    System.Collections.IEnumerator RestoreNextFrame(System.Action action)
    {
        yield return null; // GameSceneInitializer.StartTime() 이후 실행 보장
        action();
    }

    // ── 헬퍼 ─────────────────────────────────
    static int SafeInt(JsonData row, string key, int fallback)
    {
        try { return int.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static float SafeFloat(JsonData row, string key, float fallback)
    {
        try { return float.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static bool SafeBool(JsonData row, string key, bool fallback)
    {
        try { return bool.Parse(row[key].ToString()); }
        catch { return fallback; }
    }

    static int[] ParseRevenuePerPeriod(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return null;
        var parts = csv.Split(',');
        var list = new System.Collections.Generic.List<int>(parts.Length);
        foreach (var p in parts)
            if (int.TryParse(p, out int v)) list.Add(v);
        return list.Count > 0 ? list.ToArray() : null;
    }

    static string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); }
        catch { return fallback; }
    }
}
