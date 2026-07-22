using UnityEngine;

// Screen.safeArea 기반 세이프존 적용 + 기종별 보정.
// 안드로이드/iOS가 safeArea를 서로 다르게 보고하는 경우가 있어(특히 iOS 가로모드에서 실제보다 위쪽으로
// 치우쳐 보고되는 경우), 플랫폼별로 상하좌우에 추가 여백(px, 기준해상도 아니라 실제 Screen 픽셀 단위)을
// 더 깎아낼 수 있게 노출. 값은 인스펙터에서 기종 테스트하며 조절.
public class SafeAreaPanel : MonoBehaviour
{
    [Header("안드로이드 추가 여백(px) — safeArea에서 추가로 더 깎아낼 만큼")]
    public float androidExtraTop;
    public float androidExtraBottom;
    public float androidExtraLeft;
    public float androidExtraRight;

    [Header("iOS 추가 여백(px) — safeArea에서 추가로 더 깎아낼 만큼")]
    public float iosExtraTop;
    public float iosExtraBottom;
    public float iosExtraLeft;
    public float iosExtraRight;

#if UNITY_EDITOR
    public enum EditorPreviewPlatform { 실제빌드타겟그대로, Android, iOS }

    [Header("에디터 전용 미리보기")]
    [Tooltip("빌드 없이 Play 모드에서 안드로이드/iOS 여백값을 강제로 골라 미리보기. " +
             "#if UNITY_ANDROID/#if UNITY_IOS 는 '현재 빌드 타겟'에 매여있어서, 디바이스 시뮬레이터로 " +
             "아이폰 화면만 흉내내도 빌드 타겟이 Android면 iOS쪽 여백이 절대 적용 안 됨 — 이 필드로 강제 지정.")]
    public EditorPreviewPlatform editorPreview = EditorPreviewPlatform.실제빌드타겟그대로;
#endif

    RectTransform _rectTransform;
    Rect _lastSafeArea;
    ScreenOrientation _lastOrientation;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

#if UNITY_EDITOR
    bool _pendingReapply;

    // 인스펙터에서 여백값/editorPreview 드롭다운을 바꾸면 다음 Update() 에서 재적용되도록 예약만 한다.
    // ⚠️ OnValidate() 안에서 바로 RectTransform.anchorMin/Max 를 바꾸면 자식들의
    // OnRectTransformDimensionsChange 가 연쇄 호출되며 "SendMessage cannot be called during
    // Awake/CheckConsistency/OnValidate" 경고가 쏟아지고, 그 과정에서 갱신이 깨져 UI가 사라질 수 있음.
    void OnValidate()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        _pendingReapply = true;
    }
#endif

    // iOS는 특히 런치 직후/회전 직후 몇 프레임 동안 Screen.safeArea가 실제 값과 다르게(주로 이전
    // 오리엔테이션 기준 값을) 반환하다가 뒤늦게 갱신되는 경우가 있어, 값이 실제로 바뀔 때만 재적용한다.
    void Update()
    {
#if UNITY_EDITOR
        if (_pendingReapply)
        {
            _pendingReapply = false;
            ApplySafeArea();
            return;
        }
#endif
        if (Screen.safeArea != _lastSafeArea || Screen.orientation != _lastOrientation)
            ApplySafeArea();
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastOrientation = Screen.orientation;

        float extraTop = 0f, extraBottom = 0f, extraLeft = 0f, extraRight = 0f;

#if UNITY_EDITOR
        // 에디터에서는 실제 빌드 타겟(#if UNITY_ANDROID/UNITY_IOS)이 아니라 editorPreview 드롭다운으로
        // 강제 지정 — 디바이스 시뮬레이터로 아이폰 화면을 켜도 빌드 타겟이 그대로면 UNITY_IOS 는 안 켜지므로.
        switch (editorPreview)
        {
            case EditorPreviewPlatform.Android:
                extraTop = androidExtraTop; extraBottom = androidExtraBottom;
                extraLeft = androidExtraLeft; extraRight = androidExtraRight;
                break;
            case EditorPreviewPlatform.iOS:
                extraTop = iosExtraTop; extraBottom = iosExtraBottom;
                extraLeft = iosExtraLeft; extraRight = iosExtraRight;
                break;
            default:
#if UNITY_ANDROID
                extraTop = androidExtraTop; extraBottom = androidExtraBottom;
                extraLeft = androidExtraLeft; extraRight = androidExtraRight;
#elif UNITY_IOS
                extraTop = iosExtraTop; extraBottom = iosExtraBottom;
                extraLeft = iosExtraLeft; extraRight = iosExtraRight;
#endif
                break;
        }
#else
        #if UNITY_ANDROID
        extraTop = androidExtraTop; extraBottom = androidExtraBottom;
        extraLeft = androidExtraLeft; extraRight = androidExtraRight;
        #elif UNITY_IOS
        extraTop = iosExtraTop; extraBottom = iosExtraBottom;
        extraLeft = iosExtraLeft; extraRight = iosExtraRight;
        #endif
#endif

        Vector2 min = new Vector2(safeArea.xMin + extraLeft, safeArea.yMin + extraBottom);
        Vector2 max = new Vector2(safeArea.xMax - extraRight, safeArea.yMax - extraTop);

        // 안전장치 — 여백값을 너무 크게 넣어 min이 max를 넘어서면(가로/세로 폭이 음수) 패널이 통째로
        // 사라져 보이니, 최소 1px 폭은 남도록 클램프.
        min.x = Mathf.Min(min.x, max.x - 1f);
        min.y = Mathf.Min(min.y, max.y - 1f);

        Vector2 anchorMin = new Vector2(Mathf.Clamp01(min.x / Screen.width), Mathf.Clamp01(min.y / Screen.height));
        Vector2 anchorMax = new Vector2(Mathf.Clamp01(max.x / Screen.width), Mathf.Clamp01(max.y / Screen.height));

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
    }
}