// Copyright (c) 2026 John B. Shull. See LICENSE.md.
Shader "Hidden/FuzzPhyte/Tests/DitherStampProbe"
{
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "../../Runtime/Design/FP_Dither/Shaders/Includes/FP_DitherLightingInput.hlsl"
            #include "../../Runtime/Design/FP_Dither/Shaders/Includes/FP_DitherLighting.hlsl"
            float _Tone;
            float2 _Offset;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vertex(float4 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.uv = uv;
                return output;
            }
            float4 Fragment(Varyings input) : SV_Target
            {
                FP_LightingPattern pattern = FP_GetLightingPattern(input.uv + _Offset);
                float value = FP_DitherLightFactor(_Tone, _LightingSteps, _LightingDitherStrength, pattern);
                return float4(value.xxx, 1);
            }
            ENDHLSL
        }
    }
}
