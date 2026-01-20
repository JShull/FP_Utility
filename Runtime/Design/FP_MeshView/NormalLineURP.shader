Shader "FuzzPhyte/NormalLineURP"
{
    Properties
    {
        _Color("Color", Color) = (0.2, 0.9, 1.0, 1.0)
        _Opacity("Opacity", Range(0,1)) = 1
        [Toggle(_ALWAYS_ON_TOP)] _AlwaysOnTop("Always On Top", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "NormalsLines"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALWAYS_ON_TOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Matches your cache: mat.SetBuffer("_Lines", _normalLineBuffer);
            StructuredBuffer<float3> _Lines;

            // Matches your cache: mat.SetMatrix("_LocalToWorld", localToWorld);
            float4x4 _LocalToWorld;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Opacity;
            CBUFFER_END

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

                float3 pOS = _Lines[a.vertexID];
                float3 pWS = mul(_LocalToWorld, float4(pOS, 1.0)).xyz;

                o.positionCS = TransformWorldToHClip(pWS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 c = _Color;
                c.a *= _Opacity;
                return c;
            }
            ENDHLSL
        }

        // Optional "always on top" override pass
        // (enabled by keyword to avoid needing two different shaders)
        Pass
        {
            Name "NormalsLines_AlwaysOnTop"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALWAYS_ON_TOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _Lines;
            float4x4 _LocalToWorld;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Opacity;
            CBUFFER_END

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes a)
            {
                Varyings o;
                float3 pOS = _Lines[a.vertexID];
                float3 pWS = mul(_LocalToWorld, float4(pOS, 1.0)).xyz;
                o.positionCS = TransformWorldToHClip(pWS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 c = _Color;
                c.a *= _Opacity;
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
