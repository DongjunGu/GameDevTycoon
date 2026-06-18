using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class RandomEventChoiceUI : MonoBehaviour
{
    public static RandomEventChoiceUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public Image portraitImage;
    public Image portraitImage2;

    [Header("Buttons")]
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public Button confirmButton;
    [Tooltip("선택지 클릭 시 RandomEventChoiceBtn 이미지로 교체할 스프라이트")]
    public Sprite choiceSelectedSprite;

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

    // ── 초기화 ──────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
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

        SetPortrait(data.portraitId);
        SetPortrait2(data.portraitId2);

        // 초상화 2개 모두 활성(두 명)이면 말하는 주체 강조 — 처음엔 portrait1 이 화자.
        InitSpeakerEmphasis(initialSpeaker: 1);

        ClearChoiceButtons();
        confirmButton.gameObject.SetActive(false);
        confirmButton.interactable = false;

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        // description 타이핑 → 완료 후 "클릭해야" 선택지 버튼 표시 (Update 의 reveal 클릭이 SpawnChoiceButtons 호출).
        // 선택지가 없으면(정보성 이벤트 — ChoiceButtonContainer 미사용) 확인 버튼을 바로 노출해 닫을 수 있게 함.
        StartTyping(data.description, onComplete: () =>
        {
            if (data.choices != null && data.choices.Count > 0)
            {
                _pendingChoices       = data.choices;
                _awaitingChoiceReveal = true; // 클릭 대기 — Update 에서 클릭 시 SpawnChoiceButtons
            }
            else
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }
        });
    }

    // 타이핑 중 화면 어디를 클릭하든 즉시 완성 (New Input System — Mouse/Touch).
    // 타이핑 완료 후에는 무동작(선택지/확인 버튼이 진행 담당).
    void Update()
    {
        if (eventPanel == null || !eventPanel.activeSelf) return;

        // 타이핑 중: 클릭하면 즉시 완성 (선택지는 아직 표시 안 함).
        if (!_isTypingDone)
        {
            if (ClickedThisFrame()) SkipTyping();
            return;
        }

        // 타이핑 완료 후: 선택지 공개 대기 중이면 클릭해야 선택지 표시.
        if (_awaitingChoiceReveal && ClickedThisFrame())
        {
            _awaitingChoiceReveal = false;
            SpawnChoiceButtons(_pendingChoices);
            _pendingChoices = null;
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
            StartTyping(secDesc, onComplete: () =>
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            });
            return;
        }

        // Unregister 시 대기 큐의 다음 모달이 즉시 표시되며 _currentData 가 바뀔 수 있으므로 먼저 캡처.
        var    onConf = _currentData?.onConfirm;
        string sysMsg = _chosenSystemMessage;

        eventPanel.SetActive(false);
        ModalGate.I.Unregister(this);

        MoneyManager.Instance.SaveMoney();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        System.Action resume = onConf ?? (() => DevelopmentManager.Instance.ResumeFromEvent());

        if (!string.IsNullOrEmpty(sysMsg))
            AlertUI.Instance.Show(sysMsg, resume);
        else
            resume();
    }

    // ── 선택지 ──────────────────────────────────────────────────
    void SpawnChoiceButtons(List<RandomEventChoiceOption> choices)
    {
        ClearChoiceButtons();

        // 선택지 표시 시: 화자 강조 해제 — 두 초상 모두 원상복구(scale 1 / 흰색) + 두 대사 박스 비활성화.
        if (_speakerAnimCo != null) { StopCoroutine(_speakerAnimCo); _speakerAnimCo = null; }
        ResetPortrait(portraitImage);
        ResetPortrait(portraitImage2);
        if (dialogBoxImage1 != null) dialogBoxImage1.SetActive(false);
        if (dialogBoxImage2 != null) dialogBoxImage2.SetActive(false);

        foreach (var choice in choices)
        {
            var go  = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = choice.buttonLabel; lbl.raycastTarget = false; } // 글자 영역 클릭도 selectBtn 으로 통과

            var rootImg    = go.GetComponent<Image>();   // 비주얼(스프라이트 전환) 대상 = 루트 RandomEventChoiceBtn
            var btn        = GetSelectButton(go);          // 실제 클릭 = 자식 selectBtn 의 Button
            var captured   = choice;
            var capturedGo = go;
            if (btn != null)
            {
                // selectBtn 이 버튼(937×104) 전체를 덮도록 stretch-fill → 가장자리까지 클릭됨.
                var srt = btn.GetComponent<RectTransform>();
                if (srt != null)
                {
                    srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
                    srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
                }
                // selectBtn 자체가 raycast 를 받도록 Image(투명이어도) 보장.
                var srcImg = btn.GetComponent<Image>();
                if (srcImg == null) srcImg = btn.gameObject.AddComponent<Image>();
                srcImg.color = new Color(1f, 1f, 1f, 0f); // 투명 — 비주얼은 루트, 클릭만 받음
                srcImg.raycastTarget = true;

                // SpriteSwap 전환(눌림/하이라이트/비활성)이 루트 이미지에 적용되도록 targetGraphic 을 루트로.
                if (rootImg != null) btn.targetGraphic = rootImg;
                btn.interactable = !choice.disabled;
                if (!choice.disabled)
                {
                    // 누르는 동안 글씨 검정, 떼면 하양 — EventTrigger 는 클릭 대상(selectBtn)에 부착, 글씨는 루트 라벨.
                    var et   = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
                    var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    down.callback.AddListener(_ => SetLabelColor(capturedGo, Color.black));
                    et.triggers.Add(down);
                    var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                    up.callback.AddListener(_ => SetLabelColor(capturedGo, Color.white));
                    et.triggers.Add(up);

                    btn.onClick.AddListener(() => OnChoiceClicked(captured, capturedGo));
                }
            }

            _spawnedButtons.Add(go);
        }
    }

    // 선택지 클릭 버튼 — 프리팹 자식 "selectBtn" 의 Button (없으면 자식에서 첫 Button 폴백).
    Button GetSelectButton(GameObject go)
    {
        if (go == null) return null;
        var t = go.transform.Find("selectBtn");
        if (t != null) { var b = t.GetComponent<Button>(); if (b != null) return b; }
        return go.GetComponentInChildren<Button>(true);
    }

    // 선택지 클릭 → 글씨 검정으로 바꾸고 0.3초 유지한 뒤 실제 선택 처리.
    void OnChoiceClicked(RandomEventChoiceOption choice, GameObject go)
    {

        // 0.3초 동안 중복 클릭 방지 — 각 선택지의 selectBtn 비활성화
        foreach (var b in _spawnedButtons)
        {
            var bt = GetSelectButton(b);
            if (bt != null) bt.interactable = false;
        }

        // 선택한 버튼: 글씨 검정 + 루트 RandomEventChoiceBtn 이미지를 지정 스프라이트로 고정 (0.3초 유지)
        if (go != null)
        {
            var btn = GetSelectButton(go);
            var img = go.GetComponent<Image>();      // 비주얼 = 루트 이미지
            if (btn != null) btn.enabled = false;    // SpriteSwap transition 정지 → 코드 스프라이트 유지
            if (img != null && choiceSelectedSprite != null) img.sprite = choiceSelectedSprite;
            SetLabelColor(go, Color.black);
        }
        StartCoroutine(SelectAfterDelay(choice));
    }

    IEnumerator SelectAfterDelay(RandomEventChoiceOption choice)
    {
        yield return new WaitForSecondsRealtime(0.3f);   // 검정 글씨 + pressed 이미지 유지 시간
        OnChoiceSelected(choice);
    }

    void SetLabelColor(GameObject go, Color color)
    {
        if (go == null) return;
        var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null) lbl.color = color;
    }

    void OnChoiceSelected(RandomEventChoiceOption choice)
    {
        _chosenOption     = choice;
        _inSecondaryPhase = false;

        // 선택지 버튼 모두 숨김
        foreach (var go in _spawnedButtons)
            go.SetActive(false);

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

        // resultDescription 타이핑 → 완료 후 confirm 활성화
        string resultDesc;
        if (choice.resultDescriptions != null && choice.resultDescriptions.Count > 0)
            resultDesc = choice.resultDescriptions[UnityEngine.Random.Range(0, choice.resultDescriptions.Count)];
        else if (!string.IsNullOrEmpty(choice.resultDescription))
            resultDesc = choice.resultDescription;
        else
            resultDesc = _currentData.description;

        StartTyping(resultDesc, onComplete: () =>
        {
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = true;
        });
    }

    void ClearChoiceButtons()
    {
        foreach (var go in _spawnedButtons) Destroy(go);
        _spawnedButtons.Clear();
    }

    // ── 타이핑 ──────────────────────────────────────────────────
    void StartTyping(string text, System.Action onComplete = null)
    {
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
            yield return new WaitForSeconds(_typingSpeed);
        }

        _isTypingDone     = true;
        _typingOnComplete = null;
        onComplete?.Invoke();
    }

    void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        if (_typingTarget != null) _typingTarget.text = _currentFullText;
        _isTypingDone = true;

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
