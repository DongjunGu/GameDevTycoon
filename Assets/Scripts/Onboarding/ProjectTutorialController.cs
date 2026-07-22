using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 온보딩 2차 튜토리얼 — 첫 채용으로 직원을 얻고 1주 뒤 1회 실행.
// 흐름: 메뉴 버튼 강조 → (클릭) → ProjectSetupMenuBtn 강조 → (클릭) → projectStartBtn 강조 → 완료.
//
// 트리거: HiringUI.DoHire 에서 OnboardingState.ArmProjectTutorial(1) 로 1주 카운트다운 무장.
//   GameTimeManager.OnTimeChanged(주차 틱)마다 pending 감소, 0 이 되면 실행. (세션 끊겨도 PlayerPrefs 로 복구)
// 강조 방식은 TutorialController 와 동일(반투명 dim 위로 대상만 overrideSorting 올려 클릭 가능 + 펄스).
[DisallowMultipleComponent]
public class ProjectTutorialController : MonoBehaviour
{
    public static ProjectTutorialController Instance { get; private set; }

    [Header("강조할 버튼 (순서대로)")]
    public Button menuButton;          // 1) 메뉴 열기
    public Button projectSetupButton;  // 2) ProjectSetupMenuBtn (TopMenuContainer)
    public Button projectStartButton;  // 3) projectStartBtn

    [Header("연출")]
    [Range(0f, 1f)] public float dimAlpha = 0.8f;
    [Tooltip("dim이 0→dimAlpha로 빠르게 훅 들어오는 시간(초) — 짧을수록 순간 집중 유도")]
    public float dimFadeInDuration = 0.12f;
    [Tooltip("메뉴/패널 펼침 대기(초)")]
    public float settleDelay = 0.1f;
    [Tooltip("하이라이트 시 대상 버튼 둘레에서 dim을 걷어낼 기본 여백(px)")]
    public float highlightHolePadding = 14f;
    [Tooltip("여백이 pulse로 커졌다 작아지는 폭(px). highlightHolePadding보다 작아야 구멍이 항상 버튼보다 커서 버튼을 안 가림")]
    public float highlightPulseAmplitude = 6f;
    [Tooltip("하이라이트가 이전 대상 위치에서 다음 대상 위치로 슬라이드 이동하는 시간(초)")]
    public float holeMoveDuration = 0.3f;

    Canvas _dimCanvas;
    RectTransform _dimRoot;
    Image _dimTop, _dimBottom, _dimLeft, _dimRight;
    Coroutine _pulse;
    bool _running;
    Rect _currentHoleRect;
    bool _holeInitialized;

    void Start()
    {
        // [임시 비활성화] 프로젝트 튜토리얼 실행 안 함
        /*
        if (OnboardingState.ProjectTutorialDone) { Destroy(gameObject); return; }
        Instance = this;
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.OnTimeChanged += OnWeek;
        TryFire(); // pending==0(직전 만료, 리로드 복구)이면 즉시 실행
        */
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.OnTimeChanged -= OnWeek;
        EndDimTimeStop(); // 중간에 파괴돼도 시간 정지 누수 방지
    }

    // ── dim 동안 시간 정지 (패널 켜는 것과 동일) ──────────────────────────────
    bool _timeStopped;

    void BeginDimTimeStop()
    {
        if (_timeStopped) return;
        _timeStopped = true;
        OnboardingState.TutorialActive = true;            // MenuController 가 메뉴 숨김을 건너뛰도록 먼저 set
        GameTimeManager.Instance?.StopTime();
    }

    void EndDimTimeStop()
    {
        if (!_timeStopped) return;
        _timeStopped = false;
        OnboardingState.TutorialActive = false;
        GameTimeManager.Instance?.StartTime();
    }

    // 주차 경과마다 카운트다운
    void OnWeek()
    {
        if (OnboardingState.ProjectTutorialDone) return;
        int p = OnboardingState.ProjectTutorialPending;
        if (p > 0)
        {
            p--;
            OnboardingState.SetProjectTutorialPending(p);
        }
        if (p == 0) TryFire();
    }

    // 외부(채용 완료 직후 등)에서 호출 가능. pending==0(실행대기) + 모달 없을 때 실행.
    public void TryFire()
    {
        if (_running || OnboardingState.ProjectTutorialDone) return;
        if (OnboardingState.ProjectTutorialPending != 0) return; // 실행대기(0) 일 때만
        _running = true;
        // 모달(채용 리스트/이벤트 등)이 떠 있으면 닫힌 뒤 실행
        if (ModalGate.I != null) ModalGate.I.WhenFree(() => StartCoroutine(Run()));
        else StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(0.3f); // HUD 정착 대기

        EnsureDim();
        _dimCanvas.enabled = true;
        CollapseHole(); // 구멍 없이 전체 덮은 상태로 시작
        BeginDimTimeStop(); // 패널 켜는 것과 동일하게 dim 동안 시간 정지
        yield return FadeDimIn(); // 0 → dimAlpha 빠르게 훅 들어와 집중 유도

        yield return Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return Highlight(projectSetupButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return Highlight(projectStartButton);

        _dimCanvas.enabled = false;
        EndDimTimeStop();
        OnboardingState.MarkProjectTutorialDone();
        Destroy(gameObject);
    }

    // 대상 버튼 자리의 dim에 구멍을 뚫어 강조 + 구멍 크기 펄스 + 클릭 대기. (TutorialController 와 동일 패턴)
    // ⚠️ 대상 버튼 자신은 절대 건드리지 않는다 — dim 쪽 구멍만 그 버튼 자리에 맞춰 움직인다.
    IEnumerator Highlight(Button target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

        var targetRect = target.transform as RectTransform;

        // 이전 강조 위치에서 이번 대상 위치로 슬라이드 이동 — 곧장 나타나지 않고 이동하는 느낌을 줌.
        Rect destRect = ComputeHoleRect(targetRect, highlightHolePadding);
        if (_holeInitialized)
            yield return MoveHole(_currentHoleRect, destRect);
        else
        {
            ApplyHole(destRect);
            _currentHoleRect = destRect;
            _holeInitialized = true;
        }

        _pulse = StartCoroutine(PulseHole(targetRect));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        target.onClick.AddListener(cb);
        while (!clicked) yield return null;
        target.onClick.RemoveListener(cb);

        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        // CollapseHole() 미호출 — 위치를 유지해 다음 Highlight()가 여기서부터 이동해가게 함
    }

    IEnumerator PulseHole(RectTransform target)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 4f;
            float pad = highlightHolePadding + highlightPulseAmplitude * Mathf.Sin(t);
            _currentHoleRect = ComputeHoleRect(target, pad);
            ApplyHole(_currentHoleRect);
            yield return null;
        }
    }

    IEnumerator MoveHole(Rect from, Rect to)
    {
        float dur = Mathf.Max(0.0001f, holeMoveDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float eased = 1f - (1f - k) * (1f - k);
            _currentHoleRect = LerpRect(from, to, eased);
            ApplyHole(_currentHoleRect);
            yield return null;
        }
        _currentHoleRect = to;
        ApplyHole(to);
    }

    static Rect LerpRect(Rect a, Rect b, float t) => new Rect(
        Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t),
        Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    Rect ComputeHoleRect(RectTransform target, float padding)
    {
        var cam = _dimCanvas.worldCamera;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dimRoot, screenPoint, cam, out var localPoint);
            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }

        min -= new Vector2(padding, padding);
        max += new Vector2(padding, padding);
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    void ApplyHole(Rect hole)
    {
        Rect p = _dimRoot.rect;

        _dimTop.rectTransform.offsetMin    = new Vector2(0f, hole.yMax - p.yMin);
        _dimTop.rectTransform.offsetMax    = new Vector2(0f, 0f);

        _dimBottom.rectTransform.offsetMin = new Vector2(0f, 0f);
        _dimBottom.rectTransform.offsetMax = new Vector2(0f, hole.yMin - p.yMax);

        _dimLeft.rectTransform.offsetMin   = new Vector2(0f, hole.yMin - p.yMin);
        _dimLeft.rectTransform.offsetMax   = new Vector2(hole.xMin - p.xMax, hole.yMax - p.yMax);

        _dimRight.rectTransform.offsetMin  = new Vector2(hole.xMax - p.xMin, hole.yMin - p.yMin);
        _dimRight.rectTransform.offsetMax  = new Vector2(0f, hole.yMax - p.yMax);
    }

    void CollapseHole()
    {
        Rect p = _dimRoot.rect;
        var hole = new Rect(p.xMin, p.yMin, 0f, 0f);
        ApplyHole(hole);
        _currentHoleRect = hole;
    }

    void EnsureDim()
    {
        if (_dimCanvas != null) return;

        var go = new GameObject("ProjectTutorialDim", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        _dimCanvas = go.AddComponent<Canvas>();
        _dimCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _dimCanvas.worldCamera = Camera.main;
        _dimCanvas.planeDistance = 100f;
        _dimCanvas.overrideSorting = true;
        _dimCanvas.sortingOrder = 200;
        go.AddComponent<GraphicRaycaster>();

        _dimRoot = (RectTransform)go.transform;
        _dimRoot.anchorMin = Vector2.zero; _dimRoot.anchorMax = Vector2.one;
        _dimRoot.offsetMin = Vector2.zero; _dimRoot.offsetMax = Vector2.zero;

        _dimTop    = CreateDimStrip("DimTop");
        _dimBottom = CreateDimStrip("DimBottom");
        _dimLeft   = CreateDimStrip("DimLeft");
        _dimRight  = CreateDimStrip("DimRight");

        _dimCanvas.enabled = false;
    }

    Image CreateDimStrip(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_dimRoot, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, dimAlpha);
        img.raycastTarget = true;

        return img;
    }

    IEnumerator FadeDimIn()
    {
        SetDimAlpha(0f);
        float dur = Mathf.Max(0.0001f, dimFadeInDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            SetDimAlpha(Mathf.Lerp(0f, dimAlpha, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        SetDimAlpha(dimAlpha);
    }

    void SetDimAlpha(float a)
    {
        SetImgAlpha(_dimTop, a);
        SetImgAlpha(_dimBottom, a);
        SetImgAlpha(_dimLeft, a);
        SetImgAlpha(_dimRight, a);
    }

    void SetImgAlpha(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}
