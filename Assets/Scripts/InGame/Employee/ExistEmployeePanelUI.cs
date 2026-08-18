using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ConfirmHirePanel/ExistEmployeePanel — 채용 확정 화면에서 이력서 후보가 이미 보유 중인 직원과
/// 동일할 때(HiringUI._conflictingOwned) 표시되는 보유 직원 요약 카드. 초상화 + 등급뱃지 + 능력치만 표시.
/// </summary>
public class ExistEmployeePanelUI : MonoBehaviour
{
    [Header("Portrait")]
    public Image portraitImage;

    [Header("Badge")]
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI potentialText;
    public TextMeshProUGUI gradeText;
    public Image gradePanel;                // 등급별 이미지 (색 대신 sprite 교체 — gradePanelC)
    public GradeSpriteSet gradeSpriteSet;   // 공용 등급 스프라이트 세트

    [Header("Abilities")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Salary")]
    public TextMeshProUGUI salaryValueText;

    public void Setup(EmployeeData owned)
    {
        if (owned == null) return;

        if (portraitImage != null && !string.IsNullOrEmpty(owned.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/Mini/{owned.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }

        if (enhancementText != null) enhancementText.text = $"+{owned.enhancementLevel}";
        if (potentialText != null)   potentialText.text   = owned.PotentialToString();
        if (gradeText != null)       gradeText.text       = owned.GradeToString();
        GradeSpriteSet.Apply(gradePanel, gradeSpriteSet, owned.grade);

        if (planningText != null)   planningText.text   = owned.PlanningText();
        if (developText != null)    developText.text    = owned.DevelopText();
        if (artText != null)        artText.text         = owned.ArtText();
        if (creativityText != null) creativityText.text = owned.CreativityText();

        if (salaryValueText != null) salaryValueText.text = $"{owned.salary:N0} G";
    }
}
