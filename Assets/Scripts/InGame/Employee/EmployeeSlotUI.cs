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
    //public TextMeshProUGUI stateText;
    public Button selectButton;

    public void Setup(EmployeeData data, HiringUI hiringUI)
    {
        nameText.text = data.employeeName;
        roleText.text = data.RoleToString();
        gradeText.text = data.GradeToString();
        developSkillText.text = data.DevelopRangeText();  // 범위
        planningSkillText.text = data.PlanningRangeText(); // 범위
        artSkillText.text = data.ArtRangeText();      // 범위

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => hiringUI.OnSelectEmployee(data));
    }
}