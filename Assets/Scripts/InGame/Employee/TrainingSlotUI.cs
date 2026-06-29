using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrainingSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI traitText;   // 캐릭터 특성명 (grade >= Epic 일 때만, 아니면 빈 문자열)
    public TextMeshProUGUI potentialText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI creativitySkillText;
    public TextMeshProUGUI satisfactionText;
    public Button selectButton;
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic = new Color(0.55f, 0.30f, 0.85f);
    public Image bgImage;
    private EmployeeGrade _pendingGrade;
    // 콜백 기반 Setup — TrainingPanelUI(단일 화면)가 슬롯 클릭 시 호출.
    public void Setup(EmployeeData data, System.Action<EmployeeData> onSelect)
    {
        SetupVisuals(data);
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelect?.Invoke(data));

        // 이미 활성 상태에서 재생성될 때는 OnEnable 이 Setup 보다 먼저 실행되어 색이 어긋남 → 활성이면 재적용
        if (isActiveAndEnabled) ApplyGradeColor(_pendingGrade);
    }

    void SetupVisuals(EmployeeData data)
    {
        nameText.text            = data.employeeName;
        roleText.text            = data.RoleToString();
        gradeText.text            = data.GradeToString();
        CharacterTraitApplier.SetupTraitText(traitText, data);
        CharacterUniqueEvents.SetupEventText(traitText, data);
        potentialText.text           = data.PotentialToString();
        developSkillText.text    = data.DevelopText();
        planningSkillText.text   = data.PlanningText();
        artSkillText.text        = data.ArtText();
        creativitySkillText.text = data.CreativityText();
        satisfactionText.text = data.SatisfactionText();

        developSkillText.color    = EmployeeData.GetStatColor(data.developSkill,    data.EffectiveDevelopSkill);
        planningSkillText.color   = EmployeeData.GetStatColor(data.planningSkill,   data.EffectivePlanningSkill);
        artSkillText.color        = EmployeeData.GetStatColor(data.artSkill,        data.EffectiveArtSkill);
        creativitySkillText.color = EmployeeData.GetStatColor(data.creativitySkill, data.EffectiveCreativitySkill);
        float sat = data.GetSatisfactionMultiplier();
        satisfactionText.color = sat > 1f ? Color.red : sat < 1f ? Color.blue : Color.white;
        enhancementText.text     = $"+{data.enhancementLevel}";

        _pendingGrade = data.grade;
    }

    void OnEnable()
    {
        ApplyGradeColor(_pendingGrade); // 활성화될 때 실행
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (bgImage == null) return;

        StopAllCoroutines();

        if (grade == EmployeeGrade.Legendary)
        {
            StartCoroutine(LegendaryShimmer());
            return;
        }

        if (grade == EmployeeGrade.Unique)
        {
            bgImage.color = new Color(1f, 0.85f, 0.30f);
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