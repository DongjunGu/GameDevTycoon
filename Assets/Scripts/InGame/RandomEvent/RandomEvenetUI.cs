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

    [Tooltip("사무실 레벨(GridManager.CurrentOfficeLevel)에 따라 바뀌는 배경 — Resources/Dialog/BG_Office_Lv{N}")]
    public Image backgroundImage;

    [Header("Portrait / Dialog")]
    public Image portraitImage;
    public RectTransform dialogTextBG;       // 대사 배경 (슬라이드 대상)
    public GameObject namePanel;             // 이름 없을 때 숨김
    public Image namePanelImage;             // NamePanel 자체 이미지 — 캐릭터(portraitId)별로 교체
    public CharacterNamePanelSet namePanelSpriteSet;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;  // 대사 (DialogText) — 타이핑

    [Header("Title / Result (RightUpPanel)")]
    public RectTransform titleBG;            // 제목 배경 (슬라이드 대상)
    public Image titleBGImage;               // TitleBG 자체 이미지 — titleType(0=부정/1=중립/2=긍정)에 따라 교체
    public Sprite titleBGBadSprite;          // titleType == 0
    public Sprite titleBGNormalSprite;       // titleType == 1 또는 2 (중립/긍정 공용)
    public TextMeshProUGUI titleText;
    public RectTransform resultBG;           // 결과 배경 (슬라이드 대상)
    public TextMeshProUGUI resultText;       // 결과 (systemMessage)
    public RectTransform resultBG2;          // 결과2 배경 (슬라이드 대상)
    public TextMeshProUGUI resultText2;      // 결과2 (systemMessage2)

    [Header("Click")]
    public Button clickButton;               // 전체화면 진행/닫기 (IndicatorBtn)
    // DialogTextBG 안의 별도 자식(DialogTextClickCatcher, SkipButton 바로 뒤 형제) — SkipButton 영역
    // 제외하고 나머지 클릭을 clickButton과 동일하게 처리. DialogTextBG 자신에는 절대 Button을 달지
    // 말 것 — GlobalButtonClickBounce가 첫 클릭 시 그 RectTransform의 anchor/anchoredPosition을
    // 풀스트레치로 덮어써서, 이 스크립트가 슬라이드에 쓰는 좌표계 자체가 깨져 두 번째 이벤트부터
    // 위치가 틀어진다(경험담) — 그래서 애니메이션 대상과 무관한 자식에 Button을 분리해 붙인다.
    public Button dialogTextClickButton;

    [Header("진행 인디케이터 — 대사 타이핑 완료 시 등장 + 위아래 호버 연출")]
    public RectTransform indicatorImage;
    public float indicatorHoverAmplitude = 8f;
    public float indicatorHoverSpeed = 4f;

    [Header("스킵 버튼 — 3배속 타이핑 + 결과 확인 후 자동 닫기(이번 이벤트 1회만 유지)")]
    public Button skipButton;
    public float skipSpeedMultiplier = 3f;

    [Header("Slide")]
    public float portraitDelay  = 0.5f;      // 초상 표시 후 대사/제목 슬라이드까지 대기
    public float slideDuration  = 0.35f;     // 각 BG 슬라이드 시간 (실시간)
    public Vector2 dialogHiddenOffset  = new Vector2(0f, -500f);   // 대사 BG 숨김 오프셋 (아래)
    public Vector2 titleHiddenOffset   = new Vector2(1300f, 0f);   // 제목 BG 숨김 오프셋 (오른쪽)
    public Vector2 resultHiddenOffset  = new Vector2(1300f, 0f);   // 결과 BG 숨김 오프셋 (오른쪽)
    public Vector2 resultHiddenOffset2 = new Vector2(1300f, 0f);   // 결과2 BG 숨김 오프셋 (오른쪽)

    [Header("Typing")]
    public float typeInterval = 0.02f;       // 글자당 출력 간격 (실시간 — 시간정지 무관)

    // ── 내부 상태 ─────────────────────────────────────────────
    enum Step { Idle, Intro, Typing, Result }
    private Step _step = Step.Idle;

    private RandomEventData _currentEvent;
    private int _currentTitleType = 1; // 0=부정/1=중립/2=긍정 — BeginDisplay 에서 TitleBG 스프라이트에 반영
    private bool   _simpleMode;
    private System.Action _simpleOnConfirm;

    private string _resultMessage;            // 결과 텍스트 (systemMessage → ResultBG)
    private string _resultMessage2;           // 결과2 텍스트 (systemMessage2 → ResultBG2)
    private bool   _hasResult;
    private bool   _hasResult2;
    private bool   _resumeDev;               // 닫을 때 개발 재개 여부 (데이터 모드)

    private Coroutine _flowCo;
    private Coroutine _typeCo;
    private bool _isTyping;

    // 슬라이드 위치 캐시 (LayoutGroup 이 배치한 표시 위치)
    private bool _posCached;
    private Vector2 _dialogShown, _titleShown, _resultShown, _resultShown2;

    private bool _skipMode;             // 이번 이벤트 표시 동안만 유지 — Close()/BeginDisplay 에서 리셋
    private Vector2 _indicatorBasePos;  // 인디케이터 호버 연출 기준 위치

    // IndicatorBtn(clickButton)을 DialogTextBG/텍스트 등 모달 내 다른 UI보다 항상 위로, SkipButton은
    // 그보다 한 단계 더 위로 — sortingOrder만으로 강제해 어디를 눌러도(텍스트 위 포함) 클릭이 확실히
    // 먹히게 한다. ModalLayer가 매기는 기준 order는 모달 스택 상황에 따라 바뀌므로 매 프레임 따라간다
    // (바뀐 경우에만 실제로 적용 — 매 프레임 AddComponent 호출 방지).
    private ModalLayer _modalLayer;
    private Canvas _clickButtonCanvas, _skipButtonCanvas;
    private int _lastSyncedBaseOrder = int.MinValue;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (eventPanel != null) _modalLayer = eventPanel.GetComponent<ModalLayer>();
        if (indicatorImage != null) _indicatorBasePos = indicatorImage.anchoredPosition;

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipClicked);
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnScreenClick);
            clickButton.onClick.AddListener(OnScreenClick);
        }

        if (dialogTextClickButton != null)
        {
            dialogTextClickButton.onClick.RemoveListener(OnScreenClick);
            dialogTextClickButton.onClick.AddListener(OnScreenClick);
        }

        if (eventPanel != null) eventPanel.SetActive(false);
    }

    void Update()
    {
        // 인디케이터 호버 연출 — 활성화(타이핑 완료 시) 동안만 위아래로 부드럽게 왕복.
        if (indicatorImage != null && indicatorImage.gameObject.activeSelf)
        {
            float y = _indicatorBasePos.y + Mathf.Sin(Time.unscaledTime * indicatorHoverSpeed) * indicatorHoverAmplitude;
            indicatorImage.anchoredPosition = new Vector2(_indicatorBasePos.x, y);
        }

        if (eventPanel != null && eventPanel.activeInHierarchy) SyncTopmostOrdering();
    }

    // clickButton(IndicatorBtn)에 override-sorting Canvas를 달아 이 모달 안의 모든 UI(대사 텍스트 포함)
    // 보다 위로 올리고, skipButton은 그보다 한 단계 더 위로 — 이제 대사 박스 어디를 눌러도 텍스트 유무와
    // 무관하게 IndicatorBtn이 클릭을 받는다(DialogTextClickCatcher가 하던 역할을 대체).
    void SyncTopmostOrdering()
    {
        if (_modalLayer == null || clickButton == null) return;
        int baseOrder = _modalLayer.AssignedOrder;
        if (baseOrder == _lastSyncedBaseOrder) return;
        _lastSyncedBaseOrder = baseOrder;

        _clickButtonCanvas = EnsureOverrideCanvas(clickButton.transform, _clickButtonCanvas, baseOrder + 1);
        if (skipButton != null)
            _skipButtonCanvas = EnsureOverrideCanvas(skipButton.transform, _skipButtonCanvas, baseOrder + 2);
    }

    static Canvas EnsureOverrideCanvas(Transform t, Canvas cached, int order)
    {
        var canvas = cached != null ? cached : t.GetComponent<Canvas>();
        if (canvas == null) canvas = t.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = order;
        if (t.GetComponent<GraphicRaycaster>() == null) t.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // 스킵 버튼 — 이번 이벤트만 타이핑 3배속 + 결과까지 자동 진행. 이미 결과가 떠서 닫기만 남은
    // 상태(Step.Result)에서 누르면 그 마지막 클릭도 대신 처리해 바로 닫는다.
    void OnSkipClicked()
    {
        _skipMode = true;
        if (_step == Step.Result) Close();
    }

    // ── 진입점 ────────────────────────────────────────────────
    // 데이터 모드 — 다른 모달이 떠 있으면 닫힐 때까지 대기 후 표시.
    public void Show(RandomEventData evt)
    {
        ModalGate.I.WhenFree(() => DisplayData(evt));
    }

    // EventUI 호환 오버로드 — 대사만 타이핑, 효과/저장/재개는 onConfirm 담당.
    // titleType: 0=부정/1=중립/2=긍정 (기본 1=중립) — TitleBG 스프라이트 선택에만 쓰임.
    public void Show(string title, string portraitId, string message, System.Action onConfirm = null, int titleType = 1)
    {
        ModalGate.I.WhenFree(() => DisplaySimple(title, portraitId, message, null, null, onConfirm, titleType));
    }

    // systemMessage/systemMessage2 포함 오버로드 — 조건 이벤트 등 결과 텍스트가 있는 경우.
    public void Show(string title, string portraitId, string message, string systemMsg, string systemMsg2, System.Action onConfirm = null, int titleType = 1)
    {
        ModalGate.I.WhenFree(() => DisplaySimple(title, portraitId, message, systemMsg, systemMsg2, onConfirm, titleType));
    }

    void DisplayData(RandomEventData evt)
    {
        _currentEvent    = evt;
        _simpleMode      = false;
        _simpleOnConfirm = null;

        _resultMessage  = evt.systemMessage;
        _resultMessage2 = "";
        _hasResult      = !string.IsNullOrEmpty(_resultMessage);
        _hasResult2     = false;
        _resumeDev     = evt.type != RandomEventType.EmployeeRun
                      && evt.type != RandomEventType.EmployeeFight
                      && evt.type != RandomEventType.Recruit;
        _currentTitleType = evt.titleType;

        string speaker = ResolveSpeakerName(evt);
        BeginDisplay(speaker, evt.portraitId, evt.title, evt.description);
    }

    void DisplaySimple(string title, string portraitId, string message, string systemMsg, string systemMsg2, System.Action onConfirm, int titleType = 1)
    {
        _currentEvent    = null;
        _simpleMode      = true;
        _simpleOnConfirm = onConfirm;
        _currentTitleType = titleType;

        _resultMessage  = systemMsg  ?? "";
        _resultMessage2 = systemMsg2 ?? "";
        _hasResult      = !string.IsNullOrEmpty(_resultMessage);
        _hasResult2     = !string.IsNullOrEmpty(_resultMessage2);
        _resumeDev      = false;

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
        if (nameText   != null) nameText.text  = "";
        if (titleText  != null) titleText.text = "";
        if (resultText  != null) resultText.text  = "";
        if (resultText2 != null) resultText2.text = "";
        if (indicatorImage != null) indicatorImage.gameObject.SetActive(false);
        _skipMode = false; // 스킵은 이번 이벤트 표시 한정 — 새 이벤트마다 초기화

        // 캐시돼 있으면 활성화 전에 BG 를 숨김 위치로 미리 이동 (활성 첫 프레임 잔상 방지).
        if (_posCached)
        {
            if (dialogTextBG) dialogTextBG.anchoredPosition = _dialogShown  + dialogHiddenOffset;
            if (titleBG)      titleBG.anchoredPosition      = _titleShown   + titleHiddenOffset;
            if (resultBG)     resultBG.anchoredPosition     = _resultShown  + resultHiddenOffset;
            if (resultBG2)    resultBG2.anchoredPosition    = _resultShown2 + resultHiddenOffset2;
        }

        // 이름
        if (nameText  != null) nameText.text = speaker ?? "";
        if (namePanel != null) namePanel.SetActive(!string.IsNullOrEmpty(speaker));
        CharacterNamePanelSet.Apply(namePanelImage, namePanelSpriteSet, portraitId);

        // 제목 + TitleBG 이미지(titleType: 0=부정 전용 이미지 / 1·2=중립·긍정 공용 이미지)
        if (titleText != null) titleText.text = title ?? "";
        if (titleBGImage != null)
        {
            Sprite s = _currentTitleType == 0 ? titleBGBadSprite : titleBGNormalSprite;
            if (s != null) titleBGImage.sprite = s;
        }

        // 결과 (미리 채워두되 ResultBG 가 숨겨져 있어 보이지 않음)
        if (resultText  != null) resultText.text  = _resultMessage  ?? "";
        if (resultText2 != null) resultText2.text = _resultMessage2 ?? "";

        // 초상
        if (portraitImage != null)
        {
            Sprite portrait = !string.IsNullOrEmpty(portraitId)
                ? Resources.Load<Sprite>($"Portraits/{portraitId}") : null;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        // 배경 — 사무실 레벨에 따라 교체(현재 기준). 나중에 상황별 배경이 추가되면 이 자리에서 분기 추가.
        if (backgroundImage != null) backgroundImage.sprite = GridManager.LoadDialogBackgroundSprite();

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        CaptureShownPositions();

        // 모든 BG 를 숨김 위치로 (resultBG2 는 systemMessage2 가 없으면 완전 비활성)
        if (dialogTextBG) dialogTextBG.anchoredPosition = _dialogShown  + dialogHiddenOffset;
        if (titleBG)      titleBG.anchoredPosition      = _titleShown   + titleHiddenOffset;
        if (resultBG)     resultBG.anchoredPosition     = _resultShown  + resultHiddenOffset;
        if (resultBG2)
        {
            resultBG2.anchoredPosition = _resultShown2 + resultHiddenOffset2;
            resultBG2.gameObject.SetActive(false); // 필요 시 OnTypingDone 에서 활성화
        }

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
        foreach (var rt in new[] { dialogTextBG, titleBG, resultBG, resultBG2 })
        {
            if (rt == null) continue;
            var parent = rt.parent as RectTransform;
            if (parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            var g = rt.GetComponentInParent<LayoutGroup>();
            if (g != null) groups.Add(g);
        }

        if (dialogTextBG) _dialogShown  = dialogTextBG.anchoredPosition;
        if (titleBG)      _titleShown   = titleBG.anchoredPosition;
        if (resultBG)     _resultShown  = resultBG.anchoredPosition;
        if (resultBG2)    _resultShown2 = resultBG2.anchoredPosition;

        // 이후 anchoredPosition 직접 제어를 위해 그룹/LayoutElement 영향 제거
        foreach (var rt in new[] { dialogTextBG, titleBG, resultBG, resultBG2 })
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
        // BadReview(이유 없는 별점 1점): 화자는 이 사실을 보고하는 비서지 효과 대상 직원이 아님.
        // targetEmployeeId는 systemMessage({0} 만족도 -10) 포맷용으로만 쓰고 NameText는 항상 비서로 고정.
        if (evt.type != RandomEventType.BadReview && !string.IsNullOrEmpty(evt.targetEmployeeId))
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
            // 스킵 모드면 매 글자마다 다시 체크해 이미 타이핑 도중에 눌러도 그 지점부터 즉시 3배속 적용.
            float interval = _skipMode ? typeInterval / Mathf.Max(0.01f, skipSpeedMultiplier) : typeInterval;
            if (interval > 0f) yield return new WaitForSecondsRealtime(interval);
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

        // 타이핑이 끝났으니 "클릭해서 계속" 인디케이터 등장(호버 연출은 Update 에서).
        if (indicatorImage != null) indicatorImage.gameObject.SetActive(true);

        // 결과 BG 슬라이드 인 (메시지가 있는 경우 — 데이터 모드/호환 모드 공통)
        if (_hasResult  && resultBG  != null)
            StartCoroutine(SlideTo(resultBG,  _resultShown,  slideDuration));
        if (_hasResult2 && resultBG2 != null)
        {
            resultBG2.gameObject.SetActive(true);
            StartCoroutine(SlideTo(resultBG2, _resultShown2, slideDuration));
        }

        // 호환 모드이고 결과 메시지가 없으면 한 번 더 클릭 시 Close — 있으면 동일하게 Result step 유지.
        if (_simpleMode && !_hasResult && !_hasResult2) { /* Close는 다음 클릭에서 */ }

        // 스킵 모드면 결과 슬라이드가 화면에 다 나온 뒤(사용자가 그 짧은 순간이라도 보게) 곧바로 닫기까지 자동 처리.
        if (_skipMode) StartCoroutine(AutoCloseAfterResult());
    }

    IEnumerator AutoCloseAfterResult()
    {
        yield return new WaitForSecondsRealtime(slideDuration);
        if (_step == Step.Result) Close();
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
        _skipMode = false;
        if (indicatorImage != null) indicatorImage.gameObject.SetActive(false);

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
