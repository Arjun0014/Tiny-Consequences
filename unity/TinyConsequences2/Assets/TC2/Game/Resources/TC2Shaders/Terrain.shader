// TC2 terrain (D2-026): vertex colours from the cells, triplanar detail at two scales in world space, wrapped sun
// light with soft shadows, sky and ground bounce, a night floor, and aerial haze toward the horizon colour; the woods as a
// canopy of lit crowns from the simulation's forest share (D2-079).
// Globals set by the sky: _TC2FogColor (rgb, a = day), _TC2FogDensity (per metre), _TC2Detail (noise), _TC2Canopy (crowns).
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
            TEXTURE2D(_TC2Canopy); SAMPLER(sampler_TC2Canopy);
            TEXTURE2D(_TC2Cover); SAMPLER(sampler_TC2Cover);
            float _TC2CanopyTile;
            float _TC2CanopyK;
            float4 _TC2CoverMean;
            TEXTURECUBE(_TC2Clouds); SAMPLER(sampler_TC2Clouds);
            float4 _TC2SunDir;
            float4 _TC2MoonDir;
            float _TC2CloudShell;
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float _TC2SeaRadius;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 uv2 : TEXCOORD2; float4 uv3 : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; float4 farm : TEXCOORD2; half4 wood : TEXCOORD3; half4 land : TEXCOORD4; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.farm = float4(v.uv0, v.uv1);
                o.wood = v.uv2;
                o.land = v.uv3;
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

            // a crown at a point (D2-079): d its distance from the centre over its radius, num its number - it stands where its
            // number is below thr; conifers (by their number against the conifers' share) are narrower and pointed
            half CrownH(half d, half num, half thr, half con, out half isCon)
            {
                isCon = (half)(frac(num * 13.73h) < con);
                half top = 0.64h + 0.36h * frac(num * 7.31h);   // (Canopy.Top)
                half dd = d * lerp(1.0h, 1.32h, isCon);
                half shape = lerp(sqrt(saturate(1.0h - dd * dd)), saturate(1.0h - dd), isCon);
                return (half)(num < thr) * top * shape;
            }

            // one projection of the woods: the taller of the two crowns of lowest number over this point, if they stand;
            // h its height (0-1), isCon 1 for a conifer, id its number (its colour)
            void Crowns(float2 uv, half thr, half con, out half h, out half isCon, out half id)
            {
                half4 t = SAMPLE_TEXTURE2D(_TC2Canopy, sampler_TC2Canopy, uv);
                half c1, c2;
                half h1 = CrownH(t.r, t.g, thr, con, c1);
                half h2 = CrownH(t.b, t.a, thr, con, c2);
                half second = (half)(h2 > h1);
                h = max(h1, h2);
                isCon = lerp(c1, c2, second);
                id = lerp(t.g, t.a, second);
            }

            // the share of the ground under standing crowns round a point (D2-081): the cover texture keeps it at four crown
            // thresholds (Canopy.CoverAt), interpolated here; its mips are that share averaged over the pixel's ground
            half CoverOf(half4 c, half thr)
            {
                half lo = c.r * saturate(thr / 0.18h);
                half m1 = lerp(c.r, c.g, saturate((thr - 0.18h) / 0.20h));
                half m2 = lerp(c.g, c.b, saturate((thr - 0.38h) / 0.24h));
                half hi = lerp(c.b, c.a, saturate((thr - 0.62h) / 0.38h));
                return thr < 0.38h ? (thr < 0.18h ? lo : m1) : (thr < 0.62h ? m2 : hi);
            }

            half CoverAt(float2 uv, half thr) { return CoverOf(SAMPLE_TEXTURE2D(_TC2Cover, sampler_TC2Cover, uv), thr); }

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
                // beaches (D2-081), per pixel from the ground's own height over the sea - a vertex colour would follow the triangles of
                // coarse patches as a staircase along every coast: sand to 0.6 + 4.2 x the beach's width (TerrainLod.BeachAt), wet near the
                // water; under the water it is the shallows' floor
                {
                    // (a metre of noise in the height read: on coarse patches the height is flat within each big triangle, and a contour
                    // of it - the sand's edge - would follow the triangles as steps)
                    float relP = length(i.positionWS) - _TC2SeaRadius + d1 * 2.2;
                    // the sea floor too, by its depth: sand in the shallows, dark below (as TerrainLod's Shelf and Abyss)
                    half3 seaFloor = lerp(half3(0.64h, 0.57h, 0.32h), half3(0.004h, 0.012h, 0.025h), (half)sqrt(saturate(-relP / 60.0)));
                    albedo = lerp(albedo, seaFloor * (1.0h + d1 * 0.2h), smoothstep(-0.2h, -1.0h, (half)relP));
                    half bw = i.land.w;
                    half beach = (1.0h - smoothstep(0.6h, 0.6h + 4.2h * bw, (half)relP)) * step(0.001h, bw) * smoothstep(-2.0h, -0.4h, (half)relP);
                    half3 sand = lerp(half3(0.73h, 0.60h, 0.32h), half3(0.39h, 0.32h, 0.18h), 1.0h - smoothstep(0.3h, 1.6h, (half)relP));
                    albedo = lerp(albedo, sand * (1.0h + d2 * 0.12h * near), beach * 0.92h);
                }
                // the woods (D2-079): a canopy of crowns where the simulation has forest - each crown lit as the dome or spire it
                // is, the floor between them in their shade; autumn's colours and winter's bare branches where there is an autumn,
                // snow on the conifers; a crown smaller than a few pixels gives way to the canopy's mean colour (no shimmer)
                // (no branch: derivatives and filtered samples must run in uniform control flow on WebGPU; with no forest the
                // block leaves the ground as it was)
                float3 nl = n;
                half floorOcc = 1.0h;
                half dry = i.land.x;   // (how dry the climate is: leaves toward olive)
                {
                    half dens = i.wood.x;
                    half con = i.wood.y, leaves = i.wood.z, snowy = i.wood.w;
                    float3 ub = abs(up); ub = ub * ub; ub = ub * ub; ub = ub * ub; ub /= (ub.x + ub.y + ub.z + 1e-5);
                    // the crowns that stand cover the forest's share of the ground (its glades are in the share); near the camera the
                    // trees themselves stand there, at these crowns, and the canopy gives way to them (400 m to 300 m)
                    half dl = saturate(dens);
                    half handOver = smoothstep(300.0h, 400.0h, (half)dist);
                    half thr = saturate(-log(1.0h - min(dl, 0.97h)) * (half)_TC2CanopyK);
                    float sc0 = 1.0 / _TC2CanopyTile;
                    float2 uvx = i.positionWS.yz * sc0, uvy = i.positionWS.xz * sc0 + 0.37, uvz = i.positionWS.xy * sc0 + 0.71;
                    half h0, c0, id0, h1, c1, id1, h2, c2, id2;
                    Crowns(uvx, thr, con, h0, c0, id0);
                    Crowns(uvy, thr, con, h1, c1, id1);
                    Crowns(uvz, thr, con, h2, c2, id2);
                    half h = h0 * ub.x + h1 * ub.y + h2 * ub.z;
                    half isCon = c0 * ub.x + c1 * ub.y + c2 * ub.z;
                    half cid = id0 * ub.x + id1 * ub.y + id2 * ub.z;
                    // the shadow of the crowns toward the sun: is there a taller crown a few metres sunward?
                    float3 Ls = _TC2SunDir.xyz;
                    float3 lt = (Ls - up * dot(Ls, up)) / max(0.3, dot(Ls, up)) * (7.0 * sc0);
                    half s0, s1, s2, k0, k1, k2;
                    Crowns(uvx + lt.yz, thr, con, s0, k0, k1);
                    Crowns(uvy + lt.xz, thr, con, s1, k0, k1);
                    Crowns(uvz + lt.xy, thr, con, s2, k0, k2);
                    half hs = s0 * ub.x + s1 * ub.y + s2 * ub.z;
                    half shade = 1.0h - 0.55h * saturate((hs - h) * 2.5h) * handOver;
                    // far (D2-081): the ground the standing crowns cover round this point, and a few metres sunward of it - the cover
                    // texture's mips average the near canopy over the pixel, so the far woods keep its clumps and glades
                    half fc = CoverAt(uvx, thr) * ub.x + CoverAt(uvy, thr) * ub.y + CoverAt(uvz, thr) * ub.z;
                    half fcs = CoverAt(uvx + lt.yz, thr) * ub.x + CoverAt(uvy + lt.xz, thr) * ub.y + CoverAt(uvz + lt.xy, thr) * ub.z;
                    // past ~5 m a pixel the tile's own crowns would repeat as a pattern: there the woods are drawn from the same cover
                    // turned and scaled up - clumps of crowns some twenty metres across, the same share covered, a pattern that does not
                    // line up with the tile -, and past ~12 m a pixel from its mean; their glades and stands are the share's own
                    float pxw = length(fwidth(i.positionWS));
                    float2x2 turnB = float2x2(0.8, -0.6, 0.6, 0.8);
                    half fb = CoverAt(mul(turnB, uvx) * 0.413 + 0.19, thr) * ub.x + CoverAt(mul(turnB, uvy) * 0.413 + 0.53, thr) * ub.y
                            + CoverAt(mul(turnB, uvz) * 0.413 + 0.11, thr) * ub.z;
                    half fcm = CoverOf((half4)_TC2CoverMean, thr);
                    fb = lerp(fb, fcm, smoothstep(10.0h, 20.0h, (half)pxw));
                    half tileFade = smoothstep(3.5h, 7.0h, (half)pxw);
                    fc = lerp(fc, fb, tileFade); fcs = lerp(fcs, fb, tileFade);
                    // the leaves: summer greens by the crown, autumn's gold and rust, bare branches in winter (broadleaves)
                    half bare = saturate((leaves - 0.8h) * 5.0h);
                    half turn = saturate((leaves - 0.1h) / 0.55h);
                    half coverK = lerp(1.0h, 0.8h, con) * lerp(1.0h, 0.45h, bare * (1.0h - con));   // (narrow conifers; bare boughs)
                    fc *= coverK; fcs *= coverK;
                    half hue = frac(cid * 5.31h);   // (the trees near the camera take the same green: TerrainLod.Tints)
                    half3 broad = lerp(lerp(half3(0.046h, 0.125h, 0.036h), half3(0.090h, 0.200h, 0.040h), saturate(hue * 2.0h)), half3(0.170h, 0.262h, 0.046h), saturate(hue * 2.0h - 1.0h));
                    broad = lerp(broad, half3(broad.r * 1.6h + 0.015h, broad.g * 0.90h, broad.b * 0.60h), dry * 0.8h);   // (dry lands: olive)
                    broad = lerp(broad, lerp(half3(0.52h, 0.31h, 0.035h), half3(0.42h, 0.11h, 0.025h), hue), turn);
                    broad = lerp(broad, half3(0.105h, 0.082h, 0.064h), bare);
                    half3 needle = lerp(half3(0.020h, 0.058h, 0.028h), half3(0.040h, 0.092h, 0.042h), hue);
                    needle = lerp(needle, needle * half3(1.35h, 0.95h, 0.75h), dry * 0.5h);
                    half3 crown = lerp(broad, needle, isCon);
                    crown = lerp(crown, half3(0.78h, 0.80h, 0.84h), snowy * isCon * saturate(h * 1.4h - 0.25h) * 0.65h);
                    crown *= 0.50h + 0.50h * h;                                   // (the lower parts of a crown are in its own shade)
                    half mask = smoothstep(0.02h, 0.12h, h) * lerp(1.0h, 0.4h, bare * (1.0h - isCon)) * handOver;
                    half3 litter = lerp(albedo, half3(0.060h, 0.050h, 0.028h), 0.45h * dl) * (1.0h - 0.25h * dl);   // (leaf litter)
                    half3 floorCol = litter * shade;
                    half3 nearCol = lerp(floorCol, crown * lerp(1.0h, shade, 0.7h), mask);
                    // far, where a crown is under a few pixels: the crowns' mean colour over the ground they cover, the floor between
                    // in the shade of the clumps sunward of it
                    half fade = smoothstep(1.3h, 3.0h, (half)pxw);
                    half3 summer = lerp(half3(0.072h, 0.160h, 0.036h), half3(0.120h, 0.228h, 0.044h), hue);
                    summer = lerp(summer, half3(summer.r * 1.6h + 0.015h, summer.g * 0.90h, summer.b * 0.60h), dry * 0.8h);
                    half3 meanCrown = lerp(lerp(lerp(summer, half3(0.47h, 0.21h, 0.03h), turn), half3(0.105h, 0.082h, 0.064h), bare),
                                           lerp(half3(0.026h, 0.068h, 0.032h), half3(0.70h, 0.72h, 0.76h), snowy * 0.35h), con) * 0.70h;
                    // stands of a few hundred metres, older and darker or younger and lighter: the far woods' own grain (from orbit too)
                    half stand = Tri(i.positionWS, ub, 1.0 / 1700.0);
                    meanCrown *= lerp(1.0h, 0.72h + 0.56h * stand, saturate(fade * 1.5h));
                    meanCrown = lerp(meanCrown, meanCrown * half3(0.80h, 0.96h, 1.18h), saturate(stand * 2.0h - 1.0h) * fade);
                    half floorShadeF = 1.0h - 0.5h * saturate((fcs - fc) * 2.0h);
                    half3 farCol = lerp(litter * floorShadeF, meanCrown, fc);
                    albedo = lerp(nearCol, farCol, fade);
                    // (the floor under the trees sees little of the sky)
                    floorOcc = lerp(1.0h - 0.55h * dl * (1.0h - mask), 1.0h - 0.55h * dl * (1.0h - fc), fade);
                    // each crown - far off, each clump of crowns - bends the light it takes (a bump from its height, without tangents:
                    // Mikkelsen 2010): the clumps' sunward sides lit, their far sides in shade
                    float hm = lerp(h * 5.0, fc * 7.0, fade);
                    float3 dpdx = ddx(i.positionWS), dpdy = ddy(i.positionWS);
                    float3 r1 = cross(dpdy, n), r2 = cross(n, dpdx);
                    float det = dot(dpdx, r1);
                    float3 grad = (det < 0.0 ? -1.0 : 1.0) * (ddx(hm) * r1 + ddy(hm) * r2);
                    float3 nb = abs(det) > 1e-8 ? normalize(abs(det) * n - grad) : n;
                    nl = normalize(lerp(n, nb, max(mask * (1.0h - fade), fade * saturate(dl * 3.0h))));
                }
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);
                half wrap = saturate((dot(nl, l.direction) + 0.18h) / 1.18h);
                half hemi = dot(nl, up) * 0.5h + 0.5h;
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                half3 amb = lerp(_AmbientGround.rgb, _AmbientSky.rgb, hemi) * lerp(half3(0.30h, 0.34h, 0.50h), half3(1.0h, 1.0h, 1.0h), day) * floorOcc;
                // the shadow of the cloud between this point and the sun
                float3 toCloud = normalize(i.positionWS + _TC2SunDir.xyz * max(0.0, _TC2CloudShell - length(i.positionWS)));
                half cloud = smoothstep(0.38h, 0.78h, SAMPLE_TEXTURECUBE_LOD(_TC2Clouds, sampler_TC2Clouds, toCloud, 0).r * 0.95h);
                half3 sun = l.color * wrap * lerp(0.25h, 1.0h, l.shadowAttenuation) * 1.2h * (1.0h - 0.55h * cloud) * saturate(dot(up, l.direction) * 4.0h + 0.35h);
                half moon = saturate(dot(nl, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day);
                half3 c = albedo * (amb + sun + half3(0.13h, 0.17h, 0.29h) * moon);
                // under the sea, the water's own colour by the depth of water above (Beer-Lambert): the sand of the shallows
                // shows clear and turquoise, the deep goes blue; the sea's surface on top adds only sky and sun (D2-040)
                float below = _TC2SeaRadius - length(i.positionWS) - d1 * 2.2;   // (the beach's own noise: its edge and the water's move together)
                if (below > 0.0)
                {
                    half through = (half)exp(-below / 3.2);
                    half3 waterCol = lerp(half3(0.07h, 0.42h, 0.50h), half3(0.015h, 0.075h, 0.20h), (half)saturate(below / 28.0)) * (0.3h + 0.7h * day);
                    c = lerp(waterCol, c * half3(0.86h, 0.98h, 1.0h), through);
                }
                // the surf: a broken, moving line of foam just below the sea surface, where waves run up the shore
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
