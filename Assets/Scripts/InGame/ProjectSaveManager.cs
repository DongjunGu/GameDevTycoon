using UnityEngine;
using BackEnd;
using LitJson;

public enum ProjectStage { None, Developing, BugFixing, Complete }

public class ProjectSaveManager : MonoBehaviour
{
    public static ProjectSaveManager Instance { get; private set; }

    private string _rowInDate = null;

    // ── 복원 대기 데이터 ──────────────────────
    private bool            _hasPendingRestore;
    private ProjectStage    _loadedStage;
    private ProjectScale    _loadedScale;
    private ProjectGenre    _loadedGenre;
    private ProjectPlatform _loadedPlatform;
    private float           _loadedElapsed;
    private bool            _loadedTriggered25;
    private bool            _loadedTriggered75;
    private string          _loadedPlannerLeaderId;
    private string          _loadedProgrammerLeaderId;
    private string          _loadedArtistLeaderId;
    private float           _loadedAccumPlanning;
    private float           _loadedAccumDevelop;
    private float           _loadedAccumArt;
    private float           _loadedAccumBug;
    private float           _loadedAccumCreativity;
    private int             _loadedCurrentGenreIndex;
    private float           _loadedNextGenreTick;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 ──────────────────────────────────
    public void SaveProject()
    {
        var dm = DevelopmentManager.Instance;
        var dp = DevelopmentPanelUI.Instance;

        if (dm.CurrentStage == ProjectStage.None)
        {
            Debug.Log("저장 스킵: 진행 중인 프로젝트 없음");
            return;
        }

        var param = new Param();

        param.Add("isInProgress",      dm.IsStarted);
        param.Add("stage",             (int)GetCurrentStage());
        param.Add("scale",             (int)ProjectSetupUI.SelectedScale);
        param.Add("genre",             (int)ProjectSetupUI.SelectedGenre);
        param.Add("platform",          (int)ProjectSetupUI.SelectedPlatform);
        param.Add("elapsed",           dm.GetElapsed());
        param.Add("triggered25",       dm.IsTriggered25);
        param.Add("triggered75",       dm.IsTriggered75);
        param.Add("plannerLeaderId",    dm.plannerLeader    != null ? dm.plannerLeader.id    : "");
        param.Add("programmerLeaderId", dm.programmerLeader != null ? dm.programmerLeader.id : "");
        param.Add("artistLeaderId",     dm.artistLeader     != null ? dm.artistLeader.id     : "");
        param.Add("accumPlanning",     dp.GetPlanning());
        param.Add("accumDevelop",      dp.GetDevelop());
        param.Add("accumArt",          dp.GetArt());
        param.Add("accumBug",          dp.GetBug());
        param.Add("accumCreativity",   dp.GetCreativity());
        param.Add("currentGenreIndex", dm.CurrentGenreIndex);
        param.Add("nextGenreTick",     dm.NextGenreTick);

        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Backend.GameData.UpdateV2("UserProject", _rowInDate, Backend.UserInDate, param, bro =>
            {
                if (bro.IsSuccess())
                    Debug.Log("프로젝트 저장 완료");
                else
                    Debug.LogError($"프로젝트 저장 실패: {bro}");
            });
        }
        else
        {
            Backend.GameData.Insert("UserProject", param, bro =>
            {
                if (bro.IsSuccess())
                {
                    _rowInDate = bro.GetInDate();
                    Debug.Log("프로젝트 Insert 완료");
                }
                else
                {
                    Debug.LogError($"프로젝트 Insert 실패: {bro}");
                }
            });
        }
    }

    // ── 로드 (씬 전, 데이터만 파싱) ──────────
    public void LoadProject(System.Action onComplete = null)
    {
        Backend.GameData.GetMyData("UserProject", new Where(), bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogError($"프로젝트 로드 실패: {bro}");
                onComplete?.Invoke();
                return;
            }

            var rows = bro.FlattenRows();
            if (rows.Count == 0)
            {
                Debug.Log("저장된 프로젝트 없음");
                onComplete?.Invoke();
                return;
            }

            JsonData row = rows[rows.Count - 1];
            _rowInDate = SafeString(row, "inDate", "");

            bool isInProgress = SafeBool(row, "isInProgress", false);
            if (!isInProgress)
            {
                Debug.Log("진행 중인 프로젝트 없음");
                onComplete?.Invoke();
                return;
            }

            // 데이터 파싱만 → 복원은 RestoreIfNeeded()에서
            _loadedStage             = (ProjectStage)SafeInt(row, "stage",    0);
            _loadedScale             = (ProjectScale)SafeInt(row, "scale",    0);
            _loadedGenre             = (ProjectGenre)SafeInt(row, "genre",    0);
            _loadedPlatform          = (ProjectPlatform)SafeInt(row, "platform", 0);
            _loadedElapsed           = SafeFloat(row, "elapsed",     0f);
            _loadedTriggered25       = SafeBool(row,  "triggered25", false);
            _loadedTriggered75       = SafeBool(row,  "triggered75", false);
            _loadedPlannerLeaderId   = SafeString(row, "plannerLeaderId",    "");
            _loadedProgrammerLeaderId= SafeString(row, "programmerLeaderId", "");
            _loadedArtistLeaderId    = SafeString(row, "artistLeaderId",     "");
            _loadedAccumPlanning     = SafeFloat(row, "accumPlanning",   0f);
            _loadedAccumDevelop      = SafeFloat(row, "accumDevelop",    0f);
            _loadedAccumArt          = SafeFloat(row, "accumArt",        0f);
            _loadedAccumBug          = SafeFloat(row, "accumBug",        0f);
            _loadedAccumCreativity   = SafeFloat(row, "accumCreativity", 0f);
            _loadedCurrentGenreIndex = SafeInt(row,   "currentGenreIndex", 0);
            _loadedNextGenreTick     = SafeFloat(row,  "nextGenreTick",    0f);
            _hasPendingRestore       = true;

            Debug.Log($"프로젝트 로드 완료: stage={_loadedStage} elapsed={_loadedElapsed:F1}");
            onComplete?.Invoke();
        });
    }

    // ── 씬 로드 후 복원 ───────────────────────
    public void RestoreIfNeeded()
    {
        if (!_hasPendingRestore) return;
        _hasPendingRestore = false;

        ProjectSetupUI.SelectedScale    = _loadedScale;
        ProjectSetupUI.SelectedGenre    = _loadedGenre;
        ProjectSetupUI.SelectedPlatform = _loadedPlatform;

        DevelopmentManager.Instance.RestoreState(
            _loadedElapsed, _loadedTriggered25, _loadedTriggered75,
            _loadedPlannerLeaderId, _loadedProgrammerLeaderId, _loadedArtistLeaderId,
            _loadedAccumPlanning, _loadedAccumDevelop, _loadedAccumArt,
            _loadedAccumBug, _loadedAccumCreativity,
            _loadedCurrentGenreIndex, _loadedNextGenreTick,
            _loadedStage
        );

        Debug.Log($"프로젝트 복원 실행: stage={_loadedStage}");
    }

    // ── 스테이지 판별 ─────────────────────────
    ProjectStage GetCurrentStage()
    {
        if (!DevelopmentManager.Instance.IsStarted) return ProjectStage.None;
        return DevelopmentManager.Instance.CurrentStage;
    }

    // ── 헬퍼 ──────────────────────────────────
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