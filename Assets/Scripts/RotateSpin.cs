using UnityEngine;
using DG.Tweening;

// 재사용 가능한 연속 회전 — 스케일/알파는 건드리지 않고 z축 기준 한 방향으로 계속 회전만 시킨다.
// activeInHierarchy 전이에 따라 OnEnable/OnDisable로 자동 시작/정지, 정지 시 시작 회전으로 복귀한다.
public class RotateSpin : MonoBehaviour
{
    [Tooltip("360도 한 바퀴 도는 데 걸리는 시간(초)")]
    public float duration = 3f;
    [Tooltip("체크하면 반시계방향, 해제하면 시계방향")]
    public bool counterClockwise = false;

    Vector3 _baseEuler;
    Tween _tween;

    void OnEnable()
    {
        _tween?.Kill();
        _baseEuler = transform.localEulerAngles;
        float delta = counterClockwise ? 360f : -360f;
        _tween = transform.DOLocalRotate(_baseEuler + new Vector3(0f, 0f, delta), duration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true);
    }

    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        transform.localEulerAngles = _baseEuler;
    }
}
