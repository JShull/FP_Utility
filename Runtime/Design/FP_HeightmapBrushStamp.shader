Shader "Hidden/FuzzPhyte/HeightmapBrushStamp"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushTex;

            float4 _BrushCenter;
            float4 _TextureSize;
            float _BrushRadius;
            float _BrushSoftness;
            float _BrushStrength;
            float _BrushSetValue;
            float _BrushRotationRad;
            float _BrushMode;
            float _UseBrushTex;
            float _DebugMode;

            v2f_img vert(appdata_img v)
            {
                return vert_img(v);
            }

            float2 RotateUv(float2 uv, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                float2 centered = uv - float2(0.5, 0.5);
                float2 rotated = float2(
                    (centered.x * c) - (centered.y * s),
                    (centered.x * s) + (centered.y * c));
                return saturate(rotated + float2(0.5, 0.5));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv);
                float baseValue = saturate(src.r);

                float2 brushCenter = _BrushCenter.xy;
                float2 textureSize = max(_TextureSize.xy, float2(1.0, 1.0));
                float2 texelDelta = (i.uv - brushCenter) * textureSize;
                float distancePx = length(texelDelta);

                if (distancePx > _BrushRadius)
                {
                    return src;
                }

                float hardRadius = max(0.001, _BrushRadius * (1.0 - saturate(_BrushSoftness)));
                float radialFalloff = distancePx <= hardRadius
                    ? 1.0
                    : 1.0 - saturate((distancePx - hardRadius) / max(0.0001, (_BrushRadius - hardRadius)));

                float maskInfluence = radialFalloff;
                if (_UseBrushTex > 0.5)
                {
                    float2 localUv = float2(
                        (texelDelta.x / (_BrushRadius * 2.0)) + 0.5,
                        (texelDelta.y / (_BrushRadius * 2.0)) + 0.5);
                    float2 rotatedUv = RotateUv(localUv, _BrushRotationRad);
                    fixed4 brushSample = tex2D(_BrushTex, rotatedUv);
                    maskInfluence *= saturate(brushSample.a * dot(brushSample.rgb, float3(0.299, 0.587, 0.114)));
                }

                float influence = saturate(maskInfluence * _BrushStrength);

                if (_DebugMode > 1.5 && _DebugMode < 2.5)
                {
                    return src;
                }

                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                {
                    return fixed4(maskInfluence, maskInfluence, maskInfluence, 1.0);
                }

                if (_DebugMode > 3.5 && _DebugMode < 4.5)
                {
                    return fixed4(influence, influence, influence, 1.0);
                }

                float result = baseValue;

                if (_BrushMode > 0.5 && _BrushMode < 1.5)
                {
                    result = lerp(baseValue, 0.0, influence);
                }
                else if (_BrushMode > 1.5)
                {
                    result = lerp(baseValue, saturate(_BrushSetValue), influence);
                }
                else
                {
                    result = lerp(baseValue, 1.0, influence);
                }

                return fixed4(result, result, result, src.a);
            }
            ENDHLSL
        }
    }
}
