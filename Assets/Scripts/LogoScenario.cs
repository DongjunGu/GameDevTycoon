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
		SceneManager.LoadScene("OutGameScene");
	}
}

