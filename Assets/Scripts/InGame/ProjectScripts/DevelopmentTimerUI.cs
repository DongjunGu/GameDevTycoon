using UnityEngine;
using TMPro;

public class DevelopmentTimerUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI progressText;

    void Update()
    {
        if (DevelopmentManager.Instance == null) return;

        float elapsed  = DevelopmentManager.Instance.GetElapsed();
        float duration = DevelopmentManager.Instance.developmentDuration;
        float remain   = Mathf.Max(0f, duration - elapsed);
        float progress = DevelopmentManager.Instance.GetProgress();

        int minutes = (int)(remain / 60f);
        int seconds = (int)(remain % 60f);

        timerText.text   = $"{minutes:00}:{seconds:00}";
        progressText.text = $"{(int)(progress * 100f)}%";
    }
}