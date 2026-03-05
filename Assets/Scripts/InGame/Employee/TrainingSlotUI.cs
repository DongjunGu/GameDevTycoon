using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrainingSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public Button selectButton;

    public void Setup(EmployeeData data, TrainingUI trainingUI)
    {
        nameText.text          = data.employeeName;
        roleText.text          = data.RoleToString();
        gradeText.text         = data.GradeToString();
        developSkillText.text  = $"개발: {data.developSkill}";
        planningSkillText.text = $"기획: {data.planningSkill}";
        artSkillText.text      = $"아트: {data.artSkill}";

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => trainingUI.OnSelectEmployee(data));
    }
}