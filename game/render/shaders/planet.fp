#version 140
#include "/game/render/shaders/light.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump vec4 var_land;
in mediump vec4 var_fx;
in highp vec4 var_info;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;   // eye xyz, time
    highp vec4 u_sun;   // sun direction xyz (world), unused
};

const vec3 DEEP = vec3(0.090, 0.330, 0.700);
const vec3 MID = vec3(0.120, 0.470, 0.840);
const vec3 SHALLOW = vec3(0.180, 0.680, 0.860);
const vec3 HALO = vec3(0.450, 0.910, 0.880);
const vec3 SAND = vec3(0.949, 0.874, 0.655);
const vec3 ROCK = vec3(0.78, 0.74, 0.70);
const vec3 SNOW = vec3(0.96, 0.985, 1.0);
const vec3 SEA_ICE = vec3(0.88, 0.95, 1.0);
const vec3 OIL = vec3(0.106, 0.078, 0.141);
const vec3 FIRE = vec3(1.0, 0.45, 0.12);

void main()
{
    vec3 up = normalize(var_wpos);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    float t = u_cam.w;
    float h = var_info.x;
    float nz = var_info.y;
    float s = var_info.w;
    vec3 col;
    if (s > 0.0) {
        // ---- land
        highp float fn = tc_fbm(var_wpos * 2.6) - 0.5;
        highp float fn2 = tc_noise(var_wpos * 9.0) - 0.5;
        vec3 base = var_land.rgb;
        base *= 0.94 + nz * 0.18 + fn2 * 0.08;
        base = mix(base, base * vec3(1.08, 1.06, 0.80), clamp(fn * 2.0 + 0.4, 0.0, 1.0) * 0.3);
        float beach = smoothstep(0.085, 0.03, s + fn * 0.05) * smoothstep(0.45, 0.08, h);
        base = mix(base, SAND, beach * 0.92);
        float rock = smoothstep(1.7, 2.6, h + nz * 0.8 + fn * 0.6);
        base = mix(base, ROCK, rock * 0.45);
        float snow = smoothstep(0.42, 0.62, var_land.a + fn * 0.35);
        snow = max(snow, smoothstep(2.5, 3.0, h + fn * 1.2));
        base = mix(base, SNOW, snow);
        vec3 N = normalize(var_normal);
        col = tc_light(base, N, up, V, L, snow * 0.15);
        float fire = var_fx.r;
        if (fire > 0.01) {
            float fl = 0.75 + 0.25 * sin(t * 9.0 + dot(var_wpos, vec3(13.0, 7.0, 11.0)));
            col = mix(col, FIRE * (1.1 + 0.3 * fl), clamp(fire * 1.6, 0.0, 0.9));
        }
    } else {
        // ---- water
        float depth = max(-h, 0.0);
        vec3 wc = mix(HALO, SHALLOW, smoothstep(0.0, 0.25, depth));
        wc = mix(wc, MID, smoothstep(0.2, 1.0, depth));
        wc = mix(wc, DEEP, smoothstep(1.0, 3.5, depth));
        wc = mix(wc, vec3(0.20, 0.62, 0.55), clamp(var_fx.b, 0.0, 1.0) * 0.25);
        // ripple normal from animated value noise (irregular; no lattice patterns)
        highp vec3 q = var_wpos * 9.0 + vec3(t * 0.21, t * 0.13, -t * 0.17);
        highp vec3 q2 = var_wpos * 23.0 + vec3(-t * 0.33, t * 0.27, t * 0.19);
        vec3 rn = vec3(tc_noise(q), tc_noise(q + 19.1), tc_noise(q + 37.7)) - 0.5;
        rn += 0.6 * (vec3(tc_noise(q2), tc_noise(q2 + 11.3), tc_noise(q2 + 29.9)) - 0.5);
        rn *= 2.0;
        rn -= up * dot(rn, up);
        highp float wn = tc_fbm(var_wpos * 1.3 + vec3(t * 0.02, 0.0, 0.0)) - 0.5;
        vec3 N = normalize(up + rn * 0.032);
        float ripple = dot(rn, vec3(0.3, 0.4, 0.3));
        wc *= 0.96 + 0.035 * ripple + wn * 0.12;
        col = tc_light(wc, N, up, V, L, 0.06);
        vec3 H = normalize(L + V);
        vec3 Ns = normalize(up + rn * 0.018);
        float spec = pow(max(dot(Ns, H), 0.0), 380.0) * 0.9 + pow(max(dot(N, H), 0.0), 40.0) * 0.05;
        float fres = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 5.0);
        col = mix(col, vec3(0.70, 0.90, 1.0), fres * 0.35);
        // coastal foam line
        float foam = smoothstep(-0.07, -0.005, s + 0.012 * sin(t * 1.5 + dot(var_wpos, vec3(9.0, 7.0, 8.0))));
        col = mix(col, vec3(0.93, 0.98, 0.97), foam * 0.55);
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
        float ice = smoothstep(0.44, 0.54, var_fx.r + inz * 0.3 + inz2 * 0.9);
        float floe = tc_noise(var_wpos * 7.0);
        vec3 icecol = tc_light(SEA_ICE * (0.92 + 0.1 * floe), up, up, V, L, 0.08);
        icecol = mix(icecol, vec3(0.62, 0.84, 0.95), smoothstep(0.35, 0.0, ice) * 0.5);
        col = mix(col, icecol, ice);
        col += spec * (1.0 - ice) * vec3(1.0, 0.97, 0.9);
    }
    col = tc_limb(col, up, V, 0.45);
    out_color = vec4(col, 1.0);
}
