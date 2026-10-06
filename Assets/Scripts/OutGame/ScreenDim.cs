using UnityEngine;
using UnityEngine.UI;

// 패널 뒤에 풀스크린 dim 을 깔아 뒤쪽 UI 클릭을 막는다 (아웃게임용).
// 아웃게임 MainCanvas 는 카메라가 없어 인게임 ModalBlocker(ScreenSpaceCamera 캔버스)가 그 아래에 깔리므로,
// 같은 루트 캔버스 안에서 dim(order) / 패널(order + 1) 을 중첩 Canvas 정렬로 올린다.
public static class ScreenDim
{
    const float Alpha = 0.6f;

    // panel 을 SetActive(true) 한 뒤에 호출 — overrideSorting 은 활성 상태에서만 먹는다.
    public static void Show(ref GameObject dim, GameObject panel, int order)
    {
        if (panel == null) return;
        if (dim == null) dim = Create(panel);
        if (dim == null) return;
        dim.SetActive(true);
        ApplySorting(dim, order);
        ApplySorting(panel, order + 1);
    }

    public static void Hide(GameObject dim)
    {
        if (dim != null) dim.SetActive(false);
    }

    static GameObject Create(GameObject panel)
    {
        var parentCanvas = panel.GetComponentInParent<Canvas>(true);
        if (parentCanvas == null) return null;

        // SafeArea 에 잘리지 않도록 루트 캔버스 직속 풀스크린
        var dim = new GameObject(panel.name + "Dim", typeof(RectTransform), typeof(Image));
        dim.layer = panel.layer;
        var rt = (RectTransform)dim.transform;
        rt.SetParent(parentCanvas.rootCanvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, Alpha);
        return dim;
    }

    static void ApplySorting(GameObject go, int order)
    {
        if (!go.TryGetComponent<Canvas>(out var canvas)) canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (!go.TryGetComponent<GraphicRaycaster>(out _)) go.AddComponent<GraphicRaycaster>();
    }
}
