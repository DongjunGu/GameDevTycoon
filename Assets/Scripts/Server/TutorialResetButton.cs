using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// LoadingScene/CutsceneCanvas/TutBtn 전용 — 테스트 용도. 로컬 온보딩 플래그(PlayerPrefs)와 서버의
// 튜토리얼 완료 플래그(RunState.tutorialFullyDone)를 전부 리셋하고 씬을 재시작해서, 이미 튜토리얼을
// 끝낸 계정에서도 처음부터 다시 재현할 수 있게 한다. 실기기 배포 빌드에서도 그대로 동작(에디터 전용 아님).
// ⚠️ 테스트 전용 버튼 — 실제 배포 전에는 CutsceneCanvas/TutBtn 오브젝트 자체를 씬에서 지울 것.
public class TutorialResetButton : MonoBehaviour
{
    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        OnboardingState.ResetAll();

        if (RunStateManager.Instance == null)
        {
            Debug.LogWarning("[TutorialResetButton] RunStateManager 인스턴스 없음 — 로컬 플래그만 리셋됨");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // tutorialFullyDone(계정 영구 서버 플래그)을 먼저 false로 안 풀면, 씬 재시작 시 RunStateManager.LoadAsync가
        // 서버에서 true를 다시 읽어와 OnboardingState.MarkAllTutorialStepsDone()을 또 호출해서 방금 한
        // ResetAll()이 무효화된다 — 반드시 이 순서(FullyDone=false → tutorial=true)로.
        RunStateManager.Instance.SetTutorialFullyDone(false, _ =>
        {
            RunStateManager.Instance.SetTutorial(true, success =>
            {
                Debug.Log(success
                    ? "[TutorialResetButton] RunState 리셋 완료(tutorial=true, tutorialFullyDone=false) — 씬 재시작"
                    : "[TutorialResetButton] RunState.tutorial 저장 실패 — 그래도 씬은 재시작");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });
        });
    }
}
