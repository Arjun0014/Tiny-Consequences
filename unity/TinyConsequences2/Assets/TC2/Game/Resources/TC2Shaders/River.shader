// TC2 rivers (D2-084): a reach's water as a ribbon on its centre line (WaterView, Rivers) - as wide as its flow says near the camera
// (a miniature's rivers, about 2.5x a real river's width), never thinner than a few pixels far off for the planet's larger rivers so
// they read from the region and orbit views, a brook fading as it thins below a pixel; ripples running down its course, the sky in
// its fresnel and the sun's glint, deeper in the middle, a line of wet earth at its banks near the camera; haze like the land.
Shader "TC2/River"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.05, 0.17, 0.20, 1)
        _Shallow ("Shallow", Color) = (0.20, 0.37, 0.34, 1)
        _Sky ("Sky", Color) = (0.62, 0.76, 0.92, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" }
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep; half4 _Shallow; half4 _Sky;
            CBUFFER_END
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float4 _TC2MoonDir;     // (session 27, context 4) the night as the ground and the sea's shallows have it (Terrain.shader, Water.shader)
            float _TC2NightLift;

            // uv0.xyz: the unit side vector (the ribbon's across, or a joint's radius), uv0.w: where across (-1 .. 1; 0 on the line)
            // uv1.x: half the drawn width near the camera (m), uv1.y: the river's rank among the planet's (0-1)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 side : TEXCOORD1; float4 river : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 c = TransformObjectToWorld(v.positionOS.xyz);
                float d = distance(c, _WorldSpaceCameraPos);
                float px = 2.0 * d / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);      // metres a pixel spans there
                float minPx = 1.3 * smoothstep(0.55, 0.92, v.uv1.y);                    // the larger rivers: ~2.6 px across at least
                float hw = max(v.uv1.x, minPx * px);
                float3 wpos = c + v.uv0.xyz * (hw * v.uv0.w);
                // toward the camera along its own ray (depth only: it stays where it is drawn): above the ground it lies on, far off too
                wpos += normalize(_WorldSpaceCameraPos - wpos) * min(0.0015 * d, 30.0);
                o.positionWS = wpos;
                o.positionCS = TransformWorldToHClip(wpos);
                o.side = v.uv0.xyz;
                o.river = float4(v.uv0.w, saturate(hw / max(0.35 * px, 1e-4)), v.uv1.y, hw);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 up = normalize(i.positionWS);
                float3 side = i.side - up * dot(i.side, up);
                side = dot(side, side) > 1e-8 ? normalize(side) : float3(1, 0, 0);
                float3 flow = cross(up, side);
                float t = _Time.y;
                float3 camRel = i.positionWS - _WorldSpaceCameraPos;
                float dist = length(camRel);
                float3 v = -camRel / dist;
                half near = saturate(1.0 - dist / 2500.0);
                // ripples running down the river: the detail noise in the river's own frame, carried along its course
                float2 q = float2(dot(i.positionWS, flow), dot(i.positionWS, side));
                half r1 = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, q / 13.0 + float2(-t * 0.08, 0.0)).r - 0.5h;
                half r2 = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, q / 4.7 + float2(-t * 0.19, 0.37)).r - 0.5h;
                float3 n = normalize(up + (flow * r1 * 0.40 + side * r2 * 0.28) * near);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half across = (half)saturate(abs(i.river.x));
                // (session 27) far off - a ribbon a few pixels wide - a river is its mean colour with the sky it gives back, as it reads from
                // the air: its deep middle shaded across so narrow a ribbon had read as a dark tube laid over the fields (s27e8)
                half farK = smoothstep(500.0h, 1600.0h, (half)dist);
                half depth = (1.0h - across * across) * (1.0h - farK);                // (deeper in the middle)
                half lit = saturate(dot(n, l.direction) * 0.6h + 0.4h) * lerp(0.45h, 1.0h, l.shadowAttenuation);
                half3 sky = lerp(_Sky.rgb, _TC2FogColor.rgb, 0.5h);
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, depth * 0.85h) * (0.35h + 0.65h * lit);
                body = lerp(body, half3(0.09h, 0.27h, 0.34h) * (0.60h + 0.40h * lit) + sky * 0.14h, farK);   // (the sea's shallows' tone: s27d1 still read a dark teal line)
                half fres = pow(1.0h - saturate(dot(n, v)), 4.0h);
                float3 h = normalize(l.direction + v);
                half spec = pow(saturate(dot(n, h)), lerp(40.0h, 200.0h, near)) * lerp(0.8h, 2.2h, near) * l.shadowAttenuation;
                // (session 27, context 4) by night as the sea's shallows beside it - the night sky's share and the moon's sheen and path - not a
                // tenth of the day's colour: s27e2's village view at 21:00 had the river a black stroke (5/12/13) over moonlit fields (39/68/36)
                half nightK = 0.30h * (half)_TC2NightLift;
                half moonUp = saturate((half)dot(up, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day) * (half)_TC2NightLift;
                float3 hm = normalize(_TC2MoonDir.xyz + v);
                half moonPath = pow(saturate(dot(n, hm)), lerp(90.0h, 140.0h, near)) * 0.45h * (1.0h - day) * (half)_TC2NightLift;   // (as narrow as the sea's: s27n1's broad lobe lit a far river's whole length, 107/124/141)
                half3 c = (body + sky * fres * 0.55h) * (nightK + (1.0h - nightK) * day) + l.color * spec * day
                        + half3(0.13h, 0.17h, 0.29h) * moonUp * 0.6h + half3(0.62h, 0.70h, 0.85h) * moonPath;
                // wet earth along the banks, near the camera
                c = lerp(c, half3(0.17h, 0.15h, 0.10h) * (0.3h + 0.7h * day), smoothstep(0.80h, 0.97h, across) * near * 0.55h);
                half alpha = (0.94h - 0.30h * smoothstep(0.82h, 1.0h, across)) * (half)i.river.y;
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h);
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
