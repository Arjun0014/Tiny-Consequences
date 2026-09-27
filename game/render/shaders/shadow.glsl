// Sun shadow map helpers (renderer v2, D-048). The shadow pass writes light-space depth packed
// into RGB (24 bits) of an RGBA8 target; A holds cloud cover (soft cloud shadows).
// Receivers pass world positions through u_sh0..u_sh3 (rows of bias * light proj * light view).
#ifndef TC_SHADOW_GLSL
#define TC_SHADOW_GLSL

vec3 tc_pack24(highp float d)
{
    highp vec3 e = fract(d * vec3(1.0, 255.0, 65025.0));
    e -= e.yzz * vec3(1.0 / 255.0, 1.0 / 255.0, 0.0);
    return e;
}

highp float tc_unpack24(highp vec3 e)
{
    return dot(e, vec3(1.0, 1.0 / 255.0, 1.0 / 65025.0));
}

// Shadow-map coordinates of world point p (xy = uv, z = depth 0..1).
highp vec3 tc_shadow_coord(highp vec3 p, highp vec4 r0, highp vec4 r1, highp vec4 r2)
{
    highp vec4 q = vec4(p, 1.0);
    return vec3(dot(r0, q), dot(r1, q), dot(r2, q));
}

// 1 = lit, 0 = fully shadowed. prm: x = 1/map size, y = depth bias, z = strength, w = on (>0.5).
// 9 taps (a small rotated Poisson disc + centre) for soft miniature-photo edges.
float tc_shadow(highp sampler2D tex, highp vec3 sc, highp vec4 prm)
{
    if (prm.w < 0.5) return 1.0;
    if (sc.x <= 0.002 || sc.x >= 0.998 || sc.y <= 0.002 || sc.y >= 0.998 || sc.z >= 1.0) return 1.0;
    highp float z = sc.z - prm.y;
    highp float t = prm.x * 1.6;
    float s = 0.0;
    s += step(z, tc_unpack24(texture(tex, sc.xy).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(-0.94, -0.34) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(0.95, -0.29) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(-0.09, -0.93) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(0.34, 0.94) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(-0.82, 0.52) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(0.54, 0.18) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(-0.38, 0.12) * t).rgb));
    s += step(z, tc_unpack24(texture(tex, sc.xy + vec2(0.18, -0.52) * t).rgb));
    s *= 1.0 / 9.0;
    // cloud shadow: blurred cloud cover stored in A
    float cloud = texture(tex, sc.xy).a;
    s *= 1.0 - cloud * 0.55;
    return mix(1.0, s, prm.z);
}

// Circle of confusion for the post pass's tilt-shift depth of field (written to alpha by
// opaque passes). focus: x = focus distance, y = strength, z = in-focus band (relative).
float tc_coc(highp vec3 wpos, highp vec3 eye, highp vec4 focus)
{
    highp float d = length(wpos - eye);
    highp float r = abs(d - focus.x) / max(focus.x, 0.001);
    return clamp((r - focus.z) * focus.y, 0.0, 1.0);
}

#endif
