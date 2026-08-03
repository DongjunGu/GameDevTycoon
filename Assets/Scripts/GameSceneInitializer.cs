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

        // 온보딩 19-1(핸드오프 대사) 완료 ~ 20(2번째 프로젝트 기획팀장점수) 완료 사이엔 시간이 계속
        // 멈춰있어야 하는데(AllowMovementWhileStopped로 직원만 계속 움직이게 둔 채), 이 상태는
        // GameTimeManager 세션 전용이라 재접속하면 바로 위 StartTime()에 의해 풀려버린다. 아직 이
        // 구간이면(20 완료 전) 다시 멈추고, 코드 곳곳의 ForceStartTime() 호출로부터도 보호되도록
        // 하드락까지 함께 건다(LockTime — GameTimeManager 참고).
        if (OnboardingState.Tutorial19Done && !OnboardingState.Tutorial20Done)
        {
            GameTimeManager.Instance.StopTime();
            GameTimeManager.Instance.LockTime();
            GameTimeManager.Instance.AllowMovementWhileStopped = true;
        }

        // 이전 세션(아웃게임 등)에서 Unregister 없이 넘어온 잔여 ModalGate 등록 정리 — 안 하면
        // 이 세션 내내 IsBlocked 가 고착돼 상인/예약 이벤트가 영영 안 뜨는 문제로 이어질 수 있음.
        ModalGate.I.ClearAll();

        SalesSaveManager.Instance.RestoreIfNeeded();
        ProjectSaveManager.Instance.RestoreIfNeeded();

        HUDUI.Instance?.RefreshAll();

        OfficeManager.Instance?.RestoreEmployees();

        var stage = DevelopmentManager.Instance.CurrentStage;
        if (stage == ProjectStage.Developing)
            GameTimeManager.Instance.SetProjectSpeed(ProjectSetupUI.SelectedScale);
        else if (stage == ProjectStage.BugFixing)
            GameTimeManager.Instance.SetDebuggingSpeed(); // 디버깅 복원 시 4초/주

        // 배경 patrol 스케줄러 재접속 동기화 — RestoreEmployees()가 이미 켰거나(무조건 EnsurePatrolScheduler
        // 호출) ProjectSaveManager.RestoreIfNeeded()가 개발 코루틴을 재개하며 이미 껐을 수 있어 순서에
        // 따라 상태가 엇갈릴 수 있다. 여기서 최종적으로 CurrentStage 기준 한 번 더 확정한다 —
        // 개발/디버깅 중이면 꺼짐(중복 호출 무해), 아니면 켜짐(=재접속 시점이 개발 중이 아니면 배경
        // patrol 재개).
        if (stage == ProjectStage.Developing || stage == ProjectStage.BugFixing)
            OfficeManager.Instance?.StopDevelopmentPatrol();
        else
            OfficeManager.Instance?.ResumeIdlePatrol();

        // 재접속 복원 — 면접 완료(주차 0)된 채로 종료했던 채용은 후보 리스트를 다시 공개.
        // (데이터 로드는 LoadingScene 에서 끝나고, HiringUI 는 GameScene 에 있으므로 여기서 트리거)
        var em = EmployeeManager.Instance;
        if (em != null && em.HiringPendingTier >= 0 && em.HiringPendingWeeks <= 0)
        {
            int tier = em.HiringPendingTier;
            var hiringUI = HiringUI.Instance != null
                ? HiringUI.Instance
                : FindAnyObjectByType<HiringUI>(FindObjectsInactive.Include);
            if (hiringUI != null) ModalGate.I.WhenFree(() => hiringUI.RevealHiring(tier));
        }

        // 재접속 복원 — 파견 5주가 끝난(주차 0) 채로 종료했던 직원은 복귀 보고 재개
        DispatchManager.Instance?.CheckReturnOnReconnect();

        // 재접속 복원 — 새해 임금/연세 차감 알림 도중 종료했으면 단계에 맞춰 재발동
        if (GameTimeManager.Instance != null && GameTimeManager.Instance.PendingNewYearStage > 0)
            ModalGate.I.WhenFree(() => GameTimeManager.Instance.ResumeNewYearPaymentOnReconnect());

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