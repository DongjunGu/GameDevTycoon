using UnityEngine;
using BackEnd;
using UnityEngine.SceneManagement;

public class BackendManager : MonoBehaviour
{
    void Start()
    {
        if (BackendRetry.Instance == null)
            gameObject.AddComponent<BackendRetry>();

        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("초기화 성공 : " + bro);
            //FindAnyObjectByType<Progress>().Play();

#if UNITY_EDITOR
            TestLogin();
#elif UNITY_ANDROID
            FindAnyObjectByType<GPGSLogin>().StartLogin();
#elif UNITY_IOS
            // iOS는 LoginButtonPanel의 버튼으로 시작
#else
            // 스탠드얼론(Windows 등) — GPGS/Apple 불가 → 기기별 게스트 로그인 (테스터 PC 마다 별도 유저)
            GuestLoginAndEnter();
#endif
        }
        else
        {
            Debug.LogError("초기화 실패 : " + bro);
        }
    }

    void LoadAllAndEnterGame()
    {
        // 차트 데이터는 동기 로드 — 로그인 직후 한 번만 실행, 이후 캐시 반환
        RandomEventChartLoader.Load();
        RandomEventChoiceChartLoader.Load();
        RandomEventConditionChartLoader.Load();
        ItemChartLoader.Load();
        TechTreeChartLoader.Load();
        TraitChartLoader.Load();
        CharacterTraitChartLoader.Load();
        CharacterUniqueEventChartLoader.Load();
        RandomEventManager.Instance.InitConditionEvents();

        EmployeeManager.Instance.LoadAllData(() =>
        {
            OutGameCurrencyManager.Instance?.LoadCurrency(() =>
            {
            OutGameEmployeeManager.Instance?.LoadAsync(() =>
            {
            OwnedCardManager.Instance?.LoadAsync(() =>
            {
            OwnedTraitManager.Instance?.LoadAsync(() =>
            {
            CEOManager.Instance?.LoadAsync(() =>
            {
            StoneManager.Instance?.LoadAsync(() =>
            {
            RunStateManager.Instance?.LoadAsync(() =>
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
                                        SalesSaveManager.Instance.LoadSales(() =>
                                        {
                                            ItemManager.Instance.Load(() =>
                                            {
                                                DialogManager.Instance.Initialize();
                                                FindAnyObjectByType<Progress>()?.SetAllDataLoaded();
                                            });
                                        });
                                    });
                                });
                            });
                        });
                    });
                });
            });
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
            FindAnyObjectByType<Progress>()?.SetLoginComplete();
            LoadAllAndEnterGame();
        }
        else
        {
            var signUp = Backend.BMember.CustomSignUp("testuser3", "4567");
            if (signUp.IsSuccess())
            {
                Debug.Log("회원가입 성공");
                FindAnyObjectByType<Progress>()?.SetLoginComplete();
                LoadAllAndEnterGame();
            }
            else
            {
                Debug.LogError($"회원가입 실패: {signUp}");
            }
        }
    }

    // 스탠드얼론(Windows 등) 전용 — 뒤끝 게스트 로그인.
    // GuestLogin() 은 첫 실행 시 기기에 고유 게스트 계정을 자동 생성·로컬 저장하고, 재실행 시 같은 게스트로 로그인.
    // → 테스터 PC 마다 별도 유저로 집계. (editor 는 TestLogin 단일 계정 유지)
    void GuestLoginAndEnter()
    {
        var bro = Backend.BMember.GuestLogin();
        if (bro.IsSuccess())
        {
            Debug.Log("게스트 로그인 성공 : " + bro);
            FindAnyObjectByType<Progress>()?.SetLoginComplete();
            LoadAllAndEnterGame();
        }
        else
        {
            Debug.LogError($"게스트 로그인 실패: {bro}");
        }
    }

    // GPGS 로그인 성공 후 호출 (GPGSLogin.cs에서 호출)
    public void OnLoginSuccess()
    {
        LoadAllAndEnterGame();
    }
}