using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이드 인 → 잠시 표시 → 슬라이드 아웃 되는 짧은 알림 UI
// - 사용 예: InfoUI.Instance?.Show("판매 완료!");
// - 매 Show() 마다 InfoPrefab을 container(InfoPanel)에 Instantiate 해서 쓰고 끝나면 Destroy — InfoFeedUI와 동일한 생성 방식.
//   LayoutElement는 InfoFeedUI 쪽과 마찬가지로 건드리지 않는다(VerticalLayoutGroup이 세로 위치를 그대로 제어) —
//   대신 슬라이드는 루트가 아니라 한 단계 아래 자식 "Content"의 anchoredPosition을 움직여서 낸다.
//   LayoutGroup은 직계 자식만 건드리므로 손자뻘인 Content는 그 영향을 받지 않는다.
//   [[feedback_layoutgroup_child_position_animation]] 참고.
// - GameTimeManager.IsRunning 을 봐서 진행 — 직원리스트 등 모달이 떠서 시간이 멈추면 같이 멈췄다가
//   모달이 닫혀 시간이 재개되면 이어서 진행된다(Time.unscaledDeltaTime 로 애니메이션 자체는 프레임 단위로 부드럽게).
public class InfoUI : MonoBehaviour
{
    public static InfoUI Instance { get; private set; }

    [Header("References")]
    public RectTransform container;
    public GameObject infoPrefab;

    [Header("Slide")]
    public Vector2 shownAnchoredPos;
    public Vector2 hiddenAnchoredPos = new Vector2(700f, 0f);
    public float slideInDuration  = 0.3f;
    public float holdDuration     = 1.5f;
    public float slideOutDuration = 0.3f;

    private Coroutine _co;
    private System.Action _pendingCallback;
    private GameObject _activeInstance;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // GameObject가 (자신 또는 조상이) 비활성화되면 진행 중이던 코루틴은 Unity가 자동으로 멈추지만,
    // 콜백은 안 불러준다 — 여기서 직접 마무리해야 한다. 예: ConfirmPanelMoneyElevator가 EmployeePanel/
    // ConfirmHirePanel이 열릴 때 HUD 자식들을 통째로 SetActive(false)하면서 이 오브젝트도 같이 꺼지는 경우
    // (2026-07-13에 이미 겪은 버그 — 콜백이 안 불려 SalesUI.OnSalesComplete가 영영 안 불리고 패널이 멈춤).
    void OnDisable()
    {
        if (_co == null) return;
        _co = null;
        if (_activeInstance != null) { Destroy(_activeInstance); _activeInstance = null; }
        var cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke();
    }

    public void Show(string text) => Show(text, null);

    public void Show(string text, System.Action onComplete)
    {
        if (container == null || infoPrefab == null) { onComplete?.Invoke(); return; }

        // 이전 사이클이 진행 중이면 그쪽 콜백을 잃지 않도록 먼저 호출 후 새 사이클로 교체
        if (_co != null)
        {
            StopCoroutine(_co);
            if (_activeInstance != null) { Destroy(_activeInstance); _activeInstance = null; }
            var prev = _pendingCallback;
            _pendingCallback = null;
            prev?.Invoke();
        }

        var go = Instantiate(infoPrefab, container);

        var portrait = go.transform.Find("Content/InfoPortrait/InfoPortraitImage");
        if (portrait != null)
        {
            var img = portrait.GetComponent<Image>();
            if (img != null) img.enabled = false;
        }

        var textComp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (textComp != null) textComp.text = text;

        _activeInstance = go;
        _pendingCallback = onComplete;
        var content = go.transform.Find("Content") as RectTransform;
        _co = StartCoroutine(ShowRoutine(content));
    }

    IEnumerator ShowRoutine(RectTransform rect)
    {
        rect.anchoredPosition = hiddenAnchoredPos;

        yield return SlideTo(rect, hiddenAnchoredPos, shownAnchoredPos, slideInDuration);

        float t = 0f;
        float hold = Mathf.Max(0f, holdDuration);
        while (t < hold)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return SlideTo(rect, shownAnchoredPos, hiddenAnchoredPos, slideOutDuration);

        if (_activeInstance != null) { Destroy(_activeInstance); _activeInstance = null; }
        _co = null;
        var cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke();
    }

    IEnumerator SlideTo(RectTransform rect, Vector2 start, Vector2 end, float duration)
    {
        float dur = Mathf.Max(0.0001f, duration);
        float t = 0f;
        while (t < dur)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = 1f - (1f - k) * (1f - k); // ease-out
                rect.anchoredPosition = Vector2.Lerp(start, end, k);
            }
            yield return null;
        }
        rect.anchoredPosition = end;
    }
}
