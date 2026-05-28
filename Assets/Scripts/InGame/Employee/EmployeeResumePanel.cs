using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 채용 후보 "이력서" 카드. 표시 항목은 EmployeeCardUI 와 거의 동일 + 연봉(Salary).
/// 단, 능력치는 채용창 방식 — 주스탯은 강화 반영 "범위"(interval), 부스탯·창의성은 고정 수치.
/// 후보(EmployeeManager 미등록) 데이터를 직접 받아 표시하므로 Setup(EmployeeData) 사용.
/// (EmployeeCardUI 처럼 자식 UI 를 인스펙터에 연결해서 쓴다. Update/닫기/아이템·강화 버튼 없음 — 순수 표시용.)
/// </summary>
public class EmployeeResumePanel : MonoBehaviour
{
    [Header("Identity")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI potentialText;   // "잠재력: {}"
    public TextMeshProUGUI gradeText;
    public Image gradePanel;                // 등급색 배경 (Unique/Legendary 셰머)
    public Image roleBadge;                 // 역할 아이콘
    public Sprite[] roleIcons;              // role enum 순서 [Planner, Programmer, Artist]

    [Header("Trait / Event")]
    public TextMeshProUGUI traitText;       // 캐릭터 특성명 (Epic+ 일 때만)
    public TextMeshProUGUI eventText;       // 전용 이벤트명 (Unique+ 일 때만)

    [Header("Enhancement / Satisfaction")]
    public TextMeshProUGUI enhancementText;
    public Slider satisfactionSlider;
    public TextMeshProUGUI satisfactionText;

    [Header("Stats (주스탯=강화반영 범위 / 부스탯·창의성=고정)")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Salary")]
    public TextMeshProUGUI salaryText;      // "연봉: {}G"

    public void Setup(EmployeeData emp)
    {
        if (emp == null) return;

        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        if (nameText != null)      nameText.text      = emp.employeeName;
        if (potentialText != null) potentialText.text = $"잠재력: {emp.PotentialToString()}";
        if (gradeText != null)     gradeText.text     = emp.GradeToString();
        if (roleBadge != null && roleIcons != null
            && (int)emp.role >= 0 && (int)emp.role < roleIcons.Length
            && roleIcons[(int)emp.role] != null)
            roleBadge.sprite = roleIcons[(int)emp.role];

        ApplyGradeColor(emp.grade);

        // 특성/이벤트 — 라벨엔 이름(없으면 "없음"), 클릭하면 설명 패널(traitDescriptionPanel/eventDescriptionPanel)을 토글해 설명 표시.
        // AlertUI 대신 자체 패널 사용(ModalGate 차단 영향 없음). 설명만 표시(이름은 라벨에만).
        ResolveDescRefs();
        string traitName = (!emp.isCEO) ? CharacterTraitApplier.GetTraitName(emp) : "";
        WireDesc(traitText, _traitDescImg, _traitDescText, traitName, CharacterTraitApplier.GetTraitDescription(emp), "특성");
        WireDesc(eventText, _eventDescImg, _eventDescText, CharacterUniqueEvents.GetEventName(emp), CharacterUniqueEvents.GetEventDescription(emp), "이벤트");

        if (enhancementText != null) enhancementText.text = $"{emp.enhancementLevel}Lv / {emp.MaxEnhancementLevel}Lv";

        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            satisfactionSlider.value = emp.satisfaction;
            EmployeeCardUI.ApplySatisfactionColor(satisfactionSlider, emp.satisfaction);
        }
        if (satisfactionText != null) satisfactionText.text = $"{emp.satisfaction}";

        // 능력치 — 채용창과 동일(주스탯=강화 반영 범위, 부스탯·창의성=고정)이되, 라벨 없이 값만 표시 ("기획: 50~200" → "50~200")
        if (planningText   != null) planningText.text   = ValueOnly(emp.PlanningDisplayText());
        if (developText    != null) developText.text    = ValueOnly(emp.DevelopDisplayText());
        if (artText        != null) artText.text        = ValueOnly(emp.ArtDisplayText());
        if (creativityText != null) creativityText.text = ValueOnly(emp.CreativityText());

        if (salaryText != null) salaryText.text = ValueOnly(emp.SalaryRangeText());  // "연봉: nG" → "nG"
    }

    // "기획: 50~200" → "50~200" 처럼 라벨(콜론 앞)을 떼고 값만 반환
    static string ValueOnly(string labeled)
    {
        if (string.IsNullOrEmpty(labeled)) return labeled;
        int i = labeled.IndexOf(':');
        return i >= 0 ? labeled.Substring(i + 1).Trim() : labeled;
    }

    // ── 특성/이벤트 설명 패널 (자식 이름으로 자동 탐색 → 수동 배선 불필요, 복제 패널도 자동 동작) ──
    Image _traitDescImg; TMP_Text _traitDescText;
    Image _eventDescImg; TMP_Text _eventDescText;
    bool _descResolved;

    void ResolveDescRefs()
    {
        if (_descResolved) return;
        _descResolved = true;
        var tp = FindDescendant("traitDescriptionPanel");
        if (tp != null) { _traitDescImg = tp.GetComponent<Image>(); _traitDescText = tp.GetComponentInChildren<TMP_Text>(true); }
        var ep = FindDescendant("eventDescriptionPanel");
        if (ep != null) { _eventDescImg = ep.GetComponent<Image>(); _eventDescText = ep.GetComponentInChildren<TMP_Text>(true); }
    }

    Transform FindDescendant(string n)
    {
        // 이름 끝 공백 등 사소한 오타에도 견고하게 Trim 비교 (예: "traitDescriptionPanel ")
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name.Trim() == n) return t;
        return null;
    }

    // 라벨에 "{kind} : {이름}"(없으면 "{kind} : 없음") 표시 + 설명 있으면 클릭 토글 부착. 설명 패널은 기본 숨김.
    void WireDesc(TMP_Text label, Image descImg, TMP_Text descText, string name, string desc, string kind)
    {
        if (descImg  != null) descImg.enabled = false;
        if (descText != null) descText.gameObject.SetActive(false);
        if (label == null) return;

        bool has = !string.IsNullOrEmpty(name);
        label.text = has ? $"{kind} : {name}" : $"{kind} : 없음";

        bool clickable = has && !string.IsNullOrEmpty(desc) && (descImg != null || descText != null);
        label.raycastTarget = clickable;

        var toggle = label.GetComponent<ResumeDescriptionToggle>();
        if (clickable)
        {
            if (toggle == null) toggle = label.gameObject.AddComponent<ResumeDescriptionToggle>();
            toggle.Setup(descImg, descText, desc);
        }
        else if (toggle != null) toggle.Setup(null, null, null);
    }

    // ── 등급색 (gradePanel) — EmployeeCardUI 와 동일 규칙 ──
    private static readonly Color GradeNormal = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color GradeRare   = new Color(0.75f, 0.88f, 0.95f);
    private static readonly Color GradeEpic   = new Color(0.55f, 0.30f, 0.85f);

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (gradePanel == null) return;
        StopAllCoroutines();

        // 코루틴(셰머)은 활성 상태에서만 — 비활성 GameObject 에서 StartCoroutine 시 에러 방지.
        if (isActiveAndEnabled)
        {
            if (grade == EmployeeGrade.Legendary) { StartCoroutine(LegendaryShimmer()); return; }
            if (grade == EmployeeGrade.Unique)    { StartCoroutine(UniqueShimmer());    return; }
        }

        gradePanel.color = grade switch
        {
            EmployeeGrade.Rare      => GradeRare,
            EmployeeGrade.Epic      => GradeEpic,
            EmployeeGrade.Unique    => new Color(1.0f, 0.85f, 0.30f), // 비활성 시 셰머 대신 단색 골드
            EmployeeGrade.Legendary => new Color(1.0f, 0.85f, 0.30f),
            _                       => GradeNormal,
        };
    }

    System.Collections.IEnumerator UniqueShimmer()
    {
        Color goldA = new Color(1.0f, 0.85f, 0.30f);
        Color goldB = new Color(0.85f, 0.65f, 0.10f);
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 2.0f) + 1f) / 2f;
            gradePanel.color = Color.Lerp(goldB, goldA, t);
            yield return null;
        }
    }

    System.Collections.IEnumerator LegendaryShimmer()
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.unscaledTime * 0.25f, 1f);
            gradePanel.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}
