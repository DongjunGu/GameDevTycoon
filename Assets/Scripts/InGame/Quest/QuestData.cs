public enum QuestType
{
    TotalSales,      // 총 판매량 달성
    HireEmployee,    // 직원 채용
    // 추후 추가
}

[System.Serializable]
public class QuestData
{
    public string questId;       // "quest_001"
    public string title;         // "첫 판매"
    public string description;   // "게임을 총 1,000개 판매하세요"
    public QuestType type;
    public int targetValue;      // 달성 목표값
    public int rewardGold;       // 보상 골드

    // 유저 진행상황
    public int currentValue;
    public bool isCompleted;
    public bool isRewarded;
    public string rowInDate;

    public float Progress => targetValue > 0 ? (float)currentValue / targetValue : 0f;
}