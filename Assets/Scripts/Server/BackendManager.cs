using UnityEngine;
using BackEnd;

public class BackendManager : MonoBehaviour
{
    void Start()
    {
        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("초기화 성공 : " + bro);

#if UNITY_EDITOR
            // 에디터에서는 커스텀 로그인으로 테스트
            TestLogin();
#else
            // 실제 빌드에서는 GPGS 로그인
            FindAnyObjectByType<GPGSLogin>().StartLogin();
#endif
        }
        else
        {
            Debug.LogError("초기화 실패 : " + bro);
        }
    }

    void TestLogin()
    {
        var bro = Backend.BMember.CustomLogin("testuser", "1234");

        if (bro.IsSuccess())
        {
            Debug.Log("테스트 로그인 성공: " + bro.GetInDate());
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }
        else
        {
            // 없는 계정이면 가입
            var signUp = Backend.BMember.CustomSignUp("testuser", "1234");
            if (signUp.IsSuccess())
            {
                Debug.Log("테스트 계정 가입 성공");
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            }
            else
            {
                Debug.LogError("테스트 로그인 실패: " + bro);
            }
        }
    }
}