#version 140
// 2x downsample with a 4-tap bilinear box (= 4x4 source texels).
in highp vec2 var_uv;
out vec4 out_color;
uniform highp sampler2D tex0;

uniform fs_uniforms
{
    highp vec4 u_pdir;   // xy = source texel size
};

void main()
{
    highp vec2 t = u_pdir.xy;
    vec4 c = texture(tex0, var_uv + vec2(-t.x, -t.y))
           + texture(tex0, var_uv + vec2(t.x, -t.y))
           + texture(tex0, var_uv + vec2(-t.x, t.y))
           + texture(tex0, var_uv + vec2(t.x, t.y));
    out_color = c * 0.25;
}
