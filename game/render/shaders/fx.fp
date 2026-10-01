#version 140
in mediump vec4 var_color;
out vec4 out_color;

uniform fs_uniforms
{
    mediump vec4 u_sky;   // z = night at the view focus (0 day .. 1 night)
};

void main()
{
    // smoke (grey) is lit by the day and fades into the dark at night; flames (saturated) keep
    // their own light
    vec3 c = var_color.rgb;
    float sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    float night = clamp(u_sky.z, 0.0, 1.0);
    float dim = mix(1.0 - 0.72 * night, 1.0, smoothstep(0.15, 0.35, sat));
    out_color = vec4(c * dim * var_color.a, var_color.a);
}
