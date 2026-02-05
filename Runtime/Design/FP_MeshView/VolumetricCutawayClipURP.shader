Shader "FuzzPhyte/VolumetricCutawayClipURP"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry+1"  // draw after opaques
        }

        Pass
        {
            Name "CutClip"
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
            int    _UseSphere;
            float4 _BaseColor;

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

            half4 frag(Varyings IN) : SV_Target
            {
                bool inside = (_UseSphere == 1) ?
                    InsideSphere(IN.worldPos) :
                    InsideBox(IN.worldPos);

                // If inside volume, discard so it doesn’t draw
                if (inside)
                    discard;

                return _BaseColor;
            }

            ENDHLSL
        }
    }
}
