using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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

    [Header("8-1 (프로젝트 진행도 15% 시점 — DevelopmentManager.DevelopmentCoroutine 직접 호출)")]
    [Tooltip("HUDCanvas/.../DevelopmentPanel/SupriseQuestUI — 강조 유지, 클릭 대기 없음(대사 3줄 끝나면 자동 종료)")]
    public RectTransform surpriseQuestUIRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 도전 과제(SupriseQuestUI) 소개 대사(강조 유지, 3줄)")]
    public string step8_1 = "8-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 8-1 표시 위치")]
    public Vector2 step8_1Position;

    [Header("9-1/9-2/9-3 (개발팀장 점수, 4회차 조준 대기~선택~결과 — DevelopmentManager 호출)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 4회차 조준 대기 시점 안내(강조 없음, 2줄). 약/중/강 버튼은 잠그지 않음")]
    public string step9_1 = "9-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 9-1 표시 위치")]
    public Vector2 step9_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 약/중 선택 시 반응 대사(강조 없음, 1줄)")]
    public string step9_2LowMid = "9-2a";
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 강 선택 시 반응 대사(강조 없음, 1줄)")]
    public string step9_2High = "9-2b";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 9-2 표시 위치")]
    public Vector2 step9_2Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 스트레스 게이지 다 오른 뒤, 약 선택 시 반응 대사(강조 없음, 1줄)")]
    public string step9_3Low = "9-3a";
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 스트레스 게이지 다 오른 뒤, 중 선택 시 반응 대사(강조 없음, 1줄)")]
    public string step9_3Mid = "9-3b";
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 스트레스 게이지 다 오른 뒤, 강 선택 시 반응 대사(강조 없음, 1줄)")]
    public string step9_3High = "9-3c";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 9-3 표시 위치")]
    public Vector2 step9_3Position;

    [Header("10-1/10-2 (직원 카드 확인 + 만족도 소개 → AcWar 결정적 발동 — PlayTutorial9_2 종료 직후 이어서 재생)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 직원 클릭 유도 대사(강조 없음, 1줄)")]
    public string step10_1 = "10-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 10-1 표시 위치")]
    public Vector2 step10_1Position;
    [Tooltip("강조 + 클릭 대기 대상 데스크 ID — 이 자리에 앉은 직원을 강조한다")]
    public string step10_1DeskId = "desk_01";
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 만족도 슬라이더 소개 대사(강조 유지, 3줄)")]
    public string step10_2 = "10-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 10-2 표시 위치")]
    public Vector2 step10_2Position;

    [Header("10-3 (AcWar 이벤트 완전히 종료 후 — RandomEventManager.TriggerTutorialAcWar의 onResolved 호출)")]
    [Tooltip("EmployeeCardUI/EmployeeCardPanel/ECMiddlePanel — 만족도 변화의 의미 설명(강조 유지, 4줄)")]
    public RectTransform ecMiddlePanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 만족도 의미 설명(강조 유지, 4줄). 1번째 줄 텍스트에 {직원이름} 플레이스홀더 사용")]
    public string step10_3 = "10-3";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 10-3 표시 위치")]
    public Vector2 step10_3Position;

    [Header("12-1 (아트팀장 선택 — DispatchPanelUI.OpenLeaderInternal이 Artist 타입 첫 오픈 시 PlayTutorial12() 호출, 75% 진행도. 11단계는 추후 별도 구현 예정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 아트 직원이 없어 대표님이 직접 맡아야 한다는 안내(강조 유지, 3줄)")]
    public string step12_1 = "12-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 12-1 표시 위치")]
    public Vector2 step12_1Position;

    [Header("13-1/13-2 (진행도 ~95% 확정 창의성 틱 — DevelopmentManager.DevelopmentCoroutine가 그 틱 발동 직후 PlayTutorial13() 호출)")]
    [Tooltip("HUDCanvas/SafeAreaPanel/DevelopmentPanel/MainScorePanel/CreavPanel — 창의성 점수 강조(강조 유지, 3줄)")]
    public RectTransform creavPanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 창의성 점수 소개(강조 유지, 3줄)")]
    public string step13_1 = "13-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 13-1 표시 위치")]
    public Vector2 step13_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 직원 머리 위 창의성 블록 소개(강조 유지, 2줄)")]
    public string step13_2 = "13-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 13-2 표시 위치")]
    public Vector2 step13_2Position;

    [Header("13-3/13-4 (CreativityGameUI.Open — 창의성 미니게임 진입 직후 PlayTutorial13_3() 호출, 첫 프로젝트 한정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 가방에 블록을 채우면 점수를 얻는다는 소개(강조 유지, 2줄)")]
    public string step13_3 = "13-3";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 13-3 표시 위치")]
    public Vector2 step13_3Position;
    [Tooltip("CreativityCanvas/.../BlockTrayArea/InfoPanel/ScorePanel — 점수 강조(강조 유지, 2줄)")]
    public RectTransform scorePanelRect;
    [Tooltip("GuideBlockPlacement 중 표시되는 손가락 드래그 힌트 이미지 — 블록 하이라이트 위치↔목표 자리(TargetMarker) 중앙을\n루프 왕복하며 \"이걸 끌어다 놓으라\"는 뜻을 시각적으로 안내. Image의 RectTransform. raycastTarget은 꺼둘 것(드래그 방해 금지).\n평소엔 비활성 상태로 두면 됨(코드가 필요할 때만 SetActive).")]
    public RectTransform dragHintFinger;
    [Tooltip("손가락 힌트가 시작점↔목표점을 한 번 왕복하는 데 걸리는 시간(초)")]
    public float dragHintLoopDuration = 0.9f;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — Sq 배치 후 점수 상승 반응(강조 유지, 2줄)")]
    public string step13_4 = "13-4";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 13-4 표시 위치")]
    public Vector2 step13_4Position;

    [Header("13-5 (디버깅 단계 시작 직후 — DevelopmentManager.ShowCreativityGame 콜백 체인에서 PlayTutorial13_5() 호출, 첫 프로젝트 한정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 창의성 능력치가 버그 제거에도 쓰인다는 안내(강조 없음, 2줄)")]
    public string step13_5 = "13-5";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 13-5 표시 위치")]
    public Vector2 step13_5Position;

    [Header("14-1 (디버깅 끝나고 DevelopmentResultPanel 활성화 직후 — DevelopmentManager.ShowResultInternal 콜백 체인에서 PlayTutorial14_1() 호출, 첫 프로젝트 한정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 첫 게임 완성 축하 + 기여도 확인 안내(강조 없음, 2줄)")]
    public string step14_1 = "14-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 14-1 표시 위치")]
    public Vector2 step14_1Position;

    [Header("15-1/15-2 (MarketingUI.Show — 마케팅 패널 열릴 때 PlayTutorial15() 호출, 첫 프로젝트 한정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 마케팅의 중요성 소개(강조 없음, 1줄)")]
    public string step15_1 = "15-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 15-1 표시 위치")]
    public Vector2 step15_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 마케팅비 부족 시 페널티 경고(강조 유지, 1줄)")]
    public string step15_2 = "15-2";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 15-2 표시 위치")]
    public Vector2 step15_2Position;
    [Tooltip("MarketingUI.slotButtons[1] (LeftPanel 두 번째 슬롯 = \"PC방 광고\")의 RectTransform — 강조 대상")]
    public RectTransform marketingSecondSlotRect;

    [Header("16-1 (SalesUI — 판매 패널 열리고 1주차 bar 애니메이션 시작 시 PlayTutorial16_1() 호출, 첫 프로젝트 한정)")]
    [Tooltip("⚠️ 이 스텝은 시간을 멈추지 않는다(BeginDimTimeStop 미사용) — 매출 bar가 계속 오르는 걸 보여주면서 대사 진행. " +
             "TutorialDialog 차트의 stepGroup 값 — 매출 속도에 놀라는 반응(강조 유지, 2줄)")]
    public string step16_1 = "16-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 16-1 표시 위치")]
    public Vector2 step16_1Position;
    [Tooltip("SalesUI.chartBG(SalesPanel/chartBG)의 RectTransform — 강조 대상")]
    public RectTransform chartBGRect;

    [Header("17-1 (SalesUI.OnSalesComplete — 첫 판매 완료 직후, 직원 강화 메뉴 유도)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 강화 메뉴 유도 대사")]
    public string step17_1 = "17-1";
    [Tooltip("TutorialPanel의 RectTransform.anchoredPosition — 17-1 표시 위치")]
    public Vector2 step17_1Position;
    [Tooltip("MenuCanvas/SafeAreaPanel/TopMenuUI/EmployeeSubMenu/trainingMenuBtn — 메뉴→직원→강화 순 강조의 마지막 대상")]
    public Button trainingMenuButton;

    [Header("17-2~17-6 (EmployeeListUI.OpenListForEnhance — 강화 패널 첫 진입, 강화 4회 체험, 첫 판매 한정)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 강화하면 능력치+연봉이 함께 오른다는 안내")]
    public string step17_2 = "17-2";
    public Vector2 step17_2Position;
    [Tooltip("EmployeeTrainingPanel의 CurrentStatusPanel/ArrowPanel/AfterStatusPanel 3개를 감싸는 래퍼(TrainingStatusHighlightArea) RectTransform — 강조 대상")]
    public RectTransform employeeTrainingPanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 4강까지 진행해보자는 유도")]
    public string step17_3 = "17-3";
    public Vector2 step17_3Position;
    [Tooltip("TrainingPanel/EmployeeTrainingPanel/BottomPanel/enhanceBtn — 4회 클릭 내내 강조 유지")]
    public Button trainingEnhanceButton;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 4연속 성공에 대한 반응")]
    public string step17_4 = "17-4";
    public Vector2 step17_4Position;
    [Tooltip("TrainingPanel/EmployeeTrainingPanel/BadgePanel/enhancementPanel(+N 뱃지)의 RectTransform — 강조 대상")]
    public RectTransform trainingEnhancementPanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 오늘 운이 좋다는 마무리 대사")]
    public string step17_5 = "17-5";
    public Vector2 step17_5Position;
    [Tooltip("EmployeeListUI/EmployeePanel/CloseBtn — 17-5 이전엔 비활성화해뒀다가 여기서 다시 활성화 + 강조")]
    public Button trainingCloseButton;
    [Tooltip("EmployeeListUI/EmployeePanel/DetailPanel/TrainingPanel/.../backBtn — TrainingPanel 진입 시부터 17-6 끝날 때까지 비활성화")]
    public Button trainingBackButton;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 강화 패널이 닫힌 직후, 마침 상인이 왔다는 대사(강조 없음)")]
    public string step17_6 = "17-6";
    public Vector2 step17_6Position;

    [Header("17-7~17-9-2 (MerchantShopPanelUI.Open — 상인 상점이 처음 열릴 때, 구매/닫기+아이템 사용 체험까지 한 세션)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 아이템 구매/사용 유도")]
    public string step17_7 = "17-7";
    public Vector2 step17_7Position;
    [Tooltip("ItemUI/MerchantShopPanel/.../ConfirmBtn — 구매 버튼")]
    public Button merchantConfirmButton;
    [Tooltip("ItemUI/MerchantShopPanel/.../CloseBtn — 상점 닫기 버튼")]
    public Button merchantCloseButton;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 커피를 직원에게 주면 어떻게 되는지 확인해보자는 유도")]
    public string step17_8 = "17-8";
    public Vector2 step17_8Position;
    [Tooltip("ItemUI/ItemPanelBG/ItemPanel/CloseBtn — 아이템 사용 체험 도중 이탈 못 하게 잠깐 비활성화")]
    public Button itemPanelCloseButton;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 커피 마신 직원 기분 좋아 보인다는 반응(강조 없음)")]
    public string step17_9_1 = "17-9-1";
    public Vector2 step17_9_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 아이템은 위기 탈출용으로 아껴두라는 조언(강조 없음)")]
    public string step17_9_2 = "17-9-2";
    public Vector2 step17_9_2Position;

    [Header("18-1~18-4 (17-9 종료 직후 자동 연결, 또는 재접속 시 Start()가 단독 재생)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 오늘 돈을 많이 썼다는 반응")]
    public string step18_1 = "18-1";
    public Vector2 step18_1Position;
    [Tooltip("HUDCanvas/SafeAreaPanel/RightTopPanel/Time_MoneyPanel/MoneyPanel — 18-1 강조 대상")]
    public RectTransform moneyPanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 매년 1월 연봉/7월 사무실비 지출 안내")]
    public string step18_2 = "18-2";
    public Vector2 step18_2Position;
    [Tooltip("HUDCanvas/SafeAreaPanel/RightTopPanel/Salary_AnnualPanel — 18-2~18-3 강조 대상")]
    public RectTransform salaryAnnualPanelRect;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 통장이 0이 되면 파산이라는 경고")]
    public string step18_3 = "18-3";
    public Vector2 step18_3Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 파산하면 어떻게 되는지 얼버무리는 마무리(강조 없음)")]
    public string step18_4 = "18-4";
    public Vector2 step18_4Position;

    [Header("19-1 (18-4 종료 직후 자동 연결, 또는 재접속 시 Start()가 단독 재생 — 강조 없음)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 이제 혼자 해보라는 마무리 대사")]
    public string step19_1 = "19-1";
    public Vector2 step19_1Position;

    [Header("20-1~20-2 (2번째 프로젝트 기획팀장점수 강제 burst 직후 — DevelopmentManager가 직접 호출, 강조 없음)")]
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — burst로 점수가 깎인 걸 보고 놀라는 반응")]
    public string step20_1 = "20-1";
    public Vector2 step20_1Position;
    [Tooltip("TutorialDialog 차트의 stepGroup 값 — 운이 나빴을 뿐이라고 넘어가는 대사")]
    public string step20_2 = "20-2";
    public Vector2 step20_2Position;

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

    // 지금까지 정의된 모든 튜토리얼 단계가 전부 끝났는지(=온보딩 완전 종료) — 아래 Start()의 자폭 판정과
    // 반드시 같은 needStepN 목록을 유지해야 한다. 새 단계를 추가할 때 여기 한 줄만 같이 추가하면, 이 값을
    // 참조하는 외부 코드(MenuController 등)는 손댈 필요가 전혀 없다 — "마지막 단계가 몇 번인지"를 특정
    // 이벤트 발동 지점으로 수동 지정/이동하는 방식(과거 OnboardingState.OnOnboardingFullyDone)은 매번
    // 최신 마지막 단계로 옮겨줘야 해서 새 단계 추가 시 자꾸 깜빡하는 문제가 있었음 — 그래서 폐기.
    public static bool IsFullyDone() => IsFullyDoneInternal(includeStep20: true);

    // 메뉴 잠금 해제 판정 전용 — IsFullyDone()과 달리 20 이후 단계는 뺀다. 20은 "게임개발" 메뉴로
    // 새 프로젝트를 시작해야만 트리거되는데, 메뉴 잠금 해제 조건에 20을 넣으면 "메뉴가 열려야 20이
    // 끝나는데, 20이 끝나야 메뉴가 열리는" 순환 잠금이 생긴다(19-1 "이제 혼자 해보라"는 대사 직후
    // 실제로 겪었던 소프트락). MenuController는 이 메서드만 봐야 하며, 앞으로 메뉴 접근을 전제로 하는
    // 단계(20 이후에도 있을 수 있음)를 추가하면 그 단계도 여기서 제외해야 한다.
    public static bool IsMenuUnlockReady() => IsFullyDoneInternal(includeStep20: false);

    static bool IsFullyDoneInternal(bool includeStep20)
    {
        bool needStep1 = !OnboardingState.TutorialDone
            && RunStateManager.Instance != null && RunStateManager.Instance.IsTutorial;
        bool needStep3 = !OnboardingState.Tutorial3Done;
        bool needStep5 = OnboardingState.Tutorial5Pending && !OnboardingState.Tutorial6Done;
        bool needStep6 = !OnboardingState.Tutorial6Done;
        bool needStep7 = !OnboardingState.Tutorial7Done;
        bool needStep8 = OnboardingState.Tutorial7Done && !OnboardingState.Tutorial8Done;
        bool needStep9 = !OnboardingState.Tutorial9Done;
        bool needStep10 = !OnboardingState.Tutorial10Done;
        bool needStep12 = !OnboardingState.Tutorial12Done;
        bool needStep13 = !OnboardingState.Tutorial13Done;
        bool needStep13_4 = !OnboardingState.Tutorial13_4Done;
        bool needStep13_5 = !OnboardingState.Tutorial13_5Done;
        bool needStep14_1 = !OnboardingState.Tutorial14_1Done;
        bool needStep15 = !OnboardingState.Tutorial15Done;
        bool needStep16_1 = !OnboardingState.Tutorial16_1Done;
        bool needStep17_1 = !OnboardingState.Tutorial17_1Done;
        bool needStep17_2 = !OnboardingState.Tutorial17_2Done;
        bool needStep17_7 = !OnboardingState.Tutorial17_7Done;
        bool needStep18 = !OnboardingState.Tutorial18Done;
        bool needStep19 = !OnboardingState.Tutorial19Done;
        bool needStep20 = !OnboardingState.Tutorial20Done;

        bool baseDone = !needStep1 && !needStep3 && !needStep5 && !needStep6 && !needStep7 && !needStep8
            && !needStep9 && !needStep10 && !needStep12 && !needStep13 && !needStep13_4 && !needStep13_5
            && !needStep14_1 && !needStep15 && !needStep16_1 && !needStep17_1 && !needStep17_2 && !needStep17_7
            && !needStep18 && !needStep19;

        return includeStep20 ? (baseDone && !needStep20) : baseDone;
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
        // 8-1은 진행도 15% 시점에 DevelopmentManager.DevelopmentCoroutine이 Instance.PlayTutorial8_1()을
        // 직접 호출한다(9단계와 동일한 방식) — 재접속 시에도 _elapsed가 복원된 채 코루틴이 다시 돌면서
        // 그 15% 체크를 자연히 다시 타므로, 여기서 별도로 즉시 재생시킬 필요가 없다(오히려 즉시 재생하면
        // 진행도와 무관하게 너무 일찍 뜨게 됨).
        bool needStep8 = OnboardingState.Tutorial7Done && !OnboardingState.Tutorial8Done;
        // 9-1/9-2는 개발팀장 점수 화면(진행도 25% 시점에 열림, 8-1보다 한참 뒤) — 6/7단계와 동일 이유로
        // pending 불필요, BuildAndShowLeaderScore가 Programmer 타입일 때 이 조건을 직접 체크해서 부른다.
        bool needStep9 = !OnboardingState.Tutorial9Done;
        // 10-1~10-3은 9-2 코루틴 끝에서 곧바로 이어지는 all-or-nothing 구간(5-1~6-2와 동일 방식) — 9단계는
        // 끝났는데 10단계가 아직이면(재접속으로 도중 끊긴 경우 포함) 아래에서 무조건 10-1부터 다시 재생한다.
        bool needStep10 = !OnboardingState.Tutorial10Done;
        // 12-1은 아트팀장 선택(75% 진행도)이 열릴 때마다(재접속 자연 재오픈 포함) DispatchPanelUI가 완료
        // 여부를 직접 체크해서 부른다 — 6/7/9단계와 동일 이유로 pending 불필요. 11단계는 아직 미구현.
        bool needStep12 = !OnboardingState.Tutorial12Done;
        // 13-1/13-2는 진행도 ~95% 지점에서 그 확정 창의성 틱이 실제로 발동할 때 DevelopmentCoroutine이
        // 직접 체크해서 부른다(8단계와 동일 이유) — 재접속해도 _elapsed 복원 후 같은 틱 인덱스 체크를
        // 자연히 다시 타므로 여기서 즉시 재생시킬 필요 없음.
        bool needStep13 = !OnboardingState.Tutorial13Done;
        // 13-4는 창의성 미니게임이 열릴 때마다(재접속 자연 재오픈 포함) CreativityGameUI.Open이 완료
        // 여부를 직접 체크해서 부른다 — pending 불필요.
        bool needStep13_4 = !OnboardingState.Tutorial13_4Done;
        // 13-5는 디버깅 단계가 시작될 때 DevelopmentManager.ShowCreativityGame 콜백 체인에서 직접 체크해서
        // 부른다 — pending 불필요.
        bool needStep13_5 = !OnboardingState.Tutorial13_5Done;
        // 14-1은 디버깅이 끝나고 DevelopmentResultPanel이 뜰 때 DevelopmentManager.ShowResultInternal 콜백
        // 체인에서 직접 체크해서 부른다 — pending 불필요.
        bool needStep14_1 = !OnboardingState.Tutorial14_1Done;
        // 15-1/15-2는 마케팅 패널이 열릴 때마다(재접속 자연 재오픈 포함) MarketingUI.Show가 직접 체크해서
        // 부른다 — pending 불필요.
        bool needStep15 = !OnboardingState.Tutorial15Done;
        // 16-1은 판매 패널이 열릴 때마다(재접속 자연 재오픈 포함) SalesUI.ShowBarsSequentially가 1주차
        // bar 애니메이션 시작 시점에 직접 체크해서 부른다 — pending 불필요.
        bool needStep16_1 = !OnboardingState.Tutorial16_1Done;
        // 17-1은 판매가 완료될 때마다(재접속 자연 재오픈 포함) SalesUI.OnSalesComplete가 직접 체크해서
        // 부른다 — pending 불필요.
        bool needStep17_1 = !OnboardingState.Tutorial17_1Done;
        // 17-1~17-6은 강화 4연속 체험이 구간 끝(PlayTutorial17_6)에서야 한 번에 서버 저장되는
        // all-or-nothing 구간(10-1~10-3과 동일 방식) — 도중에 재접속이 끊겨도 서버 상태는 그 이전으로
        // 남아있으므로, 16-1까지는 끝났는데 이게 아직이면 아래에서 무조건 17-1부터 다시 재생한다.
        bool needStep17_2 = !OnboardingState.Tutorial17_2Done;
        // 17-7~17-9는 상점 닫기(구매 커밋)/아이템 사용(소비 커밋) 두 지점이 실제 서버 저장이라, 그
        // 사이에서 재접속이 끊기면 절대 처음부터 다시 재생하면 안 된다(중복 구매/중복 사용 방지) —
        // 아래 세 분기가 Tutorial17_7ShopDone/Tutorial17_8UsedDone 조합으로 정확히 그 다음 지점부터
        // 재개한다. 상점(MerchantShopPanelUI.Open)이 실제로 열릴 때는 그때도 직접 체크해서 부름.
        bool needStep17_7Shop = !OnboardingState.Tutorial17_7ShopDone;
        bool needStep17_8Used = !OnboardingState.Tutorial17_8UsedDone;
        bool needStep17_7 = !OnboardingState.Tutorial17_7Done;
        // 18-1~18-4는 17-9 종료 직후 자동으로 이어지는 순수 대사 구간(커밋 지점 없음) — 17-7~17-9
        // 전체(Tutorial17_7Done)가 끝났는데 이게 아직이면 처음(18-1)부터 다시 재생한다.
        bool needStep18 = !OnboardingState.Tutorial18Done;
        // 19-1은 18-1~18-4 종료 직후 자동으로 이어지는 순수 대사(강조 없음) — 18이 끝났는데 이게 아직이면
        // 처음(19-1)부터 다시 재생한다.
        bool needStep19 = !OnboardingState.Tutorial19Done;

        // 17~26단계 등 아직 미구현 콘텐츠가 남아있어 IsFullyDone() 기준 자멸 로직은 보류(추후 재논의).

        Instance = this;
        if (needStep1) StartCoroutine(Run());
        else if (needStep5) StartCoroutine(PlayTutorial5_1());
        // 10-1~10-3은 외부 재트리거 진입점이 없는 all-or-nothing 구간(5-1~6-2와 동일한 방식) — 9단계까지는
        // 끝났는데 10단계가 아직 안 끝났으면(재접속 등으로 10-1~10-3 도중 끊긴 경우 포함) 여기서 무조건
        // 처음(10-1)부터 다시 재생한다.
        else if (needStep10 && OnboardingState.Tutorial9Done) StartCoroutine(PlayTutorial10_1());
        // 17-1~17-6도 동일한 all-or-nothing 방식 — 16-1까지는 끝났는데 아직이면 처음(17-1)부터 재생.
        else if (needStep17_2 && OnboardingState.Tutorial16_1Done) StartCoroutine(PlayTutorial17_1());
        // 17-6은 끝나 저장까지 됐는데(Tutorial17_2Done) 상점을 아직 안 닫았으면(구매 미커밋 포함) —
        // 상인 소환부터 처음부터 다시(17-6→17-7Shop).
        else if (needStep17_7Shop && OnboardingState.Tutorial17_2Done) StartCoroutine(PlayTutorial17_6());
        // 상점은 닫아 구매까지 커밋됐는데 아이템을 아직 안 썼으면 — 상인/상점은 건너뛰고 17-8부터 재개.
        else if (needStep17_8Used && OnboardingState.Tutorial17_7ShopDone) StartCoroutine(PlayTutorial17_8Use());
        // 아이템 사용까지 커밋됐는데 마무리 대사(17-9)가 아직이면 — 그 대사부터(끝나면 18도 자동 이어짐).
        else if (needStep17_7 && OnboardingState.Tutorial17_8UsedDone) StartCoroutine(PlayTutorial17_9Wrap());
        // 17-7~17-9 전부 끝났는데 18-1~18-4(지출/파산 경고 대사)가 아직이면 — 처음(18-1)부터 다시 재생.
        else if (needStep18 && OnboardingState.Tutorial17_7Done) StartCoroutine(PlayTutorial18());
        // 18-1~18-4까지 끝났는데 19-1(마무리 핸드오프 대사)이 아직이면 — 그 대사만 재생.
        else if (needStep19 && OnboardingState.Tutorial18Done) StartCoroutine(PlayTutorial19());
        // needStep3/needStep6/needStep7/needStep8/needStep9만 남았으면 여기서 아무것도 안 하고 대기 —
        // HiringUI.ShowConfirmDirect가 Instance.PlayTutorial3()을, DispatchPanelUI가 Instance.PlayTutorial6()을,
        // DevelopmentManager가 Instance.PlayTutorial7_1()/PlayTutorial8_1()을 각각 해당 시점에 직접 호출한다.
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

        // SummaryPanel의 CloseBtn(OnClickClose)은 장르/플랫폼 선택을 전부 초기화하고 프로젝트 설정 자체를
        // 취소해버리므로, 5-x 튜토리얼이 이 패널을 붙잡고 있는 동안(5-4에서 실제 개발이 시작될 때까지)은
        // 눌리면 안 된다 — 여기서 숨기고 PlayTutorial5_4에서 개발 시작 직후 되돌린다.
        if (ProjectSetupUI.Instance != null && ProjectSetupUI.Instance.closeButton != null)
            ProjectSetupUI.Instance.closeButton.gameObject.SetActive(false);

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

        // 5-1에서 숨겨뒀던 CloseBtn 복원 — 개발이 시작돼 SummaryPanel이 어차피 닫히지만, 상태 정합성을
        // 위해 명시적으로 되돌려둔다.
        if (ProjectSetupUI.Instance != null && ProjectSetupUI.Instance.closeButton != null)
            ProjectSetupUI.Instance.closeButton.gameObject.SetActive(true);

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
        // 8-1은 여기서 바로 이어지지 않음 — DevelopmentManager.DevelopmentCoroutine이 진행도 15%
        // 지점에서 Instance.PlayTutorial8_1()을 직접 호출한다(아래 참고).
    }

    // ── 8-1 (DevelopmentManager.DevelopmentCoroutine — 프로젝트 진행도 15% 시점에 직접 호출) ──────
    public IEnumerator PlayTutorial8_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // DevelopmentCoroutine이 진행 중(시간 흐르는 중)에 호출하므로 여기서 직접 시간 정지
        yield return _highlighter.Show();
        int gen81 = _highlighter.CurrentGeneration;

        yield return _highlighter.BeginHighlight(surpriseQuestUIRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step8_1, step8_1Position);

        yield return _highlighter.Hide(gen81);
        EndDimTimeStop();
        // ⚠️ 위 EndDimTimeStop() 명시 호출 필수 — 예전엔 8-1 끝나고 바로 Destroy(gameObject)해서
        // OnDestroy()가 이걸 대신 불러줬는데(중간에 파괴돼도 시간 정지 누수 방지용), 9-1/9-2를 위해
        // Destroy를 없애면서 그 경로가 사라졌다 — 안 부르면 게임 시간이 영원히 멈춘 채로 남는다
        // (BeginDimTimeStop만 걸리고 짝이 안 맞음). "팀장점수 패널 닫히면 시간이 가야 하는데 안 감" 버그.
        OnboardingState.MarkTutorial8Done();
        // ⚠️ 여기서 Destroy 안 함 — 개발팀장(Programmer) 점수 화면(진행도 25% 시점, 한참 뒤)에서 이어지는
        // 9-1/9-2가 남아있다. DevelopmentManager.BuildAndShowLeaderScore가 Instance.PlayTutorial9_1()을
        // 외부에서 호출하므로 이 컴포넌트가 계속 살아있어야 함.
    }

    // ── 9-1 (DevelopmentManager.BuildAndShowLeaderScore — 개발팀장 점수, 4회차 조준 대기 시점) ──────
    public IEnumerator PlayTutorial9_1()
    {
        while (LeaderScoreUI.Instance == null || !LeaderScoreUI.Instance.IsWaitingForRound4Aim)
            yield return null;

        // ⚠️ LeaderScoreAimButtons가 interactable을 스스로 리셋하지 않아서, 이전 7-x(기획팀장) 라운드가
        // "강"만 남기고 잠가둔 상태(7-5-1/7-5-2)가 그대로 남아있다 — 여기서 세 버튼 다 명시적으로 풀어줘야
        // 실제로 약/중/강 자유 선택이 된다(안 풀면 강만 눌리는 버그).
        if (aimLowButton  != null) aimLowButton.interactable  = true;
        if (aimMidButton  != null) aimMidButton.interactable  = true;
        if (aimHighButton != null) aimHighButton.interactable = true;

        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen91 = _highlighter.CurrentGeneration;

        // 강조 없이 대사만 — 약/중/강 버튼을 잠그지 않는다(7-x와 달리 유저가 자유롭게 고름).
        // dim이 떠 있는 동안은 버튼이 화면에 덮여 자연히 클릭이 안 되고, Hide() 이후에야 눌릴 수 있다.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step9_1, step9_1Position);

        yield return _highlighter.Hide(gen91);
        // 여기서 Tutorial9Done 마크 안 함 — 유저가 실제로 조준 버튼을 눌러야(9-2) 완료된다.
    }

    // ── 9-2/9-3 (DevelopmentManager.SelectRound4Aim — 유저가 조준 버튼을 실제로 누른 직후, 결과 반영 전) ──
    // 9-2: 4회차 결과/스트레스 상승 애니메이션(PlayRound4AndFinish)을 이 대사가 끝날 때까지 미뤄야 하므로,
    // 호출부가 onDone에 그 실행을 넘겨준다 — 대사가 먼저 뜨고, 대사가 끝나야 스트레스가 오르기 시작한다.
    // 9-3: onDone(=PlayRound4AndFinish) 실행 후 스트레스 게이지가 다 올라갈 때까지(LeaderScoreUI.
    // OnRoundsVisualComplete, 7-6과 동일 신호) 기다렸다가 선택지(약/중/강)별 반응 대사.
    public IEnumerator PlayTutorial9_2(LeaderScoreAim aim, System.Action onDone)
    {
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen92 = _highlighter.CurrentGeneration;

        string step92 = aim == LeaderScoreAim.High ? step9_2High : step9_2LowMid;
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step92, step9_2Position);

        yield return _highlighter.Hide(gen92);
        onDone?.Invoke();

        bool roundsDone = false;
        System.Action onRoundsDone = () => roundsDone = true;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete += onRoundsDone;
        while (!roundsDone) yield return null;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete -= onRoundsDone;

        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen93 = _highlighter.CurrentGeneration;

        string step93 = aim switch
        {
            LeaderScoreAim.Low  => step9_3Low,
            LeaderScoreAim.Mid  => step9_3Mid,
            LeaderScoreAim.High => step9_3High,
            _ => step9_3Low
        };
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step93, step9_3Position);

        yield return _highlighter.Hide(gen93);
        OnboardingState.MarkTutorial9Done();

        // 10-1: LeaderScoreUI(개발팀장 점수 결과 패널)가 아직 화면에 떠 있으므로, 플레이어가 실제로
        // confirmBtn을 눌러 그 패널을 닫을 때까지 먼저 기다린 뒤(위 Hide()로 dim이 걷혀야 confirmBtn 클릭이
        // 가능해지므로, 여기서 구독해도 이벤트를 놓칠 위험이 없다) 2초(realtime) 뒤에 발동한다.
        bool confirmClosed = false;
        System.Action onConfirmClosed = () => confirmClosed = true;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnConfirmClosed += onConfirmClosed;
        while (!confirmClosed) yield return null;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnConfirmClosed -= onConfirmClosed;

        yield return new WaitForSecondsRealtime(2f);

        yield return PlayTutorial10_1();
    }

    // ── 10-1/10-2 (위 PlayTutorial9_2 끝에서 이어서 호출) ──────────────────────
    public IEnumerator PlayTutorial10_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // LeaderScoreUI.OnClickConfirm이 이미 StartTime()을 호출했으므로 이번엔 직접 시간 정지
        yield return _highlighter.Show();
        int gen10 = _highlighter.CurrentGeneration;

        // 10-1: 직원 클릭 유도 대사(강조 없음)
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step10_1, step10_1Position);

        // desk_01 직원 강조 + 클릭(=EmployeeCardUI가 그 직원 카드로 열림) 대기.
        var deskChar = OfficeManager.Instance?.GetCharacterAtDesk(step10_1DeskId);
        string deskEmpId = deskChar != null ? deskChar.employeeId : null;
        yield return _highlighter.BeginHighlightWorld(deskChar != null ? deskChar.transform : null);

        bool cardShown = false;
        System.Action<string> onCardShown = id => { if (id == deskEmpId) cardShown = true; };
        if (EmployeeCardUI.Instance != null) EmployeeCardUI.Instance.OnCardShown += onCardShown;
        while (!cardShown) yield return null;
        if (EmployeeCardUI.Instance != null) EmployeeCardUI.Instance.OnCardShown -= onCardShown;

        // 카드가 열리는 즉시 — AcWar 발동을 위해 desk_01 직원을 master_desk로 강제 이동 시작. 지금은 시간이
        // 멈춰있어(BeginDimTimeStop) 실제로는 안 걷다가, 아래 EndDimTimeStop으로 시간이 풀리는 순간부터
        // 실제로 걸어가기 시작한다(CharacterMover가 GameTimeManager.IsRunning을 체크하므로).
        // onResolved: 선택지 확인 + 결과 AlertUI까지 전부 닫힌 뒤(=진짜 이벤트 종료 시점)에만 10-3으로 이어감.
        if (!string.IsNullOrEmpty(deskEmpId))
            RandomEventManager.Instance?.TriggerTutorialAcWar(deskEmpId,
                winner => StartCoroutine(PlayTutorial10_3(winner)));

        // 10-2: 만족도 슬라이더로 강조 이동(슬라이드) + 대사 3줄
        RectTransform satisfactionRT = EmployeeCardUI.Instance != null && EmployeeCardUI.Instance.satisfactionSlider != null
            ? EmployeeCardUI.Instance.satisfactionSlider.transform as RectTransform
            : null;
        yield return _highlighter.BeginHighlight(satisfactionRT);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step10_2, step10_2Position);

        yield return _highlighter.Hide(gen10);
        EndDimTimeStop();
        // 여기서부터 desk_01 직원이 실제로 master_desk로 걸어가고, 도착하면 RandomEventManager.OnPatrolArrived가
        // 평소와 동일하게 RandomEventChoiceUI(AcWar)를 자연히 띄운다 — 선택→결과 AlertUI까지 전부 기존
        // 프로덕션 로직 그대로 진행되며, 그게 완전히 끝나면 TriggerTutorialAcWar의 onResolved 콜백이
        // PlayTutorial10_3()을 이어서 호출한다(바로 위 참고).
    }

    // ── 10-3 (RandomEventManager.TriggerTutorialAcWar의 onResolved — AcWar 선택+결과 AlertUI까지 전부
    // 닫힌 직후 호출) — 만족도의 의미를 설명. winner: 플레이어가 고른 쪽(만족도가 오른 직원).
    public IEnumerator PlayTutorial10_3(EmployeeData winner)
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // AcWar 확인 흐름이 이미 시간을 재개했으므로 여기서 직접 다시 정지
        yield return _highlighter.Show();
        int gen103 = _highlighter.CurrentGeneration;

        // {직원이름} 플레이스홀더를 실제 이름으로 임시 치환 — RandomEvent 차트와 동일한
        // "템플릿 캡처 → 치환 → 복원" 패턴(TutorialDialogChartLoader.Cache는 재생마다 새로 안 만들어져
        // 원본 템플릿을 반드시 복원해야 다음 재생에서 이름이 안 남는다).
        string winnerName = winner != null ? winner.employeeName : "";
        List<string> originalTexts = null;
        if (TutorialDialogChartLoader.Cache.TryGetValue(step10_3, out var lines) && lines != null)
        {
            originalTexts = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                originalTexts.Add(line.text);
                line.text = line.text?.Replace("{직원이름}", winnerName);
            }
        }

        yield return _highlighter.BeginHighlight(ecMiddlePanelRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step10_3, step10_3Position);

        if (originalTexts != null)
            for (int i = 0; i < lines.Count; i++) lines[i].text = originalTexts[i];

        yield return _highlighter.Hide(gen103);
        EndDimTimeStop();
        OnboardingState.MarkTutorial10Done();
    }

    // ── 12-1 (DispatchPanelUI.OpenLeaderInternal — 아트팀장 선택 패널이 Artist 타입으로 열릴 때 호출) ──
    // 아트 직원이 없는 게 전제라 후보는 CEO 1명뿐(BuildLeaderCandidates가 CEO를 index 0에 넣음) — 그 슬롯을
    // 강조만 하고 클릭 대기는 없음(8-1과 동일 패턴 — 대사 끝나면 자동 종료, 이후 조작은 유저가 자유롭게).
    public IEnumerator PlayTutorial12(Transform slotParent)
    {
        EnsureHighlighter();
        // DispatchPanelUI가 패널을 열면서 이미 GameTimeManager.StopTime()을 호출했으므로(모달 자체 시간정지)
        // 여기서 별도 시간정지는 불필요(6-1과 동일 이유).
        yield return _highlighter.Show();
        int gen12 = _highlighter.CurrentGeneration;

        // 대사가 뜨는 동안 목록이 재생성됐을 가능성을 배제하기 위해 지금 이 시점에 "무조건 첫 번째 자식"을
        // 다시 찾는다(6-1과 동일한 이유 — 캡처해둔 Button 참조는 재생성 시 파괴된 오브젝트가 될 수 있음).
        Button firstSlotButton = null;
        if (slotParent != null && slotParent.childCount >= 1)
            firstSlotButton = slotParent.GetChild(0).GetComponentInChildren<Button>(true);
        else
            Debug.LogWarning($"[TutorialController] PlayTutorial12: slotParent={(slotParent != null ? slotParent.name : "null")}, childCount={(slotParent != null ? slotParent.childCount : -1)} — 첫 번째 자식을 못 찾음");

        yield return _highlighter.BeginHighlight(firstSlotButton != null ? (RectTransform)firstSlotButton.transform : null);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step12_1, step12_1Position);

        yield return _highlighter.Hide(gen12);
        OnboardingState.MarkTutorial12Done();
    }

    // ── 13-1/13-2 (DevelopmentManager.DevelopmentCoroutine — 진행도 ~95% 지점, 첫 직원의 확정
    // 창의성 틱이 실제로 발동(블록 팝업 spawn)한 직후 호출) ───────────────────────────────
    public IEnumerator PlayTutorial13(EmployeeData employee, Transform blockTransform)
    {
        EnsureHighlighter();
        // 블록 팝업(BlockFloatingVisual)의 떠오르는 애니메이션도 GameTimeManager.IsRunning을 보고
        // 진행되므로, 여기서 시간을 멈추면 그 자리에 뜬 채로 같이 멈춰 안정적인 강조 대상이 된다.
        BeginDimTimeStop();
        yield return _highlighter.Show();
        int gen13 = _highlighter.CurrentGeneration;

        // 13-1: 창의성 점수 패널 강조 + 대사 3줄("이 점수는 왜 0점이야?" → "창의성 점수예요" → "블록으로 얻어요")
        yield return _highlighter.BeginHighlight(creavPanelRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step13_1, step13_1Position);

        // 13-2: 강조가 직원 머리 위 창의성 블록(월드스페이스)으로 이동 + 대사 2줄
        yield return _highlighter.BeginHighlightWorld(blockTransform);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step13_2, step13_2Position);

        yield return _highlighter.Hide(gen13);
        EndDimTimeStop(); // 여기서 시간이 다시 흐르면 멈춰있던 블록 팝업도 남은 애니메이션을 마저 재생하고 정상 소멸
        OnboardingState.MarkTutorial13Done();
    }

    // ── 13-3/13-4 (CreativityGameUI.Open — 창의성 미니게임 진입 직후, 첫 프로젝트 한정) ──────
    // 13-3: "Grid"(7x7 고정, 대부분 빈 여백) 대신 실제 활성 4x4 영역만 강조 + 대사 2줄.
    // 이어서 b1.png 설계대로 Sq 블록을 강조해 클릭/드래그를 유도하고, 드래그가 시작되는 즉시 강조를
    // 풀면서 그리드에는 Sq가 들어가야 할 자리(4x4 중앙, 로컬(0,0)~(1,1))만 표시 + 그 자리에만
    // 배치되도록 강제한다. 실제로 놓이면(점수 반영 완료) 13-4: ScorePanel 강조 + 대사 2줄 → 이어서
    // 같은 방식으로 T_U(로컬(2,1)~(3,2)의 T자, b1.png 기준)까지 배치 유도.
    public IEnumerator PlayTutorial13_3()
    {
        var miniGame = CreativityGameUI.Instance;
        var grid = miniGame != null ? miniGame.GridUI : null;
        var block = miniGame != null ? miniGame.FindActiveBlockByShapeName("Sq") : null;
        if (miniGame == null || grid == null || block == null)
        {
            Debug.LogWarning("[TutorialController] PlayTutorial13_3: CreativityGameUI/GridUI/Sq 블록 중 하나를 못 찾음 — 스킵");
            yield break;
        }

        EnsureHighlighter();
        // CreativityGameUI.Open()이 이미 GameTimeManager.StopTime()을 호출했으므로 별도 시간정지 불필요.
        yield return _highlighter.Show();
        int gen133 = _highlighter.CurrentGeneration;

        // 13-3: Grid 전체(7x7)가 아니라 실제 보이는 4x4 영역만 강조.
        yield return _highlighter.BeginHighlight(grid.GetActiveAreaRect());
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step13_3, step13_3Position);

        yield return _highlighter.Hide(gen133);

        // 4x4(튜토리얼 고정 그리드, Level1Grids 유일값) 중앙 오프셋은 (7-4)/2=1 — b1.png 로컬 좌표에
        // (1,1)을 더하면 물리 좌표가 나온다. Sq(2x2, 셰이프 로컬(0,0)~(1,1))는 b1.png 로컬(0,0)~(1,1)
        // 자리라 물리 anchor는 그대로 (1,1).
        yield return GuideBlockPlacement(miniGame, grid, "Sq", new Vector2Int(1, 1));

        // 13-4: 점수가 반영된 뒤(GuideBlockPlacement 안의 OnAnyBlockPlaced가 이미 UpdateScore까지 완료) ScorePanel 강조 + 대사 2줄.
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen134 = _highlighter.CurrentGeneration;

        yield return _highlighter.BeginHighlight(scorePanelRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step13_4, step13_4Position);

        yield return _highlighter.Hide(gen134);

        // 이어서 T_U — b1.png 로컬 (2,2),(3,1),(3,2),(3,3)이 목표 자리. T_U 셰이프 자체 좌표(0,1)(1,0)(1,1)(1,2)
        // 기준으로 역산하면 anchor=(3,2)일 때 물리 좌표 (3,3)(4,2)(4,3)(4,4)로 정확히 매핑된다.
        yield return GuideBlockPlacement(miniGame, grid, "T_U", new Vector2Int(3, 2));

        OnboardingState.MarkTutorial13_4Done();
    }

    // 지정한 이름의 블록을 강조 → 드래그 시작 즉시 강조 해제 → anchor 자리에만 배치 가능하도록 강제 +
    // 목표 위치 마커 표시 → 실제로 그 자리에 놓일 때까지 대기. 13-3(Sq)/T_U 등 여러 블록에서 재사용.
    IEnumerator GuideBlockPlacement(CreativityGameUI miniGame, CreativityGameGridUI grid, string shapeName, Vector2Int anchor)
    {
        var block = miniGame.FindActiveBlockByShapeName(shapeName);
        if (block == null)
        {
            Debug.LogWarning($"[TutorialController] GuideBlockPlacement: '{shapeName}' 블록을 못 찾음 — 스킵");
            yield break;
        }

        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen = _highlighter.CurrentGeneration;

        grid.SetForcedAnchor(block.Shape, anchor);
        grid.ShowTargetMarker(block.Shape, anchor, block.Color);

        yield return _highlighter.BeginHighlight((RectTransform)block.transform);

        // 손가락 힌트 — 블록이 원래 있던 자리(하이라이트 중앙)에서 목표 자리(TargetMarker 중앙)까지
        // 왕복 루프시켜 "이걸 여기로 끌어다 놓으라"는 걸 시각적으로 안내. 드래그 시작하면 바로 정지.
        StartDragHint((RectTransform)block.transform, grid.GetForcedAnchorWorldCenter());

        bool dragStarted = false;
        System.Action onDragStarted = () => dragStarted = true;
        block.OnDragStarted += onDragStarted;
        while (!dragStarted) yield return null;
        block.OnDragStarted -= onDragStarted;

        StopDragHint();
        yield return _highlighter.Hide(gen);

        // 표시된 자리에 실제로 놓일 때까지 대기(그 외 위치/다른 블록은 SetForcedAnchor에 의해 전부 거부됨).
        bool placed = false;
        System.Action<CreativityGameBlockUI> onPlaced = b => { if (b == block) placed = true; };
        miniGame.OnAnyBlockPlaced += onPlaced;
        while (!placed) yield return null;
        miniGame.OnAnyBlockPlaced -= onPlaced;

        grid.ClearForcedAnchor();
        grid.HideTargetMarker();
    }

    // ── 손가락 드래그 힌트 (GuideBlockPlacement 전용) ──────────────────────────
    // fromRect(블록이 원래 있던 자리) 중앙 ↔ toWorldPos(목표 자리 중앙)를 왕복 루프.
    // 서로 다른 부모(트레이/그리드) 밑이라 World 좌표로 직접 옮긴다 — 같은 Canvas 안이면 문제 없음.
    void StartDragHint(RectTransform fromRect, Vector3 toWorldPos)
    {
        if (dragHintFinger == null || fromRect == null) return;
        StopDragHint();

        dragHintFinger.gameObject.SetActive(true);
        dragHintFinger.position = GetRectWorldCenter(fromRect);
        dragHintFinger.DOKill();
        dragHintFinger.DOMove(toWorldPos, dragHintLoopDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Restart) // 왕복(Yoyo) 아님 — 끝나면 시작점으로 즉시 복귀 후 다시 출발
            .SetUpdate(true); // BeginDimTimeStop으로 시간이 멈춰있어도 계속 움직이게
    }

    void StopDragHint()
    {
        if (dragHintFinger == null) return;
        dragHintFinger.DOKill();
        dragHintFinger.gameObject.SetActive(false);
    }

    static Vector3 GetRectWorldCenter(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    // ── 13-5 (DevelopmentManager.ShowCreativityGame — 디버깅 단계 시작 직후, 첫 프로젝트 한정) ──
    public IEnumerator PlayTutorial13_5()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // 디버깅이 이미 시작(StartTime)된 뒤라 여기서 직접 다시 정지
        yield return _highlighter.Show();
        int gen135 = _highlighter.CurrentGeneration;

        // 강조 없음 — 대사만.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step13_5, step13_5Position);

        yield return _highlighter.Hide(gen135);
        EndDimTimeStop();
        OnboardingState.MarkTutorial13_5Done();
    }

    // ── 14-1 (DevelopmentResultUI.Show — 디버깅 끝나고 결과 패널 활성화 직후, 첫 프로젝트 한정) ──
    public IEnumerator PlayTutorial14_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // 결과 패널이 이미 StopTime 걸어둔 상태 — 페어링된 별도 잠금이라 안전(13-5와 동일 패턴)
        yield return _highlighter.Show();
        int gen141 = _highlighter.CurrentGeneration;

        // 강조 없음 — 대사만.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step14_1, step14_1Position);

        yield return _highlighter.Hide(gen141);
        EndDimTimeStop();
        OnboardingState.MarkTutorial14_1Done();
    }

    // ── 15-1/15-2 (MarketingUI.Show — 마케팅 패널 열릴 때, 첫 프로젝트 한정) ──
    public IEnumerator PlayTutorial15()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // 마케팅 패널이 이미 StopTime 걸어둔 상태 — 페어링된 별도 잠금이라 안전(13-5/14-1과 동일 패턴)
        yield return _highlighter.Show();
        int gen15 = _highlighter.CurrentGeneration;

        // 15-1 — 강조 없음, 대사만.
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step15_1, step15_1Position);

        // 15-2 — LeftPanel 두 번째 슬롯 강조 유지.
        if (marketingSecondSlotRect != null)
            yield return _highlighter.BeginHighlight(marketingSecondSlotRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step15_2, step15_2Position);

        yield return _highlighter.Hide(gen15);
        EndDimTimeStop();
        OnboardingState.MarkTutorial15Done();
    }

    // ── 16-1 (SalesUI — 판매 패널 열리고 1주차 bar 애니메이션 시작, 첫 프로젝트 한정) ──
    // TutorialPanel이 떠 있는 동안엔 BeginDimTimeStop()으로 게임 시간을 멈춘다 — SalesUI.ShowBarsSequentially의
    // bar 애니메이션/주차 전환/완료 대기 루프가 전부 이미 GameTimeManager.IsRunning을 체크해서 진행 여부를
    // 결정하므로, 이 코루틴 내부는 건드릴 필요 없이 바깥에서 시간만 멈추면 SalesUI 쪽도 자연히 같이
    // 멈춘다(=bar가 안 오르고, 판매도 완료 처리되지 않아 SalesUI가 사라지지 않는다). ⚠️ 예전엔 시간을
    // 안 멈추고 대사 진행 중에도 bar가 계속 오르게 뒀었는데, 그러면 대사가 끝나기 전에 판매가 먼저
    // 끝나버려 SalesUI가 자동으로 닫힐 수 있었고 CancelTutorial16_1IfRunning으로 우회 처리했었음 —
    // 이제 시간을 멈추므로 그 경합 자체가 발생하지 않는다(아래 메서드는 안전망으로 남겨둠).
    Coroutine _tutorial16_1Co;

    // SalesUI가 이 메서드로만 16-1을 트리거해야 코루틴 핸들을 잡아 나중에 취소할 수 있다.
    public void TriggerTutorial16_1()
    {
        _tutorial16_1Co = StartCoroutine(PlayTutorial16_1());
    }

    public IEnumerator PlayTutorial16_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop();
        yield return _highlighter.Show();
        int gen16_1 = _highlighter.CurrentGeneration;

        yield return _highlighter.BeginHighlight(chartBGRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step16_1, step16_1Position);

        yield return _highlighter.Hide(gen16_1);
        EndDimTimeStop();
        OnboardingState.MarkTutorial16_1Done();
        _tutorial16_1Co = null;
    }

    // SalesUI가 자동으로(판매 완료) 닫힐 때 호출 — 16-1 진행 중엔 이제 시간이 멈춰있어 판매가 먼저
    // 끝날 수 없으므로 사실상 호출될 일이 없는 안전망이다(만약을 대비해 남겨둠). 혹시라도 호출되면
    // 즉시 멈추고 하이라이트를 치운다. 그대로 두면 이미 닫힌 판매 패널의 chartBG를 계속 가리키게 됨.
    public void CancelTutorial16_1IfRunning()
    {
        if (_tutorial16_1Co == null) return;
        StopCoroutine(_tutorial16_1Co);
        _tutorial16_1Co = null;
        EndDimTimeStop(); // PlayTutorial16_1이 자기 EndDimTimeStop까지 못 가고 끊겼으므로 여기서 대신 풀어준다
        if (_highlighter != null) StartCoroutine(_highlighter.Hide(_highlighter.CurrentGeneration));
        OnboardingState.MarkTutorial16_1Done(); // 중간에 끊겼어도 재접속/재진입 시 다시 뜨지 않도록 완료 처리
    }

    // ── 17-1 (SalesUI.OnSalesComplete — 첫 판매가 완전히 끝난 직후, 직원 강화 메뉴 유도) ──
    public IEnumerator PlayTutorial17_1()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // 판매 종료로 시간이 이미 흐르는 상태 — 여기서 새로 멈춘다(13-5/14-1/15와 동일 패턴)
        yield return _highlighter.Show();
        int gen17_1 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_1, step17_1Position);

        // 메뉴 → 직원(상위) → 강화(하위) 순차 강조 — 1-1/5-2와 동일한 3단 메뉴 진입 패턴.
        yield return _highlighter.Highlight(menuButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 상위 메뉴 펼침
        yield return _highlighter.Highlight(employeeButton);
        yield return new WaitForSecondsRealtime(settleDelay); // 서브 메뉴 펼침
        yield return _highlighter.Highlight(trainingMenuButton);

        yield return _highlighter.Hide(gen17_1);
        EndDimTimeStop();
        OnboardingState.MarkTutorial17_1Done();
    }

    // ── 17-2~17-5 (EmployeeListUI.OpenListForEnhance — 강화 패널 첫 진입, 강화 4회 체험) ──
    // 6-1(DispatchPanelUI)과 동일한 이유로 별도 BeginDimTimeStop을 쓰지 않는다 — OpenListForEnhance가
    // 이미 GameTimeManager.StopTime()을 호출해 모달 자체가 시간을 정지시키므로, 패널이 실제로 닫힐 때
    // (EmployeeListUI.OnClickClose의 StartTime())가 정상적인 재개 시점이다.
    public IEnumerator PlayTutorial17_2()
    {
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen17_2 = _highlighter.CurrentGeneration;

        // 17-5 이전엔 닫기 버튼을 숨겨 강화 체험 도중 이탈하지 못하게 한다.
        if (trainingCloseButton != null) trainingCloseButton.gameObject.SetActive(false);
        // TrainingPanel 진입 시점부터 17-6이 끝날 때까지 뒤로가기 버튼도 비활성화 — 리스트로
        // 빠져나가 이 세션(17-2~17-6) 도중에 이탈하지 못하게 한다.
        if (trainingBackButton != null) trainingBackButton.gameObject.SetActive(false);

        // 17-2 — EmployeeTrainingPanel 강조, 강화하면 능력치+연봉 함께 오른다는 안내.
        yield return _highlighter.BeginHighlight(employeeTrainingPanelRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_2, step17_2Position);

        // 17-3 — 대사를 먼저 보여준 뒤 enhanceBtn을 강조하고 "실제로 클릭할 때까지" 유지한다(고정
        // 지연으로 먼저 없애버리면 누르기도 전에 사라짐). 클릭되면 Highlight()가 곧장 반환되고 바로
        // 하이라이트/딤을 치워, 이후 나머지 클릭은 유저가 원래 방식대로 자유롭게 진행한다. 표시 확률
        // (EmployeeEnhancement.GetRates)은 그대로 진짜 값을 보여주고, ForceSuccessRemaining으로
        // 실제 롤만 4회(첫 클릭 포함) 강제 성공시킨다(유저 입장에선 그냥 4번 다 성공).
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_3, step17_3Position);

        EmployeeEnhancement.ForceSuccessRemaining = 4;
        yield return _highlighter.Highlight(trainingEnhanceButton); // 클릭 시 자동으로 하이라이트 해제
        yield return _highlighter.Hide(gen17_2); // 17-4가 뜨기 전까지 딤도 완전히 제거

        while (EmployeeEnhancement.ForceSuccessRemaining > 0)
            yield return null;

        // 4번째 강화의 결과창(TrainingSuccessPanel)이 닫힐 때까지 대기 — 그 위에 17-4 하이라이트가
        // 겹쳐 뜨지 않게 한다. OnClickEnhance가 EnhanceOnce 직후 동기적으로 결과창을 띄우므로,
        // 이 시점엔 이미 활성화돼 있고 유저가 ConfirmBtn을 눌러야 꺼진다.
        if (TrainingPanelUI.Instance != null && TrainingPanelUI.Instance.trainingResultPanel != null)
            yield return new WaitUntil(() => !TrainingPanelUI.Instance.trainingResultPanel.activeSelf);

        // 17-4 — +4 뱃지(enhancementPanel) 강조, 4연속 성공에 대한 반응. 새 세션으로 다시 Show().
        yield return _highlighter.Show();
        int gen17_4 = _highlighter.CurrentGeneration;
        yield return _highlighter.BeginHighlight(trainingEnhancementPanelRect);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_4, step17_4Position);

        // 17-5 — 대사를 먼저 출력한 뒤 닫기 버튼을 복구 + 강조(클릭 시 패널이 실제로 닫히며 시간도 재개됨).
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_5, step17_5Position);
        if (trainingCloseButton != null) trainingCloseButton.gameObject.SetActive(true);

        // enhancementPanel 자리에서 슬라이드해오는 기존 흐름은 유지하되, 버튼 자체가 커졌다 작아졌다
        // 하는 스케일 펄스를 얹어서 멀리 떨어진 작은 CloseBtn도 눈에 잘 띄게 한다.
        Coroutine closeBtnPulseCo = null;
        Vector3 closeBtnBaseScale = Vector3.one;
        if (trainingCloseButton != null)
        {
            closeBtnBaseScale = trainingCloseButton.transform.localScale;
            closeBtnPulseCo = StartCoroutine(PulseScale(trainingCloseButton.transform, closeBtnBaseScale));
        }
        yield return _highlighter.Highlight(trainingCloseButton);
        if (closeBtnPulseCo != null)
        {
            StopCoroutine(closeBtnPulseCo);
            trainingCloseButton.transform.localScale = closeBtnBaseScale;
        }

        yield return _highlighter.Hide(gen17_4);

        // 17-6과 그 뒤의 저장 flush는 별도 메서드로 분리 — 재접속으로 17-6 이전에 끊긴 경우 Start()가
        // 17-1부터 재생하고, 17-6까지 실제로 끝나 저장까지 된 뒤(=여기 도달) 상인 방문~상점(17-7)에서
        // 끊긴 경우엔 Start()가 이 메서드만 다시 불러 재개한다.
        yield return PlayTutorial17_6();
    }

    // ── 17-6 (상인 소환 + 대사) — PlayTutorial17_2 끝에서 이어지거나, 재접속 시 Start()가 단독 재생 ──
    public IEnumerator PlayTutorial17_6()
    {
        // 재접속 직후(Start()의 needStep17_7Shop 분기)엔 씬이 아직 완전히 안정되지 않은 상태 —
        // 게임 부팅 자체가 로딩 완료 후 시간을 재개시키는 자기 자신의 콜을 아직 안 날린 시점일 수
        // 있다. 그 상태에서 우리가 먼저 BeginDimTimeStop을 걸면, 뒤늦게 도착하는 그 "부팅 재개" 호출이
        // 우리 정지를 덮어써버려(ForceStartTime류 클로버, [[feedback_forcestarttime_clobber]] 참고)
        // 패널/하이라이트는 떠 있는데 시간은 실제로 안 멈춘 채 상인이 계속 움직이는 버그가 생긴다.
        // 씬이 실제로 "정상 진행 중(IsRunning)" 상태로 안정된 뒤에야 우리 시퀀스를 시작해야 안전하다.
        yield return new WaitUntil(() => GameTimeManager.Instance != null && GameTimeManager.Instance.IsRunning);

        // 강화 패널이 닫히자마자 곧장 상인을 소환(패널 닫힘으로 시간은 이미 재개된 상태라 걸어들어오는
        // 게 바로 보임)하고, 1초 뒤에 "마침 상인이 왔다"는 대사(강조 없음)를 재생한다. 튜토리얼 중엔
        // MerchantManager.OnTimeChanged의 자동 방문을 꺼뒀으므로(RunStateManager.IsTutorial 가드) 여기서
        // TestVisit()으로 직접 불러온다. 진열 순서 고정 — 커피 → 에너지드링크 → 각성의 물약.
        MerchantManager.Instance?.TestVisit(new List<string> { "coffee", "energyDrink", "awaken" });
        yield return new WaitForSecondsRealtime(1f);

        EnsureHighlighter();
        BeginDimTimeStop();
        yield return _highlighter.Show();
        int gen17_6 = _highlighter.CurrentGeneration;
        var merchant = MerchantManager.Instance != null ? MerchantManager.Instance.ActiveMerchant : null;
        if (merchant != null)
            yield return _highlighter.BeginHighlightWorld(merchant.transform);
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_6, step17_6Position);
        yield return _highlighter.Hide(gen17_6);
        EndDimTimeStop();

        // 17-6이 끝났으니 뒤로가기 버튼 복구 — 다음 번 TrainingPanel 진입(일반 플레이)부터는 정상 동작.
        if (trainingBackButton != null) trainingBackButton.gameObject.SetActive(true);

        // 17-2~17-6 내내 미뤄뒀던 저장(강화 4회 분 골드+능력치, TrainingPanelUI.OnClickEnhance의
        // deferSave)을 여기서 한 번에 반영 — 이 지점부터는 재접속해도 17-1로 되돌아가지 않는다.
        MoneyManager.Instance?.SaveMoney();
        EmployeeManager.Instance?.SaveAllEmployees();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        OnboardingState.MarkTutorial17_2Done();
    }

    // ── 17-7~17-9 (MerchantShopPanelUI.Open — 상인 상점이 처음 열릴 때 시작하는 전체 세션) ──
    // 세 단계(상점/사용/마무리 대사)로 나뉜다 — 상점 닫기(구매 커밋)와 아이템 사용(소비 커밋)이 각각
    // 실제 서버 저장 지점이라, 재접속 시 이미 커밋된 지점 이전으로는 절대 되돌아가면 안 된다(중복
    // 구매/중복 사용 방지). Tutorial17_7ShopDone/Tutorial17_8UsedDone 서브 플래그가 그 두 지점을
    // 각각 기록하고, TutorialController.Start()가 그 조합에 맞는 단계부터 정확히 재개한다.
    public IEnumerator PlayTutorial17_7()
    {
        yield return PlayTutorial17_7Shop();
        yield return PlayTutorial17_8Use();
        yield return PlayTutorial17_9Wrap();
    }

    // ── 17-7 (상점 대사 + 구매 + 닫기) — 자연 진입(MerchantShopPanelUI.Open) 또는 재접속 시 Start() ──
    // 상점을 열기 전 MerchantManager.ShowPrompt가 이미 GameTimeManager.StopTime()을 호출해뒀으므로
    // (6-1/17-2와 동일 패턴) 별도 BeginDimTimeStop 없이, 상점이 닫힐 때(OnShopClosed의 StartTime())
    // 자연스럽게 재개되게 둔다.
    public IEnumerator PlayTutorial17_7Shop()
    {
        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen17_7 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_7, step17_7Position);

        yield return _highlighter.Highlight(merchantConfirmButton); // 구매 1회
        yield return _highlighter.Highlight(merchantCloseButton);   // 상점 닫기 — 이 클릭에서 MerchantManager.OnShopClosed가 구매를 실제로 서버에 저장

        yield return _highlighter.Hide(gen17_7);

        // 닫기 클릭이 이미 처리된 뒤(구매 커밋 완료) — 재접속해도 여기부터는 다시 상인을 부르지 않는다.
        OnboardingState.MarkTutorial17_7ShopDone();
    }

    // ── 17-8 (아이템 사용 체험) — PlayTutorial17_7Shop 다음에 이어지거나, 재접속 시 Start()가 단독 재생 ──
    public IEnumerator PlayTutorial17_8Use()
    {
        EnsureHighlighter();
        // 17-8 — 상점이 닫히며 이미 시간이 재개된 상태(또는 재접속 직후 정상 진행 중) → 여기서 새로
        // 시간정지 걸고 계속 이어간다.
        BeginDimTimeStop();
        yield return _highlighter.Show();
        int gen17_8 = _highlighter.CurrentGeneration;

        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_8, step17_8Position);

        // desk_01 직원 강조 + 클릭(=카드 오픈) 대기 — 10-1과 동일 패턴, step10_1DeskId 필드 공유.
        var deskChar = OfficeManager.Instance?.GetCharacterAtDesk(step10_1DeskId);
        string deskEmpId = deskChar != null ? deskChar.employeeId : null;
        yield return _highlighter.BeginHighlightWorld(deskChar != null ? deskChar.transform : null);

        bool cardShown = false;
        System.Action<string> onCardShown = id => { if (id == deskEmpId) cardShown = true; };
        if (EmployeeCardUI.Instance != null) EmployeeCardUI.Instance.OnCardShown += onCardShown;
        while (!cardShown) yield return null;
        if (EmployeeCardUI.Instance != null) EmployeeCardUI.Instance.OnCardShown -= onCardShown;

        // 아이템/강화 버튼 잠금을 여기서 해제하고, 이미 열려있는 카드의 잠금 UI를 Show() 재호출 없이
        // 즉시 재적용한다.
        OnboardingState.MarkTutorial17_8UnlockDone();
        EmployeeCardUI.Instance?.ApplyItemTrainingLock();

        // itemUseBtn 강조 + 클릭 대기(=EmployeeCardUI.OnClickItemButton → ItemPanel 오픈). ItemPanel이
        // 열리자마자 CloseBtn으로 바로 나가버리지 못하게 미리 비활성화.
        if (itemPanelCloseButton != null) itemPanelCloseButton.gameObject.SetActive(false);
        yield return _highlighter.Highlight(EmployeeCardUI.Instance != null ? EmployeeCardUI.Instance.itemUseBtn : null);

        // ItemPanel이 열린 뒤로는 강조/딤을 완전히 치워서, 유저가 아무 안내 없이 완전히 자유롭게
        // 아이템(슬롯 선택 → 상세창 → 사용)을 골라 진행하게 둔다.
        yield return _highlighter.Hide(gen17_8);

        // 사용하면 ItemManager.UseItem이 AlertUI.ShowPortrait를 띄운다 — 이 알림이 뜨는 시점에 이미
        // 소비/효과가 서버에 커밋 완료된 상태이므로, 여기서 바로 마킹한다. ⚠️ 알림이 자동으로 닫힐
        // 때까지 기다렸다가 마킹하면, 그 몇 초 사이(닫히기 전)에 종료할 경우 실제로는 아이템을 다 쓰고도
        // 플래그가 저장되지 않아 재접속 시 17-8이 다시 뜨는 버그가 있었음 — 커밋 확인 즉시 마킹, 알림이
        // 닫히길 기다리는 건 순수 UI 정리 목적으로만 분리.
        if (AlertUI.Instance != null && AlertUI.Instance.portraitPanel != null)
        {
            yield return new WaitUntil(() => AlertUI.Instance.portraitPanel.activeSelf);
            OnboardingState.MarkTutorial17_8UsedDone();
            yield return new WaitUntil(() => !AlertUI.Instance.portraitPanel.activeSelf);
        }
        else
        {
            OnboardingState.MarkTutorial17_8UsedDone();
        }

        EmployeeCardUI.Instance?.Hide(); // 콜백으로 자동 재오픈된 카드 정리
        if (itemPanelCloseButton != null) itemPanelCloseButton.gameObject.SetActive(true);
    }

    // ── 17-9 (마무리 대사, 강조 없음) — PlayTutorial17_8Use 다음에 이어지거나, 재접속 시 Start()가 단독 재생 ──
    public IEnumerator PlayTutorial17_9Wrap()
    {
        EnsureHighlighter();
        // 17-8에서 이어지면 이미 시간정지 상태(중복 호출은 무해) — 단독 재생(재접속)이면 여기서 새로 건다.
        BeginDimTimeStop();
        yield return _highlighter.Show();
        int gen17_9 = _highlighter.CurrentGeneration;
        if (TutorialPanelUI.Instance != null)
        {
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_9_1, step17_9_1Position);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step17_9_2, step17_9_2Position);
        }

        yield return _highlighter.Hide(gen17_9);
        EndDimTimeStop();
        OnboardingState.MarkTutorial17_7Done();

        if (!OnboardingState.Tutorial18Done)
            yield return PlayTutorial18();
    }

    // ── 18-1~18-4 (지출/연봉·사무실비/파산 경고) — PlayTutorial17_9Wrap 다음에 자동으로 이어지거나,
    // 재접속 시 Start()가 단독 재생. 중간에 서버 커밋 지점이 전혀 없는 순수 대사 구간이라 5-1~6-2/
    // 17-1~17-6과 동일한 all-or-nothing 방식 — 4줄 중 어디서 끊기든 재접속하면 18-1부터 다시.
    public IEnumerator PlayTutorial18()
    {
        EnsureHighlighter();
        // 17-9에서 이어지면 이미 시간정지 상태(중복 호출은 무해) — 단독 재생(재접속)이면 여기서 새로 건다.
        BeginDimTimeStop();
        // 18-4~20이 끝날 때까지(DevelopmentManager.StartDeveloping에서 UnlockTime) 시간 재개를
        // 완전히 봉인 — StopTime()의 카운터 방식만으로는 코드 곳곳의 ForceStartTime() 호출 중 하나가
        // 이 레이어까지 통째로 날려버릴 수 있어서(실제로 겪은 버그) 하드락으로 이중 방어한다.
        GameTimeManager.Instance?.LockTime();
        yield return _highlighter.Show();
        int gen18 = _highlighter.CurrentGeneration;
        if (TutorialPanelUI.Instance != null)
        {
            // 18-1 — MoneyPanel 강조, 오늘 돈을 많이 썼다는 반응.
            yield return _highlighter.BeginHighlight(moneyPanelRect);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step18_1, step18_1Position);

            // 18-2~18-3 — Salary_AnnualPanel로 강조 이동, 연봉/사무실비 지출 + 파산 경고를 이어서.
            yield return _highlighter.BeginHighlight(salaryAnnualPanelRect);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step18_2, step18_2Position);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step18_3, step18_3Position);

            // 18-4 — 강조 없이 대사만, 화면 톤다운만 유지.
            _highlighter.CollapseAndResetOrigin();
            yield return TutorialPanelUI.Instance.PlayStepGroup(step18_4, step18_4Position);
        }

        yield return _highlighter.Hide(gen18);
        OnboardingState.MarkTutorial18Done();

        if (!OnboardingState.Tutorial19Done)
            yield return PlayTutorial19();
    }

    // ── 19-1 (마무리 대사 — "이제 혼자 해보라") — PlayTutorial18 다음에 자동으로 이어지거나, 재접속
    // 시 Start()가 단독 재생. 강조 없음. ⚠️ 19-1이 끝나도 시간은 재개하지 않는다(사용자 지시 — 다음
    // 단계가 정해지면 그때 알려줄 예정) — 18-4 종료 시점부터 이어져온 "시간은 멈춰있지만 직원은 계속
    // 움직이는" 상태(AllowMovementWhileStopped)를 그대로 유지한 채 마무리한다.
    public IEnumerator PlayTutorial19()
    {
        EnsureHighlighter();
        BeginDimTimeStop(); // 18-4에서 이어지면 이미 시간정지 상태(중복 호출 무해) — 단독 재생(재접속)이면 새로 건다.
        // 18에서 이어지면 이미 잠겨있음(중복 호출 무해) — Tutorial18Done만 true인 채 재접속해 이
        // 코루틴이 단독으로 재생되는 경로에서는 18의 LockTime()을 안 거치므로 여기서도 걸어준다.
        GameTimeManager.Instance?.LockTime();
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.AllowMovementWhileStopped = true;
        yield return _highlighter.Show();
        int gen19 = _highlighter.CurrentGeneration;
        if (TutorialPanelUI.Instance != null)
            yield return TutorialPanelUI.Instance.PlayStepGroup(step19_1, step19_1Position);

        yield return _highlighter.Hide(gen19);

        // EndDimTimeStop()(StartTime 호출)을 쓰지 않고 TutorialController 자체 북키핑만 정리 —
        // GameTimeManager.StopTime() 레이어와 AllowMovementWhileStopped는 그대로 유지된다.
        // ⚠️ OnboardingState.TutorialActive는 false로 만들지 않고 true로 유지한다 — MenuController.
        // OnTimeStopChanged가 "시간이 멈춰있는데 TutorialActive가 아니면(=다른 모달이 막고 있는 걸로
        // 간주) 메뉴 버튼을 숨긴다"는 로직이라, 여기서 false로 두면 메뉴가 다시 숨겨져 버린다(실제
        // 겪은 버그). 20이 진짜로 끝나 시간이 재개될 때(DevelopmentManager.StartDeveloping)
        // TutorialActive도 함께 false로 정리한다.
        _timeStopped = false;

        OnboardingState.MarkTutorial19Done();
    }

    // ── 20-1~20-2 (2번째 프로젝트 기획팀장점수 강제 burst 직후 반응 대사, 강조 없음) ──
    // DevelopmentManager.BuildAndShowLeaderScore(완료 프로젝트 1개=2번째 프로젝트 한정)가 4회차 대기
    // 상태로 들어가는 시점에 이 코루틴을 직접 시작시킨다(7/8/9단계와 동일하게 Start() 체인이 아니라
    // 자연 발생 지점에서 직접 호출하는 방식). LeaderScoreUI.OnRoundsVisualComplete(4회차/burst 연출이
    // 화면상 다 끝난 시점) 신호를 기다렸다가 대사만 보여준다 — 이 시점엔 이미 BuildAndShowLeaderScore가
    // GameTimeManager.StopTime()을 걸어둔 상태라 별도 BeginDimTimeStop 불필요(7-6과 동일 패턴).
    public IEnumerator PlayTutorial20()
    {
        bool roundsDone = false;
        System.Action onRoundsDone = () => roundsDone = true;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete += onRoundsDone;
        while (!roundsDone) yield return null;
        if (LeaderScoreUI.Instance != null) LeaderScoreUI.Instance.OnRoundsVisualComplete -= onRoundsDone;

        EnsureHighlighter();
        yield return _highlighter.Show();
        int gen20 = _highlighter.CurrentGeneration;
        if (TutorialPanelUI.Instance != null)
        {
            yield return TutorialPanelUI.Instance.PlayStepGroup(step20_1, step20_1Position);
            yield return TutorialPanelUI.Instance.PlayStepGroup(step20_2, step20_2Position);
        }

        yield return _highlighter.Hide(gen20);
        OnboardingState.MarkTutorial20Done();
    }

    // 대상이 baseScale 기준으로 커졌다 작아졌다를 무한 반복 — 17-5의 CloseBtn처럼 작고 눈에 안 띄는
    // 버튼을 강조할 때, dim 구멍 펄스만으로는 부족해서 버튼 자체 스케일도 함께 흔든다.
    IEnumerator PulseScale(Transform target, Vector3 baseScale, float amplitude = 0.12f, float speed = 4f)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * speed;
            target.localScale = baseScale * (1f + amplitude * Mathf.Sin(t));
            yield return null;
        }
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
