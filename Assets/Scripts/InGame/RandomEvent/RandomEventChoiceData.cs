using System;
using System.Collections.Generic;

public class RandomEventChoiceOption
{
    public string buttonLabel;

    // true면 버튼을 표시하되 비활성(회색) 상태로 렌더 — onChoose는 호출되지 않음
    public bool disabled = false;

    // null이면 원래 값 유지
    public string resultPortraitId;  // 선택 시 portrait1 교체 (portrait2 숨김)
    public string resultPortraitId2; // 선택 시 portrait2 표시 + portrait1 숨김
    public string resultTitle;
    public string resultDescription;
    public List<string> resultDescriptions = new List<string>(); // 복수일 때 랜덤 선택
    public string resultSystemMessage;

    // ── 답변1/답변2 (신규) ─────────────────────────────────────
    // 선택 후 두 말풍선 박스에 순차 출력되는 캐릭터 대사. reply1 이 있으면 resultDescription 계열 대신
    // 이쪽을 우선 사용(box1→reply2 있으면 box2 로 화자 전환). reply1 이 비어있으면 기존 resultDescription
    // /resultDescriptions 랜덤선택 방식으로 fallback(기존 이벤트 하위호환).
    public string reply1;
    public string reply2;

    // ── 결과 팝업 (신규) ────────────────────────────────────────
    // resultPopupType: 0=미지정(기존 AlertUI1 plain, resultSystemMessage 사용) / 1=AlertUI4 / 2=AlertUI5 / 3=AlertUI6
    // resultMent1/2/3: 결과팝업멘트1/2/3 — 타입별 매핑은 RandomEventChoiceUI.ShowResultPopup 참고.
    public int    resultPopupType = 0;
    public string resultMent1;
    public string resultMent2;
    public string resultMent3;

    // 2번째 결과 팝업(선택) — 확인 시 첫 팝업 닫고 바로 이어서 하나 더 띄울 때 사용
    // (예: 승자 효과 팝업 → 확인 → 패자 효과 팝업 → 확인 → 진행 재개). resultPopupType2=0 이면 안 씀.
    public int    resultPopupType2 = 0;
    public string resultMent1_2;
    public string resultMent2_2;
    public string resultMent3_2;

    // 1차 결과 확인 후 이어서 표시할 2차 반응 (선택적)
    public string secondaryTitle;
    public string secondaryPortraitId;
    public bool   secondaryUsePortrait1; // true: 2차에서 portrait1 사용(portrait2 숨김)
    public List<string> secondaryDescriptions = new List<string>();
    public Action onSecondaryShow; // 2차 표시 직전 효과 적용용

    public Action onChoose;
}

public class RandomEventChoiceData
{
    public RandomEventType type;
    public string title;
    public string description;  // 대사1 — box1(portrait1)에서 타이핑되는 도입 대사
    public string dialogue2;    // 대사2 (신규) — 있으면 대사1 다음 box2(portrait2)로 화자 전환해 타이핑
    public string question;     // 질문 (신규) — 대사1(+대사2) 완료 후 QuestionText 에 즉시 표시
    public string portraitId;
    public string portraitId2;

    // 스케줄링용
    public float  weight      = 1f;
    public int    categoryMin = 1;
    public int    categoryMax = 4;

    // 패트롤 트리거
    public bool   requiresPatrol        = false;
    public string requiredPatrolPointId = "";

    // 동적 세팅 (onSetup에서 채움)
    public string targetEmployeeId = "";

    public List<RandomEventChoiceOption> choices = new List<RandomEventChoiceOption>();

    // onSetup에서 true로 설정하면 이벤트 전체 스킵
    public bool cancelled = false;

    // Show() 직전 호출 — 동적 내용 세팅용
    public Action onSetup;

    // confirm 클릭 시 DevelopmentManager.ResumeFromEvent() 대신 호출할 콜백 (null이면 기본 동작)
    public Action onConfirm;
}
