Shader "UI/GaussianBlur"
{
    // RawImage 에 붙여 _MainTex(블러 카메라 RenderTexture)를 가우시안 블러로 표시.
    // 저해상도 RT 다운샘플 + 단일 패스 13-tap 분리형 근사(가로/세로 동시) → 모달 배경용.
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size (px)", Float) = 2.5
        _Color    ("Tint", Color) = (1,1,1,1)
        // UI 마스킹/스텐실 호환용 (Canvas 가 채움)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "RenderType"       = "Transparent"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // (1/w, 1/h, w, h)
            float  _BlurSize;
            float4 _Color;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 9-tap 가우시안 가중치 (정규화 합 = 1)
            static const float W0 = 0.227027;
            static const float W1 = 0.316216; // ±1
            static const float W2 = 0.070270; // ±2

            half4 frag (Varyings IN) : SV_Target
            {
                float2 px = _MainTex_TexelSize.xy * _BlurSize;

                half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * W0;

                // 가로
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( px.x, 0)).rgb * W1;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-px.x, 0)).rgb * W1;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( px.x * 2, 0)).rgb * W2;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-px.x * 2, 0)).rgb * W2;

                // 세로
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,  px.y)).rgb * W1;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -px.y)).rgb * W1;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,  px.y * 2)).rgb * W2;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, -px.y * 2)).rgb * W2;

                // 가중치 합이 1을 넘으므로(가로0.773+세로0.546-중복) 정규화
                col /= (W0 + 4 * W1 + 4 * W2);

                return half4(col, 1.0) * IN.color;
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
