#version 140
// River water: teal-blue with flowing ripples, bright glints, wet muddy banks.
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in highp vec3 var_uvw;
in highp vec3 var_sc;
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
    vec3 up = normalize(var_normal);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    float t = u_cam.w;
    float across = abs(var_uvw.y);
    highp vec3 q = vec3(var_uvw.x * 14.0 - t * 1.6, var_uvw.y * 1.5, 0.0);
    float r1 = tc_noise(q);
    float r2 = tc_noise(q * 2.3 + vec3(3.1, 0.0, 1.7));
    vec3 deep = vec3(0.12, 0.42, 0.62);
    vec3 shallow = vec3(0.26, 0.62, 0.66);
    vec3 col = mix(deep, shallow, smoothstep(0.2, 0.9, across));
    col *= 0.92 + 0.12 * r1;
    // muddy, wet banks at the edges
    float bank = smoothstep(0.72, 1.0, across);
    col = mix(col, vec3(0.42, 0.37, 0.27), bank * 0.8);
    float sh = tc_shadow(shadowtex, var_sc, u_shadowp);
    vec3 N = normalize(up + (vec3(r1, r2, tc_noise(q + 7.0)) - 0.5) * 0.12);
    vec3 c = tc_light_sh(col, N, up, V, L, 0.05, sh);
    vec3 H = normalize(L + V);
    c += pow(max(dot(N, H), 0.0), 120.0) * 0.45 * sh * (1.0 - bank);
    c += (1.0 - smoothstep(0.0, 0.25, r2)) * 0.06 * (1.0 - bank);
    out_color = vec4(c, tc_coc(var_wpos, u_cam.xyz, u_focus));
}
