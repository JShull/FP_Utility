Shader "FuzzPhyte/MeasurementLineURP"
{
    Properties
    {
        _WidthWorld("Line Width (world)", Range(0.0005, 0.25)) = 0.01
        _Opacity("Opacity", Range(0,1)) = 1
        _Color("Line Color", Color) = (1,1,0,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "MeasurementLine"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _FPLinePoints; // [0]=A, [1]=B
            float _WidthWorld;
            float _Opacity;
            float4 _Color;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes a)
            {
                Varyings o;

                float3 A = _FPLinePoints[0];
                float3 B = _FPLinePoints[1];

                float3 dir = B - A;
                float len = max(length(dir), 1e-6);
                float3 dirN = dir / len;

                // Camera forward in world (note: view matrix is world->view, so use inverse view to fetch axes)
                float3 camFwd = -UNITY_MATRIX_I_V._m02_m12_m22;

                // Build a “right” vector for the ribbon: perpendicular to both camera forward and line direction
                float3 right = normalize(cross(camFwd, dirN));
                float3 offset = right * (_WidthWorld * 0.5);

                // 6 verts = two triangles
                // corners: (A - off), (A + off), (B + off), (A - off), (B + off), (B - off)
                float3 pWS;
                if      (a.vertexID == 0) pWS = A - offset;
                else if (a.vertexID == 1) pWS = A + offset;
                else if (a.vertexID == 2) pWS = B + offset;
                else if (a.vertexID == 3) pWS = A - offset;
                else if (a.vertexID == 4) pWS = B + offset;
                else                      pWS = B - offset;

                o.positionCS = TransformWorldToHClip(pWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = (half4)_Color;
                c.a *= _Opacity;
                return c;
            }
            ENDHLSL
        }
    }
}
