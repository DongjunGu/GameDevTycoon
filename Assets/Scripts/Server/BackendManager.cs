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
        var bro = Backend.BMember.CustomLogin("testuser2", "3456");

        if (bro.IsSuccess())
        {
            Debug.Log("테스트 로그인 성공");

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
                                    LoanManager.Instance.LoadLoans(() => // ← 추가
                                    {
                                        DialogManager.Instance.Initialize();
                                        GameTimeManager.Instance.StartTime();
                                        ProjectSaveManager.Instance.RestoreIfNeeded();
                                    });

                                });
                            });
                        });
                    });
                });
            });
        }
        else
        {
            var signUp = Backend.BMember.CustomSignUp("testuser2", "3456");
            if (signUp.IsSuccess())
            {
                EmployeeManager.Instance.LoadAllData(() =>
                {
                    MoneyManager.Instance.LoadMoney(() =>
                    {
                        GameTimeManager.Instance.LoadGameTime(() =>
                        {
                            QuestManager.Instance.LoadQuests(() =>
                            {
                                ProjectSaveManager.Instance.LoadProject(() => // ← 추가
                                {
                                    CompletedProjectManager.Instance.LoadCompletedProjects(() => // ← 추가
                                    {
                                        LoanManager.Instance.LoadLoans(() => // ← 추가
                                        {
                                            DialogManager.Instance.Initialize();
                                            GameTimeManager.Instance.StartTime();
                                            ProjectSaveManager.Instance.RestoreIfNeeded();
                                        });
                                    });
                                });
                            });
                        });
                    });
                });
            }
        }
    }
}