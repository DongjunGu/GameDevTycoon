using UnityEngine;
using BackEnd;
using UnityEngine.SceneManagement;

public class BackendManager : MonoBehaviour
{
    // 이번 앱 프로세스에서 로그인+전체 데이터 로드가 이미 한 번 완료됐는지 (DontDestroyOnLoad 매니저들이
    // 전부 메모리에 살아있는 상태). true면 LoadingScene을 다시 돌아도 뒤끝 재로그인/데이터 재로드를 스킵한다 —
    // 안 그러면 Backend.Initialize()/재로그인이 두 번째부턴 실패해서 Progress가 90%에서 영영 멈춘다
    // (TestResetBtn처럼 게임 중간에 LoadingScene으로 재진입하는 경우).
    public static bool HasInitializedThisSession = false;

    void Start()
    {
        if (HasInitializedThisSession)
        {
            var p = FindAnyObjectByType<Progress>();
            p?.SetLoginComplete();
            p?.SetAllDataLoaded();
            return;
        }

        if (BackendRetry.Instance == null)
            gameObject.AddComponent<BackendRetry>();
        if (ErrorReporter.Instance == null)
            gameObject.AddComponent<ErrorReporter>();

        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("초기화 성공 : " + bro);
            //FindAnyObjectByType<Progress>().Play();

            // 기기에 로컬 저장된 뒤끝 access/refresh 토큰으로 우선 자동 로그인 시도 — 이전에 어떤 방식으로
            // 로그인했든(Apple/GPGS/게스트 등) 성공하면 플랫폼별 최초 로그인(Apple 인증창, GPGS 계정선택 등)
            // 없이 바로 이어서 진행. 최초 실행이거나 토큰이 만료/무효(1년 경과, 다른 기기 로그인 등)일 때만 실패.
            var tokenBro = Backend.BMember.LoginWithTheBackendToken();
            if (tokenBro.IsSuccess())
            {
                Debug.Log("[BackendManager] 토큰 자동 로그인 성공: " + tokenBro);
                FindAnyObjectByType<Progress>()?.SetLoginComplete();
                LoadAllAndEnterGame();
                return;
            }
            Debug.Log($"[BackendManager] 토큰 로그인 실패(최초 실행/토큰 만료 등) - 플랫폼별 로그인 진행: {tokenBro}");

#if UNITY_EDITOR
            TestLogin();
#elif UNITY_ANDROID
            FindAnyObjectByType<GPGSLogin>().StartLogin();
#elif UNITY_IOS
            // Android(GPGS)와 동일하게 진입 즉시 자동 시도. iOS는 이미 로그인/기기 신뢰가 있으면
            // 시스템이 UI 없이(또는 Face ID 확인만으로) 조용히 완료시켜 자동 로그인처럼 동작한다.
            // 실패 시에만 LoginButtonPanel 버튼이 활성화되어 수동 재시도(=최초 유저 온보딩) 경로가 된다.
            var appleLogin = FindAnyObjectByType<GameCenterLogin>();
            if (appleLogin != null)
            {
                appleLogin.StartLogin();
            }
            else
            {
                Debug.LogError("[BackendManager] AppleLogin 오브젝트를 찾을 수 없음 — 자동 로그인 시도 불가");
                FindAnyObjectByType<LoginButtonPanel>()?.EnableRetry();
            }
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

    // 90% 대기 중 정확히 어느 단계에서 멈췄는지 화면에 표시(콘솔을 볼 수 없는 기기 테스트용).
    void Step(string label)
    {
        Debug.Log($"[LoadAllAndEnterGame] {label}");
        FindAnyObjectByType<Progress>()?.SetStep(label);
    }

    void LoadAllAndEnterGame()
    {
        ErrorReporter.ReadyToSend = true; // 로그인 완료 시점부터 에러 자동 전송 시작

        Step("차트 로드");
        // 차트 데이터는 동기 로드 — 로그인 직후 한 번만 실행, 이후 캐시 반환
        RandomEventChartLoader.Load();
        RandomEventChoiceChartLoader.Load();
        RandomEventConditionChartLoader.Load();
        ItemChartLoader.Load();
        TechTreeChartLoader.Load();
        TraitChartLoader.Load();
        CharacterTraitChartLoader.Load();
        CharacterUniqueEventChartLoader.Load();
        TutorialDialogChartLoader.Load();
        RandomEventManager.Instance.InitConditionEvents();

        Step("EmployeeManager");
        EmployeeManager.Instance.LoadAllData(() =>
        {
            Step("OutGameCurrencyManager");
            OutGameCurrencyManager.Instance?.LoadCurrency(() =>
            {
            Step("OutGameEmployeeManager");
            OutGameEmployeeManager.Instance?.LoadAsync(() =>
            {
            Step("OwnedCardManager");
            OwnedCardManager.Instance?.LoadAsync(() =>
            {
            Step("OwnedTraitManager");
            OwnedTraitManager.Instance?.LoadAsync(() =>
            {
            Step("CEOManager");
            CEOManager.Instance?.LoadAsync(() =>
            {
            Step("StoneManager");
            StoneManager.Instance?.LoadAsync(() =>
            {
            Step("RunStateManager");
            RunStateManager.Instance?.LoadAsync(() =>
            {
            Step("MoneyManager");
            MoneyManager.Instance.LoadMoney(() =>
            {
                Step("GameTimeManager");
                GameTimeManager.Instance.LoadGameTime(() =>
                {
                    Step("QuestManager");
                    QuestManager.Instance.LoadQuests(() =>
                    {
                        Step("ProjectSaveManager");
                        ProjectSaveManager.Instance.LoadProject(() =>
                        {
                            Step("CompletedProjectManager");
                            CompletedProjectManager.Instance.LoadCompletedProjects(() =>
                            {
                                Step("LoanManager");
                                LoanManager.Instance.LoadLoans(() =>
                                {
                                    Step("TechTreeManager");
                                    TechTreeManager.Instance.LoadTechTree(() =>
                                    {
                                        Step("SalesSaveManager");
                                        SalesSaveManager.Instance.LoadSales(() =>
                                        {
                                            Step("ItemManager");
                                            ItemManager.Instance.Load(() =>
                                            {
                                                Step("완료");
                                                DialogManager.Instance.Initialize();
                                                HasInitializedThisSession = true;
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