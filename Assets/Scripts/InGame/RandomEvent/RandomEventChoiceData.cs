using System;
using System.Collections.Generic;

public class RandomEventChoiceOption
{
    public string buttonLabel;

    // null이면 원래 값 유지
    public string resultTitle;
    public string resultDescription;
    public string resultSystemMessage;

    public Action onChoose;
}

public class RandomEventChoiceData
{
    public RandomEventType type;
    public string title;
    public string description;
    public string portraitId;

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

    // Show() 직전 호출 — 동적 내용 세팅용
    public Action onSetup;
}
