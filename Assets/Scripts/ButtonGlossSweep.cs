using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 재사용 가능한 버튼 글로스 스윕 — 아무 버튼에나 붙이면 그 버튼 크기/스프라이트와 무관하게
// 대각선 하이라이트 띠가 한 번씩 훑고 지나가는 연출을 반복한다. 기본은 왼쪽→오른쪽, reverseDirection 체크 시 반대.
// 버튼 자체 이미지는 건드리지 않고, 자식(RectMask2D + 그라디언트 띠)을 스스로 만들어 붙인다.
// [ExecuteAlways] — 플레이하지 않아도 에디터에서 동일한 타이밍(대기→스윕→숨김)으로 실제 좌→우 애니메이션이 재생된다.
// (플레이 모드는 DOTween, 에디터 모드는 EditorApplication.update 기반 — DOTween이 에디터에선 안 돌기 때문에 분리)
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ButtonGlossSweep : MonoBehaviour
{
    [Tooltip("한 번 훑는 데 걸리는 시간(초)")]
    public float sweepDuration = 0.6f;
    [Tooltip("스윕과 스윕 사이 대기 시간(초)")]
    public float idleInterval = 2.5f;
    [Tooltip("띠의 폭(버튼 너비 대비 비율)")]
    [Range(0.05f, 1f)] public float bandWidthRatio = 0.25f;
    [Tooltip("띠의 기울기(도)")]
    public float tiltAngle = 20f;
    [Range(0f, 1f)] public float bandAlpha = 0.35f;
    [Tooltip("체크하면 오른쪽에서 왼쪽으로 스윕 (기본은 왼쪽→오른쪽)")]
    public bool reverseDirection = false;

    RectTransform _rt;
    RectTransform _bandRT;
    Image _bandImg;
    Sequence _seq;
    float _startX, _endX;

    static Sprite _gradientSprite;

    void Awake()
    {
        _rt = transform as RectTransform;
        // 이 RectTransform 크기가 부모 LayoutGroup(예: VerticalLayoutGroup)으로 정해지는 경우, 레이아웃
        // 패스가 아직 한 번도 안 돌았으면 rect.width/height가 0으로 읽혀 밴드가 찌그러진(0 크기) 상태로
        // 생성되는 문제가 있었다 — 밴드를 만들기 전에 부모 레이아웃을 강제로 한 번 갱신해 정확한 크기를 보장.
        var parentRT = _rt.parent as RectTransform;
        if (parentRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);
        BuildMaskAndBand();
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            PlayLoop();
        }
        else
        {
            HideBand(); // interval/스윕 중이 아닐 땐 항상 숨김 상태(_startX)에서 시작
#if UNITY_EDITOR
            _editorElapsed = 0.0;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
        }
    }

    void OnDisable()
    {
        _seq?.Kill();
        // ⚠ DOTween.Kill()은 현재 위치에서 그냥 멈출 뿐 원위치로 안 돌려놓는다 — 스윕 도중(밴드가 보이는 상태)에
        // 컴포넌트가 꺼지면(예: HiringUI 티어 선택 해제로 enabled=false) 얼어붙은 채 계속 보이는 버그가 있었다.
        // 꺼질 때 항상 숨김 위치로 강제 리셋.
        HideBand();
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    void HideBand()
    {
        if (_bandRT != null) _bandRT.anchoredPosition = new Vector2(RestX, 0f);
    }

    // reverseDirection 에 따라 "쉬는 위치"와 "스윕 도착 위치"가 서로 바뀐다.
    float RestX        => reverseDirection ? _endX : _startX;
    float SweepTargetX => reverseDirection ? _startX : _endX;

    // 밴드(GlossSweepBand) 생성 — 도메인 리로드/재실행으로 Awake가 다시 불려도 이미 있으면 재사용(중복 생성 방지).
    void BuildMaskAndBand()
    {
        // 버튼 자체를 마스크로 사용 — 버튼 자체 그래픽엔 영향 없음(이미 자기 rect 안에 있으므로), 띠만 잘림.
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        if (_bandRT == null)
        {
            var existing = transform.Find("GlossSweepBand");
            GameObject bandGo;
            if (existing != null)
            {
                bandGo = existing.gameObject;
            }
            else
            {
                bandGo = new GameObject("GlossSweepBand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                bandGo.transform.SetParent(transform, false);
            }
            bandGo.transform.SetAsLastSibling(); // 버튼 배경/텍스트 위에 그려지도록

            var le = bandGo.GetComponent<LayoutElement>();
            if (le == null) le = bandGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // 버튼 자신이 LayoutGroup을 가진 경우 그 자식 배치에 안 끼어들게

            _bandRT = bandGo.GetComponent<RectTransform>();
            _bandImg = bandGo.GetComponent<Image>();
            _bandImg.raycastTarget = false; // 버튼 클릭을 가로채지 않게
            _bandImg.sprite = GetGradientSprite();

            _bandRT.anchorMin = new Vector2(0.5f, 0.5f);
            _bandRT.anchorMax = new Vector2(0.5f, 0.5f);
            _bandRT.pivot     = new Vector2(0.5f, 0.5f);
        }

        ApplyBandLayout();
        HideBand();
    }

    // 폭/기울기/알파/좌우 끝 좌표(_startX·_endX)를 인스펙터 값으로 재계산. 위치(anchoredPosition)는 건드리지 않는다.
    void ApplyBandLayout()
    {
        if (_bandRT == null || _bandImg == null || _rt == null) return;

        float w = _rt.rect.width;
        float h = _rt.rect.height;
        // 레이아웃이 아직 안 돈 프레임에 호출되면 0 크기가 들어와 밴드가 찌그러진 채 굳어버릴 수 있다 —
        // 그런 값은 무시하고 이전(또는 기본) 상태를 유지, 다음 유효한 호출에서 정상 크기로 갱신되게 한다.
        if (w <= 0f || h <= 0f) return;
        float bandWidth = Mathf.Max(4f, w * bandWidthRatio);
        float diag = Mathf.Sqrt(w * w + h * h); // 기울여도 버튼 세로를 확실히 덮도록 대각선 길이 기준

        float bandHeight = diag * 1.2f;
        _bandRT.sizeDelta = new Vector2(bandWidth, bandHeight);
        _bandRT.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);
        var c = Color.white; c.a = bandAlpha; _bandImg.color = c;

        // 기울어진 사각형의 실제 가로 폭(축 정렬 바운딩박스)은 tiltAngle이 커질수록 bandWidth보다 훨씬 넓어진다
        // (세로가 긴 띠라 특히 심함). bandWidth만으로 숨김 오프셋을 잡으면 큰 각도(예: -60도)에서 버튼 밖으로
        // 다 못 밀어내 처음부터 화면에 걸쳐 보이는 문제가 있었다 — 회전된 바운딩박스 절반 폭으로 계산.
        float rad = tiltAngle * Mathf.Deg2Rad;
        float rotatedHalfWidth = Mathf.Abs(bandWidth / 2f * Mathf.Cos(rad)) + Mathf.Abs(bandHeight / 2f * Mathf.Sin(rad));

        _startX = -(w / 2f + rotatedHalfWidth); // 왼쪽 바깥(숨김)
        _endX   =  (w / 2f + rotatedHalfWidth); // 오른쪽 바깥(숨김)
    }

    void PlayLoop()
    {
        _seq?.Kill();
        HideBand();
        _seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        _seq.AppendInterval(idleInterval);
        _seq.Append(_bandRT.DOAnchorPosX(SweepTargetX, sweepDuration).SetEase(Ease.InOutQuad));
        _seq.AppendCallback(HideBand);
        _seq.SetLoops(-1);
    }

#if UNITY_EDITOR
    double _editorElapsed;
    double _lastEditorTime;
    float _lastBandWidthRatio = -1f, _lastTiltAngle = float.NaN, _lastBandAlpha = -1f;

    // 플레이 중이 아닐 때 DOTween 대신 EditorApplication.update로 직접 시간 누적 — PlayLoop와 동일한
    // 타이밍(대기 idleInterval → 스윕 sweepDuration → 숨김)을 그대로 재현해 왼쪽→오른쪽 스윕을 실제로 보여준다.
    void EditorTick()
    {
        if (this == null) { EditorApplication.update -= EditorTick; return; }
        if (Application.isPlaying) { EditorApplication.update -= EditorTick; return; }
        if (_rt == null) _rt = transform as RectTransform;
        if (_bandRT == null) { BuildMaskAndBand(); return; }

        // 인스펙터 값이 바뀌면 크기/기울기/알파 즉시 반영
        if (_lastBandWidthRatio != bandWidthRatio || _lastTiltAngle != tiltAngle || _lastBandAlpha != bandAlpha)
        {
            ApplyBandLayout();
            _lastBandWidthRatio = bandWidthRatio; _lastTiltAngle = tiltAngle; _lastBandAlpha = bandAlpha;
        }

        double now = EditorApplication.timeSinceStartup;
        double dt = now - _lastEditorTime;
        _lastEditorTime = now;
        _editorElapsed += dt;

        float cycle = Mathf.Max(0.01f, idleInterval + sweepDuration);
        float t = (float)(_editorElapsed % cycle);

        if (t < idleInterval)
        {
            HideBand(); // interval 구간 — 안 보임
        }
        else
        {
            float sweepT = Mathf.Clamp01((t - idleInterval) / Mathf.Max(0.01f, sweepDuration));
            float eased = EaseInOutQuad(sweepT);
            _bandRT.anchoredPosition = new Vector2(Mathf.LerpUnclamped(RestX, SweepTargetX, eased), 0f);
        }

        SceneView.RepaintAll(); // 강제 리페인트 없인 씬 뷰가 안 갱신돼 애니메이션이 안 움직이는 것처럼 보임
    }

    static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
#endif

    // 가운데가 제일 밝고 양 끝은 투명한 가로 그라디언트 — 띠 폭 방향(sizeDelta.x)으로 매핑되어 부드러운 경계를 만든다.
    static Sprite GetGradientSprite()
    {
        if (_gradientSprite != null) return _gradientSprite;

        const int size = 64;
        var tex = new Texture2D(size, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < size; x++)
        {
            float t = x / (float)(size - 1);
            float a = 1f - Mathf.Abs(t - 0.5f) * 2f;
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        _gradientSprite = Sprite.Create(tex, new Rect(0, 0, size, 1), new Vector2(0.5f, 0.5f));
        return _gradientSprite;
    }
}
