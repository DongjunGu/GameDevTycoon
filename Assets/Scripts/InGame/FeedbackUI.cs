using UnityEngine;
using TMPro;

public class FeedbackUI : MonoBehaviour
{
    public static FeedbackUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject feedbackPanel;

    [Header("UI")]
    public TextMeshProUGUI feedbackText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        feedbackPanel.SetActive(false);
    }

public void Show(float marketFit, float marketingBonus, float bug = 0f, float releaseEventBonus = 0f)
{
    var sb = new System.Text.StringBuilder();

    if (marketFit <= 0f)
        sb.AppendLine("장르의 인기를 확인하셨나요?");

    if (marketingBonus < 0f)
        sb.AppendLine("마케팅 정말 열심히 하셨나요?");

    if (bug > 0f)
        sb.AppendLine("디버깅은 제대로 하셨나요?");
    if (releaseEventBonus < 0f)
        sb.AppendLine("테크트리에서 문제가 발생할 확률을 낮춰보세요.");

    if (sb.Length == 0)
        sb.AppendLine("완벽한 출시였습니다!");

    feedbackText.text = sb.ToString().TrimEnd();
    feedbackPanel.SetActive(true);
}

    public void OnClickClose()
    {
        GameTimeManager.Instance.SaveGameTime();
        feedbackPanel.SetActive(false);
    }
}