using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 숙련도(MasteryTier)별 아이콘 묶음. GradeSpriteSet/RoleIconSet과 동일 관례 —
/// 여러 UI(장르 선택 패널 등)가 이 에셋 하나를 공유해 등급별 이미지를 표시.
/// </summary>
[CreateAssetMenu(fileName = "MasterySpriteSet", menuName = "GameDevTycoon/Mastery Sprite Set")]
public class MasterySpriteSet : ScriptableObject
{
    public Sprite novice;
    public Sprite amateur;
    public Sprite pro;
    public Sprite veteran;
    public Sprite master;

    public Sprite Get(MasteryTier tier) => tier switch
    {
        MasteryTier.Amateur => amateur,
        MasteryTier.Pro     => pro,
        MasteryTier.Veteran => veteran,
        MasteryTier.Master  => master,
        _                   => novice,
    };

    // 숙련도 아이콘을 Image 에 적용. set/스프라이트 누락 시 변경 없음(기존 유지).
    public static void Apply(Image target, MasterySpriteSet set, MasteryTier tier)
    {
        if (target == null || set == null) return;
        var s = set.Get(tier);
        if (s != null) target.sprite = s;
    }
}
