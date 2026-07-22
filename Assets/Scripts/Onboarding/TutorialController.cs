using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 첫 게임씬 진입 시 1회 실행되는 온보딩 튜토리얼.
// 흐름: 비서 대사(DialogManager "tutorial_intro", 비서는 스폰 시점부터 이미 point1/master_desk에 서있음
//      — OfficeManager.SpawnSecretary 가 튜토리얼 런이면 desk_03 대신 거기서 바로 시작시킴) → TutorialPanel 대사
//      1-1(비서/나/비서 3줄) → 1-2(비서 1줄, TutorialDialog 차트) → 메뉴 버튼 강조 → (클릭→메뉴 열림)
//      → 직원 버튼 강조 → (클릭→서브 열림) → 채용하기 버튼 강조
//      → (클릭→TierPanel 열림) → tier1 버튼 강조(1단계만 선택 가능) → (클릭) → confirmBtn 강조 → (클릭) → 완료
//      → 시간이 다시 흐르는 순간(EndDimTimeStop) 비서는 GoToDesk()로 desk_03에 즉시 복귀.
//
// 강조 방식: 반투명 dim 풀스크린(클릭 차단)을 4조각(상하좌우)으로 쪼개 "대상 버튼 자리"만 구멍을 뚫는다.
// → 그 구멍 안엔 dim이 아예 없어서 버튼이 원래 밝기 그대로 보이고 클릭도 그대로 통과(대상은 전혀 안 건드림).
// 구멍 크기를 살짝 pulse 시켜 숨쉬듯 강조. 다음 대상으로 넘어갈 땐 곧장 나타나지 않고 이전 위치에서 그 자리로
// 슬라이드 이동(holeMoveDuration)해 스포트라이트가 옮겨가는 느낌을 준다. 대사 내용은 DialogManager 그룹(1단계)/
// TutorialDialog 차트(1-1,1-2)에서.
// ⚠️ hireButton은 강조는 그대로 유지하되, 누르는 순간(PointerDown)에 dim을 끔 — TierPanel(ModalLayer, useBlur)이
// 열리며 화면을 1회 캡처해 블러 배경으로 굳히는데, 그때 dim이 켜져있으면 배경이 새까맣게 캡처되어 이후 복구 불가.
// 클릭(PointerUp) 이후에 꺼서는 이미 늦어서(OpenHiring의 캡처가 그 안에서 먼저 끝남) 더 이른 PointerDown에 건다.
//
// OnboardingState.TutorialDone 으로 1회만 + RunStateManager.IsTutorial(=스크립트된 튜토리얼 런) 일 때만 실행.
// 버튼 참조는 MenuController 의 것을 인스펙터로 연결.
[DisallowMultipleComponent]
public class TutorialController : MonoBehaviour
{
    [Header("강조할 버튼 (순서대로)")]
    public Button menuButton;         // 1) 메뉴 열기
    public Button employeeButton;     // 2) 직원(상위)
    public Button hireButton;         // 3) 채용하기(하위) — 클릭 시 HiringUI.OpenHiring 이 열림
    public Button tier1Button;        // 4) TierPanel/tier1 — 채용 튜토리얼 중엔 1단계만 선택 가능
    public Button hireConfirmButton;  // 5) TierPanel/confirmBtn — 선택 확정

    [Header("비서 대사")]
    [Tooltip("DialogManager 그룹 ID. 그룹이 없으면 대사 스킵하고 바로 강조 진행. tutorial_intro 는 DialogManager 가 코드로 주입(비서 2줄).")]
    public string dialogGroupId = "tutorial_intro";

    [Header("TutorialPanel 대사 (TutorialDialog 차트, 버튼 강조 이전)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 비서/나 대사 3줄")]
    public string step1_1 = "1-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 1-1 표시 위치")]
    public Vector2 step1_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 채용 유도 1줄")]
    public string step1_2 = "1-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 1-2 표시 위치")]
    public Vector2 step1_2Position;

    [Header("연출")]
    [Range(0f, 1f)] public float dimAlpha = 0.8f;
    [Tooltip("dim이 0→dimAlpha로 빠르게 훅 들어오는 시간(초) — 짧을수록 순간 집중 유도")]
    public float dimFadeInDuration = 0.12f;
    [Tooltip("메뉴/서브 슬라이드 펼침 대기(초)")]
    public float settleDelay = 0.4f;
    [Tooltip("하이라이트 시 대상 버튼 둘레에서 dim을 걷어낼 기본 여백(px)")]
    public float highlightHolePadding = 14f;
    [Tooltip("여백이 pulse로 커졌다 작아지는 폭(px). highlightHolePadding보다 작아야 구멍이 항상 버튼보다 커서 버튼을 안 가림")]
    public float highlightPulseAmplitude = 6f;
    [Tooltip("하이라이트가 이전 대상 위치에서 다음 대상 위치로 슬라이드 이동하는 시간(초)")]
    public float holeMoveDuration = 0.15f;
    [Tooltip("게임씬 진입 후 DialogManager 준비를 기다리는 최대 시간(초). 준비되면 그 즉시 대사 표시.")]
    public float startupTimeout = 5f;

    Canvas _dimCanvas;
    RectTransform _dimRoot;
    Image _dimTop, _dimBottom, _dimLeft, _dimRight;
    Coroutine _pulse;
    Rect _currentHoleRect;   // 마지막으로 적용된 구멍 위치 — 다음 강조가 여기서부터 이동해감
    bool _holeInitialized;   // 아직 한 번도 강조 안 했으면(첫 대상) 이동 없이 그냥 나타남

    void Start()
    {
        if (OnboardingState.TutorialDone) { Destroy(gameObject); return; }
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsTutorial) { Destroy(gameObject); return; }
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // ── 1) 비서 대사 — 고정 대기 없이 DialogManager/DialogUI 준비되는 즉시 재생 ──
        // (게임씬 진입 직후 초기화가 한두 프레임 늦어도 "준비되면 바로" 띄워 빈 텀 최소화)
        if (!string.IsNullOrEmpty(dialogGroupId))
        {
            var dm = DialogManager.Instance;
            float wait = 0f;
            while ((dm == null || !dm.Initialized || !dm.HasDialogUI) && wait < startupTimeout)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
                dm = DialogManager.Instance;
            }

            if (dm != null && dm.Initialized && dm.HasDialogUI && dm.HasGroup(dialogGroupId))
            {
                bool ended = false;
                System.Action onEnd = () => ended = true;
                dm.OnDialogEnd += onEnd;
                dm.Play(dialogGroupId, triggerOnce: false);
                float t = 0f;
                while (!ended && t < 120f) { t += Time.unscaledDeltaTime; yield return null; } // 안전 타임아웃
                dm.OnDialogEnd -= onEnd;
            }
        }

        // ── 1-1, 1-2) TutorialPanel 대사 (TutorialDialog 차트, DialogUI와 별개) ──
        // 비서는 이미 point1/master_desk에 서있는 상태(OfficeManager.SpawnSecretary) — 여기서부터 시간 정지,
        // 버튼 강조 구간까지 계속 유지됨(EndDimTimeStop은 맨 끝 1회).
        BeginDimTimeStop();
        if (TutorialPanelUI.Instance != null)
        {
            yield return TutorialPanelUI.Instance.PlayStepGroup(step1_1, step1_1Position);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step1_2, step1_2Position);
        }

        // ── 2~4) 버튼 순차 강조 ──
        EnsureDim();
        _dimCanvas.enabled = true;
        CollapseHole(); // 구멍 없이 전체 덮은 상태로 시작
        yield return FadeDimIn(); // 0 → dimAlpha 빠르게 훅 들어와 집중 유도

        yield return Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 상위 메뉴 펼침
        yield return Highlight(employeeButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 서브 메뉴 펼침

        // ⚠️ hireButton 클릭(PointerUp) → HiringUI.OpenHiring()이 TierPanel(ModalLayer, useBlur=true)을 열면서
        // "같은 클릭 이벤트 안에서 동기적으로" 현재 화면을 캡처해 블러 배경으로 굳혀버린다(블러는 1회만 캡처,
        // 이후 갱신 안 됨). hireButton은 강조(스포트라이트)는 그대로 유지하되, 눌렀다가 클릭이 진짜 확정될
        // 때만 dim을 꺼서 그 직후 열리는 TierPanel이 밝은 배경으로 캡처되게 한다(자세한 이유는 Highlight 참고).
        yield return Highlight(hireButton, hideDimOnConfirmedClick: true);
        yield return new WaitForSecondsRealtime(settleDelay); // 채용 패널(TierPanel) 펼침 — 블러는 이미 밝은 배경으로 캡처됨

        _dimCanvas.enabled = true;
        CollapseHole();
        // TierPanel이 열리기 전(메뉴 쪽)과 후(패널 안)는 화면상 완전히 다른 위치라 이어서 슬라이드하면 안
        // 어울림 — 여기서 새 구간으로 리셋해서 tier1Button은 hireButton 자리에서 이동해오지 않고 그냥
        // 나타나게 하고, hireConfirmButton만 tier1Button에서 이어서 슬라이드하게(같은 패널 안이라 자연스러움).
        _holeInitialized = false;
        yield return Highlight(tier1Button);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return Highlight(hireConfirmButton);

        _dimCanvas.enabled = false;
        EndDimTimeStop();
        OnboardingState.MarkTutorialDone();
        Destroy(gameObject);
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

        // 시간이 다시 흐르는 즉시 비서를 자기 자리(desk_03)로 복귀시킴 (point1/master_desk에서 시작한 채였음)
        // ⚠️ 튜토리얼 중 씬을 나가는 경우(테스트/재시작 등) OnDestroy가 여기로 이어지는데, 그 시점엔 이미
        // OfficeManager나 비서 캐릭터가 파괴되어 있을 수 있다. Unity Object는 "파괴됐지만 참조는 non-null"인
        // 상태가 되므로 `?.`(널 조건 연산자)는 이 파괴 여부를 못 걸러낸다 — 반드시 `!=null` 비교로 확인해야 함.
        var om = OfficeManager.Instance;
        if (om == null) return;
        var oc = om.GetCharacter(om.secretaryId);
        if (oc == null) return;
        oc.GoToDesk();
    }

    void OnDestroy() => EndDimTimeStop(); // 중간에 파괴돼도 시간 정지 누수 방지

    // 대상 버튼 자리의 dim에 구멍을 뚫어 강조 + 구멍 크기 펄스 + 클릭 대기. 클릭되면 구멍을 닫고 반환.
    // ⚠️ 대상 버튼 자신은 절대 건드리지 않는다 — Canvas/GraphicRaycaster/Scale 등 아무 컴포넌트도 추가하지
    // 않고, dim 쪽 구멍만 그 버튼 자리에 맞춰 움직인다. 구멍엔 아무것도 안 그려지므로 버튼은 원래 밝기 그대로
    // 보이고 클릭도 그대로 통과한다(dim의 GraphicRaycaster가 그 자리엔 아예 히트할 그래픽이 없음).
    // hideDimOnConfirmedClick 지정 시(현재 hireButton 전용) — TierPanel(ModalLayer, useBlur)이 열리며
    // 화면을 캡처하기 전에 dim부터 꺼야 하는데, 그 캡처는 클릭(PointerUp 직후 PointerClick)이 처리되는 바로
    // 그 순간 동기적으로 일어나 우리 onClick 리스너(항상 Inspector persistent 리스너보다 늦게 실행됨)로는
    // 손을 쓸 수 없다. 그렇다고 PointerDown에서 바로 꺼버리면, 누르기만 하고 드래그로 이탈해 릴리즈해서
    // 클릭이 성사되지 않은 경우에도 dim이 사라져버린다(사용자 확인 필요 지적). 그래서:
    //  1) PointerDown 시점에 일단 dim을 끈다(클릭 확정 여부와 무관하게 캡처 시점보다는 반드시 앞서야 하므로).
    //  2) PointerUp(성사 여부와 무관하게 눌렀던 대상에 항상 발생)에서 한 프레임 뒤 clicked를 확인 — 진짜
    //     클릭이면 같은 프레임 안에서 PointerUp 바로 다음에 PointerClick(→onClick)이 처리되므로, 한 프레임만
    //     기다리면 "클릭이 진짜 확정됐는지"를 정확히 알 수 있다. 확정 안 됐으면 dim을 되돌린다.
    IEnumerator Highlight(Button target, bool hideDimOnConfirmedClick = false)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

        var targetRect = target.transform as RectTransform;

        // 이전 강조 위치에서 이번 대상 위치로 슬라이드 이동 — 곧장 나타나지 않고 "이동하는" 느낌을 주기 위함.
        // 첫 강조(아직 _currentHoleRect 없음)는 이동할 이전 위치가 없으니 그냥 나타남.
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

        EventTrigger trigger = null;
        if (hideDimOnConfirmedClick)
        {
            trigger = target.gameObject.AddComponent<EventTrigger>();

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => _dimCanvas.enabled = false);
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(_ => StartCoroutine(RestoreDimIfNotConfirmed()));
            trigger.triggers.Add(upEntry);
        }

        while (!clicked) yield return null;
        target.onClick.RemoveListener(cb);
        if (trigger != null) Destroy(trigger);

        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        // ⚠️ 여기서 CollapseHole()을 부르지 않는다 — 구멍을 이 위치에 그대로 남겨둬야 다음 Highlight()가
        // 여기서부터 다음 대상으로 슬라이드 이동해갈 수 있음(곧장 새 위치에 나타나지 않고 이동하는 느낌).

        IEnumerator RestoreDimIfNotConfirmed()
        {
            yield return null;
            if (!clicked) _dimCanvas.enabled = true; // 눌렀지만 클릭 미확정(드래그로 이탈 등) → dim 원복
        }
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

    // 구멍을 from → to로 슬라이드 이동시킨다(ease-out) — 강조가 곧장 나타나지 않고 이동하는 느낌을 줌.
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

    // target의 화면상 사각형을 dim 루트의 로컬 좌표계로 변환 + padding 적용.
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

    // dim 4조각(상하좌우)을 재배치해 hole 자리만 비운다 — 나머지는 dimAlpha로 덮음.
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

    // 구멍 없이 dim 전체를 덮은 상태로 되돌림(top 조각 혼자 화면 전체를 덮고 나머지는 0 크기로 접힘).
    void CollapseHole()
    {
        Rect p = _dimRoot.rect;
        var hole = new Rect(p.xMin, p.yMin, 0f, 0f);
        ApplyHole(hole);
        _currentHoleRect = hole; // 다음 강조는 여기(접힌 점)에서부터 이동해 나타남
    }

    void EnsureDim()
    {
        if (_dimCanvas != null) return;

        var go = new GameObject("TutorialDim", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        _dimCanvas = go.AddComponent<Canvas>();
        _dimCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _dimCanvas.worldCamera = Camera.main;
        _dimCanvas.planeDistance = 100f;
        _dimCanvas.overrideSorting = true;
        _dimCanvas.sortingOrder = 200;                   // 게임 UI(10~14)·ModalBlocker(50) 보다 위
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
        img.raycastTarget = true; // 덮인 부분만 클릭 차단 — 구멍 자리엔 조각이 없어 자연히 통과

        return img;
    }

    // dim 4조각을 0 → dimAlpha로 짧게 훅 페이드인 — 갑자기 어두워지며 시선을 확 모으는 연출.
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
