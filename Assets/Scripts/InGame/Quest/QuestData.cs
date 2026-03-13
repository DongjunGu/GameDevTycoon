public enum QuestType
{
    TotalSales,
    HireEmployee,
}

[System.Serializable]
public class QuestData
{
    public string questId;
    public string title;
    public string description;
    public QuestType type;
    public int targetValue;
    public int rewardGold;

    public int currentValue;
    public bool isCompleted;
    public bool isRewarded;
    public bool isVisible;
    public string rowInDate;

    public float Progress => targetValue > 0 ? (float)currentValue / targetValue : 0f;
}