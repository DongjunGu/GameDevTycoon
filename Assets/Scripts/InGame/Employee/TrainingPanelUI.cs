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

        // 선택 해제 시 이전(또는 에디터 디자인타임) 텍스트가 남아있지 않도록 전부 비움.
        SetText(curEnhanceText, "");    SetText(curDevelopText, "");   SetText(curPlanningText, "");
        SetText(curArtText, "");        SetText(curCreativityText, ""); SetText(curSalaryText, "");
        SetText(expEnhanceText, "");    SetText(expDevelopText, "");   SetText(expPlanningText, "");
        SetText(expArtText, "");        SetText(expCreativityText, ""); SetText(expSalaryText, "");
        SetText(successRateText, "");   SetText(failRateText, "");     SetText(costText, "");

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

        // 강화 전 수치 스냅샷 (결과 패널 before 표시 + 더미 텍스트 노출 방지)
        int oldLevel = _selected.enhancementLevel;
        int oldP = _selected.planningSkill, oldD = _selected.developSkill;
        int oldA = _selected.artSkill,      oldC = _selected.creativitySkill;

        var outcome = EmployeeEnhancement.EnhanceOnce(_selected);

        EmployeeManager.Instance.UpdateEmployee(_selected);
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();

        RefreshDetail();

        ShowEnhanceResult(outcome == EnhanceOutcome.Success, oldLevel, oldP, oldD, oldA, oldC);
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

    // ════════════════ 강화 결과 패널 (성공/실패) 애니메이션 ════════════════
    [Header("Result Animation (TrainingResultPanel)")]
    [Tooltip("TrainingResultPanel 루트만 연결 — 하위 오브젝트는 이름으로 자동 탐색")]
    public GameObject trainingResultPanel;
    [Tooltip("TrainingSuccessPanel 안의 PortraitImage")]
    public Image successPortraitImage;
    [Tooltip("TrainingFailPanel 안의 PortraitImage")]
    public Image failPortraitImage;
    [Tooltip("스탯 패널이 아래에서 위로 올라오는 거리(px)")]
    public float riseOffset = 60f;

    bool _resultResolved;
    GameObject _successRoot, _failRoot, _ellipse;
    GameObject _nameTextGo, _enhBeforeGo, _enhArrowGo, _enhAfterGo;
    RectTransform _resultImageRT, _detailRT;
    TextMeshProUGUI _nameText, _enhBefore, _enhAfter, _touchText, _failDetailText;
    Button[] _confirmBtns; // [0]=SuccessPanel, [1]=FailPanel
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
        _ellipse = FindDeep(root, "EllipseImage")?.gameObject;
        _resultImageRT = FindDeep(root, "ResultImage") as RectTransform;
        _detailRT      = FindDeep(root, "ResultDetailPanel") as RectTransform;
        _vlg           = _detailRT != null ? _detailRT.GetComponent<VerticalLayoutGroup>() : null;

        _nameText       = FindText(root, "nameText");      _nameTextGo  = _nameText  != null ? _nameText.gameObject  : null;
        _enhBefore      = FindText(root, "beforeText");    _enhBeforeGo = _enhBefore != null ? _enhBefore.gameObject : null;
        _enhAfter       = FindText(root, "afterText");     _enhAfterGo  = _enhAfter  != null ? _enhAfter.gameObject  : null;
        var arrow       = FindDeep(root, "arrowText");     _enhArrowGo  = arrow != null ? arrow.gameObject : null;
        _touchText      = FindText(root, "TouchText");
        _failDetailText = FindText(root, "FailDetailText");

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

        _confirmBtns = new Button[2];
        GameObject[] roots = { _successRoot, _failRoot };
        for (int i = 0; i < 2; i++)
        {
            if (roots[i] == null) continue;
            var btn = FindDeep(roots[i].transform, "ConfirmBtn")?.GetComponent<Button>();
            if (btn == null) continue;
            btn.onClick.RemoveListener(OnClickResultConfirm);
            btn.onClick.AddListener(OnClickResultConfirm);
            _confirmBtns[i] = btn;
        }
    }

    static TextMeshProUGUI TmpUnder(Transform root, string childName)
    {
        var c = FindDeep(root, childName);
        return c != null ? c.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    // 성공이면 TrainingSuccessPanel 애니메이션, 아니면 TrainingFailPanel 표시.
    void ShowEnhanceResult(bool success, int oldLevel, int oldP, int oldD, int oldA, int oldC)
    {
        ResolveResultRefs();
        if (trainingResultPanel == null) return;

        KillResultTweens();
        trainingResultPanel.SetActive(true);
        SetConfirmInteractable(false); // 애니 끝날 때까지 터치 차단
        if (_successRoot != null) _successRoot.SetActive(success);
        if (_failRoot    != null) _failRoot.SetActive(!success);
        ApplyPortrait(_selected);

        if (success)
        {
            PlaySuccessAnim(oldLevel, oldP, oldD, oldA, oldC);
        }
        else
        {
            // 실패: EllipseImage 숨김 + 캐릭터 이름 삽입 + TouchText 깜빡임
            SetActiveSafe(_ellipse, false);
            string empName = _selected != null ? _selected.employeeName : "";
            SetText(_failDetailText, $"'{empName}' 강화에 성공하지 못했습니다");
            SetConfirmInteractable(true);
            var failTouch = _failRoot != null ? FindText(_failRoot.transform, "TouchText") : null;
            var touch = failTouch ?? _touchText;
            if (touch != null)
            {
                touch.gameObject.SetActive(true);
                _blinkTween = touch.DOFade(0.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
        }
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
        SetActiveSafe(successPortraitImage?.gameObject, false);
        if (_resultImageRT != null) { _resultImageRT.gameObject.SetActive(true); SetScaleY(_resultImageRT, 0f); }
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

        // 1) 0.05초 후 Ellipse + Portrait 활성
        _animSeq.AppendInterval(0.05f);
        _animSeq.AppendCallback(() => { SetActiveSafe(_ellipse, true); SetActiveSafe(successPortraitImage?.gameObject, true); });

        // 2) ResultImage ScaleY 0→1
        if (_resultImageRT != null)
            _animSeq.Append(_resultImageRT.DOScaleY(1f, 0.28f).SetEase(Ease.OutCubic).SetUpdate(true));

        // 3) beforeText → arrowText → afterText 순차 활성화
        _animSeq.AppendCallback(() => { SetActiveSafe(_nameTextGo, true); SetActiveSafe(_enhBeforeGo, true); });
        _animSeq.AppendInterval(0.1f);
        _animSeq.AppendCallback(() => SetActiveSafe(_enhArrowGo, true));
        _animSeq.AppendInterval(0.1f);
        _animSeq.AppendCallback(() => SetActiveSafe(_enhAfterGo, true));
        _animSeq.AppendInterval(0.5f);

        // 4) 스탯 패널 4개 동시 fade in (stagger 0.06s)
        // HorizontalLayoutGroup 자식 위치 조작 불가 → 패널 자체 CanvasGroup alpha 사용
        float fadeDur = 0.35f;
        float stagger = 0.06f;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            _animSeq.AppendCallback(() =>
            {
                if (_statPanelCGs[idx] != null)
                    _statPanelCGs[idx].DOFade(1f, fadeDur).SetUpdate(true);
            });
            if (i < 3) _animSeq.AppendInterval(stagger);
        }
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
    }

    static void SetActiveSafe(GameObject go, bool on) { if (go != null) go.SetActive(on); }
    static void SetScaleY(RectTransform rt, float y) { var s = rt.localScale; rt.localScale = new Vector3(s.x, y, s.z); }
}
