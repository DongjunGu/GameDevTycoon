using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 파견 패널 — 메뉴-프로젝트-파견 버튼(dispatchBtn) OnClick → Open(). CEO 제외 보유 직원 리스트에서
// 한 명 선택 후 파견확정 → DispatchManager.RequestDispatch. 이미 파견중인 직원은 badge + 선택 불가.
//
// 좌(DispatchScrollView/Content): 파견 가능한 직원 슬롯 리스트(DispatchSlotPrefab).
// 우(DispatchRightPanel/ChildPanel): 슬롯 selectButton 클릭 시 상세 표시 — 초상화/특성/전용이벤트/능력치 4종.
//   강화 패널(TrainingPanelUI)·이력서(EmployeeResumePanel) 와 동일 구성. 선택 전엔 상세 3패널 비활성.
//   닫기/확정 버튼(dispatchCloseBtn/dispatchConfirmBtn)은 ChildPanel/BottomPanel 안에 있어 항상 표시 유지
//   (BottomPanel 은 detailSections 토글 대상에서 제외 — 선택 전에도 닫기 가능해야 하므로).
public class DispatchPanelUI : MonoBehaviour
{
    public static DispatchPanelUI Instance { get; private set; }

    [Header("UI")]
    public GameObject panelRoot;          // 토글되는 모달 패널(DispatchPanel). 비우면 this.gameObject
    public Transform  slotParent;         // 슬롯이 쌓일 Content
    public GameObject slotPrefab;         // DispatchSlotUI 부착 프리팹
    public Button     confirmButton;      // 파견확정 (dispatchConfirmBtn)
    public Button     closeButton;        // 닫기 (dispatchCloseBtn)

    [Header("Detail (DispatchRightPanel/ChildPanel) — 슬롯 선택 시 표시)")]
    public GameObject[] detailSections;   // 선택 전 비활성: TopPanel / TraitAEventPanel / AbilityPanel
    public GameObject   emptyPanel;       // 선택 전 placeholder — detailSections 와 반대로 토글(VerticalLayout 빈자리 방지)
    public Image portraitImage;           // 초상화
    public Image portraitBGImage;         // 등급 색 배경
    public TextMeshProUGUI traitText;     // "특성 : {}" (클릭 시 traitDetailPanel 토글)
    public TextMeshProUGUI eventText;     // "이벤트 : {}" (클릭 시 eventDetailPanel 토글)
    public GameObject traitDetailPanel;   // 특성 설명 패널(Image + 자식 TMP, 기본 숨김)
    public GameObject eventDetailPanel;   // 전용 이벤트 설명 패널
    public GameObject traitLockedPanel;   // 특성 없음(Epic 미만) 시 표시할 잠금 오버레이
    public GameObject eventLockedPanel;   // 전용 이벤트 없음(Unique 미만) 시 표시할 잠금 오버레이
    public TextMeshProUGUI planningText;  // 기획 수치
    public TextMeshProUGUI developText;   // 개발 수치
    public TextMeshProUGUI artText;       // 아트 수치
    public TextMeshProUGUI creativityText;// 창의성 수치

    private string _selectedId = "";
    private Coroutine _gradeCo;
    private readonly List<DispatchSlotUI> _slots = new();

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

        GameTimeManager.Instance?.StopTime();
        var root = panelRoot != null ? panelRoot : gameObject;
        root.SetActive(true);
        BuildList();
        HideDetail();
    }

    // 닫기/취소 버튼에 연결
    public void Close()
    {
        GameTimeManager.Instance?.StartTime();
        var root = panelRoot != null ? panelRoot : gameObject;
        root.SetActive(false);
    }

    void BuildList()
    {
        if (slotParent == null || slotPrefab == null) return;
        if (EmployeeManager.Instance == null) return;

        _selectedId = "";
        _slots.Clear();
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        // ownedEmployees 는 이미 CEO 제외
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            var slot = Instantiate(slotPrefab, slotParent);
            slot.SetActive(true); // 프리팹 루트가 비활성으로 저장돼 있어도 스폰 시 강제 활성
            var s    = slot.GetComponent<DispatchSlotUI>();
            if (s == null) continue;
            bool dispatched = DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id);
            s.Setup(emp, this, dispatched);
            _slots.Add(s);
        }

        RefreshConfirm();
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
                ? Resources.Load<Sprite>($"Portraits/{emp.portraitId}") : null;
            portraitImage.sprite  = sp;
            portraitImage.enabled = sp != null;
        }

        // 등급 색 배경
        ApplyGradeColor(emp.grade);

        // 특성 / 전용 이벤트 — 라벨엔 이름, 클릭 시 설명 패널 토글. 없으면(Epic/Unique 미만) LockedPanel 표시.
        WireDesc(traitText, traitDetailPanel, traitLockedPanel,
                 emp.isCEO ? "" : CharacterTraitApplier.GetTraitName(emp),
                 CharacterTraitApplier.GetTraitDescription(emp), "특성");
        WireDesc(eventText, eventDetailPanel, eventLockedPanel,
                 CharacterUniqueEvents.GetEventName(emp),
                 CharacterUniqueEvents.GetEventDescription(emp), "이벤트");

        // 능력치 — 확정 수치
        SetText(planningText,   emp.planningSkill.ToString());
        SetText(developText,    emp.developSkill.ToString());
        SetText(artText,        emp.artSkill.ToString());
        SetText(creativityText, emp.creativitySkill.ToString());
    }

    void ApplyGradeColor(EmployeeGrade grade)
    {
        if (portraitBGImage == null) return;
        if (_gradeCo != null) { StopCoroutine(_gradeCo); _gradeCo = null; }
        _gradeCo = EmployeeGradeColor.Apply(this, portraitBGImage, grade);
    }

    // 라벨에 "{kind} : {이름}"(없으면 "{kind} : 없음") 표시 + 설명 있으면 TextDetailPopup 부착.
    //  → 라벨 클릭 시 detailPanel 을 최상단에 표시, 바깥 클릭 시 닫힘. 패널은 기본 숨김.
    static void WireDesc(TextMeshProUGUI label, GameObject detailPanel, GameObject lockedPanel,
                         string name, string desc, string kind)
    {
        if (detailPanel != null) detailPanel.SetActive(false); // 항상 숨김으로 시작

        bool has = !string.IsNullOrEmpty(name);
        if (lockedPanel != null) lockedPanel.SetActive(!has);  // 없으면 잠금 오버레이 표시
        if (label == null) return;

        label.text = has ? $"{kind} : {name}" : $"{kind} : 없음";

        bool clickable = has && !string.IsNullOrEmpty(desc) && detailPanel != null;
        var popup = label.GetComponent<TextDetailPopup>();
        if (clickable)
        {
            if (popup == null) popup = label.gameObject.AddComponent<TextDetailPopup>();
            popup.Setup(detailPanel, desc);
        }
        else
        {
            if (popup != null) popup.Setup(null, "");
            label.raycastTarget = false;
        }
    }

    static void SetText(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }

    void RefreshConfirm()
    {
        if (confirmButton != null)
            confirmButton.interactable = !string.IsNullOrEmpty(_selectedId);
    }

    // 파견확정 버튼에 연결
    public void OnConfirm()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        string id = _selectedId;
        Close();
        DispatchManager.Instance?.RequestDispatch(id);
    }
}
