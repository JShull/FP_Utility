// Copyright (c) 2026 John B. Shull. See LICENSE.md.
// UNITY_SHADER_NO_UPGRADE
#ifndef FUZZPHYTE_DITHER_INCLUDED
#define FUZZPHYTE_DITHER_INCLUDED

// Integer-valued float arithmetic avoids a lookup texture and dynamic array indexing.
// frac wraps negative UVs into the same repeating 4x4 cell as positive UVs.
float FP_Bayer4x4(float2 coordinates)
{
    float2 cell = floor(frac(coordinates * 0.25) * 4.0);
    float2 low = cell - 2.0 * floor(cell * 0.5);
    float2 high = floor(cell * 0.5);
    float lowRank = 2.0 * abs(low.x - low.y) + low.y;
    float highRank = 2.0 * abs(high.x - high.y) + high.y;
    return (4.0 * lowRank + highRank + 0.5) / 16.0;
}

// ColorSteps counts levels including black and white. Strength is the master
// blend; Amount independently blends nearest rounding into ordered dithering.
// Operates on linear albedo, leaving normal/PBR/alpha channels untouched.
void FP_Dither_float(float3 BaseColor, float2 Coordinates, float Strength,
    float PatternScale, float ColorSteps, float Amount, float Bias, float Mask,
    out float3 DitheredColor, out float Threshold)
{
    Threshold = FP_Bayer4x4(Coordinates * max(PatternScale, 1.0));
    float intervals = max(floor(ColorSteps + 0.5), 2.0) - 1.0;
    float rounding = saturate(0.5 + (Threshold - 0.5) * saturate(Amount) + Bias);
    float3 quantized = saturate(floor(saturate(BaseColor) * intervals + rounding) / intervals);
    DitheredColor = lerp(BaseColor, quantized, saturate(Strength) * saturate(Mask));
}

// The supplied graphs use float precision to preserve UV cell placement.
void FP_Dither_half(half3 BaseColor, half2 Coordinates, half Strength,
    half PatternScale, half ColorSteps, half Amount, half Bias, half Mask,
    out half3 DitheredColor, out half Threshold)
{
    float3 color;
    float threshold;
    FP_Dither_float(BaseColor, Coordinates, Strength, PatternScale, ColorSteps,
        Amount, Bias, Mask, color, threshold);
    DitheredColor = (half3)color;
    Threshold = (half)threshold;
}
// Area enclosed by a centered circle clipped to a unit square. This turns
// distance into a uniform threshold distribution, including merged dark dots.
float FP_CircleRank(float2 cell)
{
    float radiusSquared = dot(cell, cell);
    float area = 3.14159265359 * radiusSquared;
    if (radiusSquared > 0.25)
    {
        float segment = radiusSquared * acos(0.5 * rsqrt(radiusSquared))
            - 0.5 * sqrt(radiusSquared - 0.25);
        area -= 4.0 * segment;
    }
    return saturate(area);
}

// Five-point star with inner/outer radius ratio 0.45. Reflect the point into
// one edge sector, then compute the scale at which that star contains it.
float FP_StarRank(float2 cell)
{
    const float2 first = float2(0.809016994375, -0.587785252292);
    const float2 second = float2(-0.809016994375, -0.587785252292);
    cell.x = abs(cell.x);
    cell -= 2.0 * max(dot(first, cell), 0.0) * first;
    cell -= 2.0 * max(dot(second, cell), 0.0) * second;
    float radius = cell.y + abs(cell.x) * ((1.0 - 0.45 * first.x) / (-0.45 * first.y));

    // Exact piecewise-quadratic area of the scaled star clipped to a unit
    // square, rounded to float constants. Reproduce with the Tools~/Dither
    // generate_star_coverage.py helper. No mask texture or per-pixel polygon loop.
    float4 a = max(radius - float4(0.5, 0.525731112119, 0.618033988750, 0.964353572172), 0.0);
    float4 b = max(radius - float4(1.111111111111, 1.168291360265, 1.373408863889, 1.401860823159), 0.0);
    float area = 1.322516817658 * radius * radius
        + dot(a * a, float4(-0.415923491303, -0.847321866205, -0.915441877687, -0.653447175935))
        + dot(b * b, float4(0.331534482919, 0.924759686530, -1.598095309682, 1.851418733705));
    return saturate(area);
}

float FP_ShapeRank(float2 cell, float pattern, float armRatio)
{
    float2 distance = abs(cell);
    float rank = 0.0;
    [branch] if (pattern < 1.5) // Square
    {
        float radius = max(distance.x, distance.y);
        rank = 4.0 * radius * radius;
    }
    else if (pattern < 2.5) // Circle
        rank = FP_CircleRank(cell);
    else if (pattern < 3.5) // Horizontal lines before rotation
        rank = 2.0 * distance.y;
    else if (pattern < 4.5)
    {
        // Union of two bars. Once the arms reach the cell boundary, grow their
        // thickness to reach full coverage without double-counting the center.
        float ratio = clamp(armRatio, 0.1, 1.0);
        float radius = min(max(distance.x, distance.y / ratio),
            max(distance.y, distance.x / ratio));
        float width = ratio * radius;
        rank = radius <= 0.5
            ? 4.0 * radius * radius * (2.0 * ratio - ratio * ratio)
            : 4.0 * width * (1.0 - width);
    }
    else if (pattern < 5.5) // Diamond clipped to a square at dark coverage
    {
        float radius = distance.x + distance.y;
        rank = radius <= 0.5 ? 2.0 * radius * radius
            : 1.0 - 2.0 * (1.0 - radius) * (1.0 - radius);
    }
    else
        rank = FP_StarRank(cell);
    return saturate(rank);
}

// Pattern 0 preserves the original Bayer path and ignores new shape controls.
// 1 = square, 2 = circle, 3 = lines, 4 = plus, 5 = diamond, 6 = star.
// No texture or shader keyword.
// Derivatives make this entry point fragment-only; smoothing affects RGB only.
void FP_DitherShape_float(float3 BaseColor, float2 Coordinates, float Strength,
    float PatternScale, float ColorSteps, float Amount, float Bias, float Mask,
    float Pattern, float Rotation, float ArmRatio, float Filtering,
    out float3 DitheredColor, out float Threshold)
{
    DitheredColor = BaseColor;
    Threshold = 0.5;
    [branch] if (Pattern < 0.5)
    {
        FP_Dither_float(BaseColor, Coordinates, Strength, PatternScale,
            ColorSteps, Amount, Bias, Mask, DitheredColor, Threshold);
        return;
    }

    float sine, cosine;
    sincos(Rotation * 0.01745329252, sine, cosine);
    float2 coordinates = float2(cosine * Coordinates.x - sine * Coordinates.y,
        sine * Coordinates.x + cosine * Coordinates.y) * max(PatternScale, 1.0);
    float2 cell = frac(coordinates) - 0.5;
    Threshold = FP_ShapeRank(cell, Pattern, ArmRatio);
    float intervals = max(floor(ColorSteps + 0.5), 2.0) - 1.0;
    float3 scaled = saturate(BaseColor) * intervals;
    float3 fraction = frac(scaled);
    float rounding = saturate(0.5 + (Threshold - 0.5) * saturate(Amount) + Bias);
    float edge = max(0.5 * fwidth(rounding) * saturate(Filtering), 0.00001);
    float3 upper = smoothstep(1.0 - fraction - edge, 1.0 - fraction + edge, rounding);
    // Exact quantization levels, including black and white, remain exact.
    upper *= step(0.000001, fraction);
    float3 quantized = saturate((floor(scaled) + upper) / intervals);
    float footprint = max(length(ddx(coordinates)), length(ddy(coordinates)));
    // At distance, fade to original albedo rather than an aliased threshold.
    float visibility = 1.0 - saturate(Filtering) * smoothstep(0.25, 0.75, footprint);
    DitheredColor = lerp(BaseColor, quantized,
        saturate(Strength) * saturate(Mask) * visibility);
}

void FP_DitherShape_half(half3 BaseColor, half2 Coordinates, half Strength,
    half PatternScale, half ColorSteps, half Amount, half Bias, half Mask,
    half Pattern, half Rotation, half ArmRatio, half Filtering,
    out half3 DitheredColor, out half Threshold)
{
    float3 color;
    float threshold;
    FP_DitherShape_float(BaseColor, Coordinates, Strength, PatternScale,
        ColorSteps, Amount, Bias, Mask, Pattern, Rotation, ArmRatio, Filtering,
        color, threshold);
    DitheredColor = (half3)color;
    Threshold = (half)threshold;
}
#endif
