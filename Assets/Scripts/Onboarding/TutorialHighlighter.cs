using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 튜토리얼 강조(dim 스포트라이트) 공용 컴포넌트 — TutorialController 등
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
    [Range(0f, 1f)] public float dimAlpha = 0.95f;
    [Tooltip("dim이 0→dimAlpha로 빠르게 훅 들어오는 시간(초)")]
    public float dimFadeInDuration = 0.12f;
    [Tooltip("하이라이트 시 대상 둘레에서 dim을 걷어낼 기본 여백(px)")]
    public float highlightHolePadding = 14f;
    [Tooltip("여백이 pulse로 커졌다 작아지는 폭(px). highlightHolePadding보다 작아야 구멍이 항상 대상보다 커서 안 가림")]
    public float highlightPulseAmplitude = 6f;
    [Tooltip("하이라이트가 이전 대상 위치에서 다음 대상 위치로 슬라이드 이동하는 시간(초)")]
    public float holeMoveDuration = 0.15f;
    [Tooltip("새 대상이 처음 나타날 때(첫 강조 또는 CollapseAndResetOrigin 직후) 구멍이 대상보다 이 값(px)만큼 더 크게 벌어진 채로 시작해서 사방에서 좁혀지듯 대상 크기로 줄어든다. ⚠️ 너무 크면 시작 구멍이 화면 대부분(직전 강조 위치 포함)을 덮어버려서, 대상과 완전히 다른 위치라도 마치 직전 위치에서 슬라이드해온 것처럼 보인다 — 대상 크기 대비 적당히 작게 유지할 것")]
    public float appearExpandPadding = 70f;
    [Tooltip("위 '사방에서 좁혀지는' 등장 연출의 소요 시간(초) — holeMoveDuration과 별개로 조절 가능")]
    public float appearDuration = 0.28f;
    [Tooltip("게임 UI/ModalBlocker 보다 위에 그려지도록 하는 dim Canvas sortingOrder")]
    public int dimSortingOrder = 200;

    [Header("클릭 유도 손 아이콘 (선택적)")]
    [Tooltip("Highlight/BeginHighlight의 showHand=true일 때 표시할 손 스프라이트")]
    public Sprite handSprite;
    [Tooltip("손 아이콘에 재생할 애니메이터 컨트롤러(비워두면 애니메이션 없이 정지 이미지)")]
    public RuntimeAnimatorController handAnimatorController;
    [Tooltip("손 아이콘 크기(px)")]
    public Vector2 handSize = new Vector2(81f, 81f);
    [Tooltip("대상 오른쪽 가장자리로부터 손 아이콘을 추가로 띄우는 여백(px) — 클수록 대상과 덜 겹치고 더 오른쪽에 위치")]
    public float handOffsetX = 20f;

    Canvas _dimCanvas;
    RectTransform _dimRoot;
    readonly List<Image> _tilePool = new(); // 구멍(1개든 여러 개든) 주변을 덮는 dim 타일 풀 — 필요한 만큼만 활성화
    Image _handImage; // 클릭 유도 손 아이콘 — 단일 인스턴스, showHand 요청 시에만 활성화
    Coroutine _pulse;

    // _pulse는 여러 PlayTutorialX() 코루틴이 겹쳐 돌 때(예: 5-4의 Hide 흐름과 6-1의 Show/Highlight 흐름이
    // 같은 클릭으로 거의 동시에 진행) 공유되는 필드라, "내가 시작한 펄스"를 그냥 필드로 stop하면 그 사이
    // 다른 세션이 필드를 자기 펄스로 덮어썼을 경우 엉뚱하게 남의 펄스를 죽이고 내 펄스는 고아로 계속 돈다
    // (5-4의 마지막 강조가 안 사라지고 6-1 하이라이트 순간에야 사라지던 버그의 원인). 그래서 시작한
    // 코루틴 핸들을 로컬로 들고 있다가 "그 핸들"을 직접 Stop하고, 필드는 아직도 그 핸들을 가리킬 때만 null.
    Coroutine StartPulse(IEnumerator routine)
    {
        var c = StartCoroutine(routine);
        _pulse = c;
        return c;
    }

    // 내가 시작한 펄스(myPulse)만 정확히 멈춘다 — 그 사이 다른 세션이 _pulse를 자기 것으로 덮어썼어도 안전.
    void StopPulse(Coroutine myPulse)
    {
        if (myPulse != null) StopCoroutine(myPulse);
        if (_pulse == myPulse) _pulse = null;
        HideHand(); // 이 펄스가 showHand로 띄웠을 수 있는 손 아이콘도 함께 정리
    }

    // 지금 _pulse에 뭐가 들어있든(누구 세션이든) 무조건 멈춘다 — Hide()/CollapseAndResetOrigin()처럼
    // "다음으로 넘어가기 전에 떠 있는 펄스는 전부 정리"가 목적인 곳에서만 사용.
    void StopAnyPulse()
    {
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        HideHand();
    }
    Rect _currentHoleRect;   // 마지막으로 적용된 단일 구멍 위치 — 다음 단일 강조가 여기서부터 이동해감
    bool _holeInitialized;   // 아직 한 번도 강조 안 했으면(첫 대상) 이동 없이 그냥 나타남
    Button _holeCatcher;     // Highlight() 전용 — 구멍(대상+패딩) 전체를 덮는 투명 클릭캐처(아래 참고)

    // Show()/Hide() 레이스 가드용 세대 카운터 — 같은 TutorialController가 1-1~8-1 내내 하나의
    // TutorialHighlighter 인스턴스를 계속 재사용하는데, 서로 다른 PlayTutorialX() 코루틴의 Show()/Hide()가
    // "같은 클릭"으로 거의 동시에(심지어 같은 프레임 안에서 StartCoroutine이 동기 실행되며) 겹쳐 돌 수 있다.
    // ⚠️ Hide()가 "자기 시작 시점의 _generation"을 읽는 방식은 불충분하다 — 실측 결과 늦은 Hide()가 이미
    // 새 Show()가 세대를 올린 "이후"에야 시작되는 경우가 있어서(같은 세대값을 관측), 시작 시점 비교로는
    // 걸러지지 않는다. 그래서 Show()는 자신의 세대를 "리턴값처럼" 호출부에 반드시 넘겨주고, 호출부가 그
    // 값을 로컬 변수에 들고 있다가 대응되는 Hide()에 명시적으로 전달해야 한다 — Hide()가 호출되는 "시점"이
    // 아니라 그 Hide()와 짝을 이루는 Show()가 "실제로 몇 번째 세대였는지"로 판단해야 늦게 끝나는 stale
    // Hide()가 그 사이 시작된 새 Show()를 덮어쓰는 걸 막을 수 있다.
    int _generation;

    // Show() 호출 직후 호출부가 캡처해서 대응되는 Hide()에 넘겨야 하는 세대값.
    public int CurrentGeneration => _generation;

    // 디버그 점프("N부터" 등) 전용 — 진행 중이던 Show()/Hide()/Highlight()/BeginHighlight() 코루틴이
    // 뭐든 간에 전부 무시하고 즉시(페이드 없이) dim/구멍/캐처/손 아이콘을 전부 끈다. 세대도 올려서
    // 아직 살아있는 stale Hide()가 나중에 도착해도 아무것도 안 건드리게 만든다. 정상 흐름에서는 절대
    // 쓰지 말 것 — Show()/Hide() 페어를 그대로 쓸 것.
    public void ForceHideImmediate()
    {
        StopAllCoroutines();
        _pulse = null;
        _dimHiddenForPress = false;
        if (_holeCatcher != null) _holeCatcher.gameObject.SetActive(false);
        HideHand();
        if (_dimCanvas != null) _dimCanvas.enabled = false;
        _holeInitialized = false;
        _generation++;
    }

    // ── 공개 API ──────────────────────────────────────────────────────
    // dim 준비 + 구멍 없이 전체 덮은 상태로 훅 페이드인. 강조 시퀀스 시작 시 1회 호출.
    // 리턴하는 세대값을 호출부가 들고 있다가 Hide(그 값)로 넘겨야 레이스가 안전하다.
    public IEnumerator Show()
    {
        EnsureDim();
        _generation++;
        // ⚠️ 방어적으로 무조건 정리 — 직전 세션의 pulse가 어떤 이유로든(레이스, 예외 등) 자기 정리를 못 하고
        // 남아있으면 매 프레임 자기 자리에 구멍을 계속 다시 뚫어서(PulseHole), 지금부터 시작하는 이 새
        // 세션의 CollapseHole()이 화면을 다 덮어도 다음 프레임에 그 자리가 다시 뚫려버린다 — 5-4 confirm
        // 버튼 자리처럼 완전히 다른 화면으로 넘어간 뒤에도 예전 하이라이트가 안 없어지고 남아있던 원인.
        StopAnyPulse();
        _dimCanvas.enabled = true;
        CollapseHole();
        // ⚠️ 반드시 여기서 리셋 — 직전 세션의 Hide()가 레이스 가드에 걸려 스킵된 경우(늦게 끝나서 자기
        // 세대가 이미 낡았다고 판단해 정리를 건너뜀) _holeInitialized가 true로 남아있을 수 있다. 그 상태로
        // 두면 이번 세션의 첫 Highlight/BeginHighlight가 "이어서 슬라이드"로 오판해서, 이전 세션(예: 5-4)의
        // 마지막 강조 위치에서 이번 세션(예: 6-1)의 첫 대상으로 하이라이트가 이동하는 것처럼 보인다.
        // Show()는 항상 "새 세션의 시작"이므로 무조건 여기서 false로 강제해 다음 강조가 새로 나타나게 한다.
        _holeInitialized = false;
        yield return FadeDimIn();
    }

    // dimAlpha → 0 페이드아웃 후 캔버스 비활성. 강조 시퀀스 끝날 때 1회 호출.
    // myGeneration: 이 Hide()와 짝을 이루는 Show() 직후 CurrentGeneration으로 캡처해둔 값을 반드시 전달할 것 —
    // 생략(null)하면 호출 시점의 _generation을 대신 읽지만, 이미 다른 코루틴의 새 Show()가 세대를 올린
    // "이후"에 이 Hide()가 시작됐다면(관측된 실제 레이스 패턴) 같은 값을 읽어버려 가드가 무력화된다.
    public IEnumerator Hide(int? myGeneration = null)
    {
        if (_dimCanvas == null) yield break;
        int gen = myGeneration ?? _generation;
        StopAnyPulse();
        if (_holeCatcher != null) _holeCatcher.gameObject.SetActive(false); // 방어적 정리(Highlight 도중 중단 대비)
        yield return FadeDimOut(gen);
        if (_generation != gen) yield break; // 내 세대 이후 새 Show()가 시작됨 — 그쪽 상태를 건드리지 않는다
        _dimCanvas.enabled = false;
        _holeInitialized = false; // 다음 Show()는 새 위치에 바로 나타남(슬라이드 없음)
    }

    // 화면상 완전히 다른 영역으로 넘어갈 때(예: 메뉴 → 팝업 패널 안) 페이드 없이 즉시 구멍을 접고,
    // 다음 Highlight가 슬라이드 이동 없이 새 위치에 바로 나타나게 origin을 리셋한다.
    // ⚠️ hideDimOnConfirmedClick(예: hireButton)로 직전 Highlight가 끝나면 _dimCanvas가 꺼진 채로
    // 남아있으므로, 여기서 반드시 다시 켜야 다음 Highlight가 보인다.
    // ⚠️ 세대(_generation)는 여기서 올리지 않는다 — 이 메서드는 항상 "같은 코루틴" 안에서, 그 코루틴이
    // 이미 자기 Show() 뒤에 캡처해둔 genN을 여전히 들고 있다가 뒤이어 나올 자기 Hide(genN)에 넘길 목적으로
    // 호출된다. 여기서 세대를 올리면 그 genN이 곧바로 stale 취급돼서, 코루틴 끝의 정상적인 Hide(genN)가
    // "다른 Show()가 이미 시작됐다"고 오판해 dimCanvas를 절대 안 끄는(dim이 안 풀리는) 버그가 생긴다.
    // (동기 메서드라 Show()의 비동기 FadeDimIn 같은 "끼어들 틈"이 없어 세대 보호 자체가 애초에 불필요함.)
    public void CollapseAndResetOrigin()
    {
        EnsureDim();
        // 직전이 BeginHighlight(펄스가 안 멈추고 계속 도는 강조)였다면, 펄스 코루틴이 다음 프레임에
        // 다시 그 자리에 구멍을 그려서 CollapseHole()을 무효화해버린다 — 반드시 먼저 멈춰야 한다.
        StopAnyPulse();
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
    public IEnumerator Highlight(Button target, bool hideDimOnConfirmedClick = false, bool showHand = false)
    {
        if (target == null) { Debug.LogWarning($"[TutorialHighlighter] Highlight 대상이 null — 강조 스킵됨(클릭 대기도 영원히 안 걸림)"); yield break; }

        // 튜토리얼 중 기본 SetActive(false)로 꺼둔 버튼(예: MenuController의 menuButton)이라도 지금 이
        // 순간만은 강조 대상이므로 보이고 클릭 가능해야 한다 — activeInHierarchy 체크보다 먼저 되살린다.
        // (부모까지 비활성이면 이걸로도 activeInHierarchy는 여전히 false일 수 있음 — 그 경우는 원래대로 스킵)
        bool prevSelfActive = target.gameObject.activeSelf;
        if (!prevSelfActive) target.gameObject.SetActive(true);

        if (!target.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[TutorialHighlighter] Highlight 대상 '{target.name}'이 비활성(activeInHierarchy=false) — 강조 스킵됨(클릭 대기도 영원히 안 걸림)");
            if (!prevSelfActive) target.gameObject.SetActive(prevSelfActive);
            yield break;
        }

        yield return MoveOrAppear(target.transform as RectTransform);
        // ⚠️ BeginHighlight() 직후(Hide() 없이) 곧바로 Highlight()를 호출하는 시퀀스(예: 17-4→17-5)에서,
        // 이전 펄스를 안 멈추고 새 펄스만 시작하면 _pulse 필드가 새 펄스로 덮어써지면서 이전 펄스가
        // 고아로 남아 그 자리(예: enhancementPanel)에 하이라이트가 영원히 남는 버그가 있었다 — 반드시
        // 여기서도 BeginHighlight와 동일하게 먼저 멈춰야 한다.
        StopAnyPulse();
        var myPulse = StartPulse(PulseHole(target.transform as RectTransform, showHand));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        target.onClick.AddListener(cb);

        var catcher = EnsureHoleCatcher();
        float maxPad = highlightHolePadding + highlightPulseAmplitude;
        var catcherRect = ComputeHoleRect(target.transform as RectTransform, maxPad);
        PositionCatcher(catcher, catcherRect);
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

        target.gameObject.SetActive(prevSelfActive);
        StopPulse(myPulse);
    }

    // Highlight()와 동일하지만 "보여주는 자리"와 "실제 클릭 동작"이 다른 특수 케이스용 — 강조 구멍/펄스는
    // visualTarget 위치에 뜨지만, 클릭하면 actionButton.onClick이 실행된다(visualTarget 자신의 onClick은
    // 안 건드림). 예: NextCandidateArrow(투명 히트박스, 150x160)보다 실제 화살표 이미지인
    // NextCandidateArrowImage(54x108)가 훨씬 작아서, 히트박스 전체가 아니라 눈에 보이는 이미지 크기에
    // 딱 맞춰 강조하고 싶을 때.
    public IEnumerator HighlightWithAction(RectTransform visualTarget, Button actionButton, bool showHand = false)
    {
        if (visualTarget == null || actionButton == null)
        {
            Debug.LogWarning($"[TutorialHighlighter] HighlightWithAction 대상이 null(visual={visualTarget != null}, action={actionButton != null}) — 강조 스킵됨(클릭 대기도 영원히 안 걸림)");
            yield break;
        }
        if (!visualTarget.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[TutorialHighlighter] HighlightWithAction 시각 대상 '{visualTarget.name}'이 비활성(activeInHierarchy=false) — 강조 스킵됨(클릭 대기도 영원히 안 걸림)");
            yield break;
        }

        yield return MoveOrAppear(visualTarget);
        StopAnyPulse(); // Highlight()와 동일한 이유 — 직전 BeginHighlight 펄스가 고아로 남지 않도록.
        var myPulse = StartPulse(PulseHole(visualTarget, showHand));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        actionButton.onClick.AddListener(cb);

        var catcher = EnsureHoleCatcher();
        float maxPad = highlightHolePadding + highlightPulseAmplitude;
        PositionCatcher(catcher, ComputeHoleRect(visualTarget, maxPad));
        catcher.gameObject.SetActive(true);
        UnityEngine.Events.UnityAction catcherCb = () => actionButton.onClick.Invoke();
        catcher.onClick.AddListener(catcherCb);

        while (!clicked) yield return null;
        actionButton.onClick.RemoveListener(cb);
        catcher.onClick.RemoveListener(catcherCb);
        catcher.gameObject.SetActive(false);

        StopPulse(myPulse);
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

    // PointerDown~클릭확정 사이엔 true — 이 동안은 PulseHole()이 매 프레임 부르는 ApplyHole()이 타일을
    // 다시 켜버리지 못하게 막는다(안 막으면 HideDimTilesOnly로 숨긴 다음 프레임에 PulseHole이 그대로
    // 다시 그려서 도로 나타남 — "클릭해도 하이라이트가 안 사라지고 패널 열린 뒤에야 늦게 사라지는" 버그 원인).
    bool _dimHiddenForPress;

    void HideDimTilesOnly()
    {
        _dimHiddenForPress = true;
        foreach (var tile in _tilePool) tile.gameObject.SetActive(false);
    }

    // target을 강조한 채로 "유지"하고 즉시 반환(클릭/시간 대기 없음) — 호출부가 그동안 TutorialPanel 대사 등
    // 다른 걸 진행하고, 다 끝나면 Hide() 또는 다음 BeginHighlight로 넘어가면 된다. 이전 대상에서 슬라이드 이동.
    public IEnumerator BeginHighlight(RectTransform target, bool showHand = false)
    {
        if (target == null) { Debug.LogWarning($"[TutorialHighlighter] BeginHighlight 대상이 null — 강조 스킵됨"); yield break; }
        if (!target.gameObject.activeInHierarchy) { Debug.LogWarning($"[TutorialHighlighter] BeginHighlight 대상 '{target.name}'이 비활성(activeInHierarchy=false) — 강조 스킵됨"); yield break; }
        yield return MoveOrAppear(target);
        StopAnyPulse(); // 이전(어느 세션이든) 펄스는 정지 — 이 펄스는 다음 BeginHighlight/Highlight/Hide/CollapseAndResetOrigin이 정리할 때까지 계속 돎
        StartPulse(PulseHole(target, showHand));
    }

    // 여러 대상을 동시에 강조한 채로 clickTarget 클릭을 기다린다(슬라이드 없이 바로 나타남). 3-4처럼
    // "카드 전체 + 확정 버튼"을 한 번에 짚어줘야 할 때 사용.
    public IEnumerator HighlightMultiUntilClick(Button clickTarget, params RectTransform[] highlightTargets)
    {
        if (clickTarget == null || !clickTarget.gameObject.activeInHierarchy) yield break;

        EnsureDim();
        _holeInitialized = false; // 다중 구멍은 슬라이드 대상이 아님 — 다음 단일 강조는 새로 나타나야 함

        StopAnyPulse();
        var myPulse = StartPulse(PulseHoles(highlightTargets));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        clickTarget.onClick.AddListener(cb);
        while (!clicked) yield return null;
        clickTarget.onClick.RemoveListener(cb);

        StopPulse(myPulse);
    }

    // world-space 캐릭터(스프라이트, RectTransform 없음) 전용 BeginHighlight — 클릭 대기는 호출부가 별도로
    // 처리한다(캐릭터는 Button이 아니라 OfficeCharacter.OnPointerClick으로 직접 클릭되므로). 나머지 동작
    // (등장/슬라이드/펄스)은 BeginHighlight(RectTransform)와 동일.
    public IEnumerator BeginHighlightWorld(Transform target, bool showHand = false)
    {
        if (target == null) { Debug.LogWarning($"[TutorialHighlighter] BeginHighlightWorld 대상이 null — 강조 스킵됨"); yield break; }
        yield return MoveOrAppearWorld(target);
        StopAnyPulse();
        StartPulse(PulseHoleWorld(target, showHand));
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
            yield return AppearHole(destRect);
            _currentHoleRect = destRect;
            _holeInitialized = true;
        }
    }

    IEnumerator MoveOrAppearWorld(Transform target)
    {
        EnsureDim();
        Rect destRect = ComputeHoleRectWorld(target, highlightHolePadding);
        if (_holeInitialized)
            yield return MoveHole(_currentHoleRect, destRect);
        else
        {
            yield return AppearHole(destRect);
            _currentHoleRect = destRect;
            _holeInitialized = true;
        }
    }

    // 새 대상이 처음 나타날 때(첫 강조 또는 CollapseAndResetOrigin 직후) 즉시 팍 뚫리지 않고, destRect보다
    // appearExpandPadding만큼 사방으로 더 크게 벌어진 구멍에서 시작해 destRect 크기로 좁혀지며 나타난다 —
    // MoveHole과 동일한 ease-out lerp를 재사용(시작 rect와 소요시간만 다름).
    IEnumerator AppearHole(Rect dest)
    {
        Rect from = new Rect(
            dest.x - appearExpandPadding, dest.y - appearExpandPadding,
            dest.width + appearExpandPadding * 2f, dest.height + appearExpandPadding * 2f);
        yield return MoveHole(from, dest, appearDuration);
    }

    IEnumerator RestoreDimIfNotConfirmed(System.Func<bool> wasClicked)
    {
        yield return null;
        if (!wasClicked())
        {
            _dimHiddenForPress = false; // 미확정 → PulseHole이 다시 그리도록 허용
            ApplyHole(_currentHoleRect); // 눌렀지만 클릭 미확정(드래그로 이탈 등) → 타일 원복
        }
        // 확정된 경우엔 _dimHiddenForPress를 여기서 안 풀어준다 — Highlight()가 곧 StopPulse로 펄스 자체를
        // 정지시키므로, 그때까지 PulseHole이 계속 타일을 다시 켜지 못하게 계속 억제해야 함.
    }

    IEnumerator PulseHole(RectTransform target, bool showHand = false)
    {
        _dimHiddenForPress = false; // 새 강조 세션 시작 — 이전 press 상태 잔재 제거
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 4f;
            float pad = highlightHolePadding + highlightPulseAmplitude * Mathf.Sin(t);
            _currentHoleRect = ComputeHoleRect(target, pad);
            if (!_dimHiddenForPress) ApplyHole(_currentHoleRect);
            // 손 아이콘은 pulse로 흔들리는 구멍이 아니라 대상 원래 크기 기준 고정 위치를 따라간다(패딩 무관, 흔들림 방지).
            if (showHand) ShowHand(ComputeHoleRect(target, 0f));
            yield return null;
        }
    }

    IEnumerator PulseHoleWorld(Transform target, bool showHand = false)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 4f;
            float pad = highlightHolePadding + highlightPulseAmplitude * Mathf.Sin(t);
            _currentHoleRect = ComputeHoleRectWorld(target, pad);
            ApplyHole(_currentHoleRect);
            if (showHand) ShowHand(ComputeHoleRectWorld(target, 0f));
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
    IEnumerator MoveHole(Rect from, Rect to, float duration = -1f)
    {
        float dur = Mathf.Max(0.0001f, duration >= 0f ? duration : holeMoveDuration);
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
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return WorldCornersToLocalRect(corners, padding);
    }

    // world-space 캐릭터(스프라이트, RectTransform 없음) 전용 — SpriteRenderer의 실제 렌더 바운드를
    // 4모서리로 변환해 ComputeHoleRect와 동일한 변환 파이프라인(WorldCornersToLocalRect)을 공유한다.
    Rect ComputeHoleRectWorld(Transform target, float padding)
    {
        var rend = target.GetComponentInChildren<SpriteRenderer>();
        Bounds b = rend != null ? rend.bounds : new Bounds(target.position, new Vector3(1.5f, 2f, 0f));
        var corners = new Vector3[4]
        {
            new Vector3(b.min.x, b.min.y, 0f),
            new Vector3(b.min.x, b.max.y, 0f),
            new Vector3(b.max.x, b.max.y, 0f),
            new Vector3(b.max.x, b.min.y, 0f),
        };
        return WorldCornersToLocalRect(corners, padding);
    }

    // 월드 좌표 4모서리 → dim 루트 로컬 좌표계 사각형 변환 + padding 적용 (RectTransform/world 공용).
    Rect WorldCornersToLocalRect(Vector3[] worldCorners, float padding)
    {
        var cam = _dimCanvas.worldCamera;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
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

    // 클릭 유도 손 아이콘 — 대상 rect를 기준으로 pivot(1, 0.5) 지점(오른쪽 가장자리, 세로 중앙)에 위치시킨다.
    // 대상 자체는 절대 안 건드리며, dim 타일/holeCatcher와 같은 _dimRoot(캔버스 sortingOrder=dimSortingOrder)
    // 소속이라 항상 dim보다 위에서 보인다.
    Image EnsureHandImage()
    {
        if (_handImage != null) return _handImage;

        var go = new GameObject("TutorialHandImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_dimRoot, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = handSize;

        var img = go.GetComponent<Image>();
        img.sprite = handSprite;
        img.raycastTarget = false; // 시각 안내 전용 — 클릭을 가로채지 않는다
        img.preserveAspect = true;

        if (handAnimatorController != null)
            go.AddComponent<Animator>().runtimeAnimatorController = handAnimatorController;

        _handImage = img;
        go.SetActive(false);
        return img;
    }

    // targetRect의 pivot(1, 0.5) 지점(오른쪽 가장자리, 세로 중앙)에 손 아이콘을 배치 + 활성화.
    void ShowHand(Rect targetRect)
    {
        var img = EnsureHandImage();
        Rect p = _dimRoot.rect;
        Vector2 point = new Vector2(targetRect.xMax + handOffsetX, targetRect.y + targetRect.height * 0.5f);
        img.rectTransform.anchoredPosition = new Vector2(point.x - p.xMin, point.y - p.yMin);
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
    }

    void HideHand()
    {
        if (_handImage != null) _handImage.gameObject.SetActive(false);
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
        Debug.Log($"[TutorialHighlighter] EnsureDim() 새 dimCanvas 생성 (highlighter iid={GetInstanceID()})");

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

    // FadeDimIn의 대칭 — dimAlpha → 0으로 짧게 빠져나감. generation은 Hide()의 레이스 가드용 — 도중에
    // 새 Show()가 시작되면(세대 증가) 그쪽 FadeDimIn과 alpha를 두고 다투지 않도록 즉시 중단한다.
    IEnumerator FadeDimOut(int generation)
    {
        float dur = Mathf.Max(0.0001f, dimFadeInDuration);
        float t = 0f;
        while (t < dur)
        {
            if (_generation != generation) yield break;
            t += Time.unscaledDeltaTime;
            SetDimAlpha(Mathf.Lerp(dimAlpha, 0f, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        if (_generation != generation) yield break;
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
