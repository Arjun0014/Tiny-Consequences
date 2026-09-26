// Packed prop colours (game/view/propgen.lua): color.x = (r*128 + g)*128 + b with 7-bit
// channels; color.y = shade*8 + sway (shade 0..31 -> 0.5..1, sway 0..7 -> 0..1).
#ifndef TC_UNPACK_GLSL
#define TC_UNPACK_GLSL
vec4 tc_unpack(vec2 c)
{
    float v = floor(c.x + 0.5);
    float b = mod(v, 128.0);
    v = floor(v / 128.0);
    float g = mod(v, 128.0);
    float r = floor(v / 128.0);
    float s = floor(c.y + 0.5);
    float sway = mod(s, 8.0) / 7.0;
    float shade = floor(s / 8.0) / 62.0 + 0.5;
    return vec4(vec3(r, g, b) / 127.0 * shade, sway);
}
#endif
