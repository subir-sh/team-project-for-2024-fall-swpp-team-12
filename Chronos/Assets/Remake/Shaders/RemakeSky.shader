// Gradient skybox for the remake: the camera mostly looks down into the abyss, so the
// lower hemisphere carries the mood (warm haze at the horizon -> violet -> deep navy).
Shader "Remake/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.10, 0.14, 0.30, 1)
        _Horizon ("Horizon haze", Color) = (0.93, 0.62, 0.47, 1)
        _Mid ("Below-horizon violet", Color) = (0.30, 0.24, 0.45, 1)
        _Nadir ("Nadir", Color) = (0.05, 0.07, 0.16, 1)
        _HazeWidth ("Haze width", Range(0.01, 1)) = 0.22
        _StarDensity ("Star density", Range(0, 1)) = 0.6
        _SunDir ("Sun dir", Vector) = (-0.4, 0.15, 0.9, 0)
        _SunColor ("Sun glow", Color) = (1, 0.75, 0.5, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Zenith, _Horizon, _Mid, _Nadir, _SunColor;
            float _HazeWidth, _StarDensity;
            float4 _SunDir;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col;
                if (y >= 0)
                    col = lerp(_Horizon.rgb, _Zenith.rgb, saturate(pow(y / 0.6, 0.6)));
                else
                {
                    float t = saturate(-y / _HazeWidth);
                    col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0, 1, t));
                    col = lerp(col, _Nadir.rgb, smoothstep(0.0, 1.0, saturate((-y - _HazeWidth) / (1 - _HazeWidth))));
                }
                // faint stars / dust specks everywhere (the void is "space-like")
                float3 g = d * 220;
                float3 cell = floor(g);
                float h = hash(cell);
                float3 off = float3(hash(cell + 3), hash(cell + 5), hash(cell + 11)) * 0.6 + 0.2;
                float r = length(frac(g) - off);                       // round point inside the cell
                float star = step(1 - 0.02 * _StarDensity, h) * smoothstep(0.16, 0.0, r) * (0.3 + 0.7 * hash(cell + 7));
                col += star * 0.45;
                // sun glow
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(s, 64) * 0.8 + pow(s, 6) * 0.15);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
