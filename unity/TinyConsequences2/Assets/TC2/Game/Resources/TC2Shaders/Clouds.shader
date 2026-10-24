// TC2 clouds (D2-027; session 25: the planet from space): a shell whose cover is the world's weather (_TC2Clouds: red the decks where
// it rains today and the storms' bands, green the fair-weather cumulus of the climate's moist air). The decks are drawn with ragged,
// billowing edges and the cumulus as fields of separate puffs; both forms are drawn out east-west and drift with the latitude
// bands' winds (trades from the east in the tropics, westerlies beyond 30 degrees, easterlies beyond 60) - whole noise tiles round
// each parallel so there is no seam, the poles drawn from the plain three-way projection. Sunlit tops, the side away from the sun
// in shade, thin edges grey and see-through; a rim of light at the terminator, a faint grey at night. Fades out as the camera comes
// under the layer (_TC2CloudFade). Reads the cover and the time only; its tunables in Look.cs.
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
            TEXTURECUBE(_TC2Clouds); SAMPLER(sampler_TC2Clouds);
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            TEXTURE2D(_TC2Cells); SAMPLER(sampler_TC2Cells);
            float _TC2CloudFade;
            float4 _TC2CloudForm;    // x the decks' density, y the cumulus' cover, z the relief, w whole noise tiles round a parallel
            float4 _TC2CloudScale;   // x a tile's metres along a meridian, y along the equator, z the shell's radius

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                return o;
            }

            half N(float2 uv, float2 dx, float2 dy) { return SAMPLE_TEXTURE2D_GRAD(_TC2Detail, sampler_TC2Detail, uv, dx, dy).r; }
            half Tri(float3 p, float3 bw, float s, float2 drift)
            {
                half x = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.yz * s + drift).r;
                half y = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.xz * s + drift).r;
                half z = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.xy * s + drift).r;
                return x * bw.x + y * bw.y + z * bw.z;
            }

            // the forms' noise at a point: the large shapes, the edges and the fine break-up (0..1 each)
            void Forms(float2 uv, float2 dx, float2 dy, float drift, out half n1, out half n2, out half n3)
            {
                float2 w = float2(N(uv * 0.5 + float2(drift * 0.4, 0.0), dx * 0.5, dy * 0.5), N(uv * 0.5 + float2(0.37 + drift * 0.4, 0.71), dx * 0.5, dy * 0.5)) - 0.5;
                float2 q = uv + w * 0.45;
                n1 = N(q + float2(drift, 0.0), dx, dy);
                n2 = N(q * 3.0 + float2(drift * 2.0, 0.13), dx * 3.0, dy * 3.0);
                n3 = N(q * 9.0 + float2(drift * 3.0, 0.55), dx * 9.0, dy * 9.0);
            }

            // fair-weather cumulus: small round puffs, each cell of the field on or off (more of them the moister the air), soft-edged -
            // from orbit a fine speckle, from the region view separate clouds; never drawn out (they are too small for the bands' shear)
            half Cells(float3 p, float3 bw, float s, float2 drift)
            {
                return SAMPLE_TEXTURE2D(_TC2Cells, sampler_TC2Cells, p.yz * s + drift).r * bw.x + SAMPLE_TEXTURE2D(_TC2Cells, sampler_TC2Cells, p.xz * s + drift).r * bw.y + SAMPLE_TEXTURE2D(_TC2Cells, sampler_TC2Cells, p.xy * s + drift).r * bw.z;
            }

            // fair-weather cumulus: puffs of two sizes (cells ~45 m and ~110 m), each formed or not by a coarser noise (more of them
            // the moister the air), ragged and of uneven size by a fine noise; from afar - when a pixel spans a good part of a cell -
            // the field is drawn as the soft veil it is from orbit, its mean cover, never a glitter of points
            half Puffs(float3 p, float3 bw, half cu, float t, float span)
            {
                float s = 1.0 / 360.0;
                float2 drift = float2(t * 0.0021, t * 0.0007);
                half on = Tri(p, bw, s * 0.17, drift * 0.5);
                half formed = smoothstep(1.0h - saturate(cu) * 0.8h, 1.0h - saturate(cu) * 0.8h + 0.22h, on + 0.08h);
                half rag = Tri(p, bw, s * 2.3, drift * 1.7);
                half r1 = 0.40h * formed * (0.55h + 0.9h * rag), r2 = 0.36h * formed * (0.5h + 0.8h * (1.0h - rag));
                half near = 1.0h - saturate((half)(span / 45.0) * 3.0h - 0.3h);   // (a puff under ~4 px is a speckle: the veil instead)
                half sharp = 0.0h;
                if (near > 0.01h)
                {
                    half f1 = Cells(p, bw, s, drift), f2 = Cells(p.zxy, bw.zxy, s * 0.41, drift * 0.8 + 0.37);
                    half a1 = 1.0h - smoothstep(r1 * 0.3h, r1, f1), a2 = (1.0h - smoothstep(r2 * 0.3h, r2, f2)) * 0.85h;
                    sharp = max(a1, a2);
                }
                half veil = saturate(2.2h * (r1 * r1 + r2 * r2 * 0.85h));   // (the share of a cell the puffs cover, ~pi r^2 of the soft disc)
                return lerp(veil, sharp, near) * 0.82h;
            }

            half Density(half deck, half cu, half n1, half n2, half n3, half puffs)
            {
                // a deck: the rain's cover with billowing, ragged edges; thicker and lumpier inside
                half d = smoothstep(0.22h, 0.62h, deck + (n1 - 0.5h) * 0.60h + (n2 - 0.5h) * 0.32h + (n3 - 0.5h) * 0.14h) * (0.80h + 0.20h * n3);
                return max(d, puffs * saturate(cu * 3.0h));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 up = normalize(i.positionWS);
                float t = _Time.y;
                float lat = asin(clamp(up.y, -1.0, 1.0));
                // the bands' winds: easterlies in the tropics and beyond 60 degrees, westerlies between (a slow drift of the forms)
                float drift = -cos(3.0 * lat) * 0.0016 * t;
                // zonal coordinates: whole tiles round each parallel; the gradients from whichever longitude has no seam here
                float lonA = atan2(up.x, up.z), lonB = atan2(-up.x, -up.z);
                float around = _TC2CloudForm.w, R = _TC2CloudScale.z;
                float2 uvA = float2(lonA * (around / 6.2831853), lat * R / _TC2CloudScale.x);
                float2 uvB = float2(lonB * (around / 6.2831853), uvA.y);
                float2 dxA = ddx(uvA), dyA = ddy(uvA), dxB = ddx(uvB), dyB = ddy(uvB);
                bool useB = abs(dxB.x) + abs(dyB.x) < abs(dxA.x) + abs(dyA.x);
                float2 dx = useB ? dxB : dxA, dy = useB ? dyB : dyA;
                half n1, n2, n3;
                Forms(uvA, dx, dy, drift, n1, n2, n3);
                // near the poles the parallels shrink to nothing: the plain three-way projection there
                half polar = smoothstep(0.84h, 0.94h, (half)abs(up.y));
                if (polar > 0.0h)
                {
                    float3 bw = abs(up); bw = bw * bw; bw /= (bw.x + bw.y + bw.z);
                    float3 p = i.positionWS;
                    float s = 1.0 / _TC2CloudScale.x;
                    half m1 = Tri(p, bw, s * 2.0, float2(t * 0.0009, 0.0));
                    half m2 = Tri(p, bw, s * 6.0, float2(-t * 0.0019, 0.3));
                    half m3 = Tri(p, bw, s * 18.0, float2(t * 0.0023, 0.7));
                    n1 = lerp(n1, m1, polar); n2 = lerp(n2, m2, polar); n3 = lerp(n3, m3, polar);
                }
                // the weather's cover, looked up through the forms' warp so its cells never show as blocks
                float3 east = normalize(float3(up.z, 0.0, -up.x) + float3(1e-5, 0.0, 0.0));
                float3 north = cross(up, east);
                float3 look = normalize(up + (east * (n1 - 0.5) * 0.06 + north * (n3 - 0.5) * 0.03));
                half2 cover = SAMPLE_TEXTURECUBE(_TC2Clouds, sampler_TC2Clouds, look).rg;
                half deck = cover.r * (half)_TC2CloudForm.x, cu = cover.g * (half)_TC2CloudForm.y;
                float3 bwp = abs(up); bwp = bwp * bwp; bwp /= (bwp.x + bwp.y + bwp.z);
                float span = length(fwidth(i.positionWS));   // (metres a pixel spans here)
                half puffs = cu > 0.01h ? Puffs(i.positionWS, bwp, cu, t, span) : 0.0h;
                half density = Density(deck, cu, n1, n2, n3, puffs);
                if (density < 0.004h) return half4(0, 0, 0, 0);
                Light l = GetMainLight();
                // relief: the forms a little toward the sun - where they thin that way, this side faces the sun; where they thicken, it is in shade
                float3 sunT = l.direction - up * dot(l.direction, up);
                float2 toward = float2(dot(sunT, east), dot(sunT, north)) * 0.035;
                half s1, s2, s3;
                Forms(uvA + toward, dx, dy, drift, s1, s2, s3);
                half dSun = Density(deck, cu, lerp(s1, n1, polar), lerp(s2, n2, polar), lerp(s3, n3, polar), puffs);
                half relief = saturate((dSun - density) * 2.2h) * (half)_TC2CloudForm.z;
                half sun = saturate(dot(up, l.direction));
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half rim = saturate(1.0h - abs(dot(up, l.direction)) * 4.0h) * 0.35h;
                half thick = saturate(density * 1.4h);
                half3 lit = lerp(half3(0.58h, 0.62h, 0.70h), half3(1.0h, 0.99h, 0.97h), sun * (0.55h + 0.45h * thick)) * (0.15h + 0.85h * day);
                half3 c = lit * (1.0h - 0.42h * relief) * (0.84h + 0.16h * n2) + half3(1.0h, 0.65h, 0.45h) * rim * day;
                // at grazing angles the shell's noise is only aliasing, and the air covers it
                float3 toCam = normalize(_WorldSpaceCameraPos - i.positionWS);
                half graze = smoothstep(0.03h, 0.22h, (half)dot(up, toCam));
                half alpha = density * (0.55h + 0.40h * thick) * _TC2CloudFade * (0.35h + 0.65h * day) * graze;
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
