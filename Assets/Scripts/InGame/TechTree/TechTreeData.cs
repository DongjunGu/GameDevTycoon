public enum TechCategory
{
    Money,         // 돈
    Hiring,        // 채용
    Satisfaction,  // 만족도
    Creativity,    // 창의성
    Ad             // 광고 (CSV row 0개, UI 탭만 유지)
}

[System.Serializable]
public class TechNodeData
{
    public string       id;             // nodeId (뒤끝 예약 키 회피)
    public string       name;
    public TechCategory category;
    public string       description;    // 인게임 효과 설명 (사용자에게 보이는 문구)
    public string       logic;          // 구현 로직 메모 (디자이너 노트)
    public int          requiredPoints; // 해금 비용 (테크 포인트)
    public bool         isUnlocked;
}
