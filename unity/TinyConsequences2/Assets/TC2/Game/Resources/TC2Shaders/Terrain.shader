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
            #include "TC2CloudForm.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientSky;
                half4 _AmbientGround;
            CBUFFER_END
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            TEXTURE2D(_TC2Canopy); SAMPLER(sampler_TC2Canopy);
            TEXTURE2D(_TC2Cover); SAMPLER(sampler_TC2Cover);
            TEXTURE2D(_TC2StandCdf); SAMPLER(sampler_TC2StandCdf);
            float _TC2CanopyTile;
            float _TC2CanopyK;
            float4 _TC2CoverMean;
            float4 _TC2SunDir;
            float4 _TC2MoonDir;
            float _TC2NightLift;   // (session 27) the night's sky and moon light on the ground: 1 near it, low from orbit (the night side dark)
            TEXTURECUBE(_TC2Lights); SAMPLER(sampler_TC2Lights);   // (session 27: the homes lived in, LightsView)
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float _TC2Year;   // the world's year (whole years since it began): which strips rest turns with it
            float _TC2SeaRadius;
            float _TC2Unbranched;   // (session 27, tools: 1 runs every branch below as if taken - the frame's cost of the branches measured; 0 in the game)

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 uv2 : TEXCOORD2; float4 uv3 : TEXCOORD3; float3 uv4 : TEXCOORD4; float4 uv5 : TEXCOORD5; float2 uv6 : TEXCOORD6; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; float4 farm : TEXCOORD2; half4 wood : TEXCOORD3; half4 land : TEXCOORD4; half3 kind : TEXCOORD5; half4 realm : TEXCOORD6; half2 realmS : TEXCOORD7; };

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
                o.kind = v.uv4;   // (W-FOREST: x how tropical, y how seasonally dry, z how waterlogged the wood's ground - its pools)
                o.realm = v.uv5;  // (D2-167: the realms layer's map colour premultiplied by the land's weight - rgb x w, w; zero when it is off)
                o.realmS = v.uv6.xy;   // (and the fill's strength, premultiplied alike: the seat's stronger; y the share of the crop burned here - session 29)
                return o;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            // farmland in the village grid: long staggered strips, furrows, hedges, fallow where the land rests
            half3 Farmland(half3 crop, float4 farm, float dist, float px, half burnt)
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
                float h = Hash21(cell), h2 = frac(Hash21(cell + 17.31) + _TC2Year * 0.618034);
                // the share of strips sown is the village's own (its fields against its arable: what it rests - the soil, D2-139 - and what
                // it leaves unclaimed); which strips rest turns from year to year, so a strip lies fallow every few years (session 17: before,
                // 1.15 x the share was drawn worked - most of the rest hidden - and the same strips always lay fallow)
                half worked = h2 < farm.z ? 1.0h : 0.0h;
                half furAmp = saturate(1.0 - dist / 650.0);
                half fur = 0.5h + 0.5h * sin(q.x * 6.2831 / 1.45 + h * 6.28);
                float edge = min(min(f.x, 1.0 - f.x) * sz.x, min(f.y, 1.0 - f.y) * sz.y);
                half hedge = (1.0h - smoothstep(0.5, 1.5, edge)) * saturate(1.2 - dist / 2500.0);
                // fallow: rough grazed grass and weeds - and in the growing season some of it ploughed bare (the fallow's summer ploughing)
                half3 fallow = lerp(crop, half3(0.16h, 0.22h, 0.08h), 0.75h);
                half stage = (half)floor(farm.w);
                half ploughed = (stage > 1.5h && stage < 2.5h && Hash21(cell + 41.7 + _TC2Year) < 0.35) ? 1.0h : 0.0h;
                fallow = lerp(fallow, half3(0.30h, 0.22h, 0.13h), ploughed * 0.85h);
                half3 plot = lerp(fallow, crop * (0.84h + 0.30h * h) * (1.0h - 0.16h * furAmp + 0.16h * furAmp * fur), worked);
                // (session 29) the crop burned this season (Settlement.CropBurnt: a band's torches, a wildfire) - that share of the sown strips charred
                // whole, furlong by furlong as a fire runs on from strip to strip: black stubble streaked with grey ash, the spared strips standing
                half3 ash = lerp(half3(0.022h, 0.019h, 0.016h), half3(0.17h, 0.16h, 0.15h), saturate(fur * 0.55h + h * 0.35h - 0.25h));
                half charred = worked * ((burnt > 0.01h && Hash21(blk + 5.3) * 0.6 + h * 0.4 < burnt) ? 1.0h : 0.0h);
                plot = lerp(plot, ash, charred);
                plot = lerp(plot, half3(0.045h, 0.085h, 0.025h), hedge * 0.85h);
                // a pixel wider than a strip sees the field's average, not a moire of strips (px: metres per pixel)
                half3 mean = lerp(fallow, lerp(crop * 0.99h, half3(0.06h, 0.055h, 0.05h), burnt), saturate(farm.z));
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
            // h its height (0-1), isCon 1 for a conifer, id its number (its colour). (Every sample in the woods takes the uv's
            // gradients, gx and gy, from outside their branches: WebGPU allows implicit derivatives only in uniform control flow.)
            void Crowns(float2 uv, float2 gx, float2 gy, half thr, half con, out half h, out half isCon, out half id)
            {
                half4 t = SAMPLE_TEXTURE2D_GRAD(_TC2Canopy, sampler_TC2Canopy, uv, gx, gy);
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

            half CoverAt(float2 uv, float2 gx, float2 gy, half thr) { return CoverOf(SAMPLE_TEXTURE2D_GRAD(_TC2Cover, sampler_TC2Cover, uv, gx, gy), thr); }

            // the woods' stands and glades (D2-084; Canopy.StandNoise/StandRank/Stands: the same arithmetic on the CPU, where the trees
            // near the camera stand): a value noise of the ground's position (~150-300 m features, never repeating), its rank read from
            // the noise's distribution so that ranks spread evenly, and a stand where the woods' share is above the rank
            uint StandHash(int3 i)
            {
                uint h = ((uint)i.x * 73856093u) ^ ((uint)i.y * 19349663u) ^ ((uint)i.z * 83492791u);
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return h;
            }
            float StandLat(int3 i) { return (float)(StandHash(i) & 0xFFFFFFu) / 16777216.0; }
            float StandV(float3 x)
            {
                float3 fl = floor(x);
                int3 i = (int3)fl + 1024;
                float3 t = x - fl;
                t = t * t * (3.0 - 2.0 * t);
                float a = StandLat(i), b = StandLat(i + int3(1, 0, 0)), c = StandLat(i + int3(0, 1, 0)), d = StandLat(i + int3(1, 1, 0));
                float e = StandLat(i + int3(0, 0, 1)), f = StandLat(i + int3(1, 0, 1)), g = StandLat(i + int3(0, 1, 1)), h = StandLat(i + int3(1, 1, 1));
                float ab = a + t.x * (b - a), cd = c + t.x * (d - c), ef = e + t.x * (f - e), gh = g + t.x * (h - g);
                float lo = ab + t.y * (cd - ab), hi = ef + t.y * (gh - ef);
                return lo + t.z * (hi - lo);
            }
            float StandRank(float3 wp)
            {
                float3 x = wp / 210.0;
                float v = 0.58 * StandV(x) + 0.29 * StandV(x * 2.07 + float3(17.1, 3.3, -9.4)) + 0.13 * StandV(x * 4.31 + float3(-4.7, 12.9, 6.1));
                return SAMPLE_TEXTURE2D_LOD(_TC2StandCdf, sampler_TC2StandCdf, float2(v, 0.5), 0).r;
            }

            half Tri(float3 wp, float3 bw, float scale)
            {
                half x = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.yz * scale).r;
                half y = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.xz * scale).r;
                half z = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, wp.xy * scale).r;
                return x * bw.x + y * bw.y + z * bw.z;
            }

            // the same with the position's gradients given (gx, gy: ddx and ddy of wp), for use inside a branch
            half TriG(float3 wp, float3 bw, float scale, float3 gx, float3 gy)
            {
                gx *= scale; gy *= scale;
                half x = SAMPLE_TEXTURE2D_GRAD(_TC2Detail, sampler_TC2Detail, wp.yz * scale, gx.yz, gy.yz).r;
                half y = SAMPLE_TEXTURE2D_GRAD(_TC2Detail, sampler_TC2Detail, wp.xz * scale, gx.xz, gy.xz).r;
                half z = SAMPLE_TEXTURE2D_GRAD(_TC2Detail, sampler_TC2Detail, wp.xy * scale, gx.xy, gy.xy).r;
                return x * bw.x + y * bw.y + z * bw.z;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 up = normalize(i.positionWS); // the planet is centred at the origin
                float3 bw = abs(n); bw = bw * bw; bw = bw * bw; bw /= (bw.x + bw.y + bw.z + 1e-5);
                float3 camRel = i.positionWS - _WorldSpaceCameraPos;
                float dist = length(camRel);
                // the land's relief made legible from afar (D2-084): far off, the ground's slopes are lit as if steeper (a cartographer's
                // hillshade), so hills, valleys and ridges read from the region and orbit views; near the camera the true slope
                {
                    float upn = dot(n, up);
                    float far = smoothstep(500.0, 5000.0, dist);
                    n = normalize(up * upn + (n - up * upn) * (1.0 + 2.2 * far));
                }
                // the position's derivatives, taken here in uniform control flow for the branches below (session 27, the frame on an
                // integrated GPU: the terrain cost x0.59 of the region view's frame - each block now runs only where it draws something)
                float3 dpx = ddx(i.positionWS), dpy = ddy(i.positionWS);
                float pxw = length(abs(dpx) + abs(dpy));   // (metres a pixel: length(fwidth(positionWS)))
                bool every = _TC2Unbranched > 0.5;
                // detail: a few metres and tens of metres; it fades with distance (mips) so far land stays clean (the finer two are
                // read only as near as they show: 3.5 km and 400 m)
                half near = saturate(1.0 - dist / 3500.0);
                half d1 = Tri(i.positionWS, bw, 1.0 / 23.0) - 0.5h;
                half d2 = 0.0h, d3 = 0.0h;
                UNITY_BRANCH
                if (dist < 3500.0 || every)
                {
                    d2 = TriG(i.positionWS, bw, 1.0 / 3.7, dpx, dpy) - 0.5h;
                    UNITY_BRANCH
                    if (dist < 400.0 || every) d3 = (TriG(i.positionWS, bw, 1.0 / 0.9, dpx, dpy) - 0.5h) * saturate(1.0 - dist / 400.0);
                }
                half3 albedo = i.color.rgb;
                float px = max(length(fwidth(i.farm.xy)), 1e-3);
                if (i.color.a > 0.002h) albedo = lerp(albedo, Farmland(albedo, i.farm, dist, px, i.realmS.y), i.color.a);
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
                // (session 27: branched - where the simulation has no forest the block is skipped and leaves the ground as it was; within
                // it, the near crowns only where they draw (beyond 300 m and under ~3 m a pixel), the far cover only past ~1.3 m a pixel.
                // Every sample inside takes its gradients from outside the branches and the bump's derivatives are taken after them:
                // WebGPU allows implicit derivatives only in uniform control flow)
                float3 nl = n;
                half floorOcc = 1.0h;
                half dry = i.land.x;   // (how dry the climate is: leaves toward olive)
                float hm = 0.0;        // (the crowns' height for their bump, and how much of it bends the normal: 0 outside the woods)
                half bumpK = 0.0h;
                UNITY_BRANCH
                if (i.wood.x > 0.002h || every)
                {
                    half dens = i.wood.x;
                    half con = i.wood.y, leaves = i.wood.z, snowy = i.wood.w;
                    float3 ub = abs(up); ub = ub * ub; ub = ub * ub; ub = ub * ub; ub /= (ub.x + ub.y + ub.z + 1e-5);
                    // stands and glades (D2-084, Canopy.Stands): where the woods' share is above this point's rank a stand of closed canopy,
                    // else a glade or meadow - the stands cover the share; thin woods stay scattered trees; where a pixel spans most of a
                    // stand (far off) the share itself (no shimmer)
                    {
                        half sw = smoothstep(0.12h, 0.35h, dens) * (1.0h - smoothstep(45.0h, 110.0h, (half)pxw));
                        UNITY_BRANCH
                        if (sw > 0.0h || every)
                        {
                            half rank = (half)StandRank(i.positionWS) - 0.30h * i.land.y;   // (the woods keep to a river's banks)
                            dens = lerp(dens, smoothstep(rank - 0.06h, rank + 0.06h, dens), sw);
                        }
                    }
                    // the crowns that stand cover the forest's share of the ground (its glades are in the share); near the camera the
                    // trees themselves stand there, at these crowns, and the canopy gives way to them (400 m to 300 m)
                    half dl = saturate(dens);
                    half handOver = smoothstep(300.0h, 400.0h, (half)dist);
                    half fade = smoothstep(1.3h, 3.0h, (half)pxw);   // (far, where a crown is under a few pixels: below)
                    half thr = saturate(-log(1.0h - min(dl, 0.97h)) * (half)_TC2CanopyK);
                    float sc0 = 1.0 / _TC2CanopyTile;
                    float2 uvx = i.positionWS.yz * sc0, uvy = i.positionWS.xz * sc0 + 0.37, uvz = i.positionWS.xy * sc0 + 0.71;
                    float2 gxx = dpx.yz * sc0, gyx = dpy.yz * sc0, gxy = dpx.xz * sc0, gyy = dpy.xz * sc0, gxz = dpx.xy * sc0, gyz = dpy.xy * sc0;
                    // the crowns over this point: their heights near the camera, their numbers (colours) near and far; none are drawn
                    // within 300 m, where the trees themselves stand
                    half h = 0.0h, isCon = 0.0h, cid = 0.0h;
                    UNITY_BRANCH
                    if (handOver > 0.0h || fade > 0.0h || every)
                    {
                        half h0, c0, id0, h1, c1, id1, h2, c2, id2;
                        Crowns(uvx, gxx, gyx, thr, con, h0, c0, id0);
                        Crowns(uvy, gxy, gyy, thr, con, h1, c1, id1);
                        Crowns(uvz, gxz, gyz, thr, con, h2, c2, id2);
                        h = h0 * ub.x + h1 * ub.y + h2 * ub.z;
                        isCon = c0 * ub.x + c1 * ub.y + c2 * ub.z;
                        cid = id0 * ub.x + id1 * ub.y + id2 * ub.z;
                    }
                    float3 Ls = _TC2SunDir.xyz;
                    float3 lt = (Ls - up * dot(Ls, up)) / max(0.3, dot(Ls, up)) * (7.0 * sc0);
                    // the leaves: summer greens by the crown, autumn's gold and rust, bare branches in winter (broadleaves)
                    half bare = saturate((leaves - 0.8h) * 5.0h);
                    half turn = saturate((leaves - 0.1h) / 0.55h);
                    half hue = frac(cid * 5.31h);   // (the trees near the camera take the same green: TerrainLod.Tints)
                    // (W-FOREST) the kind of wood: the rain forest's deep glossy greens, the dry tropics' olive - TerrainLod.Tints' palettes 2 and 3
                    half warm = i.kind.x, seas = i.kind.y;
                    // (W-FOREST pass 2b) the dry tropics' leaves wither khaki in the dry season (TerrainLod.Tints' `withered`), not autumn's rust
                    half dryT = saturate(seas / max(warm, 0.01h)) * warm;
                    half3 litter = lerp(albedo, half3(0.060h, 0.050h, 0.028h), 0.45h * dl) * (1.0h - 0.25h * dl);   // (leaf litter)
                    // (session 27, W-FOREST) pools between a carr's or a swamp's thickets where its ground is waterlogged (i.kind.z, the simulation's soil
                    // and standing water): dark water giving back a little of the sky, in patches of a few metres; far off a darker, wetter floor
                    UNITY_BRANCH
                    if (i.kind.z > 0.01h || every)
                    {
                        // (only where the wood itself stands, and never under snow - frozen and buried: s27d2 had drawn dark water over the snowy boreal
                        // woods and the edges of fields as a grey camouflage)
                        half wetK = (half)i.kind.z * smoothstep(0.05h, 0.35h, dl) * (1.0h - smoothstep(0.02h, 0.25h, snowy));
                        half pn = TriG(i.positionWS, ub, 1.0 / 23.0, dpx, dpy);
                        half pool = smoothstep(0.50h, 0.60h, pn + 0.22h * wetK - 0.10h) * wetK;
                        pool = lerp(pool, 0.45h * wetK, smoothstep(2.0h, 6.0h, (half)pxw));
                        half3 water = lerp(half3(0.030h, 0.045h, 0.042h), (half3)_TC2FogColor.rgb * 0.55h, 0.30h);
                        litter = lerp(litter, water, pool * 0.85h);
                    }
                    // near: each crown lit as the dome or spire it is, the floor between them in the shadow of the crowns toward the sun
                    half mask = 0.0h;
                    half3 nearCol = litter;
                    UNITY_BRANCH
                    if ((handOver > 0.0h && fade < 1.0h) || every)
                    {
                        // the shadow of the crowns toward the sun: is there a taller crown a few metres sunward?
                        half s0, s1, s2, k0, k1, k2;
                        Crowns(uvx + lt.yz, gxx, gyx, thr, con, s0, k0, k1);
                        Crowns(uvy + lt.xz, gxy, gyy, thr, con, s1, k0, k1);
                        Crowns(uvz + lt.xy, gxz, gyz, thr, con, s2, k0, k2);
                        half hs = s0 * ub.x + s1 * ub.y + s2 * ub.z;
                        half shade = 1.0h - 0.55h * saturate((hs - h) * 2.5h) * handOver;
                        // (pass 2b) each crown turns and drops its leaves in its own time, as the trees near the camera do (TerrainLod.PlaceForest)
                        half lvc = saturate(leaves + (frac(cid * 13.7h) - 0.5h) * 0.42h * saturate(leaves * 4.0h));
                        half bareC = saturate((lvc - 0.8h) * 5.0h), turnC = saturate((lvc - 0.1h) / 0.55h);
                        half3 broad = lerp(lerp(half3(0.046h, 0.125h, 0.036h), half3(0.090h, 0.200h, 0.040h), saturate(hue * 2.0h)), half3(0.170h, 0.262h, 0.046h), saturate(hue * 2.0h - 1.0h));
                        broad = lerp(broad, half3(broad.r * 1.6h + 0.015h, broad.g * 0.90h, broad.b * 0.60h), dry * 0.8h);   // (dry lands: olive)
                        half3 rainC = lerp(lerp(half3(0.010h, 0.059h, 0.013h), half3(0.020h, 0.089h, 0.016h), saturate(hue * 2.0h)), half3(0.040h, 0.120h, 0.017h), saturate(hue * 2.0h - 1.0h));
                        half3 dryC = lerp(half3(0.069h, 0.119h, 0.024h), half3(0.140h, 0.170h, 0.040h), hue);
                        broad = lerp(broad, lerp(rainC, dryC, saturate(seas / max(warm, 0.01h))), warm);
                        broad = lerp(broad, lerp(lerp(half3(0.52h, 0.31h, 0.035h), half3(0.42h, 0.11h, 0.025h), hue), lerp(half3(0.20h, 0.16h, 0.050h), half3(0.26h, 0.20h, 0.060h), hue), dryT), turnC);
                        broad = lerp(broad, half3(0.105h, 0.082h, 0.064h), bareC);
                        half3 needle = lerp(half3(0.020h, 0.058h, 0.028h), half3(0.040h, 0.092h, 0.042h), hue);
                        needle = lerp(needle, needle * half3(1.35h, 0.95h, 0.75h), dry * 0.5h);
                        half3 crown = lerp(broad, needle, isCon);
                        crown = lerp(crown, half3(0.78h, 0.80h, 0.84h), snowy * isCon * saturate(h * 1.4h - 0.25h) * 0.65h);
                        crown *= 0.50h + 0.50h * h;                                   // (the lower parts of a crown are in its own shade)
                        mask = smoothstep(0.02h, 0.12h, h) * lerp(1.0h, 0.4h, bareC * (1.0h - isCon)) * handOver;
                        half3 floorCol = litter * shade;
                        nearCol = lerp(floorCol, crown * lerp(1.0h, shade, 0.7h), mask);
                    }
                    // far (D2-081), where a crown is under a few pixels: the crowns' mean colour over the ground they cover, the floor between
                    // in the shade of the clumps sunward of it
                    half fc = 0.0h;
                    half3 farCol = litter;
                    UNITY_BRANCH
                    if (fade > 0.0h || every)
                    {
                        // the ground the standing crowns cover round this point, and a few metres sunward of it - the cover texture's mips
                        // average the near canopy over the pixel, so the far woods keep its clumps and glades. Past ~5 m a pixel the tile's
                        // own crowns would repeat as a pattern: there the woods are drawn from the same cover turned and scaled up - clumps of
                        // crowns some twenty metres across, the same share covered, a pattern that does not line up with the tile -, and past
                        // ~12 m a pixel from its mean; their glades and stands are the share's own
                        half fcm = CoverOf((half4)_TC2CoverMean, thr);
                        half tileFade = smoothstep(3.5h, 7.0h, (half)pxw), meanK = smoothstep(10.0h, 20.0h, (half)pxw);
                        half fb = fcm;
                        UNITY_BRANCH
                        if ((tileFade > 0.0h && meanK < 1.0h) || every)
                        {
                            float2x2 turnB = float2x2(0.8, -0.6, 0.6, 0.8);
                            const float tk = 0.413;
                            half fbt = CoverAt(mul(turnB, uvx) * tk + 0.19, mul(turnB, gxx) * tk, mul(turnB, gyx) * tk, thr) * ub.x
                                     + CoverAt(mul(turnB, uvy) * tk + 0.53, mul(turnB, gxy) * tk, mul(turnB, gyy) * tk, thr) * ub.y
                                     + CoverAt(mul(turnB, uvz) * tk + 0.11, mul(turnB, gxz) * tk, mul(turnB, gyz) * tk, thr) * ub.z;
                            fb = lerp(fbt, fcm, meanK);
                        }
                        fc = fb;
                        half fcs = fb;
                        UNITY_BRANCH
                        if (tileFade < 1.0h || every)
                        {
                            half fct = CoverAt(uvx, gxx, gyx, thr) * ub.x + CoverAt(uvy, gxy, gyy, thr) * ub.y + CoverAt(uvz, gxz, gyz, thr) * ub.z;
                            half fcst = CoverAt(uvx + lt.yz, gxx, gyx, thr) * ub.x + CoverAt(uvy + lt.xz, gxy, gyy, thr) * ub.y + CoverAt(uvz + lt.xy, gxz, gyz, thr) * ub.z;
                            fc = lerp(fct, fb, tileFade); fcs = lerp(fcst, fb, tileFade);
                        }
                        half coverK = lerp(1.0h, 0.8h, con) * lerp(1.0h, 0.45h, bare * (1.0h - con));   // (narrow conifers; bare boughs)
                        fc *= coverK; fcs *= coverK;
                        half3 summer = lerp(half3(0.072h, 0.160h, 0.036h), half3(0.120h, 0.228h, 0.044h), hue);
                        summer = lerp(summer, half3(summer.r * 1.6h + 0.015h, summer.g * 0.90h, summer.b * 0.60h), dry * 0.8h);
                        summer = lerp(summer, lerp(lerp(half3(0.014h, 0.068h, 0.015h), half3(0.032h, 0.105h, 0.017h), hue), lerp(half3(0.075h, 0.125h, 0.026h), half3(0.120h, 0.160h, 0.036h), hue), saturate(seas / max(warm, 0.01h))), warm);   // (W-FOREST)
                        half3 meanCrown = lerp(lerp(lerp(summer, lerp(half3(0.47h, 0.21h, 0.03h), half3(0.23h, 0.18h, 0.055h), dryT), turn), half3(0.105h, 0.082h, 0.064h), bare),
                                               lerp(half3(0.026h, 0.068h, 0.032h), half3(0.70h, 0.72h, 0.76h), snowy * 0.35h), con) * 0.70h;
                        // stands of a few hundred metres, older and darker or younger and lighter: the far woods' own grain (from orbit too)
                        half stand = TriG(i.positionWS, ub, 1.0 / 1700.0, dpx, dpy);
                        meanCrown *= lerp(1.0h, 0.72h + 0.56h * stand, saturate(fade * 1.5h));
                        meanCrown = lerp(meanCrown, meanCrown * half3(0.80h, 0.96h, 1.18h), saturate(stand * 2.0h - 1.0h) * fade);
                        half floorShadeF = 1.0h - 0.5h * saturate((fcs - fc) * 2.0h);
                        farCol = lerp(litter * floorShadeF, meanCrown, fc);
                    }
                    albedo = lerp(nearCol, farCol, fade);
                    // (the floor under the trees sees little of the sky)
                    floorOcc = lerp(1.0h - 0.55h * dl * (1.0h - mask), 1.0h - 0.55h * dl * (1.0h - fc), fade);
                    hm = lerp(h * 5.0, fc * 7.0, fade) * (1.0 + 0.6 * warm * (1.0 - saturate(seas / max(warm, 0.01h))));   // (W-FOREST: the rain forest's emergents)
                    bumpK = max(mask * (1.0h - fade), fade * saturate(dl * 3.0h));
                }
                // each crown - far off, each clump of crowns - bends the light it takes (a bump from its height, without tangents:
                // Mikkelsen 2010): the clumps' sunward sides lit, their far sides in shade (the derivatives here, in uniform control flow)
                {
                    float3 r1 = cross(dpy, n), r2 = cross(n, dpx);
                    float det = dot(dpx, r1);
                    float3 grad = (det < 0.0 ? -1.0 : 1.0) * (ddx(hm) * r1 + ddy(hm) * r2);
                    float3 nb = abs(det) > 1e-8 ? normalize(abs(det) * n - grad) : n;
                    nl = normalize(lerp(n, nb, bumpK));
                }
                // the realms map layer (D2-167, RealmsView): the land a lordship's villages work - their fields and built ground - filled with the realm's
                // map colour, evenly (a little of the ground's light and dark kept, so a field of any crop reads as the realm's), the lord's seat more
                // strongly than its tributaries, and a band of the colour some three pixels wide just inside the edge of that land (where its weight
                // crosses 0.3: the pixels from that contour by the weight's own derivative, so the band keeps its width from the village view to orbit);
                // fading out as the camera comes within a few hundred metres - there the standards and the carriers tell the bond; nothing where no
                // realm's village works the land, or with the layer off (no branch: the derivative runs in uniform control flow on WebGPU)
                {
                    half rw = saturate(i.realm.a);
                    half3 tint = i.realm.rgb / max(rw, 0.001h);
                    half strength = saturate(i.realmS.x / max(rw, 0.001h));
                    half vis = smoothstep(450.0h, 1300.0h, (half)dist);
                    half lum = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
                    albedo = lerp(albedo, tint * (0.62h + 0.55h * lum), rw * strength * 0.72h * vis);
                    float fwr = max(fwidth((float)rw), 1e-4);
                    half px = (half)(((float)rw - 0.30) / fwr);
                    half band = smoothstep(-0.6h, 0.4h, px) * (1.0h - smoothstep(2.4h, 3.4h, px));
                    albedo = lerp(albedo, tint * 0.85h, band * 0.92h * vis);
                }
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);
                half wrap = saturate((dot(nl, l.direction) + 0.18h) / 1.18h);
                half hemi = dot(nl, up) * 0.5h + 0.5h;
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                half3 amb = lerp(_AmbientGround.rgb, _AmbientSky.rgb, hemi) * lerp(half3(0.30h, 0.34h, 0.50h) * (half)_TC2NightLift, half3(1.0h, 1.0h, 1.0h), day) * floorOcc;
                // the shadow of the cloud between this point and the sun - the shell's own forms (session 27: TC2CloudForm.hlsl), so it falls where its cloud is
                half cloud = TC2CloudShadow(i.positionWS, _TC2SunDir.xyz, pxw, _TC2CloudScale.z);
                half3 sun = l.color * wrap * lerp(0.25h, 1.0h, l.shadowAttenuation) * 1.2h * (1.0h - cloud) * saturate(dot(up, l.direction) * 4.0h + 0.35h);
                half moon = saturate(dot(nl, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day) * (half)_TC2NightLift;
                half3 c = albedo * (amb + sun + half3(0.13h, 0.17h, 0.29h) * moon);
                // (session 27) the night side's lights: where people keep a fire (LightsView: every home lived in), a warm glow after dusk, from
                // the region view outward - nearer, the houses' own windows are lit (Instanced.shader)
                {
                    half night = saturate(-dot(up, l.direction) * 5.0h + 0.10h);
                    half farL = smoothstep(700.0h, 2600.0h, (half)dist);
                    UNITY_BRANCH
                    if (night * farL > 0.0h || every)
                    {
                        half lights = SAMPLE_TEXTURECUBE_LOD(_TC2Lights, sampler_TC2Lights, up, 0).r;
                        // (session 27, context 4) and a town's glow on the air around it: the homes' map four times coarser (~145 m a texel), so a
                        // town sits in a soft amber haze and a hamlet stays a few points
                        half halo = SAMPLE_TEXTURECUBE_LOD(_TC2Lights, sampler_TC2Lights, up, 3).r;
                        half grain = TriG(i.positionWS, bw, 1.0 / 19.0, dpx, dpy);
                        half lit = saturate(lights * 1.4h) * (0.35h + 1.3h * grain * grain);
                        // (a firelight amber in linear light: the old (1, 0.62, 0.28) was a screen colour's numbers and read from orbit as beige
                        // patches, 204/165/115 at their brightest - s27e2 orbit@21)
                        c += half3(1.0h, 0.47h, 0.11h) * (lit * lit * 2.4h + saturate(halo * 1.6h) * 0.22h) * night * farL;
                    }
                }
                // under the sea, the water's own colour by the depth of water above (Beer-Lambert): the sand of the shallows
                // shows clear and turquoise, the deep goes blue; the sea's surface on top adds only sky and sun (D2-040)
                float below = _TC2SeaRadius - length(i.positionWS) - d1 * 2.2;   // (the beach's own noise: its edge and the water's move together)
                if (below > 0.0)
                {
                    half through = (half)exp(-below / 3.2);
                    half3 waterCol = lerp(half3(0.07h, 0.42h, 0.50h), half3(0.010h, 0.052h, 0.155h), (half)saturate(below / 28.0)) * (0.3h * (half)_TC2NightLift + (1.0h - 0.3h * (half)_TC2NightLift) * day);
                    c = lerp(waterCol, c * half3(0.86h, 0.98h, 1.0h), through);
                }
                // the surf: a broken, moving line of foam just below the sea surface, where waves run up the shore
                if (below > -0.4 && below < 1.4)
                {
                    bool py = abs(up.y) > 0.5, px0 = abs(up.x) > 0.5;
                    float2 fuv = (py ? i.positionWS.xz : (px0 ? i.positionWS.yz : i.positionWS.xy));
                    float2 fgx = (py ? dpx.xz : (px0 ? dpx.yz : dpx.xy)) / 5.0, fgy = (py ? dpy.xz : (px0 ? dpy.yz : dpy.xy)) / 5.0;   // (a branch: gradients given)
                    half n1 = SAMPLE_TEXTURE2D_GRAD(_TC2Detail, sampler_TC2Detail, fuv / 5.0 + float2(_Time.y * 0.035, _Time.y * 0.02), fgx, fgy).r;
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
