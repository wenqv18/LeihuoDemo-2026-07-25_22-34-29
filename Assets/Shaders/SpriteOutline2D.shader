Shader "Leihuo/SpriteOutline2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness ("Outline Thickness", Float) = 3
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineThickness;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, input.texcoord) * input.color;
                float2 offset = _MainTex_TexelSize.xy * max(_OutlineThickness, 0);

                fixed outlineAlpha = 0;
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, -offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, -offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, -offset.y)).a);

                fixed outerAlpha = saturate(outlineAlpha - baseColor.a) * _OutlineColor.a;
                return fixed4(_OutlineColor.rgb, outerAlpha);
            }
            ENDCG
        }
    }
}
