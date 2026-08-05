using UnityEngine;
using DG.Tweening;

// 재사용 가능한 크기 맥동 — 부착된 오브젝트가 1↔(1+amount) 스케일을 무한 왕복(예: 별이 살짝씩 커졌다
// 작아졌다). activeInHierarchy 전이에 따라 OnEnable/OnDisable로 자동 시작/정지, 정지 시 스케일 1로 복귀.
public class ScalePulse : MonoBehaviour
{
    [Tooltip("1에서 얼마나 더 커질지 (0.15 = 최대 1.15배)")]
    public float amount = 0.15f;
    [Tooltip("1→최대 크기까지 걸리는 시간(초)")]
    public float duration = 0.5f;

    Tween _tween;

    void OnEnable()
    {
        _tween?.Kill();
        transform.localScale = Vector3.one;
        _tween = transform.DOScale(1f + amount, duration)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        transform.localScale = Vector3.one;
    }
}
