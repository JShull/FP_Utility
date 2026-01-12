Shader "FuzzPhyte/WireframeBarycentricURP"
{
    Properties
    {
        _LineWidth ("Line Width (pixels-ish)", Range(0.25, 5.0)) = 1.25
        _Opacity ("Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Wireframe"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            // fwidth requires derivatives; on some platforms Unity auto-enables.
            // WebGL2 + mobile GPUs support it; if you hit issues you can raise target or add pragma require derivatives.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _LineWidth;
            float _Opacity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;   // barycentric in rgb
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 bary       : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.bary = v.color.rgb;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // distance to the nearest edge
                float e = min(i.bary.x, min(i.bary.y, i.bary.z));

                // anti-aliased edge thickness
                float fw = fwidth(e);
                float a = 1.0 - smoothstep(0.0, fw * _LineWidth, e);

                return half4(1,1,1, a * _Opacity);
            }
            ENDHLSL
        }
    }
}
