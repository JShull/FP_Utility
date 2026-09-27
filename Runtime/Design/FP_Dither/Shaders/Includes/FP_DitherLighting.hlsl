// Copyright (c) 2026 John B. Shull. See LICENSE.md.
#ifndef FP_DITHER_LIGHTING_INCLUDED
#define FP_DITHER_LIGHTING_INCLUDED
#include "FP_Dither.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Shared sampling is evaluated before light loops; derivatives must not depend
// on which per-pixel clustered light happens to execute a branch.
struct FP_LightingPattern
{
    float rounding;
    float edge;
    float visibility;
};

FP_LightingPattern FP_GetLightingPattern(float2 uv)
{
    FP_LightingPattern p;
    float sine, cosine;
    sincos(_DitherRotation * 0.01745329252, sine, cosine);
    float2 coords = float2(cosine * uv.x - sine * uv.y,
        sine * uv.x + cosine * uv.y) * max(_DitherScale, 1.0);
    float threshold;
    [branch] if (_DitherPattern > 6.5)
        threshold = SAMPLE_TEXTURE2D_LOD(_DitherStamp, sampler_FP_Stamp_point_repeat, frac(coords), 0).r;
    else
        threshold = _DitherPattern < 0.5
            ? FP_Bayer4x4(uv * max(_DitherScale, 1.0))
            : FP_ShapeRank(frac(coords) - 0.5, _DitherPattern, _DitherArmRatio);
    p.rounding = saturate(0.5 + (threshold - 0.5) * saturate(_DitherAmount) + _DitherBias);
    p.edge = max(0.5 * fwidth(p.rounding) * saturate(_DitherFiltering), 0.00001);
    float footprint = max(length(ddx(coords)), length(ddy(coords)));
    p.visibility = 1.0 - saturate(_DitherFiltering) * smoothstep(0.25, 0.75, footprint);
    return p;
}

float FP_DitherLightFactor(float value, float steps, float strength, FP_LightingPattern p)
{
    float intervals = max(floor(steps + 0.5), 2.0) - 1.0;
    float scaled = saturate(value) * intervals;
    float fraction = frac(scaled);
    float upper = smoothstep(1.0 - fraction - p.edge, 1.0 - fraction + p.edge, p.rounding);
    upper *= step(0.000001, fraction);
    float quantized = saturate((floor(scaled) + upper) / intervals);
    return lerp(value, quantized, saturate(strength) * saturate(_DitherMaskStrength) * p.visibility);
}

half3 FP_DitherDirectLight(BRDFData brdf, Light light, InputData input, FP_LightingPattern pattern)
{
    float ndotl = saturate(dot(input.normalWS, light.direction));
    float diffuse = FP_DitherLightFactor(ndotl, _LightingSteps, _LightingDitherStrength, pattern);
    float shadow = FP_DitherLightFactor(light.shadowAttenuation, _ShadowSteps, _ShadowDitherStrength, pattern);
    // Diffuse bands are artistic. Keep physical specular response and HDR light
    // color/distance falloff; the received-shadow style also gates highlights.
    half3 response = brdf.diffuse * diffuse;
    response += brdf.specular * DirectBRDFSpecular(brdf, input.normalWS,
        light.direction, input.viewDirectionWS) * ndotl;
    return response * light.color * (light.distanceAttenuation * shadow);
}

half4 FP_StyledLighting(InputData inputData, SurfaceData surfaceData, FP_LightingPattern pattern)
{
    BRDFData brdf;
    InitializeBRDFData(surfaceData, brdf);
#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdf, debugColor)) return debugColor;
#endif
    BRDFData coat = (BRDFData)0;
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint layers = GetMeshRenderingLayer();
    Light main = GetMainLight(inputData, shadowMask, ao);
    MixRealtimeAndBakedGI(main, inputData.normalWS, inputData.bakedGI);
    LightingData lighting = CreateLightingData(inputData, surfaceData);
    lighting.giColor = GlobalIllumination(brdf, coat, 0, inputData.bakedGI,
        ao.indirectAmbientOcclusion, inputData.positionWS, inputData.normalWS,
        inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(main.layerMask, layers))
#endif
    {
        lighting.mainLightColor = FP_DitherDirectLight(brdf, main, inputData, pattern);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
        {
            lighting.additionalLightsColor += FP_DitherDirectLight(brdf, light, inputData, pattern);
        }
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
        {
            lighting.additionalLightsColor += FP_DitherDirectLight(brdf, light, inputData, pattern);
        }
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    // Vertex lights lack per-fragment direction/shadows; preserve URP fallback.
    lighting.vertexLightingColor = inputData.vertexLighting * brdf.diffuse;
#endif
    return min(CalculateFinalColor(lighting, surfaceData.alpha), HALF_MAX);
}

half4 FP_DitherFragmentPBR(InputData inputData, SurfaceData surfaceData, float2 uv)
{
    bool styleLighting = (_LightingDitherStrength > 0 || _ShadowDitherStrength > 0) && _DitherMaskStrength > 0;
    bool stampAlbedo = _DitherPattern > 6.5 && _DitherStrength > 0 && _DitherMaskStrength > 0;
    FP_LightingPattern pattern = (FP_LightingPattern)0;
    // One stamp lookup per fragment, shared by albedo and every visible light.
    [branch] if (styleLighting || stampAlbedo) pattern = FP_GetLightingPattern(uv);
    float3 albedo = surfaceData.albedo;
    float unused = 0;
    [branch] if (_DitherPattern > 6.5)
    {
        if (stampAlbedo)
            albedo = float3(
                FP_DitherLightFactor(albedo.r, _DitherColorSteps, _DitherStrength, pattern),
                FP_DitherLightFactor(albedo.g, _DitherColorSteps, _DitherStrength, pattern),
                FP_DitherLightFactor(albedo.b, _DitherColorSteps, _DitherStrength, pattern));
    }
    else
        FP_DitherShape_float(surfaceData.albedo, uv, _DitherStrength, _DitherScale,
            _DitherColorSteps, _DitherAmount, _DitherBias, _DitherMaskStrength,
            _DitherPattern, _DitherRotation, _DitherArmRatio, _DitherFiltering, albedo, unused);
    surfaceData.albedo = albedo;
    half4 result = 0;
    if (!styleLighting)
        result = UniversalFragmentPBR(inputData, surfaceData);
    else
        result = FP_StyledLighting(inputData, surfaceData, pattern);
    return result;
}
#endif

