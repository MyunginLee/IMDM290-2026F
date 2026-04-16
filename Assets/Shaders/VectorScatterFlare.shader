Shader "IMDM290/VectorScatterFlare"
{
    HLSLINCLUDE
        static const float kPi = 3.14159265359;
    ENDHLSL

    Properties
    {
        [HDR]_BaseColor("Base Color", Color) = (1, 0.45, 0.08, 1)
        _Intensity("Intensity", Range(0, 8)) = 1
        _CoreWidth("Core Width", Range(0.01, 1)) = 0.08
        _CoreHeight("Core Height", Range(0.01, 1)) = 0.16
        _StreakFalloff("Streak Falloff", Range(0.25, 8)) = 2.2
        _HaloPower("Halo Power", Range(0.25, 8)) = 2.5
        _CrossIntensity("Cross Intensity", Range(0, 2)) = 0.25
        _BandStrength("Band Strength", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _CoreWidth;
                half _CoreHeight;
                half _StreakFalloff;
                half _HaloPower;
                half _CrossIntensity;
                half _BandStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float x = abs(p.x);
                float y = abs(p.y);

                float coreWidth = max(_CoreWidth, 0.0001);
                float coreHeight = max(_CoreHeight, 0.0001);

                float core = exp(-((x * x) / (coreWidth * coreWidth) + (y * y) / (coreHeight * coreHeight)));
                float streak = pow(saturate(1.0 - x), _StreakFalloff) * exp(-y * y * 18.0);
                float halo = pow(saturate(1.0 - length(p)), _HaloPower);
                float cross = exp(-x * x * 20.0) * pow(saturate(1.0 - y), 3.0) * _CrossIntensity;
                float bandPhase = (p.x * 16.0 - p.y * 4.0) * kPi;
                float bands = lerp(1.0, 0.82 + 0.18 * cos(bandPhase), _BandStrength);

                float energy = max(core * 1.4, streak + cross) + halo * 0.35;
                energy *= bands;

                half alpha = saturate(energy) * _BaseColor.a;
                half3 color = _BaseColor.rgb * energy * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
