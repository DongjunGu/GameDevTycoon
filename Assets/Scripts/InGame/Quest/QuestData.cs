public enum QuestType
{
    HireEmployee,
    SurviveYears,
    TotalRevenue,
    CompleteFirstProject,  // 튜토리얼 퀘스트1 — 첫 프로젝트 판매 완료(SalesUI.OnSalesComplete)
    CompleteSecondProject, // 튜토리얼 퀘스트2 — 완료 조건 미정, 아직 UpdateProgress 호출부 없음(추후 연결)
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
    public bool isMainQuest;
    public string unlockAfter; // 이 questId 완료 시 공개 (빈 값 = 처음부터 공개)
    public bool defaultIsVisible; // 차트가 정의한 초기 isVisible — 런 리셋 시 isVisible 복귀 기준
    public string rowInDate;

    public float Progress => targetValue > 0 ? (float)currentValue / targetValue : 0f;
}