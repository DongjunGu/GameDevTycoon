using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private bool _triggered50 = false;
    private List<RandomEventData> _eventPool = new();

    [Header("Settings")]
    [Range(0f, 1f)]

    public float eventTriggerChance = 0.5f;
    [Header("각 이벤트 발동확률")]
    [Range(0f, 1f)]
    public float eventDetailTriggerChance = 0.5f;
    public void SetTriggered50(bool value) => _triggered50 = value;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void InitEvents()
    {
        _triggered50 = false;
        _eventPool.Clear();

        _eventPool.Add(new RandomEventData
        {
            type          = RandomEventType.Blackout,
            title         = "정전 발생!",
            description   = "갑작스러운 정전으로 작업 데이터가 손실되었습니다.\n기획, 개발, 아트 수치가 절반으로 감소합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply       = () =>
            {
                var dp = DevelopmentPanelUI.Instance;
                dp.MultiplyValues(0.5f);
            }
        });

        _eventPool.Add(new RandomEventData
        {
            type          = RandomEventType.TeamDinner,
            title         = "회식 진행!",
            description   = "팀원들의 사기가 올랐습니다!\n기획, 개발, 아트 수치가 2배로 증가합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply       = () =>
            {
                var dp = DevelopmentPanelUI.Instance;
                dp.MultiplyValues(2.0f);
            }
        });
    }

    public void Reset() => _triggered50 = false;

    // DevelopmentCoroutine에서 호출
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
        // 이벤트 발동 여부 먼저 결정 (전체 발동 확률 50%)
        if (UnityEngine.Random.value > eventTriggerChance)
        {
            Debug.Log("이벤트 미발동");
            return;
        }

        // 풀에서 랜덤 선택
        if (_eventPool.Count == 0) return;
        var evt = _eventPool[UnityEngine.Random.Range(0, _eventPool.Count)];

        // 개별 발동 확률 체크
        if (UnityEngine.Random.value > evt.triggerChance)
        {
            Debug.Log($"이벤트 확률 미달: {evt.type}");
            return;
        }

        Debug.Log($"이벤트 발동: {evt.type}");
        DevelopmentManager.Instance.PauseForEvent();
        RandomEventUI.Instance.Show(evt);
    }
}