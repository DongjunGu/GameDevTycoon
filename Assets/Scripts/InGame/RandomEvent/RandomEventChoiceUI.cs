using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

public class RandomEventChoiceUI : MonoBehaviour
{
    public static RandomEventChoiceUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Tooltip("사무실 레벨(GridManager.CurrentOfficeLevel)에 따라 바뀌는 배경 — Resources/Dialog/BG_Office_Lv{N}")]
    public Image backgroundImage;

    [Header("UI")]
    public Image portraitImage;
    public Image portraitImage2;

    [Header("질문 (신규)")]
    [Tooltip("대사1(+대사2) 타이핑 완료 후 즉시 표시되는 질문 텍스트 — CSV '질문' 컬럼")]
    public TextMeshProUGUI questionText;

    [Header("Buttons")]
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public Button confirmButton;

    [Header("타이핑 속도 (초/글자)")]
    [SerializeField] private float _typingSpeed = 0.05f;

    [Header("두 명 강조 (말하는 주체)")]
    [Tooltip("말풍선 박스 — portrait1 이 화자일 때 활성")]
    public GameObject dialogBoxImage1;
    [Tooltip("말풍선 박스 — portrait2 가 화자일 때 활성")]
    public GameObject dialogBoxImage2;
    [Tooltip("DialogBoxImage1 자식 — portrait1 의 이름/대사")]
    public TextMeshProUGUI nameText1;
    public TextMeshProUGUI descriptionText1;
    [Tooltip("DialogBoxImage2 자식 — portrait2 의 이름/대사")]
    public TextMeshProUGUI nameText2;
    public TextMeshProUGUI descriptionText2;

    [Header("NamePanel 이미지 — 캐릭터(portraitId)별로 교체 (RandomEventUI와 동일 SO)")]
    public Image namePanelImage1;
    public Image namePanelImage2;
    public CharacterNamePanelSet namePanelSpriteSet;

    [Header("진행 인디케이터 — 대사 타이핑 완료 시 등장 + 위아래 호버 연출 (박스별)")]
    public RectTransform indicatorImage1;
    public RectTransform indicatorImage2;
    public float indicatorHoverAmplitude = 8f;
    public float indicatorHoverSpeed = 4f;

    [Tooltip("돈이 드는 선택지가 있을 때만 활성화 — ConfirmPanelMoneyElevator 부착된 트리거 오브젝트(MoneyPanel을 다이얼로그 위로 끌어올림)")]
    public GameObject moneyElevatorTrigger;

    [Tooltip("선택지 표시 중 배경 딤 — 선택지 뜰 때 활성화, 패널 닫힐 때 비활성화")]
    public GameObject choiceDim;

    // 스킵 버튼 자신에는 Button을 달아도 안전 — DialogBoxImage1/2는 코드가 anchoredPosition을 직접
    // 애니메이션시키는 대상이 아니라 SetActive만 토글하므로, GlobalButtonClickBounce의 wrapper 삽입이
    // 좌표계를 깨뜨릴 걱정이 없다([[feedback_global_click_bounce_pitfalls]] 6번 항목과 다른 케이스).
    [Header("스킵 버튼 — 3배속 타이핑 + 클릭 대기 단계 자동 진행(선택지/확인은 유저가 직접). 이번 이벤트 1회만 유지")]
    public Button skipButton1;
    public Button skipButton2;
    public float skipSpeedMultiplier = 3f;
    [Tooltip("말하지 않는 초상화의 scale (화자는 1 고정)")]
    [SerializeField] private float _nonSpeakerScale = 0.85f;
    [Tooltip("말하지 않는 초상화의 색 (RGB+alpha 통합). 화자는 흰색/255")]
    [SerializeField] private Color _nonSpeakerColor = new Color(111f / 255f, 111f / 255f, 111f / 255f, 225f / 255f); // #6F6F6F, a225
    [Tooltip("화자 전환 시 보간 시간(초)")]
    [SerializeField] private float _speakerAnimDuration = 0.5f;

    private bool _twoPerson;            // 초상화 2개 모두 활성(두 명 이벤트)일 때만 강조 동작
    private Coroutine _speakerAnimCo;
    private int    _speakerSide = 1;    // 현재 말하는 쪽 (1=portrait1/왼쪽, 2=portrait2/오른쪽)
    private string _leftName  = "";     // portrait1 캐릭터 이름 (패널 숨겨도 보관)
    private string _rightName = "";     // portrait2 캐릭터 이름

    // ── 내부 상태 ────────────────────────────────────────────────
    private RandomEventChoiceData _currentData;
    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
    private Coroutine _choiceFadeInCoroutine; // 선택지 버튼 순차 페이드인(위→아래)

    private Coroutine       _typingCoroutine;
    private bool            _isTypingDone;
    private string          _currentFullText;
    private System.Action   _typingOnComplete;
    private TextMeshProUGUI _typingTarget;     // 이번 타이핑이 출력될 대상(화자 박스의 descriptionText)

    private string                 _chosenSystemMessage;
    private RandomEventChoiceOption _chosenOption;
    private bool                   _inSecondaryPhase;

    // 대사 타이핑이 끝난 뒤 "클릭해야" 선택지가 뜨도록 하는 대기 상태 + 보류된 선택지 목록.
    private bool _awaitingChoiceReveal;
    private List<RandomEventChoiceOption> _pendingChoices;

    // 대사1(description) 타이핑이 끝난 뒤 "클릭해야" 대사2(dialogue2)가 시작되도록 하는 대기 상태.
    // 화자는 절대 전환하지 않는다(같은 화자가 이어서 말하는 것으로 취급) — 클릭 한 번으로 대사가
    // 두 줄로 나뉘어 표시된다.
    private bool _awaitingDialogue2;
    private RandomEventChoiceData _pendingDialogue2Data;

    // 답변1(reply1) 타이핑이 끝난 뒤 "클릭해야" 답변2(reply2) 타이핑이 시작되도록 하는 대기 상태.
    private bool _awaitingReply2;
    private RandomEventChoiceOption _pendingReply2Choice;

    // 패널이 활성화된 바로 그 프레임의 클릭은 무시 — 직전에 다른 모달(채용 완료 버튼 등)을 닫은 클릭이
    // ModalGate 큐를 타고 같은 프레임에 이 패널을 띄우면서 그대로 스킵/선택 입력으로 새어들어오는 것을 방지.
    private int _shownFrame = -1;

    private bool   _skipMode; // 이번 이벤트 표시 동안만 유지 — DisplayInternal 에서 리셋
    private Vector2 _indicatorBasePos1, _indicatorBasePos2; // 인디케이터 호버 연출 기준 위치

    // ── 초기화 ──────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
        if (moneyElevatorTrigger != null) moneyElevatorTrigger.SetActive(false);
        if (choiceDim != null) choiceDim.SetActive(false);

        if (indicatorImage1 != null) _indicatorBasePos1 = indicatorImage1.anchoredPosition;
        if (indicatorImage2 != null) _indicatorBasePos2 = indicatorImage2.anchoredPosition;

        if (skipButton1 != null)
        {
            skipButton1.onClick.RemoveListener(OnSkipClicked);
            skipButton1.onClick.AddListener(OnSkipClicked);
        }
        if (skipButton2 != null)
        {
            skipButton2.onClick.RemoveListener(OnSkipClicked);
            skipButton2.onClick.AddListener(OnSkipClicked);
        }
    }

    // 스킵 버튼 — 이번 이벤트만 타이핑 3배속 + "클릭해야 진행되는" 대기 단계를 자동으로 통과.
    // 실제 선택지 고르기/확인 버튼 자체는 유저 결정이 필요한 지점이라 자동 클릭하지 않음 — 단,
    // 선택지 "공개"(_awaitingChoiceReveal)나 정보성 이벤트의 확인 버튼처럼 순수 "다음"류 대기는 넘겨준다.
    void OnSkipClicked()
    {
        _skipMode = true;
        if (!_isTypingDone) return; // 타이핑 가속 자체는 TypeText 루프가 매 글자 체크해서 처리

        if (_awaitingChoiceReveal)
        {
            _awaitingChoiceReveal = false;
            var choices = _pendingChoices;
            _pendingChoices = null;
            SpawnChoiceButtons(choices);
        }
        else if (_awaitingDialogue2)
        {
            _awaitingDialogue2 = false;
            var data = _pendingDialogue2Data;
            _pendingDialogue2Data = null;
            StartTyping(data.dialogue2, onComplete: () => ShowQuestionThenRevealChoices(data));
        }
        else if (_awaitingReply2)
        {
            _awaitingReply2 = false;
            var choice = _pendingReply2Choice;
            _pendingReply2Choice = null;
            StartTyping(choice.reply2, onComplete: EnableConfirm);
        }
        else if (confirmButton != null && confirmButton.gameObject.activeSelf && confirmButton.interactable)
        {
            OnClickConfirm();
        }
    }

    // 현재 화자 쪽 인디케이터. 박스 미배선 시 null.
    RectTransform ActiveIndicator()
    {
        int s = _twoPerson
            ? _speakerSide
            : (portraitImage2 != null && portraitImage2.gameObject.activeSelf ? 2 : 1);
        return (s == 2) ? indicatorImage2 : indicatorImage1;
    }

    void ShowActiveIndicator()
    {
        var ind = ActiveIndicator();
        if (ind != null) ind.gameObject.SetActive(true);
    }

    void HideIndicators()
    {
        if (indicatorImage1 != null) indicatorImage1.gameObject.SetActive(false);
        if (indicatorImage2 != null) indicatorImage2.gameObject.SetActive(false);
    }

    // 활성화된 인디케이터만 위아래로 부드럽게 왕복.
    void BobIndicator(RectTransform ind, Vector2 basePos)
    {
        if (ind == null || !ind.gameObject.activeSelf) return;
        float y = basePos.y + Mathf.Sin(Time.unscaledTime * indicatorHoverSpeed) * indicatorHoverAmplitude;
        ind.anchoredPosition = new Vector2(basePos.x, y);
    }

    // ── 공개 API ─────────────────────────────────────────────────
    // 다른 모달(AlertUI / 다른 이벤트 패널)이 떠 있으면 닫힐 때까지 대기 후 표시 — 동시 표시·덮어쓰기 방지.
    public void Show(RandomEventChoiceData data)
    {
        ModalGate.I.WhenFree(() => DisplayInternal(data));
    }

    void DisplayInternal(RandomEventChoiceData data)
    {
        _currentData          = data;
        _chosenSystemMessage  = null;
        _chosenOption         = null;
        _inSecondaryPhase     = false;
        _awaitingChoiceReveal = false;
        _pendingChoices       = null;
        _awaitingReply2       = false;
        _pendingReply2Choice  = null;
        _skipMode             = false; // 스킵은 이번 이벤트 표시 한정 — 새 이벤트마다 초기화
        HideIndicators();

        SetPortrait(data.portraitId);
        SetPortrait2(data.portraitId2);

        // 배경 — 사무실 레벨에 따라 교체(현재 기준). 나중에 상황별 배경이 추가되면 이 자리에서 분기 추가.
        if (backgroundImage != null) backgroundImage.sprite = GridManager.LoadDialogBackgroundSprite();

        // 초상화 2개 모두 활성(두 명)이면 말하는 주체 강조 — 처음엔 portrait1 이 화자.
        InitSpeakerEmphasis(initialSpeaker: 1);

        if (questionText != null) { questionText.text = ""; questionText.gameObject.SetActive(false); }

        ClearChoiceButtons();
        confirmButton.gameObject.SetActive(false);
        confirmButton.interactable = false;

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);
        _shownFrame = Time.frameCount;

        // 대사1(description) 타이핑 → (대사2 있으면 "클릭해야" 이어서 타이핑) → 질문 즉시 표시 →
        // "클릭해야" 선택지 버튼 표시 (Update 의 reveal 클릭이 SpawnChoiceButtons 호출).
        StartTyping(data.description, onComplete: () => AwaitDialogue2OrQuestion(data));
    }

    // 대사2(dialogue2)가 있으면 클릭 대기 상태로 전환(Update/OnSkipClicked 에서 클릭 시 타이핑 시작),
    // 없으면 바로 질문 단계로. 화자는 절대 전환하지 않는다 — 같은 화자가 이어서 말하는 두 번째 대사로
    // 취급(예: AntiMintchoc처럼 한 사람 대사를 두 줄로 나눌 때). TwoEmpFight 계열은 onSetup에서 emp1/emp2
    // 포트레이트를 둘 다 채워 _twoPerson이 true가 되지만, 그건 "화자가 둘"이라는 뜻이 아니라 그냥 두
    // 직원의 초상화가 둘 다 존재한다는 뜻이라 dialogue2에서 SetSpeaker(2)를 하면 안 됨.
    void AwaitDialogue2OrQuestion(RandomEventChoiceData data)
    {
        if (!string.IsNullOrEmpty(data.dialogue2))
        {
            if (_skipMode)
            {
                StartTyping(data.dialogue2, onComplete: () => ShowQuestionThenRevealChoices(data));
            }
            else
            {
                _pendingDialogue2Data = data;
                _awaitingDialogue2 = true; // 클릭 대기 — Update 에서 클릭 시 대사2 타이핑 시작
            }
        }
        else
        {
            ShowQuestionThenRevealChoices(data);
        }
    }

    // 질문(question) 텍스트를 미리 채워두되(타이핑 없음) 오브젝트는 비활성 유지 — 선택지가 실제로
    // 뜨는 시점(SpawnChoiceButtons)에만 활성화한다. 선택지가 있으면 클릭 대기 상태로 전환하고
    // 없으면(정보성 이벤트 — ChoiceButtonContainer 미사용) 확인 버튼을 바로 노출해 닫을 수 있게 함.
    void ShowQuestionThenRevealChoices(RandomEventChoiceData data)
    {
        if (questionText != null) questionText.text = data.question ?? "";

        if (data.choices != null && data.choices.Count > 0)
        {
            if (_skipMode)
            {
                SpawnChoiceButtons(data.choices); // 스킵 모드면 "클릭해야 공개" 대기 없이 바로 표시
            }
            else
            {
                _pendingChoices       = data.choices;
                _awaitingChoiceReveal = true; // 클릭 대기 — Update 에서 클릭 시 SpawnChoiceButtons
            }
        }
        else
        {
            EnableConfirm();
        }
    }

    // 타이핑 중 화면 어디를 클릭하든 즉시 완성 (New Input System — Mouse/Touch).
    // 타이핑 완료 후에는 무동작(선택지/확인 버튼이 진행 담당).
    void Update()
    {
        BobIndicator(indicatorImage1, _indicatorBasePos1);
        BobIndicator(indicatorImage2, _indicatorBasePos2);

        if (eventPanel == null || !eventPanel.activeSelf) return;
        if (Time.frameCount == _shownFrame) return; // 표시된 첫 프레임의 클릭(직전 모달 닫은 클릭)은 무시

        // 타이핑 중: 클릭하면 즉시 완성 (선택지는 아직 표시 안 함).
        if (!_isTypingDone)
        {
            if (ClickedThisFrame()) SkipTyping();
            return;
        }

        // 타이핑 완료 후: 대사2 대기 중이면 클릭해야 대사2 타이핑 시작.
        if (_awaitingDialogue2 && ClickedThisFrame())
        {
            _awaitingDialogue2 = false;
            var dialogue2Data = _pendingDialogue2Data;
            _pendingDialogue2Data = null;
            StartTyping(dialogue2Data.dialogue2, onComplete: () => ShowQuestionThenRevealChoices(dialogue2Data));
            return;
        }

        // 타이핑 완료 후: 선택지 공개 대기 중이면 클릭해야 선택지 표시.
        if (_awaitingChoiceReveal && ClickedThisFrame())
        {
            _awaitingChoiceReveal = false;
            SpawnChoiceButtons(_pendingChoices);
            _pendingChoices = null;
            return;
        }

        // 답변1 타이핑 완료 후: 답변2 대기 중이면 클릭해야 답변2 타이핑 시작.
        if (_awaitingReply2 && ClickedThisFrame())
        {
            _awaitingReply2 = false;
            var choice = _pendingReply2Choice;
            _pendingReply2Choice = null;
            StartTyping(choice.reply2, onComplete: EnableConfirm);
        }
    }

    static bool ClickedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    // description 영역 탭 → 타이핑 스킵 (전체화면 Update 와 별개로 유지 — 무해)
    public void OnClickDescription()
    {
        if (!_isTypingDone) SkipTyping();
    }

    // 확인 버튼
    public void OnClickConfirm()
    {
        // 2차 반응이 있으면 먼저 표시
        if (!_inSecondaryPhase && _chosenOption != null && _chosenOption.secondaryDescriptions.Count > 0)
        {
            _inSecondaryPhase = true;
            confirmButton.gameObject.SetActive(false);
            confirmButton.interactable = false;

            _chosenOption.onSecondaryShow?.Invoke();

            // 2차 결과: 두 명이면 둘 다 유지하고 말하는 주체만 전환(secondaryUsePortrait1이면 portrait1 화자).
            if (_twoPerson)
            {
                SetSpeaker(_chosenOption.secondaryUsePortrait1 ? 1 : 2);
            }
            else if (_chosenOption.secondaryUsePortrait1)
            {
                SetPortrait2(null);
                if (!string.IsNullOrEmpty(_chosenOption.secondaryPortraitId))
                    SetPortrait(_chosenOption.secondaryPortraitId);
            }
            else
            {
                SetPortrait(null);
                if (!string.IsNullOrEmpty(_chosenOption.secondaryPortraitId))
                    SetPortrait2(_chosenOption.secondaryPortraitId);
            }
            string secDesc = _chosenOption.secondaryDescriptions[
                UnityEngine.Random.Range(0, _chosenOption.secondaryDescriptions.Count)];
            StartTyping(secDesc, onComplete: EnableConfirm);
            return;
        }

        // Unregister 시 대기 큐의 다음 모달이 즉시 표시되며 _currentData 가 바뀔 수 있으므로 먼저 캡처.
        var    onConf      = _currentData?.onConfirm;
        var    chosen      = _chosenOption;
        string sysMsg      = _chosenSystemMessage;
        string targetEmpId = _currentData?.targetEmployeeId;
        string eventTitle  = _currentData?.title; // AlertPanel1 통합 결과팝업의 제목(이벤트 이름)으로 씀

        eventPanel.SetActive(false);
        if (moneyElevatorTrigger != null) moneyElevatorTrigger.SetActive(false);
        if (choiceDim != null) choiceDim.SetActive(false);
        ModalGate.I.Unregister(this);

        MoneyManager.Instance.SaveMoney();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        System.Action resume = onConf ?? (() => DevelopmentManager.Instance.ResumeFromEvent());

        // 결과 팝업 종류(resultPopupType)가 지정돼 있으면 AlertUI4/5/6 신규 라우팅, 없으면 기존 방식
        // (resultSystemMessage → 랜덤이벤트 결과 통합 패널) 로 fallback — 기존 이벤트 하위호환.
        if (chosen != null && chosen.resultPopupType > 0)
            ShowResultPopup(chosen, eventTitle, targetEmpId, resume);
        else if (!string.IsNullOrEmpty(sysMsg))
            AlertUI.Instance.ShowRandomEventResult(ResolvePlaceholders(eventTitle, targetEmpId), ResolvePlaceholders(sysMsg, targetEmpId), resume);
        else
            resume();
    }

    // 트리거 측(예: RandomEvents_Condition_Choice.TriggerCoffeeRequestEvent)이 onSetup에서 이미
    // "{직원이름}"을 치환해두는 게 정상 경로지만, 혹시 그 치환이 누락되거나 타이밍이 어긋나도 화면에
    // 플레이스홀더가 그대로 노출되는 사고를 막기 위한 최후 안전망 — 표시 직전에 한 번 더 치환한다.
    // 이미 치환된 문자열은 "{직원이름}"이 없어 그대로 통과(무해).
    static string ResolvePlaceholders(string text, string employeeId)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{직원이름}")) return text;
        string name = EmployeeManager.Instance?.GetEmployee(employeeId)?.employeeName ?? "";
        return text.Replace("{직원이름}", name);
    }

    // 결과 팝업 종류 1=AlertUI4(제목+3줄) / 2=AlertUI5(제목+1줄) / 3=AlertUI6(제목+2줄). 제목은 항상
    // 이벤트 이름(eventTitle, RandomEventChoiceData.title) — 2026-08-20부터 결과멘트1을 더 이상 제목
    // 자리에 안 쓰고 전부 본문으로 내림([[project_alertui_consolidation]]).
    // resultMent1 이 비어있으면(예: 유튜버 선공개 "애매한 반응" 분기 — 원래 팝업 없음) 그냥 넘어간다.
    // 1차 팝업 확인 후 resultPopupType2 가 지정돼 있으면 이어서 2차 팝업(예: 패자 효과)을 띄운다.
    // employeeId: ResolvePlaceholders 안전망용 — "{직원이름}"이 남아있을 때만 사용됨.
    static void ShowResultPopup(RandomEventChoiceOption choice, string eventTitle, string employeeId, System.Action resume)
    {
        System.Action afterFirst = () => ShowResultPopup2(choice, eventTitle, employeeId, resume);

        if (string.IsNullOrEmpty(choice.resultMent1)) { afterFirst(); return; }

        string ment1 = ResolvePlaceholders(choice.resultMent1, employeeId);
        string ment2 = ResolvePlaceholders(choice.resultMent2, employeeId);
        string ment3 = ResolvePlaceholders(choice.resultMent3, employeeId);
        string title = ResolvePlaceholders(eventTitle, employeeId);

        switch (choice.resultPopupType)
        {
            case 1: AlertUI.Instance.ShowResult4(title, ment1, ment2, ment3, afterFirst); break;
            case 2: AlertUI.Instance.ShowResult5(title, ment1, afterFirst); break;
            case 3: AlertUI.Instance.ShowResult6(title, ment1, ment2, afterFirst); break;
            default: afterFirst(); break;
        }
    }

    static void ShowResultPopup2(RandomEventChoiceOption choice, string eventTitle, string employeeId, System.Action resume)
    {
        if (string.IsNullOrEmpty(choice.resultMent1_2)) { resume(); return; }

        string ment1 = ResolvePlaceholders(choice.resultMent1_2, employeeId);
        string ment2 = ResolvePlaceholders(choice.resultMent2_2, employeeId);
        string ment3 = ResolvePlaceholders(choice.resultMent3_2, employeeId);
        string title = ResolvePlaceholders(eventTitle, employeeId);

        switch (choice.resultPopupType2)
        {
            case 1: AlertUI.Instance.ShowResult4(title, ment1, ment2, ment3, resume); break;
            case 2: AlertUI.Instance.ShowResult5(title, ment1, resume); break;
            case 3: AlertUI.Instance.ShowResult6(title, ment1, ment2, resume); break;
            default: resume(); break;
        }
    }

    // ── 선택지 ──────────────────────────────────────────────────
    void SpawnChoiceButtons(List<RandomEventChoiceOption> choices)
    {
        ClearChoiceButtons();

        // 질문(QuestionText)은 선택지가 실제로 뜨는 이 시점에만 활성화 — 알파 0→1 페이드인.
        ShowQuestionText();

        // 선택지 표시 시: 화자 강조 해제 — 두 초상 모두 원상복구(scale 1 / 흰색) + 두 대사 박스 비활성화.
        if (_speakerAnimCo != null) { StopCoroutine(_speakerAnimCo); _speakerAnimCo = null; }
        ResetPortrait(portraitImage);
        ResetPortrait(portraitImage2);
        if (dialogBoxImage1 != null) dialogBoxImage1.SetActive(false);
        if (dialogBoxImage2 != null) dialogBoxImage2.SetActive(false);

        // 돈이 드는 선택지가 하나라도 있으면 MoneyPanel을 다이얼로그 위로 끌어올려 잔액을 보여준다.
        if (moneyElevatorTrigger != null)
            moneyElevatorTrigger.SetActive(choices.Exists(c => !string.IsNullOrEmpty(c.conditionText)));

        // 선택지가 뜨는 동안 배경 딤 — 확인/닫힘 시점에 비활성화.
        if (choiceDim != null) choiceDim.SetActive(true);

        foreach (var choice in choices)
        {
            var go  = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = choice.buttonLabel; lbl.raycastTarget = false; } // 글자 영역 클릭도 루트 버튼으로 통과

            var conditionPanel = go.transform.Find("ConditionPanel");
            if (conditionPanel != null)
            {
                bool hasCondition = !string.IsNullOrEmpty(choice.conditionText);
                conditionPanel.gameObject.SetActive(hasCondition);
                if (hasCondition)
                {
                    var condText = conditionPanel.GetComponentInChildren<TextMeshProUGUI>();
                    if (condText != null) condText.text = choice.conditionText;
                }
            }

            // selectBtn 자식 없이 루트(RandomEventChoiceBtn) 자신의 Button/Image 로 클릭+ColorTint 모두 처리.
            // targetGraphic 은 프리팹에서 이미 자기 자신(루트 Image)으로 배선돼 있음 — GlobalButtonClickBounce 가
            // 이 Button 을 직접 감싸 눌림 시 루트(= 보이는 그래픽 전체)가 축소되므로 펀치 연출도 실제로 보인다.
            var btn      = go.GetComponent<Button>();
            var captured = choice;
            if (btn != null)
            {
                btn.interactable = !choice.disabled;
                if (!choice.disabled)
                {
                    btn.onClick.AddListener(() => OnChoiceClicked(captured));
                }
            }

            // 순차 페이드인 전까지 숨김 — CanvasGroup 으로 버튼 전체(아이콘/텍스트/ConditionPanel 포함) 알파 일괄 제어.
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            _spawnedButtons.Add(go);
        }

        // 질문(QuestionText)이 먼저 보이고, 0.5초 뒤 버튼들이 위에 있는 자식부터 순서대로 알파 0→1 페이드인.
        _choiceFadeInCoroutine = StartCoroutine(FadeInChoiceButtonsSequentially());
    }

    // 질문 텍스트 활성화 + 알파 0→1 페이드인.
    void ShowQuestionText()
    {
        if (questionText == null) return;
        questionText.gameObject.SetActive(true);
        questionText.DOKill();
        var c = questionText.color; c.a = 0f; questionText.color = c;
        questionText.DOFade(1f, 0.3f).SetUpdate(true);
    }

    IEnumerator FadeInChoiceButtonsSequentially()
    {
        const float preDelay = 0.5f;  // QuestionText 등장 후 버튼 등장까지 대기
        const float stagger  = 0.08f; // 버튼 간 페이드 시작 간격
        const float duration = 0.25f; // 버튼 1개당 페이드인 시간

        yield return new WaitForSecondsRealtime(preDelay);

        foreach (var go in _spawnedButtons)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.DOFade(1f, duration).SetUpdate(true);
            yield return new WaitForSecondsRealtime(stagger);
        }
        _choiceFadeInCoroutine = null;
    }

    // 선택 확정 시 버튼들을 동시에 투명해지며 사라지게 함 — 페이드인이 아직 진행 중이었으면 이어서 페이드아웃.
    void FadeOutChoiceButtons()
    {
        const float duration = 0.2f;

        if (_choiceFadeInCoroutine != null) { StopCoroutine(_choiceFadeInCoroutine); _choiceFadeInCoroutine = null; }

        // 선택지가 사라지는 시점에 배경 딤도 같이 끔 — SpawnChoiceButtons가 다시 선택지를 띄울 때
        // choiceDim.SetActive(true)로 재활성화되므로 다음 선택지 단계에선 자연히 다시 켜진다.
        if (choiceDim != null) choiceDim.SetActive(false);

        foreach (var go in _spawnedButtons)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.DOKill();
            var target = go; // 클로저 캡처
            cg.DOFade(0f, duration).SetUpdate(true).OnComplete(() =>
            {
                if (target != null) target.SetActive(false);
            });
        }
    }

    // 선택지 클릭 → 0.3초 유지한 뒤 실제 선택 처리.
    void OnChoiceClicked(RandomEventChoiceOption choice)
    {
        // 0.3초 동안 중복 클릭 방지 — 각 선택지 루트의 Button 비활성화(ColorTint가 자동으로 Disabled 색 적용)
        foreach (var b in _spawnedButtons)
        {
            var bt = b.GetComponent<Button>();
            if (bt != null) bt.interactable = false;
        }

        StartCoroutine(SelectAfterDelay(choice));
    }

    IEnumerator SelectAfterDelay(RandomEventChoiceOption choice)
    {
        yield return new WaitForSecondsRealtime(0.3f);   // pressed 이미지(ColorTint) 유지 시간
        OnChoiceSelected(choice);
    }

    void OnChoiceSelected(RandomEventChoiceOption choice)
    {
        _chosenOption     = choice;
        _inSecondaryPhase = false;

        // 선택지 버튼 모두 투명해지며 사라짐 (질문 텍스트는 즉시 숨김 — 선택지 뜰 때만 활성)
        FadeOutChoiceButtons();
        if (questionText != null) questionText.gameObject.SetActive(false);

        // 1차 결과: 두 명이면 둘 다 유지하고 말하는 주체만 전환(resultPortraitId2면 portrait2 화자).
        // 한 명이면 기존대로 해당 초상화만 표시.
        if (_twoPerson)
        {
            SetSpeaker(!string.IsNullOrEmpty(choice.resultPortraitId2) ? 2 : 1);
        }
        else if (!string.IsNullOrEmpty(choice.resultPortraitId2))
        {
            SetPortrait(null);
            SetPortrait2(choice.resultPortraitId2);
        }
        else
        {
            SetPortrait2(null);
            if (!string.IsNullOrEmpty(choice.resultPortraitId))
                SetPortrait(choice.resultPortraitId);
        }

        // 효과 즉시 적용
        choice.onChoose?.Invoke();

        // 저장할 시스템 문구
        _chosenSystemMessage = choice.resultSystemMessage;

        // 답변1(reply1)이 있으면 신규 방식(답변1→답변2 순차 타이핑) 우선, 없으면 기존 랜덤배리언트 방식으로 fallback.
        if (!string.IsNullOrEmpty(choice.reply1))
        {
            StartTyping(choice.reply1, onComplete: () => OnReply1TypingDone(choice));
            return;
        }

        // resultDescription 타이핑 → 완료 후 confirm 활성화
        string resultDesc;
        if (choice.resultDescriptions != null && choice.resultDescriptions.Count > 0)
            resultDesc = choice.resultDescriptions[UnityEngine.Random.Range(0, choice.resultDescriptions.Count)];
        else if (!string.IsNullOrEmpty(choice.resultDescription))
            resultDesc = choice.resultDescription;
        else
            resultDesc = _currentData.description;

        StartTyping(resultDesc, onComplete: EnableConfirm);
    }

    // 답변1(reply1) 타이핑 완료 직후 — 답변2(reply2)가 있으면 곧바로 넘어가지 않고 "클릭해야" 시작되도록
    // 대기 상태로 전환(선택지 공개 대기 _awaitingChoiceReveal과 동일 패턴), 없으면 바로 confirm 활성화.
    // 스킵 모드면 대기 없이 바로 답변2 타이핑을 시작한다.
    void OnReply1TypingDone(RandomEventChoiceOption choice)
    {
        if (string.IsNullOrEmpty(choice.reply2))
        {
            EnableConfirm();
            return;
        }

        if (_skipMode)
        {
            StartTyping(choice.reply2, onComplete: EnableConfirm);
            return;
        }

        _pendingReply2Choice = choice;
        _awaitingReply2       = true; // 클릭 대기 — Update 에서 클릭 시 답변2 타이핑 시작
    }

    void EnableConfirm()
    {
        confirmButton.gameObject.SetActive(true);
        confirmButton.interactable = true;
        if (_skipMode) OnClickConfirm(); // 스킵 모드면 확인 버튼 대기 없이 바로 진행
    }

    void ClearChoiceButtons()
    {
        if (_choiceFadeInCoroutine != null) { StopCoroutine(_choiceFadeInCoroutine); _choiceFadeInCoroutine = null; }
        foreach (var go in _spawnedButtons)
        {
            if (go == null) continue;
            // GlobalButtonClickBounce 가 클릭 시 버튼을 "__ClickBounceWrapper" 로 감싸 그 부모 자리에 끼워 넣는다
            // (버튼은 래퍼의 자식으로 이동됨) — 버튼(go)만 지우면 빈 래퍼가 choiceButtonContainer 밑에 남으므로,
            // 래핑된 상태면(부모가 choiceButtonContainer 가 아니면) 래퍼째로 지운다.
            var parent = go.transform.parent;
            bool wrapped = parent != null && parent != choiceButtonContainer;
            Destroy(wrapped ? parent.gameObject : go);
        }
        _spawnedButtons.Clear();
    }

    // ── 타이핑 ──────────────────────────────────────────────────
    void StartTyping(string text, System.Action onComplete = null)
    {
        HideIndicators();
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _currentFullText  = text;
        _typingOnComplete = onComplete;
        _typingTarget     = ActiveDescription();   // 현재 화자 박스의 descriptionText
        _typingCoroutine  = StartCoroutine(TypeText(text, onComplete));
    }

    // 현재 말하는 쪽의 대사 출력 대상. 박스 미배선 시 단일 descriptionText 로 폴백.
    TextMeshProUGUI ActiveDescription()
    {
        int s = _twoPerson
            ? _speakerSide
            : (portraitImage2 != null && portraitImage2.gameObject.activeSelf ? 2 : 1);
        return (s == 2) ? descriptionText2 : descriptionText1;
    }

    IEnumerator TypeText(string text, System.Action onComplete)
    {
        _isTypingDone = false;
        var tgt = _typingTarget;
        if (tgt != null) tgt.text = "";

        foreach (char c in text)
        {
            if (tgt != null) tgt.text += c;
            float interval = _skipMode ? _typingSpeed / Mathf.Max(0.01f, skipSpeedMultiplier) : _typingSpeed;
            yield return new WaitForSeconds(interval);
        }

        _isTypingDone     = true;
        _typingOnComplete = null;
        ShowActiveIndicator();
        onComplete?.Invoke();
    }

    void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        if (_typingTarget != null) _typingTarget.text = _currentFullText;
        _isTypingDone = true;
        ShowActiveIndicator();

        var cb            = _typingOnComplete;
        _typingOnComplete = null;
        cb?.Invoke();
    }

    // ── 초상화 ──────────────────────────────────────────────────
    void SetPortrait(string portraitId)
    {
        if (portraitImage == null) return;
        Sprite portrait = !string.IsNullOrEmpty(portraitId)
            ? Resources.Load<Sprite>($"Portraits/{portraitId}")
            : null;
        portraitImage.sprite = portrait;
        portraitImage.gameObject.SetActive(portrait != null);
        _leftName = ResolveCharName(portrait != null ? portraitId : null);
        if (nameText1 != null) nameText1.text = _leftName;   // box1 이름
        CharacterNamePanelSet.Apply(namePanelImage1, namePanelSpriteSet, portraitId);
        RefreshNamePanels();
    }

    void SetPortrait2(string portraitId)
    {
        if (portraitImage2 == null) return;
        Sprite portrait = !string.IsNullOrEmpty(portraitId)
            ? Resources.Load<Sprite>($"Portraits/{portraitId}")
            : null;
        portraitImage2.sprite = portrait;
        portraitImage2.gameObject.SetActive(portrait != null);
        _rightName = ResolveCharName(portrait != null ? portraitId : null);
        if (nameText2 != null) nameText2.text = _rightName;   // box2 이름
        CharacterNamePanelSet.Apply(namePanelImage2, namePanelSpriteSet, portraitId);
        RefreshNamePanels();
    }

    // 말하는 쪽 namePanel 만 활성화. 두 명이면 _speakerSide, 한 명이면 보이는 초상화 기준.
    void RefreshNamePanels()
    {
        int speaker = _twoPerson
            ? _speakerSide
            : (portraitImage2 != null && portraitImage2.gameObject.activeSelf ? 2 : 1);

        // 말풍선 박스는 말하는 쪽만 활성 (두 명/한 명 모두 동일 기준)
        if (dialogBoxImage1 != null) dialogBoxImage1.SetActive(speaker == 1);
        if (dialogBoxImage2 != null) dialogBoxImage2.SetActive(speaker == 2);
    }

    string ResolveCharName(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return "";
        if (portraitId == "portrait_secretary") return "비서"; // 비서 NPC — EmployeeData 없음
        var em = EmployeeManager.Instance;
        if (em == null) return "";
        var e = em.ownedEmployees.Find(x => x != null && x.portraitId == portraitId);
        return e != null ? e.employeeName : "";
    }

    // ── 말하는 주체 강조 (두 명 이벤트) ───────────────────────────
    // 초상화 2개가 모두 활성일 때만 동작: 화자=scale 1·alpha 255, 비화자=scale 0.9·alpha 130.
    void InitSpeakerEmphasis(int initialSpeaker)
    {
        if (_speakerAnimCo != null) { StopCoroutine(_speakerAnimCo); _speakerAnimCo = null; }

        // 이전 이벤트 잔상 제거 — 기본값(scale 1 / alpha 255)으로 복구
        ResetPortrait(portraitImage);
        ResetPortrait(portraitImage2);

        _twoPerson = portraitImage  != null && portraitImage.gameObject.activeSelf
                  && portraitImage2 != null && portraitImage2.gameObject.activeSelf;

        if (_twoPerson) ApplySpeaker(initialSpeaker, animate: false); // 초기는 즉시 적용
        else RefreshNamePanels();                                     // 한 명: 보이는 쪽 이름만
    }

    // who: 1 = portrait1 화자, 2 = portrait2 화자. (두 명일 때만 동작 — 0.5초 보간)
    void SetSpeaker(int who) => ApplySpeaker(who, animate: true);

    void ApplySpeaker(int who, bool animate)
    {
        if (!_twoPerson) return;
        _speakerSide = who;
        RefreshNamePanels();                 // 말하는 쪽 이름 패널만 켜기
        bool oneSpeaks = who == 1;
        Image spk = oneSpeaks ? portraitImage  : portraitImage2;
        Image non = oneSpeaks ? portraitImage2 : portraitImage;

        if (_speakerAnimCo != null) { StopCoroutine(_speakerAnimCo); _speakerAnimCo = null; }

        if (animate)
        {
            _speakerAnimCo = StartCoroutine(AnimateSpeaker(spk, non));
        }
        else
        {
            SetPortraitState(spk, 1f, Color.white);            // 화자 — 흰색/255/scale1
            SetPortraitState(non, _nonSpeakerScale, _nonSpeakerColor); // 비화자 — #6F6F6F/a225/scale0.85
        }
    }

    IEnumerator AnimateSpeaker(Image spk, Image non)
    {
        Vector3 spkSFrom = spk.transform.localScale, nonSFrom = non.transform.localScale;
        Color   spkCFrom = spk.color,                nonCFrom = non.color;
        Vector3 spkSTo = Vector3.one,                nonSTo = Vector3.one * _nonSpeakerScale;
        Color   spkCTo = Color.white,                nonCTo = _nonSpeakerColor;

        float t = 0f;
        while (t < _speakerAnimDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / _speakerAnimDuration);
            spk.transform.localScale = Vector3.Lerp(spkSFrom, spkSTo, k);
            non.transform.localScale = Vector3.Lerp(nonSFrom, nonSTo, k);
            spk.color = Color.Lerp(spkCFrom, spkCTo, k);
            non.color = Color.Lerp(nonCFrom, nonCTo, k);
            yield return null;
        }
        spk.transform.localScale = spkSTo;
        non.transform.localScale = nonSTo;
        spk.color = spkCTo;
        non.color = nonCTo;
        _speakerAnimCo = null;
    }

    void ResetPortrait(Image img)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one;
        img.color = Color.white;
    }

    void SetPortraitState(Image img, float scale, Color color)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one * scale;
        img.color = color;
    }
}
