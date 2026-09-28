#version 140
// Trails (W7, D-056): paths worn by the people who walk between villages (the sim's W.S.trails),
// drawn as earthen ribbons: trampled grass at the edges of a faint track, packed bare earth down
// the middle of a busy one; frayed edges.
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in highp vec3 var_uvw;   // x = distance along, y = across (-1..1), z = wear (0..1)
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
    float across = abs(var_uvw.y);
    float wear = var_uvw.z;
    float n = tc_noise(vec3(var_uvw.x * 40.0, var_uvw.y * 3.0, 0.0));
    float n2 = tc_noise(var_wpos * 60.0);
    // frayed edges: a narrow track when faint, a broad packed road when busy
    if (across > 0.55 + 0.35 * wear + (n - 0.5) * 0.5) discard;
    if (wear < 0.35 && n2 < 0.35 - wear) discard;
    vec3 grass = vec3(0.50, 0.55, 0.32);
    vec3 earth = vec3(0.56, 0.45, 0.31);
    vec3 packed = vec3(0.64, 0.55, 0.42);
    vec3 col = mix(grass, earth, smoothstep(0.1, 0.6, wear + (1.0 - across) * 0.3));
    col = mix(col, packed, smoothstep(0.6, 1.0, wear) * (1.0 - across));
    col *= 0.9 + 0.2 * n2;
    float sh = tc_shadow(shadowtex, var_sc, u_shadowp);
    vec3 c = tc_light_sh(col, up, up, V, L, 0.05, sh);
    out_color = vec4(c, tc_coc(var_wpos, u_cam.xyz, u_focus));
}
