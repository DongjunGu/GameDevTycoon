using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 직원 관리 패널 (구 해고-캐러셀 EmployeeListUI 를 새 디자인으로 교체).
//  - 우(EmployeeListRightScrollView/Content): 보유 직원 슬롯 리스트(EmployeeSlotListUI 프리팹)
//  - 좌(ListLeftPanel): 슬롯 선택 시 해당 직원 상세 (EmployeeCardUI 와 동일 항목). 선택 전엔 비활성 + EmptyPanel 표시.
//  - BottomPanel: 강화하기 / 해고하기 / 아이템 사용하기 / 닫기.
//      · 강화 → TrainingPanelUI.OpenForEmployee (이 패널 닫고 열기 → 닫히면 복귀)
//      · 해고 → ConfirmUI 확인 후 FireEmployee, 목록 갱신
//      · 아이템 → ItemPanelUI.OpenForEmployee (닫고 열기 → 복귀)
//      · 파견중 직원은 강화/아이템/해고 시 AlertUI 로 차단.
public class EmployeeListUI : MonoBehaviour
{
    public static EmployeeListUI Instance { get; private set; }

    [Header("Root (열기/닫기 토글 대상 — 비우면 이 GameObject)")]
    public GameObject panelRoot;          // EmployeeListPanel

    [Header("List (EmployeeListRightScrollView/Content)")]
    public Transform  slotParent;         // Content
    public GameObject slotPrefab;         // EmployeeSlotListUI 프리팹

    [Header("Detail toggle")]
    public GameObject leftPanel;          // ListLeftPanel — 선택 시 활성
    public GameObject emptyPanel;         // 선택 전 placeholder — leftPanel 과 반대로 토글

    [Header("Detail (ListLeftPanel) — EmployeeCardUI 와 동일 항목")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI potentialText;     // "잠재력: {}"
    public TextMeshProUGUI gradeText;
    public Image gradePanel;                  // 등급색 배경
    public Image roleBadge;                   // 역할 아이콘
    public Sprite[] roleIcons;                // role enum 순서 [Planner, Programmer, Artist]
    public TextMeshProUGUI traitText;         // 특성명 (클릭 시 설명)
    public TextMeshProUGUI eventText;         // 전용 이벤트명 (클릭 시 설명)
    public TextMeshProUGUI enhancementText;   // "Lv.{}"
    public Slider satisfactionSlider;
    public TextMeshProUGUI satisfactionText;
    public GameObject dispatchedBadge;        // 파견중 badge (옵션)
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Stat arrows — 능력치 옆 ArrowImage (상승=red / 하락=blue / 무변화=숨김)")]
    public Image planningArrow;
    public Image developArrow;
    public Image artArrow;
    public Image creativityArrow;
    public Sprite redArrow;   // 버프(상승, 빨간 수치)
    public Sprite blueArrow;  // 디버프(하락, 파란 수치)

    [Header("Buttons (BottomPanel)")]
    public Button enhanceButton;
    public Button fireButton;
    public Button itemButton;
    public Button closeButton;

    private string _selectedId = "";
    private Coroutine _gradeCo;

    GameObject Root => panelRoot != null ? panelRoot : gameObject;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (enhanceButton != null) { enhanceButton.onClick.RemoveListener(OnClickEnhance); enhanceButton.onClick.AddListener(OnClickEnhance); }
        if (fireButton    != null) { fireButton.onClick.RemoveListener(OnClickFire);       fireButton.onClick.AddListener(OnClickFire); }
        if (itemButton    != null) { itemButton.onClick.RemoveListener(OnClickItem);       itemButton.onClick.AddListener(OnClickItem); }
        if (closeButton   != null) { closeButton.onClick.RemoveListener(OnClickClose);     closeButton.onClick.AddListener(OnClickClose); }
    }

    // 메뉴 직원관리 버튼 OnClick 에 연결
    public void OpenList()
    {
        GameTimeManager.Instance?.StopTime();
        Root.SetActive(true);
        BuildList();
        HideDetail();
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        Root.SetActive(false);
    }

    // 외부(커플 동반퇴사/사직/도주 등)에서 ownedEmployees 변경 시, 열려있으면 다시 빌드.
    public void RefreshIfOpen()
    {
        if (Root.activeInHierarchy) { BuildList(); RefreshSelectedOrHide(); }
    }

    // ── 목록 ─────────────────────────────────────
    void BuildList()
    {
        if (slotParent == null || slotPrefab == null || EmployeeManager.Instance == null) return;
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            var go = Instantiate(slotPrefab, slotParent);
            go.SetActive(true); // 프리팹 루트가 비활성으로 저장돼 있어도 강제 활성
            var slot = go.GetComponent<EmployeeSlotListUI>();
            if (slot != null) slot.Setup(emp, OnSelectEmployee);
        }
    }

    // ── 선택 / 상세 ───────────────────────────────
    // public: 구 OwnedEmployeeSlotUI(연봉협상 슬롯)가 참조 — 호환 유지.
    public void OnSelectEmployee(EmployeeData emp)
    {
        if (emp == null) return;
        _selectedId = emp.id;
        SetDetailActive(true);
        PopulateDetail(emp);
    }

    void HideDetail()
    {
        _selectedId = "";
        SetDetailActive(false);
        RefreshButtons(null);
    }

    // 파견중 직원은 선택은 되지만 강화/해고/아이템 버튼 비활성(클릭 불가). 선택 없으면 셋 다 비활성.
    void RefreshButtons(EmployeeData emp)
    {
        bool ok = emp != null && !IsDispatched(emp.id);
        if (enhanceButton != null) enhanceButton.interactable = ok;
        if (fireButton    != null) fireButton.interactable    = ok;
        if (itemButton    != null) itemButton.interactable    = ok;
    }

    // 선택 직원이 아직 보유 중이면 상세 재표시, 아니면(해고 등) EmptyPanel 로.
    void RefreshSelectedOrHide()
    {
        var emp = !string.IsNullOrEmpty(_selectedId) ? EmployeeManager.Instance?.GetEmployee(_selectedId) : null;
        if (emp != null) { SetDetailActive(true); PopulateDetail(emp); }
        else HideDetail();
    }

    void SetDetailActive(bool active)
    {
        if (leftPanel  != null) leftPanel.SetActive(active);
        if (emptyPanel != null) emptyPanel.SetActive(!active);
    }

    void PopulateDetail(EmployeeData emp)
    {
        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        SetText(nameText,      emp.employeeName);
        SetText(potentialText, $"잠재력: {emp.PotentialToString()}");
        SetText(gradeText,     emp.GradeToString());
        if (roleBadge != null && roleIcons != null
            && (int)emp.role >= 0 && (int)emp.role < roleIcons.Length
            && roleIcons[(int)emp.role] != null)
            roleBadge.sprite = roleIcons[(int)emp.role];

        ApplyGradeColor(emp.grade);
        CharacterTraitApplier.SetupTraitText(traitText, emp);
        CharacterUniqueEvents.SetupEventTextDirect(eventText, emp);

        SetText(enhancementText, $"Lv.{emp.enhancementLevel}");
        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            satisfactionSlider.value = emp.satisfaction;
            EmployeeCardUI.ApplySatisfactionColor(satisfactionSlider, emp.satisfaction);
        }
        SetText(satisfactionText, $"{emp.satisfaction}");

        if (dispatchedBadge != null)
            dispatchedBadge.SetActive(IsDispatched(emp.id));

        // 능력치 = 버프/디버프 적용 실제값 + 색상 (EmployeeCardUI 와 동일)
        EmployeeCardUI.SetStatColored(planningText,   emp.planningSkill,   emp.EffectivePlanningSkill);
        EmployeeCardUI.SetStatColored(developText,    emp.developSkill,    emp.EffectiveDevelopSkill);
        EmployeeCardUI.SetStatColored(artText,        emp.artSkill,        emp.EffectiveArtSkill);
        EmployeeCardUI.SetStatColored(creativityText, emp.creativitySkill, emp.EffectiveCreativitySkill);

        // 수치 옆 화살표 — 수치 색상과 동일 기준 (상승=red / 하락=blue / 무변화=숨김)
        SetStatArrow(planningArrow,   emp.planningSkill,   emp.EffectivePlanningSkill);
        SetStatArrow(developArrow,    emp.developSkill,    emp.EffectiveDevelopSkill);
        SetStatArrow(artArrow,        emp.artSkill,        emp.EffectiveArtSkill);
        SetStatArrow(creativityArrow, emp.creativitySkill, emp.EffectiveCreativitySkill);

        RefreshButtons(emp); // 파견중이면 강화/해고/아이템 버튼 비활성
    }

    // 능력치 변화 방향에 따라 화살표 sprite 교체. 변화 없으면 Image 만 끔(layout 슬롯 보존).
    void SetStatArrow(Image arrow, int baseSkill, int effectiveSkill)
    {
        if (arrow == null) return;
        if (effectiveSkill > baseSkill)      { arrow.sprite = redArrow;  arrow.enabled = true; }
        else if (effectiveSkill < baseSkill) { arrow.sprite = blueArrow; arrow.enabled = true; }
        else                                   arrow.enabled = false;
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (gradePanel == null) return;
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        _gradeCo = EmployeeGradeColor.Apply(this, gradePanel, grade);
    }

    // ── 버튼 ─────────────────────────────────────
    EmployeeData Selected()
        => !string.IsNullOrEmpty(_selectedId) ? EmployeeManager.Instance?.GetEmployee(_selectedId) : null;

    // 강화하기 — 이 패널 닫고 강화 패널 열기 → 닫히면 복귀(선택 직원 재표시)
    public void OnClickEnhance()
    {
        var emp = Selected();
        if (emp == null || TrainingPanelUI.Instance == null) return;
        if (IsDispatched(emp.id)) { AlertUI.Instance?.Show("파견중인 직원은 강화할 수 없습니다."); return; }

        string id = emp.id;
        Root.SetActive(false);
        TrainingPanelUI.Instance.OpenForEmployee(emp, () => Reopen(id));
    }

    // 아이템 사용하기 — 이 패널 닫고 아이템 패널을 해당 직원 컨텍스트로 열기 → 닫히면 복귀
    public void OnClickItem()
    {
        var emp = Selected();
        if (emp == null || ItemPanelUI.Instance == null) return;
        if (IsDispatched(emp.id)) { AlertUI.Instance?.Show("파견중인 직원에게는 사용할 수 없습니다."); return; }

        string id = emp.id;
        Root.SetActive(false);
        ItemPanelUI.Instance.OpenForEmployee(id, () => Reopen(id));
    }

    // 해고하기 — 확인 다이얼로그 후 해고
    public void OnClickFire()
    {
        var emp = Selected();
        if (emp == null) return;
        if (IsDispatched(emp.id)) { AlertUI.Instance?.Show("파견중인 직원은 해고할 수 없습니다."); return; }
        if (ConfirmUI.Instance == null) { DoFire(emp.id); return; }

        string id = emp.id;
        ConfirmUI.Instance.Show(
            $"{emp.employeeName}을(를) 해고하시겠습니까?",
            onConfirm: () => DoFire(id),
            onCancel:  () => { },
            confirmText: "네",
            cancelText:  "아니오"
        );
    }

    void DoFire(string id)
    {
        var emp = EmployeeManager.Instance?.GetEmployee(id);
        if (emp == null) return;
        EmployeeManager.Instance.FireEmployee(emp);
        HUDUI.Instance?.RefreshAll();
        _selectedId = "";
        BuildList();
        HideDetail();   // 선택 해제 → EmptyPanel
    }

    // 강화/아이템 패널에서 복귀 — 시간정지는 이미 걸려 있으므로 추가 안 함. 패널만 다시 표시 + 선택 복원.
    void Reopen(string reselectId)
    {
        Root.SetActive(true);
        BuildList();
        var emp = !string.IsNullOrEmpty(reselectId) ? EmployeeManager.Instance?.GetEmployee(reselectId) : null;
        if (emp != null) OnSelectEmployee(emp);
        else HideDetail();
    }

    // ── 헬퍼 ─────────────────────────────────────
    static bool IsDispatched(string id)
        => DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(id);

    static void SetText(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
}
