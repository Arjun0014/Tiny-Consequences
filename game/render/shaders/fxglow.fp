#version 140
// (S26) Additive light for effects (the meteor's head, its plasma trail, the flash, the fireball, embers): the colour adds
// to what is behind it - alpha 0 under the fx pass's premultiplied blend (ONE, ONE_MINUS_SRC_ALPHA) - so overlapping
// glows brighten and a colour fading to black fades out. u_tint per object: rgb x intensity (a).
in mediump vec4 var_color;
out vec4 out_color;

uniform fs_uniforms
{
    mediump vec4 u_tint;
};

void main()
{
    out_color = vec4(var_color.rgb * u_tint.rgb * (u_tint.a * var_color.a), 0.0);
}
