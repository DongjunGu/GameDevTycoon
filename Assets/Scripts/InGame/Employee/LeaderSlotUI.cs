using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI perfectionSkillText;
    public Button selectButton;

    public void Setup(EmployeeData data, LeaderSelectUI leaderSelectUI)
    {
        nameText.text          = data.employeeName;
        roleText.text          = data.RoleToString();
        gradeText.text         = data.GradeToString();
        developSkillText.text  = data.DevelopText();
        planningSkillText.text = data.PlanningText();
        artSkillText.text      = data.ArtText();
        perfectionSkillText.text = data.PerfectionText();

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => leaderSelectUI.OnSelectLeader(data));
    }
}