#version 140
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump vec3 var_color;
in highp vec3 var_sc;
in mediump vec3 var_mat;
in mediump float var_bare;
uniform highp sampler2D shadowtex;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;
    highp vec4 u_sun;
    highp vec4 u_shadowp;
    highp vec4 u_focus;
    highp vec4 u_canopy;
};

void main()
{
    vec3 up = normalize(var_wpos);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    if (var_bare < -0.5) {
        // a window lit by the hearth inside: unlit, warm, the fire's slow flicker
        float fl = 0.92 + 0.08 * sin(u_cam.w * 5.0 + dot(var_wpos, vec3(41.0, 23.0, 37.0)));
        vec3 wc = tc_limb(var_color * 1.35 * fl, up, V, 0.4 * tc_haze_scale(u_cam.xyz));
        out_color = vec4(wc, tc_coc(var_wpos, u_cam.xyz, u_focus));
        return;
    }
    vec3 L = normalize(u_sun.xyz);
    // (S19) close in, the canopy opens over the one the camera looks at: foliage (classes 1, 2, 4)
    // above head height near the line from the look target to the eye is thinned away by a
    // world-space stipple, the trunks left standing (the forest stays as dense as it is)
    if (u_canopy.w > 0.0 && (abs(var_mat.z - 1.0) < 0.5 || abs(var_mat.z - 2.0) < 0.5 || abs(var_mat.z - 4.0) < 0.5)) {
        highp vec3 d = var_wpos - u_canopy.xyz;
        highp float hgt = dot(d, normalize(u_canopy.xyz));
        if (hgt > 0.012) {
            highp vec3 a = normalize(u_cam.xyz - u_canopy.xyz);
            highp float t = dot(d, a);
            highp float rr = length(d - a * t);
            // (a cone: wider toward the eye, where a crown hides most of the view)
            highp float rw = u_canopy.w + max(t, 0.0) * 0.45;
            float k = (1.0 - smoothstep(rw * 0.55, rw, rr)) * smoothstep(0.012, 0.02, hgt);
            if (t > -u_canopy.w * 0.3 && k > 0.0) {
                highp vec3 q = floor(var_wpos * 600.0);
                float n = fract(sin(dot(q, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
                if (n < k * 0.97) discard;
            }
        }
    }
    // bare deciduous crowns in winter: a stable world-space stipple leaves only twigs
    if (var_bare > 0.01) {
        highp vec3 q = floor(var_wpos * 160.0);
        float n = fract(sin(dot(q, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
        if (n < var_bare) discard;
    }
    vec3 N = normalize(var_normal);
    if (dot(N, V) < 0.0) N = -N;   // double-sided leaves / fronds
    float sh = tc_shadow(shadowtex, var_sc, u_shadowp);
    vec3 albedo = var_color;
    // snow settles on upward-facing surfaces
    float snow = var_mat.x * smoothstep(0.1, 0.55, dot(N, up));
    albedo = mix(albedo, vec3(0.95, 0.97, 1.0), snow);
    vec3 col = tc_light_sh(albedo, N, up, V, L, 0.0, sh);
    if (var_mat.y > 0.02) {
        float fl = 0.7 + 0.3 * sin(u_cam.w * 11.0 + dot(var_wpos, vec3(31.0, 17.0, 23.0)));
        col = mix(col, vec3(1.0, 0.42, 0.1) * (0.8 + 0.6 * fl), clamp(var_mat.y * 1.5, 0.0, 0.85));
    }
    // slightly glossy toy finish
    vec3 H = normalize(L + V);
    col += pow(max(dot(N, H), 0.0), 28.0) * 0.14 * sh * vec3(1.0, 0.97, 0.9);
    col = tc_limb(col, up, V, 0.4 * tc_haze_scale(u_cam.xyz));
    out_color = vec4(col, tc_coc(var_wpos, u_cam.xyz, u_focus));
}
