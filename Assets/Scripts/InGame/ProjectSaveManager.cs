using UnityEngine;
using BackEnd;
using LitJson;

public enum ProjectStage { None, Developing, BugFixing, Marketing, Sales, Complete }

public class ProjectSaveManager : MonoBehaviour
{
    public static ProjectSaveManager Instance { get; private set; }

    private string _rowInDate = null;

    // ── 복원 대기 데이터 ──────────────────────
    private bool _hasPendingRestore;
    private float _savedQualityScore;
    private string _savedProjectName = "프로젝트명";
    private float _savedPlanning;
    private float _savedDevelop;
    private float _savedArt;
    private float _savedCreativity;
    private float _savedBug;

    private float _loadedPlanning;
    private float _loadedDevelop;
    private float _loadedArt;
    private float _loadedCreativity;
    private float _loadedBug;
    private ProjectStage _loadedStage;
    private ProjectScale _loadedScale;
    private ProjectGenre _loadedGenre;
    private ProjectPlatform _loadedPlatform;
    private float _loadedElapsed;
    private bool _loadedTriggered25;
    private bool _loadedTriggered75;
    private string _loadedPlannerLeaderId;
    private string _loadedProgrammerLeaderId;
    private string _loadedArtistLeaderId;
    private float _loadedAccumPlanning;
    private float _loadedAccumDevelop;
    private float _loadedAccumArt;
    private float _loadedAccumBug;
    private float _loadedAccumCreativity;
    private float _loadedQualityScore;
    private ProjectScale _loadedSalesScale;
    private int _loadedGenrePopularity;
    private int _loadedGenreFatigue;
    private bool _loadedInvestmentAccepted;
    private string _loadedInvestmentStat;
    private string _loadedInvestmentStatName;
    private int _loadedTickSeed;
    private string _loadedTickIndices;
    private string _loadedMidDevData;
    private string _loadedScheduledEvents = "";
    private int    _loadedScheduledNextIndex;
    private float  _loadedDevDuration;
    private float _loadedNetworkSlowEndElapsed;
    private float _loadedProgOffsetElapsedAtEvent;
    private float _loadedProgOffsetExtension;
    private float _loadedProgVisualOffset;
    private bool   _loadedPendingLeaderScore25;
    private bool   _loadedPendingLeaderScore75;
    private string _loadedPendingEventData = "";

    private string _loadedProjectName = "프로젝트명";
    public string GetLoadedProjectName() => _loadedProjectName;
    private ProjectScale _savedSalesScale;

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

        if (dm.CurrentStage == ProjectStage.None || dm.CurrentStage == ProjectStage.Sales)
        {
            Debug.Log("저장 스킵: 진행 중인 프로젝트 없음 또는 판매 중");
            return;
        }

        var param = new Param();
        param.Add("isInProgress", dm.CurrentStage != ProjectStage.Complete && dm.CurrentStage != ProjectStage.None);
        param.Add("stage", (int)GetCurrentStage());
        param.Add("scale", (int)ProjectSetupUI.SelectedScale);
        param.Add("genre", (int)ProjectSetupUI.SelectedGenre);
        param.Add("platform", (int)ProjectSetupUI.SelectedPlatform);
        param.Add("qualityScore", _savedQualityScore);
        param.Add("salesScale", (int)_savedSalesScale);
        param.Add("elapsed", dm.GetElapsed());
        param.Add("triggered25", dm.IsTriggered25);
        param.Add("triggered75", dm.IsTriggered75);
        param.Add("plannerLeaderId", dm.plannerLeader != null ? dm.plannerLeader.id : "");
        param.Add("programmerLeaderId", dm.programmerLeader != null ? dm.programmerLeader.id : "");
        param.Add("artistLeaderId", dm.artistLeader != null ? dm.artistLeader.id : "");
        param.Add("accumPlanning", dp.GetPlanning());
        param.Add("accumDevelop", dp.GetDevelop());
        param.Add("accumArt", dp.GetArt());
        param.Add("accumBug", dp.GetBug());
        param.Add("accumCreativity", dp.GetCreativity());
        param.Add("projectName", _savedProjectName);
        param.Add("savedPlanning", _savedPlanning);
        param.Add("savedDevelop", _savedDevelop);
        param.Add("savedArt", _savedArt);
        param.Add("savedCreativity", _savedCreativity);
        param.Add("savedBug", _savedBug);
        param.Add("genrePopularity", ProjectSetupUI.SelectedGenrePopularity);
        param.Add("genreFatigue", ProjectSetupUI.SelectedGenreFatigue);
        param.Add("investmentAccepted", RandomEventManager.Instance.InvestmentAccepted);
        param.Add("investmentStat", RandomEventManager.Instance.InvestmentStat);
        param.Add("investmentStatName", RandomEventManager.Instance.InvestmentStatName);
        param.Add("tickSeed", dm.GetTickSeed());
        param.Add("tickIndices", dm.GetTickIndices());
        param.Add("midDevData", dm.GetMidDevData());
        param.Add("scheduledEvents", RandomEventManager.Instance.GetScheduledEventsString());
        param.Add("scheduledNextIndex", RandomEventManager.Instance.GetNextScheduledIndex());
        param.Add("devDuration", dm.developmentDuration);
        param.Add("networkSlowEndElapsed", dm.GetNetworkSlowEndElapsed());
        param.Add("progOffsetElapsedAtEvent", dm.GetProgressOffsetElapsedAtEvent());
        param.Add("progOffsetExtension", dm.GetProgressOffsetExtension());
        param.Add("progVisualOffset", dm.GetProgressVisualOffset());
        param.Add("pendingLeaderScore25", dm.IsPendingLeaderScore25);
        param.Add("pendingLeaderScore75", dm.IsPendingLeaderScore75);
        param.Add("pendingEventData", RandomEventManager.Instance.GetPendingEventData());

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
        BackendRetry.Instance.GetMyData("UserProject", bro =>
        {
            if (!bro.IsSuccess())
            {
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
            _loadedStage = (ProjectStage)SafeInt(row, "stage", 0);
            _loadedScale = (ProjectScale)SafeInt(row, "scale", 0);
            _loadedGenre = (ProjectGenre)SafeInt(row, "genre", 0);
            _loadedPlatform = (ProjectPlatform)SafeInt(row, "platform", 0);
            _loadedElapsed = SafeFloat(row, "elapsed", 0f);
            _loadedTriggered25 = SafeBool(row, "triggered25", false);
            _loadedTriggered75 = SafeBool(row, "triggered75", false);
            _loadedPlannerLeaderId = SafeString(row, "plannerLeaderId", "");
            _loadedProgrammerLeaderId = SafeString(row, "programmerLeaderId", "");
            _loadedArtistLeaderId = SafeString(row, "artistLeaderId", "");
            _loadedAccumPlanning = SafeFloat(row, "accumPlanning", 0f);
            _loadedAccumDevelop = SafeFloat(row, "accumDevelop", 0f);
            _loadedAccumArt = SafeFloat(row, "accumArt", 0f);
            _loadedAccumBug = SafeFloat(row, "accumBug", 0f);
            _loadedAccumCreativity = SafeFloat(row, "accumCreativity", 0f);
            _loadedQualityScore = SafeFloat(row, "qualityScore", 0f);
            _loadedSalesScale = (ProjectScale)SafeInt(row, "salesScale", 0);
            _loadedGenrePopularity = SafeInt(row, "genrePopularity", 1);
            _loadedGenreFatigue = SafeInt(row, "genreFatigue", 0);
            _loadedProjectName = SafeString(row, "projectName", "프로젝트명");
            _loadedPlanning = SafeFloat(row, "savedPlanning", 0f);
            _loadedDevelop = SafeFloat(row, "savedDevelop", 0f);
            _loadedArt = SafeFloat(row, "savedArt", 0f);
            _loadedCreativity = SafeFloat(row, "savedCreativity", 0f);
            _loadedBug = SafeFloat(row, "savedBug", 0f);
            _hasPendingRestore = true;
            _loadedInvestmentAccepted = SafeBool(row, "investmentAccepted", false);
            _loadedInvestmentStat = SafeString(row, "investmentStat", "");
            _loadedInvestmentStatName = SafeString(row, "investmentStatName", "");
            _loadedTickSeed = SafeInt(row, "tickSeed", 0);
            _loadedTickIndices = SafeString(row, "tickIndices", "");
            _loadedMidDevData = SafeString(row, "midDevData", "");
            _loadedScheduledEvents    = SafeString(row, "scheduledEvents", "");
            _loadedScheduledNextIndex = SafeInt(row, "scheduledNextIndex", 0);
            _loadedDevDuration = SafeFloat(row, "devDuration", 0f);
            _loadedNetworkSlowEndElapsed = SafeFloat(row, "networkSlowEndElapsed", 0f);
            _loadedProgOffsetElapsedAtEvent = SafeFloat(row, "progOffsetElapsedAtEvent", 0f);
            _loadedProgOffsetExtension = SafeFloat(row, "progOffsetExtension", 0f);
            _loadedProgVisualOffset = SafeFloat(row, "progVisualOffset", 0f);
            _loadedPendingLeaderScore25 = SafeBool(row, "pendingLeaderScore25", false);
            _loadedPendingLeaderScore75 = SafeBool(row, "pendingLeaderScore75", false);
            _loadedPendingEventData     = SafeString(row, "pendingEventData", "");

            Debug.Log($"프로젝트 로드 완료: stage={_loadedStage} elapsed={_loadedElapsed:F1}");
            onComplete?.Invoke();
        });
    }

    // ── 씬 로드 후 복원 ───────────────────────
    public void RestoreIfNeeded()
    {
        if (!_hasPendingRestore) return;
        _hasPendingRestore = false;

        ProjectSetupUI.SelectedScale = _loadedScale;
        ProjectSetupUI.SelectedGenre = _loadedGenre;
        ProjectSetupUI.SelectedPlatform = _loadedPlatform;
        ProjectSetupUI.SelectedGenrePopularity = _loadedGenrePopularity;
        ProjectSetupUI.SelectedGenreFatigue = _loadedGenreFatigue;

        // ── RestoreState 먼저 (SetValues 호출됨) ──
        DevelopmentManager.Instance.RestoreState(
            _loadedElapsed, _loadedTriggered25, _loadedTriggered75,
            _loadedPlannerLeaderId, _loadedProgrammerLeaderId, _loadedArtistLeaderId,
            _loadedAccumPlanning, _loadedAccumDevelop, _loadedAccumArt,
            _loadedAccumBug, _loadedAccumCreativity,
            _loadedStage,
            _loadedTickSeed, _loadedTickIndices, _loadedMidDevData,
            _loadedDevDuration, _loadedNetworkSlowEndElapsed,
            _loadedProgOffsetElapsedAtEvent, _loadedProgOffsetExtension, _loadedProgVisualOffset,
            _loadedPendingLeaderScore25, _loadedPendingLeaderScore75
        );

        // ── RandomEvent 상태 복원 ──
        // InitEvents()가 RestoreState() 내부에서 이미 호출되어 풀이 구성된 상태
        RandomEventManager.Instance.RestoreSchedule(_loadedScheduledEvents, _loadedScheduledNextIndex);
        RandomEventManager.Instance.RestorePendingEventFromSave(_loadedPendingEventData);
        RandomEventManager.Instance.InvestmentAccepted = _loadedInvestmentAccepted;
        RandomEventManager.Instance.InvestmentStat = _loadedInvestmentStat;
        RandomEventManager.Instance.InvestmentStatName = _loadedInvestmentStatName;
        if (_loadedInvestmentAccepted && !string.IsNullOrEmpty(_loadedInvestmentStatName))
        {
            float currentValue = RandomEventManager.Instance.InvestmentStat switch
            {
                "planning" => DevelopmentPanelUI.Instance.GetPlanning(),
                "develop" => DevelopmentPanelUI.Instance.GetDevelop(),
                "art" => DevelopmentPanelUI.Instance.GetArt(),
                "creativity" => DevelopmentPanelUI.Instance.GetCreativity(),
                _ => 0f
            };

            InvestmentProgressUI.Instance?.Show(
                _loadedInvestmentStatName,
                RandomEventManager.Instance.investmentThreshold,
                currentValue
            );
        }

        // ── Marketing/Sales: 프로젝트 초기화 (Sales는 SalesSaveManager가 복원) ──
        if (_loadedStage == ProjectStage.Marketing || _loadedStage == ProjectStage.Sales)
        {
            Debug.Log($"[ProjectSaveManager] RestoreIfNeeded: stage={_loadedStage}");
            DevelopmentManager.Instance.ResetProject();

            // Marketing 단계에서 껐을 때 (SalesUI 열리기 전) → 직접 복원
            // 단, SalesSaveManager에 이미 활성 세이브가 있으면 그쪽에서 처리
            bool salesAlreadyHandled = SalesSaveManager.Instance != null &&
                (SalesSaveManager.Instance.HasPendingRestore || SalesSaveManager.Instance.WasRestored);
            if (_loadedStage == ProjectStage.Marketing && _loadedQualityScore > 0f && !salesAlreadyHandled)
            {
                AlertUI.Instance.Show("판매 시작!", () =>
                {
                    SalesUI.Instance.ShowWithProjectName(
                        _loadedQualityScore, _loadedSalesScale, _loadedProjectName,
                        _loadedScale, _loadedGenre, _loadedPlatform,
                        _loadedPlanning, _loadedDevelop, _loadedArt,
                        _loadedCreativity, _loadedBug
                    );
                });
            }
            // Sales 단계는 SalesSaveManager.RestoreIfNeeded()에서 처리
            return;
        }

        Debug.Log($"프로젝트 복원 실행: stage={_loadedStage}");
    }

    public void SetQualityScore(float quality, ProjectScale scale)
    {
        _savedQualityScore = quality;
        _savedSalesScale = scale;
        _savedPlanning = DevelopmentResultUI.Instance.LastPlanning;
        _savedDevelop = DevelopmentResultUI.Instance.LastDevelop;
        _savedArt = DevelopmentResultUI.Instance.LastArt;
        _savedCreativity = DevelopmentResultUI.Instance.LastCreativity;
        _savedBug = DevelopmentResultUI.Instance.LastBug;
    }
    public void SetProjectName(string name)
    {
        _savedProjectName = name;
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
