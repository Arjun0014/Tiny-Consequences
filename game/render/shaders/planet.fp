#version 140
// Terrain + sea (renderer v2, D-048): per-pixel materials on top of the sim-cell colours.
// Land: seasonal meadow with fine grass texture, forest floor, rock on steep slopes and high
// ground (strata), beaches, drought browning, fire and ash, real farm fields (a domain-warped
// 3D lattice cut by the sphere = irregular plots with crop rows, per-plot crops and growth
// stage, hedgerows), trodden village ground, snow; micro-relief by derivative bump mapping.
// Water: depth colour, shelf halo, sandy shallows with caustics, moving shore foam, ripples,
// sun glitter, oil sheen and sea ice. Sun shadows (incl. clouds) and the DoF CoC in alpha.
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump vec4 var_land;
in mediump vec4 var_fx;
in mediump vec4 var_fx2;
in mediump vec4 var_farm;
in highp vec4 var_info;
in highp vec3 var_sc;
uniform highp sampler2D shadowtex;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;   // eye xyz, time
    highp vec4 u_sun;   // sun direction xyz (world)
    highp vec4 u_shadowp;
    highp vec4 u_focus;
};

const vec3 DEEP = vec3(0.070, 0.290, 0.640);
const vec3 MID = vec3(0.110, 0.440, 0.800);
const vec3 SHALLOW = vec3(0.160, 0.640, 0.820);
const vec3 HALO = vec3(0.420, 0.880, 0.860);
const vec3 SAND = vec3(0.945, 0.865, 0.640);
const vec3 WET_SAND = vec3(0.800, 0.700, 0.500);
const vec3 ROCK = vec3(0.47, 0.46, 0.45);
const vec3 ROCK2 = vec3(0.60, 0.58, 0.55);
const vec3 ROCK_DARK = vec3(0.32, 0.31, 0.32);
const vec3 SNOW = vec3(0.96, 0.985, 1.0);
const vec3 SEA_ICE = vec3(0.88, 0.95, 1.0);
const vec3 OIL = vec3(0.106, 0.078, 0.141);
const vec3 FIRE = vec3(1.0, 0.45, 0.12);
const vec3 SOIL = vec3(0.45, 0.33, 0.22);
const vec3 HEDGE = vec3(0.21, 0.42, 0.19);
const vec3 EARTH = vec3(0.60, 0.50, 0.38);

// Derivative bump mapping (no tangents needed): perturb N by the screen-space gradient of hgt.
vec3 tc_bump(vec3 N, highp vec3 p, highp float hgt)
{
    highp vec3 dpdx = dFdx(p);
    highp vec3 dpdy = dFdy(p);
    highp float dhdx = dFdx(hgt);
    highp float dhdy = dFdy(hgt);
    highp vec3 r1 = cross(dpdy, N);
    highp vec3 r2 = cross(N, dpdx);
    highp float det = dot(dpdx, r1);
    if (abs(det) < 1e-12) return N;
    highp vec3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * N - grad);
}

// Farm plots: returns the plot colour and writes the relief height (world units).
vec3 tc_fields(highp vec3 p, vec3 up, float stage0, float detail, out highp float hgt)
{
    highp vec3 wq = p * 6.2;
    wq += (vec3(tc_noise(p * 1.9), tc_noise(p * 1.9 + 11.3), tc_noise(p * 1.9 + 23.7)) - 0.5) * 1.6;
    highp vec3 id = floor(wq);
    highp vec3 cf = fract(wq);
    float r1 = tc_hash(id);
    float r2 = tc_hash(id + 17.0);
    float r3 = tc_hash(id + 41.0);
    // distance to the plot border, corrected for how much each lattice axis lies in the surface
    vec3 ta = max(sqrt(max(vec3(1.0) - up * up, vec3(0.0))), vec3(0.2));
    vec3 e3 = min(cf, vec3(1.0) - cf) / ta;
    float edge = min(e3.x, min(e3.y, e3.z));
    // which border is nearest, and on which side: about two thirds of the borders are hedged,
    // the rest are plain furrow edges or grass tracks
    vec3 side = step(vec3(0.5), cf);
    float bx = step(e3.x, min(e3.y, e3.z));
    float by = step(e3.y, min(e3.x, e3.z)) * (1.0 - bx);
    vec3 bsel = vec3(bx, by, 1.0 - bx - by);
    float hb = tc_hash(id * 2.0 + bsel * (side * 2.0 - 1.0) + 5.0);
    float hw = mix(0.018, 0.034, detail);
    float hedge = (1.0 - smoothstep(hw, hw * 2.0, edge)) * step(hb, 0.62);
    float track = (1.0 - smoothstep(0.008, 0.018, edge)) * (1.0 - step(hb, 0.62));
    // crop rows along one lattice axis projected onto the ground
    vec3 ax = r2 < 0.33 ? vec3(1.0, 0.0, 0.0) : (r2 < 0.66 ? vec3(0.0, 1.0, 0.0) : vec3(0.0, 0.0, 1.0));
    vec3 sd = normalize(ax - up * dot(ax, up) + vec3(1e-4));
    highp float rp = dot(p, sd) * 240.0;
    float fw = fwidth(rp);
    float rows = (0.5 + 0.5 * sin(rp)) ;
    float rowk = (1.0 - smoothstep(0.6, 1.4, fw)) * detail;
    // per-plot crop, and a stage around the cell's crop value (neighbours are sown days apart)
    float stage = clamp(stage0 + (r3 - 0.5) * 0.3, 0.0, 1.0);
    vec3 ripe = vec3(0.90, 0.75, 0.33);                          // wheat
    vec3 young = vec3(0.52, 0.70, 0.30);
    if (r1 < 0.22) ripe = vec3(0.34, 0.56, 0.22);                // vegetables: stay green
    else if (r1 < 0.36) ripe = vec3(0.87, 0.83, 0.52);           // barley
    else if (r1 < 0.46) { ripe = vec3(0.96, 0.84, 0.22); young = vec3(0.55, 0.72, 0.25); } // rapeseed
    vec3 soil = SOIL * (0.88 + 0.24 * r3);
    vec3 c = mix(soil, young, smoothstep(0.04, 0.35, stage));
    c = mix(c, ripe, smoothstep(0.5, 0.95, stage));
    // furrows show while the crop is short
    float furrow = mix(0.78 + 0.22 * rows, 0.93 + 0.07 * rows, smoothstep(0.2, 0.8, stage));
    c *= mix(1.0, furrow, rowk);
    // a few pastures and fallow plots
    if (r1 > 0.9) c = mix(vec3(0.50, 0.70, 0.30), vec3(0.62, 0.72, 0.36), r3);
    c = mix(c, vec3(0.55, 0.62, 0.36), track * 0.6);
    c = mix(c, HEDGE * (0.85 + 0.3 * tc_noise(p * 90.0)), hedge * mix(0.6, 0.95, detail));
    hgt = (rows - 0.5) * 0.0025 * rowk * (1.0 - hedge) + hedge * 0.006;
    return c;
}

void main()
{
    vec3 up = normalize(var_wpos);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    float t = u_cam.w;
    float h = var_info.x;
    float nz = var_info.y;
    float s = var_info.w;
    float dist = length(u_cam.xyz - var_wpos);
    float detail = 1.0 - smoothstep(5.0, 26.0, dist);
    vec3 col;
    float sh = tc_shadow(shadowtex, var_sc, u_shadowp);
    if (s > 0.0) {
        // ---- land
        highp float fn = tc_fbm(var_wpos * 2.6) - 0.5;
        highp float fn2 = tc_noise(var_wpos * 9.0) - 0.5;
        vec3 N0 = normalize(var_normal);
        vec3 base = var_land.rgb;
        base *= 0.95 + nz * 0.16 + fn2 * 0.08;
        base = mix(base, base * vec3(1.07, 1.05, 0.82), clamp(fn * 2.0 + 0.4, 0.0, 1.0) * 0.28);
        highp float hgt = 0.0;
        // fine grass/soil texture close to the camera
        if (detail > 0.01) {
            highp float g1 = tc_noise(var_wpos * 55.0);
            highp float g2 = tc_noise(var_wpos * 140.0);
            base *= 1.0 + ((g1 - 0.5) * 0.16 + (g2 - 0.5) * 0.12) * detail;
            hgt += (g1 * 0.004 + g2 * 0.0015) * detail;
        }
        // forest floor: darker, mossy under canopy
        float fo = var_farm.w;
        base = mix(base, base * vec3(0.78, 0.86, 0.74), smoothstep(0.3, 0.8, fo) * 0.5);
        // drought cracks and browning
        float dry = var_fx2.y;
        // rock: steep slopes and high ground, with strata
        float slope = 1.0 - dot(N0, up);
        float rockk = max(smoothstep(0.10, 0.22, slope + fn * 0.08), smoothstep(2.6, 3.6, h + nz * 0.8 + fn * 0.6));
        rockk *= 1.0 - smoothstep(0.3, 0.7, var_farm.x);
        if (rockk > 0.01) {
            // (S14) grey crags: broken faces (cellular-ish noise), dark clefts on the steep parts,
            // a hint of bedding only on gentle rock
            float crag = tc_noise(var_wpos * 11.0 + vec3(fn2 * 2.0));
            float crack = smoothstep(0.72, 0.9, tc_noise(var_wpos * 46.0));
            float strata = 0.5 + 0.5 * sin(h * 38.0 + fn * 7.0 + fn2 * 3.0);
            vec3 rc = mix(ROCK, ROCK2, crag) * (0.88 + 0.24 * tc_noise(var_wpos * 30.0));
            rc = mix(rc, ROCK_DARK, smoothstep(0.25, 0.55, slope) * 0.55 + crack * 0.35);
            rc = mix(rc, rc * (0.92 + 0.12 * strata), 1.0 - smoothstep(0.1, 0.3, slope));
            base = mix(base, rc, rockk * 0.9);
            hgt += rockk * (crag * 0.03 - crack * 0.012);
        }
        // beaches (wet sand at the waterline)
        float beach = smoothstep(0.085, 0.03, s + fn * 0.05) * smoothstep(0.45, 0.08, h);
        vec3 sandc = mix(SAND, WET_SAND, smoothstep(0.035, 0.0, s)) * (0.95 + 0.1 * tc_noise(var_wpos * 70.0));
        base = mix(base, sandc, beach * 0.92);
        // farm plots
        float farmk = smoothstep(0.3, 0.62, var_farm.x + fn * 0.25) * (1.0 - beach);
        if (farmk > 0.01) {
            highp float fh = 0.0;
            vec3 fc = tc_fields(var_wpos, up, var_farm.y, detail, fh);
            // blight: the crops rot purple-brown in blotches
            float bl = var_fx2.w;
            if (bl > 0.02) {
                float blot = smoothstep(0.35, 0.65, tc_noise(var_wpos * 30.0) + bl * 0.5);
                fc = mix(fc, vec3(0.42, 0.26, 0.36) * (0.8 + 0.3 * tc_noise(var_wpos * 90.0)), blot * min(1.0, bl * 1.4));
            }
            base = mix(base, fc, farmk);
            hgt += fh * farmk;
        }
        // village ground: trodden earth with grassy patches
        float vil = smoothstep(0.62, 0.95, var_farm.z + fn * 0.25);
        if (vil > 0.01) {
            float patchy = smoothstep(0.35, 0.65, tc_noise(var_wpos * 26.0) + fn2 * 0.3);
            vec3 ground = mix(EARTH * (0.92 + 0.16 * tc_noise(var_wpos * 80.0)), base, patchy * 0.6);
            base = mix(base, ground, vil * 0.75);
        }
        base = mix(base, base * vec3(1.12, 0.98, 0.72), dry * 0.35);
        // snow only where the ice field is substantial; soft edge, little noise (no mottling)
        float snow = smoothstep(0.5, 0.85, var_land.a + fn * 0.08);
        base = mix(base, SNOW, snow);
        hgt *= 1.0 - snow * 0.7;
        vec3 N = N0;
        if (detail > 0.01) N = tc_bump(N0, var_wpos, hgt);
        col = tc_light_sh(base, N, up, V, L, snow * 0.15, sh);
        // snow sparkle
        if (snow > 0.5 && detail > 0.01) {
            float sp = step(0.985, tc_hash(floor(var_wpos * 400.0)));
            col += sp * snow * detail * sh * 0.5;
        }
        float fire = var_fx.r;
        if (fire > 0.01) {
            // (S24) a cell burns in places - its front, its hot spots - over the share of its ground its fire's intensity
            // (the fuel alight) stands for; the rest smoulders as ash under a faint glow. (A wash of orange over the whole
            // cell read as paint, and as a fire far bigger than the one burning.)
            highp vec3 fp = var_wpos * 7.0;
            float pn = tc_noise(fp + vec3(0.0, t * 0.04, 0.0)) * 0.65 + tc_noise(fp * 3.3 + vec3(t * 0.15, 0.0, -t * 0.1)) * 0.35;
            float cover = clamp(fire * 1.4, 0.0, 1.0);
            float th = mix(0.80, 0.18, cover);
            float alight = smoothstep(th, th + 0.06, pn);
            float fl = 0.75 + 0.25 * sin(t * 9.0 + dot(var_wpos, vec3(13.0, 7.0, 11.0)));
            float ember = tc_noise(var_wpos * 40.0 + vec3(0.0, t * 0.8, 0.0));
            col = mix(col, FIRE * (1.0 + 0.5 * fl * ember), alight * 0.95);
            col += FIRE * 0.10 * cover * (1.0 - alight);
        }
    } else {
        // ---- water
        float depth = max(-h, 0.0);
        vec3 wc = mix(HALO, SHALLOW, smoothstep(0.0, 0.25, depth));
        wc = mix(wc, MID, smoothstep(0.2, 1.0, depth));
        wc = mix(wc, DEEP, smoothstep(1.0, 3.5, depth));
        wc = mix(wc, vec3(0.20, 0.62, 0.55), clamp(var_fx.b, 0.0, 1.0) * 0.22);
        // sandy shallows with drifting caustics
        float shallow = smoothstep(0.12, 0.0, depth) * smoothstep(-0.12, -0.02, s);
        if (shallow > 0.01 && detail > 0.01) {
            highp vec3 cq = var_wpos * 38.0 + vec3(t * 0.3, t * 0.2, -t * 0.25);
            float ca = abs(tc_noise(cq) - 0.5) + abs(tc_noise(cq * 1.7 + 5.0) - 0.5);
            float caus = smoothstep(0.18, 0.0, ca) * detail;
            wc = mix(wc, mix(vec3(0.62, 0.86, 0.80), vec3(0.92, 0.98, 0.9), caus), shallow * 0.55);
        }
        // ripple normal from animated value noise (irregular; no lattice patterns)
        highp vec3 q = var_wpos * 9.0 + vec3(t * 0.21, t * 0.13, -t * 0.17);
        highp vec3 q2 = var_wpos * 23.0 + vec3(-t * 0.33, t * 0.27, t * 0.19);
        vec3 rn = vec3(tc_noise(q), tc_noise(q + 19.1), tc_noise(q + 37.7)) - 0.5;
        rn += 0.6 * (vec3(tc_noise(q2), tc_noise(q2 + 11.3), tc_noise(q2 + 29.9)) - 0.5);
        if (detail > 0.01) {
            highp vec3 q3 = var_wpos * 70.0 + vec3(t * 0.6, -t * 0.4, t * 0.5);
            rn += 0.35 * detail * (vec3(tc_noise(q3), tc_noise(q3 + 7.7), tc_noise(q3 + 13.1)) - 0.5);
        }
        rn *= 2.0;
        rn -= up * dot(rn, up);
        highp float wn = tc_fbm(var_wpos * 1.3 + vec3(t * 0.02, 0.0, 0.0)) - 0.5;
        vec3 N = normalize(up + rn * 0.034);
        float ripple = dot(rn, vec3(0.3, 0.4, 0.3));
        wc *= 0.96 + 0.035 * ripple + wn * 0.12;
        col = tc_light_sh(wc, N, up, V, L, 0.06, mix(0.6, 1.0, sh));
        vec3 H = normalize(L + V);
        vec3 Ns = normalize(up + rn * 0.02);
        float spec = pow(max(dot(Ns, H), 0.0), 420.0) * 0.9 + pow(max(dot(N, H), 0.0), 60.0) * 0.04;
        // glitter: tiny sparkles in the sun path
        if (detail > 0.01) {
            float gl = step(0.992, tc_hash(floor(var_wpos * 260.0 + vec3(0.0, t * 1.5, 0.0))));
            spec += gl * pow(max(dot(N, H), 0.0), 18.0) * detail * 1.4;
        }
        float fres = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 5.0);
        col = mix(col, vec3(0.70, 0.90, 1.0), fres * 0.35);
        // shore foam: a steady line at the waterline plus bands rolling in
        float fl = -s;
        float foam = smoothstep(0.02, 0.0, fl + 0.012 * sin(t * 1.5 + dot(var_wpos, vec3(9.0, 7.0, 8.0))));
        float band = sin(fl * 160.0 + t * 2.2);
        foam += smoothstep(0.75, 0.98, band) * smoothstep(0.07, 0.02, fl) * 0.6 * (0.6 + 0.4 * tc_noise(var_wpos * 25.0));
        col = mix(col, vec3(0.94, 0.98, 0.97), clamp(foam, 0.0, 1.0) * 0.6);
        // oil slick with a purple sheen
        float oil = clamp(var_fx.g, 0.0, 1.0);
        if (oil > 0.01) {
            float sheen = 0.5 + 0.5 * sin(dot(var_wpos, vec3(21.0, 17.0, 19.0)) + t * 0.7);
            col = mix(col, mix(OIL, vec3(0.24, 0.17, 0.35), sheen * 0.6), oil * 0.9);
            spec *= 1.0 - oil * 0.5;
        }
        // sea ice: matte, bright
        highp float inz = tc_fbm(var_wpos * 3.1) - 0.5;
        highp float inz2 = tc_fbm(var_wpos * 0.45 + 3.7) - 0.5;
        float ice = smoothstep(0.44, 0.54, var_fx.a + inz * 0.3 + inz2 * 0.9);
        float floe = tc_noise(var_wpos * 7.0);
        vec3 icecol = tc_light_sh(SEA_ICE * (0.92 + 0.1 * floe), up, up, V, L, 0.08, sh);
        icecol = mix(icecol, vec3(0.62, 0.84, 0.95), smoothstep(0.35, 0.0, ice) * 0.5);
        col = mix(col, icecol, ice);
        col += spec * sh * (1.0 - ice) * vec3(1.0, 0.97, 0.9);
    }
    col = tc_limb(col, up, V, 0.45 * tc_haze_scale(u_cam.xyz));
    out_color = vec4(col, tc_coc(var_wpos, u_cam.xyz, u_focus));
}
