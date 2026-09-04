Shader "UI/SoftShadow"
{
    // UI/Default 기반 + 스프라이트 알파 모양 그대로 따라가는 블러 그림자.
    // rounded-box가 아니라 _MainTex의 알파 채널 자체를 다중 샘플링해 블러링하므로, 사다리꼴 등
    // 임의 실루엣도 그 모양 그대로 그림자가 번짐.
    // _Size는 확장 전 원본 half-size(로컬 유닛) — UISoftShadowEffect가 메시 확장 시 매 리빌드마다 갱신.
    // 마진 영역(uv0가 [0,1] 밖)은 그림자만, 원본 영역은 콘텐츠+그림자 src-over 합성.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.35)
        _ShadowOffset ("Shadow Offset (local units, 0,0=Figma 스타일 균등 확산)", Vector) = (0,0,0,0)
        _ShadowBlur ("Shadow Blur Radius (local units)", Float) = 12
        _Size ("Content Half-Size (local units)", Vector) = (100,100,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _ShadowColor;
            float4 _ShadowOffset;
            float _ShadowBlur;
            float4 _Size;

            // Vogel disk(황금각 나선) 샘플링 — 반지름이 sqrt 분포로 연속적으로 늘어나 링 경계가 없음.
            //
            // ⚠️ 탭 수가 핵심이다. 탭이 적으면 "블러"가 아니라 "원본 외곽선을 조금씩 어긋나게 여러 장
            // 겹쳐 찍은 것"이 되어, 그 복사본 하나하나가 겹겹의 층(고스팅)으로 보인다. 탭 간격은
            // 대략 blurRadius * sqrt(PI / SAMPLES) 이므로, 반경 25px에 28탭이면 간격이 5.5px나 되어
            // 층이 훤히 보였다.
            //
            // ⚠️ tex2Dlod로 밉 레벨을 지정해 탭 사이를 메우는 최적화를 시도했다가 되레 UI가 통째로
            // 사라졌다 — UI 스프라이트는 보통 밉맵이 꺼져 있는데(enableMipMap: 0), 밉이 없는 텍스처를
            // 명시적 LOD로 샘플링하면 결과가 미정의라 NaN이 나와 픽셀 전체가 날아간다. tex2D만 쓸 것.
            #define SHADOW_SAMPLES 64
            static const float SHADOW_GOLDEN_ANGLE = 2.39996323;
            // 가우시안 가중치 감쇠 계수 — 바깥 탭일수록 약하게 기여해서 그림자 끝이 부드럽게 사라짐.
            static const float SHADOW_FALLOFF = 2.5;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // uv0 [0,1] = 원본 스프라이트 영역, 그 밖(음수/1 초과)은 UISoftShadowEffect가 늘려둔
                // 그림자 전용 마진 — 두 경우 모두 이 식으로 로컬 좌표를 정확히 복원할 수 있음.
                float2 localPos = (IN.texcoord - 0.5) * (_Size.xy * 2);
                float2 shadowCenterLocal = localPos - _ShadowOffset.xy;

                // _MainTex 알파를 Vogel disk 패턴으로 다중 샘플링해 블러 — 스프라이트 실루엣(사다리꼴 등
                // 임의 모양) 그대로 그림자가 번지게 함. 알파를 step()으로 이진화하면 각 탭의 경계가 되레
                // 날카로워져 고스팅이 심해지므로, 원본 알파를 그대로(부드럽게) 쓴다.
                float2 texSize2 = _Size.xy * 2;

                float shadowSample = 0;
                float weightSum = 0;
                [unroll]
                for (int k = 0; k < SHADOW_SAMPLES; k++)
                {
                    float r = sqrt((k + 0.5) / SHADOW_SAMPLES);
                    float theta = k * SHADOW_GOLDEN_ANGLE;
                    float2 dir;
                    sincos(theta, dir.y, dir.x);
                    float2 sampleLocal = shadowCenterLocal + dir * (r * _ShadowBlur);
                    float2 sampleUV = sampleLocal / texSize2 + 0.5;
                    float insideX = step(0, sampleUV.x) * step(sampleUV.x, 1);
                    float insideY = step(0, sampleUV.y) * step(sampleUV.y, 1);

                    // 바깥쪽 탭일수록 가중치를 낮춰(가우시안) 그림자 끝이 뚝 끊기지 않고 서서히 사라지게.
                    float w = exp(-r * r * SHADOW_FALLOFF);
                    shadowSample += tex2D(_MainTex, sampleUV).a * w * insideX * insideY;
                    weightSum += w;
                }
                shadowSample /= max(weightSum, 1e-5);

                float shadowAlpha = shadowSample * _ShadowColor.a;
                fixed4 shadow = fixed4(_ShadowColor.rgb, shadowAlpha);

                float2 inWindow = step(0, IN.texcoord) * step(IN.texcoord, 1);
                float contentMask = inWindow.x * inWindow.y;
                fixed4 content = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                content.a *= contentMask;

                // 콘텐츠를 그림자 위에 src-over 합성.
                // ⚠️ Blend 모드가 SrcAlpha OneMinusSrcAlpha(스트레이트 알파)이므로 GPU가 rgb에 알파를
                // 한 번 더 곱한다. 여기서 프리멀티플라이드(rgb*a) 상태로 내보내면 알파가 두 번 곱해져(a²)
                // 중간 톤이 전부 옅게 뜬다 — 반드시 마지막에 outA로 나눠 스트레이트 알파로 되돌릴 것.
                float outA = content.a + shadow.a * (1 - content.a);
                float3 premultRGB = content.rgb * content.a + shadow.rgb * shadow.a * (1 - content.a);

                fixed4 col;
                col.rgb = premultRGB / max(outA, 1e-5);
                col.a   = outA;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
