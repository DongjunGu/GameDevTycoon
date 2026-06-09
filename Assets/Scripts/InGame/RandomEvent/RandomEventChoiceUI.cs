using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RandomEventChoiceUI : MonoBehaviour
{
    public static RandomEventChoiceUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Image portraitImage;
    public Image portraitImage2;

    [Header("Character Names")]
    public GameObject      leftCharNamePanel;   // portrait1(왼쪽) 캐릭터 이름 패널
    public TextMeshProUGUI leftCharNameText;
    public GameObject      rightCharNamePanel;  // portrait2(오른쪽) 캐릭터 이름 패널
    public TextMeshProUGUI rightCharNameText;

    [Header("Buttons")]
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public Button confirmButton;

    [Header("타이핑 속도 (초/글자)")]
    [SerializeField] private float _typingSpeed = 0.05f;

    [Header("두 명 강조 (말하는 주체)")]
    [Tooltip("말하지 않는 초상화의 scale (화자는 1 고정)")]
    [SerializeField] private float _nonSpeakerScale = 0.9f;
    [Tooltip("말하지 않는 초상화의 alpha (0~255). 화자는 255)")]
    [SerializeField] private float _nonSpeakerAlpha255 = 130f;
    [Tooltip("화자 전환 시 scale 보간 시간(초)")]
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

    private string                 _chosenSystemMessage;
    private RandomEventChoiceOption _chosenOption;
    private bool                   _inSecondaryPhase;

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
        _currentData         = data;
        _chosenSystemMessage = null;
        _chosenOption        = null;
        _inSecondaryPhase    = false;

        titleText.text = data.title;
        SetPortrait(data.portraitId);
        SetPortrait2(data.portraitId2);

        // 초상화 2개 모두 활성(두 명)이면 말하는 주체 강조 — 처음엔 portrait1 이 화자.
        InitSpeakerEmphasis(initialSpeaker: 1);

        ClearChoiceButtons();
        confirmButton.gameObject.SetActive(false);
        confirmButton.interactable = false;

        eventPanel.SetActive(true);
        ModalGate.I.Register(this);

        // description 타이핑 → 완료 후 선택지 버튼 표시.
        // 선택지가 없으면(정보성 이벤트 — ChoiceButtonContainer 미사용) 확인 버튼을 바로 노출해 닫을 수 있게 함.
        StartTyping(data.description, onComplete: () =>
        {
            if (data.choices != null && data.choices.Count > 0)
            {
                SpawnChoiceButtons(data.choices);
            }
            else
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }
        });
    }

    // description 영역 탭 → 타이핑 스킵
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
            if (!string.IsNullOrEmpty(_chosenOption.secondaryTitle))
                titleText.text = _chosenOption.secondaryTitle;

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

        foreach (var choice in choices)
        {
            var go  = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = choice.buttonLabel;

            var btn      = go.GetComponent<Button>();
            var captured = choice;
            btn.interactable = !choice.disabled;
            if (!choice.disabled)
                btn.onClick.AddListener(() => OnChoiceSelected(captured));

            _spawnedButtons.Add(go);
        }
    }

    void OnChoiceSelected(RandomEventChoiceOption choice)
    {
        _chosenOption     = choice;
        _inSecondaryPhase = false;

        // 선택지 버튼 숨기기
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

        // 제목 교체 (null이면 유지)
        if (choice.resultTitle != null)
            titleText.text = choice.resultTitle;

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
        _typingCoroutine  = StartCoroutine(TypeText(text, onComplete));
    }

    IEnumerator TypeText(string text, System.Action onComplete)
    {
        _isTypingDone        = false;
        descriptionText.text = "";

        foreach (char c in text)
        {
            descriptionText.text += c;
            yield return new WaitForSeconds(_typingSpeed);
        }

        _isTypingDone     = true;
        _typingOnComplete = null;
        onComplete?.Invoke();
    }

    void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        descriptionText.text = _currentFullText;
        _isTypingDone        = true;

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
        if (leftCharNameText != null) leftCharNameText.text = _leftName;
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
        if (rightCharNameText != null) rightCharNameText.text = _rightName;
        RefreshNamePanels();
    }

    // 말하는 쪽 namePanel 만 활성화. 두 명이면 _speakerSide, 한 명이면 보이는 초상화 기준.
    void RefreshNamePanels()
    {
        int speaker = _twoPerson
            ? _speakerSide
            : (portraitImage2 != null && portraitImage2.gameObject.activeSelf ? 2 : 1);

        if (leftCharNamePanel  != null)
            leftCharNamePanel.SetActive(speaker == 1 && !string.IsNullOrEmpty(_leftName));
        if (rightCharNamePanel != null)
            rightCharNamePanel.SetActive(speaker == 2 && !string.IsNullOrEmpty(_rightName));
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
            SetPortraitState(spk, 1f, 1f);                                   // 화자
            SetPortraitState(non, _nonSpeakerScale, _nonSpeakerAlpha255 / 255f); // 비화자
        }
    }

    IEnumerator AnimateSpeaker(Image spk, Image non)
    {
        Vector3 spkFrom = spk.transform.localScale, nonFrom = non.transform.localScale;
        float   spkAFrom = spk.color.a,             nonAFrom = non.color.a;
        Vector3 spkTo = Vector3.one,                nonTo = Vector3.one * _nonSpeakerScale;
        float   spkATo = 1f,                        nonATo = _nonSpeakerAlpha255 / 255f;

        float t = 0f;
        while (t < _speakerAnimDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / _speakerAnimDuration);
            spk.transform.localScale = Vector3.Lerp(spkFrom, spkTo, k);
            non.transform.localScale = Vector3.Lerp(nonFrom, nonTo, k);
            SetImageAlpha(spk, Mathf.Lerp(spkAFrom, spkATo, k));
            SetImageAlpha(non, Mathf.Lerp(nonAFrom, nonATo, k));
            yield return null;
        }
        spk.transform.localScale = spkTo;
        non.transform.localScale = nonTo;
        SetImageAlpha(spk, spkATo);
        SetImageAlpha(non, nonATo);
        _speakerAnimCo = null;
    }

    void ResetPortrait(Image img)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one;
        SetImageAlpha(img, 1f);
    }

    void SetPortraitState(Image img, float scale, float alpha)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one * scale;
        SetImageAlpha(img, alpha);
    }

    void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }
}
