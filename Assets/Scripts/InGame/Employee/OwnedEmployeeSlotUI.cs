using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OwnedEmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI stateText;
    public Button fireButton;

    private EmployeeData _data;
    private EmployeeListUI _listUI;

    public void Setup(EmployeeData data, EmployeeListUI listUI)
    {
        nameText.text = data.employeeName;
        roleText.text = data.RoleToString();
        gradeText.text = data.GradeToString();
        developSkillText.text = data.DevelopText();  // 확정 수치
        planningSkillText.text = data.PlanningText(); // 확정 수치
        artSkillText.text = data.ArtText();      // 확정 수치
                                                 //stateText.text         = data.StateToString();

        fireButton.onClick.RemoveAllListeners();
        fireButton.onClick.AddListener(() => listUI.OnClickFire(data));
    }
}