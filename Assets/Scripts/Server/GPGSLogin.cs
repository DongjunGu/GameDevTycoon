#if UNITY_ANDROID
using BackEnd;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

public class GPGSLogin : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    public void StartLogin()
    {
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
    }

    void ProcessAuthentication(SignInStatus status)
    {
        if (status == SignInStatus.Success)
        {
            Debug.Log("GPGS Auth Success");
            GetAccessCode();
        }
        else
        {
            Debug.LogError("GPGS Auth Failed: " + status);
        }
    }

    public void GetAccessCode()
    {
        PlayGamesPlatform.Instance.RequestServerSideAccess(
            false,
            code =>
            {
                Debug.Log("Auth Code: " + code);

                Backend.BMember.GetGPGS2AccessToken(code, googleCallback =>
                {
                    Debug.Log("GetGPGS2AccessToken Result: " + googleCallback);

                    if (!googleCallback.IsSuccess())
                    {
                        Debug.LogError("Token Exchange Failed: " + googleCallback);
                        Debug.LogError("StatusCode: " + googleCallback.GetStatusCode());
                        Debug.LogError("ErrorCode: " + googleCallback.GetErrorCode());
                        Debug.LogError("Message: " + googleCallback.GetMessage());
                        return;
                    }

                    string accessToken = googleCallback
                        .GetReturnValuetoJSON()["access_token"].ToString();

                    var bro = Backend.BMember.AuthorizeFederation(
                        accessToken, FederationType.GPGS2);
                    if (bro.IsSuccess())
                    {
                        Debug.Log("Backend Login Success! inDate: " + bro.GetInDate());
                        FindAnyObjectByType<Progress>().SetLoginComplete();
                        FindAnyObjectByType<BackendManager>().OnLoginSuccess();
                    }
                    else
                    {
                        Debug.LogError("Backend Login Failed: " + bro);
                        Debug.LogError("StatusCode: " + bro.GetStatusCode());
                        Debug.LogError("ErrorCode: " + bro.GetErrorCode());
                        Debug.LogError("Message: " + bro.GetMessage());
                    }
                });
            }
        );
    }
}
#endif