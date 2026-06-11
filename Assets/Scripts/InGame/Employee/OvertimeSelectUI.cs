using System.Collections.Generic;
using UnityEngine;

public class OvertimeSelectUI : MonoBehaviour
{
    public static OvertimeSelectUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject overtimeSelectPanel;

    [Header("List")]
    public Transform slotParent;
    public GameObject overtimeEmployeeSlotPrefab;

    private System.Action _onDone;
    private readonly List<OvertimeEmployeeSlotUI> _slots = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        overtimeSelectPanel.SetActive(false);
    }

    public void Open(System.Action onDone)
    {
        _onDone = onDone;

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
            emp.isOvertimeWorker = false;

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);
        _slots.Clear();

        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(emp.id)) continue; // 파견중 직원은 야근 선택 제외
            var go   = Instantiate(overtimeEmployeeSlotPrefab, slotParent);
            var slot = go.GetComponent<OvertimeEmployeeSlotUI>();
            slot.Setup(emp);
            _slots.Add(slot);
        }

        overtimeSelectPanel.SetActive(true);
    }

    // 확인 — 선택된 직원들 야근 적용 후 진행
    public void OnClickConfirm()
    {
        overtimeSelectPanel.SetActive(false);
        _onDone?.Invoke();
    }

    // 건너뛰기 — 야근 없이 진행
    public void OnClickSkip()
    {
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
            emp.isOvertimeWorker = false;
        overtimeSelectPanel.SetActive(false);
        _onDone?.Invoke();
    }
}
