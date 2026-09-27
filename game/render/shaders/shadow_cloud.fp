#version 140
// Clouds in the shadow pass: only cover (alpha, summed by the render script's blend), a soft disc
// per puff (heavier under rain decks and storms).
in mediump vec3 var_disc;
out vec4 out_color;

void main()
{
    float d = length(var_disc.xy);
    float a = (1.0 - smoothstep(0.55, 1.0, d)) * var_disc.z;
    if (a < 0.004) discard;
    out_color = vec4(0.0, 0.0, 0.0, a);
}
