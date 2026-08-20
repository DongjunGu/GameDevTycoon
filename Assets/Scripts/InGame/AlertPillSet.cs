using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AlertUI 통합 문구용 pill 이미지 묶음(카테고리별 1개씩) — b1~b8 목업 기준.
/// 각 카테고리는 "아이콘+고정 라벨이 하나로 합쳐진 이미지" 스프라이트 하나를 가리킨다(Money/Item처럼
/// 라벨이 없거나 가변인 경우도 아이콘 스프라이트 하나로 동일하게 취급). GradeSpriteSet/CharacterNamePanelSet과
/// 동일하게 Sprite를 직접 담는 얕은 SO — TMP Sprite Asset을 에디터에서 미리 만들어둘 필요 없이 그냥
/// Sprite만 꽂으면 됨(AlertUI.GetOrBuildSpriteAsset이 카테고리마다 런타임에 전용 TMP_SpriteAsset을 조립).
/// 아직 이미지가 없는 카테고리는 엔트리를 비워두면 됨 — AlertUI가 null이면 조용히 생략하고 텍스트만
/// 표시하므로 미완성 상태에서도 안전하게 동작한다.
/// </summary>
[CreateAssetMenu(fileName = "AlertPillSet", menuName = "GameDevTycoon/Alert Pill Set")]
public class AlertPillSet : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public AlertPillCategory category;
        public Sprite sprite;
    }

    public List<Entry> entries = new();

    public Sprite Get(AlertPillCategory category)
    {
        if (category == AlertPillCategory.None) return null;
        foreach (var e in entries)
            if (e.category == category)
                return e.sprite;
        return null;
    }
}
