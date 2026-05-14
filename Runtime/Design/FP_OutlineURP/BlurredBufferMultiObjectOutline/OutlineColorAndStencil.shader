Shader "FuzzPhyte/Outline Color And Stencil"
{
    // The _BaseColor variable is visible in the Material's Inspector, as a field 
    // called Base Color. You can use it to select a custom color. This variable
    // has the default value (1, 1, 1, 1).
    Properties
    {
        [MainColor][HDR]_BaseColor("Base Color", Color) = (0, 1, 0, 1)
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [NoScaleOffset]_BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Name "Draw Solid Color"
        
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            ZWrite Off

            Stencil
            {
                Ref 15
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }
            
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FPOutlineMaskTexture);
            SAMPLER(sampler_FPOutlineMaskTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // To make the Unity shader SRP Batcher compatible, declare all
            // properties related to a Material in a a single CBUFFER block with 
            // the name UnityPerMaterial.
            CBUFFER_START(UnityPerMaterial)
                // The following line declares the _BaseColor variable, so that you
                // can use it in the fragment shader.
                half4 _BaseColor;
                half _AlphaCutoff;
            CBUFFER_END

            half4 _FPOutlineColor;
            int _FPOutlineAlphaMode;
            half _FPOutlineAlphaCutoff;
            float4 _FPOutlineMaskTexture_ST;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv * _FPOutlineMaskTexture_ST.xy + _FPOutlineMaskTexture_ST.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_FPOutlineAlphaMode != 0)
                {
                    half alpha = SAMPLE_TEXTURE2D(_FPOutlineMaskTexture, sampler_FPOutlineMaskTexture, IN.uv).a;
                    clip(alpha - _FPOutlineAlphaCutoff);
                }

                half3 color = _FPOutlineColor.a > 0.0 ? _FPOutlineColor.rgb : _BaseColor.rgb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
