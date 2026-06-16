using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 역할(EmployeeRole)별 직업 아이콘 묶음. 여러 직원 UI(EmployeeCardUI / EmployeeListUI /
/// EmployeeResumePanel / EmployeeSatisfactionSlider)가 같은 에셋 하나를 공유한다.
/// </summary>
[CreateAssetMenu(fileName = "RoleIconSet", menuName = "GameDevTycoon/Role Icon Set")]
public class RoleIconSet : ScriptableObject
{
    public Sprite planner;     // Planner
    public Sprite programmer;  // Programmer
    public Sprite artist;      // Artist

    public Sprite Get(EmployeeRole role) => role switch
    {
        EmployeeRole.Programmer => programmer,
        EmployeeRole.Artist     => artist,
        _                       => planner,
    };

    // 역할 아이콘을 Image 에 적용. set/스프라이트 누락 시 변경 없음(기존 유지).
    public static void Apply(Image target, RoleIconSet set, EmployeeRole role)
    {
        if (target == null || set == null) return;
        var s = set.Get(role);
        if (s != null) target.sprite = s;
    }
}
