#version 140
// Final composite (renderer v2, D-048): tilt-shift depth of field from the CoC in the scene's
// alpha (sharp -> half-res -> quarter-res blurred), bloom from the blurred highlights, a gentle
// "miniature photo" grade (contrast, saturation, warm highlights, cool shadows) and a vignette.
in highp vec2 var_uv;
out vec4 out_color;
uniform highp sampler2D tex0;   // scene (MSAA resolved), alpha = CoC
uniform highp sampler2D tex1;   // half-res, lightly blurred
uniform highp sampler2D tex2;   // quarter-res, well blurred

uniform fs_uniforms
{
    highp vec4 u_post;   // x = bloom, y = vignette, z = saturation, w = contrast
    highp vec4 u_pdir;   // xy = 1/screen size, z = time, w = dof on
};

// FXAA-style edge smoothing (luma-directed blend along the edge; 5 fetches + 2).
vec3 fxaa(highp vec2 uv, highp vec2 px, vec3 rgbM)
{
    vec3 lw = vec3(0.299, 0.587, 0.114);
    float lNW = dot(texture(tex0, uv + vec2(-1.0, -1.0) * px).rgb, lw);
    float lNE = dot(texture(tex0, uv + vec2(1.0, -1.0) * px).rgb, lw);
    float lSW = dot(texture(tex0, uv + vec2(-1.0, 1.0) * px).rgb, lw);
    float lSE = dot(texture(tex0, uv + vec2(1.0, 1.0) * px).rgb, lw);
    float lM = dot(rgbM, lw);
    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
    if (lMax - lMin < max(0.04, lMax * 0.125)) return rgbM;
    highp vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), ((lNW + lSW) - (lNE + lSE)));
    float red = max((lNW + lNE + lSW + lSE) * 0.03125, 0.0078125);
    float rcp = 1.0 / (min(abs(dir.x), abs(dir.y)) + red);
    dir = clamp(dir * rcp, vec2(-8.0), vec2(8.0)) * px;
    vec3 a = 0.5 * (texture(tex0, uv + dir * (1.0 / 3.0 - 0.5)).rgb + texture(tex0, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
    vec3 b = a * 0.5 + 0.25 * (texture(tex0, uv - dir * 0.5).rgb + texture(tex0, uv + dir * 0.5).rgb);
    float lB = dot(b, lw);
    return (lB < lMin || lB > lMax) ? a : b;
}

void main()
{
    vec4 s = texture(tex0, var_uv);
    s.rgb = fxaa(var_uv, u_pdir.xy, s.rgb);
    vec3 h = texture(tex1, var_uv).rgb;
    vec3 q = texture(tex2, var_uv).rgb;
    float coc = s.a * u_pdir.w;
    // spread the CoC a little: a sharp pixel next to very blurred ones borrows some of their blur
    float cq = texture(tex2, var_uv).a * u_pdir.w;
    coc = max(coc, cq * 0.35);
    vec3 col = s.rgb;
    col = mix(col, h, smoothstep(0.0, 0.45, coc));
    col = mix(col, q, smoothstep(0.35, 1.0, coc));
    // bloom: soft glow from bright, blurred regions (clouds, snow, sunlit water)
    vec3 b = max(q - vec3(0.8), vec3(0.0));
    col += b * u_post.x;
    // grade
    float l = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(vec3(l), col, u_post.z);
    col = (col - 0.5) * u_post.w + 0.5;
    vec3 warm = vec3(1.035, 1.0, 0.95);
    vec3 cool = vec3(0.95, 0.985, 1.05);
    col *= mix(cool, warm, smoothstep(0.15, 0.85, l));
    // vignette
    highp vec2 v = var_uv - 0.5;
    v.x *= u_pdir.y / u_pdir.x;
    float vig = 1.0 - u_post.y * smoothstep(0.35, 1.05, length(v));
    col *= vig;
    out_color = vec4(clamp(col, 0.0, 1.0), 1.0);
}
