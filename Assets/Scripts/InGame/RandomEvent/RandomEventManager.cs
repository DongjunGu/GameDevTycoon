using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private bool _triggered50 = false;
    private List<RandomEventData> _eventPool = new();
    private List<RandomEventData> _releaseEventPool = new();
    private List<RandomEventData> _conditionEventPool = new();

    [Header("개발 이벤트")]
    [Range(0f, 1f)] public float eventTriggerChance = 0.5f;
    [Range(0f, 1f)] public float blackoutChance = 0.5f;
    [Range(0f, 1f)] public float teamDinnerChance = 0.5f;
    [Range(0f, 1f)] public float eventDetailTriggerChance = 0.5f;

    [Header("출시 이벤트")]
    [Range(0f, 1f)] public float releaseEventTriggerChance = 0.5f;
    [Range(0f, 1f)] public float competitorChance = 0.5f;
    [Range(0f, 1f)] public float perfectTimingChance = 0.5f;
    [Range(0f, 1f)] public float algorithmChance = 0.3f;

    [Header("조건 이벤트")]
    [Range(0f, 1f)] public float employeeRunChance = 0.5f;
    [Range(0f, 1f)] public float employeeFightChance = 0.5f;
    [Range(0f, 1f)] public float badCompanyChance = 0.3f;
    [Header("투자 이벤트")]
    [Range(0f, 1f)] public float investmentTriggerChance = 0.5f;
    public float investmentThreshold = 80f;  // 달성 기준 수치
    public int investmentReward = 1000; // 성공/실패 금액

    // 투자 상태
    private bool _investmentAccepted = false;
    private string _investmentStat = ""; // "planning"/"develop"/"art"/"creativity"

    public bool InvestmentAccepted => _investmentAccepted;
    public string InvestmentStat => _investmentStat;
    private string _investmentStatName = "";
    public string InvestmentStatName => _investmentStatName;


    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitEvents()
    {
        _triggered50 = false;
        _eventPool.Clear();
        _releaseEventPool.Clear();
        _conditionEventPool.Clear();

        RandomEvents_Dev.Register(_eventPool, this);
        RandomEvents_Release.Register(_releaseEventPool, this);
        RandomEvents_Condition.Register(_conditionEventPool, this);
    }

    public void SetTriggered50(bool value) => _triggered50 = value;

    public void Reset() => _triggered50 = false;

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
        if (UnityEngine.Random.value > eventTriggerChance)
        {
            Debug.Log("이벤트 미발동");
            return;
        }

        if (_eventPool.Count == 0) return;
        var evt = _eventPool[UnityEngine.Random.Range(0, _eventPool.Count)];

        if (UnityEngine.Random.value > evt.triggerChance)
        {
            Debug.Log($"이벤트 확률 미달: {evt.type}");
            return;
        }

        Debug.Log($"이벤트 발동: {evt.type}");
        DevelopmentManager.Instance.PauseForEvent();
        RandomEventUI.Instance.Show(evt);
    }

    // ── 출시 이벤트 트리거 (DevelopmentResultUI에서 호출) ──
    public void TryTriggerReleaseEvent(System.Action<float> onComplete)
    {
        if (UnityEngine.Random.value > releaseEventTriggerChance)
        {
            onComplete?.Invoke(0f);
            return;
        }

        var evt = _releaseEventPool[UnityEngine.Random.Range(0, _releaseEventPool.Count)];

        if (UnityEngine.Random.value > evt.triggerChance)
        {
            onComplete?.Invoke(0f);
            return;
        }

        float bonus = evt.scoreBonus;
        AlertUI.Instance.Show(
            $"{evt.title}\n{evt.description}",
            () => onComplete?.Invoke(bonus)
        );
    }

    // ── 조건 이벤트 체크 ──────────────────────
    public void CheckConditionEvents()
    {
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
        {
            if (e.satisfaction <= 50)
                TryTriggerConditionEvent(RandomEventType.EmployeeRun);
        }
    }

    void TryTriggerConditionEvent(RandomEventType type)
    {
        var evt = _conditionEventPool.Find(e => e.type == type);
        if (evt == null) return;
        if (UnityEngine.Random.value > evt.triggerChance) return;
        evt.onApply?.Invoke();
    }
    public void ResetInvestment()
    {
        _investmentAccepted = false;
        _investmentStat = "";
        _investmentStatName = "";
        InvestmentProgressUI.Instance?.Hide();
    }

    public void TriggerInvestmentEvent(System.Action onComplete)
    {
        ResetInvestment();

        if (UnityEngine.Random.value > investmentTriggerChance)
        {
            onComplete?.Invoke();
            return;
        }

        string[] stats = { "planning", "develop", "art", "creativity" };
        string[] statNames = { "기획", "개발", "아트", "창의성" };
        int idx = UnityEngine.Random.Range(0, stats.Length);

        _investmentStat = stats[idx];
        _investmentStatName = statNames[idx]; // ← 필드 추가

        ConfirmUI.Instance.Show(
            $"투자자가 찾아왔습니다!\n{statNames[idx]} 수치가 {investmentThreshold}점 이상이면\n{investmentReward:N0}G를 드리겠습니다.\n달성 실패 시 {investmentReward:N0}G를 가져갑니다.\n수락하시겠습니까?",
            onConfirm: () =>
            {
                _investmentAccepted = true;
                InvestmentProgressUI.Instance.Show(_investmentStatName, investmentThreshold);
                Debug.Log($"투자 수락: 목표 스탯 {_investmentStat} / 기준 {investmentThreshold}");
                onComplete?.Invoke();
            },
            onCancel: () =>
            {
                _investmentAccepted = false;
                Debug.Log("투자 거절");
                onComplete?.Invoke();
            },
            confirmText: "수락",
            cancelText: "거절"
        );
    }

    public void CheckInvestmentResult(float planning, float develop, float art, float creativity, System.Action onComplete = null)
    {
        if (!_investmentAccepted || string.IsNullOrEmpty(_investmentStat))
        {
            onComplete?.Invoke();
            return;
        }

        float value = _investmentStat switch
        {
            "planning" => planning,
            "develop" => develop,
            "art" => art,
            "creativity" => creativity,
            _ => 0f
        };

        string statName = _investmentStat switch
        {
            "planning" => "기획",
            "develop" => "개발",
            "art" => "아트",
            "creativity" => "창의성",
            _ => ""
        };

        ResetInvestment();

        if (value >= investmentThreshold)
        {
            MoneyManager.Instance.AddGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 성공!\n{statName} 수치: {value:F0}점\n{investmentReward:N0}G를 받았습니다!",
                () => onComplete?.Invoke()
            );
        }
        else
        {
            MoneyManager.Instance.ForceSpendGold(investmentReward);
            AlertUI.Instance.Show(
                $"투자 실패...\n{statName} 수치: {value:F0}점\n{investmentReward:N0}G를 잃었습니다.",
                () => onComplete?.Invoke()
            );
        }
    }

    // ── stub 메서드 (구현 예정) ───────────────
    public void ApplyNetworkIssue() { }
    public void TriggerScoutEvent() { }
    public void TriggerBetaTestEvent() { }
    public void TriggerAlgorithmEvent() { }
    public void TriggerEmployeeRunEvent() { }
    public void TriggerEmployeeFightEvent() { }
    public void TriggerBadCompanyEvent() { }
}