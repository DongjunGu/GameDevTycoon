// =====================================================
// DialogUI.cs
// 대화 패널 렌더링, 타이핑, 선택지 버튼 관리
// =====================================================
// [Inspector 할당 필요]
//  - _dialogPanel      : 전체 다이얼로그 패널 GameObject
//  - _speakerNamePanel : 이름창 GameObject (빈 이름이면 숨김)
//  - _speakerNameText  : 이름 TMP
//  - _dialogText       : 대사 TMP
//  - _portraitImage    : 초상화 Image
//  - _nextIndicator    : 다음 진행 표시 (화살표 등) GameObject
//  - _choicePanel      : 선택지 버튼들 부모 GameObject
//  - _choiceButtonPrefab : 선택지 버튼 프리팹 (TMP + Button 포함)
// =====================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogUI : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private GameObject _dialogPanel;
    [SerializeField] private GameObject _speakerNamePanel;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private GameObject _nextIndicator;
    public GameObject employeeSlotArea;

    [Header("선택지")]
    [SerializeField] private GameObject _choicePanel;
    [SerializeField] private GameObject _choiceButtonPrefab;

    [Header("타이핑 속도 (초/글자)")]
    [SerializeField] private float _speedNormal = 0.05f;
    [SerializeField] private float _speedFast = 0.02f;
    [SerializeField] private float _speedSlow = 0.10f;

    // ─── 내부 상태 ───────────────────────────────────────────────
    private Coroutine _typingCoroutine;
    private bool _isTypingDone;
    private string _currentFullText;   // 스킵용 전체 텍스트 저장
    private bool _hasChoiceOnDone;
    private List<ChoiceData> _pendingChoices;
    private List<GameObject> _choiceButtons = new List<GameObject>();

    void Awake()
    {
        DialogManager.Instance.SetDialogUI(this);
        SalaryNegotiationManager.Instance.SetEmployeeSlotArea(employeeSlotArea);
    }
    // ─── 공개 API ────────────────────────────────────────────────
    public void Show(DialogNodeData node, List<ChoiceData> choices)
    {
        _dialogPanel.SetActive(true);
        _hasChoiceOnDone = node.hasChoice;
        _pendingChoices = choices;
        string displayText = DialogManager.Instance.ReplacePlaceholders(node.dialogText);
        string displayName = DialogManager.Instance.ReplacePlaceholders(node.speakerName ?? "");
        string displayPortraitId = DialogManager.Instance.ReplacePlaceholders(node.speakerPortraitId ?? "");

        _currentFullText = displayText;

        // 이름창
        bool hasName = !string.IsNullOrEmpty(node.speakerName);
        _speakerNamePanel.SetActive(hasName);
        if (hasName) _speakerNameText.text = displayName;
        //초상화
        if (string.IsNullOrEmpty(displayPortraitId))
        {
            _portraitImage.gameObject.SetActive(false);
        }
        else
        {
            Sprite portrait = Resources.Load<Sprite>($"Portraits/{displayPortraitId}");
            Debug.Log(displayPortraitId);
            if (portrait != null)
            {
                _portraitImage.sprite = portrait;
                _portraitImage.gameObject.SetActive(true);
            }
            else
            {
                _portraitImage.gameObject.SetActive(false);
                Debug.Log($"없음 {displayPortraitId}");
                Debug.LogWarning($"[DialogUI] 초상화 없음: Portraits/{displayPortraitId}");
            }
        }

        // 선택지 / 인디케이터 초기화
        ClearChoices();
        _choicePanel.SetActive(false);
        _nextIndicator.SetActive(false);

        // 타이핑
        float speed = node.textSpeed switch
        {
            TextSpeed.Fast => _speedFast,
            TextSpeed.Slow => _speedSlow,
            _ => _speedNormal,
        };
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeText(displayText, speed));

        // 연출
        if (node.shakeEffect) StartCoroutine(ShakePanel());
        if (node.autoAdvance && !node.hasChoice)
            StartCoroutine(AutoAdvance(node.autoDelay));
    }

    public void Hide()
    {
        _dialogPanel.SetActive(false);
        ClearChoices();
    }

    // ─── 터치 입력 ───────────────────────────────────────────────
    // private void Update()
    // {
    //     if (!_dialogPanel.activeSelf) return;
    //     if (!Input.GetMouseButtonDown(0)) return;
    //     OnTap();
    // }

    private void OnTap()
    {
        // 타이핑 중 → 전체 텍스트 즉시 표시
        if (!_isTypingDone) { SkipTyping(); return; }
        // 선택지 대기 중 → 터치 무시
        if (_choicePanel.activeSelf) return;

        DialogManager.Instance.Next();
    }

    // ─── 타이핑 ──────────────────────────────────────────────────
    private IEnumerator TypeText(string text, float speed)
    {

        _isTypingDone = false;
        _dialogText.text = "";

        foreach (char c in text)
        {
            _dialogText.text += c;
            yield return new WaitForSeconds(speed);
        }

        OnTypingComplete();
    }

    private void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _dialogText.text = _currentFullText;
        OnTypingComplete();
    }

    private void OnTypingComplete()
    {
        _isTypingDone = true;

        if (_hasChoiceOnDone && _pendingChoices != null)
            ShowChoices(_pendingChoices);
        else
            _nextIndicator.SetActive(true);
    }

    // ─── 선택지 ──────────────────────────────────────────────────
    private void ShowChoices(List<ChoiceData> choices)
    {
        _choicePanel.SetActive(true);
        foreach (var choice in choices)
        {
            var btnObj = Instantiate(_choiceButtonPrefab, _choicePanel.transform);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = choice.choiceText;

            var captured = choice;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
                DialogManager.Instance.OnChoiceSelected(captured));

            _choiceButtons.Add(btnObj);
        }
    }

    private void ClearChoices()
    {
        foreach (var btn in _choiceButtons) Destroy(btn);
        _choiceButtons.Clear();
    }

    // ─── 연출 ────────────────────────────────────────────────────
    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_isTypingDone) DialogManager.Instance.Next();
    }

    private IEnumerator ShakePanel()
    {
        var rect = _dialogPanel.GetComponent<RectTransform>();
        var origin = rect.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            rect.anchoredPosition = origin + Random.insideUnitCircle * 6f;
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = origin;
    }
    public void OnNextButtonClick()
    {
        OnTap();
    }
}