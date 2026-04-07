using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class AlertUI : MonoBehaviour
{
    public GameObject alertPanel;
    public static AlertUI Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI messageText;

    private Queue<(string message, System.Action onConfirm)> _queue = new();
    private bool _isShowing = false;
    private System.Action _onConfirm;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        alertPanel.SetActive(false);
    }

    public void Show(string message, System.Action onConfirm = null)
    {
        _queue.Enqueue((message, onConfirm));
        if (!_isShowing) ShowNext();
    }

    void ShowNext()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }
        _isShowing = true;
        var (msg, cb) = _queue.Dequeue();
        GameTimeManager.Instance?.StopTime();
        messageText.text = msg;
        _onConfirm = cb;
        alertPanel.SetActive(true);
    }

    public void OnClickConfirm()
    {
        alertPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
        ShowNext();
    }
}