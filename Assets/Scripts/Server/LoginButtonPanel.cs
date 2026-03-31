using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// iOS 전용 로그인 버튼 패널.
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

    // 추후 Google 버튼 추가 시
    // void OnClickGoogle()
    // {
    //     googleLoginButton.interactable = false;
    //     FindAnyObjectByType<GoogleLogin>().StartLogin();
    // }
}
