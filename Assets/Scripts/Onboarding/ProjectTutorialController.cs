using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 온보딩 2차 튜토리얼 — 첫 채용으로 직원을 얻고 1주 뒤 1회 실행.
// 흐름: 메뉴 버튼 강조 → (클릭) → ProjectSetupMenuBtn 강조 → (클릭) → projectStartBtn 강조 → 완료.
//
// 트리거: HiringUI.DoHire 에서 OnboardingState.ArmProjectTutorial(1) 로 1주 카운트다운 무장.
//   GameTimeManager.OnTimeChanged(주차 틱)마다 pending 감소, 0 이 되면 실행. (세션 끊겨도 PlayerPrefs 로 복구)
// 강조(dim 스포트라이트) 자체는 TutorialHighlighter 공용 컴포넌트가 담당 — TutorialController와 동일.
[DisallowMultipleComponent]
public class ProjectTutorialController : MonoBehaviour
{
    public static ProjectTutorialController Instance { get; private set; }

    [Header("강조할 버튼 (순서대로)")]
    public Button menuButton;          // 1) 메뉴 열기
    public Button projectSetupButton;  // 2) ProjectSetupMenuBtn (TopMenuContainer)
    public Button projectStartButton;  // 3) projectStartBtn

    [Header("연출 (TutorialHighlighter 로 전달됨)")]
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

    TutorialHighlighter _highlighter;
    bool _running;

    void EnsureHighlighter()
    {
        if (_highlighter != null) return;
        _highlighter = gameObject.AddComponent<TutorialHighlighter>();
        _highlighter.dimAlpha = dimAlpha;
        _highlighter.dimFadeInDuration = dimFadeInDuration;
        _highlighter.highlightHolePadding = highlightHolePadding;
        _highlighter.highlightPulseAmplitude = highlightPulseAmplitude;
        _highlighter.holeMoveDuration = holeMoveDuration;
    }

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

        EnsureHighlighter();
        BeginDimTimeStop(); // 패널 켜는 것과 동일하게 dim 동안 시간 정지
        yield return _highlighter.Show();

        yield return _highlighter.Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return _highlighter.Highlight(projectSetupButton);
        yield return new WaitForSecondsRealtime(settleDelay);
        yield return _highlighter.Highlight(projectStartButton);

        yield return _highlighter.Hide();
        EndDimTimeStop();
        OnboardingState.MarkProjectTutorialDone();
        Destroy(gameObject);
    }
}
