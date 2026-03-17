using UnityEngine;
using TMPro;

public class RandomEventUI : MonoBehaviour
{
    public static RandomEventUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    private RandomEventData _currentEvent;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
    }

    public void Show(RandomEventData evt)
    {
        _currentEvent       = evt;
        titleText.text       = evt.title;
        descriptionText.text = evt.description;
        eventPanel.SetActive(true);
    }

    // 확인 버튼
    public void OnClickConfirm()
    {
        _currentEvent?.onApply?.Invoke();
        DevelopmentManager.Instance.ResumeFromEvent();
        eventPanel.SetActive(false);
    }
}