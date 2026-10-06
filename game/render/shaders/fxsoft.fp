#version 140
// (S26) Soft effects with a per-object fade (the meteor's smoke trail, dust, debris): fx.fp's smoke, lit by the day and
// dimmed at night, times u_tint (rgb x opacity a) so each puff can thin away on its own.
in mediump vec4 var_color;
out vec4 out_color;

uniform fs_uniforms
{
    mediump vec4 u_sky;    // z = night at the view focus
    mediump vec4 u_tint;
};

void main()
{
    vec3 c = var_color.rgb * u_tint.rgb;
    float night = clamp(u_sky.z, 0.0, 1.0);
    c *= 1.0 - 0.7 * night;
    // (the vertex opacity 0..7 arrives as 0.25..1 - fx.vp: here 0..1, so a disc's edge can fade to nothing)
    float a0 = clamp((var_color.a - 0.25) / 0.75, 0.0, 1.0);
    float a = clamp(a0 * u_tint.a, 0.0, 1.0);
    out_color = vec4(c * a, a);
}
