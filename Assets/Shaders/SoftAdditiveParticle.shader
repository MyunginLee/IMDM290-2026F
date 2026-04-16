Shader "IMDM290/SoftAdditiveParticle"
{
    Properties
    {
        [HDR]_BaseColor("Base Color", Color) = (1, 0.55, 0.18, 1)
        _Intensity("Intensity", Range(0, 8)) = 1
        _Softness("Softness", Range(0.25, 8)) = 2.2
        _Streak("Streak", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _Softness;
                half _Streak;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = saturate(1.0 - dot(p, p));
                float glow = pow(radius, _Softness);
                float streak = pow(saturate(1.0 - abs(p.y)), 6.0) * pow(saturate(1.0 - abs(p.x)), 1.5) * _Streak;
                float mask = saturate(glow + streak);

                half4 color = input.color * _BaseColor;
                half alpha = color.a * mask;
                half3 rgb = color.rgb * mask * _Intensity;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
