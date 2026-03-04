using UnityEngine;

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

		int width	= Screen.width;
		int height	= (int)(Screen.width * 9f / 16);
		Screen.SetResolution(width, height, true);

		Screen.sleepTimeout = SleepTimeout.NeverSleep;

		progress.Play(OnAfterProgress);
	}

	private void OnAfterProgress()
	{
	}
}

