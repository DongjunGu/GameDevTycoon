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

    // ── AlertUI4 (결과 팝업 — 타이틀 + 결과 2줄) ──────────────────
    [Header("AlertUI4 - 결과 팝업 (Title+result1+result2)")]
    public GameObject result4Panel;
    public TextMeshProUGUI result4TitleText;
    public TextMeshProUGUI result4Text1;
    public TextMeshProUGUI result4Text2;
    public Button result4ConfirmButton;

    // ── AlertUI5 (결과 팝업 — 단순 문구) ──────────────────────────
    [Header("AlertUI5 - 결과 팝업 (TitleText만)")]
    public GameObject result5Panel;
    public TextMeshProUGUI result5TitleText;
    public Button result5ConfirmButton;

    // ── AlertUI6 (결과 팝업 — 타이틀 + 하단 문구) ─────────────────
    [Header("AlertUI6 - 결과 팝업 (Title+Bottom)")]
    public GameObject result6Panel;
    public TextMeshProUGUI result6TitleText;
    public TextMeshProUGUI result6BottomText;
    public Button result6ConfirmButton;

    // 결과 멘트 색상 — 버프(+/증가/상승) 계열은 빨강, 디버프(-/감소/하락/차감) 계열은 파랑.
    // (프로젝트 공통 컬러 컨벤션: [[project_employee_visual_sets]] 능력치 버프#E63356/디버프#517FFF 와 동일 계열)
    static readonly Color BuffMentColor   = new Color32(0xD2, 0x2E, 0x2E, 0xFF); // #D22E2E
    static readonly Color DebuffMentColor = new Color32(0x62, 0x51, 0xD4, 0xFF); // #6251D4

    // ── 내부 ────────────────────────────────────────────────────
    enum AlertType { Default, Money, Portrait, Result4, Result5, Result6 }

    struct Entry
    {
        public string        message;
        public System.Action onConfirm;
        public AlertType     type;
        public string        portraitId;
        public string        label;
        public int?          goldAmount;
        // AlertUI4/5/6 전용 — title=결과팝업멘트1, resultText1=멘트2, resultText2=멘트3(4만 사용)
        public string        title;
        public string        resultText1;
        public string        resultText2;
        // true면 ModalGate.WhenFree 대기 없이 즉시 표시 (다른 패널이 이미 게이트를 쥔 채로 열려있는 상황용)
        public bool          bypassGate;
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
        result4Panel?.SetActive(false);
        result5Panel?.SetActive(false);
        result6Panel?.SetActive(false);

        if (alertConfirmButton    != null) alertConfirmButton.onClick.AddListener(OnClickConfirm);
        if (moneyConfirmButton    != null) moneyConfirmButton.onClick.AddListener(OnClickConfirm);
        if (portraitConfirmButton != null) portraitConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result4ConfirmButton  != null) result4ConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result5ConfirmButton  != null) result5ConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result6ConfirmButton  != null) result6ConfirmButton.onClick.AddListener(OnClickConfirm);
    }

    // ── 공개 API ────────────────────────────────────────────────

    // bypassGate: true면 다른 패널(ItemPanel 등)이 이미 열려 게이트를 쥐고 있어도 대기 없이 바로 표시.
    public void Show(string message, System.Action onConfirm = null, bool bypassGate = false)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Default, bypassGate = bypassGate });
        if (!_isShowing) ShowNext();
    }

    // goldAmount: G 아이콘 옆 금액 표시. null이면 금액 영역 숨김.
    // bypassGate: true면 다른 패널(ProjectSetupUI 등)이 이미 열려 게이트를 쥐고 있어도 대기 없이 바로 표시.
    public void ShowMoney(string message, int? goldAmount = null, System.Action onConfirm = null, bool bypassGate = false)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Money, goldAmount = goldAmount, bypassGate = bypassGate });
        if (!_isShowing) ShowNext();
    }

    // label: 특성명 / 이벤트명이면 그 이름, 아이템 발동이면 직원 이름
    public void ShowPortrait(string message, string portraitId, string label, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { message = message, onConfirm = onConfirm, type = AlertType.Portrait, portraitId = portraitId, label = label });
        if (!_isShowing) ShowNext();
    }

    // 결과 팝업 종류 1 — title(결과팝업멘트1)/result1(멘트2)/result2(멘트3). 각 텍스트는 +/- 등 내용에 따라
    // 버프(#D22E2E)/디버프(#6251D4) 자동 색상, 부호가 없으면 기본색 유지.
    public void ShowResult4(string title, string result1, string result2, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { onConfirm = onConfirm, type = AlertType.Result4, title = title, resultText1 = result1, resultText2 = result2 });
        if (!_isShowing) ShowNext();
    }

    // 결과 팝업 종류 2 — 간단한 문구 하나(TitleText).
    public void ShowResult5(string title, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { onConfirm = onConfirm, type = AlertType.Result5, title = title });
        if (!_isShowing) ShowNext();
    }

    // 결과 팝업 종류 3 — title(결과팝업멘트1, TitleText)/bottom(멘트2, BottomText).
    public void ShowResult6(string title, string bottom, System.Action onConfirm = null)
    {
        _queue.Enqueue(new Entry { onConfirm = onConfirm, type = AlertType.Result6, title = title, resultText1 = bottom });
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
        if (_queue.Peek().type == AlertType.Portrait || _queue.Peek().bypassGate) { DisplayDequeued(); return; }

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
                        moneyAmountText.text = $"{entry.goldAmount.Value:N0} G";
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

            case AlertType.Result4:
                if (result4Panel == null) { Debug.LogError("[AlertUI] result4Panel 미연결"); goto default; }
                SetMentText(result4TitleText, entry.title);
                SetMentText(result4Text1,     entry.resultText1);
                SetMentText(result4Text2,     entry.resultText2);
                EnsureTopMost(result4Panel);
                result4Panel.SetActive(true);
                break;

            case AlertType.Result5:
                if (result5Panel == null) { Debug.LogError("[AlertUI] result5Panel 미연결"); goto default; }
                SetMentText(result5TitleText, entry.title);
                EnsureTopMost(result5Panel);
                result5Panel.SetActive(true);
                break;

            case AlertType.Result6:
                if (result6Panel == null) { Debug.LogError("[AlertUI] result6Panel 미연결"); goto default; }
                SetMentText(result6TitleText,  entry.title);
                SetMentText(result6BottomText, entry.resultText1);
                EnsureTopMost(result6Panel);
                result6Panel.SetActive(true);
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

    // AlertUI4/5/6 전용 — 텍스트를 채우고, 버프(+/증가/상승 등)면 D22E2E, 디버프(-/감소/하락/차감 등)면 6251D4로
    // 색을 바꾼다. 부호/키워드가 없거나 둘 다 섞여 있으면 인스펙터 기본색 유지(색 변경 안 함).
    void SetMentText(TextMeshProUGUI tmp, string content)
    {
        if (tmp == null) return;
        tmp.text = content ?? "";
        var color = DetectMentColor(content);
        if (color.HasValue) tmp.color = color.Value;
    }

    static Color? DetectMentColor(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        bool buff   = text.Contains("+") || text.Contains("증가") || text.Contains("상승");
        bool debuff = text.Contains("-") || text.Contains("감소") || text.Contains("하락") || text.Contains("차감");

        if (buff && !debuff)   return BuffMentColor;
        if (debuff && !buff)   return DebuffMentColor;
        return null; // 둘 다 있거나(예: "+10 / -5%") 둘 다 없으면 판단 보류 — 기본색 유지
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
            case AlertType.Result4:  result4Panel?.SetActive(false);  break;
            case AlertType.Result5:  result5Panel?.SetActive(false);  break;
            case AlertType.Result6:  result6Panel?.SetActive(false);  break;
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
