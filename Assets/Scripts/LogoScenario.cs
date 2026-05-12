using UnityEngine;
using UnityEngine.SceneManagement;

public class LogoScenario : MonoBehaviour
{
	[SerializeField]
	private	Progress	progress;

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

		// RunState.IsPlaying 이면 인게임 이어하기, 아니면 아웃게임
		bool isPlaying = rs != null && rs.IsPlaying;
		SceneManager.LoadScene(isPlaying ? "GameScene" : "OutGameScene");
	}
}

