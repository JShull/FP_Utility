Shader "FuzzPhyte/Fast Mesh Outline"
{
    Properties
    {
        [MainColor][HDR]_BaseColor("Base Color", Color) = (0, 1, 1, 1)
        _OutlineWidth("Outline Width", Float) = 0.01
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [NoScaleOffset]_BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FPOutlineMaskTexture);
            SAMPLER(sampler_FPOutlineMaskTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _OutlineWidth;
                half _AlphaCutoff;
            CBUFFER_END

            half4 _FPOutlineColor;
            float _FPFastOutlineWidth;
            int _FPOutlineAlphaMode;
            half _FPOutlineAlphaCutoff;
            float4 _FPOutlineMaskTexture_ST;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float outlineWidth = _FPFastOutlineWidth > 0.0 ? _FPFastOutlineWidth : _OutlineWidth;
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz) + normalWS * outlineWidth;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.uv * _FPOutlineMaskTexture_ST.xy + _FPOutlineMaskTexture_ST.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                if (_FPOutlineAlphaMode != 0)
                {
                    half alpha = SAMPLE_TEXTURE2D(_FPOutlineMaskTexture, sampler_FPOutlineMaskTexture, IN.uv).a;
                    clip(alpha - _FPOutlineAlphaCutoff);
                }

                half4 color = _FPOutlineColor.a > 0.0 ? _FPOutlineColor : _BaseColor;
                return color;
            }
            ENDHLSL
        }
    }
}
