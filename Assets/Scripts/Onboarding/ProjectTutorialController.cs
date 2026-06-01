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
    [Header("강조할 버튼 (순서대로)")]
    public Button menuButton;          // 1) 메뉴 열기
    public Button projectSetupButton;  // 2) ProjectSetupMenuBtn (TopMenuContainer)
    public Button projectStartButton;  // 3) projectStartBtn

    [Header("연출")]
    [Range(0f, 1f)] public float dimAlpha = 0.5f;
    [Tooltip("메뉴/패널 펼침 대기(초)")]
    public float settleDelay = 0.4f;

    Canvas _dimCanvas;
    Coroutine _pulse;
    bool _running;

    void Start()
    {
        if (OnboardingState.ProjectTutorialDone) { Destroy(gameObject); return; }
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.OnTimeChanged += OnWeek;
        TryFire(); // pending==0(직전 만료, 리로드 복구)이면 즉시 실행
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.OnTimeChanged -= OnWeek;
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

    void TryFire()
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

        yield return Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return Highlight(projectSetupButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return Highlight(projectStartButton);

        _dimCanvas.enabled = false;
        OnboardingState.MarkProjectTutorialDone();
        Destroy(gameObject);
    }

    // 대상 버튼을 dim 위로 올려 강조 + 펄스 + 클릭 대기. (TutorialController 와 동일 패턴)
    IEnumerator Highlight(Button target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

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

        // 정리 — 추가한 컴포넌트는 Destroy 로 원상복구(disable 로 남기면 버튼 영구 먹통 버그), 원래 있던 Canvas 는 값만 복원.
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

        var go = new GameObject("ProjectTutorialDim", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        _dimCanvas = go.AddComponent<Canvas>();
        _dimCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _dimCanvas.worldCamera = Camera.main;
        _dimCanvas.planeDistance = 100f;
        _dimCanvas.overrideSorting = true;
        _dimCanvas.sortingOrder = 200;
        go.AddComponent<GraphicRaycaster>();

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, dimAlpha);
        img.raycastTarget = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _dimCanvas.enabled = false;
    }
}
