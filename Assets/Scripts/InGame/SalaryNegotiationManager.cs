using System.Collections.Generic;
using UnityEngine;

public enum NegotiationCriteria { HighStat, LowSalary, LowSatisfaction }

public class SalaryNegotiationManager : MonoBehaviour
{
    [Header("UI")]
    public string employeeSlotAreaName = "EmployeeSlotArea";
    public GameObject ownedEmployeeSlotPrefab;
    private GameObject _employeeSlotArea;

    public static SalaryNegotiationManager Instance { get; private set; }

    // ── 상태 ──────────────────────────────────────────────────────
    private EmployeeData _currentEmployee;
    private int          _proposedSalary;
    private bool         _freezeIsAngry;

    // ── 협상 단계 ─────────────────────────────────────────────────
    private enum NegotiationPhase { Idle, Proposal, Response }
    private NegotiationPhase _phase = NegotiationPhase.Idle;
    private string _responseGroupId;
    private string _selectedAcceptGroupId;
    private string _selectedFreezeGroupId;
    private string _pendingSatisfactionAlert;

    // ── 스케줄 ────────────────────────────────────────────────────
    private bool _negotiatedThisYear = false;
    private int  _scheduledMonth     = 0;
    private int  _scheduledWeek      = 0;
    private GameObject _currentSlot;

    public int  ScheduledMonth     => _scheduledMonth;
    public int  ScheduledWeek      => _scheduledWeek;
    public bool NegotiatedThisYear => _negotiatedThisYear;

    // ── 다이얼로그 그룹 ID 풀 ─────────────────────────────────────
    static readonly string[] AcceptGroupIds = {
        "event_sal_acc_1", "event_sal_acc_2", "event_sal_acc_3"
    };
    static readonly string[] FreezeNeutralGroupIds = {
        "event_sal_frz_neu_1", "event_sal_frz_neu_2"
    };
    static readonly string[] FreezeAngryGroupIds = {
        "event_sal_frz_ang_1", "event_sal_frz_ang_2"
    };

    // ── 생명주기 ──────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        DialogManager.Instance.OnChoiceResult += HandleDialogResult;
        DialogManager.Instance.OnDialogEnd    += OnDialogEnd;
    }

    void OnDestroy()
    {
        if (DialogManager.Instance == null) return;
        DialogManager.Instance.OnChoiceResult -= HandleDialogResult;
        DialogManager.Instance.OnDialogEnd    -= OnDialogEnd;
    }

    // 새 런 시작 — 협상 스케줄/진행 상태 전부 초기화 (메모리 only, GameTime 저장 시 자동 반영)
    public void ResetForNewRun()
    {
        _scheduledMonth = 0;
        _scheduledWeek = 0;
        _negotiatedThisYear = false;
        _phase = NegotiationPhase.Idle;
        _currentEmployee = null;
        _pendingSatisfactionAlert = null;
    }

    // ── 스케줄 저장/복원 ──────────────────────────────────────────
    public void LoadSchedule(int month, int week, bool negotiatedThisYear)
    {
        _scheduledMonth     = month;
        _scheduledWeek      = week;
        _negotiatedThisYear = negotiatedThisYear;
        Debug.Log($"[연봉협상] 스케줄 복원: {month}월 {week}주 / 완료여부: {negotiatedThisYear}");
    }

    // ── 새해(1월 1주) 호출 — 올해 협상 주차를 미리 뽑아둠 ─────────
    public void ResetYearFlag()
    {
        _negotiatedThisYear = false;
        int roll        = UnityEngine.Random.Range(0, 8);
        _scheduledMonth = roll < 4 ? 11 : 12;
        _scheduledWeek  = (roll % 4) + 1;
        Debug.Log($"[연봉협상] 올해 예정 주차: {_scheduledMonth}월 {_scheduledWeek}주");
    }

    // ── 11~12월 매주 체크 ─────────────────────────────────────────
    public void CheckNegotiationTrigger(int month, int week)
    {
        if (_negotiatedThisYear) return;
        if (_scheduledMonth == 0) return;
        if (month != _scheduledMonth || week != _scheduledWeek) return;

        var (target, criteria) = SelectNegotiationTarget();
        if (target == null) return;

        _negotiatedThisYear = true;
        StartNegotiation(target, criteria);
    }

    // ── 대상 선정 ─────────────────────────────────────────────────
    (EmployeeData emp, NegotiationCriteria criteria) SelectNegotiationTarget()
    {
        int currentYear = GameTimeManager.Instance.Year;
        var eligible    = EmployeeManager.Instance.ownedEmployees
            .FindAll(e => e.hiredYear > 0 && e.hiredYear < currentYear);

        if (eligible.Count == 0) return (null, NegotiationCriteria.HighStat);

        int criteriaIdx = UnityEngine.Random.Range(0, 3);
        var criteria    = (NegotiationCriteria)criteriaIdx;
        List<EmployeeData> candidates;

        if (criteriaIdx == 0) // 주스탯 최고
        {
            int max = 0;
            foreach (var e in eligible) if (e.GetMainStat() > max) max = e.GetMainStat();
            candidates = eligible.FindAll(e => e.GetMainStat() == max);
        }
        else if (criteriaIdx == 1) // 연봉 최저
        {
            int min = int.MaxValue;
            foreach (var e in eligible) if (e.salary < min) min = e.salary;
            candidates = eligible.FindAll(e => e.salary == min);
        }
        else // 만족도 최저
        {
            int min = int.MaxValue;
            foreach (var e in eligible) if (e.satisfaction < min) min = e.satisfaction;
            candidates = eligible.FindAll(e => e.satisfaction == min);
        }

        return (candidates[UnityEngine.Random.Range(0, candidates.Count)], criteria);
    }

    // ── 협상 시작 ─────────────────────────────────────────────────
    void StartNegotiation(EmployeeData emp, NegotiationCriteria criteria)
    {
        _currentEmployee = emp;
        _phase           = NegotiationPhase.Proposal;

        // 연봉 인상 금액 계산 (8~12% / 5~10%)
        int raisePercent = criteria == NegotiationCriteria.HighStat
            ? UnityEngine.Random.Range(8, 13)
            : UnityEngine.Random.Range(5, 11);
        _proposedSalary = emp.salary + Mathf.RoundToInt(emp.salary * raisePercent / 100f);

        // 동결 결과 사전 계산
        int angryChance = Mathf.Max(0, 90 - emp.satisfaction);
        _freezeIsAngry  = UnityEngine.Random.Range(0, 100) < angryChance;

        // 응답 그룹 사전 선정
        _selectedAcceptGroupId = AcceptGroupIds[UnityEngine.Random.Range(0, AcceptGroupIds.Length)];
        string[] freezePool    = _freezeIsAngry ? FreezeAngryGroupIds : FreezeNeutralGroupIds;
        _selectedFreezeGroupId = freezePool[UnityEngine.Random.Range(0, freezePool.Length)];

        // 플레이버 그룹 선정 (코워커 이름 플레이스홀더 포함)
        string flavorGroupId = SelectFlavorGroupId(emp, criteria);

        Debug.Log($"연봉협상 시작: {emp.employeeName} / 기준: {criteria} / 인상율: {raisePercent}%");
        GameTimeManager.Instance.StopTime();
        ShowEmployeeSlot(emp);

        DialogManager.Instance.SetPlaceholder("employeeName", emp.employeeName);
        DialogManager.Instance.SetPlaceholder("portraitId",   emp.portraitId);
        DialogManager.Instance.SetPlaceholder("salary",       emp.salary.ToString("N0"));
        DialogManager.Instance.SetPlaceholder("newSalary",    _proposedSalary.ToString("N0"));
        DialogManager.Instance.Play(flavorGroupId, false);
    }

    // ── 플레이버 그룹 ID 선택 ─────────────────────────────────────
    string SelectFlavorGroupId(EmployeeData emp, NegotiationCriteria criteria)
    {
        switch (criteria)
        {
            case NegotiationCriteria.HighStat:
                return emp.isFemale ? "event_sal_hs_female" : "event_sal_hs_common";

            case NegotiationCriteria.LowSalary:
                if (emp.enhancementLevel <= 10) return "event_sal_ls_low_enh";
                int pick = UnityEngine.Random.Range(0, 3);
                if (pick == 0)
                {
                    var others = EmployeeManager.Instance.ownedEmployees
                        .FindAll(e => e.id != emp.id);
                    string coworkerName = others.Count > 0
                        ? others[UnityEngine.Random.Range(0, others.Count)].employeeName
                        : "다른 직원";
                    DialogManager.Instance.SetPlaceholder("coworkerName", coworkerName);
                    return "event_sal_ls_coworker";
                }
                return pick == 1 ? "event_sal_ls_dog" : "event_sal_ls_quit";

            case NegotiationCriteria.LowSatisfaction:
                return new[] { "event_sal_lsat_1", "event_sal_lsat_2", "event_sal_lsat_3" }
                    [UnityEngine.Random.Range(0, 3)];

            default:
                return "event_sal_hs_common";
        }
    }

    // ── 선택 결과 처리 ────────────────────────────────────────────
    void HandleDialogResult(string resultType, int resultValue)
    {
        if (resultType == "SalaryAccept")
        {
            if (_currentEmployee == null) return;
            _currentEmployee.salary = _proposedSalary;
            _currentEmployee.ChangeSatisfaction(+20);
            EmployeeManager.Instance.UpdateEmployee(_currentEmployee);
            HUDUI.Instance?.RefreshAll();
            _responseGroupId = _selectedAcceptGroupId;
            _pendingSatisfactionAlert = $"{_currentEmployee.employeeName}의 만족도가 20 상승했습니다.";
            Debug.Log($"{_currentEmployee.employeeName} 연봉 수락: {_proposedSalary:N0}G / 만족도 +20");
        }
        else if (resultType == "SalaryFreeze")
        {
            if (_currentEmployee == null) return;
            if (_freezeIsAngry)
            {
                _currentEmployee.ChangeSatisfaction(-20);
                EmployeeManager.Instance.UpdateEmployee(_currentEmployee);
                HUDUI.Instance?.RefreshAll();
                _pendingSatisfactionAlert = $"{_currentEmployee.employeeName}의 만족도가 20 하락했습니다.";
                Debug.Log($"{_currentEmployee.employeeName} 연봉 동결 거부: 만족도 -20");
            }
            else
            {
                Debug.Log($"{_currentEmployee.employeeName} 연봉 동결 수용: 효과 없음");
            }
            _responseGroupId = _selectedFreezeGroupId;
        }
    }

    void OnDialogEnd()
    {
        if (_phase == NegotiationPhase.Idle) return;

        if (_phase == NegotiationPhase.Proposal)
        {
            _phase = NegotiationPhase.Response;
            DialogManager.Instance.Play(_responseGroupId, false);
            return;
        }

        // Response 단계 완료
        _phase = NegotiationPhase.Idle;
        _currentEmployee = null;
        HideEmployeeSlot();
        Finish();

        if (!string.IsNullOrEmpty(_pendingSatisfactionAlert))
        {
            string msg = _pendingSatisfactionAlert;
            _pendingSatisfactionAlert = null;
            AlertUI.Instance?.Show(msg);
        }
    }

    void Finish()
    {
        Debug.Log("연봉협상 완료");
        GameTimeManager.Instance.SaveGameTime();
        MoneyManager.Instance?.SaveMoney();
        ProjectSaveManager.Instance?.SaveProject();
        GameTimeManager.Instance.ForceStartTime();
        DialogManager.Instance.ClearPlaceholders();
    }

    // ── 직원 슬롯 UI ──────────────────────────────────────────────
    void ShowEmployeeSlot(EmployeeData employee)
    {
        if (_currentSlot != null) Destroy(_currentSlot);
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
        if (_employeeSlotArea != null) _employeeSlotArea.SetActive(false);
    }

    void HideEmployeeSlot()
    {
        if (_currentSlot != null) { Destroy(_currentSlot); _currentSlot = null; }
        if (_employeeSlotArea != null) _employeeSlotArea.SetActive(false);
    }
}
