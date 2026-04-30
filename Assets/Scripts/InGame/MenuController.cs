using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    }

    [Header("Refs")]
    public Button menuButton;
    public RectTransform topMenuContainer;

    [Header("Top Menus (4개)")]
    public TopMenu[] topMenus;

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
                if (tm?.subMenu != null)
                    _subOpenPos[tm] = tm.subMenu.anchoredPosition;
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
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeStopChanged -= OnTimeStopChanged;
    }

    void OnTimeStopChanged(bool stopped)
    {
        if (menuButton == null) return;
        menuButton.gameObject.SetActive(!stopped);
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
        topMenuContainer.gameObject.SetActive(true);
        topMenuContainer.anchoredPosition = _topOpenPos + topClosedOffset;
        StartTopAnim(_topOpenPos, null);
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
        }

        StartTopAnim(_topOpenPos + topClosedOffset,
            () => topMenuContainer.gameObject.SetActive(false));
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

    void StartTopAnim(Vector2 target, System.Action onDone)
    {
        if (_topAnim != null) StopCoroutine(_topAnim);
        _topAnim = StartCoroutine(SlideTo(topMenuContainer, target, slideDuration, onDone));
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

        // 외부 클릭 →
        //  · sub가 펼쳐져 있으면 sub만 닫고 top은 유지
        //  · sub가 없으면 top까지 닫음
        if (_activeSub != null)
        {
            HideSub(_activeSub);
            _activeSub = null;
        }
        else
        {
            CloseTopMenu();
        }
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
