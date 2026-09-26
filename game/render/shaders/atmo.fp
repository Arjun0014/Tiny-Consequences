#version 140
// Atmosphere rim glow (docs/ART_DIRECTION.md §3): drawn on the front faces of a shell around
// the planet. The glow depends on the view ray's closest approach to the planet centre, so it
// hugs the silhouette at any zoom: bright thin rim, a soft outer halo, faint haze inside.

in highp vec3 var_wpos;

out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_cam;   // eye xyz, time
    highp vec4 u_atmo;  // x = planet radius, y = rim thickness, z = halo thickness, w = strength
};

void main()
{
    vec3 eye = u_cam.xyz;
    vec3 d = normalize(var_wpos - eye);
    float tc = -dot(eye, d);
    vec3 closest = eye + d * tc;
    float b = length(closest);
    float R = u_atmo.x;
    float a;
    vec3 col;
    if (b > R) {
        float x = (b - R);
        float rim = exp(-x / (R * u_atmo.y));
        float halo = exp(-x / (R * u_atmo.z));
        a = clamp(rim * 0.95 + halo * 0.5, 0.0, 1.0);
        col = mix(vec3(0.72, 0.93, 1.0), vec3(1.0, 1.0, 0.98), rim);
    } else {
        float k = b / R;
        float inner = pow(k, 14.0);
        a = inner * 0.5 + k * k * 0.03;
        col = mix(vec3(0.75, 0.93, 1.0), vec3(0.97, 1.0, 1.0), inner);
    }
    a *= u_atmo.w * (1.0 - smoothstep(1.14, 1.27, b / R));   // fade before the shell edge (1.3 R)
    out_color = vec4(col * a, a);
}
