using UnityEngine;
using TMPro;

public class AlertUI : MonoBehaviour
{
    public GameObject alertPanel;
    public static AlertUI Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI messageText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        alertPanel.SetActive(false);
    }

    public void Show(string message, System.Action onConfirm = null)
    {
        GameTimeManager.Instance?.StopTime();
        messageText.text = message;
        _onConfirm = onConfirm;
        alertPanel.SetActive(true);
    }

    private System.Action _onConfirm;

    public void OnClickConfirm()
    {
        alertPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        _onConfirm?.Invoke();
    }
}