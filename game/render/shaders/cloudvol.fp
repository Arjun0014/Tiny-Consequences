#version 140
// Clouds v5 (S23, D-180): the planet's cloud layer as one soft volume, marched along each pixel's view ray.
// Where cloud stands, how much and how dark comes from the weather the world lives (game/view/clouds.lua
// writes it into covtex hour by hour: the rain spells each place has, the god's rain and dry spells, the
// humidity of the day); its shape inside that cover comes from 3D noise carried along by the zonal wind
// of each latitude. A ray from far above crosses the layer from its top; a camera inside it sees mist
// close by; under it, the deck's underside. Nothing is drawn where the weather is clear.
#include "/game/render/shaders/light.glsl"

in highp vec3 var_dir;

out vec4 out_color;

uniform sampler2D covtex;    // equirect (u lon, v lat): r, g cover (map A, B), b, a darkness (A, B)
uniform sampler2D geotex;    // equirect: r cloud base above the ground (0..1 -> BMIN..BMAX), g zonal drift
uniform sampler2D noisetex;  // 256^2 value noise: r a slice, g the next slice (offset 37, 17)

uniform fs_uniforms
{
    highp mat4 vp_frag;
    highp vec4 u_cam;      // eye (world)
    highp vec4 u_sun;      // to the sun
    highp vec4 u_cv0;      // x: hour (view epoch), y: R (sea level), z: lowest base radius, w: highest top radius
    highp vec4 u_cv1;      // x: blend map A -> B, y: steps, z: density, w: noise frequency
    highp vec4 u_cv2;      // x: base min (above R), y: base max, z: drift max (rad/h), w: advection period (h)
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

// the shape of the cloud inside its cover: two octaves and a finer one that frays the edges
highp float fbm3(highp vec3 q)
{
    return noise3(q) * 0.56 + noise3(q * 2.07 + 11.3) * 0.29 + noise3(q * 5.13 + 3.7) * 0.15;
}

highp vec3 roty(highp vec3 p, highp float a)
{
    highp float c = cos(a);
    highp float s = sin(a);
    return vec3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
}

// ray / sphere (centre 0): entry and exit distances, or (1, -1) when it misses
highp vec2 sphere(highp vec3 o, highp vec3 d, highp float r)
{
    highp float b = dot(o, d);
    highp float c = dot(o, o) - r * r;
    highp float h = b * b - c;
    if (h < 0.0) return vec2(1.0, -1.0);
    h = sqrt(h);
    return vec2(-b - h, -b + h);
}

void main()
{
    highp vec3 O = u_cam.xyz;
    highp vec3 D = normalize(var_dir);
    highp float R = u_cv0.y;
    highp float Rt = u_cv0.w;
    highp vec2 to = sphere(O, D, Rt);
    if (to.y <= 0.0) discard;
    highp float t0 = max(to.x, 0.0);
    highp float t1 = to.y;
    // the ground ends the ray
    highp vec2 tg = sphere(O, D, R);
    bool ground = tg.y > 0.0 && tg.x > 0.0;
    if (ground) t1 = min(t1, tg.x);
    if (t1 <= t0) discard;
    float blend = u_cv1.x;
    // the layer where this ray goes: its lowest base and highest top at three places along it (and
    // under the eye), so the samples fall in the cloud, not in the clear air above it
    highp float lo = 1e9;
    highp float hi = -1e9;
    float cmax = 0.0;
    // (at fixed distances along the ray, not spread over its own length: that length jumps at the planet's
    // horizon, and the bounds with it - a hard arc across the deck)
    highp float tA = min(t0 + 0.6, to.y);
    highp float tB = min(t0 + 2.4, to.y);
    highp float tC = min(t0 + 6.0, to.y);
    for (int k = 0; k < 4; k++) {
        highp float tk = k == 0 ? tA : (k == 1 ? tB : tC);
        highp vec3 p = k == 3 ? O : O + D * tk;
        highp vec3 up = normalize(p);
        highp vec2 uv = vec2(atan(up.z, up.x) * INV2PI + 0.5, asin(clamp(up.y, -1.0, 1.0)) * INVPI + 0.5);
        vec4 cv = texture(covtex, uv);
        vec4 g = texture(geotex, uv);
        float dark = max(cv.b, cv.a);
        cmax = max(cmax, max(cv.r, cv.g));
        highp float hb = R + mix(u_cv2.x, u_cv2.y, g.r);
        lo = min(lo, hb);
        hi = max(hi, hb + mix(0.14, 0.55, max(cv.r, cv.g)) + 0.85 * smoothstep(0.45, 0.85, dark) + 0.05);
    }
    // (clear sky wherever this ray goes: nothing to march)
    if (cmax < 0.015) discard;
    highp vec2 th = sphere(O, D, hi);
    if (th.y <= 0.0) discard;
    t0 = max(t0, max(th.x, 0.0));
    t1 = min(t1, th.y);
    highp float rO = length(O);
    highp vec2 tl = sphere(O, D, lo);
    if (rO > lo) {
        // (a ray going down to the ground ends at the cloud's floor; one that passes over the horizon goes
        // on through the clear air under the layer and into the layer beyond - no seam at the floor's edge)
        if (ground && tl.y > 0.0 && tl.x > t0) t1 = min(t1, tl.x);
    } else {
        t0 = max(t0, tl.y);
    }
    if (t1 <= t0) discard;
    highp float L = min(t1 - t0, 9.0);
    bool inside = rO < hi && rO > lo;
    int n = int(u_cv1.y);
    highp float jit = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));

    // the noise carried by the wind: two phases, each reset when its weight is nil
    highp float hour = u_cv0.x;
    highp float P = u_cv2.w;
    highp float ph = fract(hour / P);
    highp float tauA = ph * P;
    highp float tauB = fract(ph + 0.5) * P;
    highp float wA = 1.0 - abs(2.0 * ph - 1.0);
    highp float wB = 1.0 - wA;
    highp float cyc = floor(hour / P);
    highp vec3 offA = vec3(fract(cyc * 0.6180339) * 97.0, 13.0, fract(cyc * 0.4142135) * 61.0);
    highp float cycB = floor(hour / P + 0.5);
    highp vec3 offB = vec3(fract(cycB * 0.7548776) * 89.0, 41.0, fract(cycB * 0.5698403) * 53.0);
    highp float wn = 1.0 / sqrt(wA * wA + wB * wB);
    highp float freq = u_cv1.w;

    vec3 L3 = normalize(u_sun.xyz);
    highp float T = 1.0;
    vec3 C = vec3(0.0);
    highp float tfront = -1.0;
    highp vec3 upf = normalize(O + D * t0);
    for (int i = 0; i < 40; i++) {
        if (i >= n) break;
        highp float s0 = (float(i) + jit) / float(n);
        highp float s1 = (float(i) + 1.0) / float(n);
        highp float ta = inside ? t0 + L * s0 * s0 : t0 + L * s0;
        highp float dt = inside ? L * (s1 * s1 - (float(i) / float(n)) * (float(i) / float(n))) : L / float(n);
        highp vec3 p = O + D * ta;
        highp float r = length(p);
        highp vec3 up = p / r;
        highp vec2 uv = vec2(atan(up.z, up.x) * INV2PI + 0.5, asin(clamp(up.y, -1.0, 1.0)) * INVPI + 0.5);
        vec4 cv = texture(covtex, uv);
        float cover = mix(cv.r, cv.g, blend);
        if (cover < 0.015) continue;
        vec4 g = texture(geotex, uv);
        highp float hb = R + mix(u_cv2.x, u_cv2.y, g.r);
        float dark = mix(cv.b, cv.a, blend);
        // decks are thick, storms tower; fair-weather heaps are thin
        highp float thick = mix(0.14, 0.55, cover) + 0.85 * smoothstep(0.45, 0.85, dark);
        highp float h = (r - hb) / thick;
        if (h <= 0.0 || h >= 1.0) continue;
        highp float w = (g.g - 0.5) * 2.0 * u_cv2.z;
        highp float na = fbm3(roty(p, -w * tauA) * freq + offA);
        highp float nb = fbm3(roty(p, -w * tauB) * freq + offB);
        highp float nn = ((na - 0.5) * wA + (nb - 0.5) * wB) * wn + 0.5;
        // flat bases; the top of the mass rises and falls with the noise (heaped tops - even a full deck
        // shows its lumps from above); the cover decides how much of the field is cloud; inside, the cloud
        // is thicker and thinner (a deck has its thin places)
        float top = 0.42 + 0.58 * smoothstep(0.28, 0.72, nn);
        float prof = smoothstep(0.0, 0.07, h) * (1.0 - smoothstep(top - 0.22, top, h));
        float body = clamp((nn - (1.0 - cover) * 0.86) * 4.0, 0.0, 1.0);
        float d = body * prof * (0.4 + 0.6 * smoothstep(0.3, 0.7, nn));
        if (d <= 0.001) continue;
        // mist close to the eye is thinner than the cloud seen from outside (a soft way in and out)
        highp float sig = u_cv1.z * d * (0.8 + 0.5 * dark) * mix(0.3, 1.0, smoothstep(0.0, 0.5, ta));
        float a = 1.0 - exp(-sig * dt);
        // light: the sun through the cloud above it toward the sun; sky light from above
        float sd = dot(up, L3);
        float day = smoothstep(-0.28, 0.12, sd);
        float hr = clamp(h / top, 0.0, 1.0);
        float od = (top - h) * thick * (1.2 + 2.6 * cover) / max(0.18, sd);
        float sunT = exp(-od);
        vec3 amb = mix(vec3(0.50, 0.56, 0.68), vec3(0.82, 0.88, 0.97), hr);
        vec3 col = amb * 0.62 + vec3(1.0, 0.975, 0.94) * (0.25 + 0.75 * sunT) * 0.7;
        col = mix(col, col * vec3(0.52, 0.56, 0.66), dark);
        float dusk = smoothstep(-0.18, 0.0, sd) * (1.0 - smoothstep(0.0, 0.14, sd));
        col = col * mix(vec3(0.12, 0.15, 0.26), vec3(1.0), day) + vec3(0.16, 0.07, 0.02) * dusk * hr;
        C += T * a * col;
        T *= 1.0 - a;
        if (tfront < 0.0 && T < 0.92) { tfront = ta; upf = up; }
        if (T < 0.02) break;
    }
    float alpha = 1.0 - T;
    if (alpha < 0.004) discard;
    // a silver lining toward the sun, the limb haze over it all
    float back = pow(clamp(dot(D, L3), 0.0, 1.0), 6.0);
    C += back * 0.18 * alpha * (1.0 - alpha) * vec3(1.0, 0.96, 0.88);
    C = tc_limb(C / alpha, upf, -D, 0.3 * tc_haze_scale(O)) * alpha;
    highp vec3 P3 = O + D * max(tfront, t0 + 0.001);
    highp vec4 clip = vp_frag * vec4(P3, 1.0);
    gl_FragDepth = clamp(clip.z / clip.w * 0.5 + 0.5, 0.0, 1.0);
    out_color = vec4(C, alpha);
}
