#version 140
// Cloud puffs, ray-traced (see cloud.vp): a spheroid of radius r and vertical squash s, cut flat
// at its base. Two passes share this program (u_pass.x): the core pass (0) draws the solid
// interior with depth writes and the depth-of-field CoC in alpha; the edge pass (1) blends the
// soft, noise-frayed rims and the see-through wisps over it. Lighting: soft wrap-around sun with
// cool shadowed sides, bright tops, grey flat bases, silver linings against the sun, storm decks
// darkened by their rain, moonlit grey on the night side, warm at the terminator.
#include "/game/render/shaders/light.glsl"
#include "/game/render/shaders/shadow.glsl"

in highp vec3 var_c;
in highp vec4 var_p;
in highp vec4 var_q;
in highp vec3 var_wpos;

out vec4 out_color;

uniform fs_uniforms
{
    highp mat4 vp_frag;
    highp vec4 u_cam;
    highp vec4 u_sun;
    highp vec4 u_cut;     // xyz: view focus on the ground (world), w: cut-away strength (zoomed in)
    highp vec4 u_focus;
    highp vec4 u_pass;    // x: 0 core, 1 edge
};

void main()
{
    highp vec3 O = u_cam.xyz;
    highp vec3 D = normalize(var_wpos - O);
    highp vec3 C = var_c;
    float r = var_p.x;
    float s = var_p.y;
    float hb = var_p.z;
    highp vec3 U = normalize(C);
    highp vec3 o = O - C;
    highp float ou = dot(o, U);
    highp float du = dot(D, U);
    float is = 1.0 / s;
    highp vec3 o2 = o + U * (ou * is - ou);
    highp vec3 d2 = D + U * (du * is - du);
    highp float A = dot(d2, d2);
    highp float B = dot(o2, d2);
    highp float Cc = dot(o2, o2) - r * r;
    highp float disc = B * B - A * Cc;
    if (disc <= 0.0) discard;
    highp float sq = sqrt(disc);
    highp float t0 = (-B - sq) / A;
    highp float t1 = (-B + sq) / A;
    if (t1 <= 0.0) discard;
    // how close the ray passes to the centre (0 centre .. 1 grazing): the soft rim
    float m = clamp(1.0 - disc / (A * r * r), 0.0, 1.0);
    highp float h0 = ou + t0 * du;
    highp float t;
    vec3 N;
    float base = 0.0;
    if (h0 >= hb) {
        t = t0;
        highp vec3 p2 = o2 + d2 * t0;
        highp float pu = dot(p2, U);
        N = normalize(p2 + U * (pu * is - pu));
    } else if (du > 0.0) {
        t = (hb - ou) / du;
        if (t > t1) discard;
        N = -U;
        base = 1.0;
    } else {
        discard;
    }
    if (t < 0.0) discard;
    highp vec3 P = O + D * t;
    // frayed, soft silhouette: the rim threshold wanders with a little 3D noise
    float seed = var_q.z;
    float nz = tc_noise(P * 9.0 + seed * 13.1) * 0.6 + tc_noise(P * 23.0 + seed * 3.7) * 0.4;
    float rimf = 1.0 - smoothstep(0.64 + 0.22 * nz, 1.0, sqrt(m));
    float op = var_p.w;
    // the view cut-away: what lies between the eye and the focus fades out when zoomed in
    float keep = 1.0;
    if (u_cut.w > 0.0) {
        highp vec3 Dv = u_cut.xyz - O;
        highp float dd = dot(Dv, Dv);
        highp vec3 Fv = P - O;
        highp float tv = clamp(dot(Fv, Dv) / dd, 0.0, 1.2);
        highp float perp = length(Fv - Dv * tv);
        highp float rad = sqrt(dd) * (0.2 + 0.26 * tv);
        keep = mix(1.0, smoothstep(rad * 0.55, rad * 1.15, perp), u_cut.w * step(tv, 1.05));
    }
    // the cut-away opens a hole with frayed edges in the deck (no see-through layers)
    if (keep < 0.35 + 0.4 * nz) discard;
    bool solid = rimf >= 0.985 && op >= 0.985;
    bool core = u_pass.x < 0.5;
    float a = rimf * op;
    if (core) {
        if (!solid) discard;
    } else {
        if (solid || a < 0.004) discard;
    }

    vec3 L = normalize(u_sun.xyz);
    vec3 V = -D;
    vec3 up = normalize(P);
    float dark = var_q.y;
    float shade = var_q.x;
    // banks read as one mass: the puff's own curvature is softened toward the vertical, and the
    // brightness follows the height above the bank's shared flat base (grey below, sunlit tops)
    vec3 Ns = normalize(N * 0.5 + up);
    float wrap = clamp((dot(Ns, L) + 0.6) / 1.6, 0.0, 1.0);
    float hab = dot(P - C, U) - hb;                    // height above the flat base (world units)
    float vert = smoothstep(0.0, 0.2, hab);
    vec3 lit = vec3(1.0, 0.99, 0.965);
    vec3 under = vec3(0.68, 0.74, 0.85);
    // sides seen from above are sunlit too; only the flat underside and the shade side go grey
    vec3 col = mix(under, lit, clamp(0.42 + wrap * 0.38 + vert * 0.32, 0.0, 1.0));
    col = mix(col, vec3(0.64, 0.69, 0.79), base * 0.65);
    col *= mix(0.93, 1.02, shade);
    // rain decks and storms: heavy, blue-grey, darkest below
    vec3 stormc = mix(vec3(0.34, 0.37, 0.44), vec3(0.62, 0.65, 0.72), vert) * mix(0.85, 1.05, wrap);
    col = mix(col, stormc, dark);
    // silver lining: the rim glows when the sun is behind the cloud
    float rim = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 3.0);
    float back = clamp(dot(-V, L), 0.0, 1.0);
    col += rim * (0.06 + 0.3 * back) * (1.0 - 0.6 * dark) * vec3(1.0, 0.97, 0.9);
    // night side: moonlit grey-blue; warm edges at the terminator (D-050)
    float sd = dot(up, L);
    float day = smoothstep(-0.30, 0.12, sd);
    float dusk = smoothstep(-0.25, 0.0, sd) * (1.0 - smoothstep(0.0, 0.25, sd));
    col = col * mix(vec3(0.15, 0.18, 0.28), vec3(1.0), day) + vec3(0.32, 0.13, 0.03) * dusk * (0.4 + 0.6 * vert);
    col = tc_limb(col, up, V, 0.3 * tc_haze_scale(O));

    highp vec4 clip = vp_frag * vec4(P, 1.0);
    gl_FragDepth = clamp(clip.z / clip.w * 0.5 + 0.5, 0.0, 1.0);
    if (core) {
        out_color = vec4(col, tc_coc(P, O, u_focus));
    } else {
        out_color = vec4(col * a, a);
    }
}
