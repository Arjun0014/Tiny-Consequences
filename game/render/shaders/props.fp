#version 140
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump vec3 var_color;
in highp vec3 var_sc;
in mediump vec3 var_mat;
uniform highp sampler2D shadowtex;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;
    highp vec4 u_sun;
    highp vec4 u_shadowp;
    highp vec4 u_focus;
};

void main()
{
    vec3 up = normalize(var_wpos);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
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
