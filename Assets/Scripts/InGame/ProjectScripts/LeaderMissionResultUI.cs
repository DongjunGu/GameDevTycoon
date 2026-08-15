using UnityEngine;
using UnityEngine.UI;
using TMPro;

// LeaderScoreEntirePanel 소속 — 도전과제 성공 + 보상 확정 시 ChallengeManager 가 호출해 활성화하는 결과 팝업.
// 기본 비활성, 확인 버튼으로 닫으면 다시 숨김(다음 성공 때 재사용).
public class LeaderMissionResultUI : MonoBehaviour
{
    public static LeaderMissionResultUI Instance { get; private set; }

    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI rewardText;
    public TextMeshProUGUI satisfactionText;
    public Button confirmButton;

    // GameObject 자체는 항상 active — 표시/숨김은 CanvasGroup으로만 처리한다. 자기 자신을
    // SetActive(false)로 껐다 켜면 Awake가 최초 Show의 SetActive(true) 순간 처음 실행되며 그 안의
    // SetActive(false)가 즉시 다시 꺼버리는 버그가 남는다 — [[feedback_awake_toggle_bug]].
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (confirmButton != null) confirmButton.onClick.AddListener(Hide);
        Hide();
    }

    public void Show(string rewardLine, string satisfactionLine)
    {
        if (titleText != null) titleText.text = "도전과제 성공!";
        if (rewardText != null) rewardText.text = rewardLine;
        if (satisfactionText != null) satisfactionText.text = satisfactionLine;
        if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; canvasGroup.interactable = true; }
    }

    void Hide()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
