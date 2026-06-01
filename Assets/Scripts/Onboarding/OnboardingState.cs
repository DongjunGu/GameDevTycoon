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

#if UNITY_EDITOR
    // 테스트용 — 온보딩 재노출
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY_INTRO);
        PlayerPrefs.DeleteKey(KEY_TUTORIAL);
        PlayerPrefs.DeleteKey(KEY_FIRST_HIRE);
        PlayerPrefs.DeleteKey(KEY_PROJ_TUT);
        PlayerPrefs.DeleteKey(KEY_PROJ_PEND);
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] 플래그 리셋 — 다음 진입 시 컷씬+튜토리얼 재노출");
    }
#endif
}
