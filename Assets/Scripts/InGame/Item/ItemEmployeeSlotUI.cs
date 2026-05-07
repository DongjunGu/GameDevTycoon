using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemEmployeeSlotUI : MonoBehaviour
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
    public TextMeshProUGUI satisfactionText;
    public Button selectButton;
    public Image bgImage;

    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic   = new Color(0.55f, 0.30f, 0.85f);

    private EmployeeGrade _pendingGrade;

    public void Setup(EmployeeData data, Action<EmployeeData> onSelect)
    {
        nameText.text            = data.employeeName;
        roleText.text            = data.RoleToString();
        gradeText.text           = data.GradeToString();
        potentialText.text       = data.PotentialToString();
        developSkillText.text    = data.DevelopText();
        planningSkillText.text   = data.PlanningText();
        artSkillText.text        = data.ArtText();
        creativitySkillText.text = data.CreativityText();
        satisfactionText.text    = data.SatisfactionText();
        enhancementText.text     = $"+{data.enhancementLevel}";

        developSkillText.color    = EmployeeData.GetStatColor(data.developSkill,    data.EffectiveDevelopSkill);
        planningSkillText.color   = EmployeeData.GetStatColor(data.planningSkill,   data.EffectivePlanningSkill);
        artSkillText.color        = EmployeeData.GetStatColor(data.artSkill,        data.EffectiveArtSkill);
        creativitySkillText.color = EmployeeData.GetStatColor(data.creativitySkill, data.EffectiveCreativitySkill);

        float sat = data.GetSatisfactionMultiplier();
        satisfactionText.color = sat > 1f ? Color.red : sat < 1f ? Color.blue : Color.white;

        _pendingGrade = data.grade;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelect(data));
    }

    void OnEnable() => ApplyGradeColor(_pendingGrade);

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (bgImage == null) return;
        StopAllCoroutines();

        if (grade == EmployeeGrade.Legendary) { StartCoroutine(LegendaryShimmer()); return; }
        if (grade == EmployeeGrade.Unique) { StartCoroutine(UniqueShimmer()); return; }

        bgImage.color = grade switch
        {
            EmployeeGrade.Normal => ColorNormal,
            EmployeeGrade.Rare   => ColorRare,
            EmployeeGrade.Epic   => ColorEpic,
            _ => ColorNormal
        };
    }

    System.Collections.IEnumerator UniqueShimmer()
    {
        Color goldA = new Color(1.0f, 0.85f, 0.30f);
        Color goldB = new Color(0.85f, 0.65f, 0.10f);
        float speed = 2.0f;
        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            bgImage.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
    }

    System.Collections.IEnumerator LegendaryShimmer()
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            bgImage.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}
