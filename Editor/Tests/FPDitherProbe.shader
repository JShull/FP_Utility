// Copyright (c) 2026 John B. Shull. See LICENSE.md.
Shader "Hidden/FuzzPhyte/Tests/DitherProbe"
{
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../../Runtime/Design/FP_Dither/Shaders/Includes/FP_Dither.hlsl"
            #include "../../Runtime/Design/FP_Dither/Shaders/Includes/FP_DitherMaterialX.hlsl"
            float4 _Color;
            float _Strength, _Scale, _Steps, _Amount, _Bias, _Mask, _DebugThreshold;
            float _Pattern, _Rotation, _ArmRatio, _Filtering, _Legacy;
            float _Portable;
            float4 _UVOffset;
            float4 frag(v2f_img input) : SV_Target
            {
                float3 color;
                float threshold;
                if (_Portable > 0.5)
                    FP_DitherMaterialX_float(_Color.rgb, input.uv + _UVOffset.xy, _Strength, _Scale,
                        _Steps, _Amount, _Bias, _Mask, _Pattern, _Rotation, _ArmRatio,
                        _Filtering, color, threshold);
                else if (_Legacy > 0.5)
                    FP_Dither_float(_Color.rgb, input.uv + _UVOffset.xy, _Strength, _Scale,
                        _Steps, _Amount, _Bias, _Mask, color, threshold);
                else
                    FP_DitherShape_float(_Color.rgb, input.uv + _UVOffset.xy, _Strength, _Scale,
                        _Steps, _Amount, _Bias, _Mask, _Pattern, _Rotation, _ArmRatio,
                        _Filtering, color, threshold);
                return float4(lerp(color, threshold.xxx, _DebugThreshold), 1);
            }
            ENDHLSL
        }
    }
}
