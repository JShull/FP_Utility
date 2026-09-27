// Copyright (c) 2026 John B. Shull. See LICENSE.md.
#ifndef FP_DITHER_LIGHTING_INPUT_INCLUDED
#define FP_DITHER_LIGHTING_INPUT_INCLUDED
// Supply our material layout to the stock URP pass implementations.
#define UNIVERSAL_LIT_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"

// Identical in every pass: do not put material members behind keywords.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half _Metallic, _Smoothness, _BumpScale, _Cutoff;
float _DitherStrength, _DitherScale, _DitherColorSteps, _DitherAmount;
float _DitherBias, _DitherMaskStrength, _DitherPattern, _DitherRotation;
float _DitherArmRatio, _DitherFiltering;
float _LightingDitherStrength, _ShadowDitherStrength, _LightingSteps, _ShadowSteps;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_DitherStamp);
// Explicit point/repeat and LOD zero preserve calibrated ranks. Distance fade
// handles minification; averaged rank mipmaps would bias the tone distribution.
SAMPLER(sampler_FP_Stamp_point_repeat);

half4 SampleMetallicSpecGloss(float2 uv, half alpha)
{
    return half4(_Metallic.xxx, _Smoothness);
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half4 base = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    surface.alpha = Alpha(base.a, _BaseColor, _Cutoff);
    surface.albedo = base.rgb * _BaseColor.rgb;
    surface.metallic = _Metallic;
    surface.smoothness = _Smoothness;
    surface.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surface.occlusion = 1;
}
#endif
