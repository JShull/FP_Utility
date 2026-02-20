Shader "FuzzPhyte/Dilation"
{
    Properties
    {
        _Thickness("Thickness (px)", Integer) = 2
        _Blur("Blur (px)", Integer) = 2
        _MaxRadius("Max Radius (px)", Integer) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        SAMPLER(sampler_BlitTexture);

        int _Thickness;
        int _Blur;
        int _MaxRadius;
        ENDHLSL

        // PASS 0: Horizontal "distance + color" gather
        // Output:
        //   rgb = nearest active pixel color (in X sweep)
        //   a   = nearest active pixel X distance in pixels, normalized to [0..1] by _MaxRadius
        Pass
        {
            Name "HorizontalDistance"
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag_horizontalDist

            float4 frag_horizontalDist(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                int radius = max(1, _MaxRadius);

                int bestDist = radius + 1;
                float3 bestColor = 0;

                // Look for nearest active pixel horizontally
                [loop]
                for (int x = -radius; x <= radius; x++)
                {
                    float2 uv = i.texcoord + float2(_BlitTexture_TexelSize.x * x, 0.0f);
                    //fix warnings due to sampling path and derivatives from compiler
                     //OLD
                    //float4 buffer = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                    float4 buffer = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_BlitTexture, uv, 0);
                    // Define "active" — adjust threshold if you need
                    if (buffer.a >= 1.0)
                    {
                        int d = abs(x);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestColor = buffer.rgb;

                            // Early out if perfect hit
                            if (bestDist == 0) break;
                        }
                    }
                }

                // If nothing found in radius, mark as far
                bestDist = min(bestDist, radius);

                float xDistN = (float)bestDist / (float)radius; // 0..1
                return float4(bestColor, xDistN);
            }
            ENDHLSL
        }

        // PASS 1: Vertical combine into 2D distance, then apply thickness+blur alpha
        Pass
        {
            Name "VerticalCompose"
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 15
                Comp NotEqual
                Pass Zero
                Fail Zero
                ZFail Zero
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag_verticalCompose

            float SoftBandAlpha(float distPx, float thicknessPx, float blurPx)
            {
                // Fully opaque inside thickness
                if (distPx <= thicknessPx) return 1.0;

                // Hard edge if blur is 0
                if (blurPx <= 0.0) return 0.0;

                // Fade from thickness..thickness+blur
                float t = saturate((distPx - thicknessPx) / blurPx); // 0..1
                // Smooth fade (optional). Replace with (1-t) for linear.
                t = t * t * (3.0 - 2.0 * t);
                return 1.0 - t;
            }

            float4 frag_verticalCompose(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                int radius = max(1, _MaxRadius);
                float thickness = (float)_Thickness;
                float blur = (float)_Blur;

                float bestDist = (float)radius + 1.0;
                float3 bestColor = 0;

                [loop]
                for (int y = -radius; y <= radius; y++)
                {
                    float2 uv = i.texcoord + float2(0.0f, _BlitTexture_TexelSize.y * y);
                     //fix warnings due to sampling path and derivatives from compiler
                     //OLD
                    //float4 buffer = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                    float4 buffer = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_BlitTexture, uv, 0);
                    // buffer.a is normalized xDist from pass 0
                    float xDist = buffer.a * (float)radius;
                    float yDist = (float)abs(y);

                    // Approx true distance
                    float dist = sqrt(xDist * xDist + yDist * yDist);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestColor = buffer.rgb;

                        if (bestDist <= 0.0) break;
                    }
                }

                float a = SoftBandAlpha(bestDist, thickness, blur);

                // If nothing was found inside radius, bestDist will be > radius -> alpha 0.
                // This keeps output clean.
                if (bestDist > (float)radius) a = 0;

                return float4(bestColor, a);
            }
            ENDHLSL
        }
    }
}