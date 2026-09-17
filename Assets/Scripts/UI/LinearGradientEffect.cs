using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Image 에 2색 선형 그라데이션 — 정점 색만 바꾼다(셰이더/텍스처 불필요).
// 정점 색은 스프라이트 색에 곱해지므로, 흰색 스프라이트(또는 스프라이트 없음) + Image.color 흰색이면 지정한 색 그대로 나온다.
[RequireComponent(typeof(Graphic))]
public class LinearGradientEffect : BaseMeshEffect
{
    public enum Direction { LeftToRight, TopToBottom }

    public Direction direction = Direction.LeftToRight;
    public Color startColor = new Color32(0xC4, 0xDA, 0xEC, 0xFF); // 왼쪽 또는 위
    public Color endColor = new Color32(0xF3, 0xF1, 0xE3, 0xFF);   // 오른쪽 또는 아래

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        Rect r = graphic.rectTransform.rect;

        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float t = direction == Direction.LeftToRight
                ? Mathf.InverseLerp(r.xMin, r.xMax, v.position.x)
                : Mathf.InverseLerp(r.yMax, r.yMin, v.position.y);
            v.color = (Color32)((Color)v.color * Color.Lerp(startColor, endColor, t));
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null) graphic.SetVerticesDirty();
    }
#endif
}
