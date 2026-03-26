using UnityEngine;
using BackEnd;
using UnityEngine.SceneManagement;

public class BackendManager : MonoBehaviour
{
    void Start()
    {
        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("초기화 성공 : " + bro);
            //FindAnyObjectByType<Progress>().Play();

#if UNITY_EDITOR
            TestLogin();
#else
            FindAnyObjectByType<GPGSLogin>().StartLogin();
#endif
        }
        else
        {
            Debug.LogError("초기화 실패 : " + bro);
        }
    }

    void LoadAllAndEnterGame()
    {
        EmployeeManager.Instance.LoadAllData(() =>
        {
            MoneyManager.Instance.LoadMoney(() =>
            {
                GameTimeManager.Instance.LoadGameTime(() =>
                {
                    QuestManager.Instance.LoadQuests(() =>
                    {
                        ProjectSaveManager.Instance.LoadProject(() =>
                        {
                            CompletedProjectManager.Instance.LoadCompletedProjects(() =>
                            {
                                LoanManager.Instance.LoadLoans(() =>
                                {
                                    TechTreeManager.Instance.LoadTechTree(() =>
                                    {
                                        DialogManager.Instance.Initialize();
                                        GameTimeManager.Instance.StartTime();
                                        //SceneManager.LoadScene("GameScene"); // ← 씬 전환
                                    });
                                });
                            });
                        });
                    });
                });
            });
        });
    }

    void TestLogin()
    {
        var bro = Backend.BMember.CustomLogin("testuser2", "3456");

        if (bro.IsSuccess())
        {
            Debug.Log("테스트 로그인 성공");
            LoadAllAndEnterGame();
        }
        else
        {
            var signUp = Backend.BMember.CustomSignUp("testuser2", "3456");
            if (signUp.IsSuccess())
            {
                Debug.Log("회원가입 성공");
                LoadAllAndEnterGame();
            }
            else
            {
                Debug.LogError($"회원가입 실패: {signUp}");
            }
        }
    }

    // GPGS 로그인 성공 후 호출 (GPGSLogin.cs에서 호출)
    public void OnLoginSuccess()
    {
        LoadAllAndEnterGame();
    }
}