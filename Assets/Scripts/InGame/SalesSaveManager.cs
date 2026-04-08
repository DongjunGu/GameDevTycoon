using UnityEngine;
using BackEnd;
using LitJson;

public class SalesSaveManager : MonoBehaviour
{
    public static SalesSaveManager Instance { get; private set; }

    private string _rowInDate = null;
    private bool _hasPendingRestore = false;

    // ── 로드된 데이터 ─────────────────────────
    private bool _loadedIsActive;
    private int _loadedCompletedWeeks;
    private int _loadedTotalUnits;
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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 ─────────────────────────────────
    public void SaveSales(int completedWeeks, int totalUnits,
        float qualityScore, ProjectScale salesScale, string projectName,
        ProjectScale cachedScale, ProjectGenre cachedGenre, ProjectPlatform cachedPlatform,
        float planning, float develop, float art, float creativity, float bug)
    {
        var param = new Param();
        param.Add("isActive", true);
        param.Add("completedWeeks", completedWeeks);
        param.Add("totalUnits", totalUnits);
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
        else
        {
            Backend.GameData.Insert("UserSales", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("판매 Insert 완료");
                }
                else
                {
                    Debug.LogError($"판매 Insert 실패: {bro}");
                }
            });
        }
    }

    // ── 판매 완료 ─────────────────────────────
    public void CompleteSales()
    {
        if (string.IsNullOrEmpty(_rowInDate)) return;

        var param = new Param();
        param.Add("isActive", false);

        Backend.GameData.UpdateV2("UserSales", _rowInDate, Backend.UserInDate, param, bro =>
        {
            if (bro.IsSuccess())
            {
                _rowInDate = null;
                Debug.Log("판매 완료 처리");
            }
            else
            {
                Debug.LogError($"판매 완료 저장 실패: {bro}");
            }
        });
    }

    // ── 로드 ─────────────────────────────────
    public void LoadSales(System.Action onComplete = null)
    {
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
                onComplete?.Invoke();
                return;
            }

            JsonData row = rows[rows.Count - 1];
            _rowInDate = SafeString(row, "inDate", "");

            _loadedIsActive = SafeBool(row, "isActive", false);
            if (!_loadedIsActive)
            {
                Debug.Log("진행 중인 판매 없음");
                onComplete?.Invoke();
                return;
            }

            _loadedCompletedWeeks = SafeInt(row, "completedWeeks", 0);
            _loadedTotalUnits     = SafeInt(row, "totalUnits", 0);
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
            _loadedBug            = SafeFloat(row, "bug", 0f);

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

        AlertUI.Instance.Show("판매 시작!", () =>
        {
            SalesUI.Instance.ShowWithProjectName(
                _loadedQualityScore, _loadedSalesScale, _loadedProjectName,
                _loadedCachedScale, _loadedCachedGenre, _loadedCachedPlatform,
                _loadedPlanning, _loadedDevelop, _loadedArt, _loadedCreativity, _loadedBug,
                _loadedCompletedWeeks, _loadedTotalUnits
            );
        });
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

    static string SafeString(JsonData row, string key, string fallback)
    {
        try { return row[key].ToString(); }
        catch { return fallback; }
    }
}
