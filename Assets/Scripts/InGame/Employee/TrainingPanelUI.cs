using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 직원 강화 패널 (선택된 직원의 현재/예상 수치·확률·비용 표시 + 강화 실행).
// 목록/상단정보/초상화/패널 토글 등 구(舊) 단일화면 구조는 제거됨 — 선택은 외부(EmployeeListUI 등)에서 OpenForEmployee 로 주입.
// 강화 비용/확률/롤은 EmployeeEnhancement(공유), 예상 증가량은 EmployeeManager.GetNext*StatGain 사용.
public class TrainingPanelUI : MonoBehaviour
{
    public static TrainingPanelUI Instance { get; private set; }

    [Header("CurrentStatusPanel (현재)")]
    public TextMeshProUGUI curEnhanceText;
    public TextMeshProUGUI curDevelopText;
    public TextMeshProUGUI curPlanningText;
    public TextMeshProUGUI curArtText;
    public TextMeshProUGUI curCreativityText;
    public TextMeshProUGUI curSalaryText;   // 현재 연봉

    [Header("AfterStatusPanel (강화 후 예상)")]
    public TextMeshProUGUI expEnhanceText;
    public TextMeshProUGUI expDevelopText;
    public TextMeshProUGUI expPlanningText;
    public TextMeshProUGUI expArtText;
    public TextMeshProUGUI expCreativityText;
    public TextMeshProUGUI expSalaryText;   // 강화 후 연봉

    [Header("ThirdPanel")]
    public TextMeshProUGUI successRateText; // SuccessPanel 자식
    public TextMeshProUGUI failRateText;    // FailPanel 자식

    [Header("BottomPanel")]
    public TextMeshProUGUI costText;
    public Button enhanceButton;

    [Header("BadgePanel (역할/강화/잠재력/등급 — 선택 직원 동기화. EmployeeListUI 와 동일)")]
    [Tooltip("TrainingPanel 의 BadgePanel (내부 roleIcon/enhancementText/potentialText/gradeText/gradeBG 자동 탐색)")]
    public Transform badgePanel;
    public RoleIconSet roleIconSet;    // 역할 아이콘 세트 (공용)
    public GradeSpriteSet gradeBGSet;  // 등급 BG 세트 (GradeProfileBGSet)

    private EmployeeData _selected;
    private System.Action _onClosed; // 카드 컨텍스트 등에서 닫힐 때 1회 호출

    // BadgePanel 내부 요소 (이름으로 1회 탐색 캐시)
    private bool _badgeResolved;
    private Image _badgeRoleIcon, _badgeGradeBG;
    private TextMeshProUGUI _badgeEnhanceText, _badgePotentialText, _badgeGradeText;

    GameObject Root => gameObject;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (enhanceButton != null)
        {
            enhanceButton.onClick.RemoveListener(OnClickEnhance);
            enhanceButton.onClick.AddListener(OnClickEnhance);
        }
    }

    // ── 열기/닫기 ─────────────────────────────
    public void OpenPanel()
    {
        _onClosed = null;
        GameTimeManager.Instance?.StopTime();
        Root.SetActive(true);
        HideDetail();
    }

    // 특정 직원 강화 패널을 바로 표시 (EmployeeCardUI/EmployeeListUI '강화하기'). onClosed 는 닫힐 때 1회 호출.
    public void OpenForEmployee(EmployeeData emp, System.Action onClosed = null)
    {
        if (emp == null) return;
        _onClosed = onClosed;
        GameTimeManager.Instance?.StopTime();
        Root.SetActive(true);
        OnSelectEmployee(emp);
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        Root.SetActive(false);

        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    // ── 선택 ─────────────────────────────────
    public void OnSelectEmployee(EmployeeData emp)
    {
        _selected = emp;
        RefreshDetail();
    }

    void HideDetail()
    {
        _selected = null;
    }

    // ── 상세 갱신 ─────────────────────────────
    void RefreshDetail()
    {
        var emp = _selected;
        if (emp == null) return;

        UpdateBadge(emp);     // 역할/강화/잠재력/등급 (선택 직원 동기화)
        ColorStatPanels(emp); // 주스탯 패널만 강조색, 나머지 흰색

        // SPLeftPanel — 현재 수치 (raw 스킬 기준)
        SetText(curEnhanceText,    $"현재 : Lv{emp.enhancementLevel}");
        SetText(curDevelopText,    $"개발: {emp.developSkill}");
        SetText(curPlanningText,   $"기획: {emp.planningSkill}");
        SetText(curArtText,        $"아트: {emp.artSkill}");
        SetText(curCreativityText, $"창의성: {emp.creativitySkill}");
        SetText(curSalaryText,     $"연봉: {emp.salary:N0} G");

        if (EmployeeEnhancement.IsMax(emp))
        {
            // 예상수치 — 최대치 (변화 없음)
            SetText(expEnhanceText,    "현재 : MAX");
            SetText(expDevelopText,    $"개발: {emp.developSkill}");
            SetText(expPlanningText,   $"기획: {emp.planningSkill}");
            SetText(expArtText,        $"아트: {emp.artSkill}");
            SetText(expCreativityText, $"창의성: {emp.creativitySkill}");
            SetText(expSalaryText,     $"연봉: {emp.salary:N0} G"); // MAX — 연봉 변화 없음

            SetText(successRateText, "성공확률 : -");
            SetText(failRateText,    "실패확률: -");
            SetText(costText,        "-");

            if (enhanceButton != null) enhanceButton.interactable = false;
            return;
        }

        var mainGain = EmployeeManager.Instance.GetNextMainStatGain(emp);
        // 부스탯은 범위 대신 평균(단일값)으로 표시 — 주스탯만 범위
        int subAvg = EmployeeManager.Instance.GetNextSubStatGainAvg(emp);
        (int min, int max) subGain = (subAvg, subAvg);

        // SPRightPanel — 강화 후 예상 (현재 + 증가 범위)
        SetText(expEnhanceText,    $"강화 후 : Lv{emp.enhancementLevel + 1}");
        SetText(expDevelopText,    ExpStat("개발",   emp.developSkill,    StatIsMain(emp, "develop")  ? mainGain : subGain));
        SetText(expPlanningText,   ExpStat("기획",   emp.planningSkill,   StatIsMain(emp, "planning") ? mainGain : subGain));
        SetText(expArtText,        ExpStat("아트",   emp.artSkill,        StatIsMain(emp, "art")      ? mainGain : subGain));
        SetText(expCreativityText, ExpStat("창의성", emp.creativitySkill, subGain)); // 창의성은 항상 부스탯

        // 강화 후 연봉 = 현재 + 다음 강화 연봉 상승량(금수저 반영)
        SetText(expSalaryText, $"연봉: {emp.salary + EmployeeManager.Instance.GetNextSalaryGain(emp):N0} G");

        // ThirdPanel — 성공/실패 확률 (실패 = 100 - 성공)
        int success = EmployeeEnhancement.SuccessRate(emp);
        SetText(successRateText, $"성공확률 : {success}%");
        SetText(failRateText,    $"실패확률: {100 - success}%");

        // BottomPanel — 필요한 재화
        int cost = EmployeeEnhancement.GetCost(emp);
        SetText(costText, $"{cost:N0} G");

        if (enhanceButton != null) enhanceButton.interactable = true;
    }

    // ── 강화 실행 ─────────────────────────────
    public void OnClickEnhance()
    {
        if (_selected == null || EmployeeEnhancement.IsMax(_selected)) return;

        int cost = EmployeeEnhancement.GetCost(_selected);
        if (cost < 0 || !MoneyManager.Instance.SpendGold(cost)) return;

        EmployeeEnhancement.EnhanceOnce(_selected);

        EmployeeManager.Instance.UpdateEmployee(_selected);
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        RefreshDetail();
    }

    // ── 헬퍼 ─────────────────────────────────
    static readonly Color MainStatPanelColor = new Color(0xDB / 255f, 0x2E / 255f, 0x2E / 255f, 152f / 255f); // #DB2E2E, alpha 152

    // 선택 직원의 주스탯 패널(SPLeft/SPRight 양쪽)만 강조색, 나머지(창의성 포함)는 흰색.
    void ColorStatPanels(EmployeeData emp)
    {
        string main = MainStatKey(emp.role);
        SetPanelColor(curDevelopText,    main == "develop");
        SetPanelColor(curPlanningText,   main == "planning");
        SetPanelColor(curArtText,        main == "art");
        SetPanelColor(curCreativityText, false);
        SetPanelColor(expDevelopText,    main == "develop");
        SetPanelColor(expPlanningText,   main == "planning");
        SetPanelColor(expArtText,        main == "art");
        SetPanelColor(expCreativityText, false);
    }

    // stat 텍스트의 부모 패널 Image 색을 설정 (부모에 Image 가 있다는 구조 전제)
    static void SetPanelColor(TextMeshProUGUI statText, bool isMain)
    {
        if (statText == null || statText.transform.parent == null) return;
        var img = statText.transform.parent.GetComponent<Image>();
        if (img != null) img.color = isMain ? MainStatPanelColor : Color.white;
    }

    // ── BadgePanel (EmployeeListUI 와 동일하게 선택 직원 동기화: 역할/강화/잠재력/등급) ──
    void UpdateBadge(EmployeeData emp)
    {
        ResolveBadge();
        RoleIconSet.Apply(_badgeRoleIcon, roleIconSet, emp.role);
        SetText(_badgeEnhanceText,   $"+{emp.enhancementLevel}");
        SetText(_badgePotentialText, emp.PotentialToString());
        SetText(_badgeGradeText,     emp.GradeToString().ToUpper());
        GradeSpriteSet.Apply(_badgeGradeBG, gradeBGSet, emp.grade);
    }

    void ResolveBadge()
    {
        if (_badgeResolved || badgePanel == null) return;
        _badgeResolved = true;
        _badgeRoleIcon      = FindImage(badgePanel, "roleIcon");
        _badgeEnhanceText   = FindText(badgePanel, "enhancementText");
        _badgePotentialText = FindText(badgePanel, "potentialText");
        _badgeGradeText     = FindText(badgePanel, "gradeText");
        _badgeGradeBG       = FindImage(badgePanel, "gradeBG");
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
    static Image FindImage(Transform root, string name) { var t = FindDeep(root, name); return t != null ? t.GetComponent<Image>() : null; }
    static TextMeshProUGUI FindText(Transform root, string name) { var t = FindDeep(root, name); return t != null ? t.GetComponent<TextMeshProUGUI>() : null; }

    static string MainStatKey(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner    => "planning",
        EmployeeRole.Programmer => "develop",
        EmployeeRole.Artist     => "art",
        _                       => "develop"
    };

    static bool StatIsMain(EmployeeData emp, string stat) => MainStatKey(emp.role) == stat;

    static string ExpStat(string label, int cur, (int min, int max) gain)
    {
        int lo = cur + gain.min;
        int hi = cur + gain.max;
        return gain.min == gain.max ? $"{label}: {lo}" : $"{label}: {lo}~{hi}";
    }

    static void SetText(TextMeshProUGUI t, string s)
    {
        if (t != null) t.text = s;
    }
}
