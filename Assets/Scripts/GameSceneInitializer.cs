using UnityEngine;

public class GameSceneInitializer : MonoBehaviour
{
    void Start()
    {
        var dialogUI = FindAnyObjectByType<DialogUI>();
        if (dialogUI != null)
            DialogManager.Instance.SetDialogUI(dialogUI);
        ProjectSaveManager.Instance.RestoreIfNeeded();

        HUDUI.Instance?.RefreshAll();

        OfficeManager.Instance?.RestoreEmployees();

        //         // 게임 시작 다이얼로그 (첫 시작 시)
        // if (DialogManager.Instance.HasGroup("event_game_start"))
        //     EventDialogTable.PlayManual("event_game_start");
    }

    public void TestStartNegotiation()
    {
        SalaryNegotiationManager.Instance.StartNegotiation();
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