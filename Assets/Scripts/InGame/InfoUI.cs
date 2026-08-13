using System.Collections;
using TMPro;
using UnityEngine;

// 슬라이드 인 → 잠시 표시 → 슬라이드 아웃 되는 짧은 알림 UI
// - 사용 예: InfoUI.Instance?.Show("판매 완료!");
// - GameTimeManager.IsRunning 을 봐서 진행 — 직원리스트 등 모달이 떠서 시간이 멈추면 같이 멈췄다가
//   모달이 닫혀 시간이 재개되면 이어서 진행된다(Time.unscaledDeltaTime 로 애니메이션 자체는 프레임 단위로 부드럽게).
public class InfoUI : MonoBehaviour
{
    public static InfoUI Instance { get; private set; }

    [Header("References")]
    public GameObject infoPanel;
    [Tooltip("실제 슬라이드될 RectTransform (비우면 infoPanel의 RectTransform 사용)")]
    public RectTransform slidePanel;
    public TextMeshProUGUI infoText;

    [Header("Slide")]
    public Vector2 shownAnchoredPos;
    public Vector2 hiddenAnchoredPos;
    public float slideInDuration  = 0.3f;
    public float holdDuration     = 1.5f;
    public float slideOutDuration = 0.3f;

    private Coroutine _co;
    private System.Action _pendingCallback;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        var rect = GetSlideRect();
        if (rect != null) rect.anchoredPosition = hiddenAnchoredPos;
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // GameObject가 (자신 또는 조상이) 비활성화되면 진행 중이던 코루틴은 Unity가 자동으로 멈추지만,
    // 콜백은 안 불러준다 — 여기서 직접 마무리해야 한다. 예: ConfirmPanelMoneyElevator가 EmployeePanel/
    // ConfirmHirePanel이 열릴 때 HUD 자식들을 통째로 SetActive(false)하면서 이 오브젝트도 같이 꺼지는 경우
    // (2026-07-13에 이미 겪은 버그 — 콜백이 안 불려 SalesUI.OnSalesComplete가 영영 안 불리고 패널이 멈춤).
    void OnDisable()
    {
        if (_co == null) return;
        _co = null;
        if (infoPanel != null) infoPanel.SetActive(false);
        var cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke();
    }

    RectTransform GetSlideRect()
    {
        if (slidePanel != null) return slidePanel;
        if (infoPanel != null)  return infoPanel.transform as RectTransform;
        return null;
    }

    public void Show(string text) => Show(text, null);

    public void Show(string text, System.Action onComplete)
    {
        var rect = GetSlideRect();
        if (rect == null) { onComplete?.Invoke(); return; }
        if (infoText != null) infoText.text = text;

        // 이전 사이클이 진행 중이면 그쪽 콜백을 잃지 않도록 먼저 호출 후 새 사이클로 교체
        if (_co != null)
        {
            StopCoroutine(_co);
            var prev = _pendingCallback;
            _pendingCallback = null;
            prev?.Invoke();
        }
        _pendingCallback = onComplete;
        _co = StartCoroutine(ShowRoutine(rect));
    }

    IEnumerator ShowRoutine(RectTransform rect)
    {
        if (infoPanel != null) infoPanel.SetActive(true);
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

        if (infoPanel != null) infoPanel.SetActive(false);
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
