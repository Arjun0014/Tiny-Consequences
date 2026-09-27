#version 140
// Separable 9-tap gaussian (bilinear taps: 5 fetches) along u_pdir.xy (source texel step).
in highp vec2 var_uv;
out vec4 out_color;
uniform highp sampler2D tex0;

uniform fs_uniforms
{
    highp vec4 u_pdir;
};

void main()
{
    highp vec2 d = u_pdir.xy;
    vec4 c = texture(tex0, var_uv) * 0.2270270270;
    c += texture(tex0, var_uv + d * 1.3846153846) * 0.3162162162;
    c += texture(tex0, var_uv - d * 1.3846153846) * 0.3162162162;
    c += texture(tex0, var_uv + d * 3.2307692308) * 0.0702702703;
    c += texture(tex0, var_uv - d * 3.2307692308) * 0.0702702703;
    out_color = c;
}
