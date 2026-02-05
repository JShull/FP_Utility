Shader "FuzzPhyte/CutawayWallURP"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _VolumeCenter;
            float _SphereRadius;
            float3 _BoxExtents;
            int _UseSphere;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wpos = TransformObjectToWorld(v.positionOS.xyz);

                o.positionCS = TransformWorldToHClip(wpos);
                o.worldPos = wpos;
                o.uv = v.uv;
                return o;
            }

            bool InsideSphere(float3 w)
            {
                return distance(w, _VolumeCenter) < _SphereRadius;
            }

            bool InsideBox(float3 w)
            {
                float3 d = abs(w - _VolumeCenter);
                return (d.x < _BoxExtents.x &&
                        d.y < _BoxExtents.y &&
                        d.z < _BoxExtents.z);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                bool inside =
                    (_UseSphere == 1) ? InsideSphere(IN.worldPos)
                                      : InsideBox(IN.worldPos);

                // CUTAWAY
                if (inside)
                    discard;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return tex * _Color;
            }

            ENDHLSL
        }
    }
}
