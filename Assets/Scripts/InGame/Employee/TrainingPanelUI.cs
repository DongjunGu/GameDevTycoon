using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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
    [Tooltip("RatePanel/RateChildPanel/DownText — 하락확률. 0이면 GameObject 자체를 비활성화")]
    public TextMeshProUGUI downRateText;

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

    // 하급/중급/상급 강화권 예약 — ItemManager 가 단일 소스(사용하기 클릭 즉시 소모 + 서버 저장,
    // 재접속해도 유지). 여기선 로컬 상태를 따로 들지 않고 매번 조회한다.
    ItemChartRow PendingBoostRow => ItemManager.Instance?.PendingBoostRow;

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

        // 결과 오버레이는 강화 결과가 나올 때만 표시 — 시작 시 숨김.
        if (trainingResultPanel != null) trainingResultPanel.SetActive(false);

        // 선택 전(첫 실행 포함) 에디터 디자인타임 더미 텍스트가 그대로 노출되지 않도록 시작 시 비움.
        HideDetail();
    }

    // ── 열기/닫기 ─────────────────────────────
    public void OpenPanel()
    {
        _onClosed = null;
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        Root.SetActive(true);
        HideDetail();
    }

    // 특정 직원 강화 패널을 바로 표시 (EmployeeCardUI/EmployeeListUI '강화하기'). onClosed 는 닫힐 때 1회 호출.
    public void OpenForEmployee(EmployeeData emp, System.Action onClosed = null)
    {
        if (emp == null) return;
        _onClosed = onClosed;
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        Root.SetActive(true);
        OnSelectEmployee(emp);
    }

    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
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
        // 예약된 강화권(있다면)은 ItemManager 가 서버에 들고 있는 상태라 여기선 건드리지 않는다 —
        // 선택 해제/패널 닫기와 무관하게 실제 강화에 쓰일 때까지 유지돼야 한다.

        // 선택 해제 시 이전(또는 에디터 디자인타임) 텍스트가 남아있지 않도록 전부 비움.
        SetText(curEnhanceText, "");    SetText(curDevelopText, "");   SetText(curPlanningText, "");
        SetText(curArtText, "");        SetText(curCreativityText, ""); SetText(curSalaryText, "");
        SetText(expEnhanceText, "");    SetText(expDevelopText, "");   SetText(expPlanningText, "");
        SetText(expArtText, "");        SetText(expCreativityText, ""); SetText(expSalaryText, "");
        SetText(successRateText, "");   SetText(failRateText, "");     SetText(costText, "");
        SetText(downRateText, "");
        if (downRateText != null) downRateText.gameObject.SetActive(false);

        ResolveBadge();
        SetText(_badgeEnhanceText, ""); SetText(_badgePotentialText, ""); SetText(_badgeGradeText, "");

        if (enhanceButton != null) enhanceButton.interactable = false;
    }

    // ── 상세 갱신 ─────────────────────────────
    void RefreshDetail()
    {
        var emp = _selected;
        if (emp == null) return;

        UpdateBadge(emp);     // 역할/강화/잠재력/등급 (선택 직원 동기화)
        ColorStatPanels(emp); // 주스탯 패널만 강조색, 나머지 흰색

        // SPLeftPanel — 현재 수치 (raw 스킬 기준)
        SetText(curEnhanceText,    $"현재 +{emp.enhancementLevel}강");
        SetText(curDevelopText,    $"개발: {emp.developSkill}");
        SetText(curPlanningText,   $"기획: {emp.planningSkill}");
        SetText(curArtText,        $"아트: {emp.artSkill}");
        SetText(curCreativityText, $"창의성: {emp.creativitySkill}");
        SetText(curSalaryText,     $"연봉: {emp.salary:N0} G");

        if (EmployeeEnhancement.IsMax(emp))
        {
            // 예상수치 — 최대치 (변화 없음)
            SetText(expEnhanceText,    "현재 MAX");
            SetText(expDevelopText,    $"개발: {emp.developSkill}");
            SetText(expPlanningText,   $"기획: {emp.planningSkill}");
            SetText(expArtText,        $"아트: {emp.artSkill}");
            SetText(expCreativityText, $"창의성: {emp.creativitySkill}");
            SetText(expSalaryText,     $"연봉: {emp.salary:N0} G"); // MAX — 연봉 변화 없음

            SetText(successRateText, "성공확률 : -");
            SetText(failRateText,    "실패확률: -");
            SetText(costText,        "-");
            if (downRateText != null) downRateText.gameObject.SetActive(false);

            if (enhanceButton != null) enhanceButton.interactable = false;
            return;
        }

        var mainGain = EmployeeManager.Instance.GetNextMainStatGain(emp);
        // 부스탯은 범위 대신 평균(단일값)으로 표시 — 주스탯만 범위
        int subAvg = EmployeeManager.Instance.GetNextSubStatGainAvg(emp);
        (int min, int max) subGain = (subAvg, subAvg);

        // SPRightPanel — 강화 후 예상 (현재 + 증가 범위)
        SetText(expEnhanceText,    $"강화 후 +{emp.enhancementLevel + 1}강");
        SetText(expDevelopText,    ExpStat("개발",   emp.developSkill,    StatIsMain(emp, "develop")  ? mainGain : subGain));
        SetText(expPlanningText,   ExpStat("기획",   emp.planningSkill,   StatIsMain(emp, "planning") ? mainGain : subGain));
        SetText(expArtText,        ExpStat("아트",   emp.artSkill,        StatIsMain(emp, "art")      ? mainGain : subGain));
        SetText(expCreativityText, ExpStat("창의성", emp.creativitySkill, subGain)); // 창의성은 항상 부스탯

        // 강화 후 연봉 = 현재 + 다음 강화 연봉 상승량(금수저 반영)
        SetText(expSalaryText, $"연봉: {emp.salary + EmployeeManager.Instance.GetNextSalaryGain(emp):N0} G");

        // ThirdPanel — 성공/실패/하락 확률. 예약된 강화권(ItemManager.PendingBoostRow)이 있으면 기존
        // 확률에 취소선+반투명을 걸고 그 옆에 보너스 반영된 확률을 같이 보여준다(성공/실패만 — 하락확률은
        // 항상 보정된 값 하나만 표시).
        int pendingBoostPercent = PendingBoostRow?.effectValue ?? 0;
        var baseRates = EmployeeEnhancement.GetRates(emp);
        if (pendingBoostPercent > 0)
        {
            var boosted = EmployeeEnhancement.GetRates(emp, pendingBoostPercent);
            SetText(successRateText, RateWithBoostText("성공확률", baseRates.success,       boosted.success));
            SetText(failRateText,    RateWithBoostText("실패확률", 100f - baseRates.success, 100f - boosted.success));
            SetDownRate(boosted.downgrade);
        }
        else
        {
            SetText(successRateText, $"성공확률 : {Mathf.RoundToInt(baseRates.success)}%");
            SetText(failRateText,    $"실패확률 : {Mathf.RoundToInt(100f - baseRates.success)}%");
            SetDownRate(baseRates.downgrade);
        }

        // BottomPanel — 필요한 재화
        int cost = EmployeeEnhancement.GetCost(emp);
        SetText(costText, $"{cost:N0} G");

        if (enhanceButton != null) enhanceButton.interactable = true;
    }

    // ── 강화 실행 ─────────────────────────────
    public void OnClickEnhance()
    {
        if (_selected == null || EmployeeEnhancement.IsMax(_selected)) return;

        // 온보딩 튜토리얼 17-2~17-6(강화 4연속 강제성공 체험) 전체는 하나의 원자적 구간 — 이 클릭이
        // 그 구간에 속하면(ForceSuccessRemaining>0, EnhanceOnce가 곧 이 값을 소비하기 전에 캡처) 서버에
        // 즉시 반영하지 않고 로컬에만 적용해둔다. 도중에 재접속이 끊겨도 서버 상태가 안 바뀐 채로 남아야
        // TutorialController가 17-1부터 안전하게 재생할 수 있다(중복 골드 차감/소프트락 방지). 실제 반영은
        // TutorialController.PlayTutorial17_6이 구간 끝에서 한 번에 몰아 저장한다.
        bool deferSave = EmployeeEnhancement.ForceSuccessRemaining > 0;

        int cost = EmployeeEnhancement.GetCost(_selected);
        if (cost < 0) return;
        if (!MoneyManager.Instance.SpendGold(cost, saveImmediately: !deferSave))
        {
            // TrainingPanelUI 자신이 Open()에서 ModalGate.Register(this)로 게이트를 쥔 채 열려있는 상태라
            // bypassGate 없이 부르면 패널이 열려있는 동안 안 뜬다(MerchantShopPanelUI와 동일 원인).
            AlertUI.Instance?.Show("자금이 부족합니다.", null, bypassGate: true);
            return;
        }

        // 강화 전 수치 스냅샷 (결과 패널 before 표시 + 더미 텍스트 노출 방지)
        int oldLevel = _selected.enhancementLevel;
        int oldP = _selected.planningSkill, oldD = _selected.developSkill;
        int oldA = _selected.artSkill,      oldC = _selected.creativitySkill;

        // 예약된 강화권은 이번 클릭 1회로 소비 — 결과(성공/유지/하락/방어) 무관하게 예약 해제.
        // 아이템 자체는 이미 "사용하기" 클릭 시점(ItemManager.ReserveEnhanceBoost)에 소모+저장됐으므로
        // 여기선 값만 읽고 예약을 지운다.
        var boostRow = PendingBoostRow;
        int boost = boostRow?.effectValue ?? 0;
        if (boostRow != null) ItemManager.Instance.ClearPendingBoost();

        var emp = _selected; // ShowPortrait 콜백에서 RefreshDetail 이후에도 안전하게 쓰려고 미리 캡처
        var outcome = EmployeeEnhancement.EnhanceOnce(_selected, boost);

        if (!deferSave)
        {
            EmployeeManager.Instance.UpdateEmployee(_selected);
            GameTimeManager.Instance?.SaveGameTime();
            ProjectSaveManager.Instance?.SaveProject();
        }

        RefreshDetail();
        EmployeeListUI.Instance?.RefreshSlotLevelText(emp.id); // 우측 목록 슬롯 LevelText 즉시 갱신
        EmployeeStatusBarUI.Instance?.RefreshSlot(emp.id);     // 하단 상태바 슬롯 enhancementText 즉시 갱신

        ShowEnhanceResult(outcome, oldLevel, oldP, oldD, oldA, oldC);
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

    // 강화권 보너스 적용 전/후를 한 줄로 — 기존 값은 취소선+반투명(알파 절반), 옆에 보정된 값은 원래 밝기.
    // <s>...</s> 는 정상적으로 닫히지만, TMP 의 <alpha> 태그는 </alpha> 닫는 태그를 지원하지 않는다
    // (닫히지 않고 리터럴 텍스트로 새며 뒤 텍스트까지 계속 반투명 적용됨) — 그래서 알파만 <alpha=#FF>로
    // 다시 불투명하게 되돌리고, 취소선은 </s>로 정상적으로 닫는다.
    static string RateWithBoostText(string label, float oldPct, float newPct)
    {
        int o = Mathf.RoundToInt(oldPct);
        int n = Mathf.RoundToInt(newPct);
        return $"{label} : <alpha=#80><s>{o}%</s><alpha=#FF> {n}%";
    }

    // 하락확률 텍스트 — 0이면 GameObject 자체를 비활성화(요청 사양).
    void SetDownRate(float downgrade)
    {
        if (downRateText == null) return;
        int d = Mathf.RoundToInt(downgrade);
        downRateText.gameObject.SetActive(d > 0);
        if (d > 0) downRateText.text = $"하락확률 : {d}%";
    }

    // ════════════════ 강화 결과 패널 (성공/실패) 애니메이션 ════════════════
    [Header("Result Animation (TrainingResultPanel)")]
    [Tooltip("TrainingResultPanel 루트만 연결 — 하위 오브젝트는 이름으로 자동 탐색")]
    public GameObject trainingResultPanel;
    [Tooltip("TrainingSuccessPanel 안의 PortraitImage")]
    public Image successPortraitImage;
    [Tooltip("TrainingFailPanel 안의 PortraitImage")]
    public Image failPortraitImage;
    [Tooltip("SuccessPanel/EllipseImage/ShinePanel이 아래에서 위로 올라오는 거리(px)")]
    public float riseOffset = 60f;

    [Tooltip("Portrait 뒤에서 가운데서 펼쳐지듯 나타나는 글로우 연출 시간(초)")]
    public float portraitEllipseRevealDuration = 0.35f;
    [Tooltip("EllipseImage 최종 alpha (0~255, 기존 리소스 기준 36)")]
    public float portraitEllipseRestAlpha255 = 36f;

    [Header("EllipseImage/ShineEffect(들) 반짝임 — EllipseImage 활성화된 동안만 재생")]
    [Tooltip("각 ShineEffect가 다음 반짝임까지 대기하는 시간 범위(초) — 서로 안 겹치게 랜덤")]
    public float shineMinInterval = 0.2f;
    public float shineMaxInterval = 1.0f;
    [Tooltip("페이드 인/아웃 각각의 시간(초)")]
    public float shineFadeDuration = 0.3f;

    [Header("FaillImage/sprinkle(들) — 강화 실패 시 제자리에서 반짝임+맥동, TrainingFailPanel 활성 동안만 재생")]
    [Tooltip("스케일 1↔1.3 왕복 1회 소요 시간(초) 범위 — 각 sprinkle마다 랜덤이라 서로 안 맞물림")]
    public float sprinkleScaleMinDuration = 0.6f;
    public float sprinkleScaleMaxDuration = 1.1f;
    [Tooltip("알파가 새 랜덤값으로 바뀌는 데 걸리는 시간(초)")]
    public float sprinkleAlphaChangeDuration = 0.4f;
    [Tooltip("알파가 랜덤으로 오갈 범위(0~1)")]
    public float sprinkleAlphaMin = 0.3f;
    public float sprinkleAlphaMax = 1f;

    bool _resultResolved;
    // 유지=Fail / 하락=Down / 방어=Protected — 셋 다 TrainingFailPanel과 동일한 구조(FaillImage+sprinkle
    // 7개, TouchText, ConfirmBtn)를 그대로 복제한 패널이라 sprinkle/터치텍스트를 패널별로 따로 들고 있는다
    // (예전엔 실패 패널 1개뿐이라 전역 FindDeep 한 번으로 충분했지만, 이제 이름이 겹쳐서 루트별로 스코프해야 함).
    GameObject _successRoot, _failRoot, _downRoot, _protectedRoot, _ellipse, _shinePanel;
    Image[] _shineEffects;
    readonly List<Tween> _shineTweens = new();
    Image[] _sprinkles; // 현재 재생 중인 패널의 sprinkle 세트 — PlayFailStyleAnim 호출 시마다 갱신
    Image[] _failSprinkles, _downSprinkles, _protectedSprinkles;
    readonly List<Tween> _sprinkleTweens = new();
    RectTransform _portraitEllipseRT;
    Image _portraitEllipseImg;
    // SuccessPanel/ShinePanel — EllipseImage와 함께 "하단에서 올라오며 페이드"되는 3종. rest 위치는
    // ResolveResultRefs(최초 1회)에서 캡처해두고, 매 PlaySuccessAnim마다 rest-riseOffset → rest로 되돌린다.
    RectTransform _successRootRT, _shinePanelRT;
    CanvasGroup _successRootCG, _shinePanelCG;
    Vector2 _successRootRestPos, _shinePanelRestPos, _portraitEllipseRestPos, _successPortraitRestPos;
    GameObject _nameTextGo, _enhBeforeGo, _enhArrowGo, _enhAfterGo;
    RectTransform _resultImageRT, _detailRT;
    TextMeshProUGUI _nameText, _enhBefore, _enhAfter, _touchText, _failDetailText;
    TextMeshProUGUI _failTouchText, _downTouchText, _protectedTouchText;
    readonly List<Button> _confirmBtns = new(); // Success/Fail/Down/Protected 전부 — 전부 OnClickResultConfirm
    VerticalLayoutGroup _vlg;
    RectTransform[] _statPanels;
    CanvasGroup[]   _statPanelCGs; // 각 스탯 패널(PlanningPanel 등) CanvasGroup
    TextMeshProUGUI[] _statBefore, _statAfter;
    Sequence _animSeq;
    Tween _blinkTween;

    void ResolveResultRefs()
    {
        if (_resultResolved || trainingResultPanel == null) return;
        _resultResolved = true;
        var root = trainingResultPanel.transform;

        _successRoot   = FindDeep(root, "TrainingSuccessPanel")?.gameObject;
        _failRoot      = FindDeep(root, "TrainingFailPanel")?.gameObject;
        _downRoot      = FindDeep(root, "TrainingDownPanel")?.gameObject;
        _protectedRoot = FindDeep(root, "TrainingProtectedPanel")?.gameObject;
        _ellipse = FindDeep(root, "EllipseImage")?.gameObject;
        var portraitEllipseT = FindDeep(root, "EllipseImage");
        _portraitEllipseRT  = portraitEllipseT as RectTransform;
        _portraitEllipseImg = portraitEllipseT != null ? portraitEllipseT.GetComponent<Image>() : null;
        if (_portraitEllipseRT != null) _portraitEllipseRestPos = _portraitEllipseRT.anchoredPosition;
        if (successPortraitImage != null) _successPortraitRestPos = successPortraitImage.rectTransform.anchoredPosition;

        // SuccessPanel — 등장 시 "하단에서 올라오며 페이드" 연출용 CanvasGroup 확보 + rest 위치 캡처.
        if (_successRoot != null)
        {
            _successRootRT = _successRoot.transform as RectTransform;
            _successRootCG = _successRoot.GetComponent<CanvasGroup>();
            if (_successRootCG == null) _successRootCG = _successRoot.AddComponent<CanvasGroup>();
            if (_successRootRT != null) _successRootRestPos = _successRootRT.anchoredPosition;
        }

        // ShineEffect(들)은 이제 EllipseImage 자식이 아니라 별도 ShinePanel 아래에 있음 — ShinePanel을
        // 찾아 그 자식 중 "ShineEffect"로 시작하는 것 전부 수집(개수 가변 — ShineEffect, ShineEffect (1)...).
        // ShinePanel은 더 이상 EllipseImage의 자식이 아니므로 활성화도 EllipseImage와 별도로 맞춰줘야 한다.
        _shinePanel = FindDeep(root, "ShinePanel")?.gameObject;
        if (_shinePanel != null)
        {
            _shinePanelRT = _shinePanel.transform as RectTransform;
            _shinePanelCG = _shinePanel.GetComponent<CanvasGroup>();
            if (_shinePanelCG == null) _shinePanelCG = _shinePanel.AddComponent<CanvasGroup>();
            if (_shinePanelRT != null) _shinePanelRestPos = _shinePanelRT.anchoredPosition;
            var shines = new List<Image>();
            foreach (Transform child in _shinePanel.transform)
            {
                if (!child.name.StartsWith("ShineEffect")) continue;
                var img = child.GetComponent<Image>();
                if (img != null) shines.Add(img);
            }
            _shineEffects = shines.ToArray();
        }

        // Fail/Down/Protected 세 패널 모두 자기 하위에 FaillImage/.../sprinkle(들)을 각자 가지고 있음 —
        // 이름이 겹치므로(전부 "FaillImage"/"sprinkle") 반드시 각 패널 루트 밑으로 스코프해서 수집해야
        // 한다(전역으로 찾으면 항상 트리 순서상 첫 번째 것만 잡혀 나머지 패널이 빈 sprinkle을 갖게 됨).
        _failSprinkles      = _failRoot      != null ? CollectSprinkles(_failRoot.transform)      : null;
        _downSprinkles      = _downRoot      != null ? CollectSprinkles(_downRoot.transform)      : null;
        _protectedSprinkles = _protectedRoot != null ? CollectSprinkles(_protectedRoot.transform)  : null;

        _resultImageRT = FindDeep(root, "ResultImage") as RectTransform;
        _detailRT      = FindDeep(root, "ResultDetailPanel") as RectTransform;
        _vlg           = _detailRT != null ? _detailRT.GetComponent<VerticalLayoutGroup>() : null;

        _nameText       = FindText(root, "nameText");      _nameTextGo  = _nameText  != null ? _nameText.gameObject  : null;
        _enhBefore      = FindText(root, "beforeText");    _enhBeforeGo = _enhBefore != null ? _enhBefore.gameObject : null;
        _enhAfter       = FindText(root, "afterText");     _enhAfterGo  = _enhAfter  != null ? _enhAfter.gameObject  : null;
        var arrow       = FindDeep(root, "arrowText");     _enhArrowGo  = arrow != null ? arrow.gameObject : null;
        // TouchText는 4개 패널(Success/Fail/Down/Protected) 전부에 동일한 이름으로 있어서 각자 스코프해서 찾아야 함.
        _touchText          = _successRoot   != null ? FindText(_successRoot.transform,   "TouchText") : null;
        _failTouchText      = _failRoot      != null ? FindText(_failRoot.transform,      "TouchText") : null;
        _downTouchText      = _downRoot      != null ? FindText(_downRoot.transform,      "TouchText") : null;
        _protectedTouchText = _protectedRoot != null ? FindText(_protectedRoot.transform, "TouchText") : null;
        _failDetailText     = FindText(root, "FailDetailText"); // TrainingFailPanel 전용 — Down/Protected엔 없음

        string[] names = { "PlanningPanel", "DevPanel", "ArtPanel", "CreativityPanel" };
        _statPanels   = new RectTransform[4];
        _statPanelCGs = new CanvasGroup[4];
        _statBefore   = new TextMeshProUGUI[4]; _statAfter = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var p = FindDeep(root, names[i]) as RectTransform;
            _statPanels[i] = p;
            if (p == null) continue;
            // 패널 자체에 CanvasGroup 확보 (HorizontalLayoutGroup 자식 개별 조작 불가)
            var cg = p.GetComponent<CanvasGroup>();
            if (cg == null) cg = p.gameObject.AddComponent<CanvasGroup>();
            _statPanelCGs[i] = cg;
            _statBefore[i] = TmpUnder(p, "BeforeTextPanel");
            _statAfter[i]  = TmpUnder(p, "AfterPanel");
        }

        _confirmBtns.Clear();
        GameObject[] roots = { _successRoot, _failRoot, _downRoot, _protectedRoot };
        foreach (var r in roots)
        {
            if (r == null) continue;
            var btn = FindDeep(r.transform, "ConfirmBtn")?.GetComponent<Button>();
            if (btn == null) continue;
            btn.onClick.RemoveListener(OnClickResultConfirm);
            btn.onClick.AddListener(OnClickResultConfirm);
            _confirmBtns.Add(btn);
        }
    }

    // FaillImage 자식의 sprinkle(들) — Fail/Down/Protected 세 패널이 각자 동일한 구조를 복제해 가지고
    // 있어서, root를 스코프해 넘겨야 서로 안 섞인다.
    static Image[] CollectSprinkles(Transform panelRoot)
    {
        var faillImageT = FindDeep(panelRoot, "FaillImage");
        if (faillImageT == null) return null;
        var sprinkles = new List<Image>();
        foreach (Transform child in faillImageT)
        {
            if (!child.name.Equals("sprinkle", System.StringComparison.OrdinalIgnoreCase)) continue;
            var img = child.GetComponent<Image>();
            if (img != null) sprinkles.Add(img);
        }
        return sprinkles.ToArray();
    }

    static TextMeshProUGUI TmpUnder(Transform root, string childName)
    {
        var c = FindDeep(root, childName);
        return c != null ? c.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    // Success면 TrainingSuccessPanel 애니메이션. Maintain/Downgrade/Protected는 전부 같은 구조를 복제한
    // "실패풍" 패널(FaillImage+sprinkle+TouchText+ConfirmBtn) 중 해당하는 것 하나만 켠다.
    void ShowEnhanceResult(EnhanceOutcome outcome, int oldLevel, int oldP, int oldD, int oldA, int oldC)
    {
        ResolveResultRefs();
        if (trainingResultPanel == null) return;

        KillResultTweens();
        trainingResultPanel.SetActive(true);
        SetConfirmInteractable(false); // 애니 끝날 때까지 터치 차단

        bool success = outcome == EnhanceOutcome.Success;
        SetActiveSafe(_successRoot,   success);
        SetActiveSafe(_failRoot,      outcome == EnhanceOutcome.Maintain);
        SetActiveSafe(_downRoot,      outcome == EnhanceOutcome.Downgrade);
        SetActiveSafe(_protectedRoot, outcome == EnhanceOutcome.Protected);
        ApplyPortrait(_selected);

        if (success)
        {
            PlaySuccessAnim(oldLevel, oldP, oldD, oldA, oldC);
            return;
        }

        // 실패풍 공통 연출: EllipseImage(+ShinePanel) 숨김 + TouchText 깜빡임 + sprinkle 반짝임/맥동.
        SetActiveSafe(_ellipse, false);
        SetActiveSafe(_shinePanel, false);
        SetConfirmInteractable(true);

        TextMeshProUGUI touch;
        switch (outcome)
        {
            case EnhanceOutcome.Downgrade:
                _sprinkles = _downSprinkles;
                touch = _downTouchText;
                break;
            case EnhanceOutcome.Protected:
                _sprinkles = _protectedSprinkles;
                touch = _protectedTouchText;
                break;
            default: // Maintain — 기존 "실패" 패널. 성공하지 못했다는 문구는 여기서만 표시.
                _sprinkles = _failSprinkles;
                touch = _failTouchText ?? _touchText;
                string empName = _selected != null ? _selected.employeeName : "";
                SetText(_failDetailText, $"'{empName}' 강화에 성공하지 못했습니다");
                break;
        }

        if (touch != null)
        {
            touch.gameObject.SetActive(true);
            _blinkTween = touch.DOFade(0.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
        StartSprinkles();
    }

    void PlaySuccessAnim(int oldLevel, int oldP, int oldD, int oldA, int oldC)
    {
        var emp = _selected;

        // 텍스트 먼저 채움 (디자인타임 더미 노출 방지)
        SetText(_nameText,  emp != null ? emp.employeeName : "");
        SetText(_enhBefore, $"+{oldLevel}");
        SetText(_enhAfter,  $"+{(emp != null ? emp.enhancementLevel : oldLevel)}");
        SetStat(0, oldP, emp != null ? emp.planningSkill   : oldP);
        SetStat(1, oldD, emp != null ? emp.developSkill    : oldD);
        SetStat(2, oldA, emp != null ? emp.artSkill        : oldA);
        SetStat(3, oldC, emp != null ? emp.creativitySkill : oldC);

        // 초기 상태
        SetActiveSafe(_ellipse, false);
        SetActiveSafe(_shinePanel, false);
        SetActiveSafe(successPortraitImage?.gameObject, false);
        if (_resultImageRT != null) { _resultImageRT.gameObject.SetActive(true); SetScaleY(_resultImageRT, 0f); }
        // SuccessPanel/EllipseImage/ShinePanel — 전부 rest 위치에서 riseOffset만큼 아래로 내려놓고 alpha=0으로
        // 시작(하단에서 올라오며 페이드 인 연출용). localScale은 더 이상 안 건드림.
        if (_successRootRT != null) _successRootRT.anchoredPosition = _successRootRestPos - new Vector2(0f, riseOffset);
        if (_successRootCG != null) _successRootCG.alpha = 0f;
        if (_shinePanelRT  != null) _shinePanelRT.anchoredPosition  = _shinePanelRestPos  - new Vector2(0f, riseOffset);
        if (_shinePanelCG  != null) _shinePanelCG.alpha = 0f;
        if (_portraitEllipseRT != null)
        {
            _portraitEllipseRT.gameObject.SetActive(false);
            _portraitEllipseRT.localScale = Vector3.one;
            _portraitEllipseRT.anchoredPosition = _portraitEllipseRestPos - new Vector2(0f, riseOffset);
        }
        if (_portraitEllipseImg != null) { var c = _portraitEllipseImg.color; c.a = 0f; _portraitEllipseImg.color = c; } // 0에서 rest알파까지 페이드 인
        if (successPortraitImage != null)
        {
            var pc = successPortraitImage.color; pc.a = 0f; successPortraitImage.color = pc;
            // 위에서 아래로 내려오며 페이드 인 — rest보다 riseOffset만큼 위에서 시작.
            successPortraitImage.rectTransform.anchoredPosition = _successPortraitRestPos + new Vector2(0f, riseOffset);
        }
        SetActiveSafe(_nameTextGo, false);
        SetActiveSafe(_enhBeforeGo, false);
        SetActiveSafe(_enhArrowGo, false);
        SetActiveSafe(_enhAfterGo, false);
        if (_touchText != null) _touchText.gameObject.SetActive(false);

        // 스탯 패널 활성, 패널 자체 alpha=0 으로 숨김
        // (HorizontalLayoutGroup 하에서 자식 anchoredPosition 제어 불가 → 패널 단위 fade)
        for (int i = 0; i < 4; i++)
        {
            if (_statPanels[i] == null) continue;
            _statPanels[i].gameObject.SetActive(true);
            if (_statPanelCGs[i] != null) _statPanelCGs[i].alpha = 0f;
        }

        _animSeq = DOTween.Sequence().SetUpdate(true);

        // 1) 0.05초 후 SuccessPanel/EllipseImage/ShinePanel 동시 등장 — 셋 다 rest 위치보다 riseOffset만큼
        // 아래에서 올라오며(anchoredPosition) 동시에 alpha 0→목표값으로 페이드 인.
        _animSeq.AppendInterval(0.05f);
        _animSeq.AppendCallback(() =>
        {
            SetActiveSafe(_ellipse, true);
            SetActiveSafe(_shinePanel, true);
            if (_portraitEllipseRT != null) _portraitEllipseRT.gameObject.SetActive(true);
        });
        if (_successRootRT != null)
            _animSeq.Join(_successRootRT.DOAnchorPos(_successRootRestPos, portraitEllipseRevealDuration).SetEase(Ease.OutCubic).SetUpdate(true));
        if (_successRootCG != null)
            _animSeq.Join(_successRootCG.DOFade(1f, portraitEllipseRevealDuration).SetUpdate(true));
        if (_shinePanelRT != null)
            _animSeq.Join(_shinePanelRT.DOAnchorPos(_shinePanelRestPos, portraitEllipseRevealDuration).SetEase(Ease.OutCubic).SetUpdate(true));
        if (_shinePanelCG != null)
            _animSeq.Join(_shinePanelCG.DOFade(1f, portraitEllipseRevealDuration).SetUpdate(true));
        if (_portraitEllipseRT != null)
            _animSeq.Join(_portraitEllipseRT.DOAnchorPos(_portraitEllipseRestPos, portraitEllipseRevealDuration).SetEase(Ease.OutCubic).SetUpdate(true));
        if (_portraitEllipseImg != null)
            _animSeq.Join(_portraitEllipseImg.DOFade(Mathf.Clamp01(portraitEllipseRestAlpha255 / 255f), portraitEllipseRevealDuration).SetUpdate(true));

        // PortraitImage — 위에서 아래로 내려오면서(anchoredPosition) 동시에 alpha 0→1로 페이드 인.
        _animSeq.AppendCallback(() => SetActiveSafe(successPortraitImage?.gameObject, true));
        if (successPortraitImage != null)
        {
            _animSeq.Join(successPortraitImage.DOFade(1f, portraitEllipseRevealDuration).SetUpdate(true));
            _animSeq.Join(successPortraitImage.rectTransform
                .DOAnchorPos(_successPortraitRestPos, portraitEllipseRevealDuration).SetEase(Ease.OutCubic).SetUpdate(true));
        }

        // EllipseImage 펼쳐지는 연출이 끝난 직후부터 ShineEffect들이 각자 랜덤 타이밍으로 반짝이기 시작.
        _animSeq.AppendCallback(StartShineEffects);

        // 2) ResultImage ScaleY 0→1
        if (_resultImageRT != null)
            _animSeq.Append(_resultImageRT.DOScaleY(1f, 0.28f).SetEase(Ease.OutCubic).SetUpdate(true));

        // 3) beforeText → arrowText → afterText 순차 활성화. before/after는 알파는 그대로 두고
        // 자기 중심(pivot)에서 scale 0→1로 펼쳐지듯 등장(PlayCenterExpand) — arrowText는 기존처럼 그냥 활성화.
        _animSeq.AppendCallback(() => { SetActiveSafe(_nameTextGo, true); PlayCenterExpand(_enhBeforeGo); });
        _animSeq.AppendInterval(0.1f);
        _animSeq.AppendCallback(() => SetActiveSafe(_enhArrowGo, true));
        _animSeq.AppendInterval(0.1f);

        // 4) afterText 출력되는 시점에 스탯 패널 4개 전부 한꺼번에 fade in (stagger 없이 동시)
        // HorizontalLayoutGroup 하에서 자식 anchoredPosition 제어 불가 → 패널 자체 CanvasGroup alpha 사용
        float fadeDur = 0.35f;
        _animSeq.AppendCallback(() =>
        {
            PlayCenterExpand(_enhAfterGo);
            for (int i = 0; i < 4; i++)
                if (_statPanelCGs[i] != null)
                    _statPanelCGs[i].DOFade(1f, fadeDur).SetUpdate(true);
        });
        _animSeq.AppendInterval(fadeDur);

        // 5) TouchText 깜빡임 + ConfirmBtn 활성화
        _animSeq.AppendCallback(() =>
        {
            SetConfirmInteractable(true);
            if (_touchText == null) return;
            _touchText.gameObject.SetActive(true);
            _blinkTween = _touchText.DOFade(0.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        });
    }

    // beforeText/afterText 전용 — 알파는 건드리지 않고, 세로로 접혀있다가(scaleY 0) 펴지는(scaleY 0→1)
    // 느낌으로 등장. X는 항상 1 유지 — 균일 확대(줌인)가 아니라 접힌 종이가 펴지는 인상을 주기 위함.
    static void PlayCenterExpand(GameObject go, float duration = 0.2f)
    {
        if (go == null) return;
        var t = go.transform;
        t.localScale = new Vector3(1f, 0f, 1f);
        go.SetActive(true);
        t.DOScaleY(1f, duration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    void SetStat(int i, int before, int after)
    {
        SetText(_statBefore[i], before.ToString());
        SetText(_statAfter[i],  after.ToString());
    }

    // ConfirmBtn — 결과 한 번에 닫고 계속 강화 가능.
    public void OnClickResultConfirm()
    {
        KillResultTweens();
        if (trainingResultPanel != null) trainingResultPanel.SetActive(false);
        RefreshDetail();
    }

    void ApplyPortrait(EmployeeData emp)
    {
        if (emp == null || string.IsNullOrEmpty(emp.portraitId)) return;
        var sprite = Resources.Load<Sprite>($"Portraits/Mini/{emp.portraitId}");
        if (sprite == null) return;
        if (successPortraitImage != null) successPortraitImage.sprite = sprite;
        if (failPortraitImage    != null) failPortraitImage.sprite    = sprite;
    }

    void SetConfirmInteractable(bool v)
    {
        if (_confirmBtns == null) return;
        foreach (var b in _confirmBtns) if (b != null) b.interactable = v;
    }

    void KillResultTweens()
    {
        _animSeq?.Kill();    _animSeq    = null;
        _blinkTween?.Kill(); _blinkTween = null;
        StopShineEffects();
        StopSprinkles();
    }

    // FaillImage 자식 sprinkle들 — 제자리에서 스케일 1↔1.3 무한 왕복(개별 랜덤 주기) + 알파가 계속
    // 새 랜덤값으로 부드럽게 바뀜(둘은 서로 독립적인 루프). 강화 실패 결과가 뜨는 동안만 재생.
    void StartSprinkles()
    {
        StopSprinkles();
        if (_sprinkles == null) return;
        foreach (var img in _sprinkles)
        {
            if (img == null) continue;
            img.rectTransform.localScale = Vector3.one;
            var c = img.color; c.a = Random.Range(sprinkleAlphaMin, sprinkleAlphaMax); img.color = c;

            float scaleDur = Random.Range(sprinkleScaleMinDuration, sprinkleScaleMaxDuration);
            var scaleTween = img.rectTransform
                .DOScale(1.3f, scaleDur)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true).SetTarget(this);
            _sprinkleTweens.Add(scaleTween);

            ScheduleSprinkleAlpha(img);
        }
    }

    void ScheduleSprinkleAlpha(Image img)
    {
        if (img == null) return;
        float target = Random.Range(sprinkleAlphaMin, sprinkleAlphaMax);
        var t = img.DOFade(target, sprinkleAlphaChangeDuration)
            .SetUpdate(true).SetTarget(this)
            .OnComplete(() => ScheduleSprinkleAlpha(img));
        _sprinkleTweens.Add(t);
    }

    void StopSprinkles()
    {
        foreach (var t in _sprinkleTweens) t?.Kill();
        _sprinkleTweens.Clear();
        if (_sprinkles != null)
            foreach (var img in _sprinkles)
            {
                if (img == null) continue;
                img.rectTransform.localScale = Vector3.one;
                var c = img.color; c.a = 1f; img.color = c;
            }
    }

    // ShinePanel 자식 ShineEffect들 — 각자 랜덤 간격으로 알파 0→1→0을 반복(서로 안 겹치는 자연스러운 반짝임).
    void StartShineEffects()
    {
        StopShineEffects();
        if (_shineEffects == null) return;
        foreach (var img in _shineEffects)
        {
            if (img == null) continue;
            var c = img.color; c.a = 0f; img.color = c;
            ScheduleShine(img);
        }
    }

    void ScheduleShine(Image img)
    {
        float delay = Random.Range(shineMinInterval, shineMaxInterval);
        var t = DOVirtual.DelayedCall(delay, () => PlayShineCycle(img)).SetUpdate(true).SetTarget(this);
        _shineTweens.Add(t);
    }

    void PlayShineCycle(Image img)
    {
        if (img == null) return;
        var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        seq.Append(img.DOFade(1f, shineFadeDuration));
        seq.Append(img.DOFade(0f, shineFadeDuration));
        seq.OnComplete(() => ScheduleShine(img));
        _shineTweens.Add(seq);
    }

    void StopShineEffects()
    {
        foreach (var t in _shineTweens) t?.Kill();
        _shineTweens.Clear();
        if (_shineEffects != null)
            foreach (var img in _shineEffects)
            {
                if (img == null) continue;
                var c = img.color; c.a = 0f; img.color = c;
            }
    }

    static void SetActiveSafe(GameObject go, bool on) { if (go != null) go.SetActive(on); }
    static void SetScaleY(RectTransform rt, float y) { var s = rt.localScale; rt.localScale = new Vector3(s.x, y, s.z); }
}
