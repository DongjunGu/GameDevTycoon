using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 재사용 가능한 깜빡임 이펙트 — TouchTextBlink(TMP 전용)의 Image 버전. 부착된 Image가 활성화될 때마다
// 알파 1↔minAlpha 를 자동으로 반복하고, 비활성화되면 멈춘다. 선택 표시(DispatchSlotUI.selectedIndicator)처럼
// SetActive(on/off) 만으로 깜빡임을 켜고 끄고 싶을 때 붙이면 된다.
[RequireComponent(typeof(Image))]
public class ImageBlink : MonoBehaviour
{
    [SerializeField] float minAlpha = 0.25f;
    [SerializeField] float duration = 0.45f;

    Image _image;
    Tween _tween;

    void Awake() => _image = GetComponent<Image>();

    void OnEnable()
    {
        _tween?.Kill();
        var c = _image.color; c.a = 1f; _image.color = c;
        _tween = _image.DOFade(minAlpha, duration).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
    }
}
