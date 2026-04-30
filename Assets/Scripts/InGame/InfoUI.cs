using System.Collections;
using TMPro;
using UnityEngine;

// 슬라이드 인 → 잠시 표시 → 슬라이드 아웃 되는 짧은 알림 UI
// - 사용 예: InfoUI.Instance?.Show("판매 완료!");
// - 게임 일시정지 중에도 동작 (Time.unscaledDeltaTime)
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
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

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
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - (1f - k) * (1f - k); // ease-out
            rect.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        rect.anchoredPosition = end;
    }
}
