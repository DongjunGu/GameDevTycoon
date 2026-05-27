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

