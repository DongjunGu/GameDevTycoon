using System.Collections.Generic;
using UnityEngine;

public class SalaryNegotiationManager : MonoBehaviour
{
    [Header("UI")]
    //public GameObject employeeSlotArea; // 슬롯 표시할 부모 오브젝트
    public string employeeSlotAreaName = "EmployeeSlotArea";
    public GameObject ownedEmployeeSlotPrefab;
    private GameObject _employeeSlotArea;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float resignChance = 0.5f; // 퇴사 확률

    private bool _isResigning = false;
    private GameObject _currentSlot;
    public static SalaryNegotiationManager Instance { get; private set; }

    private Queue<EmployeeData> _negotiationQueue = new();
    private EmployeeData _currentEmployee;
    private int _proposedSalary;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        DialogManager.Instance.OnChoiceResult += HandleDialogResult;
        DialogManager.Instance.OnDialogEnd += OnDialogEnd;
    }

    void OnDestroy()
    {
        if (DialogManager.Instance == null) return;
        DialogManager.Instance.OnChoiceResult -= HandleDialogResult;
        DialogManager.Instance.OnDialogEnd -= OnDialogEnd;
    }

    // ── 협상 시작 (연도 변경 시 호출) ────────
    public void StartNegotiation()
    {
        _negotiationQueue.Clear();

        foreach (var employee in EmployeeManager.Instance.ownedEmployees)
            _negotiationQueue.Enqueue(employee);

        Debug.Log($"연봉협상 시작: {_negotiationQueue.Count}명");
        GameTimeManager.Instance.StopTime();
        ProcessNext();
    }

    void ProcessNext()
    {
        if (_negotiationQueue.Count == 0)
        {
            HideEmployeeSlot();
            Debug.Log("연봉협상 전체 완료");
            GameTimeManager.Instance.SaveGameTime();
            GameTimeManager.Instance.ForceStartTime();
            DialogManager.Instance.ClearPlaceholders();
            return;
        }

        _currentEmployee = _negotiationQueue.Dequeue();
        _proposedSalary = _currentEmployee.salary + 500; //연봉 변경 임시 500

        ShowEmployeeSlot(_currentEmployee);

        // 플레이스홀더 세팅
        DialogManager.Instance.SetPlaceholder("employeeName", _currentEmployee.employeeName);
        DialogManager.Instance.SetPlaceholder("salary", _currentEmployee.salary.ToString("N0"));
        DialogManager.Instance.SetPlaceholder("newSalary", _proposedSalary.ToString("N0"));
        DialogManager.Instance.SetPlaceholder("portraitId", _currentEmployee.portraitId);
        DialogManager.Instance.Play("event_salary_negotiation", false);
    }

    void HandleDialogResult(string resultType, int resultValue)
    {
        if (resultType == "SalaryUpdate")
        {
            if (_currentEmployee == null) return;
            _currentEmployee.salary = _proposedSalary;
            _currentEmployee.ChangeSatisfaction(+10);
            EmployeeManager.Instance.UpdateEmployee(_currentEmployee);
            HUDUI.Instance?.RefreshAll();
            Debug.Log($"{_currentEmployee.employeeName} 연봉 업데이트: {_proposedSalary:N0}G");
        }
        else if (resultType == "SalaryReject")
        {
            // 두번째 거절 → 퇴사 확률 체크
            _currentEmployee?.ChangeSatisfaction(-5); // 거절 시 만족도 하락
            if (UnityEngine.Random.value <= resignChance)
            {
                _isResigning = true;
                DialogManager.Instance.Play("event_salary_resign", false);
            }
        }
    }

    void OnDialogEnd()
    {
        // 협상 진행 중이 아니면 무시
        if (_currentEmployee == null && _negotiationQueue.Count == 0) return;
        if (_currentEmployee == null) return;

        if (_isResigning)
        {
            _isResigning = false;
            var resignedEmployee = _currentEmployee;

            AlertUI.Instance.Show($"{resignedEmployee.employeeName}이(가) 퇴사했습니다.", () =>
            {
                EmployeeManager.Instance.FireEmployee(resignedEmployee);
                GameTimeManager.Instance.SaveGameTime();
                HUDUI.Instance?.RefreshAll();
                HideEmployeeSlot();
                _currentEmployee = null;
                ProcessNext();
            });
            return;
        }

        _currentEmployee = null;
        HideEmployeeSlot();
        ProcessNext();
    }
    void ShowEmployeeSlot(EmployeeData employee)
    {
        if (_currentSlot != null)
            Destroy(_currentSlot);

        if (_employeeSlotArea == null || ownedEmployeeSlotPrefab == null) return;

        _employeeSlotArea.SetActive(true);
        _currentSlot = Instantiate(ownedEmployeeSlotPrefab, _employeeSlotArea.transform);
        _currentSlot.SetActive(true);

        var slotUI = _currentSlot.GetComponent<OwnedEmployeeSlotUI>();
        if (slotUI.fireButton != null)
            slotUI.fireButton.gameObject.SetActive(false);

        slotUI.Setup(employee, null);
    }
    public void SetEmployeeSlotArea(GameObject area)
    {
        _employeeSlotArea = area;
        if (_employeeSlotArea != null)
            _employeeSlotArea.SetActive(false);
        Debug.Log($"EmployeeSlotArea 연결: {_employeeSlotArea != null}");
    }

    void HideEmployeeSlot()
    {
        if (_currentSlot != null)
        {
            Destroy(_currentSlot);
            _currentSlot = null;
        }

        if (_employeeSlotArea != null)
            _employeeSlotArea.SetActive(false);
    }
}