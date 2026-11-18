// Mars sky: butterscotch horizon, dusty rose-brown zenith, the bluish halo Mars has around the sun,
// and its two small moons Phobos and Deimos (lit from the sun's side).
Shader "MarsGo/MarsSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.58, 0.36, 0.28, 1)
        _Horizon ("Horizon", Color) = (0.93, 0.70, 0.52, 1)
        _Ground ("Below Horizon", Color) = (0.55, 0.30, 0.20, 1)
        _Exponent ("Gradient Exponent", Float) = 0.55
        _SunDir ("Sun Direction", Vector) = (0.4, 0.35, 0.85, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.97, 0.92, 1)
        _SunSize ("Sun Size (cos)", Float) = 0.9993
        _Halo ("Sun Halo (blue)", Color) = (0.62, 0.72, 0.9, 1)
        _HaloPower ("Halo Tightness", Float) = 40
        _HaloStrength ("Halo Strength", Float) = 0.55
        _Moon1Dir ("Phobos Direction", Vector) = (0.35, 0.42, 0.84, 0)
        _Moon1Size ("Phobos Radius (rad)", Float) = 0.022
        _Moon2Dir ("Deimos Direction", Vector) = (-0.15, 0.55, 0.82, 0)
        _Moon2Size ("Deimos Radius (rad)", Float) = 0.011
        _MoonLit ("Moon Lit Colour", Color) = (0.86, 0.80, 0.74, 1)
        _MoonDark ("Moon Dark Colour", Color) = (0.55, 0.40, 0.33, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Horizon, _Ground, _SunColor, _Halo, _MoonLit, _MoonDark;
            float _Exponent, _SunSize, _HaloPower, _HaloStrength, _Moon1Size, _Moon2Size;
            float4 _SunDir, _Moon1Dir, _Moon2Dir;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            // Lumpy little moon: disc with a wobbly edge, shaded toward the sun, a few dark craters.
            float3 Moon(float3 d, float3 center, float radius, float3 sun, float3 col, float seed)
            {
                center = normalize(center);
                float3 right = normalize(cross(float3(0, 1, 0), center));
                float3 up = cross(center, right);
                float2 p = float2(dot(d - center, right), dot(d - center, up)) / radius;
                float ang = atan2(p.y, p.x);
                float edge = 1.0 + 0.12 * sin(ang * 3.0 + seed) + 0.06 * sin(ang * 5.0 + seed * 2.0);
                float r = length(p) / edge;
                if (r > 1.0 || dot(d, center) < 0) return col;
                float2 sun2 = normalize(float2(dot(sun, right), dot(sun, up)) + 1e-4);
                float3 n = float3(p / edge, sqrt(saturate(1.0 - r * r)));
                float light = saturate(dot(n, normalize(float3(sun2, 0.6))));
                float3 c = lerp(_MoonDark.rgb, _MoonLit.rgb, light);
                float crater = smoothstep(0.25, 0.18, length(p - float2(0.3, -0.2)))
                             + smoothstep(0.18, 0.12, length(p - float2(-0.35, 0.25)));
                c *= 1.0 - 0.25 * saturate(crater);
                float aa = smoothstep(1.0, 0.92, r);
                return lerp(col, c, aa);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = h >= 0
                    ? lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(h), _Exponent))
                    : lerp(_Horizon.rgb, _Ground.rgb, saturate(-h * 5));
                float3 sun = normalize(_SunDir.xyz);
                float c = dot(d, sun);
                col = lerp(col, _Halo.rgb, pow(saturate(c), _HaloPower) * _HaloStrength);
                col = lerp(col, _SunColor.rgb * 2.5, smoothstep(_SunSize, _SunSize + 0.0003, c));
                col = Moon(d, _Moon1Dir.xyz, _Moon1Size, sun, col, 1.7);
                col = Moon(d, _Moon2Dir.xyz, _Moon2Size, sun, col, 4.1);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
