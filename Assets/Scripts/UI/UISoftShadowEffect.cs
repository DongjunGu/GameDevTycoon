using UnityEngine;
using UnityEngine.UI;

// 오브젝트를 새로 생성하지 않고, 같은 Graphic의 메시를 정점 단계에서 바깥으로 확장해 그 마진에
// 스프라이트 알파 모양을 그대로 따라가는 블러 그림자를 그린다(UI/SoftShadow.shader — rounded-box가
// 아니라 _MainTex 알파를 다중 샘플링하는 방식이라 사다리꼴 등 임의 실루엣도 그 모양대로 번짐).
// Unity 내장 Shadow/Outline과 동일한 BaseMeshEffect 패턴 — 새 GameObject 없음.
//
// ⚠️ ButtonSurroundShadow / UnityEngine.UI.Shadow / Outline과 같은 GameObject에 절대 같이 달지 말 것.
// IMeshModifier는 GetComponents 순서대로 이전 이펙트가 만든 메시를 이어받는데, 이 컴포넌트는
// "원본 그래픽의 순수 사각형"이라고 가정하고 정점 min/max로 bounding box를 계산하므로, 다른
// 메시 이펙트가 먼저 정점을 늘려놓으면 계산이 깨진다.
//
// Image.Type.Simple(또는 Filled)에서만 정확 — Sliced/Tiled는 정점 수·UV 구조가 달라 미지원.
[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
[DisallowMultipleComponent]
public class UISoftShadowEffect : BaseMeshEffect
{
    public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    // (0,0) = Figma Drop Shadow의 X/Y 오프셋 0과 동일 — 그림자 SDF 중심이 콘텐츠와 겹쳐 사방으로
    // 고르게 번짐. 0이 아닌 값을 넣으면 그 방향으로만 삐져나오는 일반적인(Unity Shadow류) 방향성
    // 드롭섀도우가 됨.
    public Vector2 shadowOffset = Vector2.zero;
    [Min(0f)] public float shadowBlur = 12f;

    static readonly int ShadowColorId  = Shader.PropertyToID("_ShadowColor");
    static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
    static readonly int ShadowBlurId   = Shader.PropertyToID("_ShadowBlur");
    static readonly int SizeId         = Shader.PropertyToID("_Size");

    Material _instanceMaterial;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureMaterial();
        PushMaterialProps();
    }

    protected override void OnDisable()
    {
        CleanupMaterial();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        CleanupMaterial();
        base.OnDestroy();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (!isActiveAndEnabled) return;
        EnsureMaterial();
        PushMaterialProps();
        graphic.SetVerticesDirty();
    }
#endif

    void EnsureMaterial()
    {
        if (_instanceMaterial != null) return;
        var shader = Shader.Find("UI/SoftShadow");
        if (shader == null) { Debug.LogWarning("[UISoftShadowEffect] Shader 'UI/SoftShadow' not found."); return; }
        _instanceMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        graphic.material = _instanceMaterial;
    }

    void PushMaterialProps()
    {
        if (_instanceMaterial == null) return;
        _instanceMaterial.SetColor(ShadowColorId, shadowColor);
        _instanceMaterial.SetVector(ShadowOffsetId, shadowOffset);
        _instanceMaterial.SetFloat(ShadowBlurId, shadowBlur);
    }

    void CleanupMaterial()
    {
        if (graphic != null) graphic.material = null;
        if (_instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(_instanceMaterial);
            else                       DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
        }
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!isActiveAndEnabled || vh.currentVertCount == 0) return;
        EnsureMaterial();
        if (_instanceMaterial == null) return;

        int n = vh.currentVertCount;

        // 1) 실제 정점 min/max로 bounding box 중심/half-size 계산 — RectTransform.pivot을 믿지 않음
        //    (예: EmployeeTabBtn처럼 pivot이 (1.95, 0.5)인 비정상 케이스가 이미 있었음).
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
        var vert = new UIVertex();
        for (int i = 0; i < n; i++)
        {
            vh.PopulateUIVertex(ref vert, i);
            min = Vector3.Min(min, vert.position);
            max = Vector3.Max(max, vert.position);
        }
        Vector2 center = (Vector2)(min + max) * 0.5f;
        Vector2 halfSize = (Vector2)(max - min) * 0.5f;
        if (halfSize.x <= 0f || halfSize.y <= 0f) return;

        // 2) 블러+오프셋 크기만큼 마진을 두고 바깥으로 확장.
        float margin = shadowBlur + Mathf.Max(Mathf.Abs(shadowOffset.x), Mathf.Abs(shadowOffset.y));
        Vector2 expandedHalfSize = halfSize + new Vector2(margin, margin);
        Vector2 uvScale = new Vector2(expandedHalfSize.x / halfSize.x, expandedHalfSize.y / halfSize.y);

        for (int i = 0; i < n; i++)
        {
            vh.PopulateUIVertex(ref vert, i);

            Vector2 local = (Vector2)vert.position - center;
            Vector2 sign = new Vector2(Mathf.Sign(local.x), Mathf.Sign(local.y));
            Vector2 expandedLocal = new Vector2(sign.x * expandedHalfSize.x, sign.y * expandedHalfSize.y);
            vert.position = new Vector3(center.x + expandedLocal.x, center.y + expandedLocal.y, vert.position.z);

            // 원본 스프라이트는 그대로, 마진 영역만 uv0이 [0,1] 밖으로 비례 확장되어
            // 셰이더가 "그림자 전용" 영역으로 인식.
            Vector2 uv = vert.uv0;
            vert.uv0 = new Vector2(0.5f + (uv.x - 0.5f) * uvScale.x, 0.5f + (uv.y - 0.5f) * uvScale.y);

            vh.SetUIVertex(vert, i);
        }

        // 3) 매 리빌드마다 원본 half-size 갱신 — 해상도/앵커 변경에 자동 추종(별도 폴링 불필요).
        _instanceMaterial.SetVector(SizeId, halfSize);
    }
}
