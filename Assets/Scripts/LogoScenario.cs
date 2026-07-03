using UnityEngine;
using UnityEngine.SceneManagement;

public class LogoScenario : MonoBehaviour
{
	[SerializeField]
	private	Progress	progress;

	[SerializeField]
	[Tooltip("첫 게스트 컷씬 (LoadingScene 내 배치). 비어 있으면 컷씬 없이 바로 새 런 시작.")]
	private	CutsceneController	cutscene;

	private void Awake()
	{
		SystemSetup();
	}

	private void SystemSetup()
	{
		Application.runInBackground = true;

		//Screen.sleepTimeout = SleepTimeout.NeverSleep;

		// BackendManager는 EmployeeManager와 같은 GameObject에 있고 그쪽이 DontDestroyOnLoad라
		// 두 번째부터는 Start()가 다시 안 돈다 — 그럼 Progress가 로그인/데이터로드 완료를 영원히 못 받으므로,
		// 이미 이번 세션에 로그인+로드가 끝났으면(TestResetBtn 등으로 LoadingScene 재진입) 여기서 바로 완료 처리.
		if (BackendManager.HasInitializedThisSession)
		{
			progress.SetLoginComplete();
			progress.SetAllDataLoaded();
		}

		progress.Play(OnAfterProgress);
	}

	private void OnAfterProgress()
	{
		var rs = RunStateManager.Instance;

		// 이전 NewRun 도중 죽었으면 자동 재시도 (LogoScenario 시점이라 매니저들은 LoadAllAndEnterGame 으로 로드 완료 상태)
		if (rs != null && rs.ResetInProgress)
		{
			Debug.LogWarning("[Recovery] 이전 NewRun 미완료 감지 - 자동 재시도");
			NewRunInitializer.StartNewRun();
			return;
		}

		// 첫 게스트 온보딩 (1회): 컷씬 재생 → 새 런 시작 → GameScene (비서 튜토리얼은 GameScene 쪽에서 처리).
		// 컷씬이 끝난 뒤에 MarkIntroDone (중간 종료 시 다음 진입에 재노출).
		if (!OnboardingState.IntroDone)
		{
			if (cutscene != null)
				cutscene.Play(() => { OnboardingState.MarkIntroDone(); NewRunInitializer.StartNewRun(); });
			else
			{
				OnboardingState.MarkIntroDone();
				NewRunInitializer.StartNewRun();
			}
			return;
		}

		// RunState.IsPlaying 이면 인게임 이어하기, 아니면 아웃게임
		bool isPlaying = rs != null && rs.IsPlaying;
		SceneManager.LoadScene(isPlaying ? "GameScene" : "OutGameScene");
	}
}

