using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 등급(EmployeeGrade)별 스프라이트 묶음. 테두리 프레임·등급BG 등 "등급에 따라 바뀌는 이미지"를 한 에셋에 모아
/// 여러 직원 UI(EmployeeCardUI / EmployeeSatisfactionSlider 등)가 공유한다.
/// 용도별로 에셋을 따로 만든다 (예: GradeFrameSet=Frame_*, GradeProfileBGSet=Profile_Rank_*).
/// </summary>
[CreateAssetMenu(fileName = "GradeSpriteSet", menuName = "GameDevTycoon/Grade Sprite Set")]
public class GradeSpriteSet : ScriptableObject
{
    public Sprite normal;
    public Sprite rare;
    public Sprite epic;
    public Sprite unique;
    public Sprite legendary;

    public Sprite Get(EmployeeGrade grade) => grade switch
    {
        EmployeeGrade.Rare      => rare,
        EmployeeGrade.Epic      => epic,
        EmployeeGrade.Unique    => unique,
        EmployeeGrade.Legendary => legendary,
        _                       => normal,
    };

    // 등급 스프라이트를 Image 에 적용. set/스프라이트 누락 시 변경 없음(기존 유지).
    public static void Apply(Image target, GradeSpriteSet set, EmployeeGrade grade)
    {
        if (target == null || set == null) return;
        var s = set.Get(grade);
        if (s != null) target.sprite = s;
    }
}
