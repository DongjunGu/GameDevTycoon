using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private bool _triggered50 = false;
    private List<RandomEventData> _eventPool = new();
    private List<RandomEventData> _conditionEventPool = new();
    private Queue<string> _nextWeekPopups = new();


    [Header("개발 이벤트")]
    [Range(0f, 1f)] public float eventTriggerChance = 0.5f;
    [Range(0f, 1f)] public float blackoutChance = 0.5f;
    [Range(0f, 1f)] public float teamDinnerChance = 0.5f;
    [Range(0f, 1f)] public float eventDetailTriggerChance = 0.5f;

    [Header("조건 이벤트")]
    [Range(0f, 1f)] public float employeeRunChance = 0.5f;
    [Range(0f, 1f)] public float employeeFightChance = 0.5f;
    [Range(0f, 1f)] public float badCompanyChance = 0.3f;
    [Header("투자 이벤트")]
    [Range(0f, 1f)] public float investmentTriggerChance = 0.5f;
    public float investmentThreshold = 80f;  // 달성 기준 수치
    public int investmentReward = 1000; // 성공/실패 금액

    // ── 상태 프로퍼티 (RandomEvents_Dev에서 접근) ──
    public bool InvestmentAccepted { get; set; } = false;
    public string InvestmentStat { get; set; } = "";
    public string InvestmentStatName { get; set; } = "";
    public void SetTriggered50(bool value) => _triggered50 = value;


    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 조건 이벤트 풀은 게임 내내 활성화
        RandomEvents_Condition.Register(_conditionEventPool, this);
    }

    void Start()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged += OnWeekChanged;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnWeekChanged;
    }

    void OnWeekChanged()
    {
        while (_nextWeekPopups.Count > 0)
            AlertUI.Instance.Show(_nextWeekPopups.Dequeue());
    }

    public void InitEvents()
    {
        _triggered50 = false;
        InvestmentAccepted = false;
        InvestmentStat = "";
        InvestmentStatName = "";
        _eventPool.Clear();
        RandomEvents_Dev.Register(_eventPool, this);
    }

    public void Reset()
    {
        _triggered50 = false;
        InvestmentAccepted = false;
        InvestmentStat = "";
        InvestmentStatName = "";
        InvestmentProgressUI.Instance?.Hide();
    }

    // ── 개발중 이벤트 트리거 (DevelopmentCoroutine에서 호출) ──
    public void CheckTrigger(float progress)
    {
        if (!_triggered50 && progress >= 0.5f)
        {
            _triggered50 = true;
            TryTriggerEvent();
        }
    }

    void TryTriggerEvent()
    {
        if (UnityEngine.Random.value > eventTriggerChance) return;
        if (_eventPool.Count == 0) return;

        var evt = _eventPool[UnityEngine.Random.Range(0, _eventPool.Count)];
        if (UnityEngine.Random.value > evt.triggerChance) return;

        DevelopmentManager.Instance.PauseForEvent();
        RandomEventUI.Instance.Show(evt);
    }

    public void TriggerInvestmentEvent(System.Action onComplete)
    {
        if (UnityEngine.Random.value > investmentTriggerChance)
        {
            onComplete?.Invoke();
            return;
        }

        string[] stats = { "planning", "develop", "art", "creativity" };
        string[] statNames = { "기획", "개발", "아트", "창의성" };
        int idx = UnityEngine.Random.Range(0, stats.Length);

        InvestmentStat = stats[idx];
        InvestmentStatName = statNames[idx];

        ConfirmUI.Instance.Show(
            $"투자자가 찾아왔습니다!\n{statNames[idx]} 수치가 {investmentThreshold}점 이상이면\n{investmentReward:N0}G 지급\n달성 실패 시 {investmentReward:N0}G 차감",
            onConfirm: () =>
            {
                InvestmentAccepted = true;
                InvestmentProgressUI.Instance?.Show(InvestmentStatName, investmentThreshold);
                onComplete?.Invoke(); // ← 수락 후 호출
            },
            onCancel: () =>
            {
                onComplete?.Invoke(); // ← 거절 후 호출
            },
            confirmText: "수락",
            cancelText: "거절"
        );
    }

    public void CheckInvestmentResult(float planning, float develop, float art, float creativity, System.Action onComplete = null)
    {
        if (!InvestmentAccepted || string.IsNullOrEmpty(InvestmentStat))
        {
            onComplete?.Invoke();
            return;
        }

        float value = InvestmentStat switch
        {
            "planning" => planning,
            "develop" => develop,
            "art" => art,
            "creativity" => creativity,
            _ => 0f
        };

        InvestmentAccepted = false;
        InvestmentProgressUI.Instance?.Hide();

        if (value >= investmentThreshold)
        {
            MoneyManager.Instance.AddGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 성공!\n{InvestmentStatName} 수치: {value:F0}점\n{investmentReward:N0}G를 받았습니다!",
                () => onComplete?.Invoke()
            );
        }
        else
        {
            MoneyManager.Instance.ForceSpendGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 실패...\n{InvestmentStatName} 수치: {value:F0}점\n{investmentReward:N0}G를 잃었습니다.",
                () => onComplete?.Invoke()
            );
        }
    }

    public void CheckConditionEvents()
    {
        foreach (var emp in new List<EmployeeData>(EmployeeManager.Instance.ownedEmployees))
        {
            if (emp.satisfaction >= 40) continue;

            float triggerChance = (50 - emp.satisfaction) / 100f;
            if (UnityEngine.Random.value >= triggerChance) continue;

            var captured = emp;
            if (UnityEngine.Random.value < 0.7f)
                TriggerEmployeeResignationEvent(captured);
            else
                TriggerEmployeeRunEvent(captured);
        }
    }

    // ── stub 메서드 (구현 예정) ───────────────

    public void TriggerScoutEvent() { }
    public void TriggerBetaTestEvent() { }
    public void TriggerAlgorithmEvent() { }
    public void TriggerEmployeeFightEvent() { }
    public void TriggerBadCompanyEvent() { }

    public void TriggerEmployeeResignationEvent(EmployeeData emp)
    {
        // TODO: 야근 모드 구현 후 IsOvertimeActive 조건 교체
        bool isOvertime = false;
        string message = isOvertime
            ? RandomEvents_Condition.ResignationOvertimeMessage
            : RandomEvents_Condition.ResignationMessages[
                UnityEngine.Random.Range(0, RandomEvents_Condition.ResignationMessages.Length)];

        EventUI.Instance.Show("사직서 제출", emp.portraitId, $"{emp.employeeName}\n\n{message}", () =>
        {
            EmployeeManager.Instance.FireEmployee(emp);
            EmployeeManager.Instance.ReduceAllSatisfactionExcept(10, emp);
            AlertUI.Instance.Show(
                $"{emp.employeeName}이(가) 사직서를 제출하고 퇴사했습니다.\n남은 직원들의 만족도가 10 하락합니다."
            );
        });
    }

    public void TriggerEmployeeRunEvent(EmployeeData emp)
    {
        string message = RandomEvents_Condition.RunAwayMessages[
            UnityEngine.Random.Range(0, RandomEvents_Condition.RunAwayMessages.Length)];

        EventUI.Instance.Show("직원 도망", emp.portraitId, $"{emp.employeeName}\n\n{message}", () =>
        {
            EmployeeManager.Instance.FireEmployee(emp);
            EmployeeManager.Instance.ReduceAllSatisfactionExcept(10, emp);
            AlertUI.Instance.Show(
                $"{emp.employeeName}이(가) 도망쳤습니다.\n남은 직원들의 만족도가 10 하락합니다."
            );
            // TODO: 1주 후 팝업 내용 확정 후 아래 내용 교체
            _nextWeekPopups.Enqueue($"[{emp.employeeName}]\n...");
        });
    }
}