using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 첫 게임씬 진입 시 1회 실행되는 온보딩 튜토리얼.
// 흐름: 비서 대사(DialogManager "tutorial_intro", 비서는 스폰 시점부터 이미 point1/master_desk에 서있음
//      — OfficeManager.SpawnSecretary 가 튜토리얼 런이면 desk_03 대신 거기서 바로 시작시킴) → TutorialPanel 대사
//      1-1(비서/나/비서 3줄) → 1-2(비서 1줄, TutorialDialog 차트) → 메뉴 버튼 강조 → (클릭→메뉴 열림)
//      → 직원 버튼 강조 → (클릭→서브 열림) → 채용하기 버튼 강조
//      → (클릭→TierPanel 열림) → tier1 버튼 강조(1단계만 선택 가능) → (클릭) → confirmBtn 강조 → (클릭) → 완료
//      → 시간이 다시 흐르는 순간(EndDimTimeStop) 비서는 GoToDesk()로 desk_03에 즉시 복귀.
//
// 강조(dim 스포트라이트) 자체는 TutorialHighlighter 공용 컴포넌트가 담당 — 이 클래스는 "어떤 순서로,
// 어떤 대상을, 얼마나" 만 기술한다. 새 튜토리얼 스텝을 추가할 때는 필드(stepGroup/위치) + Run()/PlayXxx()에
// _highlighter.Highlight(...)/HighlightForDuration(...) 한 줄만 추가하면 됨 — dim/펄스/슬라이드 구현은 공용.
//
// ⚠️ hireButton은 강조는 그대로 유지하되, 누르는 순간(PointerDown)에 dim을 끔 — TierPanel(ModalLayer, useBlur)이
// 열리며 화면을 1회 캡처해 블러 배경으로 굳히는데, 그때 dim이 켜져있으면 배경이 새까맣게 캡처되어 이후 복구 불가.
// 클릭(PointerUp) 이후에 꺼서는 이미 늦어서(OpenHiring의 캡처가 그 안에서 먼저 끝남) 더 이른 PointerDown에 건다.
// (TutorialHighlighter.Highlight의 hideDimOnConfirmedClick 파라미터로 처리)
//
// OnboardingState.TutorialDone 으로 1회만 + RunStateManager.IsTutorial(=스크립트된 튜토리얼 런) 일 때만 실행.
// 버튼 참조는 MenuController 의 것을 인스펙터로 연결.
//
// ⚠️ 3-1/3-2(HiringUI.ShowConfirmDirect — ConfirmHirePanel 첫 노출, 채용 확정 몇 주 뒤) 도 이 컴포넌트가 관리한다.
// 1-1~1-2(위 흐름)가 끝나도 자기 자신을 Destroy하지 않고 계속 살아있다가(Tutorial3Done 이 아직 false인 동안),
// HiringUI 가 Instance.PlayTutorial3()을 외부에서 호출하면 그때 재생한다 — 두 스텝의 시점이 몇 주씩 떨어져 있어
// (파견/면접 대기처럼) 같은 코루틴 흐름 안에 못 넣고 별도 트리거로 분리돼 있음.
[DisallowMultipleComponent]
public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }

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

    [Header("3-1/3-2 (ConfirmHirePanel 첫 노출 시, HiringUI가 PlayTutorial3() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 지원자 도착 안내")]
    public string step3_1 = "3-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 3-1 표시 위치")]
    public Vector2 step3_1Position;
    [Tooltip("ConfirmHirePanel/EmployeeResumePanel/ERTopPanel/BadgePanel/roleBadgePanel — 3-2 강조 대상(대사가 뜨는 동안도 강조 유지)")]
    public RectTransform roleBadgePanel;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — roleBadgePanel 강조 중 뜨는 직업 설명")]
    public string step3_2 = "3-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 3-2 표시 위치")]
    public Vector2 step3_2Position;
    [Tooltip("ConfirmHirePanel/EmployeeResumePanel/ERTopPanel/BadgePanel/potentialPanel — 3-3 강조 대상(대사가 뜨는 동안도 강조 유지)")]
    public RectTransform potentialPanel;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — potentialPanel 강조 중 뜨는 잠재력 설명")]
    public string step3_3 = "3-3";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 3-3 표시 위치")]
    public Vector2 step3_3Position;
    [Tooltip("ConfirmHirePanel/EmployeeResumePanel/ERMiddlePanel/AbilityPanel — 3-4 강조 대상(대사가 뜨는 동안도 강조 유지)")]
    public RectTransform abilityPanel;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — abilityPanel 강조 중 뜨는 능력치 설명")]
    public string step3_4 = "3-4";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 3-4 표시 위치")]
    public Vector2 step3_4Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 채용 독려 대사(confirmBtn 강조 이전)")]
    public string step3_5 = "3-5";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 3-5 표시 위치")]
    public Vector2 step3_5Position;
    [Tooltip("ConfirmHirePanel/confirmBtn — 3-5 대사 뒤 강조(클릭 대기, 실제 채용 확정 버튼)")]
    public Button confirmHireButton;

    [Header("연출 (TutorialHighlighter 로 전달됨)")]
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

    TutorialHighlighter _highlighter;

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
        // 1-1~1-2(버튼강조까지)는 아직 안 했고 + 스크립트된 튜토리얼 런일 때만.
        bool needStep1 = !OnboardingState.TutorialDone
            && RunStateManager.Instance != null && RunStateManager.Instance.IsTutorial;
        // 3-1/3-2는 IsTutorial과 무관하게(몇 주 뒤 다른 세션에서 재접속했을 수도 있음) 아직 안 했으면 대기.
        bool needStep3 = !OnboardingState.Tutorial3Done;

        if (!needStep1 && !needStep3) { Destroy(gameObject); return; }

        Instance = this;
        if (needStep1) StartCoroutine(Run());
        // needStep3만 남았으면 여기서 아무것도 안 하고 대기 — HiringUI.ShowConfirmDirect가
        // Instance.PlayTutorial3()을 채용 확정 화면이 열릴 때 직접 호출한다.
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

        // dim은 TutorialPanel이 뜨는 시점(1-1)부터 항상 켜져 있어야 함(PlayTutorial3와 동일 원칙) —
        // 대사만 있고 하이라이트 대상이 없는 구간이라도 CollapseAndResetOrigin()으로 구멍만 접어 유지.
        EnsureHighlighter();
        yield return _highlighter.Show(); // 0 → dimAlpha 빠르게 훅 들어와 집중 유도

        if (TutorialPanelUI.Instance != null)
        {
            yield return TutorialPanelUI.Instance.PlayStepGroup(step1_1, step1_1Position);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step1_2, step1_2Position);
        }

        // ── 2~4) 버튼 순차 강조 ──
        yield return _highlighter.Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 상위 메뉴 펼침
        yield return _highlighter.Highlight(employeeButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 서브 메뉴 펼침

        // 1-3: 채용하기 버튼 강조 (TierPanel 진입)
        yield return _highlighter.Highlight(hireButton, hideDimOnConfirmedClick: true);
        yield return new WaitForSecondsRealtime(settleDelay); // 채용 패널(TierPanel) 펼침 — 블러는 이미 밝은 배경으로 캡처됨

        // TierPanel이 열리기 전(메뉴 쪽)과 후(패널 안)는 화면상 완전히 다른 위치라 이어서 슬라이드하면 안
        // 어울림 — 여기서 새 구간으로 리셋해서 tier1Button은 hireButton 자리에서 이동해오지 않고 그냥
        // 나타나게 하고, hireConfirmButton만 tier1Button에서 이어서 슬라이드하게(같은 패널 안이라 자연스러움).
        _highlighter.CollapseAndResetOrigin();
        // 2-1: TierPanel/tier1 강조 (대사 없음, 하이라이트만)
        yield return _highlighter.Highlight(tier1Button);
        yield return new WaitForSecondsRealtime(settleDelay);
        // 2-2: TierPanel/confirmBtn 강조 (대사 없음, 하이라이트만)
        yield return _highlighter.Highlight(hireConfirmButton);

        yield return _highlighter.Hide();
        EndDimTimeStop();
        OnboardingState.MarkTutorialDone();
        // ⚠️ 여기서 Destroy 안 함 — Tutorial3Done 이 아직이면 이 컴포넌트가 계속 살아서 HiringUI의
        // PlayTutorial3() 외부 호출을 기다려야 한다(채용 확정 몇 주 뒤 ConfirmHirePanel 노출 시점).
        if (OnboardingState.Tutorial3Done) Destroy(gameObject);
    }

    // ── 3-1/3-2/3-3/3-4/3-5 (HiringUI 가 ConfirmHirePanel 첫 노출 시 호출) ──────────
    // dim은 TutorialPanel이 떠 있는 내내(하이라이트 대상이 없는 대사 구간 포함) 유지된다 — Show()/Hide()를
    // 스텝마다 껐다 켜지 않고 전체를 한 번씩만 감싼다. 하이라이트 없는 구간은 CollapseAndResetOrigin()으로
    // 구멍만 접어(dim은 계속 켜진 채) 표현한다.
    public IEnumerator PlayTutorial3()
    {
        EnsureHighlighter();
        yield return _highlighter.Show();

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step3_1, step3_1Position);

        // 3-2: roleBadgePanel 강조를 켠 채로 유지하면서 그 위에 대사 표시 — 대사 끝날 때까지 안 사라짐.
        yield return _highlighter.BeginHighlight(roleBadgePanel);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step3_2, step3_2Position);

        // 3-3: roleBadgePanel → potentialPanel로 강조가 슬라이드 이동, 역시 강조 유지한 채로 대사 표시.
        yield return _highlighter.BeginHighlight(potentialPanel);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step3_3, step3_3Position);

        // 3-4: potentialPanel → abilityPanel로 강조가 슬라이드 이동, 역시 강조 유지한 채로 대사 표시.
        yield return _highlighter.BeginHighlight(abilityPanel);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step3_4, step3_4Position);

        // 3-5 대사: 하이라이트 대상 없음 — dim은 유지한 채 구멍만 접는다(전체 화면 톤다운, 사라지지 않음).
        _highlighter.CollapseAndResetOrigin();
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step3_5, step3_5Position);

        // 3-5 강조: confirmBtn 강조, 클릭 대기(실제 채용 확정 버튼). 대사 없음.
        yield return _highlighter.Highlight(confirmHireButton);

        // 3-6: confirmBtn 클릭 시 HiringUI.OnClickConfirmHire()가 동기적으로 띄우는 ConfirmUI
        // ("OO을(를) 채용하시겠습니까?")의 확인("네") 버튼 강조, 클릭 대기. 대사 없음.
        // ConfirmHirePanel과는 완전히 다른 화면(모달 다이얼로그)이라 슬라이드 없이 새로 나타나야 함.
        _highlighter.CollapseAndResetOrigin();
        if (ConfirmUI.Instance != null)
            yield return _highlighter.Highlight(ConfirmUI.Instance.confirmButton);

        yield return _highlighter.Hide();

        OnboardingState.MarkTutorial3Done();
        Destroy(gameObject); // 1-1~3-6 전부 끝 — 더 이상 대기할 스텝 없음
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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        EndDimTimeStop(); // 중간에 파괴돼도 시간 정지 누수 방지
    }
}
