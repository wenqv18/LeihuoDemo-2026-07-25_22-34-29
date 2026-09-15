Shader "Custom/VideoBlackKey"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _KeyThreshold ("Key Threshold", Range(0, 0.5)) = 0.04
        _KeySoftness ("Key Softness", Range(0.0, 0.3)) = 0.04
        _FlipY ("Flip Y", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _KeyThreshold;
                float _KeySoftness;
                float _FlipY;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.uv.y = lerp(output.uv.y, 1.0 - output.uv.y, _FlipY);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
                half alpha = smoothstep(_KeyThreshold - _KeySoftness, _KeyThreshold + _KeySoftness, luminance);
                color.a *= alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
