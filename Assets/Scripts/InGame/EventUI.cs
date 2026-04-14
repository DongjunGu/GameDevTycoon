using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EventUI : MonoBehaviour
{
    public static EventUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public Image portraitImage;
    public TextMeshProUGUI messageText;

    private struct EventRequest
    {
        public string title;
        public string portraitId;
        public string message;
        public System.Action onConfirm;
    }

    private Queue<EventRequest> _queue = new();
    private bool _isShowing = false;
    private System.Action _onConfirm;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
    }

    public void Show(string title, string portraitId, string message, System.Action onConfirm = null)
    {
        _queue.Enqueue(new EventRequest
        {
            title      = title,
            portraitId = portraitId,
            message    = message,
            onConfirm  = onConfirm
        });
        if (!_isShowing) ShowNext();
    }

    void ShowNext()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }

        _isShowing = true;
        var req = _queue.Dequeue();

        titleText.text   = req.title;
        messageText.text = req.message;
        _onConfirm       = req.onConfirm;

        // 초상화 로드
        Sprite portrait = Resources.Load<Sprite>($"Portraits/{req.portraitId}");
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        GameTimeManager.Instance?.StopTime();
        eventPanel.SetActive(true);
    }

    public void OnClickConfirm()
    {
        eventPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
        ShowNext();
    }
}
