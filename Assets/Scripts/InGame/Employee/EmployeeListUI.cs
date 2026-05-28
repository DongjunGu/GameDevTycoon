using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 해고 UI — 채용(HiringUI) 흐름을 미러링.
// 보유 직원을 EmployeeSlotPrefab2(채용과 동일) 슬롯 리스트로 표시 → 선택 → 이력서 캐러셀(좌:이전/중앙:현재/우:다음, 좌우 화살표) →
// confirmBtn("해고") 클릭 시 ConfirmUI 로 "{이름}을 해고하시겠습니까?" 확인 → 네=해고+목록 복귀 / 아니오=ConfirmUI 만 닫음.
public class EmployeeListUI : MonoBehaviour
{
    public static EmployeeListUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject listPanel;
    public GameObject confirmPanel;          // ConfirmFirePanel

    [Header("List")]
    public Transform slotParent;
    public GameObject employeeSlotPrefab;    // EmployeeSlotPrefab2 (채용과 동일)

    [Header("Resume Carousel")]
    public ResumeFlipper resumeFlipper;
    public EmployeeResumePanel resumeCenterPanel;
    public EmployeeResumePanel resumeLeftPanel;
    public EmployeeResumePanel resumeRightPanel;
    public Button prevButton;
    public Button nextButton;

    private readonly List<EmployeeData> _list = new();
    private int _currentIndex = -1;
    private EmployeeData _selectedEmployee;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (prevButton != null) prevButton.onClick.AddListener(OnClickPrev);
        if (nextButton != null) nextButton.onClick.AddListener(OnClickNext);
    }

    public void OpenList()
    {
        GameTimeManager.Instance?.StopTime();
        gameObject.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        ShowList();
    }

    // 외부(커플 동반퇴사/사직/도주 등)에서 ownedEmployees 변경 시, 리스트가 열려있으면 다시 빌드.
    public void RefreshIfOpen()
    {
        if (listPanel != null && listPanel.activeInHierarchy) ShowList();
    }

    void ShowList()
    {
        if (slotParent != null)
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (listPanel != null) listPanel.SetActive(true);

        if (slotParent == null || employeeSlotPrefab == null) return;
        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
        {
            var slot = Instantiate(employeeSlotPrefab, slotParent);
            slot.GetComponent<EmployeeSlotUI>().Setup(employee, OnSelectEmployee);
        }
    }

    // 슬롯 선택 → 캐러셀로 진입(선택 직원이 가운데).
    public void OnSelectEmployee(EmployeeData employee)
    {
        _list.Clear();
        _list.AddRange(EmployeeManager.Instance.ownedEmployees);
        int idx = _list.IndexOf(employee);
        _currentIndex = idx >= 0 ? idx : 0;

        // 패널을 먼저 활성화한 뒤 Populate — 비활성 상태에서 등급 셰머 코루틴 StartCoroutine 시 에러 방지.
        if (listPanel != null) listPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(true);
        PopulateResume();
        UpdateArrowButtons();
    }

    public void OnClickNext() => FlipTo(+1);
    public void OnClickPrev() => FlipTo(-1);

    void FlipTo(int dir)
    {
        if (_list.Count <= 1) return;
        if (resumeFlipper != null && resumeFlipper.IsFlipping) return;

        int next = (_currentIndex + dir + _list.Count) % _list.Count;
        if (resumeFlipper != null)
            resumeFlipper.Flip(dir, () => { _currentIndex = next; PopulateResume(); });
        else { _currentIndex = next; PopulateResume(); }
    }

    // 보유 직원 2명 이상일 때만 화살표 + 좌/우 이력서 노출 (1명 이하면 가운데만).
    void UpdateArrowButtons()
    {
        bool multi = _list.Count > 1;
        if (prevButton != null) prevButton.gameObject.SetActive(multi);
        if (nextButton != null) nextButton.gameObject.SetActive(multi);
        if (resumeLeftPanel  != null) resumeLeftPanel.gameObject.SetActive(multi);
        if (resumeRightPanel != null) resumeRightPanel.gameObject.SetActive(multi);
    }

    // 좌:이전 / 가운데:현재 / 우:다음 (양끝 순환)
    void PopulateResume()
    {
        if (_list.Count == 0) return;
        int n = _list.Count, ci = _currentIndex;
        var center = _list[ci];
        _selectedEmployee = center;
        if (resumeCenterPanel != null) resumeCenterPanel.Setup(center);
        if (resumeLeftPanel  != null) resumeLeftPanel.Setup(_list[(ci - 1 + n) % n]);
        if (resumeRightPanel != null) resumeRightPanel.Setup(_list[(ci + 1) % n]);
    }

    // confirmBtn("해고") — 즉시 해고하지 않고 확인 다이얼로그.
    public void OnClickFire()
    {
        if (_selectedEmployee == null) return;
        ConfirmUI.Instance.Show(
            $"{_selectedEmployee.employeeName}을(를) 해고하시겠습니까?",
            onConfirm: DoFire,
            onCancel: () => { },     // 아니오 — ConfirmUI 만 닫고 이력서 유지
            confirmText: "네",
            cancelText: "아니오"
        );
    }

    void DoFire()
    {
        if (_selectedEmployee == null) return;
        EmployeeManager.Instance.FireEmployee(_selectedEmployee);
        HUDUI.Instance?.RefreshAll();
        _selectedEmployee = null;
        if (confirmPanel != null) confirmPanel.SetActive(false);
        ShowList();   // 해고된 직원 빠진 목록으로 복귀 (계속 해고 가능)
    }

    // 확인 화면 → 목록으로
    public void OnClickBack()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (listPanel != null) listPanel.SetActive(true);
    }

    // 닫기
    public void OnClickClose()
    {
        GameTimeManager.Instance?.StartTime();
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (listPanel != null) listPanel.SetActive(false);
    }
}
