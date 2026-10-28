// TC2 atmosphere (session 25: the planet from space): the sunlight the air scatters toward the eye, worked out along each ray through
// an exponential atmosphere (Rayleigh's blue, a little grey haze) - single scattering, the sun's light dimmed on its own way in, the
// planet's shadow giving the night side and the terminator's reddened light. Over the ground it lays the veil that deepens toward the
// limb; above the limb it is the thin bright band of air fading to space. The ground is the sea's sphere (no depth texture: from orbit
// and the region view the relief is a few pixels). The shell is drawn after the clouds over everything it covers (ZTest Always) and
// composes as  colour + what lies behind x the air's transmittance. Close to the ground the views' own haze (Sky) takes over
// (_TC2AtmoFade). Presentation only; its tunables in Look.cs.
Shader "TC2/Atmosphere"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+40" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One SrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float _TC2AtmoGround;   // the ground's sphere: the sea's radius (m)
            float _TC2AtmoTop;      // the air's drawn top (m from the centre)
            float _TC2AtmoH;        // Rayleigh scale height (m); the haze's is a third of it
            float4 _TC2AtmoBeta;    // Rayleigh scattering per metre at sea level (rgb), w the haze's
            float _TC2AtmoSun;      // the sunlight scattered (x the main light's colour)
            float _TC2AtmoFade;     // 1 from orbit and the region view, 0 close to the ground
            float _TC2AtmoOverGround; // the share of the in-scatter laid over the ground

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                return o;
            }

            // a ray against a sphere at the planet's centre: (near, far); far < near when it misses
            float2 Sphere(float3 ro, float3 rd, float r)
            {
                float b = dot(ro, rd), c = dot(ro, ro) - r * r, d = b * b - c;
                if (d < 0.0) return float2(1e9, -1e9);
                d = sqrt(d);
                return float2(-b - d, -b + d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_TC2AtmoFade <= 0.001) return half4(0, 0, 0, 1);
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(i.positionWS - ro);
                float Rg = _TC2AtmoGround, Rt = _TC2AtmoTop, H = _TC2AtmoH, HM = H * 0.33;
                float2 ta = Sphere(ro, rd, Rt);
                if (ta.y <= 0.0) return half4(0, 0, 0, 1);
                float t0 = max(ta.x, 0.0), t1 = ta.y;
                float2 tg = Sphere(ro, rd, Rg);
                bool ground = tg.y >= tg.x && tg.x > 0.0;
                if (ground) t1 = min(t1, tg.x);
                // (session 30) a ray that passes over the ground's sphere without meeting it may still end on high land beyond the sphere's horizon - nothing
                // here tells land from sky (no depth) - and it took the limb's whole grazing way: from 1,100-1,400 m a warm veil with a straight edge (the
                // sphere's horizon) lay over high fields at dawn (s30wedge_air: gone with the air off). Close to the ground a ray grazing the sphere within
                // the land's height ends where it passes closest and is veiled as the ground is; from the region view and orbit the limb keeps its whole way.
                float graze = 0.0;
                if (!ground)
                {
                    float tc = -dot(ro, rd);
                    float hc = length(ro + rd * tc) - Rg;
                    graze = (1.0 - smoothstep(150.0, 600.0, hc)) * (1.0 - smoothstep(2500.0, 5000.0, length(ro) - Rg));
                    if (graze > 0.0 && tc > t0) t1 = lerp(t1, min(t1, tc), graze);
                }
                Light sun = GetMainLight();
                float3 L = sun.direction;
                float3 bR = _TC2AtmoBeta.rgb;
                float bM = _TC2AtmoBeta.w;
                // (session 27, s27perf: the air x0.90-0.92 of the frame on the integrated GPU) over the ground the way through the air is short and
                // smooth: half the steps there; the limb's long grazing ways keep all sixteen
                int N = ground ? 8 : 16;
                float ds = (t1 - t0) / N;
                float odR = 0.0, odM = 0.0;
                float3 sR = 0.0, sM = 0.0;
                [loop] for (int k = 0; k < N; k++)
                {
                    float3 p = ro + rd * (t0 + (k + 0.5) * ds);
                    float h = max(0.0, length(p) - Rg);
                    float dR = exp(-h / H) * ds, dM = exp(-h / HM) * ds;
                    odR += dR * 0.5; odM += dM * 0.5;
                    // the sun's light on its way in: behind the terminator its ray passes the planet at some height - under the ground
                    // nothing, grazing it a soft penumbra (the twilight wedge), its long way through the air dimmed and reddened below
                    float along = dot(p, L);
                    float lit = along >= 0.0 ? 1.0 : smoothstep(Rg - H * 0.6, Rg + H * 0.8, length(p - along * L));
                    if (lit > 0.0)
                    {
                        float ls = max(Sphere(p, L, Rt).y, 0.0) * 0.25;
                        float lR = 0.0, lM = 0.0;
                        [unroll] for (int j = 0; j < 4; j++)
                        {
                            float hq = max(0.0, length(p + L * ((j + 0.5) * ls)) - Rg);
                            lR += exp(-hq / H); lM += exp(-hq / HM);
                        }
                        float3 att = exp(-(bR * (odR + lR * ls) + bM * 1.1 * (odM + lM * ls))) * lit;
                        sR += dR * att; sM += dM * att;
                    }
                    odR += dR * 0.5; odM += dM * 0.5;
                }
                float mu = dot(rd, L);
                float pR = 0.0596831 * (1.0 + mu * mu);                       // 3/(16 pi) (1 + mu^2)
                const float g = 0.76;
                float pM = 0.1193662 * ((1.0 - g * g) * (1.0 + mu * mu)) / ((2.0 + g * g) * pow(max(1e-4, 1.0 + g * g - 2.0 * g * mu), 1.5));
                float3 col = sun.color * _TC2AtmoSun * (sR * bR * pR + sM * bM * pM);
                float3 T = exp(-(bR * odR + bM * 1.1 * odM));
                half tr = (half)dot(T, float3(0.30, 0.45, 0.25));
                // over the ground the veil may be lighter than the air's own (Look.AtmoGround); the limb keeps all of it
                float gk = ground ? 1.0 : graze;   // (session 30: a grazing ray near the ground veiled as the ground, by how far it grazes)
                if (gk > 0.0) { col *= lerp(1.0, _TC2AtmoOverGround, gk); tr = lerp(tr, lerp(1.0h, tr, (half)_TC2AtmoOverGround), (half)gk); }
                col *= _TC2AtmoFade;
                tr = lerp(1.0h, tr, (half)_TC2AtmoFade);
                return half4(col, tr);
            }
            ENDHLSL
        }
    }
}
