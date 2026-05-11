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
		// RunState.IsPlaying 이면 인게임 이어하기, 아니면 아웃게임
		bool isPlaying = RunStateManager.Instance != null && RunStateManager.Instance.IsPlaying;
		SceneManager.LoadScene(isPlaying ? "GameScene" : "OutGameScene");
	}
}

