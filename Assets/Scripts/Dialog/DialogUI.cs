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
using UnityEngine.InputSystem;
using TMPro;

public class DialogUI : MonoBehaviour
{
    [Header("기본 UI")]
    [SerializeField] private GameObject _dialogPanel;
    [SerializeField] private GameObject _speakerNamePanel;
    [Tooltip("NamePanel 자체 이미지 — RandomEventUI와 동일하게 캐릭터(portraitId)별로 교체")]
    [SerializeField] private Image _speakerNamePanelImage;
    [SerializeField] private CharacterNamePanelSet _namePanelSpriteSet;
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private GameObject _nextIndicator;
    [Tooltip("대사 박스 전체(이름창/대사/스킵버튼 등 포함) — 선택지가 뜨는 동안만 숨겼다가 다음 노드에서 다시 켬")]
    [SerializeField] private GameObject _dialogTextBG;
    public GameObject employeeSlotArea;

    [Header("선택지")]
    [SerializeField] private GameObject _choicePanel;
    [SerializeField] private GameObject _choiceButtonPrefab;

    [Header("타이핑 속도 (초/글자)")]
    [SerializeField] private float _speedNormal = 0.05f;
    [SerializeField] private float _speedFast = 0.02f;
    [SerializeField] private float _speedSlow = 0.10f;

    [Header("스킵 버튼 — RandomEventUI와 동일 정책: 3배속 타이핑 + 클릭대기 노드 자동 진행(세션 1회만 유지, 선택지는 자동선택 안 함)")]
    [SerializeField] private Button _skipButton;
    [SerializeField] private float _skipSpeedMultiplier = 3f;

    // ─── 내부 상태 ───────────────────────────────────────────────
    private Coroutine _typingCoroutine;
    private bool _isTypingDone;
    private string _currentFullText;   // 스킵용 전체 텍스트 저장
    private bool _hasChoiceOnDone;
    private bool _hasAutoAdvanceOnDone; // 스킵 모드에서 이중 Next() 방지용(자기 타이머로 이미 넘어가는 노드)
    private bool _awaitingChoiceReveal; // 선택지 노드 — 타이핑은 끝났지만 아직 클릭 전(선택지 버튼 미노출)
    private bool _skipMode;             // 이번 다이얼로그 세션(Show~Hide) 동안만 유지
    private List<ChoiceData> _pendingChoices;
    private List<GameObject> _choiceButtons = new List<GameObject>();

    // 노드가 표시된 바로 그 프레임의 클릭은 무시 — 직전 모달을 닫거나 이전 노드를 넘긴 클릭이
    // 같은 프레임에 새 노드를 여는 경우(ModalGate 큐, 선택지 클릭 즉시 전환 등) 타이핑 스킵으로 새어들어가는 것을 방지.
    private int _shownFrame = -1;

    void Awake()
    {
        DialogManager.Instance.SetDialogUI(this);
        SalaryNegotiationManager.Instance.SetEmployeeSlotArea(employeeSlotArea);

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(OnSkipClicked);
            _skipButton.onClick.AddListener(OnSkipClicked);
        }
    }

    // 스킵 버튼 — RandomEventUI.OnSkipClicked와 동일 정책: 이후 타이핑은 3배속, "클릭해야 넘어가는"
    // 순수 대기 노드는 자동으로 넘겨준다. 선택지가 있는 노드는 유저 결정이 필요하므로 자동 선택하지
    // 않고 그대로 대기 — 단, "선택지 버튼을 아직 공개 안 한 대기"(_awaitingChoiceReveal)는 RandomEventChoiceUI와
    // 동일하게 스킵이 그 공개 자체는 대신 눌러준다(선택 자체는 여전히 유저가 함). 순수 대기 노드는 그
    // 자리에서 바로 다음으로 넘긴다(RandomEventUI가 Step.Result에서 즉시 Close하는 것과 동일한 정책).
    private void OnSkipClicked()
    {
        _skipMode = true;
        if (!_isTypingDone) return;
        if (_awaitingChoiceReveal) { RevealChoices(); return; }
        if (!_hasChoiceOnDone && !_hasAutoAdvanceOnDone) DialogManager.Instance.Next();
    }
    // ─── 공개 API ────────────────────────────────────────────────
    public void Show(DialogNodeData node, List<ChoiceData> choices)
    {
        // 다이얼로그 패널이 처음 떠오를 때 시간 정지 (Hide 에서 1회 재개 — 노드 전환 시 중복 정지 방지)
        bool wasActive = _dialogPanel.activeSelf;
        _dialogPanel.SetActive(true);
        if (!wasActive) GameTimeManager.Instance?.StopTime();
        if (!wasActive) _skipMode = false; // 스킵은 이번 세션(Show~Hide) 한정 — 새 세션마다 초기화
        if (_dialogTextBG != null) _dialogTextBG.SetActive(true); // 이전 노드에서 선택지가 떠 숨겨졌을 수 있음 — 새 노드는 항상 다시 켬
        _hasChoiceOnDone = node.hasChoice;
        _hasAutoAdvanceOnDone = node.autoAdvance;
        _awaitingChoiceReveal = false;
        _pendingChoices = choices;
        string displayText = DialogManager.Instance.ReplacePlaceholders(node.dialogText);
        string displayName = DialogManager.Instance.ReplacePlaceholders(node.speakerName ?? "");
        string displayPortraitId = DialogManager.Instance.ReplacePlaceholders(node.speakerPortraitId ?? "");

        _currentFullText = displayText;

        // 이름창 — 텍스트뿐 아니라 RandomEventUI와 동일하게 NamePanel 배경 이미지도 화자(portraitId)별로 교체.
        bool hasName = !string.IsNullOrEmpty(node.speakerName);
        _speakerNamePanel.SetActive(hasName);
        if (hasName) _speakerNameText.text = displayName;
        CharacterNamePanelSet.Apply(_speakerNamePanelImage, _namePanelSpriteSet, displayPortraitId);
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
        _shownFrame = Time.frameCount;

        // 연출
        if (node.shakeEffect) StartCoroutine(ShakePanel());
        if (node.autoAdvance && !node.hasChoice)
            StartCoroutine(AutoAdvance(node.autoDelay));
    }

    public void Hide()
    {
        // 패널이 떠 있던 경우에만 시간 재개 (Show 의 StopTime 과 1:1 균형)
        if (_dialogPanel.activeSelf) GameTimeManager.Instance?.StartTime();
        _dialogPanel.SetActive(false);
        ClearChoices();
        _skipMode = false; // 세션 종료 — 다음 다이얼로그는 스킵 꺼진 상태로 새로 시작
    }

    // ─── 터치 입력 (New Input System — Mouse/Touch) ──────────────
    // 타이핑 중 화면 어디를 클릭하든 즉시 전체 텍스트 표시(완성). 완료 후 클릭은 무동작 —
    // 다음 노드 진행/선택은 기존 흐름(선택지 버튼·autoAdvance·직원선택 등)이 담당해 흐름 깨짐 방지.
    private void Update()
    {
        if (_dialogPanel == null || !_dialogPanel.activeSelf) return;
        if (Time.frameCount == _shownFrame) return; // 표시된 첫 프레임의 클릭은 무시
        if (!_isTypingDone && ClickedThisFrame()) SkipTyping();
    }

    private static bool ClickedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    private void OnTap()
    {
        // 타이핑 중 → 전체 텍스트 즉시 표시
        if (!_isTypingDone) { SkipTyping(); return; }
        // 선택지 버튼이 이미 떠 있으면 화면 탭 무시 — 버튼 클릭으로만 선택.
        if (_choicePanel.activeSelf) return;
        // 선택지 노드 — 타이핑 끝나고 아직 버튼 공개 전이면, 다른 대사와 동일하게 이 클릭으로 공개한다.
        if (_awaitingChoiceReveal) { RevealChoices(); return; }

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
            // 스킵 모드면 3배속 — 매 글자마다 다시 체크해 타이핑 도중에 눌러도 그 지점부터 즉시 적용.
            float interval = _skipMode ? speed / Mathf.Max(0.01f, _skipSpeedMultiplier) : speed;
            yield return new WaitForSeconds(interval);
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
        {
            // 선택지 노드도 다른 대사와 동일하게 "클릭해서 다음" 인디케이터를 한 번 거친 뒤에야 선택지
            // 버튼을 공개한다(타이핑 끝나자마자 바로 안 뜨게) — RevealChoices()가 실제 공개를 담당.
            _nextIndicator.SetActive(true);
            _awaitingChoiceReveal = true;
            if (_skipMode) RevealChoices(); // 스킵은 이 대기만 건너뜀 — 선택 자체는 여전히 유저가 함
        }
        else
        {
            _nextIndicator.SetActive(true);
            // 스킵 모드 — autoAdvance 노드는 자기 타이머로 이미 넘어가므로 건드리지 않고, "클릭해야
            // 넘어가는" 순수 대기 노드만 대신 눌러준다.
            if (_skipMode && !_hasAutoAdvanceOnDone) DialogManager.Instance.Next();
        }
    }

    // ─── 선택지 ──────────────────────────────────────────────────
    private void RevealChoices()
    {
        _awaitingChoiceReveal = false;
        _nextIndicator.SetActive(false);
        ShowChoices(_pendingChoices);
    }

    private void ShowChoices(List<ChoiceData> choices)
    {
        _choicePanel.SetActive(true);
        if (_dialogTextBG != null) _dialogTextBG.SetActive(false); // 선택지 뜨는 동안 대사 박스는 숨김 — 다음 노드(Show)에서 다시 켜짐
        DialogManager.Instance.NotifyChoicesShown();
        foreach (var choice in choices)
        {
            var btnObj = Instantiate(_choiceButtonPrefab, _choicePanel.transform);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = DialogManager.Instance.ReplacePlaceholders(choice.choiceText);

            var captured = choice;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
                DialogManager.Instance.OnChoiceSelected(captured));

            _choiceButtons.Add(btnObj);
        }
    }

    // GlobalButtonClickBounce가 버튼이 처음 클릭되는 순간 그 버튼과 원래 부모 사이에
    // __ClickBounceWrapper를 끼워넣고 버튼을 그 안으로 재배치한다([[feedback_global_click_bounce_pitfalls]]
    // 1/4/5번). 클릭된 선택지 버튼을 그냥 Destroy(btn)만 하면 방금 생긴 __ClickBounceWrapper는 빈 채로
    // ChoicePanel 밑에 그대로 남아 잔여물이 되므로, 래핑됐으면 래퍼째로 지운다.
    private void ClearChoices()
    {
        foreach (var btn in _choiceButtons)
        {
            if (btn == null) continue;
            var parent = btn.transform.parent;
            if (parent != null && parent.name == "__ClickBounceWrapper")
                Destroy(parent.gameObject);
            else
                Destroy(btn);
        }
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