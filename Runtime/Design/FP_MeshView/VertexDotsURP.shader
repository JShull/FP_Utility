Shader "FuzzPhyte/VertexDotsURP"
{
    Properties
    {
        _Size("Dot Size (world units)", Range(0.0005, 0.10)) = 0.01
        _Opacity("Opacity", Range(0,1)) = 1
        _Color("Dot Color",Color) = (1,1,1,1)
        _MinSize("Min Size", Range(0.0001, 0.01)) = 0.001
        _MaxSize("Max Size", Range(0.01, 0.2)) = 0.05

    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "VertexDots"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //target 2.0
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _FPVertices;
            //new 
             CBUFFER_START(UnityPerMaterial)
                float _Size;
                float _Opacity;
                float4 _Color;
                float _MinSize;
                float _MaxSize;
            CBUFFER_END

            //float4x4 _LocalToWorld;
            //float _Size;
           // float _Opacity;
            //float4 _Color;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes a)
            {
                Varyings o;

                uint baseVert = a.vertexID / 6;
                uint corner   = a.vertexID % 6;

                float3 pOS = _FPVertices[baseVert];
                
                //float3 pWS = mul(_LocalToWorld, float4(pOS, 1)).xyz;

                float3 pWS = TransformObjectToWorld(pOS);
                // Camera right/up in world space
                float3 rightWS = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 upWS    = UNITY_MATRIX_I_V._m01_m11_m21;

                // 6 verts = 2 triangles forming a quad
                float2 c;
                if      (corner == 0) c = float2(-1, -1);
                else if (corner == 1) c = float2(-1,  1);
                else if (corner == 2) c = float2( 1,  1);
                else if (corner == 3) c = float2(-1, -1);
                else if (corner == 4) c = float2( 1,  1);
                else                  c = float2( 1, -1);

                float distToCam = distance(_WorldSpaceCameraPos,pWS);
                float sizeWS = clamp(_Size * distToCam, _MinSize, _MaxSize);
                //float3 posWS = pWS + (rightWS * c.x + upWS * c.y) * _Size;
                float3 posWS = pWS + (rightWS * c.x + upWS * c.y) * sizeWS;
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
