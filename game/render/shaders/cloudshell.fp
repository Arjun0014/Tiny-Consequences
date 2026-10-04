#version 140
// Clouds v5 (S23) in the shadow pass: the cloud at the middle of its layer above this point (the same
// cover, darkness, height and wind-carried noise as cloudvol.fp), as cover in alpha.
in highp vec3 var_p;

out vec4 out_color;

uniform sampler2D covtex;
uniform sampler2D geotex;
uniform sampler2D noisetex;

uniform fs_uniforms
{
    highp vec4 u_cv0;
    highp vec4 u_cv1;
    highp vec4 u_cv2;
};

const float INV2PI = 0.15915494;
const float INVPI = 0.31830989;

highp float noise3(highp vec3 x)
{
    highp vec3 p = floor(x);
    highp vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    highp vec2 uv = (p.xy + vec2(37.0, 17.0) * p.z) + f.xy;
    vec2 rg = texture(noisetex, (uv + 0.5) / 256.0).xy;
    return mix(rg.x, rg.y, f.z);
}

highp float fbm2(highp vec3 q)
{
    return (noise3(q) * 0.56 + noise3(q * 2.07 + 11.3) * 0.29) / 0.85;
}

highp vec3 roty(highp vec3 p, highp float a)
{
    highp float c = cos(a);
    highp float s = sin(a);
    return vec3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
}

void main()
{
    highp vec3 up = normalize(var_p);
    highp vec2 uv = vec2(atan(up.z, up.x) * INV2PI + 0.5, asin(clamp(up.y, -1.0, 1.0)) * INVPI + 0.5);
    vec4 cv = texture(covtex, uv);
    float cover = mix(cv.r, cv.g, u_cv1.x);
    if (cover < 0.015) discard;
    float dark = mix(cv.b, cv.a, u_cv1.x);
    vec4 g = texture(geotex, uv);
    highp float hb = u_cv0.y + mix(u_cv2.x, u_cv2.y, g.r);
    highp float thick = mix(0.12, 0.42, cover) + 0.6 * smoothstep(0.45, 0.85, dark);
    highp vec3 p = up * (hb + 0.4 * thick);
    highp float hour = u_cv0.x;
    highp float P = u_cv2.w;
    highp float ph = fract(hour / P);
    highp float wA = 1.0 - abs(2.0 * ph - 1.0);
    highp float wB = 1.0 - wA;
    highp float cyc = floor(hour / P);
    highp vec3 offA = vec3(fract(cyc * 0.6180339) * 97.0, 13.0, fract(cyc * 0.4142135) * 61.0);
    highp float cycB = floor(hour / P + 0.5);
    highp vec3 offB = vec3(fract(cycB * 0.7548776) * 89.0, 41.0, fract(cycB * 0.5698403) * 53.0);
    highp float w = (g.g - 0.5) * 2.0 * u_cv2.z;
    highp float freq = u_cv1.w;
    highp float na = fbm2(roty(p, -w * ph * P) * freq + offA);
    highp float nb = fbm2(roty(p, -w * fract(ph + 0.5) * P) * freq + offB);
    highp float nn = ((na - 0.5) * wA + (nb - 0.5) * wB) / sqrt(wA * wA + wB * wB) + 0.5;
    float d = clamp((nn - (1.0 - cover) * 0.86) * 4.0, 0.0, 1.0) * (0.4 + 0.6 * smoothstep(0.3, 0.7, nn));
    float a = d * (0.55 + 0.45 * cover) * (0.75 + 0.25 * dark);
    if (a < 0.004) discard;
    out_color = vec4(0.0, 0.0, 0.0, a);
}
