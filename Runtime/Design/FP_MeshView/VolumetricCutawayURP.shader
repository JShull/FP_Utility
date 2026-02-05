Shader "FuzzPhyte/VolumetricCutawayReveal"
{
    Properties
    {
        _RevealColor("Reveal Color", Color) = (1,1,0,0.6)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "RevealPass"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _VolumeCenter;
            float _SphereRadius;
            float3 _BoxExtents;
            int _UseSphere;

            float4 _RevealColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;

                // compute world position
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);

                o.positionCS = TransformWorldToHClip(worldPos);
                o.worldPos   = worldPos;

                return o;
            }

            bool InsideSphere(float3 worldPos)
            {
                return distance(worldPos, _VolumeCenter) < _SphereRadius;
            }

            bool InsideBox(float3 worldPos)
            {
                float3 local = abs(worldPos - _VolumeCenter);
                return all(local < _BoxExtents);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                bool inside =
                    (_UseSphere == 1) ? InsideSphere(IN.worldPos)
                                      : InsideBox(IN.worldPos);

                // If outside, don’t reveal
                if (!inside)
                    discard;

                half4 c = _RevealColor;
                return c;
            }

            ENDHLSL
        }
    }
}
