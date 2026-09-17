using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Image/RawImage 아래쪽을 점점 투명하게 — 셰이더 없이 메쉬 정점 알파만 조절한다.
// 덮개 이미지 방식과 달리 배경이 무엇이든 자연스럽고, 스프라이트를 런타임에 바꿔도 그대로 적용된다.
// Simple 타입(정점 4개)은 fadeStart 높이에서 사각형을 둘로 나눠 "위는 불투명, 그 아래부터 페이드"를 만든다.
[RequireComponent(typeof(Graphic))]
public class VerticalFadeEffect : BaseMeshEffect
{
    [Tooltip("페이드가 시작되는 높이 (아래=0, 위=1). 이 높이 위는 불투명")]
    [Range(0f, 1f)] public float fadeStart = 0.5f;

    [Tooltip("맨 아래 알파 (0=완전 투명)")]
    [Range(0f, 1f)] public float bottomAlpha = 0f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        if (vh.currentVertCount == 4)
        {
            // Image Simple 정점 순서: 0=좌하, 1=좌상, 2=우상, 3=우하
            var bl = new UIVertex(); vh.PopulateUIVertex(ref bl, 0);
            var tl = new UIVertex(); vh.PopulateUIVertex(ref tl, 1);
            var tr = new UIVertex(); vh.PopulateUIVertex(ref tr, 2);
            var br = new UIVertex(); vh.PopulateUIVertex(ref br, 3);

            var ml = Lerp(bl, tl, fadeStart);
            var mr = Lerp(br, tr, fadeStart);
            bl.color.a = (byte)(bl.color.a * bottomAlpha);
            br.color.a = (byte)(br.color.a * bottomAlpha);

            vh.Clear();
            vh.AddVert(bl); vh.AddVert(ml); vh.AddVert(mr); vh.AddVert(br);
            vh.AddVert(tl); vh.AddVert(tr);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0); // 페이드 구간
            vh.AddTriangle(1, 4, 5); vh.AddTriangle(5, 2, 1); // 불투명 구간
            return;
        }

        // Sliced/Tiled 등 — 정점 y 위치 기준으로 알파만 곱한다(정점 사이는 선형 보간).
        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        Rect r = graphic.rectTransform.rect;
        float cutY = r.yMin + r.height * fadeStart;
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float t = fadeStart <= 0f ? 1f : Mathf.Clamp01((v.position.y - r.yMin) / (cutY - r.yMin));
            v.color.a = (byte)(v.color.a * Mathf.Lerp(bottomAlpha, 1f, t));
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    static UIVertex Lerp(UIVertex a, UIVertex b, float t)
    {
        a.position = Vector3.Lerp(a.position, b.position, t);
        a.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
        a.color = Color32.Lerp(a.color, b.color, t);
        return a;
    }
}
