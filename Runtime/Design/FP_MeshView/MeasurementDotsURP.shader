Shader "FuzzPhyte/MeasurementDotsURP"
{
    Properties
    {
        _Size("Dot Size (world units)", Range(0.0005, 0.10)) = 0.01
        _Opacity("Opacity", Range(0,1)) = 1
        _Color("Dot Color", Color) = (0,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "MeasurementDots"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _FPPoints;
            float _Size;
            float _Opacity;
            float4 _Color;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes a)
            {
                Varyings o;

                uint baseVert = a.vertexID / 6;   // point index
                uint corner   = a.vertexID % 6;   // quad corner index

                float3 pWS = _FPPoints[baseVert];

                // Camera right/up in world space (matches your VertexDots approach)
                float3 rightWS = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 upWS    = UNITY_MATRIX_I_V._m01_m11_m21;

                float2 c;
                if      (corner == 0) c = float2(-1, -1);
                else if (corner == 1) c = float2(-1,  1);
                else if (corner == 2) c = float2( 1,  1);
                else if (corner == 3) c = float2(-1, -1);
                else if (corner == 4) c = float2( 1,  1);
                else                  c = float2( 1, -1);

                float3 posWS = pWS + (rightWS * c.x + upWS * c.y) * _Size;
                o.positionCS = TransformWorldToHClip(posWS);
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
