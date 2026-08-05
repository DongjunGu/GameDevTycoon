using UnityEngine;
using DG.Tweening;

// 재사용 가능한 좌우 회전 흔들림 — 부착된 오브젝트의 "현재" z회전을 기준점으로 삼아 ±amplitude 도만큼
// 무한 왕복(예: 손 흔들기 제스처). activeInHierarchy 전이에 따라 OnEnable/OnDisable로 자동 시작/정지,
// 정지 시 기준 회전으로 복귀한다.
public class RotateWobble : MonoBehaviour
{
    [Tooltip("기준 회전에서 ±몇 도까지 왕복할지")]
    public float amplitude = 4f;
    [Tooltip("한쪽 끝에서 반대쪽 끝까지 걸리는 시간(초)")]
    public float duration = 0.6f;

    float _baseZ;
    Tween _tween;

    void OnEnable()
    {
        _tween?.Kill();
        _baseZ = transform.localEulerAngles.z;
        SetZ(_baseZ - amplitude);
        _tween = transform.DOLocalRotate(new Vector3(0f, 0f, _baseZ + amplitude), duration, RotateMode.Fast)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        SetZ(_baseZ);
    }

    void SetZ(float z)
    {
        var e = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(e.x, e.y, z);
    }
}
