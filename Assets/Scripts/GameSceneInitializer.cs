using UnityEngine;

public class GameSceneInitializer : MonoBehaviour
{
    void Start()
    {
        var dialogUI = FindAnyObjectByType<DialogUI>();
        if (dialogUI != null)
            DialogManager.Instance.SetDialogUI(dialogUI);

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