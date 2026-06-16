using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 만족도 슬라이더 Fill 에 구간별로 적용할 sprite 묶음 (색 대신 이미지 교체).
/// 여러 직원 UI(EmployeeCardUI / EmployeeSatisfactionSlider / EmployeeSlotListUI /
/// EmployeeResumePanel / EmployeeListUI)가 같은 에셋 하나를 참조해 일관성을 유지한다.
///   81~100 / 61~80 / 60 미만
/// </summary>
[CreateAssetMenu(fileName = "SatisfactionFillSet", menuName = "GameDevTycoon/Satisfaction Fill Set")]
public class SatisfactionFillSet : ScriptableObject
{
    public Sprite high;     // 81~100
    public Sprite mid;      // 61~80
    public Sprite low;      // 60 미만

    public Sprite Get(int satisfaction)
    {
        if (satisfaction >= 81) return high;
        if (satisfaction >= 61) return mid;
        return low;
    }

    // 슬라이더 Fill(또는 명시한 fill Image)에 구간 sprite 적용.
    public static void Apply(Slider slider, SatisfactionFillSet set, int satisfaction, Image fillOverride = null)
    {
        if (set == null) return;
        var fill = fillOverride;
        if (fill == null && slider != null && slider.fillRect != null)
            fill = slider.fillRect.GetComponent<Image>();
        if (fill == null) return;

        var s = set.Get(satisfaction);
        if (s != null) fill.sprite = s;
    }
}
