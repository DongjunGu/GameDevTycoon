using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 이벤트 다이얼로그 (EventPanel2 / DialogBackgroundPanel) — InfoUI 식 슬라이드 연출.
// 흐름:
//   1) 패널 활성 → PortraitImage 표시 (DialogTextBG/TitleBG/ResultBG 는 화면 밖에 숨김)
//   2) portraitDelay(0.5s) 후 → DialogTextBG + TitleBG 슬라이드 인, nameText/titleText 표시, 대사 타이핑
//   3) 타이핑 완료(자동) 또는 클릭(스킵) → 효과 적용 + ResultBG 슬라이드 인 (결과는 ResultText 에 출력, AlertUI 안 띄움)
//   4) 다시 클릭(IndicatorBtn 전체화면) → 닫기 + 개발 재개
// 호환 모드 Show(title, portraitId, message, onConfirm) 는 동일 연출을 타되, 결과/저장/재개는 onConfirm 이 담당
// (onConfirm 안의 AlertUI → ResultText 전환은 후속 작업).
public class RandomEventUI : MonoBehaviour
{
    public static RandomEventUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("Portrait / Dialog")]
    public Image portraitImage;
    public RectTransform dialogTextBG;       // 대사 배경 (슬라이드 대상)
    public GameObject namePanel;             // 이름 없을 때 숨김
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;  // 대사 (DialogText) — 타이핑

    [Header("Title / Result (RightUpPanel)")]
    public RectTransform titleBG;            // 제목 배경 (슬라이드 대상)
    public TextMeshProUGUI titleText;
    public RectTransform resultBG;           // 결과 배경 (슬라이드 대상)
    public TextMeshProUGUI resultText;       // 결과 (기존 AlertUI 내용)

    [Header("Click")]
    public Button clickButton;               // 전체화면 진행/닫기 (IndicatorBtn)

    [Header("Slide")]
    public float portraitDelay  = 0.5f;      // 초상 표시 후 대사/제목 슬라이드까지 대기
    public float slideDuration  = 0.35f;     // 각 BG 슬라이드 시간 (실시간)
    public Vector2 dialogHiddenOffset = new Vector2(0f, -500f);   // 대사 BG 숨김 오프셋 (아래)
    public Vector2 titleHiddenOffset  = new Vector2(1300f, 0f);   // 제목 BG 숨김 오프셋 (오른쪽)
    public Vector2 resultHiddenOffset = new Vector2(1300f, 0f);   // 결과 BG 숨김 오프셋 (오른쪽)

    [Header("Typing")]
    public float typeInterval = 0.02f;       // 글자당 출력 간격 (실시간 — 시간정지 무관)

    // ── 내부 상태 ─────────────────────────────────────────────
    enum Step { Idle, Intro, Typing, Result }
    private Step _step = Step.Idle;

    private RandomEventData _currentEvent;
    private bool   _simpleMode;
    private System.Action _simpleOnConfirm;

    private string _resultMessage;           // 데이터 모드 결과 텍스트
    private bool   _hasResult;
    private bool   _resumeDev;               // 닫을 때 개발 재개 여부 (데이터 모드)

    private Coroutine _flowCo;
    private Coroutine _typeCo;
    private bool _isTyping;

    // 슬라이드 위치 캐시 (LayoutGroup 이 배치한 표시 위치)
    private bool _posCached;
    private Vector2 _dialogShown, _titleShown, _resultShown;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnScreenClick);
            clickButton.onClick.AddListener(OnScreenClick);
        }

        if (eventPanel != null) eventPanel.SetActive(false);
    }

    // ── 진입점 ────────────────────────────────────────────────
    // 데이터 모드 — 다른 모달이 떠 있으면 닫힐 때까지 대기 후 표시.
    public void Show(RandomEventData evt)
    {
        ModalGate.I.WhenFree(() => DisplayData(evt));
    }

    // EventUI 호환 오버로드 — 대사만 타이핑, 효과/저장/재개는 onConfirm 담당.
    public void Show(string title, string portraitId, string message, System.Action onConfirm = null)
    {
        ModalGate.I.WhenFree(() => DisplaySimple(title, portraitId, message, onConfirm));
    }

    void DisplayData(RandomEventData evt)
    {
        _currentEvent    = evt;
        _simpleMode      = false;
        _simpleOnConfirm = null;

        _resultMessage = evt.systemMessage;
        _hasResult     = !string.IsNullOrEmpty(_resultMessage);
        _resumeDev     = evt.type != RandomEventType.EmployeeRun
                      && evt.type != RandomEventType.EmployeeFight
                      && evt.type != RandomEventType.BadCompany
                      && evt.type != RandomEventType.Recruit;

        string speaker = ResolveSpeakerName(evt);
        BeginDisplay(speaker, evt.portraitId, evt.title, evt.description);
    }

    void DisplaySimple(string title, string portraitId, string message, System.Action onConfirm)
    {
        _currentEvent    = null;
        _simpleMode      = true;
        _simpleOnConfirm = onConfirm;

        _resultMessage = "";
        _hasResult     = false;
        _resumeDev     = false;

        // 호환 모드는 EventUI 처럼 시간 정지 (데이터 모드는 호출 컨텍스트가 이미 정지 상태)
        GameTimeManager.Instance?.StopTime();

        string speaker = ResolveSpeakerByPortrait(portraitId);
        BeginDisplay(speaker, portraitId, title, message);
    }

    // 공통 표시 시작 — 텍스트/초상 세팅 후 슬라이드 연출 코루틴 가동.
    void BeginDisplay(string speaker, string portraitId, string title, string description)
    {
        if (eventPanel == null) return;

        // 이전 이벤트의 대사/이름/제목/결과 잔상 방지 — 표시 전에 모두 공백으로 비움.
        // (대사는 portraitDelay 뒤 TypeRoutine 이 채우므로, 비워두지 않으면 그 사이 이전 대사가 보임)
        if (descriptionText != null) { descriptionText.text = ""; descriptionText.maxVisibleCharacters = 0; }
        if (nameText  != null) nameText.text = "";
        if (titleText != null) titleText.text = "";
        if (resultText != null) resultText.text = "";

        // 캐시돼 있으면 활성화 전에 BG 를 숨김 위치로 미리 이동 (활성 첫 프레임 잔상 방지).
        if (_posCached)
        {
            if (dialogTextBG) dialogTextBG.anchoredPosition = _dialogShown + dialogHiddenOffset;
            if (titleBG)      titleBG.anchoredPosition      = _titleShown  + titleHiddenOffset;
            if (resultBG)     resultBG.anchoredPosition     = _resultShown + resultHiddenOffset;
        }

        // 이름
        if (nameText  != null) nameText.text = speaker ?? "";
        if (namePanel != null) namePanel.SetActive(!string.IsNullOrEmpty(speaker));

        // 제목
        if (titleText != null) titleText.text = title ?? "";

        // 결과 (미리 채워두되 ResultBG 가 숨겨져 있어 보이지 않음)
        if (resultText != null) resultText.text = _resultMessage ?? "";

        // 초상
        if (portraitImage != null)
        {
            Sprite portrait = !string.IsNullOrEmpty(portraitId)
                ? Resources.Load<Sprite>($"Portraits/{portraitId}") : null;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        CaptureShownPositions();

        // 모든 BG 를 숨김 위치로
        if (dialogTextBG) dialogTextBG.anchoredPosition = _dialogShown + dialogHiddenOffset;
        if (titleBG)      titleBG.anchoredPosition      = _titleShown  + titleHiddenOffset;
        if (resultBG)     resultBG.anchoredPosition     = _resultShown + resultHiddenOffset;

        _step = Step.Intro;
        if (_flowCo != null) StopCoroutine(_flowCo);
        _flowCo = StartCoroutine(IntroRoutine(description));
    }

    // 슬라이드될 BG 들이 LayoutGroup 자식이면 표시 위치를 캡처한 뒤 그룹을 꺼 직접 제어.
    void CaptureShownPositions()
    {
        if (_posCached) return;

        // LayoutGroup 이 자식을 배치하도록 강제 갱신 후 위치 캡처
        Canvas.ForceUpdateCanvases();
        var groups = new HashSet<LayoutGroup>();
        foreach (var rt in new[] { dialogTextBG, titleBG, resultBG })
        {
            if (rt == null) continue;
            var parent = rt.parent as RectTransform;
            if (parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            var g = rt.GetComponentInParent<LayoutGroup>();
            if (g != null) groups.Add(g);
        }

        if (dialogTextBG) _dialogShown = dialogTextBG.anchoredPosition;
        if (titleBG)      _titleShown  = titleBG.anchoredPosition;
        if (resultBG)     _resultShown = resultBG.anchoredPosition;

        // 이후 anchoredPosition 직접 제어를 위해 그룹/LayoutElement 영향 제거
        foreach (var rt in new[] { dialogTextBG, titleBG, resultBG })
        {
            if (rt == null) continue;
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }
        foreach (var g in groups) g.enabled = false;

        _posCached = true;
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

    // ── 연출 ──────────────────────────────────────────────────
    IEnumerator IntroRoutine(string description)
    {
        // 초상만 보인 채 잠시 대기
        if (portraitDelay > 0f) yield return new WaitForSecondsRealtime(portraitDelay);

        // 대사/제목 BG 동시 슬라이드 인
        if (dialogTextBG) StartCoroutine(SlideTo(dialogTextBG, _dialogShown, slideDuration));
        if (titleBG)      yield return SlideTo(titleBG, _titleShown, slideDuration);
        else if (dialogTextBG) yield return new WaitForSecondsRealtime(slideDuration);

        // 대사 타이핑
        _step = Step.Typing;
        StartTyping(description);
        _flowCo = null;
    }

    void StartTyping(string full)
    {
        if (_typeCo != null) StopCoroutine(_typeCo);
        _typeCo = StartCoroutine(TypeRoutine(full ?? ""));
    }

    IEnumerator TypeRoutine(string full)
    {
        _isTyping = true;

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

        // 타이핑 자동 완료 → 결과 단계로
        OnTypingDone();
    }

    void CompleteTyping()
    {
        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        if (descriptionText != null)
        {
            descriptionText.maxVisibleCharacters = int.MaxValue;
            descriptionText.ForceMeshUpdate();
        }
        _isTyping = false;
        OnTypingDone();
    }

    // ── 클릭 (IndicatorBtn 전체화면) ──────────────────────────
    public void OnScreenClick()
    {
        switch (_step)
        {
            case Step.Typing:
                if (_isTyping) CompleteTyping();   // 스킵 → 즉시 완료 → 결과
                break;
            case Step.Result:
                Close();                            // 결과까지 봤으면 닫기
                break;
            // Idle / Intro 중 클릭은 무시
        }
    }

    // 타이핑 완료 직후 — 결과(systemMessage)만 ResultBG 로 표시.
    // 효과 적용/저장/onApply 는 닫을 때(Close) 처리 — onApply 가 채용 후보 리스트처럼
    // "다음 UI" 를 띄우는 경우 다이얼로그가 닫힌 뒤에 떠야 하기 때문.
    void OnTypingDone()
    {
        if (_step != Step.Typing) return;
        _step = Step.Result;

        // 호환 모드: 결과/효과는 onConfirm 담당 → ResultBG 안 띄움. 한 번 더 클릭하면 Close.
        if (_simpleMode) return;

        // 데이터 모드: 결과 텍스트는 이미 세팅돼 있으니 ResultBG 슬라이드 인만.
        if (_hasResult && resultBG != null)
            StartCoroutine(SlideTo(resultBG, _resultShown, slideDuration));
    }

    // ── 닫기 ──────────────────────────────────────────────────
    void Close()
    {
        // Unregister 시 대기 큐의 다음 모달이 즉시 표시되며 상태가 덮어써질 수 있으므로 먼저 캡처.
        bool simple          = _simpleMode;
        bool resumeDev       = _resumeDev;
        var  simpleOnConfirm = _simpleOnConfirm;
        var  evt             = _currentEvent;

        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        if (_flowCo != null) { StopCoroutine(_flowCo); _flowCo = null; }
        _isTyping = false;
        _step = Step.Idle;
        _simpleOnConfirm = null;
        _currentEvent = null;

        if (eventPanel != null) eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);

        if (simple)
        {
            // 호환 모드: 시간 재개 후 onConfirm (효과/AlertUI 등)
            GameTimeManager.Instance?.StartTime();
            simpleOnConfirm?.Invoke();
            return;
        }

        // 데이터 모드: 효과 적용 + 저장 (패널 닫은 뒤 — onApply 가 채용 후보 리스트 등 다음 UI 를 띄울 수 있어
        // 다이얼로그가 사라진 다음에 실행돼야 함). EventUI 원본 OnClickConfirm 과 동일한 순서.
        evt?.onApply?.Invoke();
        ProjectSaveManager.Instance?.SaveProject();
        GameTimeManager.Instance?.SaveGameTime();
        EmployeeManager.Instance?.SaveAllEmployees();

        // 개발중 이벤트면 개발 재개 (ForceStartTime)
        if (resumeDev) DevelopmentManager.Instance?.ResumeFromEvent();
    }

    // ── 슬라이드 ──────────────────────────────────────────────
    IEnumerator SlideTo(RectTransform rect, Vector2 end, float duration)
    {
        Vector2 start = rect.anchoredPosition;
        float dur = Mathf.Max(0.0001f, duration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - (1f - k) * (1f - k); // ease-out
            rect.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        rect.anchoredPosition = end;
    }
}
