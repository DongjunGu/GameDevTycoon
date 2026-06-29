using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderSlotUI : MonoBehaviour
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
    public GameObject dispatchedBadge; // 파견중 뱃지 (옵션)
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic = new Color(0.55f, 0.30f, 0.85f);
    public Image bgImage;
    private EmployeeGrade _pendingGrade;
    public void Setup(EmployeeData data, LeaderSelectUI leaderSelectUI)
    {
        nameText.text = data.employeeName;
        roleText.text = data.RoleToString();
        gradeText.text = data.GradeToString();
        // 팀장 선택 슬롯에서는 특성 클릭 시 AlertUI 설명을 띄우지 않음 (클릭은 슬롯 선택으로 통과)
        CharacterTraitApplier.SetupTraitText(traitText, data, clickable: false);
        CharacterUniqueEvents.SetupEventText(traitText, data);
        potentialText.text = data.PotentialToString();
        developSkillText.text = data.DevelopText();
        planningSkillText.text = data.PlanningText();
        artSkillText.text = data.ArtText();
        creativitySkillText.text = data.isCEO ? "" : data.CreativityText();
        enhancementText.text     = $"+{data.enhancementLevel}";
        satisfactionText.text = data.SatisfactionText();

        developSkillText.color    = EmployeeData.GetStatColor(data.developSkill,    data.EffectiveDevelopSkill);
        planningSkillText.color   = EmployeeData.GetStatColor(data.planningSkill,   data.EffectivePlanningSkill);
        artSkillText.color        = EmployeeData.GetStatColor(data.artSkill,        data.EffectiveArtSkill);
        creativitySkillText.color = EmployeeData.GetStatColor(data.creativitySkill, data.EffectiveCreativitySkill);
        float sat = data.GetSatisfactionMultiplier();
        satisfactionText.color = sat > 1f ? Color.red : sat < 1f ? Color.blue : Color.white;
        // CEO 는 등급 표시 X — Legendary shimmer 등 배경 효과 무력화
        _pendingGrade = data.isCEO ? EmployeeGrade.Normal : data.grade;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => leaderSelectUI.OnSelectLeader(data));

        // 파견중이면 희미하게 + badge + 선택 차단
        DispatchSlotVisual.Apply(this, dispatchedBadge, data.id, blockSelect: true);

        // 이미 활성 상태에서 재생성될 때는 OnEnable 이 Setup 보다 먼저 실행되어 색이 어긋남 → 활성이면 재적용
        if (isActiveAndEnabled) ApplyGradeColor(_pendingGrade);
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