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
        _isShowing = true; // WhenFree 대기 중에도 isShowing 유지해 중복 ShowNext 호출 방지
        // 다른 모달(LeaderScoreUI, RandomEventUI 등)이 차단 중이면 닫힐 때까지 대기 후 실제 표시
        ModalGate.I.WhenFree(DisplayDequeued);
    }

    void DisplayDequeued()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }
        var (msg, cb) = _queue.Dequeue();
        GameTimeManager.Instance?.StopTime();
        messageText.text = msg;
        _onConfirm = cb;
        alertPanel.SetActive(true);
        ModalGate.I.Register(this); // 표시 시점에 차단 모달로 등록
    }

    public void OnClickConfirm()
    {
        alertPanel.SetActive(false);
        ModalGate.I.Unregister(this);
        GameTimeManager.Instance?.StartTime();
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
        ShowNext();
    }
}