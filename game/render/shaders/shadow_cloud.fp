#version 140
// Clouds in the shadow pass: only cover (alpha, max-blended by the render script's colour mask).
in mediump vec3 var_normal;
out vec4 out_color;

void main()
{
    out_color = vec4(0.0, 0.0, 0.0, 0.55);
}
