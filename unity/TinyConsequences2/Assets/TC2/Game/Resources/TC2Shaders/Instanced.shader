// TC2 instanced objects (people, trees, houses, fire): a colour, emission and walk of its own per instance (D2-109: one draw a
// batch, whatever the colours), sun + ambient + shadows. GPU instancing via Graphics.RenderMeshInstanced.
Shader "TC2/Instanced"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _WallColor ("Walls", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Float) = 0
        _Anim ("Walk", Float) = 0
        _AmbientSky ("Ambient Sky", Color) = (0.45, 0.50, 0.62, 1)
        _AmbientGround ("Ambient Ground", Color) = (0.22, 0.19, 0.16, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        // The person's body (the kit's figure: legs below the hip at 0.88 m, arms beside the tunic from the shoulder at 1.5 m to the hands
        // at 0.92 m, |x| over 0.215 and within 0.09 of the body's plane) posed by what the simulation says they do (TC2.Core.Agents.Presence:
        // _Anim is the pose's code). A limb or the trunk turns about the body's x axis through a joint's height: a > 0 swings what hangs below
        // the joint forward (+z), a < 0 leans what stands above it forward.
        float3 TC2Rot(float3 p, float py, float a)
        {
            float dy = p.y - py, s = sin(a), c = cos(a);
            return float3(p.x, py + dy * c + p.z * s, p.z * c - dy * s);
        }
        float3 TC2RotN(float3 n, float a)
        {
            float s = sin(a), c = cos(a);
            return float3(n.x, n.y * c + n.z * s, n.z * c - n.y * s);
        }
        // a stroke: slow up for `up` of the cycle, then a quick fall (an axe, a pick, a hammer): 0 struck .. 1 raised
        float TC2Stroke(float ph, float up)
        {
            float f = frac(ph);
            return f < up ? smoothstep(0.0, 1.0, f / up) : 1.0 - smoothstep(0.0, 1.0, (f - up) / (1.0 - up));
        }
        void TC2Pose(inout float3 lp, inout float3 n, float anim, float ph)
        {
            int code = (int)round(anim);
            if (code <= 0) return;
            bool arm = abs(lp.x) > 0.215 && abs(lp.z) < 0.09 && lp.y > 0.8;
            bool leg = lp.y < 0.88 && !arm;
            float side = lp.x >= 0 ? 1.0 : -1.0;
            float armL = 0.0, armR = 0.0;   // arm swings (left x < 0, right x > 0)
            float legA = 0.0;                // this leg's swing
            float lean = 0.0;                // the trunk's lean forward (radians, < 0)
            float down = 0.0;                // the whole figure lowered (sitting)
            float bob = 0.0;
            if (code == 1 || code == 8 || code == 13)
            {
                // walking (8: with a load held before them; 13: running)
                float run = code == 13 ? 1.6 : 1.0;
                float t = ph * 7.0 * (code == 13 ? 1.5 : 1.0);
                legA = sin(t) * 0.42 * run * side;
                armL = -sin(t) * 0.35 * run; armR = sin(t) * 0.35 * run;
                if (code == 8) { armL = 1.15; armR = 1.15; }
                bob = abs(sin(t)) * 0.035 * run;
                lean = code == 13 ? -0.12 : 0.0;
            }
            else if (code == 2)
            {
                // hoeing: the hoe raised and brought down before the feet, bent to it
                float s = TC2Stroke(ph * 0.55, 0.6);
                armL = 0.5 + 1.5 * s; armR = armL;
                lean = -(0.30 + 0.18 * (1.0 - s));
                legA = 0.12 * side;
            }
            else if (code == 3)
            {
                // reaping, gathering: bent low, the hands working at the crop by turns
                float t = ph * 2.6;
                lean = -(0.85 + 0.08 * sin(t * 0.5));
                armL = 0.45 + 0.35 * sin(t); armR = 0.45 - 0.35 * sin(t);
                legA = 0.15 * side;
            }
            else if (code == 4)
            {
                // an axe or a pick swung from overhead
                float s = TC2Stroke(ph * 0.7, 0.7);
                armL = 0.35 + 2.55 * s; armR = armL;
                lean = -(0.08 + 0.32 * (1.0 - s));
                legA = 0.16 * side;
            }
            else if (code == 5)
            {
                // hammering: the right hand strikes, the left holds the work
                float s = TC2Stroke(ph * 1.5, 0.55);
                armR = 0.5 + 1.4 * s; armL = 0.95;
                lean = -0.22;
            }
            else if (code == 6 || code == 9 || code == 15 || code == 16)
            {
                // sitting on the ground or a step (9: rowing; 15: work in the lap - spinning, mending, a pot on the wheel; 16: talking)
                legA = 1.45;
                down = 0.80;
                armL = 0.35; armR = 0.35;
                if (code == 9) { float t = ph * 2.2; armL = 0.9 + 0.6 * sin(t); armR = armL; lean = -0.25 * sin(t); }
                if (code == 15) { float t = ph * 3.0; armL = 0.95 + 0.12 * sin(t); armR = 0.95 - 0.12 * sin(t * 1.3); lean = -0.18; }
                if (code == 16) { float t = ph * 0.9; armR = 0.35 + 0.7 * max(0.0, sin(t)) * (0.6 + 0.4 * sin(t * 2.7)); lean = -0.05 * sin(t * 0.5); }
            }
            else if (code == 7)
            {
                // talking: a hand that comes up as they speak, a weight shifted
                float t = ph * 0.9;
                armR = 0.25 + 0.75 * max(0.0, sin(t)) * (0.6 + 0.4 * sin(t * 2.7));
                armL = 0.1;
                lean = -0.04 * sin(t * 0.5);
            }
            else if (code == 10)
            {
                // at the loom: the hands before them, the shuttle thrown from side to side
                float t = ph * 2.4;
                armL = 1.15 + 0.3 * sin(t); armR = 1.15 - 0.3 * sin(t);
                lean = -0.1;
            }
            else if (code == 11)
            {
                // drawing water: hand over hand on the rope
                float t = ph * 2.0;
                armL = 1.6 + 0.8 * sin(t); armR = 1.6 - 0.8 * sin(t);
                lean = -0.12;
            }
            else if (code == 12)
            {
                // sparring: thrusts, the front foot forward
                float t = ph * 2.5;
                armL = 1.25 + 0.45 * max(0.0, sin(t)); armR = armL;
                legA = 0.38 * side;
                lean = -0.1 - 0.12 * max(0.0, sin(t));
                bob = 0.02 * abs(sin(t));
            }
            else if (code == 14)
            {
                // fishing from the shore: the rod held out, a twitch now and then
                armL = 1.0 + 0.06 * sin(ph * 1.3); armR = armL;
                lean = -0.06;
            }
            else if (code == 17)
            {
                // threshing with a flail (C3.7b): the swingle whirled up over the shoulder and brought down flat on the sheaves on the floor,
                // upright, to the beat of the others on the floor
                float s = TC2Stroke(ph * 1.2, 0.45);
                armL = 0.75 + 2.1 * s; armR = armL;
                lean = -(0.10 + 0.22 * (1.0 - s));
                legA = 0.18 * side;
            }
            if (arm)
            {
                float a = lp.x < 0 ? armL : armR;
                lp = TC2Rot(lp, 1.5, a); n = TC2RotN(n, a);
            }
            if (leg)
            {
                lp = TC2Rot(lp, 0.9, legA); n = TC2RotN(n, legA);
            }
            else
            {
                lp = TC2Rot(lp, 0.9, lean); n = TC2RotN(n, lean);
            }
            lp.y += bob - down;
        }
        ENDHLSL
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
                half4 _AmbientSky;
                half4 _AmbientGround;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _WallColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
                UNITY_DEFINE_INSTANCED_PROP(float, _Anim)
            UNITY_INSTANCING_BUFFER_END(PerInstance)
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float4 _TC2MoonDir;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; half4 base : TEXCOORD2; half emission : TEXCOORD3; half4 wall : TEXCOORD4; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.base = (half4)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                o.wall = (half4)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _WallColor);
                o.emission = (half)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Emission);
                float anim = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Anim);
                float3 lp = v.positionOS.xyz;
                float3 nos = v.normalOS;
                if (anim > 0.5)
                {
                    // the body posed as the person works, walks or sits (TC2Pose; each instance its own phase by where it stands)
                    float3 origin = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                    TC2Pose(lp, nos, anim, _Time.y + dot(origin, float3(0.37, 0.61, 0.83)));
                }
                VertexPositionInputs p = GetVertexPositionInputs(lp);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(nos);
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
                // the kit marks tintable parts (clothes, roofs) with vertex alpha 1 and walls with 0.25 (their own colour, D2-114); the
                // rest keeps its colour, scaled by alpha
                half4 baseC = i.base;
                half emission = i.emission;
                half3 tint = lerp(baseC.aaa, baseC.rgb, i.color.a);
                half wallM = step(0.15h, i.color.a) * step(i.color.a, 0.35h);
                tint = lerp(tint, i.wall.rgb * baseC.a, wallM);
                half3 albedo = i.color.rgb * tint;
                half wrap = saturate((dot(n, l.direction) + 0.2h) / 1.2h);
                // _Emission > 0 lights the whole object (fire); < 0 lights only its windows (vertex alpha 0.5)
                half window = step(0.4h, i.color.a) * step(i.color.a, 0.6h);
                half3 glow = emission > 0 ? albedo * emission : half3(1.0h, 0.52h, 0.18h) * (-emission) * window * 1.15h;
                half moon = saturate(dot(n, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day);
                // the planet hides a sun below the horizon (shadow maps reach only near the camera): round shapes such as
                // canopies would otherwise catch it from beneath at night
                half sunUp = saturate(dot(up, l.direction) * 4.0h + 0.35h);
                half3 c = albedo * (amb * lerp(half3(0.34h, 0.38h, 0.52h), half3(1.0h, 1.0h, 1.0h), day) + l.color * wrap * lerp(0.3h, 1.0h, l.shadowAttenuation) * 1.15h * sunUp + half3(0.13h, 0.17h, 0.29h) * moon) + glow;
                float dist = length(i.positionWS - _WorldSpaceCameraPos);
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h * (1.0h - saturate(abs(emission))));
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
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _WallColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
                UNITY_DEFINE_INSTANCED_PROP(float, _Anim)
            UNITY_INSTANCING_BUFFER_END(PerInstance)
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 sv(A v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 lp = v.positionOS.xyz, nos = v.normalOS;
                float anim = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Anim);
                if (anim > 0.5)
                {
                    // (the shadow of the posed body)
                    float3 origin = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                    TC2Pose(lp, nos, anim, _Time.y + dot(origin, float3(0.37, 0.61, 0.83)));
                }
                float3 pw = TransformObjectToWorld(lp);
                float3 nw = TransformObjectToWorldNormal(nos);
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
