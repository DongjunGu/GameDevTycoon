using System.Collections.Generic;
using UnityEngine;

// 블록 모양(CreativityGameData.BlockShape.name) 별로 적용할 셀 스프라이트를 지정하는 데이터 에셋.
// 커스텀 인스펙터(Assets/Editor/CreativityBlockSpriteConfigEditor.cs)에서 각 블록 모양을 미리보기로
// 보면서 스프라이트를 시각적으로 지정할 수 있다.
[CreateAssetMenu(fileName = "CreativityBlockSpriteConfig", menuName = "GameDevTycoon/Creativity Block Sprite Config")]
public class CreativityBlockSpriteConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string blockName;
        public Sprite[] cellSprites;
        // cellSprites 와 같은 인덱스로 대응하는 기본 회전(도). 뱀모양 자동회전(진행방향)에 추가로 더해지고,
        // 분기/고리라 자동회전이 안 되는 모양에서는 이 값이 그대로(셀 인덱스 매칭) 적용된다.
        public float[] cellRotations;
    }

    public List<Entry> entries = new();

    // blockName 에 등록된 스프라이트 배열 반환. 없거나 비어있으면 null(호출부에서 단색 fallback).
    public Sprite[] GetSprites(string blockName)
    {
        var e = FindEntry(blockName);
        return (e != null && e.cellSprites != null && e.cellSprites.Length > 0) ? e.cellSprites : null;
    }

    // blockName 에 등록된 기본 회전 배열 반환 (cellSprites 와 같은 인덱스). 없으면 null(전부 0도).
    public float[] GetRotations(string blockName)
    {
        var e = FindEntry(blockName);
        return (e != null && e.cellRotations != null && e.cellRotations.Length > 0) ? e.cellRotations : null;
    }

    Entry FindEntry(string blockName)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].blockName == blockName) return entries[i];
        return null;
    }
}
