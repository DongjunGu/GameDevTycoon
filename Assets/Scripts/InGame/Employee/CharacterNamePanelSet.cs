using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터(portraitId)별 다이얼로그 이름표(NamePanel) 이미지 묶음. 목록에 없는 portraitId는
/// defaultSprite로 폴백 — 새 캐릭터 전용 이름표를 추가할 때 코드 변경 없이 엔트리만 추가하면 된다.
/// </summary>
[CreateAssetMenu(fileName = "CharacterNamePanelSet", menuName = "GameDevTycoon/Character Name Panel Set")]
public class CharacterNamePanelSet : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string portraitId;
        public Sprite sprite;
    }

    [Tooltip("목록에 없는 portraitId일 때 쓸 기본 이름표")]
    public Sprite defaultSprite;
    public List<Entry> entries = new();

    public Sprite Get(string portraitId)
    {
        if (!string.IsNullOrEmpty(portraitId))
        {
            foreach (var e in entries)
                if (e.portraitId == portraitId)
                    return e.sprite != null ? e.sprite : defaultSprite;
        }
        return defaultSprite;
    }

    // NamePanel 이미지에 적용. set/스프라이트 누락 시 변경 없음(기존 유지).
    public static void Apply(Image target, CharacterNamePanelSet set, string portraitId)
    {
        if (target == null || set == null) return;
        var s = set.Get(portraitId);
        if (s != null) target.sprite = s;
    }
}
