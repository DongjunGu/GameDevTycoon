using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 재사용 가능한 반짝임+맥동 이펙트 — TrainingPanelUI의 FaillImage/sprinkle과 동일한 동작(스케일 1↔1.3
// 무한 왕복(개별 랜덤 주기) + 알파가 계속 새 랜덤값으로 부드럽게 바뀜, 둘은 서로 독립 루프)을 재사용
// 가능한 컴포넌트로 뺀 버전. 부착된 Image가 활성화될 때마다(부모 패널이 켜지는 것 포함, activeInHierarchy
// 전이 시 자동 호출됨) 자동으로 시작하고, 비활성화되면 멈추고 스케일/알파를 원상복구한다.
[RequireComponent(typeof(Image))]
public class SprinkleBlink : MonoBehaviour
{
    [Tooltip("스케일 1↔1.3 왕복 1회 소요 시간(초) 범위 — 각 인스턴스마다 랜덤이라 서로 안 맞물림")]
    public float sprinkleScaleMinDuration = 0.6f;
    public float sprinkleScaleMaxDuration = 1.1f;
    [Tooltip("알파가 새 랜덤값으로 바뀌는 데 걸리는 시간(초)")]
    public float sprinkleAlphaChangeDuration = 0.4f;
    [Tooltip("알파가 랜덤으로 오갈 범위(0~1)")]
    public float sprinkleAlphaMin = 0.3f;
    public float sprinkleAlphaMax = 1f;

    Image _image;
    readonly System.Collections.Generic.List<Tween> _tweens = new();

    void Awake() => _image = GetComponent<Image>();

    void OnEnable()
    {
        StopTweens();

        _image.rectTransform.localScale = Vector3.one;
        var c = _image.color; c.a = Random.Range(sprinkleAlphaMin, sprinkleAlphaMax); _image.color = c;

        float scaleDur = Random.Range(sprinkleScaleMinDuration, sprinkleScaleMaxDuration);
        _tweens.Add(_image.rectTransform.DOScale(1.3f, scaleDur)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));

        ScheduleAlpha();
    }

    void ScheduleAlpha()
    {
        float target = Random.Range(sprinkleAlphaMin, sprinkleAlphaMax);
        _tweens.Add(_image.DOFade(target, sprinkleAlphaChangeDuration)
            .SetUpdate(true).OnComplete(ScheduleAlpha));
    }

    void OnDisable()
    {
        StopTweens();
        if (_image == null) return;
        _image.rectTransform.localScale = Vector3.one;
        var c = _image.color; c.a = 1f; _image.color = c;
    }

    void StopTweens()
    {
        foreach (var t in _tweens) t?.Kill();
        _tweens.Clear();
    }
}
