// TC2 water (D2-026): colour from the real depth of water under each pixel (the scene depth texture): clear
// turquoise shallows over sand, deep blue offshore; foam where the water thins at the shore; ripples close in,
// a soft sheen far away; fresnel sky reflection and sun glint; aerial haze like the land.
Shader "TC2/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.02, 0.11, 0.24, 1)
        _Shallow ("Shallow", Color) = (0.15, 0.55, 0.57, 1)
        _Sky ("Sky", Color) = (0.58, 0.72, 0.90, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.62
        _DepthScale ("Depth scale (m)", Float) = 14
        _SeaSphere ("The sea's true radius (0: the mesh as it is)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "TC2CloudForm.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep; half4 _Shallow; half4 _Sky; half _Alpha; float _DepthScale; float _SeaSphere;
            CBUFFER_END
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float4 _TC2MoonDir;
            float _TC2NightLift;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 screenPos : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(p.positionCS);
                return o;
            }

            half Noise2(float2 uv) { return SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, uv).r; }

            half4 frag(Varyings i, out float depthOut : SV_Depth) : SV_Target
            {
                // (session 30) the sea where the true sphere is, not its mesh: the mesh's faces (~100 m) lie up to ~0.4 m inside the sphere between
                // their corners, and the ground colours its shallows, wet sand and surf by the true radius (_TC2SeaRadius) - on a flat beach the water's
                // edge stood tens of metres off them in the faces' pattern, a sawtooth of bare and covered triangles along the shore (s29book8, Kaihope's
                // shore). The view ray's own meeting with the sphere gives the depth the ground is tested against; lakes keep their meshes (_SeaSphere 0).
                // The sphere a quarter of a metre below the sea's level, about the faces' mean sag: the first quarter metre of water at the shore is the
                // swash - wet sand and the ground's surf (Terrain.shader), uncovered - an even band where the faces had left it in their triangles
                // (s30v4 shore-low: on the exact sphere the edge was crisp and the surf under the surface)
                depthOut = i.positionCS.z;
                if (_SeaSphere > 0.0)
                {
                    float rs = _SeaSphere - 0.25;
                    float3 ro = _WorldSpaceCameraPos, rd = normalize(i.positionWS - ro);
                    float b = dot(ro, rd), cc = dot(ro, ro) - rs * rs, disc = b * b - cc;
                    if (cc > 0.0 && disc > 0.0)
                    {
                        float4 cp = TransformWorldToHClip(ro + rd * (-b - sqrt(disc)));
                        float z = cp.z / cp.w;
                        #if !UNITY_REVERSED_Z
                        if (UNITY_NEAR_CLIP_VALUE < 0.0) z = z * 0.5 + 0.5;   // (OpenGL's -1..1 to the window's 0..1)
                        #endif
                        depthOut = z;
                    }
                }
                float3 up = normalize(i.positionWS);
                float t = _Time.y;
                float3 camRel = i.positionWS - _WorldSpaceCameraPos;
                float dist = length(camRel);
                float3 v = -camRel / dist;
                half near = saturate(1.0 - dist / 6000.0);
                // ripples: two scrolling noise layers tilt the normal (close in only)
                float3 wp = i.positionWS;
                float2 uvA = (abs(up.y) > 0.5 ? wp.xz : (abs(up.x) > 0.5 ? wp.yz : wp.xy));
                half r1 = Noise2(uvA / 37.0 + float2(t * 0.011, t * 0.007)) - 0.5h;
                half r2 = Noise2(uvA / 9.0 - float2(t * 0.019, -t * 0.013)) - 0.5h;
                float3 tangent = normalize(cross(up, float3(0.0, 1.0, 0.001)));
                float3 bitangent = cross(up, tangent);
                // far away the wind roughens the sea in broad patches: they break up the glint and the colour
                half far = 1.0h - near;
                half w1 = Noise2(uvA / 900.0 + float2(t * 0.002, t * 0.0013)) - 0.5h;
                half w2 = Noise2(uvA / 2600.0 - float2(t * 0.0011, 0.0)) - 0.5h;
                // (session 27) ripples only as far as a pixel resolves them - finer than a pixel they were a speckle of glints from afar; what a pixel
                // cannot resolve roughens the sea instead (a broader, dimmer glint)
                float fp = length(fwidth(i.positionWS));
                half a1 = (half)saturate(1.0 - fp / 1.5), a2 = (half)saturate(1.0 - fp / 0.45), aw = (half)saturate(1.0 - fp / 160.0);
                half rough = 1.0h - 0.5h * (a1 + a2);
                float3 n = normalize(up + (tangent * (r1 * 0.22 * a1 + r2 * 0.12 * a2) + bitangent * (r2 * 0.22 * a2 - r1 * 0.10 * a1)) * near
                                        + (tangent * w1 + bitangent * w2) * 0.16 * far * aw);
                // the sea floor below is coloured by its own depth (sand in the shallows): the water lets it through
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half ndl = saturate(dot(n, l.direction) * 0.6h + 0.4h);
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, 0.55h) * (0.35h + 0.65h * ndl);
                half fres = pow(1.0h - saturate(dot(n, v)), 4.0h);
                float3 h = normalize(l.direction + v);
                // the sun's glint: a broad soft spot from afar (a rough sea), a sharp sparkle close in
                half spec = pow(saturate(dot(n, h)), lerp(220.0h, 70.0h, rough)) * lerp(2.6h, 0.45h, rough) * lerp(0.35h, 1.0h, l.shadowAttenuation);
                // (session 27) from afar the glint is broken by the wind's fields - brighter where the sea is roughened, dark lanes of calm between -
                // so it reads as sun on a wide sea, not a smooth pale veil over it (s27p3 region)
                half broken = 1.0h;   // (session 27, context 4: the moon's path broken by the same fields - by night s27n1's region view had a smooth pale veil)
                {
                    half g1 = Noise2(uvA / 340.0 + float2(t * 0.004, -t * 0.003)), g2 = Noise2(uvA / 120.0 - float2(t * 0.006, t * 0.002));
                    half field = saturate(0.25h + 1.1h * g1 * g1 + 0.6h * (g2 - 0.5h) * (half)saturate(1.0 - fp / 40.0) + 0.8h * (w1 + 0.5h) * (w1 + 0.5h));   // (the finer field only where a pixel resolves it)
                    broken = lerp(1.0h, field * 1.35h, rough * far);
                    spec *= broken;
                }
                // (session 27) a cloud between the water and the sun: its shadow dims the sea and takes the glint
                half cloud = TC2CloudShadow(i.positionWS, l.direction, length(fwidth(i.positionWS)), _TC2CloudScale.z);
                spec *= 1.0h - cloud;
                body *= 1.0h - 0.6h * cloud;
                // the moon's path on the water at night
                float3 hm = normalize(_TC2MoonDir.xyz + v);
                half moon = pow(saturate(dot(n, hm)), 90.0h) * 0.55h * (1.0h - day) * (half)_TC2NightLift * broken;
                half3 sky = lerp(_Sky.rgb, _TC2FogColor.rgb, 0.6h) * half3(0.85h, 0.95h, 1.05h);
                half3 c = (body * (1.0h + (w1 + w2) * 0.25h * far) + sky * fres * 0.42h) * (0.12h * (half)_TC2NightLift + (0.88h + 0.12h * (1.0h - (half)_TC2NightLift)) * day) + l.color * spec * day + half3(0.62h, 0.70h, 0.85h) * moon;
                half alpha = saturate(_Alpha + fres * 0.3h);
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h);
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
