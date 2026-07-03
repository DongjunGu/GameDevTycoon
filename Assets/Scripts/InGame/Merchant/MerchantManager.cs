using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 인게임 상인 시스템 — 1년에 한 번 5~7월 랜덤 주차에 방문.
// master_desk 로 이동 → 도착 시 (랜덤이벤트/팀장 선택 진행 중이면 대기) 즉시 AlertUI 표시.
// 사용자가 AlertUI 확인 → MerchantShopPanelUI 열림. 닫으면 상인 즉시 퇴장.
//
// 스케줄/방문완료여부는 UserGameTime 에 저장:
//   - merchantSchedule (string "M,W") — 그 해 방문 예정 월/주
//   - merchantVisitedYear (int) — 마지막으로 방문 완료한 연도
// 새해 진입(_scheduledYear != currentYear) 시 reroll 후 즉시 SaveGameTime 호출.
// 구매는 메모리만 변경, 패널 닫힐 때(OnShopClosed) 4-set save 한 번에.
public class MerchantManager : MonoBehaviour
{
    public static MerchantManager Instance { get; private set; }

    [Header("Spawn")]
    [Tooltip("상인 캐릭터 prefab. OfficeCharacter 부착 필수. 일단 portrait_secretary 그대로.")]
    public GameObject merchantPrefab;
    [Tooltip("출입 위치 — OfficeManager.spawnPoint 와 같은 위치(문 앞) 사용 권장.")]
    public Transform spawnPoint;
    [Tooltip("도착 PatrolPoint 의 pointId. 씬에서 PatrolPoint 검색해 transform 으로 사용.")]
    public string destinationPatrolPointId = "master_desk";
    [Tooltip("PatrolPoint 못 찾았을 때 폴백으로 사용할 Transform (선택). 비워두면 patrolPointId 만 사용.")]
    public Transform destinationFallback;

    [Header("UI")]
    public MerchantShopPanelUI shopPanelUI;

    [Header("Schedule")]
    [Tooltip("방문 가능 시작 월 (포함)")]
    public int visitStartMonth = 5;
    [Tooltip("방문 가능 끝 월 (포함)")]
    public int visitEndMonth = 7;

    [Tooltip("AlertUI 안내 문구")]
    public string promptMessage = "아이템 사세요!";

    [Header("Shop Settings")]
    [Tooltip("한 번 방문에 노출할 아이템 수")]
    public int itemsPerVisit = 3;

    // 스케줄 상태 — _scheduledYear 는 메모리(어느 해 스케줄인지 표식), 나머지는 UserGameTime 에 저장.
    int _scheduledYear  = int.MinValue; // 메모리 전용 (저장 X)
    int _scheduledMonth = 0;
    int _scheduledWeek  = 0;
    int _visitedYear    = 0; // 0 = 미방문
    readonly List<string> _scheduledItems = new(); // 새해 추첨된 진열 품목 (방문 시 그대로 사용)

    // GameTimeManager.SaveGameTime 가 직렬화할 때 읽는 getter
    public int    ScheduledMonth   => _scheduledMonth;
    public int    ScheduledWeek    => _scheduledWeek;
    public int    VisitedYear      => _visitedYear;
    public string GetScheduleString() => (_scheduledMonth > 0 && _scheduledWeek > 0)
        ? $"{_scheduledMonth},{_scheduledWeek}" : "";
    public string GetItemsString() => string.Join(",", _scheduledItems);

    OfficeCharacter _activeMerchant;
    Coroutine _arrivalCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // LoadingScene 에 배치되어 GameScene/OutGameScene 사이 영속. 씬별 참조는 OnSceneLoaded 에서 hookup.
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged += OnTimeChanged;
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnTimeChanged -= OnTimeChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // GameScene 진입 시 그 씬의 OfficeManager.spawnPoint / MerchantShopPanelUI 자동 hookup.
    // GameScene 이 아니면 씬과 함께 파괴된 참조를 무효화하고 코루틴/활성 상인 상태 정리.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            if (OfficeManager.Instance != null)
                spawnPoint = OfficeManager.Instance.spawnPoint;
            shopPanelUI = FindAnyObjectByType<MerchantShopPanelUI>(FindObjectsInactive.Include);
            // destinationFallback 은 PatrolPoint 검색이 우선이라 null 로 둠.
            destinationFallback = null;
        }
        else
        {
            // 씬 떠나면 활성 상인/코루틴은 자동 파괴되지만 참조는 dangling 상태이므로 정리.
            _activeMerchant = null;
            _resolvedDestination = null;
            if (_arrivalCo != null) { StopCoroutine(_arrivalCo); _arrivalCo = null; }
            spawnPoint = null;
            shopPanelUI = null;
            destinationFallback = null;
        }
    }

    public void ResetForNewRun()
    {
        _scheduledYear  = int.MinValue;
        _scheduledMonth = 0;
        _scheduledWeek  = 0;
        _visitedYear    = 0;
        _scheduledItems.Clear();
        if (_arrivalCo != null) { StopCoroutine(_arrivalCo); _arrivalCo = null; }
        DestroyMerchant();
    }

    // GameTimeManager.LoadGameTime 에서 호출 — 저장된 "M,W" + 품목 CSV + visitedYear 주입.
    // schedule 이 비어있으면 다음 새해 진입 시 reroll.
    public void LoadSchedule(string schedule, string items, int visitedYear)
    {
        _scheduledMonth = 0;
        _scheduledWeek  = 0;
        if (!string.IsNullOrEmpty(schedule))
        {
            var parts = schedule.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int w))
            {
                _scheduledMonth = m;
                _scheduledWeek  = w;
            }
        }
        _scheduledItems.Clear();
        if (!string.IsNullOrEmpty(items))
        {
            foreach (var id in items.Split(','))
                if (!string.IsNullOrEmpty(id)) _scheduledItems.Add(id);
        }
        _visitedYear = visitedYear;
        // 로드된 스케줄은 현재 연도용으로 간주 — reroll 방지
        if (_scheduledMonth > 0 && GameTimeManager.Instance != null)
            _scheduledYear = GameTimeManager.Instance.Year;
        Debug.Log($"[Merchant] LoadSchedule: schedule='{schedule}' items='{items}' visitedYear={_visitedYear}");
    }

    // 디버그 강제 트리거 — 스케줄/방문 플래그 무관하게 즉시 방문 시작.
    public void TestVisit()
    {
        if (_arrivalCo != null) { StopCoroutine(_arrivalCo); _arrivalCo = null; }
        TriggerVisit();
    }

    void OnTimeChanged()
    {
        if (GameTimeManager.Instance == null) return;
        int y = GameTimeManager.Instance.Year;
        int m = GameTimeManager.Instance.Month;
        int w = GameTimeManager.Instance.Week;

        // 새 연도 진입 → 스케줄 + 품목 reroll → 즉시 SaveGameTime 으로 백엔드 반영
        if (y != _scheduledYear)
        {
            _scheduledYear  = y;
            _scheduledMonth = Random.Range(visitStartMonth, visitEndMonth + 1);
            _scheduledWeek  = Random.Range(1, 5); // 1~4주
            RollItems();
            Debug.Log($"[Merchant] {y}년 방문 예정: {_scheduledMonth}월 {_scheduledWeek}주, 품목=[{string.Join(",", _scheduledItems)}]");
            GameTimeManager.Instance.SaveGameTime();
        }

        if (_visitedYear == y) return; // 이미 이 해에 방문함
        if (m == _scheduledMonth && w == _scheduledWeek)
            TriggerVisit();
    }

    // 차트에서 현재 stage 매칭 풀 추려 itemsPerVisit 개 픽. _scheduledItems 에 저장.
    // StageManager 가 없거나 매칭 결과가 비면 전체 풀 폴백.
    void RollItems()
    {
        _scheduledItems.Clear();
        var cache = ItemChartLoader.Cache;
        if (cache == null || cache.Count == 0) return;

        var ids = new List<string>();
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 0;
        if (stage > 0)
        {
            string stageStr = stage.ToString();
            foreach (var kv in cache)
            {
                if (kv.Value.category == "강화") continue; // 강화권/초심 회복기 등 강화 계열은 상인 판매 제외
                var stages = kv.Value.appearStages;
                if (string.IsNullOrEmpty(stages)) continue;
                foreach (var s in stages.Split(','))
                    if (s.Trim() == stageStr) { ids.Add(kv.Key); break; }
            }
        }
        if (ids.Count == 0)
        {
            Debug.LogWarning($"[Merchant] stage={stage} 매칭 아이템 없음 — 전체 풀 폴백");
            foreach (var kv in cache)
                if (kv.Value.category != "강화") ids.Add(kv.Key);
        }
        Shuffle(ids);
        int n = Mathf.Min(itemsPerVisit, ids.Count);
        for (int i = 0; i < n; i++) _scheduledItems.Add(ids[i]);
    }

    Transform _resolvedDestination;

    Transform ResolveDestination()
    {
        if (!string.IsNullOrEmpty(destinationPatrolPointId))
        {
            var pts = FindObjectsByType<PatrolPoint>(FindObjectsSortMode.None);
            foreach (var p in pts) if (p != null && p.pointId == destinationPatrolPointId) return p.transform;
            Debug.LogWarning($"[Merchant] PatrolPoint pointId='{destinationPatrolPointId}' 없음 → fallback 사용");
        }
        return destinationFallback;
    }

    void TriggerVisit()
    {
        if (merchantPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[Merchant] prefab/spawn 인스펙터 미연결 — 방문 스킵");
            return;
        }
        _resolvedDestination = ResolveDestination();
        if (_resolvedDestination == null)
        {
            Debug.LogWarning("[Merchant] 도착 지점 못 찾음 — 방문 스킵");
            return;
        }
        DestroyMerchant();

        var obj = Instantiate(merchantPrefab, spawnPoint.position, Quaternion.identity);
        _activeMerchant = obj.GetComponent<OfficeCharacter>();
        if (_activeMerchant == null)
        {
            Debug.LogError("[Merchant] merchantPrefab 에 OfficeCharacter 컴포넌트 없음");
            Destroy(obj);
            return;
        }

        // OfficeCharacter 의 ForcePatrolTo 패턴 재사용 — PatrolPoint(또는 폴백) transform 으로 이동.
        _activeMerchant.ForcePatrolTo(_resolvedDestination, stayDuration: 9999f);
        _arrivalCo = StartCoroutine(WaitArrivalThenPrompt());
    }

    IEnumerator WaitArrivalThenPrompt()
    {
        // 도착 판정 — XY 거리만 사용 (z 차이 무시). 캐릭터 z 가 sorting 용으로 0/−1 등 다를 수 있어 Vector3.Distance 면 임계 못 맞춤.
        float timeout = 20f;
        float elapsed = 0f;
        float arrivalThreshold = 0.6f;
        while (_activeMerchant != null && _resolvedDestination != null && elapsed < timeout)
        {
            Vector2 a = _activeMerchant.transform.position;
            Vector2 b = _resolvedDestination.position;
            float d = Vector2.Distance(a, b);
            if (d < arrivalThreshold) { Debug.Log($"[Merchant] 도착 판정 통과 (dist={d:F2})"); break; }
            // timeout 은 "시간이 흐르는 동안"만 누적 — 정지 중엔 상인 patrol 도 멈추므로,
            // unscaled 로 재면 정지된 동안 timeout 이 소진돼 상인이 이동 중인데 강제 진행되는 버그가 생긴다.
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                elapsed += Time.deltaTime;
            yield return null;
        }
        if (elapsed >= timeout)
            Debug.LogWarning("[Merchant] 도착 timeout — 강제 진행");

        // 도착했어도 (1) 다른 차단 모달이 떠 있거나(ModalGate) (2) 다른 패널로 시간이 멈춰 있으면(IsRunning=false)
        // 모두 닫히고 시간이 재개될 때까지 대기 후 표시 → 열린 패널 위에 상인 팝업이 겹쳐 뜨지 않도록.
        // (이동을 막는 게 아니라 도착 후 팝업만 미루는 방식 — 상인은 책상 앞에서 대기하다가 시간 재개 시 팝업)
        var gate = ModalGate.I;
        if (gate.IsBlocked)
        {
            string names = string.Join(", ", gate.GetActiveNames());
            Debug.Log($"[Merchant] ModalGate 대기 — 활성 모달: {names}");
        }
        yield return new WaitUntil(() =>
            !ModalGate.I.IsBlocked &&
            (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning));
        ShowPrompt();
    }

    void ShowPrompt()
    {
        GameTimeManager.Instance?.StopTime();
        if (AlertUI.Instance != null)
        {
            Debug.Log("[Merchant] AlertUI 표시");
            AlertUI.Instance.Show(promptMessage, OnPromptClicked);
        }
        else
        {
            Debug.LogWarning("[Merchant] AlertUI.Instance null — 바로 ShopPanel 열기");
            OpenShop();
        }
    }

    // 차단 UI 판정은 ModalGate 가 담당. 각 모달이 ModalGateRegistrant 컴포넌트로 자동 등록.

    void OnPromptClicked()
    {
        OpenShop();
    }

    void OpenShop()
    {
        if (shopPanelUI == null)
        {
            Debug.LogWarning("[Merchant] shopPanelUI 미연결");
            OnShopClosed();
            return;
        }

        // 새해에 추첨돼 _scheduledItems 에 저장된 품목을 그대로 진열 (방문마다 셔플 X).
        // 비어있으면 (로드된 세이브에 품목 없거나 TestVisit 직후) 즉석 reroll 폴백.
        if (_scheduledItems.Count == 0)
        {
            Debug.LogWarning("[Merchant] _scheduledItems 비어있음 — 즉석 reroll");
            RollItems();
        }
        shopPanelUI.Open(new List<string>(_scheduledItems), OnShopClosed);
    }

    void OnShopClosed()
    {
        // 방문 완료 마킹 → 그 해 중복 방문 방지
        if (GameTimeManager.Instance != null)
            _visitedYear = GameTimeManager.Instance.Year;

        // 상인 즉시 퇴장 → 출입구로 다시 patrol 후 destroy
        if (_activeMerchant != null && spawnPoint != null)
        {
            var mc = _activeMerchant;
            mc.ForcePatrolTo(spawnPoint, stayDuration: 0.1f);
            StartCoroutine(DestroyAfterReachExit(mc));
        }
        _activeMerchant = null;
        GameTimeManager.Instance?.StartTime();

        // Batched 4-set save — OnClickBuy 가 NoSave 로 누적해둔 인벤토리/골드 변경,
        // 방금 셋업한 visitedYear, 그리고 프로젝트/시간 상태를 한 번에 백엔드 반영.
        ItemManager.Instance?.Save();
        MoneyManager.Instance?.SaveMoney();
        GameTimeManager.Instance?.SaveGameTime();
        ProjectSaveManager.Instance?.SaveProject();
    }

    IEnumerator DestroyAfterReachExit(OfficeCharacter mc)
    {
        float timeout = 15f;
        float elapsed = 0f;
        while (mc != null && elapsed < timeout)
        {
            // XY 거리만 사용 — 도착 코루틴과 동일(캐릭터 z 는 sorting 용으로 달라 Vector3 면 임계 못 맞춤).
            Vector2 a = mc.transform.position;
            Vector2 b = spawnPoint.position;
            if (Vector2.Distance(a, b) < 0.6f) break;
            // timeout 은 "시간이 흐르는 동안"만 누적 — 정지 중엔 상인 patrol 도 멈추므로,
            // unscaled 로 재면 정지된 동안 timeout 이 소진돼 상인이 이동 중인데 도중에 파괴되는 버그가 생긴다.
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                elapsed += Time.deltaTime;
            yield return null;
        }
        if (mc != null) Destroy(mc.gameObject);
    }

    void DestroyMerchant()
    {
        if (_activeMerchant != null) Destroy(_activeMerchant.gameObject);
        _activeMerchant = null;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
