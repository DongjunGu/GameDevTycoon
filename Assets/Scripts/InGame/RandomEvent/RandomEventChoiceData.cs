using System;
using System.Collections.Generic;

public class RandomEventChoiceOption
{
    public string buttonLabel;

    // null이면 원래 값 유지
    public string resultPortraitId;  // 선택 시 portrait1 교체 (portrait2 숨김)
    public string resultPortraitId2; // 선택 시 portrait2 표시 + portrait1 숨김
    public string resultTitle;
    public string resultDescription;
    public List<string> resultDescriptions = new List<string>(); // 복수일 때 랜덤 선택
    public string resultSystemMessage;

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
    public string description;
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
}
