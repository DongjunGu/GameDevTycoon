using System.Collections;
using UnityEngine;

/// <summary>
/// 창밖 야경(Background/FinalOffice/Sight_BG)을 시간 경과에 따라 천천히 크로스페이드로 바꿔주는 연출 컴포넌트.
///
/// 렌더 순서 주의: Sight_BG(sortingOrder 0)는 창틀 스프라이트(BG_1F order 0 / BG_2F order 1) 뒤에 깔리는
/// 배경이다. 그래서 페이드용 보조 렌더러를 order+1 로 "위에" 얹으면 창틀까지 덮어버린다. 대신 보조 렌더러를
/// order-1 로 "뒤에" 깔아두고 다음 장을 불투명하게 세팅한 뒤, 현재 장(_front)의 알파를 1→0 으로 내린다.
/// 결과 픽셀은 lerp(다음장, 현재장, alpha) 라 일반적인 크로스페이드와 완전히 동일하고, 순서상 창틀보다
/// 뒤라서 다른 배경 요소에는 아무 영향이 없다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SightBackgroundCycler : MonoBehaviour
{
    [Header("순환할 배경 스프라이트 (배열 순서대로 전환)")]
    public Sprite[] sprites;

    [Header("타이밍(초)")]
    [Tooltip("한 장이 그대로 머무는 시간")]
    public float holdSeconds = 45f;
    [Tooltip("다음 장으로 서서히 섞이는 시간")]
    public float fadeSeconds = 15f;

    [Header("동작")]
    [Tooltip("켜면 1→2→3→4→3→2→1… 로 되돌아오며 순환(밤이 깊어졌다 다시 밝아지는 느낌). 끄면 4→1 로 바로 감김")]
    public bool pingPong = true;
    [Tooltip("게임 시간이 멈춘 동안(모달/일시정지)에는 배경 전환도 같이 멈춘다")]
    public bool followGameTime = true;
    [Tooltip("시작할 때 첫 장을 무작위로 고른다")]
    public bool randomStart = false;

    // 현재 보이는 장. 알파를 내려서 뒤에 깔린 다음 장을 드러낸다.
    private SpriteRenderer _front;
    // 다음 장 전용. 평소엔 꺼두고 페이드 중에만 켠다.
    private SpriteRenderer _back;

    private int _index;
    private int _dir = 1;
    private Coroutine _co;

    private void Awake()
    {
        _front = GetComponent<SpriteRenderer>();
        EnsureBackRenderer();
    }

    private void EnsureBackRenderer()
    {
        if (_back != null) return;

        var go = new GameObject("Sight_BG_Fade");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        _back = go.AddComponent<SpriteRenderer>();
        _back.sharedMaterial = _front.sharedMaterial;
        _back.sortingLayerID = _front.sortingLayerID;
        _back.sortingOrder = _front.sortingOrder - 1;   // 반드시 뒤. 위에 올리면 창틀을 가린다.
        _back.drawMode = _front.drawMode;
        _back.size = _front.size;
        _back.flipX = _front.flipX;
        _back.flipY = _front.flipY;
        _back.maskInteraction = _front.maskInteraction;
        _back.color = Color.white;
        _back.enabled = false;
    }

    private void OnEnable()
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("[SightBackgroundCycler] sprites 배열이 비어 있어 배경 순환을 시작하지 않습니다.");
            return;
        }

        EnsureBackRenderer();

        // 인스펙터에 현재 걸려 있는 스프라이트가 배열 안에 있으면 그 위치에서 이어서 시작한다.
        _index = randomStart ? Random.Range(0, sprites.Length) : Mathf.Max(0, System.Array.IndexOf(sprites, _front.sprite));
        _dir = 1;
        _front.sprite = sprites[_index];
        SetAlpha(_front, 1f);
        _back.enabled = false;

        if (sprites.Length < 2) return;
        _co = StartCoroutine(CoCycle());
    }

    private void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        // 페이드 도중에 꺼졌다면 반투명하게 남지 않도록 복구.
        if (_front != null) SetAlpha(_front, 1f);
        if (_back != null) _back.enabled = false;
    }

    private IEnumerator CoCycle()
    {
        while (true)
        {
            yield return WaitGameSeconds(holdSeconds);

            int next = NextIndex();
            _back.sprite = sprites[next];
            _back.enabled = true;

            float t = 0f;
            float dur = Mathf.Max(0.01f, fadeSeconds);
            while (t < dur)
            {
                t += DeltaGameTime();
                // SmoothStep 으로 시작/끝을 완만하게 — 선형이면 전환 시작 순간이 눈에 띈다.
                SetAlpha(_front, 1f - Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }

            _front.sprite = sprites[next];
            SetAlpha(_front, 1f);
            _back.enabled = false;
            _index = next;
        }
    }

    private int NextIndex()
    {
        if (!pingPong) return (_index + 1) % sprites.Length;

        int next = _index + _dir;
        if (next >= sprites.Length || next < 0)
        {
            _dir = -_dir;
            next = _index + _dir;
        }
        return Mathf.Clamp(next, 0, sprites.Length - 1);
    }

    private IEnumerator WaitGameSeconds(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += DeltaGameTime();
            yield return null;
        }
    }

    // 게임 시간이 멈춰 있으면 0을 돌려줘서 연출도 그 자리에 정지시킨다.
    private float DeltaGameTime()
    {
        if (followGameTime && GameTimeManager.Instance != null && !GameTimeManager.Instance.IsRunning)
            return 0f;
        return Time.deltaTime;
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
