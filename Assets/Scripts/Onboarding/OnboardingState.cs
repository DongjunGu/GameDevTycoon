using UnityEngine;

// 첫 게스트 온보딩(컷씬 + 게임씬 비서 튜토리얼) 1회 게이트.
// 로컬(PlayerPrefs) 저장 — DialogManager 의 triggerOnce 와 동일한 기기 단위 방식.
// (테스터 PC 마다 1회씩 노출. 뒤끝 계정 단위가 필요하면 추후 서버 플래그로 교체.)
public static class OnboardingState
{
    const string KEY_INTRO      = "onboarding_intro_done";      // 컷씬 + 첫 런 진입 완료
    const string KEY_TUTORIAL   = "onboarding_tutorial_done";   // 게임씬 비서 튜토리얼 완료
    const string KEY_FIRST_HIRE = "onboarding_first_hire_done"; // 첫 채용(온보딩) 완료 — 1회만 1주 면접
    const string KEY_PROJ_TUT   = "onboarding_project_tutorial_done";    // 프로젝트 튜토리얼 완료
    const string KEY_PROJ_PEND  = "onboarding_project_tutorial_pending"; // 프로젝트 튜토리얼 카운트다운(-1 없음/0 실행대기/>0 남은주차)
    const string KEY_TUT3       = "onboarding_tutorial3_done";  // 튜토리얼 3-1(ConfirmHirePanel 첫 노출) 완료
    const string KEY_TUT5       = "onboarding_tutorial5_done";  // 튜토리얼 5-1~5-4 완료
    // 5-1~5-4 무장(진행 중, 아직 미완료) 여부 — 이 구간은 3-1~4-2와 달리 직원 2명이 이미 서버에 커밋된
    // 뒤라 재접속해도 게임 상태가 자연히 리셋되지 않는다. pending 없이는 완료 전 재접속 시 통째로 유실됨.
    const string KEY_TUT5_PEND  = "onboarding_tutorial5_pending";
    // 튜토리얼 6-1~6-3 완료 — 기획팀장 선택(DispatchPanelUI, Planner). pending 불필요: DevelopmentManager가
    // pendingLeaderSelect 를 자체 영속화해 재접속 시 이 패널을 자연히 다시 열어주므로(3-1~4-2와 동일한 이유),
    // Tutorial6Done 이 false인 동안엔 패널이 열릴 때마다 매번 6-1부터 재생하면 된다.
    const string KEY_TUT6       = "onboarding_tutorial6_done";
    // 튜토리얼 7-1~7-6 완료 — 첫 기획팀장 점수 화면(DevelopmentManager.BuildAndShowLeaderScore, Planner).
    // pending 불필요: 6단계와 동일한 이유(재접속 재개도 BuildAndShowLeaderScore를 다시 타므로 자연 재생).
    const string KEY_TUT7       = "onboarding_tutorial7_done";

    // 튜토리얼 dim 이 떠 있는 동안 true (세션 전용, 저장 안 함).
    // 시간 정지 중에도 메뉴 버튼을 숨기지 않도록 MenuController 가 참조.
    public static bool TutorialActive { get; set; }

    public static bool IntroDone => PlayerPrefs.GetInt(KEY_INTRO, 0) == 1;
    public static void MarkIntroDone()
    {
        PlayerPrefs.SetInt(KEY_INTRO, 1);
        PlayerPrefs.Save();
    }

    public static bool TutorialDone => PlayerPrefs.GetInt(KEY_TUTORIAL, 0) == 1;
    public static void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(KEY_TUTORIAL, 1);
        PlayerPrefs.Save();
    }

    // 첫 채용(온보딩) — 면접 대기를 3주가 아닌 1주로 단축, 1회만.
    public static bool FirstHireDone => PlayerPrefs.GetInt(KEY_FIRST_HIRE, 0) == 1;
    public static void MarkFirstHireDone()
    {
        PlayerPrefs.SetInt(KEY_FIRST_HIRE, 1);
        PlayerPrefs.Save();
    }

    // 프로젝트 튜토리얼(온보딩) — 첫 직원 획득 후 1주 뒤 1회. pending: -1 없음 / 0 실행대기 / >0 남은 주차.
    public static bool ProjectTutorialDone => PlayerPrefs.GetInt(KEY_PROJ_TUT, 0) == 1;
    public static void MarkProjectTutorialDone()
    {
        PlayerPrefs.SetInt(KEY_PROJ_TUT, 1);
        PlayerPrefs.SetInt(KEY_PROJ_PEND, -1);
        PlayerPrefs.Save();
    }

    public static int ProjectTutorialPending => PlayerPrefs.GetInt(KEY_PROJ_PEND, -1);
    public static void SetProjectTutorialPending(int weeks)
    {
        PlayerPrefs.SetInt(KEY_PROJ_PEND, weeks);
        PlayerPrefs.Save();
    }

    // 직원 획득 시 호출 — 아직 안 했고 무장 안 됐으면 weeks 주 카운트다운 시작.
    public static void ArmProjectTutorial(int weeks)
    {
        if (ProjectTutorialDone || ProjectTutorialPending >= 0) return;
        SetProjectTutorialPending(weeks);
    }

    // 튜토리얼 3-1 — 첫 ConfirmHirePanel 노출 시 1회. 버튼 강조 없이 TutorialPanel 대사만 재생.
    public static bool Tutorial3Done => PlayerPrefs.GetInt(KEY_TUT3, 0) == 1;
    public static void MarkTutorial3Done()
    {
        PlayerPrefs.SetInt(KEY_TUT3, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 5-1~5-4 — 두 번째(진짜) 채용이 확정되고 2초 뒤 시작, 프로젝트 개발 시작 확정까지.
    public static bool Tutorial5Done => PlayerPrefs.GetInt(KEY_TUT5, 0) == 1;
    public static void MarkTutorial5Done()
    {
        PlayerPrefs.SetInt(KEY_TUT5, 1);
        PlayerPrefs.DeleteKey(KEY_TUT5_PEND); // 완료됐으니 무장 해제(이미 done 체크로 걸러지지만 위생상 정리)
        PlayerPrefs.Save();
    }

    // 두 번째(진짜) 채용 확정 시 HiringUI.DoHire가 호출 — 5-1 재생 시작 "전"에 무장해 둬야 그 사이에
    // 재접속해도(2초 지연 포함) 다음 진입 시 TutorialController.Start()가 5-1부터 재개할 수 있다.
    public static bool Tutorial5Pending => PlayerPrefs.GetInt(KEY_TUT5_PEND, 0) == 1;
    public static void ArmTutorial5()
    {
        if (Tutorial5Done) return;
        PlayerPrefs.SetInt(KEY_TUT5_PEND, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 6-1~6-3 — 기획팀장 선택 패널(DispatchPanelUI, Planner)이 처음 열릴 때(재접속 시 자연
    // 재오픈 포함) 마다 재생하다가, 실제로 팀장을 확정하면 1회만 마크.
    public static bool Tutorial6Done => PlayerPrefs.GetInt(KEY_TUT6, 0) == 1;
    public static void MarkTutorial6Done()
    {
        PlayerPrefs.SetInt(KEY_TUT6, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼 7-1~7-6 — 첫 기획팀장 점수 화면이 열릴 때마다(재접속 자연 재오픈 포함) 재생하다가,
    // 4회차(burst)까지 연출이 끝나면 1회만 마크.
    public static bool Tutorial7Done => PlayerPrefs.GetInt(KEY_TUT7, 0) == 1;
    public static void MarkTutorial7Done()
    {
        PlayerPrefs.SetInt(KEY_TUT7, 1);
        PlayerPrefs.Save();
    }

    // 테스트용 — 온보딩 재노출 (TestResetBtn 등에서 빌드에서도 호출 가능하도록 UNITY_EDITOR 가드 제거)
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY_INTRO);
        PlayerPrefs.DeleteKey(KEY_TUTORIAL);
        PlayerPrefs.DeleteKey(KEY_FIRST_HIRE);
        PlayerPrefs.DeleteKey(KEY_PROJ_TUT);
        PlayerPrefs.DeleteKey(KEY_PROJ_PEND);
        PlayerPrefs.DeleteKey(KEY_TUT3);
        PlayerPrefs.DeleteKey(KEY_TUT5);
        PlayerPrefs.DeleteKey(KEY_TUT5_PEND);
        PlayerPrefs.DeleteKey(KEY_TUT6);
        PlayerPrefs.DeleteKey(KEY_TUT7);
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] 플래그 리셋 — 다음 진입 시 컷씬+튜토리얼 재노출");
    }
}
