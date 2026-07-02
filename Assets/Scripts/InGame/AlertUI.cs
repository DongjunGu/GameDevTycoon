using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AlertUI : MonoBehaviour
{
    public static AlertUI Instance { get; private set; }

    // ── AlertUI1 (기본) ──────────────────────────────────────────
    [Header("AlertUI1 - 기본")]
    public GameObject alertPanel;
    public TextMeshProUGUI messageText;
    public Button alertConfirmButton;   // 패널 전체를 덮는 투명 버튼

    // ── AlertUI2 (돈) ────────────────────────────────────────────
    [Header("AlertUI2 - 돈")]
    public GameObject moneyPanel;
    public TextMeshProUGUI moneyMessageText;   // 상단 일반 메시지
    public GameObject moneyAmountRoot;          // G 아이콘 + 금액 묶음 (금액 없으면 숨김)
    public TextMeshProUGUI moneyAmountText;    // 금액 전용 텍스트
    public Button moneyConfirmButton;           // 패널 전체를 덮는 투명 버튼

    // ── AlertUI3 (직원 portrait) ─────────────────────────────────
    [Header("AlertUI3 - 직원 portrait")]
    public GameObject portraitPanel;
    public TextMeshProUGUI portraitLabelText;   // 특성명 / 이벤트명 / 직원이름
    public TextMeshProUGUI portraitMessageText;
    public Image portraitImage;
    public Button portraitConfirmButton;        // 패널 전체를 덮는 투명 버튼

    // ── 내부 ────────────────────────────────────────────────────
    enum AlertType { Default, Money, Portrait }

    struct Entry
    {
        public string        message;
        public System.Action onConfirm;
        public AlertType     type;
        public string        portraitId;
        public string        label;
        public int?          goldAmount;
    }

    private Queue<Entry> _queue      = new();
    private bool         _isShowing  = false;
    private System.Action _onConfirm;
    private AlertType     _currentType;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        alertPanel?.SetActive(false);
        moneyPanel?.SetActive(false);
        portraitPanel?.SetActive(false);

        if (alertConfirmButton   != null) alertConfirmButton.onClick.AddListener(OnClickConfirm);
        if (moneyConfirmButton   != null) moneyConfirmButton.onClick.AddListener(OnClickConfirm);
        if (portraitConfirmButton != null) portraitConfirmButton.onClick.AddListener(OnClickConfirm);
    }

    // ── 공개 API ────────────────────────────────────────────────

    public void Show(string message, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Default });
        if (!_isShowing) ShowNext();
    }

    // goldAmount: G 아이콘 옆 금액 표시. null이면 금액 영역 숨김.
    public void ShowMoney(string message, int? goldAmount = null, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Money, goldAmount = goldAmount });
        if (!_isShowing) ShowNext();
    }

    // label: 특성명 / 이벤트명이면 그 이름, 아이템 발동이면 직원 이름
    public void ShowPortrait(string message, string portraitId, string label, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Portrait, portraitId = portraitId, label = label });
        if (!_isShowing) ShowNext();
    }

    // ── 내부 큐 ─────────────────────────────────────────────────

    void ShowNext()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }
        _isShowing = true;

        // Portrait(특성/이벤트 설명, 아이템 사용 결과 등)는 "지금 보고 있는 패널 위에 즉시 뜨는" 정보 팝업이라
        // 다른 UI 가 점유 중인 ModalGate 를 기다리지 않고 바로 표시한다(자기 자신도 아래서 ModalGate.Register 됨).
        // 안 그러면 예: HiringUI 가 ConfirmHirePanel 이 떠 있는 동안 게이트를 쥐고 있어서, 그 안에서 특성/이벤트
        // 버튼을 눌러도 안 뜨고 ConfirmHirePanel 이 닫혀 게이트가 풀리는 순간에야 뒤늦게 뜨는 문제가 있었음.
        if (_queue.Peek().type == AlertType.Portrait) { DisplayDequeued(); return; }

        ModalGate.I.WhenFree(DisplayDequeued);
    }

    void DisplayDequeued()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }
        var entry = _queue.Dequeue();
        GameTimeManager.Instance?.StopTime();
        _onConfirm   = entry.onConfirm;
        _currentType = entry.type;

        switch (entry.type)
        {
            case AlertType.Money:
                if (moneyPanel == null) { Debug.LogError("[AlertUI] moneyPanel 미연결"); goto default; }
                if (moneyMessageText != null) moneyMessageText.text = entry.message;
                if (moneyAmountRoot != null)
                {
                    bool hasAmount = entry.goldAmount.HasValue;
                    moneyAmountRoot.SetActive(hasAmount);
                    if (hasAmount && moneyAmountText != null)
                        moneyAmountText.text = $"{entry.goldAmount.Value:N0}G";
                }
                EnsureTopMost(moneyPanel);
                moneyPanel.SetActive(true);
                break;

            case AlertType.Portrait:
                if (portraitPanel == null) { Debug.LogError("[AlertUI] portraitPanel 미연결"); goto default; }
                if (portraitLabelText   != null) portraitLabelText.text   = entry.label ?? "";
                if (portraitMessageText != null) portraitMessageText.text = entry.message;
                if (portraitImage != null)
                {
                    var sp = string.IsNullOrEmpty(entry.portraitId)
                        ? null
                        : Resources.Load<Sprite>($"Portraits/Mini/{entry.portraitId}");
                    portraitImage.sprite  = sp;
                    portraitImage.enabled = sp != null;
                }
                EnsureTopMost(portraitPanel);
                portraitPanel.SetActive(true);
                break;

            default:
                if (alertPanel == null) { Debug.LogError("[AlertUI] alertPanel 미연결"); return; }
                if (messageText != null) messageText.text = entry.message;
                EnsureTopMost(alertPanel);
                alertPanel.SetActive(true);
                break;
        }

        ModalGate.I.Register(this);
    }

    void EnsureTopMost(GameObject panel)
    {
        if (panel == null) return;
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 32000;
        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();
        panel.transform.SetAsLastSibling();
    }

    // AlertUI1/2/3 각각의 확인 버튼에서 이 메서드를 호출
    public void OnClickConfirm()
    {
        switch (_currentType)
        {
            case AlertType.Money:    moneyPanel?.SetActive(false);    break;
            case AlertType.Portrait: portraitPanel?.SetActive(false); break;
            default:                 alertPanel?.SetActive(false);    break;
        }

        ModalGate.I.Unregister(this);
        GameTimeManager.Instance?.StartTime();
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
        ShowNext();
    }
}
