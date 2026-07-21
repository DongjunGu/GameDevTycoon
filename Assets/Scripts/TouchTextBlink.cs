using UnityEngine;
using TMPro;
using DG.Tweening;

// 재사용 가능한 "터치하여 닫기" 텍스트 깜빡임 — TrainingPanelUI 가 결과 패널에서 쓰던 것과 동일한 타이밍
// (알파 1↔0.25, 0.45초 야요 반복)을 공용화. 부착된 TMP 텍스트가 활성화될 때마다 자동으로 시작, 비활성화되면
// 자동으로 멈춘다 — AlertUI/CriticUI 등 다른 스크립트를 손댈 필요 없이 TouchText 오브젝트에 붙이기만 하면 됨.
[RequireComponent(typeof(TMP_Text))]
public class TouchTextBlink : MonoBehaviour
{
    [SerializeField] float minAlpha = 0.25f;
    [SerializeField] float duration = 0.45f;

    TMP_Text _text;
    Tween _tween;

    void Awake() => _text = GetComponent<TMP_Text>();

    void OnEnable()
    {
        _tween?.Kill();
        var c = _text.color; c.a = 1f; _text.color = c;
        _tween = _text.DOFade(minAlpha, duration).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
    }
}
