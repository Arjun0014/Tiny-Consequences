#version 140
// Sky gradient (#2B6FC4 top -> #5FD0F0 -> #FFF1B8 horizon glow) with slow diagonal wisps.

in highp vec2 var_uv;
out vec4 out_color;

uniform fs_uniforms
{
    highp vec4 u_sky;   // x = time, y = aspect (w/h), z = night (0 day .. 1 night at the view focus)
    highp vec4 u_focus; // w = CoC of the sky (depth of field)
};

const vec3 TOP = vec3(0.169, 0.435, 0.769);
const vec3 MIDC = vec3(0.373, 0.816, 0.941);
const vec3 LOW = vec3(0.62, 0.90, 0.93);
const vec3 HORIZON = vec3(1.0, 0.945, 0.722);

float streak(vec2 p, float freq, float width, float speed, float seed, float t)
{
    // p: rotated coords (x along the streak, y across)
    float y = p.y * freq + seed;
    float cell = floor(y);
    float f = fract(y) - 0.5;
    float line = smoothstep(width, 0.0, abs(f));
    float h = fract(sin(cell * 91.7 + seed * 13.1) * 43758.5);
    float along = p.x * (0.9 + h * 0.8) + t * speed * (0.6 + h) + h * 7.0;
    float seg = smoothstep(0.35, 0.95, sin(along) * 0.5 + 0.5);
    return line * seg * step(0.45, h);
}

void main()
{
    float y = var_uv.y;
    vec3 col = mix(MIDC, TOP, smoothstep(0.52, 1.0, y));
    col = mix(LOW, col, smoothstep(0.16, 0.5, y));
    col = mix(HORIZON, col, smoothstep(0.0, 0.2, y));
    float t = u_sky.x;
    vec2 p = vec2((var_uv.x - 0.5) * u_sky.y, var_uv.y - 0.5);
    float ca = 0.94, sa = 0.34;
    vec2 r = vec2(p.x * ca + p.y * sa, -p.x * sa + p.y * ca);
    float w = streak(r * vec2(3.0, 1.0), 9.0, 0.06, 0.05, 1.3, t) * 0.22
            + streak(r * vec2(2.0, 1.0), 5.0, 0.05, 0.03, 4.7, t) * 0.16;
    w *= smoothstep(0.0, 0.3, y) * (1.0 - smoothstep(0.9, 1.0, y) * 0.5);
    col = mix(col, vec3(1.0), w);
    // night at the view focus (D-050): deep blue sky, a faint warm horizon, stars
    float night = clamp(u_sky.z, 0.0, 1.0);
    if (night > 0.001) {
        vec3 ncol = mix(vec3(0.035, 0.05, 0.13), vec3(0.08, 0.12, 0.27), smoothstep(0.0, 0.7, 1.0 - y));
        ncol = mix(vec3(0.30, 0.22, 0.30), ncol, smoothstep(0.0, 0.16, y));
        vec2 g = floor(p * 90.0);
        float h = fract(sin(dot(g, vec2(12.9898, 78.233))) * 43758.5453);
        vec2 f = fract(p * 90.0) - 0.5;
        float star = step(0.985, h) * smoothstep(0.18, 0.0, length(f)) * (0.6 + 0.4 * sin(t * 2.0 + h * 40.0));
        ncol += vec3(0.9, 0.92, 1.0) * star * smoothstep(0.1, 0.4, y);
        col = mix(col, ncol, night);
    }
    out_color = vec4(col, u_focus.w);
}
