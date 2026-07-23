using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 튜토리얼 강조(dim 스포트라이트) 공용 컴포넌트 — TutorialController/ProjectTutorialController 등
// 여러 튜토리얼 컨트롤러가 각자 중복 구현하던 dim 구멍뚫기 로직을 여기 하나로 통합.
// 새 튜토리얼을 만들 때는 이 컴포넌트를 gameObject.AddComponent 로 붙이고 Show()/Highlight()/Hide()만
// 순서대로 호출하면 된다 — dim 생성/구멍 계산/펄스/페이드/슬라이드 이동은 전부 여기서 처리.
//
// 강조 방식: 반투명 dim 풀스크린(클릭 차단)을 "대상 자리"만 빼고 그리드로 쪼갠 타일들로 덮는다(ApplyHoles).
// 구멍 안엔 dim이 아예 없어서 대상은 원래 밝기 그대로 보이고 클릭도 그대로 통과(대상 자체는 전혀 안 건드림).
// 구멍 크기를 살짝 pulse 시켜 숨쉬듯 강조. 연속 단일-대상 Highlight/BeginHighlight 호출 시 이전 위치에서
// 다음 위치로 슬라이드 이동한다. 구멍은 1개(BeginHighlight/Highlight)든 여러 개(HighlightMultiUntilClick)든
// 동일한 그리드 분할 알고리즘(ApplyHoles)으로 처리된다.
public class TutorialHighlighter : MonoBehaviour
{
    [Header("연출")]
    [Range(0f, 1f)] public float dimAlpha = 0.8f;
    [Tooltip("dim이 0→dimAlpha로 빠르게 훅 들어오는 시간(초)")]
    public float dimFadeInDuration = 0.12f;
    [Tooltip("하이라이트 시 대상 둘레에서 dim을 걷어낼 기본 여백(px)")]
    public float highlightHolePadding = 14f;
    [Tooltip("여백이 pulse로 커졌다 작아지는 폭(px). highlightHolePadding보다 작아야 구멍이 항상 대상보다 커서 안 가림")]
    public float highlightPulseAmplitude = 6f;
    [Tooltip("하이라이트가 이전 대상 위치에서 다음 대상 위치로 슬라이드 이동하는 시간(초)")]
    public float holeMoveDuration = 0.15f;
    [Tooltip("게임 UI/ModalBlocker 보다 위에 그려지도록 하는 dim Canvas sortingOrder")]
    public int dimSortingOrder = 200;

    Canvas _dimCanvas;
    RectTransform _dimRoot;
    readonly List<Image> _tilePool = new(); // 구멍(1개든 여러 개든) 주변을 덮는 dim 타일 풀 — 필요한 만큼만 활성화
    Coroutine _pulse;
    Rect _currentHoleRect;   // 마지막으로 적용된 단일 구멍 위치 — 다음 단일 강조가 여기서부터 이동해감
    bool _holeInitialized;   // 아직 한 번도 강조 안 했으면(첫 대상) 이동 없이 그냥 나타남
    Button _holeCatcher;     // Highlight() 전용 — 구멍(대상+패딩) 전체를 덮는 투명 클릭캐처(아래 참고)

    // ── 공개 API ──────────────────────────────────────────────────────
    // dim 준비 + 구멍 없이 전체 덮은 상태로 훅 페이드인. 강조 시퀀스 시작 시 1회 호출.
    public IEnumerator Show()
    {
        EnsureDim();
        _dimCanvas.enabled = true;
        CollapseHole();
        yield return FadeDimIn();
    }

    // dimAlpha → 0 페이드아웃 후 캔버스 비활성. 강조 시퀀스 끝날 때 1회 호출.
    public IEnumerator Hide()
    {
        if (_dimCanvas == null) yield break;
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        if (_holeCatcher != null) _holeCatcher.gameObject.SetActive(false); // 방어적 정리(Highlight 도중 중단 대비)
        yield return FadeDimOut();
        _dimCanvas.enabled = false;
        _holeInitialized = false; // 다음 Show()는 새 위치에 바로 나타남(슬라이드 없음)
    }

    // 화면상 완전히 다른 영역으로 넘어갈 때(예: 메뉴 → 팝업 패널 안) 페이드 없이 즉시 구멍을 접고,
    // 다음 Highlight가 슬라이드 이동 없이 새 위치에 바로 나타나게 origin을 리셋한다.
    // ⚠️ hideDimOnConfirmedClick(예: hireButton)로 직전 Highlight가 끝나면 _dimCanvas가 꺼진 채로
    // 남아있으므로, 여기서 반드시 다시 켜야 다음 Highlight가 보인다.
    public void CollapseAndResetOrigin()
    {
        EnsureDim();
        // 직전이 BeginHighlight(펄스가 안 멈추고 계속 도는 강조)였다면, 펄스 코루틴이 다음 프레임에
        // 다시 그 자리에 구멍을 그려서 CollapseHole()을 무효화해버린다 — 반드시 먼저 멈춰야 한다.
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        if (_holeCatcher != null) _holeCatcher.gameObject.SetActive(false); // 방어적 정리(Highlight 도중 중단 대비)
        _dimCanvas.enabled = true;
        CollapseHole();
        _holeInitialized = false;
    }

    // 대상 버튼 자리의 dim에 구멍을 뚫어 강조 + 구멍 크기 펄스 + 클릭 대기. 클릭되면 구멍을 닫지 않고 반환
    // (다음 Highlight가 여기서부터 슬라이드 이동해감). 대상 자체는 절대 건드리지 않는다.
    // hideDimOnConfirmedClick: 클릭이 다른 모달(블러 캡처 등)을 동기적으로 여는 경우, 그 캡처 전에 dim을
    // 꺼야 할 때 사용(PointerDown에서 미리 끄고, 클릭 미확정 시 PointerUp 한 프레임 뒤 원복).
    //
    // ⚠️ 구멍은 대상 버튼보다 패딩(highlightHolePadding±pulse)만큼 더 크게 뚫리는데, 이 여백은 순전히
    // 시각적인 것이라 버튼 자신의 실제 히트박스는 넓어지지 않는다 — 그 여백을 누르면 dim 구멍이라 dim에는
    // 안 걸리지만 버튼도 안 맞아 클릭이 그대로 배경까지 뚫고 내려가버린다. 메뉴 버튼처럼 "빈 곳 클릭 시
    // 메뉴 닫힘" 로직이 있는 대상이면 이 상태에서 그 로직이 "외부 클릭"으로 오판해 메뉴가 닫히고, 하이라이트는
    // (버튼이 사라졌으니) 영원히 클릭을 못 받아 튜토리얼이 멈춘다. 구멍 전체(대상+패딩)를 덮는 투명
    // 클릭캐처(_holeCatcher)를 dim 캔버스(sortingOrder=dimSortingOrder, 메뉴보다 항상 위) 소속으로 띄워서
    // 해결 — 패딩 클릭도 캐처가 받아 target.onClick으로 그대로 포워딩하고, 캐처 자신이 메뉴보다 위 캔버스에
    // 있어 MenuController의 "외부 클릭=닫기" 판정도 안 탄다(오버레이가 클릭을 가로챈 것으로 인식됨).
    public IEnumerator Highlight(Button target, bool hideDimOnConfirmedClick = false)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

        yield return MoveOrAppear(target.transform as RectTransform);
        _pulse = StartCoroutine(PulseHole(target.transform as RectTransform));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        target.onClick.AddListener(cb);

        var catcher = EnsureHoleCatcher();
        float maxPad = highlightHolePadding + highlightPulseAmplitude;
        PositionCatcher(catcher, ComputeHoleRect(target.transform as RectTransform, maxPad));
        catcher.gameObject.SetActive(true);
        UnityEngine.Events.UnityAction catcherCb = () => target.onClick.Invoke();
        catcher.onClick.AddListener(catcherCb);

        EventTrigger trigger = null, catcherTrigger = null;
        if (hideDimOnConfirmedClick)
        {
            trigger = AddPressReleaseTrigger(target.gameObject, () => clicked);
            catcherTrigger = AddPressReleaseTrigger(catcher.gameObject, () => clicked);
        }

        while (!clicked) yield return null;
        target.onClick.RemoveListener(cb);
        catcher.onClick.RemoveListener(catcherCb);
        catcher.gameObject.SetActive(false);
        if (trigger != null) Destroy(trigger);
        if (catcherTrigger != null) Destroy(catcherTrigger);

        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
    }

    // PointerDown 즉시 dim을 끄고, PointerUp 한 프레임 뒤 클릭이 확정 안 됐으면(드래그로 이탈 등) 원복.
    // target/캐처 양쪽에 동일하게 붙여써서 어느 쪽을 눌러도 같은 "확정 전 미리 끔" 동작을 보장한다.
    // ⚠️ "dim을 끔"은 _dimCanvas.enabled가 아니라 타일만 끈다(HideDimTilesOnly) — 캔버스 자체를 끄면 같은
    // 캔버스에 사는 _holeCatcher까지 그 순간 raycast 대상에서 빠져버려서, 패딩(캐처) 클릭으로 대상을 누른
    // 경우 캐처 자신의 PointerUp/Click 판정이 깨져 클릭이 영영 확정 안 되는 문제가 있었다(hireButton 사례).
    EventTrigger AddPressReleaseTrigger(GameObject go, System.Func<bool> wasClicked)
    {
        var trigger = go.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(_ => HideDimTilesOnly());
        trigger.triggers.Add(downEntry);

        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener(_ => StartCoroutine(RestoreDimIfNotConfirmed(wasClicked)));
        trigger.triggers.Add(upEntry);

        return trigger;
    }

    void HideDimTilesOnly()
    {
        foreach (var tile in _tilePool) tile.gameObject.SetActive(false);
    }

    // target을 강조한 채로 "유지"하고 즉시 반환(클릭/시간 대기 없음) — 호출부가 그동안 TutorialPanel 대사 등
    // 다른 걸 진행하고, 다 끝나면 Hide() 또는 다음 BeginHighlight로 넘어가면 된다. 이전 대상에서 슬라이드 이동.
    public IEnumerator BeginHighlight(RectTransform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;
        yield return MoveOrAppear(target);
        if (_pulse != null) StopCoroutine(_pulse);
        _pulse = StartCoroutine(PulseHole(target));
    }

    // 여러 대상을 동시에 강조한 채로 clickTarget 클릭을 기다린다(슬라이드 없이 바로 나타남). 3-4처럼
    // "카드 전체 + 확정 버튼"을 한 번에 짚어줘야 할 때 사용.
    public IEnumerator HighlightMultiUntilClick(Button clickTarget, params RectTransform[] highlightTargets)
    {
        if (clickTarget == null || !clickTarget.gameObject.activeInHierarchy) yield break;

        EnsureDim();
        _holeInitialized = false; // 다중 구멍은 슬라이드 대상이 아님 — 다음 단일 강조는 새로 나타나야 함

        if (_pulse != null) StopCoroutine(_pulse);
        _pulse = StartCoroutine(PulseHoles(highlightTargets));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        clickTarget.onClick.AddListener(cb);
        while (!clicked) yield return null;
        clickTarget.onClick.RemoveListener(cb);

        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
    }

    // ── 내부 공통 구현 ────────────────────────────────────────────────
    IEnumerator MoveOrAppear(RectTransform target)
    {
        EnsureDim();
        Rect destRect = ComputeHoleRect(target, highlightHolePadding);
        if (_holeInitialized)
            yield return MoveHole(_currentHoleRect, destRect);
        else
        {
            ApplyHole(destRect);
            _currentHoleRect = destRect;
            _holeInitialized = true;
        }
    }

    IEnumerator RestoreDimIfNotConfirmed(System.Func<bool> wasClicked)
    {
        yield return null;
        if (!wasClicked()) ApplyHole(_currentHoleRect); // 눌렀지만 클릭 미확정(드래그로 이탈 등) → 타일 원복
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

    IEnumerator PulseHoles(RectTransform[] targets)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 4f;
            float pad = highlightHolePadding + highlightPulseAmplitude * Mathf.Sin(t);
            var rects = new List<Rect>(targets.Length);
            foreach (var tr in targets)
                if (tr != null) rects.Add(ComputeHoleRect(tr, pad));
            ApplyHoles(rects);
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

    void ApplyHole(Rect hole) => ApplyHoles(new List<Rect> { hole });

    // dim 화면을 "구멍이 아닌 영역"만 덮는 타일들로 채운다 — 구멍 개수(0개=전체 덮음, 1개, N개)에 무관하게
    // 동일한 그리드 분할로 처리(홀들의 x/y 경계로 격자를 나누고, 격자 칸 중심이 어떤 홀에도 안 들어가면
    // 그 칸만 dim 타일로 채움). 구멍이 겹치지만 않으면 몇 개든 동시에 뚫을 수 있다.
    void ApplyHoles(List<Rect> holes)
    {
        Rect p = _dimRoot.rect;

        var xs = new List<float> { p.xMin, p.xMax };
        var ys = new List<float> { p.yMin, p.yMax };
        foreach (var h in holes)
        {
            xs.Add(Mathf.Clamp(h.xMin, p.xMin, p.xMax));
            xs.Add(Mathf.Clamp(h.xMax, p.xMin, p.xMax));
            ys.Add(Mathf.Clamp(h.yMin, p.yMin, p.yMax));
            ys.Add(Mathf.Clamp(h.yMax, p.yMin, p.yMax));
        }
        xs = xs.Distinct().OrderBy(x => x).ToList();
        ys = ys.Distinct().OrderBy(y => y).ToList();

        int used = 0;
        for (int i = 0; i < xs.Count - 1; i++)
        {
            for (int j = 0; j < ys.Count - 1; j++)
            {
                var cell = new Vector2((xs[i] + xs[i + 1]) * 0.5f, (ys[j] + ys[j + 1]) * 0.5f);
                bool insideHole = false;
                foreach (var h in holes)
                    if (h.Contains(cell)) { insideHole = true; break; }
                if (insideHole) continue;

                var tile = GetTile(used++);
                var rt = tile.rectTransform;
                // anchorMin=anchorMax=(0,0)인 자식의 anchoredPosition은 "부모 rect의 (0,0) 앵커 기준점"
                // (=부모 pivot에 따라 대개 p.xMin/p.yMin, 0이 아님)으로부터의 오프셋이라, xs/ys(dimRoot
                // 로컬 절대좌표)를 그대로 넣으면 p.xMin/p.yMin만큼 이중으로 밀려서 화면 밖으로 나간다.
                // 기준점을 빼서 순수 오프셋으로 변환해야 한다.
                rt.anchoredPosition = new Vector2(xs[i] - p.xMin, ys[j] - p.yMin);
                rt.sizeDelta = new Vector2(xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                tile.gameObject.SetActive(true);
            }
        }
        for (int k = used; k < _tilePool.Count; k++)
            _tilePool[k].gameObject.SetActive(false);
    }

    Image GetTile(int index)
    {
        while (_tilePool.Count <= index)
            _tilePool.Add(CreateDimTile($"DimTile{_tilePool.Count}"));
        return _tilePool[index];
    }

    // Highlight() 전용 — 구멍(대상+패딩) 전체를 덮는 완전 투명 클릭캐처. dim 타일과 같은 부모(_dimRoot)라
    // 같은 Canvas(sortingOrder=dimSortingOrder)에 속해 항상 메뉴/일반 UI보다 위에서 클릭을 받는다.
    Button EnsureHoleCatcher()
    {
        if (_holeCatcher != null) return _holeCatcher;

        var go = new GameObject("TutorialHoleCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_dimRoot, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // 완전 투명 — 시각 변화 없이 클릭만 받음
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        _holeCatcher = btn;
        go.SetActive(false);
        return btn;
    }

    // dim 타일과 동일한 좌표 변환(부모 rect 기준점 보정)으로 캐처를 hole 위치/크기에 맞춤.
    void PositionCatcher(Button catcher, Rect hole)
    {
        Rect p = _dimRoot.rect;
        var rt = (RectTransform)catcher.transform;
        rt.anchoredPosition = new Vector2(hole.xMin - p.xMin, hole.yMin - p.yMin);
        rt.sizeDelta = new Vector2(hole.width, hole.height);
    }

    // 구멍 없이 dim 전체를 덮은 상태로 되돌림.
    void CollapseHole()
    {
        ApplyHoles(new List<Rect>());
        Rect p = _dimRoot.rect;
        _currentHoleRect = new Rect(p.xMin, p.yMin, 0f, 0f); // 다음 강조는 여기(접힌 점)에서부터 이동해 나타남
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
        _dimCanvas.sortingOrder = dimSortingOrder;
        go.AddComponent<GraphicRaycaster>();

        _dimRoot = (RectTransform)go.transform;
        _dimRoot.anchorMin = Vector2.zero; _dimRoot.anchorMax = Vector2.one;
        _dimRoot.offsetMin = Vector2.zero; _dimRoot.offsetMax = Vector2.zero;

        _dimCanvas.enabled = false;
    }

    // 좌하단 피벗/앵커 고정 — ApplyHoles가 anchoredPosition+sizeDelta로 절대 배치한다.
    Image CreateDimTile(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_dimRoot, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, dimAlpha);
        img.raycastTarget = true; // 덮인 부분만 클릭 차단 — 구멍 자리엔 타일이 없어 자연히 통과

        return img;
    }

    // dim을 0 → dimAlpha로 짧게 훅 페이드인 — 갑자기 어두워지며 시선을 확 모으는 연출.
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

    // FadeDimIn의 대칭 — dimAlpha → 0으로 짧게 빠져나감.
    IEnumerator FadeDimOut()
    {
        float dur = Mathf.Max(0.0001f, dimFadeInDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            SetDimAlpha(Mathf.Lerp(dimAlpha, 0f, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        SetDimAlpha(0f);
    }

    void SetDimAlpha(float a)
    {
        foreach (var tile in _tilePool)
        {
            var c = tile.color;
            c.a = a;
            tile.color = c;
        }
    }
}
