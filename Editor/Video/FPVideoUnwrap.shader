Shader "Hidden/FuzzPhyte/VideoUnwrap"
{
    Properties { _MainTex("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Projection, _SourceProjection, _Aspect, _HorizontalFov, _VerticalFov;
            float _LensFov, _Blend, _FlipX, _FlipY;
            float _RightLensFov, _DiagnosticMode, _OverlayMix, _DifferenceGain, _DirectionalSeam, _SeamWidth;
            float4 _LeftCenter, _RightCenter, _LensRadius, _RightLensRadius, _SeamNormal;
            float4x4 _ViewRotation, _LeftRotation, _RightRotation;
            sampler2D _SeamFlow;
            float _FlowChartInput, _FlowChartLens, _FlowEnabled, _FlowBand, _FlowVertical, _SeamYaw, _FlowStrength, _FlowThreshold;
            float4 _FlowAtlas;
            float _GradeEnabled;
            float _LinearOutput;
            float _FactoryEnabled;
            float4 _FactoryAI, _FactoryAC, _FactoryAD0, _FactoryAD1, _FactoryAD2, _FactoryAD3;
            float4 _FactoryBI, _FactoryBC, _FactoryBD0, _FactoryBD1, _FactoryBD2, _FactoryBD3;
            float4 _GradeParams, _GradeBalance, _GradeCurve;
            #define PI 3.141592653589793

            float3 decodeDisplay(float3 code)
            {
                return lerp(code/12.92,pow((code+.055)/1.055,2.4),step(.04045,code));
            }
            float3 encodeDisplay(float3 linearValue)
            {
                linearValue=max(0,linearValue);
                return lerp(linearValue*12.92,1.055*pow(linearValue,1/2.4)-.055,step(.0031308,linearValue));
            }
            float3 vividShadows(float3 color, bool vivid, bool shadows)
            {
                color=saturate(color);
                float luminance=dot(color,float3(.2126,.7152,.0722));
                float target=luminance;
                if (shadows) target+=2*target*pow(1-target,6);
                if (vivid)
                {
                    float code=encodeDisplay(target.xxx).x;
                    target=decodeDisplay(lerp(code,smoothstep(0,1,code),.35).xxx).x;
                }
                float maximum=max(color.r,max(color.g,color.b)),minimum=min(color.r,min(color.g,color.b));
                float vibrance=vivid ? 1+.35*(1-(maximum-minimum)/max(maximum,.000001)) : 1;
                float3 chroma=(color-luminance)*(target/max(luminance,.000001))*vibrance;
                // Fit chroma uniformly into SDR gamut instead of clipping RGB independently.
                float high=max(chroma.r,max(chroma.g,chroma.b)),low=min(chroma.r,min(chroma.g,chroma.b));
                float fit=1;
                if (high>0) fit=min(fit,(1-target)/high);
                if (low<0) fit=min(fit,-target/low);
                return saturate(target+chroma*saturate(fit));
            }

            float3 grade(float3 color)
            {
                if (_GradeEnabled < .5) return color;
                #ifdef UNITY_COLORSPACE_GAMMA
                color=decodeDisplay(color);
                #endif
                float temperature=_GradeBalance.x, tint=_GradeBalance.y;
                color*=exp2(_GradeParams.x)*exp2(float3(temperature*.5+tint*.25,-tint*.5,-temperature*.5+tint*.25));
                color=max(0,(color-.18)*_GradeParams.y+.18);
                color=max(0,lerp(dot(color,float3(.2126,.7152,.0722)).xxx,color,_GradeParams.z));
                if (_GradeCurve.x > .5 && _GradeCurve.y > 0 && !(_GradeCurve.x > 2.5 && _GradeCurve.x < 3.5 && _GradeCurve.z == 1))
                {
                    // Creative display-code curves, not camera-profile decoding or gamut transforms.
                    float3 encoded=encodeDisplay(saturate(color));
                    float3 look;
                    if (_GradeCurve.x < 1.5) look=lerp(encoded,smoothstep(0,1,encoded),.5);
                    else if (_GradeCurve.x < 2.5) look=log2(1+112*saturate(color))/log2(113.0);
                    else if (_GradeCurve.x < 3.5) look=pow(max(encoded,0),1/_GradeCurve.z);
                    else look=encodeDisplay(vividShadows(color,_GradeCurve.x<4.5||_GradeCurve.x>5.5,_GradeCurve.x>4.5));
                    color=decodeDisplay(lerp(encoded,look,_GradeCurve.y));
                }
                #ifdef UNITY_COLORSPACE_GAMMA
                color=encodeDisplay(color);
                #endif
                return saturate(color);
            }

            float3 direction(float2 uv)
            {
                float longitude = (uv.x - .5) * 2 * PI;
                float3 ray;
                if (_Projection > 1.5)
                    ray = normalize(float3((uv * 2 - 1) * float2(tan(_HorizontalFov * PI / 360), tan(_HorizontalFov * PI / 360) / _Aspect), 1));
                else if (_Projection > .5)
                    ray = normalize(float3(sin(longitude), (uv.y * 2 - 1) * tan(_VerticalFov * PI / 360), cos(longitude)));
                else
                {
                    float latitude = (uv.y - .5) * PI;
                    ray = float3(sin(longitude) * cos(latitude), sin(latitude), cos(longitude) * cos(latitude));
                }
                return mul((float3x3)_ViewRotation, ray);
            }
            float2 factoryUv(float3 ray, float4 intr, float4 center, float4 d0, float4 d1, float4 d2, float4 d3, out float inside)
            {
                float denominator=ray.z+intr.x;
                float2 m=float2(ray.x,-ray.y)/max(denominator,.000001);
                float r2=dot(m,m),r4=r2*r2;
                float radial=1+r2*(d0.x+r2*(d0.y+r2*(d0.z+r2*(d0.w+r2*d1.x))));
                float tx=d1.y+d1.w*r2,ty=d1.z+d2.x*r2;
                float2 p=m*radial;
                p.x+=(r2+2*m.x*m.x)*tx+2*m.x*m.y*ty+d2.y*r2+d2.w*r4;
                p.y+=(r2+2*m.y*m.y)*ty+2*m.x*m.y*tx+d2.z*r2+d3.x*r4;
                p.x*=intr.w;
                float2 local=float2(p.x*intr.y+center.x,1-(p.y*intr.z+center.y));
                inside=step(.000001,denominator)*step(0,local.x)*step(local.x,1)*step(0,local.y)*step(local.y,1);
                return float2(center.z+local.x*center.w,local.y);
            }
            float4 lens(float3 ray, float2 center, float lensFov, float2 radius, float lensIndex, out float valid)
            {
                float angle = acos(clamp(ray.z, -1, 1));
                float halfFov = lensFov * PI / 360;
                if (_FactoryEnabled>.5 && angle>halfFov) { valid=0; return float4(0,0,0,0); }
                float2 radial = ray.xy / max(length(ray.xy), .000001);
                float2 uv = center + radial * (angle / halfFov) * radius;
                float factoryValid=1;
                if (_FactoryEnabled>.5)
                {
                    if (lensIndex<.5) uv=factoryUv(ray,_FactoryAI,_FactoryAC,_FactoryAD0,_FactoryAD1,_FactoryAD2,_FactoryAD3,factoryValid);
                    else uv=factoryUv(ray,_FactoryBI,_FactoryBC,_FactoryBD0,_FactoryBD1,_FactoryBD2,_FactoryBD3,factoryValid);
                }
                if (_FlipX > .5) uv.x = 1 - uv.x;
                if (_FlipY > .5) uv.y = 1 - uv.y;
                float weight = 1 - smoothstep(halfFov - _Blend * PI / 180, halfFov, angle);
                valid = step(angle, halfFov) * step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                valid*=factoryValid;
                weight *= valid;
                return float4(tex2D(_MainTex, saturate(uv)).rgb, weight);
            }
            float3 chartRay(float2 uv)
            {
                float chart = uv.x < .5 ? 0 : 1;
                float longitude = (_SeamYaw + (chart < .5 ? 90 : -90) + ((uv.x - chart * .5) * 2 - .5) * _FlowBand) * PI / 180;
                float latitude = (uv.y - .5) * _FlowVertical * PI / 180;
                return float3(sin(longitude)*cos(latitude),sin(latitude),cos(longitude)*cos(latitude));
            }
            float chartUv(float3 ray, out float2 uv, out float delta)
            {
                float longitude = atan2(ray.x,ray.z)*180/PI;
                float a = frac((longitude-_SeamYaw-90+180)/360)*360-180;
                float b = frac((longitude-_SeamYaw+90+180)/360)*360-180;
                float chart = abs(a)<abs(b) ? 0 : 1; delta = chart < .5 ? a : b;
                float latitude=asin(clamp(ray.y,-1,1))*180/PI;
                uv=float2(chart*.5+.25+delta/(_FlowBand*2),.5+latitude/_FlowVertical);
                return abs(delta)<_FlowBand*.5 && abs(latitude)<_FlowVertical*.5 ? 1 : 0;
            }
            float4 unwrap(v2f_img i)
            {
                if (_FlowChartInput > .5)
                {
                    float valid; float3 r=chartRay(i.uv);
                    float4 color;
                    if (_FlowChartLens < .5) color=lens(mul((float3x3)_LeftRotation,r),_LeftCenter.xy,_LensFov,_LensRadius.xy,0,valid);
                    else color=lens(mul((float3x3)_RightRotation,r),_RightCenter.xy,_RightLensFov,_RightLensRadius.xy,1,valid);
                    return float4(color.rgb,valid);
                }
                float3 ray = direction(i.uv);
                if (_SourceProjection > .5)
                {
                    float2 uv = float2(frac(atan2(ray.x, ray.z) / (2 * PI) + .5), asin(clamp(ray.y, -1, 1)) / PI + .5);
                    if (_FlipX > .5) uv.x = 1 - uv.x;
                    if (_FlipY > .5) uv.y = 1 - uv.y;
                    return float4(grade(tex2D(_MainTex, uv).rgb), 1);
                }
                float va, vb;
                float4 a = lens(mul((float3x3)_LeftRotation, ray), _LeftCenter.xy, _LensFov, _LensRadius.xy, 0, va);
                float4 b = lens(mul((float3x3)_RightRotation, ray), _RightCenter.xy, _RightLensFov, _RightLensRadius.xy, 1, vb);
                if (_DiagnosticMode > .5 && _DiagnosticMode < 1.5) return float4(a.rgb * va, 1);
                if (_DiagnosticMode > 1.5 && _DiagnosticMode < 2.5) return float4(b.rgb * vb, 1);
                if (_DiagnosticMode > 2.5 && _DiagnosticMode < 3.5)
                    return float4(va * vb > .5 ? lerp(b.rgb, a.rgb, _OverlayMix) : (a.rgb * va + b.rgb * vb) * .15, 1);
                if (_DiagnosticMode > 3.5 && _DiagnosticMode < 4.5)
                    return float4(saturate(abs(a.rgb - b.rgb) * _DifferenceGain) * va * vb, 1);
                if (_DiagnosticMode > 4.5 && _DiagnosticMode < 5.5) return float4(va * float3(0,1,1) + vb * float3(1,0,1), 1);
                if (_DiagnosticMode > 5.5 && _FlowEnabled < .5) return float4(0,0,0,1);
                float2 flowUv=0;float delta=0;
                float inside=_FlowEnabled>.5 ? chartUv(ray,flowUv,delta) : 0;
                float4 field=inside>.5 ? tex2D(_SeamFlow,flowUv) : 0;
                bool accepted=inside>.5 && field.z>=_FlowThreshold && va*vb>.5;
                if (_DiagnosticMode > 5.5 && _DiagnosticMode < 6.5)
                {
                    float2 angular=field.xy*float2(_FlowBand/_FlowAtlas.z,_FlowVertical/_FlowAtlas.y);
                    float hue=atan2(angular.y,angular.x)/(2*PI)+.5;
                    float3 rgb=saturate(abs(frac(hue+float3(0,2.0/3,1.0/3))*6-3)-1);
                    return float4(rgb*saturate(length(angular)/max(_FlowAtlas.w,.001))*field.z,1);
                }
                if (_DiagnosticMode > 6.5 && _DiagnosticMode < 7.5) return float4(field.zzz,1);
                if (accepted)
                {
                    float taper=1-smoothstep(_FlowBand*.35,_FlowBand*.5,abs(delta));
                    float2 warped=flowUv+field.xy/_FlowAtlas.xy*(_FlowStrength*taper);
                    float valid;
                    float4 corrected=lens(mul((float3x3)_RightRotation,chartRay(warped)),_RightCenter.xy,_RightLensFov,_RightLensRadius.xy,1,valid);
                    if (valid>.5) b.rgb=corrected.rgb; else accepted=false;
                }
                if (_DiagnosticMode > 7.5)
                    return float4(inside<.5 || va*vb<.5 ? float3(0,0,0) : accepted ? saturate(abs(a.rgb-b.rgb)*_DifferenceGain) : float3(1,0,1),1);
                if (_DirectionalSeam > .5)
                {
                    float distance = asin(clamp(dot(ray, _SeamNormal.xyz), -1, 1)) * 180 / PI;
                    float t = _SeamWidth < .0001 ? step(0, distance) : smoothstep(-_SeamWidth * .5, _SeamWidth * .5, distance);
                    a.a = t * va; b.a = (1 - t) * vb;
                    if (a.a + b.a < .00001) { a.a = va; b.a = vb; }
                }
                // Rejected matches retain the ordinary seam blend, including its feather width.
                return float4(grade((a.rgb * a.a + b.rgb * b.a) / max(a.a + b.a, .00001)), 1);
            }
            float4 frag(v2f_img i) : SV_Target
            {
                float4 color=unwrap(i);
                #ifdef UNITY_COLORSPACE_GAMMA
                if (_LinearOutput>.5) color.rgb=decodeDisplay(color.rgb);
                #endif
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
