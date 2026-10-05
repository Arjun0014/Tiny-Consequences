// TC2 terrain (D2-026): vertex colours from the cells, triplanar detail at two scales in world space, wrapped sun
// light with soft shadows, sky and ground bounce, a night floor, and aerial haze toward the horizon colour.
// Globals set by the sky each frame: _TC2FogColor (rgb, a = day), _TC2FogDensity (per metre), _TC2Detail (noise).
Shader "TC2/Terrain"
{
    Properties
    {
        _AmbientSky ("Ambient Sky", Color) = (0.50, 0.58, 0.72, 1)
        _AmbientGround ("Ambient Ground", Color) = (0.24, 0.21, 0.17, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientSky;
                half4 _AmbientGround;
            CBUFFER_END
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            TEXTURECUBE(_TC2Clouds); SAMPLER(sampler_TC2Clouds);
            float4 _TC2SunDir;
            float4 _TC2MoonDir;
            float _TC2CloudShell;
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float _TC2SeaRadius;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; float4 farm : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.farm = float4(v.uv0, v.uv1);
                return o;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            // farmland in the village grid: long staggered strips, furrows, hedges, fallow where the land rests
            half3 Farmland(half3 crop, float4 farm, float dist, float px)
            {
                float2 q = farm.xy;
                // furlongs: blocks of strips, each lying its own way with its own strip size
                float2 blk = floor(q / 150.0 + 0.5 * Hash21(floor(q / 150.0)));
                float hb = Hash21(blk + 3.7);
                float ang = hb < 0.45 ? 0.0 : (hb < 0.8 ? 1.5708 : (hb - 0.8) * 2.0 - 0.2);
                float ca = cos(ang), sa = sin(ang);
                q = float2(ca * q.x - sa * q.y, sa * q.x + ca * q.y);
                float2 sz = float2(17.0 + 14.0 * Hash21(blk + 9.1), 52.0 + 36.0 * Hash21(blk + 1.3));
                float2 g = q / sz;
                g.x += floor(g.y) * 0.37;
                float2 cell = floor(g), f = frac(g);
                float h = Hash21(cell), h2 = Hash21(cell + 17.31);
                half worked = h2 < farm.z * 1.15 ? 1.0h : 0.0h;
                half furAmp = saturate(1.0 - dist / 650.0);
                half fur = 0.5h + 0.5h * sin(q.x * 6.2831 / 1.45 + h * 6.28);
                float edge = min(min(f.x, 1.0 - f.x) * sz.x, min(f.y, 1.0 - f.y) * sz.y);
                half hedge = (1.0h - smoothstep(0.5, 1.5, edge)) * saturate(1.2 - dist / 2500.0);
                half3 fallow = lerp(crop, half3(0.13h, 0.21h, 0.07h), 0.75h);
                half3 plot = lerp(fallow, crop * (0.84h + 0.30h * h) * (1.0h - 0.16h * furAmp + 0.16h * furAmp * fur), worked);
                plot = lerp(plot, half3(0.045h, 0.085h, 0.025h), hedge * 0.85h);
                // a pixel wider than a strip sees the field's average, not a moire of strips (px: metres per pixel)
                half3 mean = lerp(fallow, crop * 0.99h, saturate(farm.z * 1.15h));
                return lerp(plot, mean, smoothstep(0.18, 0.45, px / sz.x));
            }

            half Tri(float3 wp, float3 bw, float scale)
            {
                half x = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.yz * scale).r;
                half y = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.xz * scale).r;
                half z = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.xy * scale).r;
                return x * bw.x + y * bw.y + z * bw.z;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 up = normalize(i.positionWS); // the planet is centred at the origin
                float3 bw = abs(n); bw = bw * bw; bw = bw * bw; bw /= (bw.x + bw.y + bw.z + 1e-5);
                float3 camRel = i.positionWS - _WorldSpaceCameraPos;
                float dist = length(camRel);
                // detail: a few metres and tens of metres; it fades with distance (mips) so far land stays clean
                half near = saturate(1.0 - dist / 3500.0);
                half d1 = Tri(i.positionWS, bw, 1.0 / 23.0) - 0.5h;
                half d2 = Tri(i.positionWS, bw, 1.0 / 3.7) - 0.5h;
                half d3 = (Tri(i.positionWS, bw, 1.0 / 0.9) - 0.5h) * saturate(1.0 - dist / 400.0);
                half3 albedo = i.color.rgb;
                float px = max(length(fwidth(i.farm.xy)), 1e-3);
                if (i.color.a > 0.002h) albedo = lerp(albedo, Farmland(albedo, i.farm, dist, px), i.color.a);
                albedo *= (1.0h + d1 * 0.32h * (0.35h + 0.65h * near) + d2 * 0.30h * near + d3 * 0.26h);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);
                half wrap = saturate((dot(n, l.direction) + 0.18h) / 1.18h);
                half hemi = dot(n, up) * 0.5h + 0.5h;
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                half3 amb = lerp(_AmbientGround.rgb, _AmbientSky.rgb, hemi) * lerp(half3(0.30h, 0.34h, 0.50h), half3(1.0h, 1.0h, 1.0h), day);
                // the shadow of the cloud between this point and the sun
                float3 toCloud = normalize(i.positionWS + _TC2SunDir.xyz * max(0.0, _TC2CloudShell - length(i.positionWS)));
                half cloud = smoothstep(0.38h, 0.78h, SAMPLE_TEXTURECUBE_LOD(_TC2Clouds, sampler_TC2Clouds, toCloud, 0).r * 0.95h);
                half3 sun = l.color * wrap * lerp(0.25h, 1.0h, l.shadowAttenuation) * 1.2h * (1.0h - 0.55h * cloud) * saturate(dot(up, l.direction) * 4.0h + 0.35h);
                half moon = saturate(dot(n, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day);
                half3 c = albedo * (amb + sun + half3(0.13h, 0.17h, 0.29h) * moon);
                // the surf: a broken, moving line of foam just below the sea surface, where waves run up the shore
                float below = _TC2SeaRadius - length(i.positionWS);
                if (below > -0.4 && below < 1.4)
                {
                    float2 fuv = (abs(up.y) > 0.5 ? i.positionWS.xz : (abs(up.x) > 0.5 ? i.positionWS.yz : i.positionWS.xy));
                    half n1 = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, fuv / 5.0 + float2(_Time.y * 0.035, _Time.y * 0.02)).r;
                    half band = smoothstep(-0.4, 0.05, below) * (1.0h - smoothstep(0.2, 1.4, below));
                    half foam = band * smoothstep(0.55h, 0.72h, n1 + 0.14h * sin(_Time.y * 0.9 + below * 3.0)) * saturate(1.0 - dist / 2200.0);
                    c = lerp(c, half3(0.90h, 0.93h, 0.95h) * (0.25h + 0.75h * day), foam * 0.8h);
                }
                // aerial haze toward the horizon colour (thicker low down, gone from orbit)
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h);
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 sv(A v) : SV_POSITION
            {
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                float4 pc = TransformWorldToHClip(ApplyShadowBias(pw, nw, _LightDirection));
                #if UNITY_REVERSED_Z
                    pc.z = min(pc.z, pc.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    pc.z = max(pc.z, pc.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return pc;
            }
            half4 sf() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            float4 dv(A v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half df() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex nv
            #pragma fragment nf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V nv(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 nf(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
