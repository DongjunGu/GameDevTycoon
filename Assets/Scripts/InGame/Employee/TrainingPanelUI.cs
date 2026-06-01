using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 단일 화면 직원 강화 패널.
//  - TrainingRightScrollView(listContent): 직원 목록(슬롯 프리팹은 인스펙터 할당, TrainingSlotUI 재사용)
//  - TrainingLeftPanel: TopPanel / SecondPanel(SPLeft·SPRight) / ThirdPanel(Success·Fail) / BottomPanel
//    → 직원 미선택 시 4개 패널 비활성, 슬롯 클릭 시 활성 + 상세 표시.
// 강화 비용/확률/롤은 EmployeeEnhancement(공유), 예상 증가량은 EmployeeManager.GetNext*StatGain 사용.
public class TrainingPanelUI : MonoBehaviour
{
    public static TrainingPanelUI Instance { get; private set; }

    [Header("Panel Root (열기/닫기 토글 대상)")]
    public GameObject panelRoot;            // TrainingPanel. 비우면 이 컴포넌트의 GameObject 사용.

    [Header("List (TrainingRightScrollView)")]
    public Transform listContent;          // 슬롯이 생성될 부모 (ScrollView Content)
    public GameObject employeeSlotPrefab;   // TrainingSlotUI 가 붙은 슬롯 프리팹

    [Header("Left Panels (선택 전 비활성)")]
    public GameObject topPanel;
    public GameObject secondPanel;
    public GameObject thirdPanel;
    public GameObject bottomPanel;

    [Header("TopPanel (선택)")]
    public TextMeshProUGUI topNameText;
    public TextMeshProUGUI topRoleText;
    public TextMeshProUGUI topPotentialText;  // 잠재력 표시 (등급은 portraitBGImage 색으로 표시)
    public Image portraitBGImage;             // 등급 색 배경

    [Header("SPLeftPanel (현재)")]
    public Image portraitImage;
    public TextMeshProUGUI curEnhanceText;
    public TextMeshProUGUI curDevelopText;
    public TextMeshProUGUI curPlanningText;
    public TextMeshProUGUI curArtText;
    public TextMeshProUGUI curCreativityText;

    [Header("SPRightPanel (강화 후 예상)")]
    public TextMeshProUGUI expEnhanceText;
    public TextMeshProUGUI expDevelopText;
    public TextMeshProUGUI expPlanningText;
    public TextMeshProUGUI expArtText;
    public TextMeshProUGUI expCreativityText;

    [Header("ThirdPanel")]
    public TextMeshProUGUI successRateText; // SuccessPanel 자식
    public TextMeshProUGUI failRateText;    // FailPanel 자식

    [Header("BottomPanel")]
    public TextMeshProUGUI costText;
    public Button enhanceButton;
    public TextMeshProUGUI enhanceButtonText; // 선택
    public Button closeButton;                // 선택 (OnClickClose 자동 연결)

    private EmployeeData _selected;
    private System.Action _onClosed; // 카드 컨텍스트 등에서 닫힐 때 1회 호출
    private Coroutine _gradeCo;
    private readonly Dictionary<EmployeeData, TrainingEmployeeSlotUI> _slotMap = new();

    GameObject Root => panelRoot != null ? panelRoot : gameObject;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (enhanceButton != null)
        {
            enhanceButton.onClick.RemoveListener(OnClickEnhance);
            enhanceButton.onClick.AddListener(OnClickEnhance);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnClickClose);
            closeButton.onClick.AddListener(OnClickClose);
        }
    }

    // ── 열기/닫기 ─────────────────────────────
    public void OpenPanel()
    {
        _onClosed = null;
        GameTimeManager.Instance?.StopTime();
        Root.SetActive(true);
        HideDetail();
        PopulateList();
    }

    // 특정 직원 강화 패널을 바로 표시 (EmployeeCardUI '강화하기' 등). onClosed 는 닫힐 때 1회 호출.
    public void OpenForEmployee(EmployeeData emp, System.Action onClosed = null)
    {
        if (emp == null) return;
        _onClosed = onClosed;
        GameTimeManager.Instance?.StopTime();
        Root.SetActive(true);
        PopulateList();
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

    // ── 목록 ─────────────────────────────────
    void PopulateList()
    {
        foreach (Transform child in listContent)
            Destroy(child.gameObject);
        _slotMap.Clear();

        if (EmployeeManager.Instance == null || employeeSlotPrefab == null) return;

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            var go = Instantiate(employeeSlotPrefab, listContent);
            var slot = go.GetComponent<TrainingEmployeeSlotUI>();
            if (slot == null) continue;
            slot.Setup(emp, OnSelectEmployee);
            _slotMap[emp] = slot;
        }
    }

    // ── 선택 ─────────────────────────────────
    void OnSelectEmployee(EmployeeData emp)
    {
        _selected = emp;
        SetDetailPanelsActive(true);
        RefreshDetail();
    }

    void HideDetail()
    {
        _selected = null;
        SetDetailPanelsActive(false);
    }

    void SetDetailPanelsActive(bool active)
    {
        if (topPanel != null)    topPanel.SetActive(active);
        if (secondPanel != null) secondPanel.SetActive(active);
        if (thirdPanel != null)  thirdPanel.SetActive(active);
        if (bottomPanel != null) bottomPanel.SetActive(active);
    }

    // ── 상세 갱신 ─────────────────────────────
    void RefreshDetail()
    {
        var emp = _selected;
        if (emp == null) return;

        // 초상화
        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }

        // TopPanel — 등급은 색(portraitBGImage), 텍스트는 잠재력
        SetText(topNameText,      emp.employeeName);
        SetText(topRoleText,      emp.RoleToString());
        SetText(topPotentialText, emp.PotentialToString());
        ApplyGradeColor(emp.grade);
        ColorStatPanels(emp); // 주스탯 패널만 강조색, 나머지 흰색

        // SPLeftPanel — 현재 수치 (raw 스킬 기준)
        SetText(curEnhanceText,    $"현재 : Lv{emp.enhancementLevel}");
        SetText(curDevelopText,    $"개발: {emp.developSkill}");
        SetText(curPlanningText,   $"기획: {emp.planningSkill}");
        SetText(curArtText,        $"아트: {emp.artSkill}");
        SetText(curCreativityText, $"창의성: {emp.creativitySkill}");

        if (EmployeeEnhancement.IsMax(emp))
        {
            // 예상수치 — 최대치 (변화 없음)
            SetText(expEnhanceText,    "현재 : MAX");
            SetText(expDevelopText,    $"개발: {emp.developSkill}");
            SetText(expPlanningText,   $"기획: {emp.planningSkill}");
            SetText(expArtText,        $"아트: {emp.artSkill}");
            SetText(expCreativityText, $"창의성: {emp.creativitySkill}");

            SetText(successRateText, "성공확률 : -");
            SetText(failRateText,    "실패 확률: -");
            SetText(costText,        "필요한 재화 : -");

            if (enhanceButton != null) enhanceButton.interactable = false;
            SetText(enhanceButtonText, "최대치입니다");
            return;
        }

        var mainGain = EmployeeManager.Instance.GetNextMainStatGain(emp);
        // 부스탯은 범위 대신 평균(단일값)으로 표시 — 주스탯만 범위
        int subAvg = EmployeeManager.Instance.GetNextSubStatGainAvg(emp);
        (int min, int max) subGain = (subAvg, subAvg);

        // SPRightPanel — 강화 후 예상 (현재 + 증가 범위)
        SetText(expEnhanceText,    $"다음 : Lv{emp.enhancementLevel + 1}");
        SetText(expDevelopText,    ExpStat("개발",   emp.developSkill,    StatIsMain(emp, "develop")  ? mainGain : subGain));
        SetText(expPlanningText,   ExpStat("기획",   emp.planningSkill,   StatIsMain(emp, "planning") ? mainGain : subGain));
        SetText(expArtText,        ExpStat("아트",   emp.artSkill,        StatIsMain(emp, "art")      ? mainGain : subGain));
        SetText(expCreativityText, ExpStat("창의성", emp.creativitySkill, subGain)); // 창의성은 항상 부스탯

        // ThirdPanel — 성공/실패 확률 (실패 = 100 - 성공)
        int success = EmployeeEnhancement.SuccessRate(emp);
        SetText(successRateText, $"성공확률 : {success}%");
        SetText(failRateText,    $"실패 확률: {100 - success}%");

        // BottomPanel — 필요한 재화
        int cost = EmployeeEnhancement.GetCost(emp);
        SetText(costText, $"필요한 재화 : {cost:N0}G");

        if (enhanceButton != null) enhanceButton.interactable = true;
        SetText(enhanceButtonText, "강화하기");
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

        // 패널 + 해당 슬롯 갱신
        RefreshDetail();
        RefreshSelectedSlot();
    }

    void RefreshSelectedSlot()
    {
        if (_selected != null && _slotMap.TryGetValue(_selected, out var slot) && slot != null)
            slot.Setup(_selected, OnSelectEmployee);
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

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (portraitBGImage == null) return;
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        _gradeCo = EmployeeGradeColor.Apply(this, portraitBGImage, grade);
    }

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
