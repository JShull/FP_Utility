// Copyright (c) 2026 John B. Shull. See LICENSE.md.
Shader "Hidden/FuzzPhyte/VideoColorResolve"
{
    Properties { _MainTex ("Linear video", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i) : SV_Target
            {
                float4 color=tex2D(_MainTex,i.uv);
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 value=max(0,color.rgb);
                color.rgb=lerp(value*12.92,1.055*pow(value,1/2.4)-.055,step(.0031308,value));
                #endif
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
