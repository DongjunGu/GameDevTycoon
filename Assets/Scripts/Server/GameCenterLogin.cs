using System.Runtime.InteropServices;
using BackEnd;
using UnityEngine;

// 파일명은 유지하되 실제로는 Sign in with Apple 처리
// 씬의 GameObject 이름은 "AppleLogin" 으로 설정할 것
public class GameCenterLogin : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        gameObject.name = "AppleLogin"; // .mm의 GAME_OBJECT_NAME과 반드시 일치
    }

#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void _RequestAppleSignIn();
#endif

    public void StartLogin()
    {
#if UNITY_IOS
        _RequestAppleSignIn();
#else
        Debug.LogWarning("[AppleLogin] iOS 전용입니다.");
#endif
    }

    // 네이티브에서 identityToken 수신 시 호출
    public void OnTokenReceived(string identityToken)
    {
        Debug.Log("[AppleLogin] identityToken 수신");
        FindAnyObjectByType<Progress>()?.SetStep("Apple 토큰 수신 → 뒤끝 인증 시도");

        var bro = Backend.BMember.AuthorizeFederation(identityToken, FederationType.Apple);

        if (bro.IsSuccess())
        {
            Debug.Log("[AppleLogin] 뒤끝 로그인 성공: " + bro.GetInDate());
            FindAnyObjectByType<Progress>()?.SetStep("뒤끝 인증 성공 → 데이터 로드 시작");
            FindAnyObjectByType<Progress>()?.SetLoginComplete();
            var bm = FindAnyObjectByType<BackendManager>();
            if (bm == null)
            {
                Debug.LogError("[AppleLogin] BackendManager 를 찾을 수 없음 — 데이터 로드를 시작할 수 없음");
                FindAnyObjectByType<Progress>()?.SetStep("오류: BackendManager 없음");
                return;
            }
            bm.OnLoginSuccess();
        }
        else
        {
            Debug.LogError("[AppleLogin] 뒤끝 로그인 실패: " + bro);
            Debug.LogError("StatusCode: " + bro.GetStatusCode());
            Debug.LogError("ErrorCode: " + bro.GetErrorCode());
            Debug.LogError("Message: " + bro.GetMessage());
            FindAnyObjectByType<Progress>()?.SetStep($"오류: 뒤끝 인증 실패 ({bro.GetStatusCode()}/{bro.GetErrorCode()})");
        }
    }

    public void OnTokenFailed(string error)
    {
        Debug.LogError("[AppleLogin] 로그인 실패: " + error);
        FindAnyObjectByType<Progress>()?.SetStep($"오류: Apple 로그인 실패 ({error})");
    }
}
