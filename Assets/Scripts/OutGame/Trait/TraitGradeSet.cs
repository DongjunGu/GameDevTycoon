using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "TraitGradeSet", menuName = "GameDev/Trait Grade Set")]
public class TraitGradeSet : ScriptableObject
{
    [Tooltip("인덱스 0=C, 1=B, 2=A, 3=S (TraitGrade enum 값과 동일)")]
    public Sprite[] sprites = new Sprite[4];

    // 스프라이트를 적용했으면 true (미지정이면 false → 호출측이 색상 폴백)
    public static bool Apply(Image target, TraitGradeSet set, TraitGrade grade)
    {
        if (target == null || set == null) return false;
        int idx = Mathf.Clamp((int)grade, 0, set.sprites.Length - 1);
        var sp = set.sprites[idx];
        if (sp == null) return false;
        target.sprite = sp;
        target.color = Color.white;
        return true;
    }
}
