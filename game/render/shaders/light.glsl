// Shared lighting for the planet, props and movers (Stage 1.8; docs/ART_DIRECTION.md §7):
// warm sun from the upper left (follows the camera), strong soft ambient, soft toon banding,
// cool rim light, and a pale limb haze that fades toward the horizon colour.
#ifndef TC_LIGHT_GLSL
#define TC_LIGHT_GLSL

const vec3 TC_SUN = vec3(1.0, 0.94, 0.80);
const vec3 TC_SKY_AMB = vec3(0.70, 0.84, 1.0);
const vec3 TC_GROUND_AMB = vec3(0.62, 0.60, 0.52);
const vec3 TC_HAZE = vec3(0.80, 0.95, 1.0);

// albedo lit by sun + hemisphere ambient; N = surface normal, up = planet radial, V = to eye
vec3 tc_light(vec3 albedo, vec3 N, vec3 up, vec3 V, vec3 L, float ambient_boost)
{
    float ndl = dot(N, L);
    float wrap = clamp((ndl + 0.35) / 1.35, 0.0, 1.0);
    float w3 = wrap * 3.0;
    float band = (floor(w3) + smoothstep(0.35, 0.65, fract(w3))) / 3.0;   // soft toon steps
    float diff = mix(wrap, band, 0.3);
    float hemi = clamp(0.5 + 0.5 * dot(N, up), 0.0, 1.0);
    vec3 amb = mix(TC_GROUND_AMB, TC_SKY_AMB, hemi) * (0.52 + ambient_boost);
    vec3 col = albedo * (amb + TC_SUN * diff * 0.72);
    float rim = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 3.0);
    col += rim * 0.10 * TC_HAZE;
    return col;
}

// Same, with a sun shadow factor sh (1 lit, 0 shadowed): shadows remove the sun term and a
// little of the ambient (contact occlusion), so they read clearly without going muddy.
vec3 tc_light_sh(vec3 albedo, vec3 N, vec3 up, vec3 V, vec3 L, float ambient_boost, float sh)
{
    float ndl = dot(N, L);
    float wrap = clamp((ndl + 0.35) / 1.35, 0.0, 1.0);
    float w3 = wrap * 3.0;
    float band = (floor(w3) + smoothstep(0.35, 0.65, fract(w3))) / 3.0;
    float diff = mix(wrap, band, 0.2) * sh;
    float hemi = clamp(0.5 + 0.5 * dot(N, up), 0.0, 1.0);
    vec3 amb = mix(TC_GROUND_AMB, TC_SKY_AMB, hemi) * (0.48 + ambient_boost) * mix(0.82, 1.0, sh);
    vec3 col = albedo * (amb + TC_SUN * diff * 0.82);
    float rim = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 3.0);
    col += rim * 0.10 * TC_HAZE;
    return col;
}

// Close to the surface the limb haze would wash out far forests: scale it down with the eye's
// distance from the planet centre (R = 10).
float tc_haze_scale(vec3 eye)
{
    return clamp((length(eye) - 13.0) / 25.0, 0.3, 1.0);
}

// Pale atmospheric haze toward the planet limb (uses the radial direction, not the normal).
vec3 tc_limb(vec3 col, vec3 up, vec3 V, float amount)
{
    float f = 1.0 - clamp(dot(up, V), 0.0, 1.0);
    float haze = pow(f, 3.2) * amount;
    return mix(col, TC_HAZE, clamp(haze, 0.0, 0.6));
}

// Cheap 3D value noise (fragment detail: ice/snow/beach edges, colour mottling).
highp float tc_hash(highp vec3 p)
{
    p = fract(p * 0.3183099 + vec3(0.71, 0.113, 0.419));
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

highp float tc_noise(highp vec3 x)
{
    highp vec3 i = floor(x);
    highp vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(tc_hash(i), tc_hash(i + vec3(1.0, 0.0, 0.0)), f.x),
                   mix(tc_hash(i + vec3(0.0, 1.0, 0.0)), tc_hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
               mix(mix(tc_hash(i + vec3(0.0, 0.0, 1.0)), tc_hash(i + vec3(1.0, 0.0, 1.0)), f.x),
                   mix(tc_hash(i + vec3(0.0, 1.0, 1.0)), tc_hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}

highp float tc_fbm(highp vec3 x)
{
    return tc_noise(x) * 0.55 + tc_noise(x * 2.13 + 7.1) * 0.3 + tc_noise(x * 4.37 + 3.3) * 0.15;
}

#endif
