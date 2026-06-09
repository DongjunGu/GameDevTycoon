using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RandomEventUI : MonoBehaviour
{
    public static RandomEventUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public TextMeshProUGUI nameText;        // 직원 이름 (LeftPanel/NamePanel/NameBG/NameText)
    public GameObject namePanel;            // 이름 없을 때 숨김 (LeftPanel/NamePanel)
    public TextMeshProUGUI descriptionText; // 대사 텍스트 — description 타이핑만 (효과는 AlertUI 로)
    public Image portraitImage;
    public Button clickButton;              // 전체화면 클릭 — 진행/스킵 (FullScreenClickBtn)

    [Header("Typing")]
    public float typeInterval = 0.03f;      // 글자당 출력 간격 (실시간 — 시간정지 무관)

    private RandomEventData _currentEvent;
    private Coroutine _typeCo;
    private bool _isTyping;
    private bool _hasSystem;
    private string _systemMessage;

    // EventUI 호환(단순 대사) 모드 — 효과/저장/개발재개는 호출자(onConfirm)가 담당.
    private bool _simpleMode;
    private System.Action _simpleOnConfirm;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnScreenClick);
            clickButton.onClick.AddListener(OnScreenClick);
        }

        eventPanel.SetActive(false);
    }

    // 다른 모달(AlertUI / 다른 이벤트 패널)이 떠 있으면 닫힐 때까지 대기 후 표시 — 동시 표시 방지.
    public void Show(RandomEventData evt)
    {
        ModalGate.I.WhenFree(() => DisplayInternal(evt));
    }

    // ── EventUI 호환 오버로드 ─────────────────────────────────
    // 대사만 타이핑 후 클릭 시 onConfirm 호출. 효과/저장/개발재개는 onConfirm(보통 AlertUI)이 담당.
    // title 은 표시하지 않음(대사 다이얼로그 정책). 시간 정지/재개만 EventUI 와 동일하게 처리.
    public void Show(string title, string portraitId, string message, System.Action onConfirm = null)
    {
        ModalGate.I.WhenFree(() => DisplaySimple(portraitId, message, onConfirm));
    }

    void DisplaySimple(string portraitId, string message, System.Action onConfirm)
    {
        _currentEvent    = null;
        _simpleMode      = true;
        _simpleOnConfirm = onConfirm;
        _systemMessage   = "";
        _hasSystem       = false;

        // 이름 — portraitId 로 보유직원 매칭(비서 등 매칭 없으면 숨김)
        string speaker = ResolveSpeakerByPortrait(portraitId);
        if (nameText  != null) nameText.text = speaker;
        if (namePanel != null) namePanel.SetActive(!string.IsNullOrEmpty(speaker));

        if (portraitImage != null)
        {
            Sprite portrait = !string.IsNullOrEmpty(portraitId)
                ? Resources.Load<Sprite>($"Portraits/{portraitId}") : null;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        GameTimeManager.Instance?.StopTime();
        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        StartTyping(message);
    }

    void DisplayInternal(RandomEventData evt)
    {
        _currentEvent = evt;
        _simpleMode   = false;

        // 이름 — targetEmployeeId 우선, 없으면 portraitId 로 보유직원 매칭
        string speaker = ResolveSpeakerName(evt);
        if (nameText != null) nameText.text = speaker;
        if (namePanel != null) namePanel.SetActive(!string.IsNullOrEmpty(speaker));

        // 효과 메시지는 닫을 때 AlertUI 로 표시 (EventUI 와 동일)
        _systemMessage = evt.systemMessage;
        _hasSystem     = !string.IsNullOrEmpty(_systemMessage);

        // 초상화
        if (portraitImage != null)
        {
            Sprite portrait = !string.IsNullOrEmpty(evt.portraitId)
                ? Resources.Load<Sprite>($"Portraits/{evt.portraitId}")
                : null;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        // 대사 타이핑 시작
        StartTyping(evt.description);
    }

    string ResolveSpeakerName(RandomEventData evt)
    {
        if (EmployeeManager.Instance == null) return "";

        if (!string.IsNullOrEmpty(evt.targetEmployeeId))
        {
            var e = EmployeeManager.Instance.GetEmployee(evt.targetEmployeeId);
            if (e != null) return e.employeeName;
        }
        return ResolveSpeakerByPortrait(evt.portraitId);
    }

    string ResolveSpeakerByPortrait(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return "";
        if (portraitId == "portrait_secretary") return "비서"; // 비서 NPC — EmployeeData 없음
        if (EmployeeManager.Instance == null) return "";
        var e = EmployeeManager.Instance.ownedEmployees
            .FirstOrDefault(x => x != null && x.portraitId == portraitId);
        return e != null ? e.employeeName : "";
    }

    // ── 타이핑 ────────────────────────────────────────────────
    void StartTyping(string full)
    {
        if (_typeCo != null) StopCoroutine(_typeCo);
        _typeCo = StartCoroutine(TypeRoutine(full ?? ""));
    }

    IEnumerator TypeRoutine(string full)
    {
        _isTyping = true;

        // 전체 텍스트를 미리 세팅하고 maxVisibleCharacters 로만 노출 → 글자마다 레이아웃 리플로우 없음
        descriptionText.text = full;
        descriptionText.maxVisibleCharacters = 0;
        descriptionText.ForceMeshUpdate();
        int total = descriptionText.textInfo.characterCount;

        for (int i = 0; i <= total; i++)
        {
            descriptionText.maxVisibleCharacters = i;
            if (typeInterval > 0f) yield return new WaitForSecondsRealtime(typeInterval);
            else yield return null;
        }

        descriptionText.maxVisibleCharacters = total;
        _isTyping = false;
        _typeCo = null;
    }

    void CompleteTyping()
    {
        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        descriptionText.maxVisibleCharacters = int.MaxValue;
        descriptionText.ForceMeshUpdate();
        _isTyping = false;
    }

    // ── 전체화면 클릭 ─────────────────────────────────────────
    public void OnScreenClick()
    {
        // 1) 타이핑 중이면 즉시 전체 출력
        if (_isTyping) { CompleteTyping(); return; }

        // 2) 대사 다 봤으면 닫기 (EventUI 호환은 onConfirm, 기본은 효과 AlertUI)
        if (_simpleMode) OnSimpleConfirm();
        else             OnClickConfirm();
    }

    // EventUI 호환 확정 — 닫고 시간 재개 후 onConfirm 호출 (효과/저장은 onConfirm 이 처리)
    void OnSimpleConfirm()
    {
        var cb = _simpleOnConfirm;
        _simpleOnConfirm = null;
        _simpleMode      = false;

        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        _isTyping = false;

        eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);
        GameTimeManager.Instance?.StartTime();
        cb?.Invoke();
    }

    // 확인 (이벤트 적용 + 닫기 → 효과는 AlertUI 로)
    public void OnClickConfirm()
    {
        // Unregister 시 대기 큐의 다음 모달이 즉시 표시되며 _currentEvent / _systemMessage 가
        // 덮어써질 수 있으므로 먼저 모두 캡처.
        var    evt    = _currentEvent;
        string sysMsg = _systemMessage;
        bool   hasSys = _hasSystem;

        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        _isTyping = false;

        eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);
        evt?.onApply?.Invoke();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        // 개발중 이벤트일 때만 재개 — 단, ResumeFromEvent 는 ForceStartTime(정지카운트=0) 이라
        // AlertUI 의 시간정지를 무력화하므로 효과 알림을 닫은 뒤(콜백)에서 실행한다.
        bool resumeDev = evt != null &&
            evt.type != RandomEventType.EmployeeRun &&
            evt.type != RandomEventType.EmployeeFight &&
            evt.type != RandomEventType.BadCompany &&
            evt.type != RandomEventType.Recruit;

        System.Action after = () =>
        {
            if (resumeDev) DevelopmentManager.Instance.ResumeFromEvent();
        };

        // 효과(systemMessage)는 EventUI 와 동일하게 AlertUI 로 표시
        if (hasSys) AlertUI.Instance.Show(sysMsg, after);
        else        after();
    }
}
