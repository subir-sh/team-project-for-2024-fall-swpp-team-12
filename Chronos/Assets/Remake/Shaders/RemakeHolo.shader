// Phantom hologram: translucent fill + bright fresnel rim + moving scanlines.
// Depth pre-pass keeps the silhouette clean (no inner self-overlap at game distance).
Shader "Remake/Holo"
{
    Properties
    {
        _Color ("Fill", Color) = (0.25, 0.85, 1.0, 0.35)
        _RimColor ("Rim", Color) = (0.6, 1.0, 1.0, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.2
        _ScanDensity ("Scan density", Float) = 38
        _ScanSpeed ("Scan speed", Float) = 1.6
        _Flicker ("Flicker", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite On
            ColorMask 0
        }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color, _RimColor;
            float _RimPower, _ScanDensity, _ScanSpeed, _Flicker;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float3 wp : TEXCOORD2; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = normalize(_WorldSpaceCameraPos - o.wp);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float rim = pow(1 - saturate(abs(dot(normalize(i.n), i.v))), _RimPower);
                float scan = 0.5 + 0.5 * sin((i.wp.y * _ScanDensity) - _Time.y * _ScanSpeed * 6.2831);
                scan = smoothstep(0.35, 1.0, scan);
                float flick = 1 - _Flicker * step(0.93, frac(sin(floor(_Time.y * 12) * 12.9898) * 43758.5453));
                float3 col = _Color.rgb * (0.75 + 0.35 * scan) + _RimColor.rgb * rim * 1.4;
                float a = saturate(_Color.a * (0.8 + 0.4 * scan) + rim * _RimColor.a);
                return fixed4(col * flick, a * flick);
            }
            ENDCG
        }
    }
    Fallback Off
}
