// The phantom: "the heat haze that came from the past" (아지랑이). A warm, pale, translucent
// afterimage whose surface shimmers (vertex wobble + drifting bands), brighter at the edge.
// Depth pre-pass keeps one clean silhouette. _Alpha drives the trail echoes (instanced).
Shader "Remake/Afterimage"
{
    Properties
    {
        _Color ("Fill", Color) = (1.0, 0.9, 0.72, 0.38)
        _EdgeColor ("Edge", Color) = (1.0, 0.97, 0.9, 1)
        _EdgePower ("Edge power", Range(0.5, 6)) = 1.6
        _Wobble ("Shimmer amount", Range(0, 0.05)) = 0.012
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        half _Wobble;
        float4 Shimmer(float4 v, float3 n)
        {
            float3 wp = mul(unity_ObjectToWorld, v).xyz;
            float w = sin(wp.y * 9.0 + _Time.y * 5.3) * 0.6 + sin(wp.x * 7.0 - _Time.y * 3.7) * 0.4;
            v.xyz += n * w * _Wobble;
            return v;
        }
        ENDCG
        Pass
        {
            ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp
            float4 vp(float4 v : POSITION, float3 n : NORMAL) : SV_POSITION { return UnityObjectToClipPos(Shimmer(v, n)); }
            fixed4 fp() : SV_Target { return 0; }
            ENDCG
        }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            fixed4 _Color, _EdgeColor;
            half _EdgePower;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half, _Alpha)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float4 v = Shimmer(i.vertex, i.normal);
                o.pos = UnityObjectToClipPos(v);
                o.wp = mul(unity_ObjectToWorld, v).xyz;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(_WorldSpaceCameraPos - o.wp);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half a = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                half edge = pow(1 - saturate(abs(dot(normalize(i.n), i.v))), _EdgePower);
                half band = 0.5 + 0.5 * sin(i.wp.y * 22.0 - _Time.y * 4.0);       // drifting heat bands
                half3 col = lerp(_Color.rgb, _EdgeColor.rgb, edge) * (0.92 + 0.12 * band);
                half alpha = saturate(_Color.a * (0.8 + 0.35 * band) + edge * 0.5) * a;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
