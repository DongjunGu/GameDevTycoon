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
    public TextMeshProUGUI potentialText;   // "{잠재력}"
    public TextMeshProUGUI gradeText;
    public Image gradePanel;                // 등급별 이미지 (색 대신 sprite 교체)
    public GradeSpriteSet gradeSpriteSet;   // 공용 등급 스프라이트 세트 (gradePanel 에 적용)
    public Image roleBadge;                 // 역할 아이콘
    public RoleIconSet roleIconSet;         // 공용 역할 아이콘 세트

    [Header("Trait / Event")]
    public TextMeshProUGUI traitText;       // 캐릭터 특성명 (등급 무관 항상 표시)
    public TextMeshProUGUI eventText;       // 전용 이벤트명 (등급 무관 항상 표시)
    [Tooltip("특성 등급(Epic) 미충족 시 활성화되는 덮개 — 위에 raycast Image 가 있어 클릭 차단")]
    public GameObject traitLockedPanel;
    [Tooltip("이벤트 등급(Unique) 미충족 시 활성화되는 덮개 — 위에 raycast Image 가 있어 클릭 차단")]
    public GameObject eventLockedPanel;

    [Header("Enhancement / Satisfaction")]
    public TextMeshProUGUI enhancementText;
    public Slider satisfactionSlider;
    public SatisfactionFillSet satisfactionFillSet; // 구간별 Fill sprite 묶음 (공용 에셋)
    public TextMeshProUGUI satisfactionText;

    [Header("Stats (주스탯=강화반영 범위 / 부스탯·창의성=고정)")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Salary")]
    public TextMeshProUGUI salaryText;      // "연봉: {}G"

    // showActualStats=false(채용): 주스탯 강화 반영 "범위"(interval) 표시.
    // showActualStats=true(해고): 확정된 실제 수치 + 버프/디버프 색상(버프 빨강 / 디버프 파랑) 표시.
    public void Setup(EmployeeData emp, bool showActualStats = false)
    {
        if (emp == null) return;

        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/Mini/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        if (nameText != null)      nameText.text      = emp.employeeName;
        if (potentialText != null) potentialText.text = emp.PotentialToString();
        if (gradeText != null)     gradeText.text     = emp.GradeToString();
        RoleIconSet.Apply(roleBadge, roleIconSet, emp.role);

        // 등급별 이미지 교체 (색 변경 대신 sprite 세트)
        GradeSpriteSet.Apply(gradePanel, gradeSpriteSet, emp.grade);

        // 특성/이벤트 — 등급 무관 이름을 항상 표시(다 출력). 등급 충족이면 클릭 시 설명 패널 토글,
        // 미충족이면 덮개(lockedPanel) 활성화로 클릭 차단. (EmployeeCardUI 와 동일 규칙)
        ResolveDescRefs();
        string traitName = (!emp.isCEO) ? CharacterTraitApplier.GetTraitNameAnyGrade(emp) : "";
        bool traitUnlocked = (!emp.isCEO) && CharacterTraitApplier.IsTraitUnlocked(emp);
        WireDesc(traitText, _traitDescImg, _traitDescText, traitName,
                 CharacterTraitApplier.GetTraitDescription(emp), "특성", traitUnlocked, traitLockedPanel);

        string eventName = CharacterUniqueEvents.GetEventNameAnyGrade(emp);
        bool eventUnlocked = CharacterUniqueEvents.IsEventUnlocked(emp);
        WireDesc(eventText, _eventDescImg, _eventDescText, eventName,
                 CharacterUniqueEvents.GetEventDescription(emp), "이벤트", eventUnlocked, eventLockedPanel);

        if (enhancementText != null) enhancementText.text = $"+{emp.enhancementLevel}";

        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            satisfactionSlider.value = emp.satisfaction;
            SatisfactionFillSet.Apply(satisfactionSlider, satisfactionFillSet, emp.satisfaction);
        }
        if (satisfactionText != null) satisfactionText.text = $"{emp.satisfaction}";

        // 능력치 — 해고(actual): 확정 실제값 + 버프/디버프 색상 / 채용(interval): 강화 반영 범위, 라벨 없이 값만 ("기획: 50~200" → "50~200")
        if (showActualStats)
        {
            EmployeeCardUI.SetStatColored(planningText,   emp.planningSkill,   emp.EffectivePlanningSkill);
            EmployeeCardUI.SetStatColored(developText,    emp.developSkill,    emp.EffectiveDevelopSkill);
            EmployeeCardUI.SetStatColored(artText,        emp.artSkill,        emp.EffectiveArtSkill);
            EmployeeCardUI.SetStatColored(creativityText, emp.creativitySkill, emp.EffectiveCreativitySkill);
        }
        else
        {
            if (planningText   != null) planningText.text   = ValueOnly(emp.PlanningDisplayText());
            if (developText    != null) developText.text    = ValueOnly(emp.DevelopDisplayText());
            if (artText        != null) artText.text        = ValueOnly(emp.ArtDisplayText());
            if (creativityText != null) creativityText.text = ValueOnly(emp.CreativityText());
        }

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
    // unlocked=false(등급 미충족)면 lockedPanel 덮개 활성화 + 클릭(설명) 비활성.
    void WireDesc(TMP_Text label, Image descImg, TMP_Text descText, string name, string desc, string kind,
                  bool unlocked, GameObject lockedPanel)
    {
        if (descImg  != null) descImg.enabled = false;
        if (descText != null) descText.gameObject.SetActive(false);

        bool has = !string.IsNullOrEmpty(name);

        // 특성/이벤트가 있는데 등급 미충족일 때만 덮개 노출 (미보유는 덮을 게 없음)
        if (lockedPanel != null) lockedPanel.SetActive(has && !unlocked);

        if (label == null) return;
        label.text = has ? $"{kind} : {name}" : $"{kind} : 없음";

        // 등급 충족(unlocked) + 설명 존재일 때만 클릭으로 설명 토글. 미충족이면 덮개가 클릭 차단.
        bool clickable = has && unlocked && !string.IsNullOrEmpty(desc) && (descImg != null || descText != null);
        label.raycastTarget = clickable;

        var toggle = label.GetComponent<ResumeDescriptionToggle>();
        if (clickable)
        {
            if (toggle == null) toggle = label.gameObject.AddComponent<ResumeDescriptionToggle>();
            toggle.Setup(descImg, descText, desc);
        }
        else if (toggle != null) toggle.Setup(null, null, null);
    }

}
