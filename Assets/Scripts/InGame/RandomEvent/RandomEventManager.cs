using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private bool _triggered50 = false;
    private List<RandomEventData> _eventPool = new();
    private List<RandomEventData> _conditionEventPool = new();


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
        // 만족도 40 미만 직원 중 한 명만 퇴사 이벤트 체크
        var candidates = EmployeeManager.Instance.ownedEmployees
            .FindAll(e => e.satisfaction < 40);
        if (candidates.Count == 0) return;

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        TryTriggerConditionEvent(RandomEventType.EmployeeRun, candidates[idx]);
    }

    void TryTriggerConditionEvent(RandomEventType type, EmployeeData target = null)
    {
        var evt = _conditionEventPool.Find(e => e.type == type);
        if (evt == null) return;
        if (UnityEngine.Random.value > evt.triggerChance) return;

        _pendingResignTarget = target;
        evt.onApply?.Invoke();
    }


    // ── stub 메서드 (구현 예정) ───────────────

    public void TriggerScoutEvent() { }
    public void TriggerBetaTestEvent() { }
    public void TriggerAlgorithmEvent() { }
    public void TriggerEmployeeFightEvent() { }
    public void TriggerBadCompanyEvent() { }

    private EmployeeData _pendingResignTarget;

    public void TriggerEmployeeRunEvent()
    {
        var emp = _pendingResignTarget;
        _pendingResignTarget = null;
        if (emp == null) return;

        AlertUI.Instance?.Show(
            $"{emp.employeeName}이(가) 만족도 저하로 퇴사했습니다.",
            () => EmployeeManager.Instance.FireEmployee(emp)
        );
    }
}