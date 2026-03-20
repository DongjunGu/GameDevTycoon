public enum TechCategory
{
    EmployeeSatisfaction,  // 직원관리 - 만족도 유지
    EmployeeEfficiency,    // 직원관리 - 효율성 극대화
    GenrePlatform,         // 기술연구 - 장르/플랫폼
    Novelty,               // 기술연구 - 참신함
    Utility                // 유틸리티
}

[System.Serializable]
public class TechNodeData
{
    public string   id;
    public string   name;
    public TechCategory category;
    public int      order;        // 카테고리 내 순서 (1부터)
    public string   prerequisiteId; // 선행 테크 ID (없으면 "")
    public int      cost;
    public bool     isUnlocked;
}