using UnityEngine;

// 첫 게스트 온보딩(컷씬 + 게임씬 비서 튜토리얼) 1회 게이트.
// 로컬(PlayerPrefs) 저장 — DialogManager 의 triggerOnce 와 동일한 기기 단위 방식.
// (테스터 PC 마다 1회씩 노출. 뒤끝 계정 단위가 필요하면 추후 서버 플래그로 교체.)
public static class OnboardingState
{
    const string KEY_INTRO    = "onboarding_intro_done";    // 컷씬 + 첫 런 진입 완료
    const string KEY_TUTORIAL = "onboarding_tutorial_done"; // 게임씬 비서 튜토리얼 완료

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

#if UNITY_EDITOR
    // 테스트용 — 온보딩 재노출
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY_INTRO);
        PlayerPrefs.DeleteKey(KEY_TUTORIAL);
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] 플래그 리셋 — 다음 진입 시 컷씬+튜토리얼 재노출");
    }
#endif
}
