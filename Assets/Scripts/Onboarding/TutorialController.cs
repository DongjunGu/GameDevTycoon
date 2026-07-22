using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 첫 게임씬 진입 시 1회 실행되는 온보딩 튜토리얼.
// 흐름: 비서 대사 → 메뉴 버튼 강조 → (클릭→메뉴 열림) → 직원 버튼 강조 → (클릭→서브 열림) → 채용하기 버튼 강조
//      → (클릭→TierPanel 열림) → tier1 버튼 강조(1단계만 선택 가능) → (클릭) → confirmBtn 강조 → (클릭) → 완료.
//
// 강조 방식: 반투명 dim 풀스크린(클릭 차단) 위로 "대상 버튼만" 정렬을 올려(overrideSorting) 밝게+클릭 가능하게 하고 펄스.
// → 다른 모든 UI 는 dim 에 막혀 대상만 누를 수 있음. 대사 내용은 DialogManager 그룹(데이터)에서.
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

    [Header("연출")]
    [Range(0f, 1f)] public float dimAlpha = 0.5f;
    [Tooltip("메뉴/서브 슬라이드 펼침 대기(초)")]
    public float settleDelay = 0.4f;
    [Tooltip("게임씬 진입 후 DialogManager 준비를 기다리는 최대 시간(초). 준비되면 그 즉시 대사 표시.")]
    public float startupTimeout = 5f;

    Canvas _dimCanvas;
    Coroutine _pulse;

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

        // ── 2~4) 버튼 순차 강조 ──
        EnsureDim();
        _dimCanvas.enabled = true;
        BeginDimTimeStop(); // 패널 켜는 것과 동일하게 dim 동안 시간 정지

        yield return Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 상위 메뉴 펼침
        yield return Highlight(employeeButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 서브 메뉴 펼침
        yield return Highlight(hireButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 채용 패널(TierPanel) 펼침
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
    }

    void OnDestroy() => EndDimTimeStop(); // 중간에 파괴돼도 시간 정지 누수 방지

    // 대상 버튼을 dim 위로 올려 강조 + 펄스 + 클릭 대기. 클릭되면 정리하고 반환.
    IEnumerator Highlight(Button target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

        // dim(200) 위로 올리려면 자체 Canvas(overrideSorting)가 필요한데, 자식 Canvas 가 생기면
        // 그 버튼 그래픽이 부모 GraphicRaycaster 시야에서 빠지므로 자체 GraphicRaycaster 도 필요.
        // ⚠️ 둘 다 "내가 추가한 경우에만" cleanup 에서 Destroy 로 원상복구해야 함.
        //    (disable/false 로 남기면 nested Canvas 만 남아 버튼이 영영 클릭 불가 — 메뉴버튼 먹통 버그)
        var canvas = target.GetComponent<Canvas>();
        bool addedCanvas = canvas == null;
        if (addedCanvas) canvas = target.gameObject.AddComponent<Canvas>();
        bool prevOverride = canvas.overrideSorting;
        int  prevOrder    = canvas.sortingOrder;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 201;

        var gr = target.GetComponent<GraphicRaycaster>();
        bool addedRaycaster = gr == null;
        if (addedRaycaster) gr = target.gameObject.AddComponent<GraphicRaycaster>();

        _pulse = StartCoroutine(Pulse(target.transform));

        bool clicked = false;
        UnityEngine.Events.UnityAction cb = () => clicked = true;
        target.onClick.AddListener(cb);
        while (!clicked) yield return null;
        target.onClick.RemoveListener(cb);

        // 정리 — 추가한 컴포넌트는 제거(원상복구), 원래 있던 Canvas 는 값만 복원.
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        target.transform.localScale = Vector3.one;
        if (addedRaycaster && gr != null) Destroy(gr);   // Canvas 보다 먼저 (RequireComponent 의존)
        if (addedCanvas)
        {
            if (canvas != null) Destroy(canvas);
        }
        else
        {
            canvas.overrideSorting = prevOverride;
            canvas.sortingOrder    = prevOrder;
        }
    }

    IEnumerator Pulse(Transform tr)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 4f;
            tr.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t));
            yield return null;
        }
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

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, dimAlpha);
        img.raycastTarget = true;                        // dim 영역 클릭 차단 (대상 버튼만 위로 빠져나감)
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _dimCanvas.enabled = false;
    }
}
