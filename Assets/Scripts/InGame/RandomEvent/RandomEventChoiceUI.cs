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

    [Header("Buttons")]
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public Button confirmButton;

    [Header("타이핑 속도 (초/글자)")]
    [SerializeField] private float _typingSpeed = 0.05f;

    // ── 내부 상태 ────────────────────────────────────────────────
    private RandomEventChoiceData _currentData;
    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

    private Coroutine       _typingCoroutine;
    private bool            _isTypingDone;
    private string          _currentFullText;
    private System.Action   _typingOnComplete;

    private string _chosenSystemMessage;

    // ── 초기화 ──────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        eventPanel.SetActive(false);
    }

    // ── 공개 API ─────────────────────────────────────────────────
    public void Show(RandomEventChoiceData data)
    {
        _currentData         = data;
        _chosenSystemMessage = null;

        titleText.text = data.title;
        SetPortrait(data.portraitId);

        ClearChoiceButtons();
        confirmButton.gameObject.SetActive(false);
        confirmButton.interactable = false;

        eventPanel.SetActive(true);

        // description 타이핑 → 완료 후 선택지 버튼 표시
        StartTyping(data.description, onComplete: () =>
        {
            SpawnChoiceButtons(data.choices);
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
        eventPanel.SetActive(false);

        MoneyManager.Instance.SaveMoney();
        ProjectSaveManager.Instance.SaveProject();
        GameTimeManager.Instance.SaveGameTime();
        EmployeeManager.Instance.SaveAllEmployees();

        DevelopmentManager.Instance.ResumeFromEvent();

        if (!string.IsNullOrEmpty(_chosenSystemMessage))
            AlertUI.Instance.Show(_chosenSystemMessage);
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
            btn.onClick.AddListener(() => OnChoiceSelected(captured));

            _spawnedButtons.Add(go);
        }
    }

    void OnChoiceSelected(RandomEventChoiceOption choice)
    {
        // 선택지 버튼 숨기기
        foreach (var go in _spawnedButtons)
            go.SetActive(false);

        // 제목 교체 (null이면 유지)
        if (choice.resultTitle != null)
            titleText.text = choice.resultTitle;

        // 효과 즉시 적용
        choice.onChoose?.Invoke();

        // 저장할 시스템 문구
        _chosenSystemMessage = choice.resultSystemMessage;

        // resultDescription 타이핑 → 완료 후 confirm 활성화
        string resultDesc = !string.IsNullOrEmpty(choice.resultDescription)
            ? choice.resultDescription
            : _currentData.description;

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
    }
}
