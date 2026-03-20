using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private bool _triggered50 = false;
    private List<RandomEventData> _eventPool = new();
    private List<RandomEventData> _releaseEventPool = new();

    [Header("개발 이벤트")]
    [Range(0f, 1f)]
    public float eventTriggerChance = 0.5f;
    [Header("출시 이벤트")]
    [Range(0f, 1f)]
    public float releaseEventTriggerChance = 0.5f;
    [Header("개발중 이벤트 개별 확률")]
    [Range(0f, 1f)]
    public float blackoutChance = 0.5f; // 정전 확률
    [Range(0f, 1f)]
    public float teamDinnerChance = 0.5f; // 회식 확률

    [Range(0f, 1f)]
    public float eventDetailTriggerChance = 0.5f; //개발이벤트 자식 확률

    [Header("출시 이벤트 개별 확률")]
    [Range(0f, 1f)]
    public float competitorChance = 0.5f; // 경쟁사 출시 확률
    [Range(0f, 1f)]
    public float perfectTimingChance = 0.5f; // 완벽한 타이밍 확률

    public void SetTriggered50(bool value) => _triggered50 = value;
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

        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.Blackout,
            title = "정전 발생!",
            description = "갑작스러운 정전으로\n작업 데이터가 손실되었습니다.\n기획, 개발, 아트 수치가 절반으로 감소합니다.",
            triggerChance = blackoutChance, // ← 인스펙터 값 사용
            onApply = () => DevelopmentPanelUI.Instance.MultiplyValues(0.5f)
        });

        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.TeamDinner,
            title = "회식 진행!",
            description = "팀원들의 사기가\n올랐습니다!\n기획, 개발, 아트 수치가 2배로 증가합니다.",
            triggerChance = teamDinnerChance, // ← 인스펙터 값 사용
            onApply = () => DevelopmentPanelUI.Instance.MultiplyValues(2.0f)
        });
        // 개발 수치 +50
        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.DevBoost,
            title = "개발 돌파구 발견!",
            description = "팀이 기술적 난관을\n극복했습니다!\n개발 수치가 50 증가합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 50f, 0f, 0f, 0f)
        });

        // 기획 수치 +50
        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.PlanBoost,
            title = "기획 아이디어 폭발!",
            description = "기획팀에서 혁신적인\n아이디어가 나왔습니다!\n기획 수치가 50 증가합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(50f, 0f, 0f, 0f, 0f)
        });

        // 아트 수치 +50
        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.ArtBoost,
            title = "아트 영감 폭발!",
            description = "아트팀이 놀라운 영감을\n받았습니다!\n아트 수치가 50 증가합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 0f, 50f, 0f, 0f)
        });
        _eventPool.Add(new RandomEventData
        {
            type = RandomEventType.CreativityBoost,
            title = "창의적 영감!",
            description = "팀에 창의적인\n아이디어가 넘칩니다!\n창의성이 30 증가합니다.",
            triggerChance = eventDetailTriggerChance,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, 0f, 30f)
        });



        // 출시 이벤트
        _releaseEventPool.Add(new RandomEventData
        {
            type = RandomEventType.CompetitorRelease,
            title = "경쟁사 출시!",
            description = "경쟁사에서 비슷한 게임을 출시...\n점수에 불이익이 생겼습니다.",
            triggerChance = competitorChance,
            scoreBonus = -3f, // ← 여기서 바로 설정
        });

        _releaseEventPool.Add(new RandomEventData
        {
            type = RandomEventType.PerfectTiming,
            title = "완벽한 타이밍!",
            description = "아주 좋은 타이밍에 출시했어요!\n점수에 보너스가 생겼습니다.",
            triggerChance = perfectTimingChance,
            scoreBonus = 3f,
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
    public void TryTriggerReleaseEvent(System.Action<float> onComplete)
    {
        if (UnityEngine.Random.value > releaseEventTriggerChance)
        {
            onComplete?.Invoke(0f);
            return;
        }

        var evt = _releaseEventPool[UnityEngine.Random.Range(0, _releaseEventPool.Count)];

        // 개별 확률 체크
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
}