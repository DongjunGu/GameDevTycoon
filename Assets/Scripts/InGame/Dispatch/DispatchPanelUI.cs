using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 파견 패널 — 메뉴-프로젝트-파견 버튼(dispatchBtn) OnClick → Open(). CEO 제외 보유 직원 리스트에서
// 한 명 선택 후 파견확정 → DispatchManager.RequestDispatch. 이미 파견중인 직원은 badge + 선택 불가.
// 강화(TrainingPanelUI.OpenPanel) / 해고(EmployeeListUI.OpenList) 와 동일 패턴:
// 버튼이 Open() 을 호출하고, 그 안에서 시간정지 + SetActive + 리스트 빌드를 한 번에 한다.
public class DispatchPanelUI : MonoBehaviour
{
    public static DispatchPanelUI Instance { get; private set; }

    [Header("UI")]
    public GameObject panelRoot;          // 토글되는 모달 패널(DispatchPanel). 비우면 this.gameObject
    public Transform  slotParent;         // 슬롯이 쌓일 Content
    public GameObject slotPrefab;         // DispatchSlotUI 부착 프리팹
    public Button     confirmButton;      // 파견확정

    private string _selectedId = "";
    private readonly List<DispatchSlotUI> _slots = new();

    void Awake()
    {
        Instance = this;
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
        RefreshConfirm();
    }

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
