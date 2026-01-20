Shader "FuzzPhyte/GridPlaneURP"
{
    Properties
    {
        [Header(Colors)]
        _MinorColor("Minor Color", Color) = (1,1,1,0.25)
        _MajorColor("Major Color", Color) = (1,1,1,0.45)
        _Opacity("Opacity", Range(0,1)) = 1

        [Header(Grid)]
        _SpacingWorld("Spacing (World Units)", Float) = 0.1
        _MajorEvery("Major Every (Lines)", Int) = 10

        [Header(Thickness (Pixels))]
        _MinorThicknessPx("Minor Thickness (px)", Float) = 1.0
        _MajorThicknessPx("Major Thickness (px)", Float) = 1.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "FPGridPlaneURP"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0; // object-space position for plane-local coords
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MinorColor;
                float4 _MajorColor;
                float  _Opacity;

                float  _SpacingWorld;
                int    _MajorEvery;

                float  _MinorThicknessPx;
                float  _MajorThicknessPx;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            // Returns a smooth line mask for a grid line in "cell space" (coord / spacing).
            // thicknessPx is applied using fwidth to approximate pixel thickness.
            float LineMask(float cellCoord, float thicknessPx)
            {
                // Nearest integer line index in cell space
                float lineIndex = round(cellCoord);

                // Distance to the nearest line in cell units (0 at the line)
                float dist = abs(cellCoord - lineIndex);

                // Anti-alias width in cell units
                float aa = max(fwidth(cellCoord), 1e-6);

                // Convert "pixel thickness" into cell-space half-width
                // (this is an approximation, but stable and readable)
                float halfWidth = thicknessPx * aa;

                // 1 at line center, 0 away from line
                return 1.0 - smoothstep(halfWidth, halfWidth + aa, dist);
            }

            bool IsMajorLine(float lineIndex, int majorEvery)
            {
                // Handle invalid majorEvery safely
                majorEvery = max(1, majorEvery);

                // Because lineIndex is derived from round(), it is effectively an integer.
                // Consider it "major" when it's a multiple of majorEvery.
                float m = fmod(abs(lineIndex), (float)majorEvery);
                return (m < 0.5); // tolerance for float math
            }

            float4 frag(Varyings i) : SV_Target
            {

                // object-to-world basis lengths (handles non-uniform scale)
                float scaleX = length(unity_ObjectToWorld._m00_m10_m20);
                float scaleZ = length(unity_ObjectToWorld._m02_m12_m22);

                float2 p = float2(i.positionOS.x * scaleX, i.positionOS.z * scaleZ);

                float spacing = max(_SpacingWorld, 1e-6);
                float2 cell = p / spacing;

                // Minor lines (X and Z)
                float minorX = LineMask(cell.x, _MinorThicknessPx);
                float minorZ = LineMask(cell.y, _MinorThicknessPx);
                float minorMask = saturate(max(minorX, minorZ));

                // Major lines: detect if the nearest line indices are major
                float ix = round(cell.x);
                float iz = round(cell.y);

                bool majorXOn = (minorX > 0.0) && IsMajorLine(ix, _MajorEvery);
                bool majorZOn = (minorZ > 0.0) && IsMajorLine(iz, _MajorEvery);

                // Compute major masks using the thicker width, but only where indices are major
                float majorX = majorXOn ? LineMask(cell.x, _MajorThicknessPx) : 0.0;
                float majorZ = majorZOn ? LineMask(cell.y, _MajorThicknessPx) : 0.0;
                float majorMask = saturate(max(majorX, majorZ));

                // Combine:
                // - major overrides minor where they overlap
                float4 minorCol = _MinorColor;
                float4 majorCol = _MajorColor;

                // Apply global opacity multiplier
                minorCol.a *= _Opacity;
                majorCol.a *= _Opacity;

                // Composite alpha and color
                float4 col = 0;

                // Start with minor
                col.rgb = minorCol.rgb;
                col.a   = minorCol.a * minorMask;

                // Then overlay major
                float majorA = majorCol.a * majorMask;
                col.rgb = lerp(col.rgb, majorCol.rgb, saturate(majorMask));
                col.a   = saturate(col.a + majorA - col.a * majorA); // standard alpha union

                // Early out to reduce overdraw cost a bit
                clip(col.a - 0.001);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
