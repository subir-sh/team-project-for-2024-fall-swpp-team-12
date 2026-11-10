// Cel shading for characters and the level kit: banded diffuse with soft band edges,
// coloured shadow tint, rim light and a small specular highlight. Receives/casts shadows.
// Optional world-space triplanar material detail (_DetailTex, greyscale) so floors and walls
// read as a material (iron plate, sandstone, concrete, patina...) instead of flat colour.
Shader "Remake/Toon"
{
    Properties
    {
        _Color ("Base", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade tint", Color) = (0.62, 0.6, 0.78, 1)
        _Bands ("Band softness", Range(0.001, 0.3)) = 0.06
        _Mid ("Mid band", Range(0, 1)) = 0.35
        _RimColor ("Rim", Color) = (1, 1, 1, 0.35)
        _RimPower ("Rim power", Range(1, 8)) = 3.5
        _Spec ("Specular", Range(0, 1)) = 0.25
        _Gloss ("Gloss", Range(8, 256)) = 64
        [HDR] _Emission ("Emission", Color) = (0, 0, 0, 0)
        _DetailTex ("Material detail (grey)", 2D) = "white" {}
        _DetailScale ("Detail scale (per metre)", Float) = 0.5
        _DetailStrength ("Detail strength", Range(0, 1)) = 0
        _OverlayTex ("Overlay mask (grey)", 2D) = "black" {}
        _OverlayColor ("Overlay colour (a = amount)", Color) = (0, 0, 0, 0)
        _OverlayScale ("Overlay scale", Float) = 0.25
        _OverlayUp ("Overlay on up faces only", Range(0, 1)) = 0
        _SinkFade ("Darken below y=0 (per metre)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color, _ShadeColor, _RimColor, _Emission;
        half _Bands, _Mid, _RimPower, _Spec, _Gloss, _DetailScale, _DetailStrength;
        sampler2D _DetailTex, _OverlayTex;
        fixed4 _OverlayColor; half _OverlayScale, _OverlayUp, _SinkFade;

        struct Input { float3 viewDir; float3 worldPos; float3 worldNormal; };
        struct SurfaceOutputToon { fixed3 Albedo; fixed3 Normal; fixed3 Emission; fixed Alpha; half Rim; };

        half4 LightingToon (SurfaceOutputToon s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir) * 0.5 + 0.5;
            half lit = ndl * saturate(atten * 1.4);
            half b1 = smoothstep(_Mid - _Bands, _Mid + _Bands, lit);
            half b2 = smoothstep(0.72 - _Bands, 0.72 + _Bands, lit);
            half3 shade = s.Albedo * _ShadeColor.rgb;
            half3 diff = lerp(shade, lerp(s.Albedo * 0.88, s.Albedo, b2), b1);
        #ifdef UNITY_PASS_FORWARDADD
            diff *= saturate(atten * 2);   // point/spot lights must not add their shade term outside their reach
        #endif
            half3 h = normalize(lightDir + viewDir);
            half spec = smoothstep(0.5, 0.55, pow(saturate(dot(s.Normal, h)), _Gloss)) * _Spec * b1;
            half3 col = diff * _LightColor0.rgb + spec * _LightColor0.rgb;
            col += _RimColor.rgb * _RimColor.a * s.Rim * b1;
            return half4(col, 1);
        }

        half Tri(sampler2D t, float3 p, float3 n, half sc)
        {
            float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
            half x = tex2D(t, p.zy * sc).r;
            half y = tex2D(t, p.xz * sc).r;
            half z = tex2D(t, p.xy * sc).r;
            return x * w.x + y * w.y + z * w.z;
        }

        void surf (Input IN, inout SurfaceOutputToon o)
        {
            float3 n = normalize(IN.worldNormal);
            half d = Tri(_DetailTex, IN.worldPos, n, _DetailScale);
            o.Albedo = _Color.rgb * lerp(1, d * 1.15, _DetailStrength);
            if (_OverlayColor.a > 0.001)
            {
                half m = Tri(_OverlayTex, IN.worldPos + 7.3, n, _OverlayScale);
                m *= lerp(1, smoothstep(0.35, 0.8, n.y), _OverlayUp);
                o.Albedo = lerp(o.Albedo, _OverlayColor.rgb * (0.75 + 0.4 * d), saturate(m) * _OverlayColor.a);
            }
            half sink = lerp(1, 0.06, saturate(-IN.worldPos.y * _SinkFade));
            o.Albedo *= sink;
            o.Emission = _Emission.rgb;
            o.Rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower) * sink;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
