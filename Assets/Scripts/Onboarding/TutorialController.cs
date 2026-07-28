using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 첫 게임씬 진입 시 1회 실행되는 온보딩 튜토리얼.
// 흐름: 비서 대사(DialogManager "tutorial_intro", 비서는 스폰 시점부터 이미 point1/master_desk에 서있음
//      — OfficeManager.SpawnSecretary 가 튜토리얼 런이면 desk_03 대신 거기서 바로 시작시킴) → TutorialPanel 대사
//      1-1(비서/나/비서 3줄) → 1-2(비서 1줄, TutorialDialog 차트) → 메뉴 버튼 강조 → (클릭→메뉴 열림)
//      → 직원 버튼 강조 → (클릭→서브 열림) → 채용하기 버튼 강조
//      → (클릭→TierPanel 열림) → tier1 버튼 강조(1단계만 선택 가능) → (클릭) → confirmBtn 강조 → (클릭)
//      → 몇 주 뒤 ConfirmHirePanel(3-1~3-6) → 채용 확정 시 튜토리얼 한정 보너스 라운드(HiringUI)로 패널
//      안 닫고 자동 다음 후보 전환 → 4-1/4-2 대사 → 두 번째(진짜) 채용 확정 2초 후 → 5-1 대사 → 5-2 대사 +
//      메뉴→ProjectSetupMenuBtn→projectStartBtn 순차 강조 → (클릭→SummaryPanel 열림) → 5-3 대사(강조 없음,
//      플랫폼/장르는 유저가 직접 SummaryPanel/GenrePanel/PlatformPanel에서 선택) → 둘 다 선택되면(ProjectSetupUI가
//      TutorialController.NotifyProjectSetupSelection 호출) → 5-4 대사 + SummaryPanel/ConfirmBtn 강조(클릭
//      →개발 시작) → 자동으로 열리는 기획팀장 선택(DispatchPanelUI)에서 6-1 대사 + 두 번째 슬롯(비 CEO
//      직원) 강조(클릭) → 6-2 대사(강조 유지, planningPanel) → 6-3 dispatchConfirmBtn 강조(클릭) →
//      자동으로 뜨는 팀장점수 화면(LeaderScoreUI)에서 1회차만 재생 후 7-1 대사 → 7-2 대사(강조 유지,
//      dsSlider) → 2~3회차 이어서 재생 → 4회차 대기 상태가 되면 7-3 대사(2줄, 강조 없음) → 7-4 대사
//      (강조 유지, aimButtonsRoot, 이 구간 내내 조준 버튼 클릭 잠금) → 7-5 대사(2줄, 강조 유지,
//      aimHighButton) → 마지막 줄 이후에만 클릭 허용 + 강제 오버플로 무장 → 클릭 시 4회차(burst)
//      연출 → 다 끝나면 7-6 대사(2줄, 강조 없음) → 완료.
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

    [Header("4-1/4-2 (튜토리얼 첫 채용 보너스 라운드 — 두 번째 후보로 자동 전환된 직후, HiringUI가 PlayTutorial4_1() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 직원이 늘어난 소감 대사(강조 없음)")]
    public string step4_1 = "4-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 4-1 표시 위치")]
    public Vector2 step4_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 이번엔 직접 골라보라는 안내(강조 없음, 2줄)")]
    public string step4_2 = "4-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 4-2 표시 위치")]
    public Vector2 step4_2Position;
    [Tooltip("ConfirmHirePanel/NextCandidateArrow/NextCandidateArrowImage — 4-2 강조가 '보여지는' 자리(눈에 보이는 화살표 이미지, 54x108). NextCandidateArrow 자신은 투명 히트박스(150x160)라 그대로 강조하면 이미지보다 훨씬 크게 뚫림")]
    public RectTransform nextCandidateArrowImageRect;
    [Tooltip("ConfirmHirePanel/NextCandidateArrow(HiringUI.nextCandidateButton) — 4-2 강조 클릭 시 '실제로 실행되는' 동작(후보 넘기기 히트박스)")]
    public Button nextCandidateButton;

    [Header("5-1 (두 번째(진짜) 채용 확정 2초 후 — HiringUI.DoHire가 PlayTutorial5_1() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 사무실이 좁다는 소감 대사(강조 없음, 2줄)")]
    public string step5_1 = "5-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 5-1 표시 위치")]
    public Vector2 step5_1Position;

    [Header("5-2 (5-1 대사 직후 이어서 — 프로젝트 시작 유도, PlayTutorial5_1() 안에서 계속 재생)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 게임 개발 시작 유도 대사(강조 없음)")]
    public string step5_2 = "5-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 5-2 표시 위치")]
    public Vector2 step5_2Position;
    [Tooltip("ProjectSetupMenuBtn(TopMenuContainer) — 5-2 대사 뒤 강조(menuButton 클릭으로 메뉴 펼친 다음)")]
    public Button projectSetupButton;
    [Tooltip("projectStartBtn — projectSetupButton 강조 다음, 클릭 대기(실제 프로젝트 시작 버튼)")]
    public Button projectStartButton;

    [Header("5-3 (projectStartBtn 클릭 직후 — SummaryPanel(ProjectSetupUI.mainPanel)이 막 열린 상태, 대사만)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 소형+플랫폼/장르 직접 골라보라는 안내(강조 없음, 2줄)")]
    public string step5_3 = "5-3";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 5-3 표시 위치")]
    public Vector2 step5_3Position;

    [Header("5-4 (플랫폼+장르 둘 다 선택된 직후 — ProjectSetupUI가 NotifyProjectSetupSelection() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 선택 칭찬 + 시작 유도 대사(강조 없음)")]
    public string step5_4 = "5-4";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 5-4 표시 위치")]
    public Vector2 step5_4Position;
    [Tooltip("SummaryPanel/ConfirmBtn(ProjectSetupUI.startButton) — 5-4 대사 뒤 강조, 클릭 대기(실제 개발 시작 버튼)")]
    public Button summaryConfirmButton;

    [Header("6-1~6-3 (기획팀장 선택 — DispatchPanelUI.OpenLeaderInternal이 Planner 타입 첫 오픈 시 PlayTutorial6() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 직원 실력 확인 유도 대사(강조 없음)")]
    public string step6_1 = "6-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 6-1 표시 위치")]
    public Vector2 step6_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 팀장/능력치 설명 대사(강조 유지, planningPanel 위에 표시)")]
    public string step6_2 = "6-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 6-2 표시 위치")]
    public Vector2 step6_2Position;
    [Tooltip("DispatchRightPanel/ChildPanel/AbilityPanel/planningPanel — 6-2 대사 뒤 강조 유지(대사가 뜨는 동안도)")]
    public RectTransform planningPanel;
    [Tooltip("DispatchPanel/.../dispatchConfirmBtn(DispatchPanelUI.confirmButton) — 6-3 강조, 클릭 대기(대사 없음)")]
    public Button dispatchConfirmButton;

    [Header("7-1~7-6 (첫 기획팀장 점수 화면 — DevelopmentManager.BuildAndShowLeaderScore가 PlayTutorial7_1()/PlayTutorial7_3ToEnd() 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 1회차 끝난 직후 소감 대사(강조 없음)")]
    public string step7_1 = "7-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-1 표시 위치")]
    public Vector2 step7_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 스트레스 설명 대사(강조 유지, dsSlider 위에 표시)")]
    public string step7_2 = "7-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-2 표시 위치")]
    public Vector2 step7_2Position;
    [Tooltip("LeaderScoreUI.dsSlider — 7-2 대사 뒤 강조 유지(대사가 뜨는 동안도)")]
    public RectTransform dsSliderRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 3회차 끝난 직후(4회차 대기) 스트레스↔점수 상관 설명(강조 없음, 2줄)")]
    public string step7_3 = "7-3";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-3 표시 위치")]
    public Vector2 step7_3Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 4회차 조준 선택 안내(강조 유지, aimButtonsRoot 위에 표시)")]
    public string step7_4 = "7-4";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-4 표시 위치")]
    public Vector2 step7_4Position;
    [Tooltip("LeaderScoreAimButtons.aimButtonsRoot — 7-4/7-5-1 강조(이 구간 동안 자식 버튼 클릭은 코드로 막아둠)")]
    public RectTransform aimButtonsRootRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 강도별 스트레스 범위 참고 + \"강\" 유도(강조 유지, aimHighButton 위에 표시, 2줄)")]
    public string step7_5 = "7-5";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-5 표시 위치")]
    public Vector2 step7_5Position;
    [Tooltip("LeaderScoreAimButtons.lowButton(아무 것도 안 함, 7-4/7-5 동안 interactable=false로 잠금)")]
    public Button aimLowButton;
    [Tooltip("LeaderScoreAimButtons.midButton(위와 동일하게 잠금)")]
    public Button aimMidButton;
    [Tooltip("LeaderScoreAimButtons.highButton(\"강\") — 7-4/7-5-1 동안은 잠금, 7-5-2 대사 뒤에만 강조+클릭 허용")]
    public Button aimHighButton;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 스트레스 100 초과(burst)로 점수 깎인 직후 설명(강조 없음, 2줄)")]
    public string step7_6 = "7-6";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 7-6 표시 위치")]
    public Vector2 step7_6Position;

    [Header("8-1 (팀장점수 패널이 실제로 닫히고 개발 시작된 직후 — LeaderScoreUI.OnConfirmClosed 호출)")]
    [Tooltip("HUDCanvas/.../DevelopmentPanel/SupriseQuestUI — 강조 유지, 클릭 대기 없음(대사 3줄 끝나면 자동 종료)")]
    public RectTransform surpriseQuestUIRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 도전 과제(SupriseQuestUI) 소개 대사(강조 유지, 3줄)")]
    public string step8_1 = "8-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 8-1 표시 위치")]
    public Vector2 step8_1Position;

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
    [Tooltip("새 대상이 처음 나타날 때 구멍이 대상보다 이 값(px)만큼 더 크게 벌어진 채로 시작해서 사방에서 좁혀지듯 줄어든다. 너무 크면 직전 강조 위치까지 덮어버려 슬라이드처럼 보이니 주의")]
    public float appearExpandPadding = 70f;
    [Tooltip("위 '사방에서 좁혀지는' 등장 연출의 소요 시간(초)")]
    public float appearDuration = 0.28f;
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
        _highlighter.appearExpandPadding = appearExpandPadding;
        _highlighter.appearDuration = appearDuration;
    }

    void Start()
    {
        // 1-1~1-2(버튼강조까지)는 아직 안 했고 + 스크립트된 튜토리얼 런일 때만.
        bool needStep1 = !OnboardingState.TutorialDone
            && RunStateManager.Instance != null && RunStateManager.Instance.IsTutorial;
        // 3-1/3-2는 IsTutorial과 무관하게(몇 주 뒤 다른 세션에서 재접속했을 수도 있음) 아직 안 했으면 대기.
        bool needStep3 = !OnboardingState.Tutorial3Done;
        // 5-1~6-2(팀장 확정 전)는 서버에 아무것도 저장되지 않는 구간(플랫폼/장르 선택, 개발 시작, 팀장
        // 선택 전부 팀장점수 burst 시점에야 값 잠금 저장됨) — 즉 이 구간 어디서 중단/재접속하든 서버
        // 상태는 4-2 직후로 되돌아가 있으므로, Tutorial6Done(팀장 확정)이 아직이면 무조건 처음(5-1)부터
        // 다시 재생한다(3-1~4-2와 동일한 all-or-nothing 방식). Tutorial5Pending은 이제 Tutorial6Done에서
        // 해제된다(OnboardingState.MarkTutorial6Done 참고).
        bool needStep5 = OnboardingState.Tutorial5Pending && !OnboardingState.Tutorial6Done;
        // needStep5와 조건이 사실상 같아졌지만(Tutorial5Pending은 4-2 직후 무장돼 6Done까지 유지됨),
        // Tutorial5Pending이 아직 무장 안 된 예외적 상황을 대비한 방어적 폴백으로 남겨둠 — 이 경우엔
        // Start()가 능동적으로 아무것도 안 하고, DispatchPanelUI가 패널을 열 때 Instance.PlayTutorial6()을
        // 직접 호출하는 것만 기다린다.
        bool needStep6 = !OnboardingState.Tutorial6Done;
        // 7-1~7-6도 6단계와 동일 이유로 pending 불필요 — 첫 기획팀장 점수 화면이 열릴 때마다(재접속
        // 자연 재개 포함) DevelopmentManager.BuildAndShowLeaderScore가 완료 여부를 직접 체크해서 부른다.
        bool needStep7 = !OnboardingState.Tutorial7Done;
        // 8-1은 6/7단계와 달리 재트리거해줄 외부 진입점이 없다(LeaderScoreUI.OnConfirmClosed는 같은 세션
        // 안에서만 발동) — 대신 CurrentStage==Developing 자체가 "7단계까지 끝나고 개발이 진짜 시작됨"을
        // 뜻하는, 이미 서버에 커밋된 상태이므로 재접속 시 이 조건으로 직접 재개한다.
        bool needStep8 = OnboardingState.Tutorial7Done && !OnboardingState.Tutorial8Done
            && DevelopmentManager.Instance != null && DevelopmentManager.Instance.CurrentStage == ProjectStage.Developing;

        if (!needStep1 && !needStep3 && !needStep5 && !needStep6 && !needStep7 && !needStep8) { Destroy(gameObject); return; }

        Instance = this;
        if (needStep1) StartCoroutine(Run());
        else if (needStep5) StartCoroutine(PlayTutorial5_1());
        else if (needStep8) StartCoroutine(PlayTutorial8_1());
        // needStep3/needStep6/needStep7만 남았으면 여기서 아무것도 안 하고 대기 — HiringUI.ShowConfirmDirect가
        // Instance.PlayTutorial3()을, DispatchPanelUI가 Instance.PlayTutorial6()을, DevelopmentManager가
        // Instance.PlayTutorial7_1()을 각각 해당 시점에 직접 호출한다.
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
        int gen1 = _highlighter.CurrentGeneration; // 아래 Hide()에 그대로 넘겨 레이스 방지(TutorialHighlighter.Hide 주석 참고)

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

        yield return _highlighter.Hide(gen1);
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
        int gen3 = _highlighter.CurrentGeneration;

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

        yield return _highlighter.Hide(gen3);

        // ⚠️ 여기서 Destroy도 MarkTutorial3Done()도 안 함 — 3-6에서 채용한 후보는 튜토리얼 한정 보너스
        // 라운드(HiringUI.DoHire의 _tutorialBonusHirePending 분기)로 이어져 패널이 안 닫히고 한 명 더
        // 채용시킨다. Tutorial3Done은 그 두 번째 채용이 실제로 확정될 때(HiringUI.DoHire) 마크된다 —
        // 여기서 미리 마크해버리면 두 번째 채용 전에 재접속했을 때 서버엔 아직 원본 3명 그대로인데
        // Tutorial3Done=true라 튜토리얼이 다시 안 뜨고 빈 후보 화면만 보이는 불일치가 생긴다. 그 직후(두
        // 번째 후보로 자동 전환된 다음) HiringUI가 PlayTutorial4_1()을 외부에서 호출하므로 이 컴포넌트가
        // 계속 살아있어야 함.
    }

    // ── 4-1 (HiringUI.TutorialAdvanceAfterFirstHire — 첫 채용 보너스 라운드에서 두 번째 후보로
    // 자동 전환하는 애니메이션이 끝난 직후 호출) ────────────────────────────────
    public IEnumerator PlayTutorial4_1()
    {
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen4 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step4_1, step4_1Position);

        // 4-2: 기획자(첫 채용) 다음은 개발자를 뽑아보자는 안내 + 이번엔 직접 골라보라는 안내(대사 없이 이어짐).
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step4_2, step4_2Position);

        // 4-2 강조: 눈에 보이는 화살표 이미지(NextCandidateArrowImage) 자리를 강조하되, 실제 클릭 동작은
        // 히트박스(NextCandidateArrow)로 — 대표님이 직접 후보를 넘겨보게. 대사 없음.
        yield return _highlighter.HighlightWithAction(nextCandidateArrowImageRect, nextCandidateButton);

        yield return _highlighter.Hide(gen4);
        // ⚠️ 여기서 Destroy 안 함 — 두 번째(진짜) 채용이 아직 확정 전이다. 확정 2초 후 HiringUI.DoHire가
        // Instance.PlayTutorial5_1()을 외부에서 호출하므로 이 컴포넌트가 계속 살아있어야 함.
    }

    // ── 5-1/5-2 (HiringUI.DoHire — 튜토리얼 두 번째(진짜) 채용이 확정되고 2초 뒤 호출) ──────────
    public IEnumerator PlayTutorial5_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // ConfirmHirePanel이 이미 닫힌 뒤라 이번엔 직접 시간 정지
        yield return _highlighter.Show();
        int gen5 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step5_1, step5_1Position);

        // 5-2: 이어서 프로젝트 시작 유도 대사 + 메뉴 → ProjectSetupMenuBtn → projectStartBtn 순차 강조.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step5_2, step5_2Position);

        yield return _highlighter.Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 메뉴 펼침
        yield return _highlighter.Highlight(projectSetupButton);
        yield return new WaitForSecondsRealtime(settleDelay); // ProjectSetupPanel 펼침
        yield return _highlighter.Highlight(projectStartButton);
        // projectStartBtn 클릭으로 ProjectSetupUI.OnClickProjectStart()가 이미 동기 실행돼 SummaryPanel이
        // 열렸다 — 그 안부터는 ProjectSetupUI 자신이 StopTime/StartTime을 쥐고 있으므로(모달 자체 시간정지)
        // 우리 쪽 시간정지는 여기서 끝내도 된다(계속 멈춰있음).
        EndDimTimeStop();

        // 5-3: SummaryPanel이 막 열린 상태 — 이전(메뉴 안) 강조 위치와는 완전히 다른 화면이라 슬라이드 없이
        // 새로 나타나게 리셋 후, 강조 없이 대사만(플랫폼/장르 직접 골라보라는 안내).
        _highlighter.CollapseAndResetOrigin();
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step5_3, step5_3Position);

        // dim 해제 — 플랫폼/장르는 유저가 SummaryPanel/GenrePanel/PlatformPanel에서 직접 골라야 하니 자유 조작.
        yield return _highlighter.Hide(gen5);
        _waitingForProjectSetupChoice = true;
        // ⚠️ 여기서 Destroy 안 함 — 플랫폼+장르가 둘 다 선택되면 ProjectSetupUI가
        // NotifyProjectSetupSelection()을 외부에서 호출하고, 그때 PlayTutorial5_4()가 이어서 실행된다.
    }

    bool _waitingForProjectSetupChoice;

    // ProjectSetupUI.OnClickPlatform/OnClickGenre 에서 선택이 바뀔 때마다 호출됨 — 5-3 대기 중이고
    // 플랫폼+장르가 둘 다 선택된 순간에만 5-4로 이어간다(그 외엔 아무것도 안 함, 튜토리얼 밖 일반 플레이 포함).
    public void NotifyProjectSetupSelection(bool platformChosen, bool genreChosen)
    {
        if (!_waitingForProjectSetupChoice) return;
        if (!platformChosen || !genreChosen) return;
        _waitingForProjectSetupChoice = false;
        StartCoroutine(PlayTutorial5_4());
    }

    // ── 5-4 (플랫폼+장르 둘 다 선택된 직후 — NotifyProjectSetupSelection이 호출) ──────────────
    IEnumerator PlayTutorial5_4()
    {
        EnsureHighlighter();
        yield return _highlighter.Show(); // ProjectSetupUI가 이미 시간 정지 중이라 여긴 별도 시간정지 불필요
        int gen54 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step5_4, step5_4Position);

        // SummaryPanel/ConfirmBtn 강조, 클릭 대기(실제 개발 시작).
        yield return _highlighter.Highlight(summaryConfirmButton);

        // ⚠️ 이 클릭이 실제로 개발을 시작시켜 DispatchPanelUI가 곧장(같은 프레임에 가깝게) PlayTutorial6()의
        // Show()를 호출할 수 있다 — 세대를 명시적으로 넘겨야 늦게 끝나는 이 Hide()가 6-1의 Show()를
        // 덮어쓰지 않는다(TutorialHighlighter.Hide 주석 참고, 실제로 관측된 레이스).
        yield return _highlighter.Hide(gen54);
        OnboardingState.MarkTutorial5Done();
        // ⚠️ 여기서 Destroy 안 함 — 개발 시작 직후 자동으로 열리는 기획팀장 선택(DispatchPanelUI)에서
        // 이어지는 6-1~6-3이 남아있다. DispatchPanelUI.OpenLeaderInternal이 Instance.PlayTutorial6()을
        // 외부에서 호출하므로 이 컴포넌트가 계속 살아있어야 함.
    }

    // ── 6-1~6-3 (DispatchPanelUI.OpenLeaderInternal — 기획팀장 선택 패널이 Planner 타입으로 열릴 때 호출) ──
    public IEnumerator PlayTutorial6(Transform slotParent)
    {
        EnsureHighlighter();
        // DispatchPanelUI가 패널을 열면서 이미 GameTimeManager.StopTime()을 호출했으므로(모달 자체 시간정지)
        // 여기서 별도 시간정지는 불필요.
        yield return _highlighter.Show();
        int gen6 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step6_1, step6_1Position);

        // 6-1 강조: 두 번째 슬롯(CEO가 아닌 직원) 강조, 클릭 대기.
        // 대사가 뜨는 동안 목록이 재생성됐을 가능성을 배제하기 위해 지금 이 시점에 "무조건 두 번째 자식"을
        // 다시 찾는다(캡처해둔 Button 참조를 쓰지 않음) — DispatchSlotPrefab(Clone)의 selectButton은
        // 슬롯 루트의 자식이라 GetComponentInChildren으로 찾아야 함.
        Button secondSlotButton = null;
        if (slotParent != null && slotParent.childCount >= 2)
            secondSlotButton = slotParent.GetChild(1).GetComponentInChildren<Button>(true);
        else
            Debug.LogWarning($"[TutorialController] PlayTutorial6: slotParent={(slotParent != null ? slotParent.name : "null")}, childCount={(slotParent != null ? slotParent.childCount : -1)} — 두 번째 자식을 못 찾음");
        Debug.Log(secondSlotButton != null
            ? $"[TutorialController] PlayTutorial6: 6-1 강조 대상 resolve됨 — name={secondSlotButton.name}, path={GetPath(secondSlotButton.transform)}, activeInHierarchy={secondSlotButton.gameObject.activeInHierarchy}, interactable={secondSlotButton.interactable}"
            : "[TutorialController] PlayTutorial6: secondSlotButton이 null로 resolve됨");
        yield return _highlighter.Highlight(secondSlotButton);

        // 6-2: planningPanel 강조를 유지한 채로 대사 표시(같은 패널 안이라 슬롯 자리에서 자연스럽게 슬라이드).
        yield return _highlighter.BeginHighlight(planningPanel);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step6_2, step6_2Position);

        // 6-3: dispatchConfirmBtn 강조, 클릭 대기(대사 없음). BeginHighlight로 계속 돌던 pulse를 먼저
        // 접어야(CollapseAndResetOrigin) 새 Highlight의 pulse와 겹쳐 구멍이 두 자리에서 흔들리지 않는다
        // (3-4→3-5 전환과 동일한 이유).
        _highlighter.CollapseAndResetOrigin();
        yield return _highlighter.Highlight(dispatchConfirmButton);

        // ⚠️ 이 클릭이 팀장을 확정시켜 곧장 팀장점수 화면(7-1)의 Show()로 이어질 수 있다 — 세대를
        // 명시적으로 넘겨 레이스 방지(5-4→6-1과 동일한 이유, TutorialHighlighter.Hide 주석 참고).
        yield return _highlighter.Hide(gen6);
        OnboardingState.MarkTutorial6Done();
        // ⚠️ 여기서 Destroy 안 함 — 팀장 확정 직후 자동으로 뜨는 팀장점수 화면(LeaderScoreUI)에서
        // 이어지는 7-1~7-6이 남아있다. DevelopmentManager.BuildAndShowLeaderScore가
        // Instance.PlayTutorial7_1()을 외부에서 호출하므로 이 컴포넌트가 계속 살아있어야 함.
    }

    // ── 7-1/7-2 (DevelopmentManager.BuildAndShowLeaderScore — 팀장점수 1회차 끝난 직후 호출) ──────
    // 1회차만 먼저 재생돼있는 상태(LeaderScoreUI.ShowRound1Then)에서 대사만 보여주고, 끝나면
    // onDone(2~3회차 재생 시작)을 호출한다. 강조는 없음(7-1)/dsSlider 유지(7-2).
    public IEnumerator PlayTutorial7_1(System.Action onDone)
    {
        EnsureHighlighter();
        // LeaderScoreUI.InitPanel이 이미 GameTimeManager.StopTime()을 호출했으므로 별도 시간정지 불필요.
        yield return _highlighter.Show();
        int gen71 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_1, step7_1Position);

        // 7-2: dsSlider 강조를 유지한 채로 대사 표시.
        yield return _highlighter.BeginHighlight(dsSliderRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_2, step7_2Position);

        yield return _highlighter.Hide(gen71);
        onDone?.Invoke(); // 2~3회차 재생 시작(LeaderScoreUI.ContinueRounds2And3)
    }

    // ── 7-3~7-6 (PlayTutorial7_1의 onDone → 2~3회차 재생 직후 호출) ──────────────────────────
    // 2~3회차 연출이 실제로 끝나 4회차 대기 상태(IsWaitingForRound4Aim)가 될 때까지 기다린 뒤 진행.
    public IEnumerator PlayTutorial7_3ToEnd()
    {
        while (LeaderScoreUI.Instance == null || !LeaderScoreUI.Instance.IsWaitingForRound4Aim)
            yield return null;

        // 유저가 안내(7-3~7-5-1) 끝나기 전에 조준 버튼을 못 누르게 즉시 잠금 — aimButtonsRoot 자체는
        // LeaderScoreAimButtons.Update()가 자동으로 활성화하지만(폴링), interactable은 별도라 여기서
        // 직접 꺼야 한다. 7-5-2에서 aimHighButton만 다시 켬(그 외엔 계속 잠김 — "강" 강제 유도).
        if (aimLowButton  != null) aimLowButton.interactable  = false;
        if (aimMidButton  != null) aimMidButton.interactable  = false;
        if (aimHighButton != null) aimHighButton.interactable = false;

        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen73 = _highlighter.CurrentGeneration;

        // 7-3: 강조 없이 대사만(2줄) — 스트레스가 높을수록 점수가 높다는 상관관계 설명.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_3, step7_3Position);

        // 7-4: aimButtonsRoot 강조 유지한 채 대사(아직 클릭은 안 됨).
        yield return _highlighter.BeginHighlight(aimButtonsRootRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_4, step7_4Position);

        // 7-5-1/7-5-2: "강"(aimHighButton) 으로 강조 이동, 유지한 채 대사 2줄 — 마지막 줄까지는 여전히 잠김.
        yield return _highlighter.BeginHighlight(aimHighButton != null ? (RectTransform)aimHighButton.transform : null);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_5, step7_5Position);

        // 7-5-2 대사 직후에만 클릭 허용 — DevelopmentManager가 1~3회차 U를 이미 고정해뒀으므로(합 31 이상)
        // "강" 조준의 4회차 U가 얼마가 나오든 항상 100을 넘겨 burst된다(강제 오버플로 불필요).
        // BeginHighlight로 계속 돌던 pulse를 먼저 접어야(CollapseAndResetOrigin) 새 Highlight의 pulse와
        // 안 겹친다(3-4→3-5, 6-2→6-3과 동일한 이유). 같은 자리라 슬라이드 없이 그대로 다시 나타남.
        _highlighter.CollapseAndResetOrigin();
        if (aimHighButton != null) aimHighButton.interactable = true;
        yield return _highlighter.Highlight(aimHighButton); // 클릭 → LeaderScoreAimButtons.Select(High) → SelectRound4Aim

        yield return _highlighter.Hide(gen73);

        // 4회차(burst 확정) 연출이 화면상 다 끝날 때까지 대기 — LeaderScoreUI.OnRoundsVisualComplete 1회성 구독.
        bool roundsDone = false;
        System.Action onRoundsDone = () => roundsDone = true;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete += onRoundsDone;
        while (!roundsDone) yield return null;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete -= onRoundsDone;

        // 7-6: 강조 없이 대사만(2줄) — burst로 점수가 깎인 이유 설명.
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen76 = _highlighter.CurrentGeneration;
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step7_6, step7_6Position);

        yield return _highlighter.Hide(gen76);
        OnboardingState.MarkTutorial7Done();

        // 8-1: confirmBtn을 눌러 팀장점수 패널이 "실제로" 닫힐 때까지 대기 — 위 Hide()로 dim이 걷혀야
        // confirmBtn 클릭이 가능해지므로(그 전엔 dim이 화면 전체를 덮어 클릭 자체가 막혀있음) 여기서
        // 구독해도 이벤트를 놓칠 위험이 없다.
        bool confirmClosed = false;
        System.Action onConfirmClosed = () => confirmClosed = true;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnConfirmClosed += onConfirmClosed;
        while (!confirmClosed) yield return null;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnConfirmClosed -= onConfirmClosed;

        yield return PlayTutorial8_1();
    }

    // ── 8-1 (LeaderScoreUI.OnConfirmClosed — 팀장점수 패널이 실제로 닫히고 개발이 시작된 직후) ──────
    public IEnumerator PlayTutorial8_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // LeaderScoreUI.OnClickConfirm이 이미 GameTimeManager.StartTime()을 호출했으므로 이번엔 직접 시간 정지
        yield return _highlighter.Show();
        int gen81 = _highlighter.CurrentGeneration;

        yield return _highlighter.BeginHighlight(surpriseQuestUIRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step8_1, step8_1Position);

        yield return _highlighter.Hide(gen81);
        OnboardingState.MarkTutorial8Done();
        Destroy(gameObject); // 1-1~8-1 전부 끝 — 온보딩 튜토리얼 전체 완료
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

    // 진단 로그용 — 하이라이트 대상이 정확히 어느 오브젝트인지 계층 경로로 확인하기 위함.
    static string GetPath(Transform t)
    {
        if (t == null) return "(null)";
        var sb = new System.Text.StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
        return sb.ToString();
    }
}
