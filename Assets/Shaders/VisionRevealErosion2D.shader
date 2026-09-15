Shader "Leihuo/VisionRevealErosion2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RevealCenter ("Reveal Center", Vector) = (0,0,0,0)
        _RevealRadius ("Reveal Radius", Float) = 0
        _RevealSoftness ("Reveal Softness", Float) = 0.12
        _EdgeWidth ("Erosion Edge Width", Float) = 0.18
        _NoiseScale ("Noise Scale", Float) = 18
        _NoiseStrength ("Noise Strength", Float) = 0.22
        _RevealInvert ("Invert Reveal", Float) = 0
        _Seed ("Seed", Float) = 0
        _TealColor ("Teal Accent", Color) = (0.12,1,0.45,1)
        _RedColor ("Red Accent", Color) = (0.42,0.02,0.05,1)
        _BlackColor ("Black Erosion", Color) = (0,0,0,1)
        _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _RendererColor;
            float4 _RevealCenter;
            float _RevealRadius;
            float _RevealSoftness;
            float _EdgeWidth;
            float _NoiseScale;
            float _NoiseStrength;
            float _RevealInvert;
            float _Seed;
            fixed4 _TealColor;
            fixed4 _RedColor;
            fixed4 _BlackColor;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i + _Seed);
                float b = hash21(i + float2(1.0, 0.0) + _Seed);
                float c = hash21(i + float2(0.0, 1.0) + _Seed);
                float d = hash21(i + float2(1.0, 1.0) + _Seed);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                o.color = v.color * _Color * _RendererColor;
                o.texcoord = v.texcoord;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.worldXY = world.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.texcoord) * i.color;
                float alpha = sprite.a;
                if (alpha <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                float2 p = i.worldXY;
                float dist = distance(p, _RevealCenter.xy);
                float n1 = valueNoise(p * _NoiseScale);
                float n2 = valueNoise(p * _NoiseScale * 2.7 + 17.31);
                float noiseOffset = ((n1 - 0.5) * 1.4 + (n2 - 0.5) * 0.55) * _NoiseStrength;
                float signedEdge = dist - (_RevealRadius + noiseOffset);
                float reveal = 1.0 - smoothstep(0.0, max(0.0001, _RevealSoftness), signedEdge);
                float revealMask = lerp(reveal, 1.0 - reveal, step(0.5, _RevealInvert));
                float edge = 1.0 - smoothstep(0.0, max(0.0001, _EdgeWidth), abs(signedEdge));
                float outsideEdge = edge * smoothstep(-_EdgeWidth, _EdgeWidth, signedEdge);
                float insideEdge = edge * (1.0 - smoothstep(-_EdgeWidth, _EdgeWidth, signedEdge));

                float3 color = sprite.rgb;
                color = lerp(color, _BlackColor.rgb, saturate(insideEdge * 0.82 + outsideEdge * 0.96));

                float tealSpeck = step(0.965, n2) * edge;
                float redSpeck = step(0.942, n1) * step(n2, 0.18) * edge;
                color = lerp(color, _RedColor.rgb, redSpeck * 0.46);
                color = lerp(color, _TealColor.rgb, tealSpeck * 0.55);

                float edgeAlpha = edge * alpha * (0.16 + n1 * 0.32);
                float outputAlpha = alpha * revealMask + edgeAlpha;
                return fixed4(color, saturate(outputAlpha));
            }
            ENDCG
        }
    }
}
