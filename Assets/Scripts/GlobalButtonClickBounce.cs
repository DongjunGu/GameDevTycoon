using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

// 전역 버튼 클릭 펀치 연출 — 어떤 버튼을 눌러도 살짝 작아졌다 커지게 한다.
// GlobalUiClickSfx 와 동일 패턴: 버튼마다 리스너를 다는 대신, 클릭 순간 UI 레이캐스트로
// Button 이 맞았는지 보고 트윈 적용 → 런타임에 생성된 버튼(카드/슬롯 등)까지 자동 커버.
// 버튼의 쉬는 scale 은 항상 1(1,1,1)이라고 가정 — 대부분의 UI 버튼은 sizeDelta 로 크기를 조절하지
// localScale 을 따로 쓰지 않으므로, 원래 scale 을 버튼별로 캐시하지 않고 항상 1로 되돌린다
// (캐시하면 런타임 생성/파괴되는 버튼이 많은 이 프로젝트 특성상 Dictionary 가 계속 불어남).
public class GlobalButtonClickBounce : MonoBehaviour
{
    [SerializeField] private float shrinkScale = 0.9f;
    [SerializeField] private float shrinkDuration = 0.06f;
    [SerializeField] private float growDuration = 0.12f;

    private readonly List<RaycastResult> results = new List<RaycastResult>();

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        var es = EventSystem.current;
        if (es == null) return;

        var data = new PointerEventData(es) { position = pointer.position.ReadValue() };
        results.Clear();
        es.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            var btn = results[i].gameObject.GetComponentInParent<Button>();
            if (btn != null && btn.interactable)
            {
                Bounce(btn.transform);
                return; // 가장 위 버튼 1개만
            }
        }
    }

    void Bounce(Transform t)
    {
        t.DOKill();
        var seq = DOTween.Sequence().SetUpdate(true).SetTarget(t);
        seq.Append(t.DOScale(shrinkScale, shrinkDuration).SetEase(Ease.OutQuad));
        seq.Append(t.DOScale(1f, growDuration).SetEase(Ease.OutBack));
    }
}
