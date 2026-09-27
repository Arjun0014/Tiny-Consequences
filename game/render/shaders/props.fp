#version 140
#include "/game/render/shaders/light.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump vec3 var_color;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;
    highp vec4 u_sun;
};

void main()
{
    vec3 up = normalize(var_wpos);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    vec3 N = normalize(var_normal);
    if (dot(N, V) < 0.0) N = -N;   // double-sided leaves / fronds
    vec3 col = tc_light(var_color, N, up, V, L, 0.0);
    // slightly glossy toy finish
    vec3 H = normalize(L + V);
    col += pow(max(dot(N, H), 0.0), 28.0) * 0.14 * vec3(1.0, 0.97, 0.9);
    col = tc_limb(col, up, V, 0.4 * tc_haze_scale(u_cam.xyz));
    out_color = vec4(col, 1.0);
}
