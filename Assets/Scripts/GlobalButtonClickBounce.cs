using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

// 전역 버튼 클릭 펀치 연출 — 어떤 버튼을 눌러도 살짝 작아졌다 커지게 한다.
// GlobalUiClickSfx 와 동일 패턴: 버튼마다 리스너를 다는 대신, 클릭 순간 UI 레이캐스트로
// Button 이 맞았는지 보고 트윈 적용 → 런타임에 생성된 버튼(카드/슬롯 등)까지 자동 커버.
//
// 버튼 자신의 RectTransform 을 직접 스케일하면 두 가지 문제가 있다:
//   1) pivot 이 버튼마다 제각각(좌상단/모서리/중앙 등)이라 pivot 기준으로 줄어들어 보인다.
//   2) 대부분의 버튼이 VerticalLayoutGroup/HorizontalLayoutGroup/GridLayoutGroup 의 자식이라,
//      레이아웃이 리빌드될 때마다 anchoredPosition/sizeDelta 를 되돌려버려 위치 보정이 씹힌다.
//
// [설계] 버튼을 "복제"하지 않고, 버튼의 부모 자리에 pivot(0.5,0.5) 래퍼를 끼워넣은 뒤 버튼
// 자신을 그 래퍼의 풀스트레치 자식으로 재배치한다. 레이아웃 그룹은 이제 래퍼를 직접적인 자식
// 으로 인식해 위치/크기를 관리하고, 버튼은 한 단계 안쪽에서 항상 래퍼를 꽉 채운 채 유지된다.
// 스케일 애니메이션은 래퍼의 localScale 에만 적용 — 레이아웃은 localScale 을 절대 건드리지
// 않으므로 리빌드와 충돌하지 않고, 래퍼 pivot 이 항상 중앙이라 어떤 버튼이든 중앙 기준으로
// 커지고 작아진다.
//
// ⚠ 이전에는 버튼의 배경 Image 를 복제해 별도 자식(VisualCopy)으로 만들고 Button.targetGraphic
// 을 그 사본으로 재지정한 뒤 원본을 투명화하는 방식이었다. 이 경우 재지정이 일어나는 "첫 클릭"
// 프레임에 Unity 의 Selectable 전환(Pressed/Selected 등)이 재지정 전의 원본에 적용될 수도 있어,
// 화면엔 스프라이트가 안 바뀐 채 얼어붙은 사본만 스케일되는 레이스 컨디션이 있었다. 지금 방식은
// 버튼 GameObject(Image/Button 컴포넌트/자식/targetGraphic)를 전혀 복제·변형하지 않으므로 이
// 문제 자체가 발생할 수 없다 — SpriteSwap/ColorTint 등 어떤 Transition 을 쓰든 항상 원본 그대로.
//
// 트레이드오프: 스케일이 래퍼 전체(버튼 포함)에 적용되므로, 눌림 애니메이션 도중에는 클릭
// 판정 영역도 함께 살짝 작아진다(이전 방식은 원본 판정을 항상 풀사이즈로 유지했음). 애니메이션이
// 짧고(60~120ms) 클릭은 이미 press 시점에 확정되므로 실질적 영향은 미미하다.
public class GlobalButtonClickBounce : MonoBehaviour
{
    [SerializeField] private float shrinkScale = 0.9f;
    [SerializeField] private float shrinkDuration = 0.06f;
    [SerializeField] private float growDuration = 0.12f;

    const string WrapperName = "__ClickBounceWrapper";

    // 래퍼 GameObject 에 붙는 빈 마커 — "이미 래핑됐는지" 판별용(이름 문자열 비교보다 안전).
    class WrapperMarker : MonoBehaviour { }

    private readonly List<RaycastResult> results = new List<RaycastResult>();
    private RectTransform _pressedWrapper; // 눌려서 축소된 채 유지 중인 래퍼 (뗄 때 원복)

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            var es = EventSystem.current;
            if (es == null) return;

            var data = new PointerEventData(es) { position = pointer.position.ReadValue() };
            results.Clear();
            es.RaycastAll(data, results);

            for (int i = 0; i < results.Count; i++)
            {
                // 맨 위(가장 먼저 맞은) 결과가 팝업(PopupClickAway) 안이면 뒤로 더 넘어가지 않고 즉시 중단.
                // 안 그러면 팝업 자신은 Button 조상이 없어 이 루프가 팝업 뒤에 가려진 버튼까지 뚫고
                // 들어가 그 버튼이 눌린 것처럼 튕겨버린다 — 팝업을 눌렀는데 뒤의 버튼이 반응하는 버그.
                if (results[i].gameObject.GetComponentInParent<PopupClickAway>() != null) return;

                var btn = results[i].gameObject.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    var wrapper = GetOrCreateWrapper(btn);
                    if (wrapper == null) return;
                    _pressedWrapper = wrapper;
                    Animate(wrapper, shrinkScale, shrinkDuration, Ease.OutQuad);
                    return; // 가장 위 버튼 1개만
                }
            }
        }
        else if (pointer.press.wasReleasedThisFrame && _pressedWrapper != null)
        {
            Animate(_pressedWrapper, 1f, growDuration, Ease.OutBack);
            _pressedWrapper = null;
        }
    }

    // 버튼의 부모 자리에 pivot(0.5,0.5) 래퍼를 끼워넣고(이미 있으면 재사용), 버튼을 그 래퍼의
    // 풀스트레치 자식으로 재배치한다. 버튼 GameObject 자체(컴포넌트/자식/targetGraphic)는
    // 전혀 손대지 않는다 — 계층 구조상 위치만 한 단계 안쪽으로 들어간다.
    RectTransform GetOrCreateWrapper(Button btn)
    {
        var btnRt = btn.transform as RectTransform;
        if (btnRt == null) return null;

        // 이미 래핑된 버튼이면 그 부모(래퍼)를 재사용.
        if (btnRt.parent != null && btnRt.parent.GetComponent<WrapperMarker>() != null)
            return btnRt.parent as RectTransform;

        var originalParent = btnRt.parent;
        if (originalParent == null) return null; // 씬 루트 버튼은 래핑 대상에서 제외(드묾)

        var wrapperGO = new GameObject(WrapperName, typeof(RectTransform));
        wrapperGO.AddComponent<WrapperMarker>();
        var wrapperRt = (RectTransform)wrapperGO.transform;
        wrapperRt.SetParent(originalParent, false);

        // 버튼이 레이아웃에서 차지하던 자리(앵커/피벗/위치/크기)를 래퍼가 그대로 이어받는다.
        wrapperRt.anchorMin        = btnRt.anchorMin;
        wrapperRt.anchorMax        = btnRt.anchorMax;
        wrapperRt.pivot            = btnRt.pivot;
        wrapperRt.anchoredPosition = btnRt.anchoredPosition;
        wrapperRt.sizeDelta        = btnRt.sizeDelta;
        wrapperRt.localScale       = Vector3.one;
        wrapperRt.SetSiblingIndex(btnRt.GetSiblingIndex()); // 반드시 버튼을 옮기기 전에 — 지금 인덱스가 곧 버튼의 원래 자리

        // LayoutElement 가 있으면(레이아웃 그룹이 크기 계산에 참조) 래퍼에도 동일하게 복제해
        // 레이아웃이 래퍼를 실제 자식으로 취급했을 때 크기가 어긋나지 않게 한다.
        var srcLE = btnRt.GetComponent<LayoutElement>();
        if (srcLE != null)
        {
            var dstLE = wrapperGO.AddComponent<LayoutElement>();
            dstLE.ignoreLayout    = srcLE.ignoreLayout;
            dstLE.minWidth        = srcLE.minWidth;
            dstLE.minHeight       = srcLE.minHeight;
            dstLE.preferredWidth  = srcLE.preferredWidth;
            dstLE.preferredHeight = srcLE.preferredHeight;
            dstLE.flexibleWidth   = srcLE.flexibleWidth;
            dstLE.flexibleHeight  = srcLE.flexibleHeight;
            dstLE.layoutPriority  = srcLE.layoutPriority;
        }

        // 버튼을 래퍼의 풀스트레치 자식으로 이동 — 버튼 GameObject 는 완전히 그대로(컴포넌트/
        // 자식/targetGraphic 전부 무손상), 계층 위치만 한 단계 안쪽으로 들어간다.
        btnRt.SetParent(wrapperRt, false);
        btnRt.anchorMin        = Vector2.zero;
        btnRt.anchorMax        = Vector2.one;
        btnRt.pivot            = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = Vector2.zero;
        btnRt.sizeDelta        = Vector2.zero;

        return wrapperRt;
    }

    void Animate(RectTransform rt, float targetScale, float duration, Ease ease)
    {
        rt.DOKill();
        rt.DOScale(targetScale, duration).SetEase(ease).SetUpdate(true);
    }
}
