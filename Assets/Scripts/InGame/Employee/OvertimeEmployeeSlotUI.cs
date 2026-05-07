using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OvertimeEmployeeSlotUI : MonoBehaviour
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
    public Button slotButton;
    public Image bgImage;

    private static readonly Color ColorNormal   = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare     = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic     = new Color(0.55f, 0.30f, 0.85f);
    private static readonly Color ColorSelected = new Color(1.0f,  0.3f,  0.3f);  // 야근 선택 색

    private EmployeeData _data;
    private EmployeeGrade _grade;

    public void Setup(EmployeeData data)
    {
        _data  = data;
        _grade = data.grade;

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

        data.isOvertimeWorker = false;
        RefreshColor();

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnClickSlot);
    }

    void OnClickSlot()
    {
        _data.isOvertimeWorker = !_data.isOvertimeWorker;
        RefreshColor();
    }

    void RefreshColor()
    {
        if (bgImage == null) return;
        StopAllCoroutines();

        if (_data.isOvertimeWorker)
        {
            bgImage.color = ColorSelected;
            return;
        }

        if (_grade == EmployeeGrade.Legendary)
        {
            StartCoroutine(LegendaryShimmer());
            return;
        }

        if (_grade == EmployeeGrade.Unique)
        {
            StartCoroutine(UniqueShimmer());
            return;
        }

        bgImage.color = _grade switch
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
