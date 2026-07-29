using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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

    [Header("Snap Scroll (세로 캐러셀 — 비우면 slotParent 부모에서 자동 탐색)")]
    public VerticalSnapList snapList;

    [Header("Detail toggle")]
    public GameObject leftPanel;          // ListLeftPanel — 선택 시 활성
    public GameObject emptyPanel;         // 선택 전 placeholder — leftPanel 과 반대로 토글

    [Header("Detail (ListLeftPanel) — EmployeeCardUI 와 동일 항목")]
    public Image portraitImage;
    [Tooltip("PortraitPanel 자식 NameFrame — 직원 0명 시 비활성")]
    public GameObject nameFrame;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI potentialText;     // "잠재력: {}"
    public TextMeshProUGUI gradeText;
    [Tooltip("등급별 BG 스프라이트 (EmployeeCardUI 와 동일 — 공용 GradeSpriteSet). 색 변경은 사용 안 함, 스프라이트 교체만.")]
    public Image gradeBG;
    public GradeSpriteSet gradeBGSet;
    public Image roleBadge;                   // 역할 아이콘
    public RoleIconSet roleIconSet;           // 공용 역할 아이콘 세트
    public TextMeshProUGUI traitText;         // 특성명 (등급 무관 항상 출력, 클릭 시 설명)
    public TextMeshProUGUI eventText;         // 전용 이벤트명 (등급 무관 항상 출력, 클릭 시 설명)
    [Tooltip("특성 등급(Epic) 미충족 시 활성화되는 잠금 덮개")]
    public GameObject traitLockedPanel;
    [Tooltip("이벤트 등급(Unique) 미충족 시 활성화되는 잠금 덮개")]
    public GameObject eventLockedPanel;
    public TextMeshProUGUI enhancementText;   // "Lv.{}"
    public Slider satisfactionSlider;
    public SatisfactionFillSet satisfactionFillSet; // 구간별 Fill sprite 묶음 (공용 에셋)
    public TextMeshProUGUI satisfactionText;
    [Tooltip("EmployeeInfoPanel/ERSalaryPanel/SalaryValueText — 연봉 값만 표시(라벨은 별도 정적 UI)")]
    public TextMeshProUGUI salaryText;
    public GameObject dispatchedBadge;        // 파견중 badge (옵션)
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;

    [Header("Stat arrows — 능력치 옆 ArrowImage (버프=기본 / 디버프=Y축 flip / 무변화=숨김)")]
    public Image planningArrow;
    public Image developArrow;
    public Image artArrow;
    public Image creativityArrow;
    [Tooltip("화살표 스프라이트 1개. 버프는 그대로, 디버프는 Y축으로 뒤집어 사용")]
    public Sprite statArrowSprite;

    [Header("Buttons (BottomPanel)")]
    public Button enhanceButton;
    public Button fireButton;
    public Button itemButton;
    public Button closeButton;
    [Tooltip("아이템 사용 모드 전용 버튼 — 평소 비활성, OpenForUseItem 시에만 표시")]
    public Button useItemButton;
    [Tooltip("정원 초과 강제 해고 모드 전용 버튼 — 평소 비활성, OpenForForceFire 시에만 표시. 나머지 버튼은 전부 숨김.")]
    public Button fireEmployeeButton;

    [Header("Detail Slide (InfoPanel ↔ TrainingPanel)")]
    [Tooltip("DetailPanel 안의 InfoPanel (강화 시 오른쪽으로 슬라이드 퇴장)")]
    public RectTransform infoSlidePanel;
    [Tooltip("DetailPanel 안의 TrainingPanel (강화 시 오른쪽에서 왼쪽으로 슬라이드 진입)")]
    public RectTransform trainingSlidePanel;
    [Tooltip("TrainingPanel 안의 backBtn — 누르면 역순 슬라이드")]
    public Button backButton;
    [Tooltip("슬라이드 거리(px). 0이면 DetailPanel(또는 TrainingPanel) 너비 자동 사용")]
    public float slideWidth = 0f;
    public float slideDuration = 0.3f;
    [Tooltip("슬라이드 끝에서 살짝 튕기는 정도 (0이면 오버슈트 없음, 1.7 근처가 자연스러움)")]
    public float slideOvershoot = 1.7f;

    [Header("Open 연출 (패널 열릴 때)")]
    [Tooltip("ScrollPanel/PortraitPanel/DetailPanel 페이드인 시간(초)")]
    public float openFadeDuration = 0.35f;
    [Tooltip("DetailPanel 안쪽(Info/TrainingPanel)이 열릴 때 밀고 들어오는 시작 오프셋(px) — slideOvershoot 커브로 튕기며 들어옴")]
    public float openIntroOffset = 60f;

    CanvasGroup _scrollPanelCG, _portraitPanelCG, _infoPanelCG, _trainingPanelCG;
    bool _openFxResolved;

    [Header("Menu")]
    [Tooltip("메뉴 '강화하기' 버튼 — 누르면 패널 열고 바로 TrainingPanel 표시")]
    public Button trainingMenuButton;

    private string _selectedId = "";
    private readonly List<EmployeeSlotListUI> _slots = new();
    private readonly List<EmployeeData> _emps = new();
    private bool _snapHooked;

    // 아이템 사용 모드
    private bool         _useItemMode;
    private ItemChartRow _useItemRow;
    private EmployeeRole? _useItemRoleFilter;

    // 정원 초과 강제 해고 모드 — HiringUI 가 정원이 꽉 찬 상태에서 신규 채용을 시도할 때 유도.
    // FireEmployeeBtn 만 노출하고, 해고 성공 시 이 콜백으로 채용 흐름에 복귀시킨다.
    private bool _forceFireMode;
    private System.Action<bool> _onForceFireDone; // bool: 실제로 해고했는지(true) / 취소·닫기(false)

    GameObject Root => panelRoot != null ? panelRoot : gameObject;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (enhanceButton  != null) { enhanceButton.onClick.RemoveListener(OnClickEnhance);    enhanceButton.onClick.AddListener(OnClickEnhance); }
        if (fireButton     != null) { fireButton.onClick.RemoveListener(OnClickFire);           fireButton.onClick.AddListener(OnClickFire); }
        if (itemButton     != null) { itemButton.onClick.RemoveListener(OnClickItem);           itemButton.onClick.AddListener(OnClickItem); }
        if (closeButton    != null) { closeButton.onClick.RemoveListener(OnClickClose);         closeButton.onClick.AddListener(OnClickClose); }
        if (backButton     != null) { backButton.onClick.RemoveListener(OnClickBack);           backButton.onClick.AddListener(OnClickBack); }
        if (useItemButton  != null) { useItemButton.onClick.RemoveListener(OnClickUseItem);     useItemButton.onClick.AddListener(OnClickUseItem); }
        if (fireEmployeeButton != null) { fireEmployeeButton.onClick.RemoveListener(OnClickFireEmployee); fireEmployeeButton.onClick.AddListener(OnClickFireEmployee); }
        if (trainingMenuButton != null) { trainingMenuButton.onClick.RemoveListener(OpenListForEnhance); trainingMenuButton.onClick.AddListener(OpenListForEnhance); }

        SetButtonSlotActive(useItemButton, false);
        SetButtonSlotActive(fireEmployeeButton, false);
    }

    // 메뉴 직원관리 버튼 OnClick 에 연결
    public void OpenList()
    {
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        _selectedId = ""; // 진입 시 맨 위(첫) 직원 자동 선택
        Root.SetActive(true);
        ResetSlide();      // 항상 InfoPanel 부터 시작
        BuildList();       // 스냅 리스트가 맨 위 슬롯 자동 선택 → 상세 표시
        PlayOpenIntro();
    }

    public void OnClickClose()
    {
        if (_useItemMode) { ExitUseItemMode(); return; }
        if (_forceFireMode) { CloseForceFireMode(fired: false); return; }
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        Root.SetActive(false);
    }

    // 외부(커플 동반퇴사/사직/도주 등)에서 ownedEmployees 변경 시, 열려있으면 다시 빌드.
    public void RefreshIfOpen()
    {
        if (Root.activeInHierarchy) BuildList(); // _selectedId 유지 → 스냅이 같은 직원 재선택
    }

    // ── 목록 ─────────────────────────────────────
    void BuildList()
    {
        if (slotParent == null || slotPrefab == null || EmployeeManager.Instance == null) return;
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);
        _slots.Clear();
        _emps.Clear();

        var sorted = new List<EmployeeData>(EmployeeManager.Instance.ownedEmployees);
        if (_useItemMode && _useItemRoleFilter.HasValue)
            sorted.RemoveAll(e => e.role != _useItemRoleFilter.Value);
        sorted.Sort((a, b) =>
        {
            int c = b.grade.CompareTo(a.grade);                  // 등급 내림차순
            if (c != 0) return c;
            c = b.enhancementLevel.CompareTo(a.enhancementLevel); // 레벨 내림차순
            if (c != 0) return c;
            c = b.potential.CompareTo(a.potential);               // 잠재력 내림차순
            if (c != 0) return c;
            return RoleOrder(a.role).CompareTo(RoleOrder(b.role)); // 직군 기획>개발>아트
        });

        foreach (var emp in sorted)
        {
            var go = Instantiate(slotPrefab, slotParent);
            go.SetActive(true); // 프리팹 루트가 비활성으로 저장돼 있어도 강제 활성
            var slot = go.GetComponent<EmployeeSlotListUI>();
            if (slot != null)
            {
                slot.Setup(emp, OnSlotClicked); // 클릭 → 해당 슬롯으로 스냅
                _slots.Add(slot);
                _emps.Add(emp);
            }
        }
        SetupSnap();
    }

    // 스냅 리스트 구성 + 맨 위(또는 기존 선택) 자동 선택
    void SetupSnap()
    {
        var snap = ResolveSnap();
        if (snap == null)
        {
            // 폴백: 스냅 리스트 없음 → 첫(또는 기존 선택) 직원 즉시 선택
            if (_slots.Count == 0) { HideDetail(); return; }
            int idx0 = IndexOfSelected();
            _slots[idx0].SetSelected(true);
            OnSelectEmployee(_emps[idx0]);
            return;
        }

        if (!_snapHooked) { snap.OnSelectedChanged += OnSnapSelected; _snapHooked = true; }

        if (_slots.Count == 0)
        {
            snap.Setup((RectTransform)slotParent, new List<RectTransform>(), 0); // snap 내부 상태 리셋
            HideDetail();
            return;
        }
        int initial = IndexOfSelected();
        var rects = new List<RectTransform>(_slots.Count);
        foreach (var s in _slots) rects.Add((RectTransform)s.transform);

        snap.Setup((RectTransform)slotParent, rects, initial); // 동기 즉시 배치 → 뜨는 즉시 타겟 직원에 위치
    }

    VerticalSnapList ResolveSnap()
    {
        if (snapList != null) return snapList;
        // slotParent(Content) → Viewport → ScrollView(VerticalSnapList)
        if (slotParent != null && slotParent.parent != null && slotParent.parent.parent != null)
            snapList = slotParent.parent.parent.GetComponent<VerticalSnapList>();
        return snapList;
    }

    int IndexOfSelected()
    {
        if (!string.IsNullOrEmpty(_selectedId))
        {
            int f = _emps.FindIndex(e => e != null && e.id == _selectedId);
            if (f >= 0) return f;
        }
        return 0;
    }

    // 슬롯 클릭 → 해당 슬롯으로 스르륵 스냅 (스냅 완료/시작 시 OnSnapSelected 로 상세 갱신)
    void OnSlotClicked(EmployeeData emp)
    {
        if (emp == null) return;
        var snap = ResolveSnap();
        if (snap != null)
        {
            int idx = _emps.FindIndex(e => e != null && e.id == emp.id);
            if (idx >= 0) { snap.SnapToIndex(idx); return; }
        }
        OnSelectEmployee(emp); // 폴백
    }

    // 스냅 리스트가 선택 슬롯을 알려줄 때 → 상세 표시
    void OnSnapSelected(int index)
    {
        if (_emps.Count == 0) { HideDetail(); return; }
        if (index < 0 || index >= _emps.Count) return;
        OnSelectEmployee(_emps[index]);
    }

    // ── 선택 / 상세 ───────────────────────────────
    // public: 구 OwnedEmployeeSlotUI(연봉협상 슬롯)가 참조 — 호환 유지.
    public void OnSelectEmployee(EmployeeData emp)
    {
        if (emp == null) return;
        _selectedId = emp.id;
        SetDetailActive(true);
        PopulateDetail(emp);
        if (_onTraining) TrainingPanelUI.Instance?.OnSelectEmployee(emp); // 트레이닝 뷰 중이면 강화 패널도 갱신
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
        if (_useItemMode)
        {
            if (useItemButton != null)
                useItemButton.interactable = emp != null && !IsDispatched(emp.id) && IsRoleMatchForItem(emp);
            return;
        }
        if (_forceFireMode)
        {
            if (fireEmployeeButton != null)
                fireEmployeeButton.interactable = emp != null && !IsDispatched(emp.id);
            return;
        }
        bool ok = emp != null && !IsDispatched(emp.id);
        if (enhanceButton != null) enhanceButton.interactable = ok;
        if (fireButton    != null) fireButton.interactable    = ok;
        if (itemButton    != null) itemButton.interactable    = ok;
    }

    bool IsRoleMatchForItem(EmployeeData emp)
    {
        if (_useItemRoleFilter == null) return true;
        return emp != null && emp.role == _useItemRoleFilter.Value;
    }

    // 아이템 사용 모드 진입 — ItemDetailUI 에서 사용하기 클릭 시 호출
    public void OpenForUseItem(ItemChartRow row, EmployeeRole? roleFilter)
    {
        _useItemRow        = row;
        _useItemRoleFilter = roleFilter;
        _useItemMode       = true;
        ApplyUseItemModeVisual(true);
        _selectedId = "";
        Root.SetActive(true);
        ResetSlide();
        BuildList();
        PlayOpenIntro();
    }

    void ApplyUseItemModeVisual(bool on)
    {
        SetButtonSlotActive(enhanceButton, !on);
        SetButtonSlotActive(fireButton,    !on);
        SetButtonSlotActive(itemButton,    !on);
        SetButtonSlotActive(useItemButton, on);
    }

    // GlobalButtonClickBounce 가 버튼이 처음 눌리는 순간 그 버튼의 부모를 "__ClickBounceWrapper"로
    // 바꿔치기한다(버튼은 래퍼의 풀스트레치 자식이 됨) — ERBottomPanel 의 HorizontalLayoutGroup 입장에서는
    // 이제 래퍼가 실제 자식이라, 버튼 자신만 SetActive(false) 해도 래퍼는 활성 상태로 남아 빈 레이아웃
    // 슬롯을 계속 차지해버린다(다른 버튼들이 밀리는 원인). 래핑된 버튼이면 래퍼까지 같이 토글한다.
    static void SetButtonSlotActive(Button btn, bool active)
    {
        if (btn == null) return;
        var parent = btn.transform.parent;
        if (parent != null && parent.name == "__ClickBounceWrapper")
            parent.gameObject.SetActive(active);
        else
            btn.gameObject.SetActive(active);
    }

    public void OnClickUseItem()
    {
        var emp = Selected();
        if (emp == null || _useItemRow == null) return;

        if (ItemManager.Instance.UseItem(_useItemRow.itemId, emp))
        {
            ItemDetailUI.Instance?.HideDetail();
            ItemPanelUI.Instance?.Refresh();
        }

        ExitUseItemMode();
    }

    void ExitUseItemMode()
    {
        _useItemMode       = false;
        _useItemRow        = null;
        _useItemRoleFilter = null;
        ApplyUseItemModeVisual(false);
        Root.SetActive(false); // StartTime 없이 닫기 (ItemPanel 이 뒤에서 시간정지 유지)
    }

    // 정원 초과 강제 해고 모드 진입 — HiringUI 가 신규 채용인데 정원이 꽉 찼을 때 호출.
    // FireEmployeeBtn 만 노출. 패널이 닫힐 때(해고 성공/취소 무관) onClosed(fired) 를 항상 호출 —
    // 호출자는 이걸로 ConfirmHirePanel 재활성 등 뒷정리를 하고, fired 가 true 일 때만 채용을 이어간다.
    public void OpenForForceFire(System.Action<bool> onClosed)
    {
        _forceFireMode   = true;
        _onForceFireDone = onClosed;
        ApplyForceFireModeVisual(true);

        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        _selectedId = ""; // 진입 시 맨 위(첫) 직원 자동 선택
        Root.SetActive(true);
        ResetSlide();
        BuildList();
        PlayOpenIntro();
    }

    void ApplyForceFireModeVisual(bool on)
    {
        SetButtonSlotActive(enhanceButton,      !on);
        SetButtonSlotActive(fireButton,         !on);
        SetButtonSlotActive(itemButton,         !on);
        SetButtonSlotActive(fireEmployeeButton, on);
    }

    // FireEmployeeBtn — 확인 다이얼로그 후 해고, 성공하면 강제 해고 모드 종료 + 채용 흐름 복귀 콜백 실행.
    public void OnClickFireEmployee()
    {
        var emp = Selected();
        if (emp == null) return;
        if (IsDispatched(emp.id)) { AlertUI.Instance?.Show("파견중인 직원은 해고할 수 없습니다."); return; }

        string id = emp.id;
        ConfirmUI.Instance.Show(
            $"{emp.employeeName}을(를) 해고하시겠습니까?",
            onConfirm: () => { DoFire(id); CloseForceFireMode(fired: true); },
            onCancel:  () => { },
            confirmText: "네",
            cancelText:  "아니오"
        );
    }

    // fired=false — 해고 없이 취소/닫기. 어느 경우든 onClosed(fired) 는 항상 호출(호출자가 뒷정리하도록).
    void CloseForceFireMode(bool fired)
    {
        _forceFireMode = false;
        ApplyForceFireModeVisual(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        Root.SetActive(false);

        var cb = _onForceFireDone;
        _onForceFireDone = null;
        cb?.Invoke(fired);
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
        // 직원 0명 시 PortraitPanel 하위 + DetailPanel 하위 숨김
        if (portraitImage      != null) portraitImage.gameObject.SetActive(active);
        if (nameFrame          != null) nameFrame.SetActive(active);
        if (infoSlidePanel     != null) infoSlidePanel.gameObject.SetActive(active);
        if (trainingSlidePanel != null) trainingSlidePanel.gameObject.SetActive(active);
    }

    void PopulateDetail(EmployeeData emp)
    {
        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        SetText(nameText,      emp.employeeName);
        SetText(potentialText, emp.PotentialToString());           // "A" 단독 (라벨 없음)
        SetText(gradeText,     emp.GradeToString().ToUpper());     // 등급 대문자
        RoleIconSet.Apply(roleBadge, roleIconSet, emp.role);

        ApplyGradeBG(emp.grade);
        SetupTraitWithLock(emp);  // 등급 무관 이름 출력 + 미충족 시 lockedPanel 활성
        SetupEventWithLock(emp);

        SetText(enhancementText, $"+{emp.enhancementLevel}");      // "+N"
        if (satisfactionSlider != null)
        {
            satisfactionSlider.minValue = 0f;
            satisfactionSlider.maxValue = 100f;
            satisfactionSlider.value = emp.satisfaction;
            SatisfactionFillSet.Apply(satisfactionSlider, satisfactionFillSet, emp.satisfaction);
        }
        SetText(satisfactionText, $"{emp.satisfaction}/100");
        SetText(salaryText, $"{emp.salary:N0} G");

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

    // 능력치 변화 방향에 따라 화살표 표시. 스프라이트는 1개 — 버프는 그대로, 디버프는 Y축 flip. 무변화면 숨김.
    void SetStatArrow(Image arrow, int baseSkill, int effectiveSkill)
    {
        if (arrow == null) return;
        if (effectiveSkill == baseSkill) { arrow.enabled = false; return; }

        bool debuff = effectiveSkill < baseSkill;
        arrow.sprite  = statArrowSprite;
        arrow.color   = EmployeeData.GetStatColor(baseSkill, effectiveSkill); // 수치 색상과 동일 (버프 #E63356 / 디버프 #517FFF)
        arrow.enabled = true;

        // Y축 flip (디버프) / 원복 (버프) — x·z 는 유지
        var rt = arrow.rectTransform;
        var s  = rt.localScale;
        float magY = Mathf.Abs(s.y);
        rt.localScale = new Vector3(s.x, debuff ? -magY : magY, s.z);
    }

    // 등급 BG = 스프라이트(공용 SO)만 교체. 색 변경(gradePanel 코루틴)은 제거.
    void ApplyGradeBG(EmployeeGrade grade)
    {
        GradeSpriteSet.Apply(gradeBG, gradeBGSet, grade);
    }

    // 특성 — 등급 무관 이름 표시(해당 등급 아니어도 출력). 충족이면 클릭 시 설명, 미충족이면 lockedPanel 활성(터치 차단).
    void SetupTraitWithLock(EmployeeData emp)
    {
        string name = CharacterTraitApplier.GetTraitNameAnyGrade(emp);
        bool unlocked = CharacterTraitApplier.IsTraitUnlocked(emp);
        bool hasTrait = !string.IsNullOrEmpty(name);

        if (traitText != null)
        {
            traitText.text = hasTrait ? $"특성 : {name}" : "";
            var btn = traitText.GetComponent<TraitDescriptionButton>();
            if (hasTrait && unlocked)
            {
                if (btn == null) btn = traitText.gameObject.AddComponent<TraitDescriptionButton>();
                btn.Bind(emp);
                traitText.raycastTarget = true;
            }
            else
            {
                if (btn != null) btn.Bind(null);
                traitText.raycastTarget = false; // 미충족/미보유 → 설명 클릭 비활성
            }
        }
        // 특성이 있는데 등급 미충족일 때만 잠금 패널 노출 (미보유는 잠글 게 없음)
        if (traitLockedPanel != null) traitLockedPanel.SetActive(hasTrait && !unlocked);
    }

    // 전용 이벤트 — 등급 무관 이름 표시. 충족이면 클릭 시 설명, 미충족이면 lockedPanel 활성.
    void SetupEventWithLock(EmployeeData emp)
    {
        string name = CharacterUniqueEvents.GetEventNameAnyGrade(emp);
        bool unlocked = CharacterUniqueEvents.IsEventUnlocked(emp);
        bool hasEvent = !string.IsNullOrEmpty(name);

        if (eventText != null)
        {
            eventText.text = hasEvent ? $"이벤트 : {name}" : "";
            var btn = eventText.GetComponent<EventDescriptionButton>();
            if (hasEvent && unlocked)
            {
                if (btn == null) btn = eventText.gameObject.AddComponent<EventDescriptionButton>();
                btn.Bind(emp);
                eventText.raycastTarget = true;
            }
            else
            {
                if (btn != null) btn.Bind(null);
                eventText.raycastTarget = false;
            }
        }
        if (eventLockedPanel != null) eventLockedPanel.SetActive(hasEvent && !unlocked);
    }

    // ── 버튼 ─────────────────────────────────────
    EmployeeData Selected()
        => !string.IsNullOrEmpty(_selectedId) ? EmployeeManager.Instance?.GetEmployee(_selectedId) : null;

    // 강화하기 — InfoPanel 오른쪽 퇴장 + TrainingPanel 왼쪽 진입 (슬라이드). 패널은 닫지 않음.
    public void OnClickEnhance()
    {
        var emp = Selected();
        if (emp == null) return;
        if (IsDispatched(emp.id)) { AlertUI.Instance?.Show("파견중인 직원은 강화할 수 없습니다."); return; }

        TrainingPanelUI.Instance?.OnSelectEmployee(emp); // 트레이닝 패널에 선택 직원 표시
        SlideToTraining();
    }

    // 강화 패널 backBtn — 역순 슬라이드 (TrainingPanel 퇴장 + InfoPanel 복귀) + 상세 갱신(강화로 변한 수치 반영)
    public void OnClickBack()
    {
        SlideToInfo();
        var emp = Selected();
        if (emp != null) PopulateDetail(emp);
    }

    // 메뉴 '강화하기' 버튼 — 패널 열고 바로 TrainingPanel 표시 (선택 직원 = 맨 위 자동선택)
    public void OpenListForEnhance()
    {
        // OpenList()를 그대로 쓰면 그 내부의 PlayOpenIntro()가 "InfoPanel 노출"을 목표로 코루틴을 먼저
        // 시작해버려서, 뒤이은 SetSlideInstant(true)로 순간이동해도 그 코루틴이 다시 InfoPanel로 되돌려놓는다.
        // OpenForEnhance와 동일하게 슬라이드를 먼저 확정한 뒤 PlayOpenIntro()를 호출해야 한다.
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        _selectedId = ""; // 진입 시 맨 위(첫) 직원 자동 선택
        Root.SetActive(true);
        SetSlideInstant(true);  // InfoPanel 대신 TrainingPanel 부터 (애니 없이)
        BuildList();             // 스냅 리스트가 맨 위 슬롯 자동 선택 → 상세 표시
        // snap 선택 콜백은 비동기라, 그 전까지 TrainingPanel 에 디자인타임 더미 텍스트(예: 창의성 7000)가 노출된다.
        // 맨 위(자동선택될) 직원으로 동기 채워 더미가 보이지 않게 한다. 이후 snap 정착 시 동일 값으로 재갱신.
        var emp = _emps.Count > 0 ? _emps[IndexOfSelected()] : null;
        if (emp != null) TrainingPanelUI.Instance?.OnSelectEmployee(emp);
        PlayOpenIntro();
    }

    // 외부(EmployeeCardUI 등)에서 특정 직원 강화 — 패널 열고 그 직원 선택 + TrainingPanel 표시
    public void OpenForEnhance(EmployeeData emp)
    {
        if (emp == null) return;
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        _selectedId = emp.id;     // 이 직원 선택 (BuildList→snap 이 IndexOfSelected 로 선택)
        Root.SetActive(true);
        SetSlideInstant(true);    // InfoPanel 대신 TrainingPanel 부터
        BuildList();              // 동기 setup → 뜨는 즉시 emp 슬롯에 배치
        TrainingPanelUI.Instance?.OnSelectEmployee(emp); // 동기 채움 → snap 비동기 선택 전 더미 텍스트 노출 방지
        PlayOpenIntro();
    }

    // ── 슬라이드 ─────────────────────────────────
    private Coroutine _slideCo;
    private bool _onTraining; // 현재 TrainingPanel 노출 중인지

    float SlideDist()
    {
        if (slideWidth > 0f) return slideWidth;
        if (trainingSlidePanel != null && trainingSlidePanel.rect.width > 0f) return trainingSlidePanel.rect.width;
        if (infoSlidePanel != null && infoSlidePanel.rect.width > 0f) return infoSlidePanel.rect.width;
        return 693f;
    }

    // 애니 없이 즉시 배치. training=false → InfoPanel(x=0)/TrainingPanel(+W), true → 반대
    void SetSlideInstant(bool training)
    {
        if (_slideCo != null) { StopCoroutine(_slideCo); _slideCo = null; }
        float w = SlideDist();
        SetSlideX(infoSlidePanel,     training ? w  : 0f);
        SetSlideX(trainingSlidePanel, training ? 0f : w);
        _onTraining = training;
    }

    // 진입 시 초기 상태: InfoPanel 부터
    void ResetSlide() => SetSlideInstant(false);

    void SlideToTraining() { StartSlide(SlideDist(), 0f); _onTraining = true; }  // info → +W, training → 0
    void SlideToInfo()     { StartSlide(0f, SlideDist()); _onTraining = false; } // info → 0, training → +W

    void StartSlide(float infoTo, float trainTo)
    {
        if (infoSlidePanel == null || trainingSlidePanel == null) return;
        if (_slideCo != null) StopCoroutine(_slideCo);
        _slideCo = StartCoroutine(SlideRoutine(infoTo, trainTo));
    }

    IEnumerator SlideRoutine(float infoTo, float trainTo)
    {
        float infoFrom  = infoSlidePanel.anchoredPosition.x;
        float trainFrom = trainingSlidePanel.anchoredPosition.x;
        float dur = Mathf.Max(0.01f, slideDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / dur), slideOvershoot);
            SetSlideX(infoSlidePanel,  Mathf.LerpUnclamped(infoFrom,  infoTo,  k));
            SetSlideX(trainingSlidePanel, Mathf.LerpUnclamped(trainFrom, trainTo, k));
            yield return null;
        }
        SetSlideX(infoSlidePanel, infoTo);
        SetSlideX(trainingSlidePanel, trainTo);
        _slideCo = null;
    }

    // 목표값 도달 직전에 살짝 지나쳤다가(overshoot) 되돌아오는 ease-out-back 커브.
    static float EaseOutBack(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }

    // ── Open 연출 ─────────────────────────────────
    // ScrollPanel/PortraitPanel 은 alpha 0→1 페이드만, DetailPanel 안쪽(Info/TrainingPanel)은
    // alpha 페이드 + slideOvershoot 커브로 살짝 밀고 들어오는 연출까지 함께.
    void ResolveOpenFx()
    {
        if (_openFxResolved) return;
        _openFxResolved = true;
        _scrollPanelCG   = GetOrAddCG(Root.transform.Find("ScrollPanel"));
        _portraitPanelCG = GetOrAddCG(Root.transform.Find("PortraitPanel"));
        _infoPanelCG     = infoSlidePanel     != null ? GetOrAddCG(infoSlidePanel.transform)     : null;
        _trainingPanelCG = trainingSlidePanel != null ? GetOrAddCG(trainingSlidePanel.transform) : null;
    }

    static CanvasGroup GetOrAddCG(Transform t)
    {
        if (t == null) return null;
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    void PlayOpenIntro()
    {
        ResolveOpenFx();
        FadeIn(_scrollPanelCG);
        FadeIn(_portraitPanelCG);
        PlayDetailIntro(infoSlidePanel,     _infoPanelCG);
        PlayDetailIntro(trainingSlidePanel, _trainingPanelCG);
    }

    void FadeIn(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f;
        cg.DOFade(1f, openFadeDuration).SetUpdate(true);
    }

    void PlayDetailIntro(RectTransform rt, CanvasGroup cg)
    {
        if (rt == null) return;
        FadeIn(cg);
        StartCoroutine(IntroSlideRoutine(rt));
    }

    IEnumerator IntroSlideRoutine(RectTransform rt)
    {
        float targetX = rt.anchoredPosition.x;
        float fromX = targetX + openIntroOffset;
        float dur = Mathf.Max(0.01f, slideDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / dur), slideOvershoot);
            SetSlideX(rt, Mathf.LerpUnclamped(fromX, targetX, k));
            yield return null;
        }
        SetSlideX(rt, targetX);
    }

    static void SetSlideX(RectTransform rt, float x)
    {
        if (rt == null) return;
        var p = rt.anchoredPosition;
        p.x = x;
        rt.anchoredPosition = p;
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
        // 특성 's1'(fireNoMoraleEvent) 장착 시 직접 해고를 퇴사 카운트에서 제외
        // → YearlyExitCount 미증가 → 불안정 회사/불안감 조성(만족도 하락) 이벤트로 안 이어짐.
        bool countAsExit = !TraitEffectApplier.HasFireMoraleImmunity();
        EmployeeManager.Instance.FireEmployee(emp, countAsExit);
        HUDUI.Instance?.RefreshAll();
        _selectedId = "";
        BuildList();    // 남은 직원 중 맨 위 자동 선택 (없으면 EmptyPanel)
    }

    // 강화/아이템 패널에서 복귀 — 시간정지는 이미 걸려 있으므로 추가 안 함. 패널만 다시 표시 + 선택 복원.
    void Reopen(string reselectId)
    {
        _selectedId = reselectId ?? "";
        Root.SetActive(true);
        BuildList(); // 복귀 직원 재선택 (스냅이 해당 슬롯으로)
    }

    // ── 헬퍼 ─────────────────────────────────────
    static bool IsDispatched(string id)
        => DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(id);

    static void SetText(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }

    static int RoleOrder(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner    => 0,
        EmployeeRole.Programmer => 1,
        EmployeeRole.Artist     => 2,
        _ => 3
    };
}
