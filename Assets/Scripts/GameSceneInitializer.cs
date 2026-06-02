using UnityEngine;

public class GameSceneInitializer : MonoBehaviour
{
    [Header("이 씬에서만 재생할 BGM (GameScene 전용)")]
    [SerializeField] private SoundData gameBgm;

    void Start()
    {
        // GameScene 진입 시 BGM 시작. SoundManager 는 영속이지만 OnDestroy 에서 멈추므로 이 씬에서만 흐른다.
        if (gameBgm != null) SoundManager.Instance?.Play(gameBgm);

        var dialogUI = FindAnyObjectByType<DialogUI>();
        if (dialogUI != null)
            DialogManager.Instance.SetDialogUI(dialogUI);

        // CEO 인스턴스 셋업 (메타 CEOManager 의 강화 단계 기준 능력치 계산해서 메모리 EmployeeData 생성)
        EmployeeManager.Instance?.ApplyCEOFromManager();

        // StartTime을 먼저 호출해야 RestoreState 내부에서 StopTime()으로 상쇄 가능
        GameTimeManager.Instance.StartTime();

        SalesSaveManager.Instance.RestoreIfNeeded();
        ProjectSaveManager.Instance.RestoreIfNeeded();

        HUDUI.Instance?.RefreshAll();

        OfficeManager.Instance?.RestoreEmployees();

        var stage = DevelopmentManager.Instance.CurrentStage;
        if (stage == ProjectStage.Developing || stage == ProjectStage.BugFixing)
            GameTimeManager.Instance.SetProjectSpeed(ProjectSetupUI.SelectedScale);

        //         // 게임 시작 다이얼로그 (첫 시작 시)
        // if (DialogManager.Instance.HasGroup("event_game_start"))
        //     EventDialogTable.PlayManual("event_game_start");
    }

    void OnDestroy()
    {
        // GameScene 을 벗어날 때(아웃게임 복귀 등) BGM 정지 → GameScene 에서만 들리게.
        if (gameBgm != null) SoundManager.Instance?.StopBGM();
    }

    public void TestGameStartDialog()
    {
        EventDialogTable.PlayManual("event_game_start");
    }
    public void TestProjectResult()
    {
        EventDialogTable.PlayManual("event_project_complete");
    }

    public void TestFirstHireDialog()
    {
        EventDialogTable.PlayManual("event_first_hire");
    }
}