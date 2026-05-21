using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmployeeSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image bgImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI enhancementText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI traitText;   // 캐릭터 특성명 (grade >= Epic 일 때만 표시, 아니면 빈 문자열)
    public TextMeshProUGUI potentialText;
    public TextMeshProUGUI developSkillText;
    public TextMeshProUGUI planningSkillText;
    public TextMeshProUGUI artSkillText;
    public TextMeshProUGUI creativitySkillText;
    public TextMeshProUGUI salaryText;
    public TextMeshProUGUI satisfactionText;
    public Button selectButton;
    public GameObject ownedBadge;  // 보유중 뱃지 이미지
    private static readonly Color ColorNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color ColorRare = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color ColorEpic = new Color(0.55f, 0.30f, 0.85f);
    private EmployeeGrade _pendingGrade;

    public void Setup(EmployeeData data, HiringUI hiringUI)
    {
        nameText.text = data.employeeName;
        roleText.text = data.RoleToString();
        gradeText.text = data.GradeToString();
        if (traitText != null) traitText.text = CharacterTraitApplier.GetTraitName(data);
        potentialText.text = data.PotentialToString();
        developSkillText.text = data.DevelopDisplayText();
        planningSkillText.text = data.PlanningDisplayText();
        artSkillText.text = data.ArtDisplayText();
        creativitySkillText.text = data.CreativityText();
        salaryText.text = data.SalaryRangeText();
        enhancementText.text     = $"+{data.enhancementLevel}";
        satisfactionText.text = data.SatisfactionText();
        _pendingGrade = data.grade;

        // 보유중 뱃지 (masterEmployeeId 공백이면 이름으로 대조)
        bool isOwned = EmployeeManager.Instance.ownedEmployees
            .Exists(e => IsSameEmployee(e, data));
        if (ownedBadge != null) ownedBadge.SetActive(isOwned);

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => hiringUI.OnSelectEmployee(data));

        // 새로고침 등 이미 활성 상태에서 재생성될 때는 OnEnable 이 Setup 보다 먼저 실행되어
        // _pendingGrade 가 기본값일 때 색이 칠해짐 → 활성 상태면 여기서 다시 적용.
        // 비활성이면 코루틴 에러 방지 위해 건너뛰고 OnEnable 에 위임.
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

    // masterEmployeeId가 없는 구형 데이터는 이름으로 대조
    public static bool IsSameEmployee(EmployeeData owned, EmployeeData candidate)
    {
        if (!string.IsNullOrEmpty(owned.masterEmployeeId))
            return owned.masterEmployeeId == candidate.id;
        return owned.employeeName == candidate.employeeName;
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

    System.Collections.IEnumerator LegendaryShimmer()
    {
        // 무지개 HSV cycling — Unique보다 화려
        while (true)
        {
            float h = Mathf.Repeat(Time.time * 0.25f, 1f);
            bgImage.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}