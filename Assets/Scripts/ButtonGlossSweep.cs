using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 재사용 가능한 버튼 글로스 스윕 — 아무 버튼에나 붙이면 그 버튼 크기/스프라이트와 무관하게
// 대각선 하이라이트 띠가 왼쪽→오른쪽으로 한 번씩 훑고 지나가는 연출을 반복한다.
// 버튼 자체 이미지는 건드리지 않고, 런타임에 자식(RectMask2D + 그라디언트 띠)을 스스로 만들어 붙인다.
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

    RectTransform _rt;
    RectTransform _bandRT;
    Image _bandImg;
    Sequence _seq;
    float _startX, _endX;

    static Sprite _gradientSprite;

    void Awake()
    {
        _rt = transform as RectTransform;
        BuildMaskAndBand();
    }

    void OnEnable()  => PlayLoop();
    void OnDisable() => _seq?.Kill();

    void BuildMaskAndBand()
    {
        // 버튼 자체를 마스크로 사용 — 버튼 자체 그래픽엔 영향 없음(이미 자기 rect 안에 있으므로), 띠만 잘림.
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        var bandGo = new GameObject("GlossSweepBand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bandGo.transform.SetParent(transform, false);
        bandGo.transform.SetAsLastSibling(); // 버튼 배경/텍스트 위에 그려지도록

        _bandRT = bandGo.GetComponent<RectTransform>();
        _bandImg = bandGo.GetComponent<Image>();
        _bandImg.raycastTarget = false; // 버튼 클릭을 가로채지 않게
        _bandImg.sprite = GetGradientSprite();
        var c = Color.white; c.a = bandAlpha; _bandImg.color = c;

        float w = _rt.rect.width;
        float h = _rt.rect.height;
        float bandWidth = Mathf.Max(4f, w * bandWidthRatio);
        float diag = Mathf.Sqrt(w * w + h * h); // 기울여도 버튼 세로를 확실히 덮도록 대각선 길이 기준

        _bandRT.anchorMin = new Vector2(0.5f, 0.5f);
        _bandRT.anchorMax = new Vector2(0.5f, 0.5f);
        _bandRT.pivot     = new Vector2(0.5f, 0.5f);
        _bandRT.sizeDelta = new Vector2(bandWidth, diag * 1.2f);
        _bandRT.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);

        _startX = -(w / 2f + bandWidth);
        _endX   =  (w / 2f + bandWidth);
        _bandRT.anchoredPosition = new Vector2(_startX, 0f);
    }

    void PlayLoop()
    {
        _seq?.Kill();
        _bandRT.anchoredPosition = new Vector2(_startX, 0f);
        _seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        _seq.AppendInterval(idleInterval);
        _seq.Append(_bandRT.DOAnchorPosX(_endX, sweepDuration).SetEase(Ease.InOutQuad));
        _seq.AppendCallback(() => _bandRT.anchoredPosition = new Vector2(_startX, 0f));
        _seq.SetLoops(-1);
    }

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
