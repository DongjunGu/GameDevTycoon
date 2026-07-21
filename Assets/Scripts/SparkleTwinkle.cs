using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 재사용 가능한 "반짝임(twinkle)" 이펙트 — 아무 UI 오브젝트에나 붙이면 그 위에 4방향 별빛(star flare)이
// 랜덤 간격으로 확 커졌다 사라지는 걸 반복한다. ButtonGlossSweep과 같은 방식(런타임 프로시저럴 텍스처 +
// DOTween)으로 구현 — 참고 영상(반짝프레임 폴더)에서 보석 아이콘 위에 순간적으로 뜨는 별 모양 하이라이트가
// 이 컴포넌트가 재현하는 부분이고, 배경을 가로지르는 빛줄기 스윕은 ButtonGlossSweep이 이미 담당한다.
// [ExecuteAlways] — 플레이하지 않아도 에디터에서 동일한 타이밍(대기→팝→유지→페이드아웃)으로 미리보기된다.
[ExecuteAlways]
public class SparkleTwinkle : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("다음 반짝임까지 대기 시간 범위(초)")]
    public float minInterval = 1.0f;
    public float maxInterval = 2.2f;
    [Tooltip("0→최대 크기로 확 커지는 시간")]
    public float popDuration = 0.16f;
    [Tooltip("최대 크기에서 유지되는 시간")]
    public float holdDuration = 0.06f;
    [Tooltip("사라지는(축소+페이드) 시간")]
    public float fadeOutDuration = 0.32f;

    [Header("모양")]
    public Vector2 glintSize = new Vector2(36f, 36f);
    public Color glintColor = Color.white;
    [Tooltip("팝 될 때 튕기는 정도 (OutBack 오버슈트 진폭)")]
    public float overshoot = 1.2f;

    [Header("위치")]
    [Tooltip("반짝일 수 있는 지점들(부착된 오브젝트 기준 anchoredPosition 오프셋). 비우면 (0,0) 한 곳만.")]
    public Vector2[] spawnOffsets = { Vector2.zero };
    [Tooltip("체크: spawnOffsets 중 매번 랜덤 한 곳만 반짝 / 해제: 전부 각자 독립적인 타이밍으로 반짝")]
    public bool randomSingleSpot = true;

    RectTransform _rt;
    readonly List<RectTransform> _glints = new();
    readonly List<Tween> _seqs = new();
    static Sprite _starSprite;

    void Awake()
    {
        _rt = transform as RectTransform;
        BuildGlints();
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            KillAll();
            int count = randomSingleSpot ? 1 : Mathf.Max(1, spawnOffsets.Length);
            for (int i = 0; i < count; i++)
                ScheduleNext(i);
        }
#if UNITY_EDITOR
        else
        {
            StartEditorPreview();
        }
#endif
    }

    void OnDisable()
    {
        KillAll();
#if UNITY_EDITOR
        StopEditorPreview();
#endif
    }

    void KillAll()
    {
        foreach (var s in _seqs) s?.Kill();
        _seqs.Clear();
        foreach (var rt in _glints)
        {
            if (rt == null) continue;
            rt.localScale = Vector3.zero;
            var img = rt.GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = 0f; img.color = c; }
        }
    }

    // 글린트(별빛) 오브젝트 필요한 개수만큼 생성 — 도메인 리로드로 Awake가 다시 불려도 기존 걸 재사용.
    // 에디터 프리뷰(플레이 전)에서는 randomSingleSpot과 무관하게 spawnOffsets 전부를 각자 슬롯으로 만들어
    // 설정한 지점을 전부 보여준다 — 실제 플레이 동작(랜덤 한 곳만)은 OnEnable에서 별도로 따른다.
    void BuildGlints()
    {
        if (spawnOffsets == null || spawnOffsets.Length == 0) spawnOffsets = new[] { Vector2.zero };
        int need = Application.isPlaying
            ? (randomSingleSpot ? 1 : spawnOffsets.Length)
            : spawnOffsets.Length;

        for (int i = 0; i < need; i++)
        {
            if (i < _glints.Count && _glints[i] != null) continue;

            var existing = transform.Find($"SparkleGlint{i}");
            GameObject go = existing != null ? existing.gameObject
                : new GameObject($"SparkleGlint{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = GetStarSprite();
            img.color = glintColor;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = glintSize;
            rt.localScale = Vector3.zero;

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // 부착 대상이 LayoutGroup 자식이어도 배치에 안 끼어들게

            if (i < _glints.Count) _glints[i] = rt;
            else _glints.Add(rt);
        }
    }

    void ScheduleNext(int slot)
    {
        float delay = Random.Range(minInterval, maxInterval);
        var t = DOVirtual.DelayedCall(delay, () => PlayPop(slot)).SetUpdate(true).SetTarget(this);
        _seqs.Add(t);
    }

    void PlayPop(int slot)
    {
        if (slot >= _glints.Count || _glints[slot] == null) return;
        var rt = _glints[slot];
        var img = rt.GetComponent<Image>();
        if (img == null) return;

        Vector2 pos = randomSingleSpot
            ? spawnOffsets[Random.Range(0, spawnOffsets.Length)]
            : spawnOffsets[slot % spawnOffsets.Length];
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.zero;
        var c = img.color; c.a = 0f; img.color = c;

        var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        seq.Append(rt.DOScale(1f, popDuration).SetEase(Ease.OutBack, overshoot)); // 확 커지며 튕김
        seq.Join(img.DOFade(1f, popDuration * 0.6f));
        seq.AppendInterval(holdDuration);
        seq.Append(img.DOFade(0f, fadeOutDuration));
        seq.Join(rt.DOScale(0f, fadeOutDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => ScheduleNext(slot));
        _seqs.Add(seq);
    }

#if UNITY_EDITOR
    // 플레이 없이도 씬 뷰에서 같은 타이밍(대기→팝→유지→페이드아웃)을 재현 — DOTween이 에디터에서 안 돌기
    // 때문에 EditorApplication.update로 직접 시간 누적해서 흉내낸다 (ButtonGlossSweep의 EditorTick과 동일 패턴).
    enum Phase { Idle, Pop, Hold, FadeOut }
    Phase[] _ePhase;
    float[] _eTimer;
    float[] _eNextDelay;
    double _eLastTime;

    void StartEditorPreview()
    {
        // 프리뷰는 randomSingleSpot 무시하고 spawnOffsets 전부를 각자 슬롯으로 — 설정한 지점을 전부 보여준다.
        int count = Mathf.Max(1, spawnOffsets.Length);
        _ePhase = new Phase[count];
        _eTimer = new float[count];
        _eNextDelay = new float[count];
        for (int i = 0; i < count; i++)
            _eNextDelay[i] = Random.Range(minInterval, maxInterval);

        _eLastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    void StopEditorPreview() => EditorApplication.update -= EditorTick;

    void EditorTick()
    {
        if (this == null) { EditorApplication.update -= EditorTick; return; }
        if (Application.isPlaying) { EditorApplication.update -= EditorTick; return; }
        if (_rt == null) _rt = transform as RectTransform;
        if (_glints.Count == 0) BuildGlints();
        if (_ePhase == null) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - _eLastTime);
        _eLastTime = now;

        for (int i = 0; i < _ePhase.Length && i < _glints.Count; i++)
            TickSlot(i, dt);

        SceneView.RepaintAll();
    }

    void TickSlot(int i, float dt)
    {
        var rt = _glints[i];
        if (rt == null) return;
        var img = rt.GetComponent<Image>();
        if (img == null) return;

        _eTimer[i] += dt;
        var c = img.color;

        switch (_ePhase[i])
        {
            case Phase.Idle:
                if (_eTimer[i] >= _eNextDelay[i])
                {
                    _eTimer[i] = 0f;
                    _ePhase[i] = Phase.Pop;
                    // 프리뷰는 슬롯=오프셋 고정 매칭(전부 순서대로 보여주기 위해 randomSingleSpot 무시)
                    rt.anchoredPosition = spawnOffsets[i % spawnOffsets.Length];
                }
                break;

            case Phase.Pop:
                float pt = Mathf.Clamp01(_eTimer[i] / Mathf.Max(0.001f, popDuration));
                rt.localScale = Vector3.one * EaseOutBack(pt, overshoot);
                c.a = Mathf.Clamp01(_eTimer[i] / Mathf.Max(0.001f, popDuration * 0.6f));
                img.color = c;
                if (pt >= 1f)
                {
                    _eTimer[i] = 0f;
                    _ePhase[i] = Phase.Hold;
                    rt.localScale = Vector3.one;
                    c.a = 1f; img.color = c;
                }
                break;

            case Phase.Hold:
                if (_eTimer[i] >= holdDuration) { _eTimer[i] = 0f; _ePhase[i] = Phase.FadeOut; }
                break;

            case Phase.FadeOut:
                float ft = Mathf.Clamp01(_eTimer[i] / Mathf.Max(0.001f, fadeOutDuration));
                rt.localScale = Vector3.one * (1f - ft);
                c.a = 1f - ft;
                img.color = c;
                if (ft >= 1f)
                {
                    _eTimer[i] = 0f;
                    _ePhase[i] = Phase.Idle;
                    _eNextDelay[i] = Random.Range(minInterval, maxInterval);
                    rt.localScale = Vector3.zero;
                }
                break;
        }
    }

    // DOTween의 Ease.OutBack(overshoot)와 동일한 커브를 직접 계산(에디터 프리뷰용, DOTween 미실행 상태라 자체 계산).
    static float EaseOutBack(float t, float amplitude)
    {
        float c1 = amplitude;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
#endif

    // 4방향(십자) 별빛 텍스처를 런타임에 직접 그려서 만든다 — 중앙의 부드러운 원형 코어 + 가로/세로 얇은 광선.
    // 외부 스프라이트 에셋 없이 동작(ButtonGlossSweep의 그라디언트 띠 생성과 동일한 방식).
    static Sprite GetStarSprite()
    {
        if (_starSprite != null) return _starSprite;

        const int size = 64;
        const float half = size / 2f;
        const float rayThicknessPx = 2.6f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / half; // 0(중심)~1(가장자리)

                // 중심 코어 — 부드러운 원형 광원
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 1.7f), 2f);

                // 가로/세로 십자 광선 — 축에서 rayThicknessPx 안쪽만 강하게, 중심→끝으로 갈수록 옅어짐
                float axisFalloff = Mathf.Clamp01(1f - r);
                float rayH = Mathf.Clamp01(1f - Mathf.Abs(dy) / rayThicknessPx) * axisFalloff;
                float rayV = Mathf.Clamp01(1f - Mathf.Abs(dx) / rayThicknessPx) * axisFalloff;
                float ray = Mathf.Pow(Mathf.Max(rayH, rayV), 1.5f);

                float a = Mathf.Clamp01(Mathf.Max(core, ray));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _starSprite;
    }
}
