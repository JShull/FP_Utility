Shader "FuzzPhyte/VolumetricCutawayDepthURP"
{
    
     SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry-1"  // before opaque
        }

        Pass
        {
            Name "DepthOnlyCut"
            ZWrite On
            ColorMask 0       // DO NOT write color
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _VolumeCenter;
            float _SphereRadius;
            float3 _BoxExtents;
            int    _UseSphere;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wpos = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wpos);
                o.worldPos = wpos;
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

            float4 frag(Varyings IN) : SV_Target
            {
                bool inside = (_UseSphere == 1) ?
                    InsideSphere(IN.worldPos) :
                    InsideBox(IN.worldPos);

                // **If inside volume, do NOT write depth → hole**
                if (inside)
                    discard;

                // else write depth
                return 0;
            }
            ENDHLSL
        }
    }
}
