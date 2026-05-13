using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 인게임 상인 시스템 — 1년에 한 번 5~7월 랜덤 주차에 방문.
// master_desk 로 이동 → 도착 시 (랜덤이벤트/팀장 선택 진행 중이면 대기) 1.5초 후 MerchantPromptUI 표시.
// 사용자가 prompt 클릭 → MerchantShopPanelUI 열림. 닫으면 상인 즉시 퇴장.
//
// 새 런 시작 시 ResetForNewRun 으로 _scheduledYear/lastVisitYear 모두 초기화.
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
    public MerchantPromptUI promptUI;
    public MerchantShopPanelUI shopPanelUI;

    [Header("Schedule")]
    [Tooltip("방문 가능 시작 월 (포함)")]
    public int visitStartMonth = 5;
    [Tooltip("방문 가능 끝 월 (포함)")]
    public int visitEndMonth = 7;
    [Tooltip("랜덤이벤트/팀장 패널 종료 후 대기 시간(초)")]
    public float waitAfterClearSec = 1.5f;

    [Header("Shop Settings")]
    [Tooltip("한 번 방문에 노출할 아이템 수")]
    public int itemsPerVisit = 3;

    // 스케줄 상태
    int _scheduledYear  = int.MinValue;
    int _scheduledMonth = -1;
    int _scheduledWeek  = -1;
    int _lastVisitYear  = int.MinValue;

    OfficeCharacter _activeMerchant;
    Coroutine _arrivalCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
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
    }

    public void ResetForNewRun()
    {
        _scheduledYear  = int.MinValue;
        _scheduledMonth = -1;
        _scheduledWeek  = -1;
        _lastVisitYear  = int.MinValue;
        if (_arrivalCo != null) { StopCoroutine(_arrivalCo); _arrivalCo = null; }
        DestroyMerchant();
    }

    // 디버그 강제 트리거 — 스케줄/방문 플래그 무관하게 즉시 방문 시작.
    public void TestVisit()
    {
        if (_arrivalCo != null) { StopCoroutine(_arrivalCo); _arrivalCo = null; }
        _lastVisitYear = GameTimeManager.Instance != null ? GameTimeManager.Instance.Year : 0;
        TriggerVisit();
    }

    void OnTimeChanged()
    {
        if (GameTimeManager.Instance == null) return;
        int y = GameTimeManager.Instance.Year;
        int m = GameTimeManager.Instance.Month;
        int w = GameTimeManager.Instance.Week;

        // 첫 호출 또는 새 연도 진입 → 스케줄
        if (y != _scheduledYear)
        {
            _scheduledYear  = y;
            _scheduledMonth = Random.Range(visitStartMonth, visitEndMonth + 1);
            _scheduledWeek  = Random.Range(1, 5); // 1~4주
            Debug.Log($"[Merchant] {y}년 방문 예정: {_scheduledMonth}월 {_scheduledWeek}주");
        }

        if (_lastVisitYear == y) return; // 이미 이 해에 방문함
        if (m == _scheduledMonth && w == _scheduledWeek)
        {
            _lastVisitYear = y;
            TriggerVisit();
        }
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
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (elapsed >= timeout)
            Debug.LogWarning("[Merchant] 도착 timeout — 강제 진행");

        // ModalGate 큐잉 — 차단 UI 가 모두 닫히면 콜백 실행. 차단 없으면 즉시.
        var gate = ModalGate.I;
        if (gate.IsBlocked)
        {
            string names = string.Join(", ", gate.GetActiveNames());
            Debug.Log($"[Merchant] ModalGate 대기 — 활성 모달: {names}");
        }
        gate.WhenFree(() => StartCoroutine(ShowPromptAfterDelay()));
    }

    IEnumerator ShowPromptAfterDelay()
    {
        Debug.Log($"[Merchant] prompt 대기 시작 ({waitAfterClearSec}s)");
        yield return new WaitForSecondsRealtime(waitAfterClearSec);
        GameTimeManager.Instance?.StopTime();
        if (promptUI != null)
        {
            Debug.Log("[Merchant] prompt 표시");
            promptUI.Show(OnPromptClicked);
        }
        else
        {
            Debug.LogWarning("[Merchant] promptUI null — 바로 ShopPanel 열기");
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

        // 차트에서 랜덤 N개
        var cache = ItemChartLoader.Cache;
        var ids = new List<string>(cache.Keys);
        Shuffle(ids);
        int n = Mathf.Min(itemsPerVisit, ids.Count);
        var picked = ids.GetRange(0, n);

        shopPanelUI.Open(picked, OnShopClosed);
    }

    void OnShopClosed()
    {
        // 상인 즉시 퇴장 → 출입구로 다시 patrol 후 destroy
        if (_activeMerchant != null && spawnPoint != null)
        {
            var mc = _activeMerchant;
            mc.ForcePatrolTo(spawnPoint, stayDuration: 0.1f);
            StartCoroutine(DestroyAfterReachExit(mc));
        }
        _activeMerchant = null;
        GameTimeManager.Instance?.StartTime();
    }

    IEnumerator DestroyAfterReachExit(OfficeCharacter mc)
    {
        float timeout = 15f;
        float elapsed = 0f;
        while (mc != null && elapsed < timeout)
        {
            float d = Vector3.Distance(mc.transform.position, spawnPoint.position);
            if (d < 0.2f) break;
            elapsed += Time.unscaledDeltaTime;
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
