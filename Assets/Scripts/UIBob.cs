using UnityEngine;

// FloatBob(월드스페이스용)의 UI RectTransform 버전 — 기준 anchoredPosition 주변에서 sin파로 위아래 둥실거림.
// 웨이브 전용 그림 없이도 기존 스프라이트 조각(예: 게이지 상단 캡)을 얹어 살짝 출렁이는 느낌만 내고 싶을 때 사용.
public class UIBob : MonoBehaviour
{
    [Tooltip("위아래로 흔들리는 폭 (RectTransform 로컬 단위)")]
    public float amplitude = 4f;
    [Tooltip("흔들리는 속도 (클수록 빠름)")]
    public float speed = 2.5f;

    RectTransform _rt;
    Vector2 _baseAnchoredPos;

    void OnEnable()
    {
        _rt = transform as RectTransform;
        _baseAnchoredPos = _rt.anchoredPosition;
    }

    void Update()
    {
        _rt.anchoredPosition = _baseAnchoredPos + Vector2.up * (Mathf.Sin(Time.time * speed) * amplitude);
    }
}
