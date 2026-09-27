#version 140

in highp vec3 var_wpos;
in mediump vec3 var_normal;
in mediump float var_shade;
in mediump vec3 var_tint;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;
    highp vec4 u_sun;
    highp vec4 u_cut;     // xyz: view focus on the ground (world), w: cut-away strength (zoomed in)
};

void main()
{
    vec3 N = normalize(var_normal);
    vec3 V = normalize(u_cam.xyz - var_wpos);
    vec3 L = normalize(u_sun.xyz);
    float wrap = clamp((dot(N, L) + 0.6) / 1.6, 0.0, 1.0);
    vec3 shadow = vec3(0.74, 0.84, 0.95);
    vec3 col = mix(shadow, vec3(1.0, 0.995, 0.97), wrap) * mix(0.9, 1.04, var_shade) * var_tint;
    float edge = clamp(dot(N, V), 0.0, 1.0);
    // soft, fluffy silhouettes: wide alpha falloff broken up by a little noise
    float n = fract(sin(dot(floor(var_wpos * 40.0), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
    float a = pow(smoothstep(0.0, 0.85, edge + (n - 0.5) * 0.12), 0.9) * 0.9;
    // cut-away: fade what lies between the eye and the view focus, in a cone that widens toward
    // the ground (the sky around stays full of clouds)
    if (u_cut.w > 0.0) {
        vec3 D = u_cut.xyz - u_cam.xyz;
        float dd = dot(D, D);
        vec3 F = var_wpos - u_cam.xyz;
        float t = clamp(dot(F, D) / dd, 0.0, 1.2);
        float perp = length(F - D * t);
        float rad = sqrt(dd) * (0.08 + 0.32 * t);
        float keep = smoothstep(rad * 0.55, rad * 1.15, perp);
        a *= mix(1.0, keep, u_cut.w * step(t, 1.05));
    }
    col += (1.0 - edge) * 0.12;
    // night side: moonlit grey-blue clouds; warm edges at the terminator (D-050)
    float sd = dot(normalize(var_wpos), L);
    float day = smoothstep(-0.30, 0.12, sd);
    float dusk = smoothstep(-0.25, 0.0, sd) * (1.0 - smoothstep(0.0, 0.25, sd));
    col = col * mix(vec3(0.16, 0.19, 0.30), vec3(1.0), day) + vec3(0.30, 0.12, 0.02) * dusk;
    out_color = vec4(col * a, a);
}
