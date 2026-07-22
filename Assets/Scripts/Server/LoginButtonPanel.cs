using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// iOS 전용 로그인 버튼 패널.
/// BackendManager가 진입 시 자동으로 Apple 로그인을 시도하므로, 이 버튼은 평소엔 비활성 상태로
/// 대기하다가 자동 로그인이 실패했을 때만(GameCenterLogin.EnableRetry 경유) 눌러서 재시도하는
/// 폴백 겸 최초 유저용 수동 진입 경로다.
/// Android는 GPGS 자동 로그인이므로 비활성화.
/// 추후 Google 로그인 버튼도 여기에 추가.
/// </summary>
public class LoginButtonPanel : MonoBehaviour
{
    [Header("버튼")]
    public Button appleLoginButton;
    // public Button googleLoginButton; // 추후 추가

    void Awake()
    {
#if UNITY_IOS
        gameObject.SetActive(true);
        appleLoginButton.interactable = false; // BackendManager의 자동 로그인 시도 중 — 실패 시에만 활성화
        appleLoginButton.onClick.AddListener(OnClickApple);
#else
        gameObject.SetActive(false);
#endif
    }

    void OnClickApple()
    {
        appleLoginButton.interactable = false; // 중복 탭 방지
        FindAnyObjectByType<GameCenterLogin>().StartLogin();
    }

    // 자동 로그인 실패 시 GameCenterLogin이 호출 — 사용자가 버튼으로 재시도할 수 있게 함
    public void EnableRetry()
    {
        appleLoginButton.interactable = true;
    }

    // 추후 Google 버튼 추가 시
    // void OnClickGoogle()
    // {
    //     googleLoginButton.interactable = false;
    //     FindAnyObjectByType<GoogleLogin>().StartLogin();
    // }
}
