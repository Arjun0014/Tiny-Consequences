#version 140
#include "/game/render/shaders/shadow.glsl"
out vec4 out_color;

void main()
{
    out_color = vec4(tc_pack24(gl_FragCoord.z), 0.0);
}
