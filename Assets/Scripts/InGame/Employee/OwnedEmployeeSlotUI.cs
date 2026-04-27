using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OwnedEmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI potentialText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI creativitySkillText;
    public TextMeshProUGUI salaryText;
    public TextMeshProUGUI satisfactionText;
    //public TextMeshProUGUI stateText;
    public Button fireButton;

    private EmployeeData _data;
    private EmployeeListUI _listUI;
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic = new Color(0.82f, 0.75f, 0.95f);
    public Image bgImage;
    private EmployeeGrade _pendingGrade;
    public void Setup(EmployeeData data, EmployeeListUI listUI)
    {
        _data = data;
        _listUI = listUI;

        nameText.text = data.employeeName;
        roleText.text = data.RoleToString();
        gradeText.text = data.GradeToString();
        potentialText.text = data.PotentialToString();
        developSkillText.text = data.DevelopText();
        planningSkillText.text = data.PlanningText();
        artSkillText.text = data.ArtText();
        creativitySkillText.text = data.CreativityText();
        salaryText.text = data.SalaryText();
        enhancementText.text = $"+{data.enhancementLevel}";
        satisfactionText.text = data.SatisfactionText();

        developSkillText.color    = EmployeeData.GetStatColor(data.developSkill,    data.EffectiveDevelopSkill);
        planningSkillText.color   = EmployeeData.GetStatColor(data.planningSkill,   data.EffectivePlanningSkill);
        artSkillText.color        = EmployeeData.GetStatColor(data.artSkill,        data.EffectiveArtSkill);
        creativitySkillText.color = EmployeeData.GetStatColor(data.creativitySkill, data.EffectiveCreativitySkill);
        float sat = data.GetSatisfactionMultiplier();
        satisfactionText.color = sat > 1f ? Color.red : sat < 1f ? Color.blue : Color.white;
        //stateText.text           = data.StateToString();
        _pendingGrade = data.grade;
        ApplyGradeColor(_pendingGrade);

        fireButton.onClick.RemoveAllListeners();
        if (_listUI != null)
            fireButton.onClick.AddListener(() => _listUI.OnClickFire(_data));

    }

    void OnEnable()
    {
        ApplyGradeColor(_pendingGrade); // 활성화될 때 실행
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (bgImage == null) return;

        StopAllCoroutines();

        if (grade == EmployeeGrade.Unique)
        {
            StartCoroutine(UniqueShimmer());
            return;
        }

        bgImage.color = grade switch
        {
            EmployeeGrade.Normal => ColorNormal,
            EmployeeGrade.Rare => ColorRare,
            EmployeeGrade.Epic => ColorEpic,
            _ => ColorNormal
        };
    }

    System.Collections.IEnumerator UniqueShimmer()
    {
        // 황금색 두 가지를 오가며 반짝임
        Color goldA = new Color(1.0f, 0.85f, 0.30f); // 밝은 황금
        Color goldB = new Color(0.85f, 0.65f, 0.10f); // 어두운 황금
        float speed = 2.0f;

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            bgImage.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
    }
}