using UnityEngine;
using UnityEngine.UI;

// 버튼을 8방향(상하좌우 + 대각선)으로 감싸는 Shadow를 깔아 사방 전체(모서리 포함)에서 살짝
// 삐져나오는 그림자를 만든다. UnityEngine.UI.Shadow는 원본 도형을 오프셋만큼 복제해 색을
// 입히는 방식이라 블러는 없지만, 4방향(상하좌우)만 쓰면 사이 대각선 구간(특히 둥근 모서리)이
// 비어 보인다 — 자식 8개(N/S/E/W/NE/NW/SE/SW)에 원본과 같은 스프라이트를 복제해두고 각각
// 다른 방향으로 하나씩 Shadow를 달아 링 형태로 완전히 감싸지게 만든다.
// (한 GameObject에 Shadow를 여러 개 붙이면 이후 개별 인스턴스를 골라 값을 바꾸기 까다로워서
// 자식으로 나눠 관리 — 각 자식은 원본과 정확히 같은 위치/크기라 "원본 복제" 부분은 실제
// 버튼 뒤에 완전히 가려지고, Shadow가 만든 오프셋 사본만 가장자리로 보인다.)
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ButtonSurroundShadow : MonoBehaviour
{
    public float distance = 5f;
    public Color color = new Color(0.1608f, 0.0235f, 0.0627f, 1f);

    const string RootName = "__SurroundShadow";
    const float Diagonal = 0.70710678f; // 대각선 성분(1/sqrt(2)) — 대각선도 상하좌우와 같은 거리감이 되도록 정규화

    void OnEnable() => Build();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Build();
    }
#endif

    void Build()
    {
        var srcImage = GetComponent<Image>();
        var rt = transform as RectTransform;
        if (srcImage == null || rt == null) return;

        var rootT = rt.Find(RootName);
        RectTransform root = rootT != null ? (RectTransform)rootT : (RectTransform)new GameObject(RootName, typeof(RectTransform)).transform;
        root.SetParent(rt, false);
        root.SetAsFirstSibling(); // 버튼 배경보다 뒤(아래)에 깔림
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // 버튼 자신에 (Horizontal/Vertical)LayoutGroup이 붙어있으면 그 자식들을 나란히 배치하는데,
        // __SurroundShadow는 배치용 자식이 아니라 전체를 덮는 오버레이라 레이아웃 계산에서 제외해야 함.
        var layoutElement = root.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = root.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        root.localScale = Vector3.one;

        float d = distance;
        float dd = distance * Diagonal;
        MakeDirection(root, srcImage, "N",  new Vector2(0f, d));
        MakeDirection(root, srcImage, "S",  new Vector2(0f, -d));
        MakeDirection(root, srcImage, "E",  new Vector2(d, 0f));
        MakeDirection(root, srcImage, "W",  new Vector2(-d, 0f));
        MakeDirection(root, srcImage, "NE", new Vector2(dd, dd));
        MakeDirection(root, srcImage, "NW", new Vector2(-dd, dd));
        MakeDirection(root, srcImage, "SE", new Vector2(dd, -dd));
        MakeDirection(root, srcImage, "SW", new Vector2(-dd, -dd));
    }

    void MakeDirection(RectTransform root, Image srcImage, string name, Vector2 offset)
    {
        var childT = root.Find(name);
        GameObject go = childT != null ? childT.gameObject : new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = srcImage.sprite;
        img.type = srcImage.type;
        img.color = Color.white;
        img.raycastTarget = false;

        var shadow = go.GetComponent<Shadow>();
        if (shadow == null) shadow = go.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }
}
