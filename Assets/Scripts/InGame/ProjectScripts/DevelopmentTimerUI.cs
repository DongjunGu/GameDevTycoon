using UnityEngine;
using TMPro;

public class DevelopmentTimerUI : MonoBehaviour
{
    [Header("UI")]
    //public TextMeshProUGUI timerText;
    public TextMeshProUGUI progressText;
    public static DevelopmentTimerUI Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        ResetTimer();
    }
    
    void Update()
    {
        if (!DevelopmentManager.Instance.IsStarted) return;

        var stage = DevelopmentManager.Instance.CurrentStage;
        if (stage != ProjectStage.Developing && stage != ProjectStage.BugFixing) return;

        float remaining = DevelopmentManager.Instance.developmentDuration
                        - DevelopmentManager.Instance.GetElapsed();
        remaining = Mathf.Max(0f, remaining);

        int min = (int)(remaining / 60f);
        int sec = (int)(remaining % 60f);
        //timerText.text = $"{min:00}:{sec:00}";

        float progress = DevelopmentManager.Instance.GetProgress();
        progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }
    public void ResetTimer()
    {
        //timerText.text = "";
        progressText.text = "";
    }

    // 개발 단계가 아닐 때(defaultText 표시 중) 타이머/진행률 텍스트 숨김. enabled 토글로 layout 자리 유지.
    public void SetTextVisible(bool visible)
    {
        //if (timerText != null)    timerText.enabled = visible;
        if (progressText != null) progressText.enabled = visible;
    }
}