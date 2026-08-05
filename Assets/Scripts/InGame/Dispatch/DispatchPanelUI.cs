using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 파견 + 팀장 선택 패널 (동일 UI/프리팹 재사용, 모드로 분기).
// ── 파견 모드: 메뉴-프로젝트-파견 버튼(dispatchBtn) OnClick → Open(). CEO 제외 보유 직원 리스트에서
//   한 명 선택 후 확정 → DispatchManager.RequestDispatch. 이미 파견중인 직원은 badge + 선택 불가.
// ── 팀장 선택 모드: OpenForLeaderSelect(LeaderType, onComplete) — 기존 LeaderSelectUI 대체.
//   role 필터(+CEO 항상 포함) 후 확정 → DevelopmentManager.SetLeader. 닫기 버튼 숨김(필수 선택).
//
// 좌(DispatchScrollView/Content): 직원 슬롯 리스트(DispatchSlotPrefab).
// 우(DispatchRightPanel/ChildPanel): 슬롯 selectButton 클릭 시 상세 표시 — 초상화/특성/전용이벤트/능력치 4종.
//   강화 패널(TrainingPanelUI)·이력서(EmployeeResumePanel) 와 동일 구성. 선택 전엔 상세 3패널 비활성.
//   닫기/확정 버튼(dispatchCloseBtn/dispatchConfirmBtn)은 ChildPanel/BottomPanel 안에 있어 항상 표시 유지
//   (BottomPanel 은 detailSections 토글 대상에서 제외 — 선택 전에도 닫기 가능해야 하므로).
public class DispatchPanelUI : MonoBehaviour
{
    public static DispatchPanelUI Instance { get; private set; }

    enum PanelMode { Dispatch, Leader }

    [Header("UI")]
    public GameObject panelRoot;          // 토글되는 모달 패널(DispatchPanel). 비우면 this.gameObject
    public TextMeshProUGUI titleText;     // 상단 제목 — 모드별 문구 ("직원 파견" / "OO팀장 선택")
    public Transform  slotParent;         // 슬롯이 쌓일 Content
    public GameObject slotPrefab;         // DispatchSlotUI 부착 프리팹
    public Button     confirmButton;      // 확정 (dispatchConfirmBtn)
    public Button     closeButton;        // 닫기 (dispatchCloseBtn) — 팀장 선택 모드에서는 숨김(필수 선택)

    [Header("Detail (DispatchRightPanel/ChildPanel) — 슬롯 선택 시 표시)")]
    public GameObject[] detailSections;   // 선택 전 비활성: TopPanel / TraitAEventPanel / AbilityPanel
    public GameObject   emptyPanel;       // 선택 전 placeholder — detailSections 와 반대로 토글(VerticalLayout 빈자리 방지)
    public Image portraitImage;           // 초상화
    //public Image portraitBGImage;         // 등급 색 배경
    public TextMeshProUGUI traitText;     // "특성 : {}" (클릭 시 AlertUI3 로 설명 표시)
    public TextMeshProUGUI eventText;     // "이벤트 : {}" (클릭 시 AlertUI3 로 설명 표시)
    [Tooltip("TopPanel/PoritraitPanel/CountSeqPanel — 연속 팀장 횟수 표시, 기본 비활성/횟수(consecutiveLeaderCount)가 있을 때만 활성")]
    public GameObject countSeqPanel;
    public TextMeshProUGUI countSeqText;
    public GameObject traitLockedPanel;   // 특성 없음(Epic 미만) 시 표시할 잠금 오버레이
    public GameObject eventLockedPanel;   // 전용 이벤트 없음(Unique 미만) 시 표시할 잠금 오버레이
    public TextMeshProUGUI planningText;  // 기획 수치
    public TextMeshProUGUI developText;   // 개발 수치
    public TextMeshProUGUI artText;       // 아트 수치
    public TextMeshProUGUI creativityText;// 창의성 수치

    [Header("Stat arrows — 능력치 옆 ArrowImage (버프=기본 / 디버프=Y축 flip / 무변화=숨김)")]
    public Image planningArrow;
    public Image developArrow;
    public Image artArrow;
    public Image creativityArrow;
    public Sprite statArrowSprite;

    private string _selectedId = "";
    private Coroutine _gradeCo;
    private readonly List<DispatchSlotUI> _slots = new();

    private PanelMode _mode = PanelMode.Dispatch;
    private LeaderType _leaderType;
    private System.Action _onLeaderSelected;

    void Awake()
    {
        Instance = this;

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirm);
            confirmButton.onClick.AddListener(OnConfirm);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    // 파견 버튼 OnClick 에 연결
    public void Open()
    {
        // 이미 파견 중이면 패널을 열지 않고 안내만 표시
        if (DispatchManager.Instance != null && DispatchManager.Instance.IsActive)
        {
            string name = !string.IsNullOrEmpty(DispatchManager.Instance.DispatchEmployeeName)
                ? DispatchManager.Instance.DispatchEmployeeName
                : "직원";
            AlertUI.Instance?.Show($"{name}이 이미 파견중입니다.");
            return;
        }

        _mode = PanelMode.Dispatch;
        if (titleText != null) titleText.text = "직원 파견";
        if (closeButton != null) closeButton.gameObject.SetActive(true);

        ShowPanelAndBuildList();
    }

    // 팀장 선택 — 기존 LeaderSelectUI.Open 대체. onComplete 는 확정(SetLeader 호출) 직후 실행.
    public void OpenForLeaderSelect(LeaderType type, System.Action onComplete)
    {
        // 상시 개발틱 카운트업 진행 중이면 끝날 때까지 대기 후 표시 (LeaderSelectUI 의 기존 로직 이식)
        if (StatTickPopup.ActiveCount > 0)
        {
            StartCoroutine(OpenLeaderAfterPopups(type, onComplete));
            return;
        }
        OpenLeaderInternal(type, onComplete);
    }

    IEnumerator OpenLeaderAfterPopups(LeaderType type, System.Action onComplete)
    {
        // [주의] 예전엔 여기서 ForceStartTime()으로 시간을 풀었다가 다시 StopTime() 1회로 되돌렸는데,
        // 호출 전 stopCount가 1보다 크면 복원이 덜 돼서(강제 리셋 후 1회만 재적립) 이후 시점에 시간이
        // 조기 재개되는 버그(팀장 선택 중 시계가 도는 것처럼 보임)로 이어졌다. GameTimeManager.IsRunning은
        // 여기 진입 전 이미 항상 정지 상태이므로, 시간을 건드리지 않고 real-time으로만 대기한다.
        // StatTickPopup 애니메이션은 GameTimeManager.IsRunning을 봐서 진행하므로(StatTickPopup.cs),
        // 정지 상태에선 자연 완료되지 않고 아래 3초 타임아웃으로 강제 종료된다 — 의도된 동작.
        float waited = 0f;
        while (StatTickPopup.ActiveCount > 0 && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (StatTickPopup.ActiveCount > 0)
        {
            Debug.LogWarning($"[DispatchPanelUI] 개발틱 팝업 대기 타임아웃(ActiveCount={StatTickPopup.ActiveCount}) — 강제 진행");
            StatTickPopup.ActiveCount = 0;
        }

        OpenLeaderInternal(type, onComplete);
    }

    void OpenLeaderInternal(LeaderType type, System.Action onComplete)
    {
        _mode = PanelMode.Leader;
        _leaderType = type;
        _onLeaderSelected = onComplete;

        if (titleText != null)
            titleText.text = type switch
            {
                LeaderType.Planner    => "기획팀장 선택",
                LeaderType.Programmer => "개발팀장 선택",
                LeaderType.Artist     => "아트팀장 선택",
                _ => ""
            };
        // 팀장 선택은 필수 — 취소 없이 반드시 한 명을 골라야 하므로 닫기 버튼 숨김
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        // entireLeaderPanel 은 LeaderScoreUI(leaderscorePanel)의 부모 컨테이너 — 원래 LeaderSelectUI.Open 이 함께 켰던 것을
        // 그대로 이식(안 켜면 SetLeader 이후 팀장점수 패널이 활성화돼도 부모가 꺼져있어 화면에 안 보임).
        // leaderPanel(구 선택 목록)은 DispatchPanel 로 대체됐으니 끈 채로 둔다.
        if (LeaderSelectUI.Instance != null)
        {
            if (LeaderSelectUI.Instance.entireLeaderPanel != null)
                LeaderSelectUI.Instance.entireLeaderPanel.gameObject.SetActive(true);
            if (LeaderSelectUI.Instance.leaderPanel != null)
                LeaderSelectUI.Instance.leaderPanel.gameObject.SetActive(false);
        }

        ModalGate.I.Register(this);
        ShowPanelAndBuildList();

        // 온보딩: 튜토리얼 6-1~6-3 — 기획팀장 선택 패널이 열릴 때마다(재접속으로 자연 재오픈된 경우 포함)
        // 아직 완료 전이면 재생. 두 번째 슬롯(index 1)은 CEO(index 0) 다음의 첫 기획자 후보.
        if (type == LeaderType.Planner && !OnboardingState.Tutorial6Done
            && TutorialController.Instance != null && _slots.Count >= 2)
        {
            // 캡처해둔 Button 참조 대신 slotParent(스크롤뷰 Content)를 넘겨서, 하이라이트 직전에
            // "지금 두 번째 자식"을 다시 찾게 한다 — 대사가 뜨는 동안 슬롯이 재생성되면 미리 캡처한
            // Button 참조가 파괴된 오브젝트가 돼버려 하이라이트가 조용히 스킵되는 문제를 회피.
            StartCoroutine(TutorialController.Instance.PlayTutorial6(slotParent));
        }

        // 온보딩: 튜토리얼 12-1 — 아트팀장 선택 패널이 열릴 때마다(재접속으로 자연 재오픈된 경우 포함)
        // 아직 완료 전이면 재생. 아트 직원이 없는 게 전제라 첫 번째(유일한) 슬롯이 곧 CEO.
        if (type == LeaderType.Artist && !OnboardingState.Tutorial12Done
            && TutorialController.Instance != null && _slots.Count >= 1)
        {
            StartCoroutine(TutorialController.Instance.PlayTutorial12(slotParent));
        }
    }

    void ShowPanelAndBuildList()
    {
        GameTimeManager.Instance?.StopTime();
        var root = panelRoot != null ? panelRoot : gameObject;
        root.SetActive(true);
        BuildList();
        HideDetail();
    }

    // 닫기/취소 버튼에 연결
    public void Close()
    {
        // 팀장 선택 모드는 확정 즉시 팀장점수(LeaderScoreUI) 모달로 바로 이어지므로 여기서 시간을 풀면 안 됨 —
        // (StopTime/StartTime 카운트가 우연히 맞아떨어지는 것에 의존하던 게 실제로 새는 원인이었음).
        // 시간 재개는 그 뒤 이어지는 모달 체인의 최종 ForceStartTime()에서만 처리한다.
        if (_mode != PanelMode.Leader)
            GameTimeManager.Instance?.StartTime();
        var root = panelRoot != null ? panelRoot : gameObject;
        root.SetActive(false);
        if (_mode == PanelMode.Leader) ModalGate.I.Unregister(this);
    }

    void BuildList()
    {
        if (slotParent == null || slotPrefab == null) return;
        if (EmployeeManager.Instance == null) return;

        _selectedId = "";
        _slots.Clear();
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        List<EmployeeData> list = _mode == PanelMode.Leader ? BuildLeaderCandidates() : BuildDispatchCandidates();

        foreach (var emp in list)
        {
            var slot = Instantiate(slotPrefab, slotParent);
            slot.SetActive(true); // 프리팹 루트가 비활성으로 저장돼 있어도 스폰 시 강제 활성
            var s    = slot.GetComponent<DispatchSlotUI>();
            if (s == null) continue;
            bool dispatched = DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id);
            s.Setup(emp, this, dispatched, _mode == PanelMode.Leader);
            _slots.Add(s);
        }

        RefreshConfirm();
    }

    // 파견 모드 — ownedEmployees(이미 CEO 제외) 전체를 등급 내림차순 → 레벨 내림차순 → 직군(기획>개발>아트) 정렬.
    List<EmployeeData> BuildDispatchCandidates()
    {
        var sorted = new List<EmployeeData>(EmployeeManager.Instance.ownedEmployees);
        sorted.Sort((a, b) =>
        {
            int c = b.grade.CompareTo(a.grade);
            if (c != 0) return c;
            c = b.enhancementLevel.CompareTo(a.enhancementLevel);
            if (c != 0) return c;
            return RoleOrder(a.role).CompareTo(RoleOrder(b.role));
        });
        return sorted;
    }

    // 팀장 선택 모드 — 기존 LeaderSelectUI 필터 그대로: role 일치 + 파견중 제외, CEO 는 항상 맨 앞에 포함.
    List<EmployeeData> BuildLeaderCandidates()
    {
        EmployeeRole filterRole = _leaderType switch
        {
            LeaderType.Planner    => EmployeeRole.Planner,
            LeaderType.Programmer => EmployeeRole.Programmer,
            LeaderType.Artist     => EmployeeRole.Artist,
            _ => EmployeeRole.Planner
        };

        var filtered = EmployeeManager.Instance.ownedEmployees
            .FindAll(e => e.role == filterRole
                          && (DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id)));

        // 등급 내림차순 → 레벨 내림차순 (같은 role 만 모여있어 직군 비교는 불필요)
        filtered.Sort((a, b) =>
        {
            int c = b.grade.CompareTo(a.grade);
            return c != 0 ? c : b.enhancementLevel.CompareTo(a.enhancementLevel);
        });

        var ceo = EmployeeManager.Instance.CEO;
        if (ceo != null) filtered.Insert(0, ceo);
        return filtered;
    }

    public void OnSelect(string empId)
    {
        _selectedId = empId;
        foreach (var s in _slots)
            s.SetSelected(s.EmployeeId == empId);

        var emp = FindEmployee(empId);
        if (emp != null) ShowDetail(emp);
        RefreshConfirm();
    }

    EmployeeData FindEmployee(string empId)
    {
        if (EmployeeManager.Instance == null) return null;
        var ceo = EmployeeManager.Instance.CEO; // 팀장 선택 모드는 CEO 도 후보 — ownedEmployees 엔 없음
        if (ceo != null && ceo.id == empId) return ceo;
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
            if (e.id == empId) return e;
        return null;
    }

    // ── 상세 (우측 ChildPanel) ─────────────────
    void HideDetail()
    {
        SetDetailActive(false);
    }

    void SetDetailActive(bool active)
    {
        if (detailSections != null)
            foreach (var go in detailSections)
                if (go != null) go.SetActive(active);

        if (emptyPanel != null) emptyPanel.SetActive(!active); // 선택 전엔 EmptyPanel 로 빈자리 채움
    }

    void ShowDetail(EmployeeData emp)
    {
        SetDetailActive(true);

        // 초상화
        if (portraitImage != null)
        {
            Sprite sp = !string.IsNullOrEmpty(emp.portraitId)
                ? Resources.Load<Sprite>($"Portraits/Mini/{emp.portraitId}") : null;
            portraitImage.sprite  = sp;
            portraitImage.enabled = sp != null;
        }

        // 특성 / 전용 이벤트 — 등급 무관 이름은 항상 표시(AnyGrade), 등급 미충족이면 LockedPanel 로 클릭만 차단.
        // (EmployeeCardUI/EmployeeResumePanel 과 동일 규칙 — 이전엔 등급게이팅된 GetTraitName/GetEventName 을 써서
        //  Epic/Unique 미만이면 이름이 있어도 항상 "없음" 으로만 보이던 문제)
        WireDesc(traitText, traitLockedPanel,
                 emp.isCEO ? "" : CharacterTraitApplier.GetTraitNameAnyGrade(emp),
                 CharacterTraitApplier.GetTraitDescription(emp), "특성", emp.portraitId,
                 !emp.isCEO && CharacterTraitApplier.IsTraitUnlocked(emp));
        WireDesc(eventText, eventLockedPanel,
                 CharacterUniqueEvents.GetEventNameAnyGrade(emp),
                 CharacterUniqueEvents.GetEventDescription(emp), "이벤트", emp.portraitId,
                 CharacterUniqueEvents.IsEventUnlocked(emp));

        // 연속 팀장 횟수 — 기본 비활성, 값이 있을 때만 "연속 n회" 표시(CEO 는 항상 0이라 자연히 숨겨짐).
        if (countSeqPanel != null)
        {
            bool hasStreak = emp.consecutiveLeaderCount > 0;
            countSeqPanel.SetActive(hasStreak);
            if (hasStreak && countSeqText != null) countSeqText.text = $"연속 {emp.consecutiveLeaderCount}회";
        }

        // 능력치 — 버프/디버프 적용된 실제값 + 색상(EmployeeCardUI 와 동일 규칙: 버프 빨강 / 디버프 파랑 / 무변화 흰색)
        EmployeeCardUI.SetStatColored(planningText,   emp.planningSkill,   emp.EffectivePlanningSkill);
        EmployeeCardUI.SetStatColored(developText,    emp.developSkill,    emp.EffectiveDevelopSkill);
        EmployeeCardUI.SetStatColored(artText,        emp.artSkill,        emp.EffectiveArtSkill);
        EmployeeCardUI.SetStatColored(creativityText, emp.creativitySkill, emp.EffectiveCreativitySkill);

        // 수치 옆 화살표 — 수치 색상과 동일 기준 (상승=red / 하락=blue / 무변화=숨김)
        SetStatArrow(planningArrow,   emp.planningSkill,   emp.EffectivePlanningSkill);
        SetStatArrow(developArrow,    emp.developSkill,    emp.EffectiveDevelopSkill);
        SetStatArrow(artArrow,        emp.artSkill,        emp.EffectiveArtSkill);
        SetStatArrow(creativityArrow, emp.creativitySkill, emp.EffectiveCreativitySkill);
    }

    // 능력치 변화 방향에 따라 화살표 표시. 스프라이트는 1개 — 버프는 그대로, 디버프는 Y축 flip. 무변화면 숨김.
    void SetStatArrow(Image arrow, int baseSkill, int effectiveSkill)
    {
        if (arrow == null) return;
        if (effectiveSkill == baseSkill) { arrow.enabled = false; return; }

        bool debuff = effectiveSkill < baseSkill;
        arrow.sprite  = statArrowSprite;
        arrow.color   = EmployeeData.GetStatColor(baseSkill, effectiveSkill);
        arrow.enabled = true;

        var rt = arrow.rectTransform;
        var s  = rt.localScale;
        float magY = Mathf.Abs(s.y);
        rt.localScale = new Vector3(s.x, debuff ? -magY : magY, s.z);
    }

    // 라벨에 "{kind} : {이름}"(없으면 "{kind} : 없음") 표시 + 등급 충족(unlocked)이고 설명 있으면 클릭 시 AlertUI3(ShowPortrait) 로 최상단 표시.
    static void WireDesc(TextMeshProUGUI label, GameObject lockedPanel,
                         string name, string desc, string kind, string portraitId, bool unlocked)
    {
        bool has = !string.IsNullOrEmpty(name);
        // 있는데 등급 미충족일 때만 덮개 노출 (미보유는 덮을 게 없음)
        if (lockedPanel != null) lockedPanel.SetActive(has && !unlocked);
        if (label == null) return;

        label.text = has ? $"{kind} : {name}" : $"{kind} : 없음";

        bool clickable = has && unlocked && !string.IsNullOrEmpty(desc);
        label.raycastTarget = clickable;

        var btn = label.GetComponent<Button>();
        if (clickable)
        {
            if (btn == null) btn = label.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => AlertUI.Instance?.ShowPortrait(desc, portraitId, name));
        }
        else if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
        }
    }

    void RefreshConfirm()
    {
        if (confirmButton != null)
            confirmButton.interactable = !string.IsNullOrEmpty(_selectedId);
    }

    // 확정 버튼(dispatchConfirmBtn)에 연결 — 모드에 따라 파견 요청 또는 팀장 지정
    public void OnConfirm()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        string id = _selectedId;

        if (_mode == PanelMode.Leader)
        {
            var emp = FindEmployee(id);
            Close();
            if (emp != null) DevelopmentManager.Instance.SetLeader(_leaderType, emp);
            var cb = _onLeaderSelected;
            _onLeaderSelected = null;
            cb?.Invoke();
        }
        else
        {
            Close();
            DispatchManager.Instance?.RequestDispatch(id);
        }
    }

    static int RoleOrder(EmployeeRole role) => role switch
    {
        EmployeeRole.Planner    => 0,
        EmployeeRole.Programmer => 1,
        EmployeeRole.Artist     => 2,
        _ => 3
    };
}
