using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

// ═══════════════════════════════════════════════════════════════════════════
// 씬 세팅 가이드
// ───────────────────────────────────────────────────────────────────────────
//  Canvas
//  ├─ MenuButton (Button)                   ← [menuButton]
//  ├─ TopMenuContainer (VerticalLayoutGroup) ← [topMenuContainer]
//  │  ├─ ProjectButton
//  │  ├─ EmployeeButton
//  │  ├─ ManageButton
//  │  └─ TechTreeButton
//  ├─ ProjectSubMenu (VerticalLayoutGroup)  ← TopMenu.subMenu
//  │  └─ ...
//  ├─ EmployeeSubMenu (VerticalLayoutGroup)
//  │  ├─ HireButton
//  │  ├─ FireButton
//  │  └─ EnhanceButton
//  └─ ...
//
// 컨테이너들은 인스펙터에서 "열림 상태" 위치/크기로 배치해 두세요.
// 슬라이드 닫힘 위치는 (열림 위치 + closedOffset) 로 자동 계산됨.
// ═══════════════════════════════════════════════════════════════════════════
public class MenuController : MonoBehaviour
{
    [System.Serializable]
    public class TopMenu
    {
        [Tooltip("인스펙터 식별용 (런타임 미사용)")]
        public string label;

        [Tooltip("상위 버튼 (4개 중 하나)")]
        public Button button;

        [Tooltip("바로 열 패널. subMenu가 null일 때 사용")]
        public GameObject directPanel;

        [Tooltip("하위 버튼 컨테이너. 지정되면 클릭 시 슬라이드로 펼쳐짐")]
        public RectTransform subMenu;

        // ── 런타임 캐시 (인스펙터 미노출) ──
        [System.NonSerialized] public TextMeshProUGUI text;
        [System.NonSerialized] public TMP_FontAsset originalFont;
        [System.NonSerialized] public Color originalColor;
        [System.NonSerialized] public bool isPressed;
    }

    [Header("Refs")]
    public Button menuButton;
    public RectTransform topMenuContainer;

    [Header("Top Menus (4개)")]
    public TopMenu[] topMenus;

    [Header("Top 버튼 텍스트 — Pressed/Selected 스타일")]
    [Tooltip("눌려있거나(pressed) 해당 서브메뉴가 열려있는(selected) 동안 버튼 텍스트에 적용할 폰트")]
    public TMP_FontAsset activeFontAsset;
    [Tooltip("Pressed/Selected 상태의 텍스트 색상")]
    public Color activeTextColor = Color.white;

    [Header("Slide")]
    [Tooltip("Top 메뉴의 닫힘 오프셋 (열림 위치 + offset = 닫힘 위치). 위→아래 슬라이드면 (0, +N)")]
    public Vector2 topClosedOffset = new Vector2(0f, 100f);

    [Tooltip("Sub 메뉴의 닫힘 오프셋. 왼→오 슬라이드면 (-N, 0)")]
    public Vector2 subClosedOffset = new Vector2(-100f, 0f);

    [Tooltip("슬라이드 시간(초)")]
    public float slideDuration = 0.2f;

    // ── 런타임 상태 ──────────────────────────────────────────────────────────
    private bool _topOpen;
    private TopMenu _activeSub;
    private Vector2 _topOpenPos;
    private readonly Dictionary<TopMenu, Vector2> _subOpenPos = new();
    private Coroutine _topAnim;
    private readonly Dictionary<TopMenu, Coroutine> _subAnims = new();

    void Awake()
    {
        // 인스펙터에 배치된 위치를 "열림" 기준으로 캐시
        if (topMenuContainer != null) _topOpenPos = topMenuContainer.anchoredPosition;
        if (topMenus != null)
        {
            foreach (var tm in topMenus)
            {
                if (tm == null) continue;
                if (tm.subMenu != null)
                    _subOpenPos[tm] = tm.subMenu.anchoredPosition;

                // 버튼 자식 텍스트 + 원래 폰트/색 캐시 (Pressed/Selected 스타일 복원용)
                if (tm.button != null)
                {
                    tm.text = tm.button.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (tm.text != null)
                    {
                        tm.originalFont  = tm.text.font;
                        tm.originalColor = tm.text.color;
                    }
                }
            }
        }
    }

    void Start()
    {
        if (topMenuContainer != null) topMenuContainer.gameObject.SetActive(false);
        if (topMenus != null)
        {
            foreach (var tm in topMenus)
            {
                if (tm == null) continue;
                if (tm.subMenu != null)
                {
                    tm.subMenu.gameObject.SetActive(false);
                    // sub 메뉴 안의 모든 버튼: 클릭하면 메뉴 전체 닫힘 (기존 onClick 실행 후)
                    var subButtons = tm.subMenu.GetComponentsInChildren<Button>(true);
                    foreach (var b in subButtons)
                        b.onClick.AddListener(CloseTopMenu);
                }
                if (tm.button != null)
                {
                    var captured = tm;
                    tm.button.onClick.AddListener(() => OnTopClick(captured));
                    SetupPressWatcher(captured);
                }
            }
        }
        if (menuButton != null) menuButton.onClick.AddListener(ToggleTopMenu);

        // 시간이 멈출 때 메뉴 버튼 비활성, 재개 시 활성
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeStopChanged += OnTimeStopChanged;
            // 현재 상태 즉시 반영 (씬 진입 시 이미 정지 상태일 수 있음)
            OnTimeStopChanged(!GameTimeManager.Instance.IsRunning);
        }

        // 온보딩 튜토리얼(9단계까지) 진행 중엔 메뉴 버튼 오브젝트 자체를 꺼버린다 — 튜토리얼이 스스로
        // 강조하는 시점(TutorialHighlighter.Highlight)에만 일시적으로 다시 SetActive(true)로 되살아난다.
        if (menuButton != null && !OnboardingState.Tutorial10Done)
        {
            _tutorialHideActive = true;
            menuButton.gameObject.SetActive(false);
        }
        OnboardingState.OnOnboardingFullyDone += OnOnboardingFullyDone;
    }

    // 튜토리얼(9단계까지) 진행 중엔 true — 이 동안은 아래 OnTimeStopChanged가 menuButton의 SetActive를
    // 건드리지 않는다(시간 정지/재개가 수시로 오가는 튜토리얼 중에 매번 되살아나 버리는 것 방지).
    bool _tutorialHideActive;

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged -= OnTimeStopChanged;
        OnboardingState.OnOnboardingFullyDone -= OnOnboardingFullyDone;
    }

    void OnOnboardingFullyDone()
    {
        _tutorialHideActive = false;
        if (menuButton != null) menuButton.gameObject.SetActive(true);
    }

    void OnTimeStopChanged(bool stopped)
    {
        if (menuButton == null) return;
        if (!_tutorialHideActive)
        {
            // 튜토리얼 dim 중에는 시간이 멈춰도 메뉴 버튼을 숨기지 않음 (튜토리얼이 메뉴를 강조·클릭하게 하므로)
            if (!(stopped && OnboardingState.TutorialActive))
                menuButton.gameObject.SetActive(!stopped);
        }
        // 정지 시 펼쳐진 메뉴도 같이 닫음 (버튼이 사라지니 뒷정리)
        if (stopped && _topOpen) CloseTopMenu();
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void ToggleTopMenu()
    {
        if (_topOpen) CloseTopMenu();
        else          OpenTopMenu();
    }

    public void OpenTopMenu()
    {
        if (_topOpen || topMenuContainer == null) return;
        _topOpen = true;
        if (_topAnim != null) { StopCoroutine(_topAnim); _topAnim = null; }
        topMenuContainer.DOKill();
        topMenuContainer.gameObject.SetActive(true);
        topMenuContainer.anchoredPosition = _topOpenPos;
        // 촤르륵 펼침 — 세로로 접힌 상태에서 OutBack 으로 펴짐
        topMenuContainer.localScale = new Vector3(1f, 0f, 1f);
        topMenuContainer.DOScaleY(1f, slideDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void CloseTopMenu()
    {
        if (!_topOpen || topMenuContainer == null) return;
        _topOpen = false;

        // 닫을 때 펼쳐진 sub도 함께 닫음
        if (_activeSub != null)
        {
            HideSub(_activeSub);
            _activeSub = null;
            EventSystem.current?.SetSelectedGameObject(null); // Selected 상태 해제(selectedSprite 원복)
            RefreshAllTopMenuTextStyles();
        }

        if (_topAnim != null) { StopCoroutine(_topAnim); _topAnim = null; }
        topMenuContainer.DOKill();
        topMenuContainer.DOScaleY(0f, slideDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() =>
            {
                topMenuContainer.gameObject.SetActive(false);
                topMenuContainer.localScale = Vector3.one;
            });
    }

    // ── 내부 ─────────────────────────────────────────────────────────────────
    void OnTopClick(TopMenu tm)
    {
        // sub가 없으면 직접 패널 오픈 + 메뉴 전체 닫음
        if (tm.subMenu == null)
        {
            if (tm.directPanel != null) tm.directPanel.SetActive(true);
            CloseTopMenu();
            return;
        }

        // 같은 버튼 다시 클릭 → 닫지 않음 (요구사항)
        if (_activeSub == tm) return;

        // 다른 sub 열려있으면 닫기 → 새 sub 열기
        if (_activeSub != null) HideSub(_activeSub);
        ShowSub(tm);
        _activeSub = tm;
        // Unity Selectable의 Selected 상태로 전환 — Button Transition=SpriteSwap이면 selectedSprite가
        // 자동으로 표시되고, 마우스가 버튼을 벗어나도(포인터 뗀 뒤) 서브메뉴가 열려있는 동안 계속 유지됨.
        EventSystem.current?.SetSelectedGameObject(tm.button != null ? tm.button.gameObject : null);
        RefreshAllTopMenuTextStyles();
    }

    // ── Top 버튼 텍스트 Pressed/Selected 스타일 ─────────────────────────────
    void SetupPressWatcher(TopMenu tm)
    {
        if (tm.button == null) return;
        var trigger = tm.button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = tm.button.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => { tm.isPressed = true; RefreshTopMenuTextStyle(tm); });
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => { tm.isPressed = false; RefreshTopMenuTextStyle(tm); });
        trigger.triggers.Add(up);
    }

    // pressed(누르는 중) 또는 selected(해당 서브메뉴가 열려있음) 이면 activeFontAsset+activeTextColor,
    // 아니면 캐시해둔 원래 폰트/색으로 복원. 버튼 이미지 자체는 더 이상 여기서 건드리지 않음 —
    // Button의 Transition=SpriteSwap이 Pressed/Selected 상태에 맞는 스프라이트를 알아서 보여준다
    // (서브메뉴 열림 유지는 OnTopClick/CloseTopMenu 등에서 EventSystem.SetSelectedGameObject로 처리).
    void RefreshTopMenuTextStyle(TopMenu tm)
    {
        if (tm == null) return;
        bool active = tm.isPressed || _activeSub == tm;

        if (tm.text != null)
        {
            tm.text.font  = active ? activeFontAsset : tm.originalFont;
            tm.text.color = active ? activeTextColor : tm.originalColor;
        }
    }

    void RefreshAllTopMenuTextStyles()
    {
        if (topMenus == null) return;
        foreach (var tm in topMenus) RefreshTopMenuTextStyle(tm);
    }

    void ShowSub(TopMenu tm)
    {
        if (tm.subMenu == null) return;
        if (_subAnims.TryGetValue(tm, out var prev) && prev != null) StopCoroutine(prev);

        var openPos = _subOpenPos[tm];
        tm.subMenu.gameObject.SetActive(true);
        tm.subMenu.anchoredPosition = openPos + subClosedOffset;
        _subAnims[tm] = StartCoroutine(SlideTo(tm.subMenu, openPos, slideDuration, null));
    }

    void HideSub(TopMenu tm)
    {
        if (tm.subMenu == null) return;
        if (_subAnims.TryGetValue(tm, out var prev) && prev != null) StopCoroutine(prev);

        var openPos = _subOpenPos[tm];
        var rt      = tm.subMenu;
        _subAnims[tm] = StartCoroutine(SlideTo(rt, openPos + subClosedOffset, slideDuration,
            () => rt.gameObject.SetActive(false)));
    }

    // ── 외부 클릭 감지 (열려있을 때 다른 곳 누르면 닫음) ───────────────────
    private static readonly List<RaycastResult> _raycastResults = new();
    void Update()
    {
        if (!_topOpen) return;
        if (!TryGetPressedPointerPosition(out var pos)) return;

        // 메뉴 버튼 / topMenuContainer / 활성 sub 메뉴 위 클릭이면 닫지 않음
        if (EventSystem.current == null) return;
        var ped = new PointerEventData(EventSystem.current) { position = pos };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _raycastResults);

        bool insideMenuBtn = false, insideTop = false, insideSub = false;
        foreach (var r in _raycastResults)
        {
            var t = r.gameObject.transform;
            if (menuButton != null && t.IsChildOf(menuButton.transform)) insideMenuBtn = true;
            if (topMenuContainer != null && t.IsChildOf(topMenuContainer)) insideTop = true;
            if (_activeSub != null && _activeSub.subMenu != null && t.IsChildOf(_activeSub.subMenu)) insideSub = true;
        }

        // 메뉴 영역 내부 클릭은 무시 (각 버튼 onClick이 개별 처리)
        if (insideMenuBtn || insideTop || insideSub) return;

        // 메뉴 위에 떠 있는 오버레이(튜토리얼 dim, 모달 블로커 등)가 클릭을 가로챘으면
        // "외부 클릭"으로 보지 않고 무시 — 오버레이가 입력을 소유 중이므로 메뉴를 닫지 않는다.
        // (튜토리얼: 하이라이트 안 한 곳을 눌러도 메뉴가 닫혀 dim 과 어긋나던 문제 방지)
        if (_raycastResults.Count > 0)
        {
            var topCanvas = _raycastResults[0].gameObject.GetComponentInParent<Canvas>();
            if (topCanvas != null && topCanvas.rootCanvas.sortingOrder > MenuCanvasOrder())
                return;
        }

        // 외부 클릭 →
        //  · sub가 펼쳐져 있으면 sub만 닫고 top은 유지
        //  · sub가 없으면 top까지 닫음
        if (_activeSub != null)
        {
            HideSub(_activeSub);
            _activeSub = null;
            EventSystem.current?.SetSelectedGameObject(null); // Selected 상태 해제(selectedSprite 원복)
            RefreshAllTopMenuTextStyles();
        }
        else
        {
            CloseTopMenu();
        }
    }

    // 메뉴가 올라간 캔버스의 정렬값 (오버레이 판별 기준). 1회 캐시.
    int _menuCanvasOrder = int.MinValue;
    int MenuCanvasOrder()
    {
        if (_menuCanvasOrder != int.MinValue) return _menuCanvasOrder;
        var c = GetComponentInParent<Canvas>();
        _menuCanvasOrder = c != null ? c.rootCanvas.sortingOrder : 0;
        return _menuCanvasOrder;
    }

    static bool TryGetPressedPointerPosition(out Vector2 position)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            position = touch.primaryTouch.position.ReadValue();
            return true;
        }
        position = default;
        return false;
    }

    IEnumerator SlideTo(RectTransform rt, Vector2 target, float duration, System.Action onDone)
    {
        if (duration <= 0f)
        {
            rt.anchoredPosition = target;
            onDone?.Invoke();
            yield break;
        }

        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            // ease-out quad
            k = 1f - (1f - k) * (1f - k);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, target, k);
            yield return null;
        }
        rt.anchoredPosition = target;
        onDone?.Invoke();
    }
}
