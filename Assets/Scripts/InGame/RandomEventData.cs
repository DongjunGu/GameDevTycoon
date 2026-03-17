using System;

public enum RandomEventType
{
    Blackout,   // 정전
    TeamDinner, // 회식
    // 추후 추가
}

[Serializable]
public class RandomEventData
{
    public RandomEventType type;
    public string title;
    public string description;
    public float  triggerChance; // 0~1 발동 확률
    public Action onApply;       // 실제 효과
}