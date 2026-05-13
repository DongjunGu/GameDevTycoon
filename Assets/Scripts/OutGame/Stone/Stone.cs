using System;

// 돌(Stone) — CEO 강화 수치 1세트.
// "활성 돌" 은 CEOManager 가 직접 보관(=PieceLeft 표시). 이 클래스는 "보관함(StoneManager)" 에 들어가는 돌만.
// JSON 직렬화로 뒤끝 UserStoneList 단일 row 의 stonesJson 컬럼에 저장.
[Serializable]
public class Stone
{
    public string id;
    public int planningStage;
    public int developStage;
    public int artStage;
    public int planningProgress;
    public int developProgress;
    public int artProgress;

    public Stone()
    {
        id = Guid.NewGuid().ToString();
    }

    public Stone(int ps, int ds, int aS, int pp = 0, int dp = 0, int ap = 0)
    {
        id = Guid.NewGuid().ToString();
        planningStage    = ps;
        developStage     = ds;
        artStage         = aS;
        planningProgress = pp;
        developProgress  = dp;
        artProgress      = ap;
    }

    public Stone Clone()
    {
        return new Stone
        {
            id = id,
            planningStage    = planningStage,
            developStage     = developStage,
            artStage         = artStage,
            planningProgress = planningProgress,
            developProgress  = developProgress,
            artProgress      = artProgress,
        };
    }
}

// JsonUtility 는 최상위 배열을 못 다루므로 래퍼 사용.
[Serializable]
public class StoneListWrap
{
    public System.Collections.Generic.List<Stone> stones = new();
}
