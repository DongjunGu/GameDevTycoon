using UnityEngine;
using TMPro;

public class DevelopmentTimerUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI progressText;

void Update()
{
    if (!DevelopmentManager.Instance.IsStarted) return;

    float remaining = DevelopmentManager.Instance.developmentDuration
                    - DevelopmentManager.Instance.GetElapsed();
    remaining = Mathf.Max(0f, remaining);

    int min = (int)(remaining / 60f);
    int sec = (int)(remaining % 60f);
    timerText.text = $"{min:00}:{sec:00}";

    float progress = DevelopmentManager.Instance.GetProgress();
    progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
}
}