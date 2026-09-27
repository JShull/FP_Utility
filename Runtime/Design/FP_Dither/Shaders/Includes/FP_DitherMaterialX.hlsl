// Copyright (c) 2026 John B. Shull. See LICENSE.md.
// Candidate for PolySpatial 3.1 Custom Function -> MaterialX conversion.
// Float-only assignment expressions: no includes, globals, arrays, loops,
// statement branches, return statements or URP lighting APIs.
// Keep all expressions finite even when another pattern is selected.
// MaterialX may evaluate unselected expressions; device cost is unmeasured.
void FP_DitherMaterialX_float(float3 BaseColor, float2 Coordinates, float Strength,
    float PatternScale, float ColorSteps, float Amount, float Bias, float Mask,
    float Pattern, float Rotation, float ArmRatio, float Filtering,
    out float3 DitheredColor, out float Threshold)
{
    float scale = max(PatternScale, 1.0);
    float2 bayerCell = floor(frac(Coordinates * scale * 0.25) * 4.0);
    float2 low = bayerCell - 2.0 * floor(bayerCell * 0.5);
    float2 high = floor(bayerCell * 0.5);
    float lowRank = 2.0 * abs(low.x - low.y) + low.y;
    float highRank = 2.0 * abs(high.x - high.y) + high.y;
    float bayer = (4.0 * lowRank + highRank + 0.5) / 16.0;

    float angle = Rotation * 0.01745329252;
    float sine = sin(angle);
    float cosine = cos(angle);
    float2 coords = float2(cosine * Coordinates.x - sine * Coordinates.y,
        sine * Coordinates.x + cosine * Coordinates.y) * scale;
    float2 cell = frac(coords) - 0.5;
    float2 distance = abs(cell);
    float squareRadius = max(distance.x, distance.y);
    float squareRank = 4.0 * squareRadius * squareRadius;
    float radiusSquared = dot(cell, cell);
    float segment = radiusSquared * acos(clamp(0.5 * rsqrt(max(radiusSquared, 0.25)), 0.0, 1.0))
        - 0.5 * sqrt(max(radiusSquared - 0.25, 0.0));
    float circleRank = saturate(3.14159265359 * radiusSquared - 4.0 * segment);
    float lineRank = 2.0 * distance.y;
    float ratio = clamp(ArmRatio, 0.1, 1.0);
    float plusRadius = min(max(distance.x, distance.y / ratio), max(distance.y, distance.x / ratio));
    float width = ratio * plusRadius;
    float plusRank = plusRadius <= 0.5
        ? 4.0 * plusRadius * plusRadius * (2.0 * ratio - ratio * ratio)
        : 4.0 * width * (1.0 - width);
    float diamondRadius = distance.x + distance.y;
    float diamondRank = diamondRadius <= 0.5 ? 2.0 * diamondRadius * diamondRadius
        : 1.0 - 2.0 * (1.0 - diamondRadius) * (1.0 - diamondRadius);

    float2 first = float2(0.809016994375, -0.587785252292);
    float2 second = float2(-0.809016994375, -0.587785252292);
    float2 starCell = float2(abs(cell.x), cell.y);
    starCell = starCell - 2.0 * max(dot(first, starCell), 0.0) * first;
    starCell = starCell - 2.0 * max(dot(second, starCell), 0.0) * second;
    float starRadius = starCell.y + abs(starCell.x) * ((1.0 - 0.45 * first.x) / (-0.45 * first.y));
    float4 a = max(starRadius - float4(0.5, 0.525731112119, 0.618033988750, 0.964353572172), 0.0);
    float4 b = max(starRadius - float4(1.111111111111, 1.168291360265, 1.373408863889, 1.401860823159), 0.0);
    float starRank = saturate(1.322516817658 * starRadius * starRadius
        + dot(a * a, float4(-0.415923491303, -0.847321866205, -0.915441877687, -0.653447175935))
        + dot(b * b, float4(0.331534482919, 0.924759686530, -1.598095309682, 1.851418733705)));

    Threshold = saturate(Pattern < 0.5 ? bayer : Pattern < 1.5 ? squareRank
        : Pattern < 2.5 ? circleRank : Pattern < 3.5 ? lineRank
        : Pattern < 4.5 ? plusRank : Pattern < 5.5 ? diamondRank : starRank);
    float intervals = max(floor(ColorSteps + 0.5), 2.0) - 1.0;
    float rounding = saturate(0.5 + (Threshold - 0.5) * saturate(Amount) + Bias);
    float3 scaled = saturate(BaseColor) * intervals;
    float3 bayerColor = saturate(floor(scaled + rounding) / intervals);
    float3 fraction = frac(scaled);
    float edge = max(0.5 * fwidth(rounding) * saturate(Filtering), 0.00001);
    float3 upper = smoothstep(1.0 - fraction - edge, 1.0 - fraction + edge, rounding);
    upper = upper * step(0.000001, fraction);
    float3 shapeColor = saturate((floor(scaled) + upper) / intervals);
    float footprint = max(length(ddx(coords)), length(ddy(coords)));
    float visibility = 1.0 - saturate(Filtering) * smoothstep(0.25, 0.75, footprint);
    float3 quantized = Pattern < 0.5 ? bayerColor : shapeColor;
    float blend = saturate(Strength) * saturate(Mask) * (Pattern < 0.5 ? 1.0 : visibility);
    DitheredColor = lerp(BaseColor, quantized, blend);
}
