// TC2 cloud forms (session 27: the planet from orbit), shared by the cloud shell (Clouds.shader) and the cloud shadows on the ground and the sea
// (Terrain.shader, Water.shader), so a shadow falls where its cloud is. What the sky holds is the world's weather - _TC2Clouds (CloudView): r the
// rain's decks and the storms' bands, g fair-weather cumulus where the sun heats moist ground (and over warm moist seas), b the layered cloud of a
// damp day round the rain (the weather's own draw, near rain), a how warm - convective - the air is. The forms are a tileable 3-D noise
// (_TC2CloudNoise, CloudNoise.cs: r large shapes, g billows, b puffs, a fine grain) laid round the planet and drawn out along the parallels. All
// lookups take an explicit level of detail, so they may run in any control flow (WebGPU) and from the ground's shader.
#ifndef TC2_CLOUD_FORM_INCLUDED
#define TC2_CLOUD_FORM_INCLUDED

TEXTURECUBE(_TC2Clouds); SAMPLER(sampler_TC2Clouds);
TEXTURE3D(_TC2CloudNoise); SAMPLER(sampler_TC2CloudNoise);
float4 _TC2CloudForm;    // x the decks' and layers' density, y the cumulus' cover, z the relief, w the forms drawn out along the parallels (1 round)
float4 _TC2CloudScale;   // x the noise's tile (m), y the shell's radius (m), z the forms' clock (s), w the shadows' strength
float _TC2CloudShell;    // the shell's radius (m)

// the weather's cover over a direction (the cube is coarse and smooth: its finest level)
half4 TC2Cover(float3 dir) { return SAMPLE_TEXTURECUBE_LOD(_TC2Clouds, sampler_TC2Clouds, dir, 0); }

// a direction from the planet's centre -> the noise's domain, in tiles: the circles of latitude (x, z) scaled down by the stretch and the arc
// along the meridian (y), so the forms lie east-west at every latitude; the whole turning slowly eastward (no shear between latitudes, ever) while
// the circles breathe a little, so the forms change as they drift
float3 TC2CloudP(float3 dir, float t)
{
    float L = _TC2CloudScale.y / _TC2CloudScale.x;
    float s = max(1.0, _TC2CloudForm.w);
    float a = t * 0.0011, ca = cos(a), sa = sin(a);
    float2 xz = float2(ca * dir.x - sa * dir.z, sa * dir.x + ca * dir.z);
    float breathe = 1.0 + 0.035 * sin(t * 0.0045);
    return float3(xz.x * L / s * breathe, asin(clamp(dir.y, -1.0, 1.0)) * L, xz.y * L / s * breathe);
}

half4 TC2Noise(float3 q, float lod) { return SAMPLE_TEXTURE3D_LOD(_TC2CloudNoise, sampler_TC2CloudNoise, q, lod); }

// The cloud over a direction: its density (0..1, how much of the light behind it it hides) and its thickness (how much it shades below and how
// bright its top), from the cover and two looks at the forms - at the tile's scale and three and a half times finer. lod: the level of detail of
// the coarser look (log2 of the noise texels a pixel spans).
half TC2CloudDensity(float3 dir, half4 cover, float lod, float t, out half thick, out half billow)
{
    half deck = cover.r * (half)_TC2CloudForm.x, cu = cover.g * (half)_TC2CloudForm.y, layer = cover.b * (half)_TC2CloudForm.x, conv = cover.a;
    thick = 0.0h; billow = 1.0h;
    if (deck + cu + layer < 0.01h) return 0.0h;
    float3 q = TC2CloudP(dir, t);
    half4 a = TC2Noise(q, max(0.0, lod));
    half4 b = TC2Noise(q * 3.71 + float3(0.37, 0.11, 0.73), max(0.0, lod + 1.89));
    // a third look, 14 times finer than the tile, where a pixel is small enough to see it (the region view and nearer): ragged, crisp edges
    half fineW = (half)saturate(-0.5 - (lod + 1.89) * 0.5);
    half4 f = fineW > 0.0h ? TC2Noise(q * 14.3 + float3(0.71, 0.29, 0.13), max(0.0, lod + 3.84)) : half4(0.5h, 0.5h, 0.5h, 0.5h);
    // near the poles the circles shrink to nothing: the plain 3-D position there
    half polar = smoothstep(0.86h, 0.95h, (half)abs(dir.y));
    if (polar > 0.0h)
    {
        float3 qp = dir * (_TC2CloudScale.y / _TC2CloudScale.x) * 0.8;
        a = lerp(a, TC2Noise(qp, max(0.0, lod)), polar);
        b = lerp(b, TC2Noise(qp * 3.71 + float3(0.37, 0.11, 0.73), max(0.0, lod + 1.89)), polar);
    }
    // the large shapes, their edges frayed by the finer grain
    half shape = a.r + (b.a - 0.5h) * 0.30h + (b.r - 0.5h) * 0.20h + (f.r - 0.5h) * 0.16h * fineW;
    // the edges' softness: a pixel's share of the forms' finest scale - soft from orbit, crisp near
    half soft = (half)clamp(0.03 + 0.05 * (lod + 3.0), 0.03, 0.2);
    // decks: the rain's cover with ragged, billowing edges; where the air is warm the deck is heaped into clusters of towers
    half dk = smoothstep(0.44h - soft * 2.0h, 0.44h + soft * 2.0h, deck + (shape - 0.5h) * 0.80h);
    dk *= lerp(1.0h, 0.45h + 0.75h * a.g, conv * 0.85h);
    // layers: thinner sheets round the rain - a damp day's grey - torn at their edges
    half ly = smoothstep(0.55h - soft * 2.2h, 0.55h + soft * 2.2h, layer + (shape - 0.5h) * 0.95h);
    // a layer is broken into the cells of stratocumulus, rows of lumps with clear lanes between
    ly *= smoothstep(0.30h, 0.62h, a.g * 0.6h + b.g * 0.4h + (f.g - 0.5h) * 0.2h * fineW);
    // cumulus: separate puffs on the finer lattice where the shapes let them form (fields with clear gaps), each ragged and of its own size
    half formed = smoothstep(1.0h - cu * 0.95h, 1.0h - cu * 0.95h + 0.22h, a.r * 0.75h + a.a * 0.25h + 0.06h);
    half rad = 0.32h + 0.22h * a.g;
    half puff = 1.0h - smoothstep(rad * (0.85h - soft * 2.0h), rad, b.b + (b.a - 0.5h) * 0.22h + (f.a - 0.5h) * 0.10h * fineW);
    half cud = puff * formed;
    half density = 1.0h - (1.0h - dk) * (1.0h - ly) * (1.0h - cud);
    // the heaped tops' light and shade: the billows at three scales (the cauliflower of a convective deck, the lumps of a layer)
    billow = saturate(a.g * 0.45h + b.g * 0.35h + lerp(0.5h, f.g, fineW) * 0.20h);
    thick = dk * (1.3h + 0.9h * conv) * (0.75h + 0.5h * a.g) + ly * (0.55h + 0.35h * b.g) + cud * (0.7h + 0.6h * (1.0h - b.b));
    return density;
}

// The share of the sun's light a cloud above a point takes (0..1): the point's way to the sun meets the shell; footprint: the metres a pixel
// spans on the ground there (for the forms' level of detail).
half TC2CloudShadow(float3 positionWS, float3 sunDir, float footprint, float t)
{
    float r = length(positionWS);
    float up = max(0.05, dot(positionWS / r, sunDir));
    float3 toCloud = normalize(positionWS + sunDir * (max(0.0, _TC2CloudShell - r) / up));
    half4 cover = TC2Cover(toCloud);
    float texel = _TC2CloudScale.x / 64.0;
    float lod = log2(max(footprint / texel, 1e-3)) + 0.5;
    half thick, billow;
    half d = TC2CloudDensity(toCloud, cover, lod, t, thick, billow);
    return saturate((1.0h - exp(-thick * 2.2h)) * d * 1.15h) * (half)_TC2CloudScale.w;
}

#endif
