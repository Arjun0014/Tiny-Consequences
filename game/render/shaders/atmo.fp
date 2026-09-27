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
    highp vec4 u_sun;   // xyz = direction to the sun (the real sun since D-050)
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
    // the glow is sunlight scattered by the air: bright over the day side, a thin dim blue line at
    // night, warm at the terminator
    vec3 L = normalize(u_sun.xyz);
    float sd = dot(normalize(closest), L);
    float day = smoothstep(-0.35, 0.2, sd);
    float dusk = smoothstep(-0.35, -0.05, sd) * (1.0 - smoothstep(-0.05, 0.3, sd));
    col = mix(vec3(0.25, 0.35, 0.65), col, day) + vec3(0.45, 0.18, 0.0) * dusk;
    a *= mix(0.28, 1.0, day);
    out_color = vec4(col * a, a);
}
