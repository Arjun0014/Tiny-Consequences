// TC2 clouds (D2-027; session 25: the planet from space; session 27: forms from a 3-D noise, the weather's four covers): a shell whose cover is the
// world's weather (TC2CloudForm.hlsl: the rain's decks and the storms' bands, fair-weather cumulus where the sun heats moist ground, the layered
// cloud of a damp day round the rain, heaped towers where the air is warm). Thick clouds bright on top, thin ones grey and see-through; the side away
// from the sun in the shade of the forms sunward of it; the light reddened where the sun is low, a rim of it at the terminator, the moon's at night.
// Fades out as the camera comes under the layer (_TC2CloudFade). Reads the cover and the time only; its tunables in Look.cs.
Shader "TC2/Clouds"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TC2CloudForm.hlsl"
            float _TC2CloudFade;
            float4 _TC2MoonDir;     // (session 27) the moon, and how much of the night's light the view lets through (the ground's own: Terrain.shader)
            float _TC2NightLift;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 up = normalize(i.positionWS);
                float t = _TC2CloudScale.z;
                // the forms' level of detail from the metres a pixel spans
                float span = length(fwidth(i.positionWS));
                float texel = _TC2CloudScale.x / 64.0;
                float lod = log2(max(span / texel, 1e-3));   // (session 27: may be below 0 - near views take the finer looks sharp)
                half thick, billow;
                half4 cov = TC2Cover(up);
                half density = TC2CloudDensity(up, cov, lod, t, thick, billow);
                if (density < 0.004h) return half4(0, 0, 0, 0);
                Light l = GetMainLight();
                float3 L = l.direction;
                half mu = (half)dot(up, L);
                // relief: the forms a little toward the sun - a thicker cloud there leaves this side in its shade, a thinner one leaves it facing the sun
                float3 sunT = L - up * dot(L, up);
                float3 dirS = normalize(i.positionWS + sunT * (_TC2CloudScale.x * 0.035));
                half thickS, billowS;
                TC2CloudDensity(dirS, TC2Cover(dirS), lod, t, thickS, billowS);
                half relief = saturate((thickS - thick) * 1.5h) * (half)_TC2CloudForm.z;
                half lift = saturate((thick - thickS) * 1.2h) * (half)_TC2CloudForm.z;
                half day = saturate(mu * 3.0h + 0.2h);
                half body = saturate(thick * 0.85h);
                // the sun's light on the tops, reddened on its long way through the air when it is low; the sky's blue in the shade
                half3 sunTint = lerp(half3(1.0h, 0.60h, 0.38h), half3(1.0h, 0.99h, 0.97h), smoothstep(0.0h, 0.32h, mu));
                half3 sky = half3(0.52h, 0.60h, 0.74h);
                half3 lit = lerp(sky, sunTint * 0.95h, saturate(0.30h + 0.70h * saturate(mu * 1.4h)) * (0.58h + 0.42h * body));
                lit *= (1.0h - 0.42h * relief) * (1.0h + 0.08h * lift);
                lit *= lerp(0.80h, 1.0h, body);                                      // (thin cloud: lit through, greyer than a top)
                // the heaps: a convective deck's cauliflower and a layer's lumps in their own light and shade, the sunward lumps brighter
                // (session 31: from the region view a deck was one flat white shape) its body in its own light and shade, deeper where it is thick, its rim lit through
                half heaped = saturate(body * (0.80h + 0.45h * cov.a));
                lit *= lerp(1.0h, 0.50h + 0.66h * billow + 0.26h * (billow - billowS), heaped);   // (session 32: deeper - a deck's heaps had clipped to flat white)
                lit *= lerp(1.0h, 0.90h, saturate(body * 1.2h));
                lit *= lerp(half3(0.86h, 0.90h, 0.98h), half3(1.0h, 1.0h, 1.0h), smoothstep(0.30h, 0.95h, billow));   // (the valleys between its heaps take the sky's blue)
                // by night only the moon's light, where it is up, as the ground below takes it (session 27: a fixed grey had drawn the night side's
                // clouds as pale shapes over a black sea, moon or none - s27p3 orbit_21)
                half moon = saturate((half)dot(up, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (half)_TC2NightLift * (1.0h - day);
                half3 c = lit * day + half3(0.09h, 0.115h, 0.19h) * moon * (0.6h + 0.4h * body);
                // the terminator: the tops still catch the sun a little past the ground's dusk
                half rim = saturate(1.0h - abs(mu) * 5.0h) * smoothstep(-0.12h, 0.04h, mu) * 0.32h;
                c += half3(1.0h, 0.52h, 0.30h) * rim * body;
                // at grazing angles the shell's forms are only aliasing, and the air covers them
                float3 toCam = normalize(_WorldSpaceCameraPos - i.positionWS);
                half graze = smoothstep(0.03h, 0.20h, (half)dot(up, toCam));
                half alpha = saturate((1.0h - exp(-thick * 2.4h)) * 1.15h) * saturate(density * 1.2h);
                alpha *= _TC2CloudFade * graze * (0.30h + 0.70h * day);
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
