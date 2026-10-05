// TC2 instanced objects (people, trees, houses, fire): one colour per draw (instances grouped by colour),
// optional emission, sun + ambient + shadows. GPU instancing via Graphics.RenderMeshInstanced.
Shader "TC2/Instanced"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Float) = 0
        _Anim ("Walk", Float) = 0
        _AmbientSky ("Ambient Sky", Color) = (0.45, 0.50, 0.62, 1)
        _AmbientGround ("Ambient Ground", Color) = (0.22, 0.19, 0.16, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Emission;
                half _Anim;
                half4 _AmbientSky;
                half4 _AmbientGround;
            CBUFFER_END
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float4 _TC2MoonDir;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 lp = v.positionOS.xyz;
                if (_Anim > 0.5)
                {
                    // a walk: each leg swings about the hip, arms the other way, the body rises at each step
                    float3 origin = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                    float phase = _Time.y * 7.0 + dot(origin, float3(0.37, 0.61, 0.83));
                    float side = lp.x >= 0 ? 1.0 : -1.0;
                    if (lp.y < 0.9)
                    {
                        float a = sin(phase) * 0.42 * side;
                        float dy = lp.y - 0.9;
                        lp.z += -dy * sin(a); lp.y = 0.9 + dy * cos(a);
                    }
                    else if (lp.y < 1.5 && abs(lp.x) > 0.2)
                    {
                        float a = -sin(phase) * 0.35 * side;
                        float dy = lp.y - 1.5;
                        lp.z += -dy * sin(a); lp.y = 1.5 + dy * cos(a);
                    }
                    lp.y += abs(sin(phase)) * 0.035;
                }
                VertexPositionInputs p = GetVertexPositionInputs(lp);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);
                float3 up = normalize(i.positionWS);
                half ndl = saturate(dot(n, l.direction));
                half hemi = dot(n, up) * 0.5h + 0.5h;
                half3 amb = lerp(_AmbientGround.rgb, _AmbientSky.rgb, hemi);
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                // the kit marks tintable parts (clothes, roofs) with vertex alpha 1; the rest keeps its colour, scaled by alpha
                half3 albedo = i.color.rgb * lerp(_BaseColor.aaa, _BaseColor.rgb, i.color.a);
                half wrap = saturate((dot(n, l.direction) + 0.2h) / 1.2h);
                // _Emission > 0 lights the whole object (fire); < 0 lights only its windows (vertex alpha 0.5)
                half window = step(0.4h, i.color.a) * step(i.color.a, 0.6h);
                half3 glow = _Emission > 0 ? albedo * _Emission : half3(1.0h, 0.52h, 0.18h) * (-_Emission) * window * 1.15h;
                half moon = saturate(dot(n, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day);
                // the planet hides a sun below the horizon (shadow maps reach only near the camera): round shapes such as
                // canopies would otherwise catch it from beneath at night
                half sunUp = saturate(dot(up, l.direction) * 4.0h + 0.35h);
                half3 c = albedo * (amb * lerp(half3(0.34h, 0.38h, 0.52h), half3(1.0h, 1.0h, 1.0h), day) + l.color * wrap * lerp(0.3h, 1.0h, l.shadowAttenuation) * 1.15h * sunUp + half3(0.13h, 0.17h, 0.29h) * moon) + glow;
                float dist = length(i.positionWS - _WorldSpaceCameraPos);
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h * (1.0h - saturate(abs(_Emission))));
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 sv(A v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                float4 pc = TransformWorldToHClip(ApplyShadowBias(pw, nw, _LightDirection));
                #if UNITY_REVERSED_Z
                    pc.z = min(pc.z, pc.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    pc.z = max(pc.z, pc.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return pc;
            }
            half4 sf() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
