Shader "UI/ErosionLogo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ErosionAmount ("Erosion Amount", Range(0,1)) = 0
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _SignalFlash ("Signal Flash", Range(0,1)) = 0
        _GlitchSeed ("Glitch Seed", Float) = 0
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _ErosionAmount;
            float _GlitchAmount;
            float _SignalFlash;
            float _GlitchSeed;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 grid = floor(uv * float2(96.0, 44.0));
                float noise = hash21(grid + _GlitchSeed);
                float rowNoise = hash21(float2(grid.y + _GlitchSeed, 17.0));
                float glitchBand = step(0.58, rowNoise) * _GlitchAmount;
                float2 offset = float2((noise - 0.5) * 0.024 * glitchBand, 0);
                fixed4 baseTex = tex2D(_MainTex, uv + offset) * i.color;
                fixed4 redTex = tex2D(_MainTex, uv + offset + float2(0.006 * glitchBand, 0)) * i.color;
                fixed4 greenTex = tex2D(_MainTex, uv + offset - float2(0.004 * glitchBand, 0)) * i.color;

                float titleZone = smoothstep(0.2, 0.28, uv.y) * (1.0 - smoothstep(0.7, 0.8, uv.y));
                float keepThreshold = 0.985 - _ErosionAmount * 0.32;
                float keep = lerp(1.0, step(noise, keepThreshold), titleZone);

                float3 color;
                color.r = redTex.r;
                color.g = greenTex.g;
                color.b = baseTex.b;
                color += float3(0.02, 0.3, 0.68) * glitchBand;
                color += float3(0.48, 0.0, 0.42) * step(0.91, noise) * _GlitchAmount;
                color = lerp(color, float3(0.0, 0.88, 1.0), _SignalFlash * 0.42);

                float baseAlpha = baseTex.a * keep;

                // The preview uses transparent cyan scanlines that can extend beyond the logo pixels.
                float scanPhase = frac(uv.y * 34.0);
                float scanLine = 1.0 - smoothstep(0.025, 0.16, scanPhase);
                float scanMask = smoothstep(0.06, 0.12, uv.x) * (1.0 - smoothstep(0.88, 0.94, uv.x));
                scanMask *= smoothstep(0.08, 0.16, uv.y) * (1.0 - smoothstep(0.84, 0.92, uv.y));
                float scanBreak = step(0.12, hash21(float2(floor(uv.x * 12.0), floor(uv.y * 34.0) + _GlitchSeed)));
                float scanAlpha = scanLine * scanMask * scanBreak * (0.14 + _GlitchAmount * 0.08) * i.color.a;

                float artifact = step(0.997, hash21(grid + float2(_GlitchSeed * 1.7, _GlitchSeed * 0.43))) * _GlitchAmount;
                float artifactMask = smoothstep(0.12, 0.2, uv.x) * (1.0 - smoothstep(0.8, 0.88, uv.x));
                artifactMask *= smoothstep(0.16, 0.24, uv.y) * (1.0 - smoothstep(0.76, 0.84, uv.y));
                float artifactAlpha = artifact * artifactMask * 0.42 * i.color.a;
                float3 artifactColor = lerp(float3(0.95, 0.02, 0.76), float3(0.0, 0.82, 1.0), step(0.5, noise));

                float outputAlpha = saturate(baseAlpha + scanAlpha + artifactAlpha);
                float3 premultiplied = color * baseAlpha;
                premultiplied += float3(0.02, 0.88, 0.7) * scanAlpha;
                premultiplied += artifactColor * artifactAlpha;
                baseTex.rgb = saturate(premultiplied / max(outputAlpha, 0.0001));
                baseTex.a = outputAlpha;
                #ifdef UNITY_UI_CLIP_RECT
                baseTex.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(baseTex.a - 0.001);
                #endif
                return baseTex;
            }
            ENDCG
        }
    }
}
