// Copyright (c) 2026 John B. Shull. See LICENSE.md.
Shader "Hidden/FuzzPhyte/MeshSurfacePreview"
{
    Properties
    {
        _Color ("Color", Color) = (.55,.68,.76,1)
        _Faceted ("Faceted", Float) = 0
        _Flat ("Flat", Float) = 0
        _SrcBlend ("Source blend", Float) = 5
        _DstBlend ("Destination blend", Float) = 10
        _Cull ("Cull", Float) = 0
        _ZWrite ("Depth write", Float) = 1
        _Offset ("Depth offset", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Cull [_Cull] ZWrite [_ZWrite] ZTest LEqual Offset [_Offset], [_Offset]
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color;
            float _Faceted, _Flat;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float3 viewPosition : TEXCOORD0; float3 normal : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.viewPosition = mul(UNITY_MATRIX_MV, v.vertex).xyz;
                o.normal = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(v.normal));
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                if (_Flat > .5) return _Color;
                float3 face = cross(ddx(i.viewPosition), ddy(i.viewPosition));
                float3 n = (_Faceted > .5 || dot(i.normal, i.normal) < 1e-8) ? face : i.normal;
                n *= rsqrt(max(dot(n, n), 1e-12));
                if (n.z < 0) n = -n;
                float key = saturate(dot(n, normalize(float3(-.45,.65,.75))));
                float fill = saturate(dot(n, normalize(float3(.7,-.25,.5))));
                return float4(_Color.rgb * (.22 + .65 * key + .13 * fill), _Color.a);
            }
            ENDCG
        }
    }
}
