Shader "FuzzPhyte/Video/360 Unlit URP"
{
    Properties
    {
        [MainTexture] _BaseMap("Video Texture", 2D) = "black" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Exposure("Exposure", Range(0,8)) = 1
        _RotationDegrees("Yaw Rotation", Range(-180,180)) = 0
        _FlipX("Flip X", Float) = 0
        _FlipY("Flip Y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _Exposure;
                float _RotationDegrees;
                float _FlipX;
                float _FlipY;
            CBUFFER_END

            float2 RotateUV(float2 uv, float rotationDegrees)
            {
                const float degreesToRadians = 0.017453292519943295;
                float rotationRadians = rotationDegrees * degreesToRadians;
                float sine = sin(rotationRadians);
                float cosine = cos(rotationRadians);

                float2 centered = uv - 0.5;
                float2 rotated;
                rotated.x = centered.x * cosine - centered.y * sine;
                rotated.y = centered.x * sine + centered.y * cosine;
                return rotated + 0.5;
            }

            float2 ApplyVideoUVAdjustments(float2 uv)
            {
                float2 adjusted = TRANSFORM_TEX(uv, _BaseMap);
                adjusted = RotateUV(adjusted, _RotationDegrees);

                if (_FlipX > 0.5)
                {
                    adjusted.x = 1.0 - adjusted.x;
                }

                if (_FlipY > 0.5)
                {
                    adjusted.y = 1.0 - adjusted.y;
                }

                return adjusted;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = ApplyVideoUVAdjustments(input.uv);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 video = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                video.rgb *= (_Tint.rgb * _Exposure);
                video.a *= _Tint.a;
                return video;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
