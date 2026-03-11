using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI perfectionSkillText;
    public TextMeshProUGUI salaryText;
    public Button selectButton;

    public void Setup(EmployeeData data, HiringUI hiringUI)
    {
        nameText.text           = data.employeeName;
        roleText.text           = data.RoleToString();
        gradeText.text          = data.GradeToString();
        developSkillText.text   = data.DevelopRangeText();
        planningSkillText.text  = data.PlanningRangeText();
        artSkillText.text       = data.ArtRangeText();
        perfectionSkillText.text= data.PerfectionRangeText();
        salaryText.text = data.SalaryRangeText();

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => hiringUI.OnSelectEmployee(data));
    }
}