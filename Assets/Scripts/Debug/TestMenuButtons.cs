using UnityEngine;
using UnityEngine.SceneManagement;

// 관리 서브메뉴의 테스트 전용 버튼들(TestResetBtn / NextLevelBtn) OnClick 진입점.
public class TestMenuButtons : MonoBehaviour
{
    // TestResetBtn — 온보딩 플래그 초기화 후 LoadingScene부터 다시 시작 → 컷씬 + 게임씬 튜토리얼 재노출.
    public void OnClickTestReset()
    {
        // GameTimeManager는 DontDestroyOnLoad라 씬을 넘어가도 안 죽고 계속 틱을 돈다 — 컷씬/새 런 초기화가
        // 끝날 때까지(NewRunInitializer.StartNewRun이 리셋하기 전까지) 켜져있으면 안 되므로 여기서 먼저 정지.
        GameTimeManager.Instance?.StopTime();
        OnboardingState.ResetAll();
        SceneManager.LoadScene("LoadingScene");
    }

    [Header("NextLevelBtn")]
    public CameraZoomController cameraZoomController; // Main Camera의 CameraZoomController
    public GameObject[] panelsToDeactivate;
    public GameObject[] panelsToActivate;

    // NextLevelBtn — CameraZoomController 활성화 + 지정된 패널 전환.
    public void OnClickNextLevel()
    {
        if (cameraZoomController != null) cameraZoomController.enabled = true;

        if (panelsToDeactivate != null)
            foreach (var p in panelsToDeactivate) if (p != null) p.SetActive(false);

        if (panelsToActivate != null)
            foreach (var p in panelsToActivate) if (p != null) p.SetActive(true);
    }
}
