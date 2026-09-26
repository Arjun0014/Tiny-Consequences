#version 140

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump float var_shade;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;
    highp vec4 u_sun;
};

void main()
{
    vec3 N = normalize(var_normal);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    float wrap = clamp((dot(N, L) + 0.6) / 1.6, 0.0, 1.0);
    vec3 shadow = vec3(0.74, 0.84, 0.95);
    vec3 col = mix(shadow, vec3(1.0, 0.995, 0.97), wrap) * mix(0.9, 1.04, var_shade);
    float edge = clamp(dot(N, V), 0.0, 1.0);
    float a = smoothstep(0.05, 0.7, edge) * 0.88;
    col += (1.0 - edge) * 0.1;
    out_color = vec4(col * a, a);
}
