#version 140
in mediump vec4 var_color;
out vec4 out_color;

void main()
{
    out_color = vec4(var_color.rgb * var_color.a, var_color.a);
}
