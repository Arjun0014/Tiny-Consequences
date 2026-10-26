// TC2 puffs (D2-029): soft camera-facing sprites for steam, smoke, ash columns and glow. A billboard needs no rotation,
// so the instance matrix carries the puff's data instead: translation = centre, m00 = size (m), m11 = opacity,
// m22 = ash (0 white steam .. 1 dark ash), m01 = glow (orange emission), m02 = seed (shape variety), m10 = sun share.
// (session 27) m21 = 1: a flame - a tongue of fire standing up from the ground (stretched along the planet's up as the camera sees it), its
// heat m12 (0 a dying flame, red and low; 1 a roaring one, its heart white-yellow), flickering and licking upward with the time.
// Instanced with Graphics.RenderMeshInstanced (culling bounds are set by the caller).
Shader "TC2/Puff"
{
    Properties
    {
        _Steam ("Steam", Color) = (0.93, 0.94, 0.96, 1)
        _Ash ("Ash", Color) = (0.27, 0.25, 0.24, 1)
        _Glow ("Glow", Color) = (1.0, 0.45, 0.12, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Steam;
                half4 _Ash;
                half4 _Glow;
            CBUFFER_END
            float4 _TC2FogColor;
            float _TC2FogDensity;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 data : TEXCOORD2;   // opacity, ash, glow, seed
                float2 flame : TEXCOORD6;  // (session 27) 1 a flame, its heat
                float3 side : TEXCOORD3;   // camera right (for the light's side)
                float3 upv : TEXCOORD4;    // camera up
                float sun : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 m = UNITY_MATRIX_M;
                float3 centre = float3(m._m03, m._m13, m._m23);
                float size = m._m00;
                float seed = m._m02;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                // each puff turned by its seed so they do not repeat
                float a = seed * 6.2831853;
                float2 q = v.positionOS.xy;
                float2 rq = float2(q.x * cos(a) - q.y * sin(a), q.x * sin(a) + q.y * cos(a));
                float3 pw = centre + (right * rq.x + up * rq.y) * size;
                o.flame = float2(m._m21, m._m12);
                if (m._m21 > 0.5)
                {
                    // a flame stands on the ground: its quad upright along the planet's up, facing the camera about that axis; the centre is its foot
                    float3 gu = normalize(centre);
                    float3 toCam = normalize(_WorldSpaceCameraPos - centre);
                    float3 side = cross(gu, toCam);
                    side = dot(side, side) > 1e-6 ? normalize(side) : right;
                    pw = centre + side * (q.x * size * 0.55) + gu * ((q.y + 0.5) * size);
                }
                o.positionWS = pw;
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = v.positionOS.xy * 2.0;      // -1..1
                o.data = float4(m._m11, m._m22, m._m01, seed);
                o.side = right; o.upv = up;
                o.sun = m._m10;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            half4 Flame(Varyings i)
            {
                // the tongue: wide at its foot, tapering to a point that sways; noise licking upward with the time; its heart hotter
                float2 p = i.uv;                       // -1..1 across, -1 foot .. 1 tip
                float h = p.y * 0.5 + 0.5;
                float seed = i.data.w;
                float t = _Time.y * (2.6 + seed);
                float sway = (vnoise(float2(seed * 7.0, t * 0.9)) - 0.5) * 0.55 * h * h;
                float x = p.x - sway;
                float lick = vnoise(float2(x * 2.2 + seed * 13.0, h * 3.0 - t * 2.4)) * 0.55 + vnoise(float2(x * 4.7 - seed * 5.0, h * 6.0 - t * 3.7)) * 0.45;
                float width = (1.0 - h) * (0.65 + 0.35 * (1.0 - h)) + 0.04;
                float body = width * (0.55 + 0.75 * lick) - abs(x);
                float alpha = saturate(body * 6.0) * saturate((1.0 - h) * 5.0) * saturate(p.y * 3.0 + 3.2);
                if (alpha < 0.01) discard;
                float heat = i.flame.y;
                float core = saturate(body * 3.0) * (1.0 - h * 0.8) * (0.35 + 0.65 * heat);
                // red-orange tongues round a white-yellow heart; only the heart bright beyond white (by day the whole tongue had been so bright that the
                // tone curve drew it a pale cream cone)
                half3 c = lerp(half3(0.80h, 0.14h, 0.02h), half3(1.0h, 0.46h, 0.06h), (half)saturate(core * 1.5));
                c = lerp(c, half3(1.0h, 0.90h, 0.58h), (half)saturate(core * 2.4 - 1.2));
                c *= 1.25h + (half)(core * (1.6 + 1.4 * heat));
                return half4(c, (half)(alpha * i.data.x));
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (i.flame.x > 0.5) return Flame(i);
                float2 p = i.uv;
                float r2 = dot(p, p);
                if (r2 >= 1.0) discard;
                float seed = i.data.w;
                // a cauliflower edge: the radius wobbles with noise
                float n = vnoise(p * 2.6 + seed * 17.0) * 0.6 + vnoise(p * 5.3 - seed * 9.0) * 0.4;
                float edge = 1.0 - r2 / (0.55 + 0.45 * n);
                float alpha = saturate(edge * 3.2) * i.data.x;
                if (alpha < 0.004) discard;
                // light: a puff is a ball, lit from the sun's side; ash is darker and absorbs more
                Light l = GetMainLight();
                float3 nrm = normalize(i.side * p.x + i.upv * p.y - normalize(i.positionWS - _WorldSpaceCameraPos) * sqrt(saturate(1.0 - r2)));
                float3 up = normalize(i.positionWS);
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                half lit = saturate(dot(nrm, l.direction) * 0.65h + 0.45h) * i.sun;
                half3 base = lerp(_Steam.rgb, _Ash.rgb, i.data.y);
                // billows: the lit side bright, the far side in its own shadow
                half3 c = base * (lerp(half3(0.18h, 0.21h, 0.30h), half3(0.46h, 0.48h, 0.54h), day) + l.color * lit * day * 0.95h);
                // glow from below (lava light in the column, the crater's fire)
                half glow = i.data.z * (0.6h + 0.4h * n) * lerp(1.0h, 0.4h, day); // fire shows most at night
                c += _Glow.rgb * glow * (1.2h - 0.5h * r2);
                float dist = length(i.positionWS - _WorldSpaceCameraPos);
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.8h * (1.0h - saturate(i.data.z)));
                return half4(c, saturate(alpha + glow * 0.3h * edge));
            }
            ENDHLSL
        }
    }
}
