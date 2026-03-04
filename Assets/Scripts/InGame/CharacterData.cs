using System;

// 서버 저장/불러오기용 순수 데이터 클래스
[Serializable]
public class CharacterData
{
    public string characterId;    // 고유 ID
    public string characterName;
    public int cellX;             // 현재 위치 (셀 좌표)
    public int cellY;
    public string state;          // "Idle", "Moving", "Working" 등
    public int level;
    public long lastSavedAt;      // 마지막 저장 시각 (Unix timestamp)
    public string rowInDate;

    public CharacterData(string id, string name)
    {
        characterId = id;
        characterName = name;
        state = "Idle";
        level = 1;
    }
}