using UnityEngine;
using BackEnd;
using LitJson;

public enum ProjectStage { None, Developing, BugFixing, Complete }

public class ProjectSaveManager : MonoBehaviour
{
    public static ProjectSaveManager Instance { get; private set; }

    private string _rowInDate = null;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 저장 ──────────────────────────────────
    public void SaveProject()
    {
        var dm  = DevelopmentManager.Instance;
        var dp  = DevelopmentPanelUI.Instance;

        var param = new Param();

        // 프로젝트 기본
        param.Add("isInProgress",      dm.IsStarted);
        param.Add("stage",             (int)GetCurrentStage());
        param.Add("scale",             (int)ProjectSetupUI.SelectedScale);
        param.Add("genre",             (int)ProjectSetupUI.SelectedGenre);
        param.Add("platform",          (int)ProjectSetupUI.SelectedPlatform);

        // 진행도
        param.Add("elapsed",           dm.GetElapsed());
        param.Add("triggered25",       dm.IsTriggered25);
        param.Add("triggered75",       dm.IsTriggered75);

        // 팀장
        param.Add("plannerLeaderId",    dm.plannerLeader    != null ? dm.plannerLeader.id    : "");
        param.Add("programmerLeaderId", dm.programmerLeader != null ? dm.programmerLeader.id : "");
        param.Add("artistLeaderId",     dm.artistLeader     != null ? dm.artistLeader.id     : "");

        // 누적 수치
        param.Add("accumPlanning",     dp.GetPlanning());
        param.Add("accumDevelop",      dp.GetDevelop());
        param.Add("accumArt",          dp.GetArt());
        param.Add("accumBug",          dp.GetBug());
        param.Add("accumCreativity",   dp.GetCreativity());

        // 장르 인기
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

    // ── 로드 ──────────────────────────────────
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

            // 프로젝트 기본
            var stage    = (ProjectStage)SafeInt(row, "stage",    0);
            var scale    = (ProjectScale)SafeInt(row, "scale",    0);
            var genre    = (ProjectGenre)SafeInt(row, "genre",    0);
            var platform = (ProjectPlatform)SafeInt(row, "platform", 0);

            ProjectSetupUI.SelectedScale    = scale;
            ProjectSetupUI.SelectedGenre    = genre;
            ProjectSetupUI.SelectedPlatform = platform;

            // 진행도
            float elapsed      = SafeFloat(row, "elapsed",     0f);
            bool  triggered25  = SafeBool(row,  "triggered25", false);
            bool  triggered75  = SafeBool(row,  "triggered75", false);

            // 팀장 ID
            string plannerLeaderId    = SafeString(row, "plannerLeaderId",    "");
            string programmerLeaderId = SafeString(row, "programmerLeaderId", "");
            string artistLeaderId     = SafeString(row, "artistLeaderId",     "");

            // 누적 수치
            float accumPlanning   = SafeFloat(row, "accumPlanning",   0f);
            float accumDevelop    = SafeFloat(row, "accumDevelop",    0f);
            float accumArt        = SafeFloat(row, "accumArt",        0f);
            float accumBug        = SafeFloat(row, "accumBug",        0f);
            float accumCreativity = SafeFloat(row, "accumCreativity", 0f);

            // 장르
            int   currentGenreIndex = SafeInt(row,   "currentGenreIndex", 0);
            float nextGenreTick     = SafeFloat(row,  "nextGenreTick",    0f);

            // DevelopmentManager에 복원
            DevelopmentManager.Instance.RestoreState(
                elapsed, triggered25, triggered75,
                plannerLeaderId, programmerLeaderId, artistLeaderId,
                accumPlanning, accumDevelop, accumArt, accumBug, accumCreativity,
                currentGenreIndex, nextGenreTick,
                stage
            );

            Debug.Log($"프로젝트 로드 완료: stage={stage} elapsed={elapsed:F1}");
            onComplete?.Invoke();
        });
    }

    // ── 스테이지 판별 ─────────────────────────
    ProjectStage GetCurrentStage()
    {
        if (!DevelopmentManager.Instance.IsStarted) return ProjectStage.None;
        // DevelopmentManager에서 현재 단계 판별
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