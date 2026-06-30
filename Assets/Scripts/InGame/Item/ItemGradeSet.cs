using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "ItemGradeSet", menuName = "GameDev/Item Grade Set")]
public class ItemGradeSet : ScriptableObject
{
    [Tooltip("인덱스 0=1등급, 1=2등급, 2=3등급, 3=4등급")]
    public Sprite[] frames = new Sprite[4];

    public static void Apply(Image target, ItemGradeSet set, int grade)
    {
        if (target == null || set == null) return;
        int idx = Mathf.Clamp(grade - 1, 0, set.frames.Length - 1);
        var sp = set.frames[idx];
        if (sp != null) target.sprite = sp;
    }
}
