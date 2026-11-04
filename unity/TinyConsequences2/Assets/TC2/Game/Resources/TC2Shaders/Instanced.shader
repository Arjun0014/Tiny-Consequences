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
        // the joint forward (+z), a < 0 leans what stands above it forward. (session 32, Arjun: "most people I'm looking at are standing still and
        // doing nothing") the strokes are larger, so that they read from the village view (120-300 m, where a body is ten to twenty-five pixels
        // tall): the hoe and the axe raised high and brought down with the body, the reaper's bent trunk sweeping, a walker's step dipping the body
        // so the planted foot keeps the ground; and those in place at leisure are astir (codes 18-24): the weight shifted, a look round, a hand to
        // the hip or the brow, a stretch, an arm out toward what they face, a child hopping and clapping.
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
        // a turn about the body's upright through its middle (the trunk turned on the hips, the whole body turned to someone); also for normals
        float3 TC2Yaw(float3 p, float a)
        {
            float s = sin(a), c = cos(a);
            return float3(p.x * c + p.z * s, p.y, p.z * c - p.x * s);
        }
        // a tilt to a side about the z axis through (x0, py) (the weight shifted over a foot; an arm raised out to the side at the shoulder)
        float3 TC2Roll(float3 p, float x0, float py, float a)
        {
            float dx = p.x - x0, dy = p.y - py, s = sin(a), c = cos(a);
            return float3(x0 + dx * c - dy * s, py + dx * s + dy * c, p.z);
        }
        float3 TC2RollN(float3 n, float a)
        {
            float s = sin(a), c = cos(a);
            return float3(n.x * c - n.y * s, n.x * s + n.y * c, n.z);
        }
        // a stroke: slow up for `up` of the cycle, then a quick fall (an axe, a pick, a hammer): 0 struck .. 1 raised
        float TC2Stroke(float ph, float up)
        {
            float f = frac(ph);
            return f < up ? smoothstep(0.0, 1.0, f / up) : 1.0 - smoothstep(0.0, 1.0, (f - up) / (1.0 - up));
        }
        // a spell's own draw (which gesture a few seconds of leisure take) and its swell in and out (0 at its ends, 1 through its middle)
        float TC2Hash(float v) { return frac(sin(v * 12.9898 + 4.1414) * 43758.5453); }
        float TC2Swell(float f) { return smoothstep(0.0, 0.18, f) * (1.0 - smoothstep(0.82, 1.0, f)); }
        void TC2Pose(inout float3 lp, inout float3 n, float anim, float ph)
        {
            int code = (int)round(anim);
            if (code <= 0) return;
            bool arm = abs(lp.x) > 0.215 && abs(lp.z) < 0.09 && lp.y > 0.8;
            bool leg = lp.y < 0.88 && !arm;
            float side = lp.x >= 0 ? 1.0 : -1.0;
            float armL = 0.0, armR = 0.0;   // arm swings (left x < 0, right x > 0)
            float outL = 0.0, outR = 0.0;   // arms raised out to the side at the shoulder (radians)
            float legA = 0.0;                // this leg's swing
            float lean = 0.0;                // the trunk's lean forward (radians, < 0)
            float twist = 0.0;               // the trunk turned on the hips (radians)
            float turn = 0.0;                // the whole body turned about its upright
            float sway = 0.0;                // the whole body tilted over its feet (the weight shifted)
            float down = 0.0;                // the whole figure lowered (sitting)
            float bob = 0.0;
            if (code == 1 || code == 8 || code == 13 || code == 23)
            {
                // walking (8: with a load held before them; 13: running at play; 23: running in alarm - hard, bent to it, the arms pumping, now
                // and then a hand flung up and a look back)
                float run = code == 13 ? 1.5 : code == 23 ? 1.85 : 1.0;
                float t = ph * 7.0 * (code == 13 ? 1.45 : code == 23 ? 1.7 : 1.0);
                float st = sin(t);
                float amp = 0.55 * min(run, 1.45);
                legA = st * amp * side;
                armL = -st * 0.5 * run; armR = st * 0.5 * run;
                if (code == 8) { armL = 1.15; armR = 1.15; }
                // a walk dips as the legs part (the planted foot keeps the ground) and rises as they pass; a run springs off the ground
                float dip = 0.85 * (1.0 - cos(amp * st));   // (how far the parted legs bring the hips down)
                bob = code == 1 || code == 8 ? -dip : max(abs(st) * 0.11 * run - 0.5 * dip, -dip);
                lean = code == 13 ? -0.18 : code == 23 ? -0.32 : code == 8 ? -0.06 : -0.03;
                sway = st * 0.035;
                if (code == 23)
                {
                    float fl = frac(ph / 4.3);
                    float up = smoothstep(0.70, 0.78, fl) * (1.0 - smoothstep(0.90, 0.98, fl));
                    armR = lerp(armR, 2.85, up);
                    twist = 0.55 * up;
                }
            }
            else if (code == 2)
            {
                // hoeing: the hoe raised high over the shoulder and brought down before the feet, the body bending into the stroke
                float s = TC2Stroke(ph * 0.55, 0.6);
                armL = 0.2 + 2.2 * s; armR = armL;
                lean = -(0.22 + 0.38 * (1.0 - s));
                legA = 0.14 * side;
                bob = -0.05 * (1.0 - s);
            }
            else if (code == 3)
            {
                // reaping, gathering: bent low, the hands working at the crop by turns, the bent trunk sweeping from side to side with the sickle;
                // every few seconds up straight with the handful, laid by
                float t = ph * 2.6;
                lean = -(0.78 + 0.12 * sin(t * 0.5));
                armR = 0.55 + 0.6 * sin(t); armL = 0.7 - 0.35 * sin(t);
                twist = 0.38 * sin(t);
                legA = 0.15 * side;
                float g = frac(ph / 9.0);
                float up = smoothstep(0.80, 0.86, g) * (1.0 - smoothstep(0.94, 1.0, g));
                lean = lerp(lean, -0.12, up); twist *= 1.0 - up;
                armR = lerp(armR, 1.25, up); armL = lerp(armL, 1.25, up);
                bob = -0.04 * (1.0 - up);
            }
            else if (code == 4)
            {
                // an axe or a pick swung from high overhead, the body coming down with it
                float s = TC2Stroke(ph * 0.7, 0.7);
                armL = 0.3 + 2.75 * s; armR = armL;
                lean = -(0.04 + 0.44 * (1.0 - s));
                legA = 0.18 * side;
                bob = -0.06 * (1.0 - s);
            }
            else if (code == 5)
            {
                // hammering: the right hand strikes from high, the left holds the work
                float s = TC2Stroke(ph * 1.5, 0.55);
                armR = 0.4 + 1.9 * s; armL = 0.95;
                lean = -0.22 - 0.1 * (1.0 - s);
                twist = 0.12 * s;
            }
            else if (code == 6 || code == 9 || code == 15 || code == 16 || code == 19)
            {
                // sitting on the ground or a step (9: rowing; 15: work in the lap - spinning, mending, a pot on the wheel; 16: talking; 19: at
                // leisure - spells of leaning back on the hands, hugging the knees, a look round, a hand raised to a neighbour)
                legA = 1.45;
                down = 0.80;
                armL = 0.35; armR = 0.35;
                if (code == 9) { float t = ph * 2.2; armL = 0.9 + 0.6 * sin(t); armR = armL; lean = -0.25 * sin(t); }
                if (code == 15) { float t = ph * 3.0; armL = 0.95 + 0.18 * sin(t); armR = 0.95 - 0.18 * sin(t * 1.3); lean = -0.2 - 0.05 * sin(t * 0.4); }
                if (code == 16)
                {
                    float t = ph * 0.9;
                    armR = 0.35 + 1.05 * max(0.0, sin(t)) * (0.6 + 0.4 * sin(t * 2.7));
                    lean = -0.08 * sin(t * 0.5);
                    twist = 0.3 * sin(t * 0.31);
                }
                if (code == 19)
                {
                    float blk = floor(ph / 7.0), e = TC2Swell(frac(ph / 7.0)), g = TC2Hash(blk + 0.37);
                    float sg = TC2Hash(blk + 5.1) < 0.5 ? -1.0 : 1.0;
                    if (g < 0.3) { armL = lerp(0.35, -0.65, e); armR = armL; lean = 0.24 * e; }        // leaning back on the hands
                    else if (g < 0.55) { armL = 0.35 + 0.85 * e; armR = armL; lean = -0.28 * e; }      // hugging the knees
                    else if (g < 0.8) { twist = 0.75 * e * sg; lean = -0.05; }                          // a look round
                    else { armR = 0.35 + 1.4 * e * (0.8 + 0.2 * sin(ph * 3.0)); twist = 0.25 * e * sg; } // a hand raised to a neighbour
                }
            }
            else if (code == 7)
            {
                // talking: a hand that comes up as they speak, the other now and then, the trunk turned to those about them, a weight shifted
                float t = ph * 0.9;
                armR = 0.25 + 1.1 * max(0.0, sin(t)) * (0.6 + 0.4 * sin(t * 2.7));
                armL = 0.1 + 0.55 * max(0.0, sin(t * 0.63 + 2.0));
                lean = -0.05 * sin(t * 0.5);
                twist = 0.28 * sin(t * 0.37);
                sway = 0.04 * sin(t * 0.5 + 1.0);
            }
            else if (code == 10)
            {
                // at the loom: the hands before them, the shuttle thrown from side to side
                float t = ph * 2.4;
                armL = 1.15 + 0.35 * sin(t); armR = 1.15 - 0.35 * sin(t);
                lean = -0.1;
                twist = 0.12 * sin(t);
            }
            else if (code == 11)
            {
                // drawing water: hand over hand on the rope
                float t = ph * 2.0;
                armL = 1.6 + 0.8 * sin(t); armR = 1.6 - 0.8 * sin(t);
                lean = -0.12 - 0.08 * sin(t);
            }
            else if (code == 12)
            {
                // sparring: thrusts, the front foot forward
                float t = ph * 2.5;
                armL = 1.25 + 0.55 * max(0.0, sin(t)); armR = armL;
                legA = 0.38 * side;
                lean = -0.1 - 0.15 * max(0.0, sin(t));
                twist = 0.2 * sin(t * 0.5);
                bob = 0.03 * abs(sin(t));
            }
            else if (code == 14)
            {
                // fishing from the shore: the rod held out, a twitch now and then, and every ten seconds or so the line cast again (the rod
                // swept up and back over the shoulder and flicked out)
                float c = frac(ph / 10.0);
                float back = smoothstep(0.0, 0.06, c) * (1.0 - smoothstep(0.06, 0.12, c));
                armL = 1.0 + 0.08 * sin(ph * 1.3) + 1.6 * back; armR = armL;
                lean = -0.06 + 0.12 * back;
            }
            else if (code == 17)
            {
                // threshing with a flail (C3.7b): the swingle whirled up over the shoulder and brought down flat on the sheaves on the floor,
                // upright, to the beat of the others on the floor
                float s = TC2Stroke(ph * 1.2, 0.45);
                armL = 0.75 + 2.1 * s; armR = armL;
                lean = -(0.10 + 0.26 * (1.0 - s));
                legA = 0.18 * side;
            }
            else if (code == 18)
            {
                // standing at leisure: the weight shifted from foot to foot, and spells of a look round, the hands to the hips, a hand to the head,
                // the arms folded, a stretch, turned to someone near
                float blk = floor(ph / 6.0), e = TC2Swell(frac(ph / 6.0)), g = TC2Hash(blk + 0.71);
                float sg = TC2Hash(blk + 3.3) < 0.5 ? -1.0 : 1.0;
                sway = 0.05 * sin(ph * 0.8);
                legA = 0.08 * side * sin(ph * 0.8);
                if (g < 0.22) { twist = 0.75 * sg * e; turn = 0.3 * sg * e; }
                else if (g < 0.4) { outL = 0.6 * e; outR = 0.6 * e; armL = -0.3 * e; armR = armL; }
                else if (g < 0.55) { if (sg > 0.0) armR = 2.6 * e; else armL = 2.6 * e; }
                else if (g < 0.72) { armL = 0.9 * e; armR = 0.9 * e; lean = -0.04 * e; }
                else if (g < 0.84) { armL = 2.9 * e; armR = armL; lean = 0.12 * e; }
                else { turn = 0.95 * sg * e; armR = 0.6 * e * max(0.0, sin(ph * 2.2)); }
            }
            else if (code == 20)
            {
                // pointing: an arm out toward what they face, held with a jab or two, then lowered to turn and speak to the others; again
                float blk = floor(ph / 5.0), e = TC2Swell(frac(ph / 5.0)), g = TC2Hash(blk + 1.9);
                float talks = step(0.55, g);
                armR = 0.15 + (1.6 + 0.1 * sin(ph * 4.5)) * e * (1.0 - 0.6 * talks);
                armL = 0.15 + 0.7 * talks * e * max(0.0, sin(ph * 1.7));
                twist = -0.5 * talks * e;
                lean = -0.06 * e;
                sway = 0.03 * sin(ph * 0.9);
            }
            else if (code == 21)
            {
                // looking out: a hand to the brow, the trunk turning slowly across what they watch, the weight shifted; now an arm out at what they
                // see, now the hands to the hips
                float blk = floor(ph / 8.0), e = TC2Swell(frac(ph / 8.0)), g = TC2Hash(blk + 2.3);
                twist = 0.6 * sin(ph * 0.45);
                sway = 0.04 * sin(ph * 0.3 + 1.0);
                lean = -0.05;
                if (g < 0.5) armR = 0.15 + 2.45 * e;
                else if (g < 0.75) armL = 0.15 + 1.5 * e;
                else { outL = 0.6 * e; outR = 0.6 * e; armL = -0.3 * e; armR = armL; }
            }
            else if (code == 22)
            {
                // stoking a fire or a clamp: bent to it, a pole worked in with both hands - thrust and drawn back
                float s = TC2Stroke(ph * 0.8, 0.5);
                armL = 0.65 + 0.7 * s; armR = armL;
                lean = -(0.32 + 0.22 * s);
                legA = 0.22 * side;
                bob = -0.04 * s;
            }
            else if (code == 24)
            {
                // a child at play hopping and clapping
                float t = ph * 6.0;
                bob = abs(sin(t)) * 0.24;
                legA = -0.3 * abs(sin(t));
                armL = 1.25 + 0.3 * sin(t * 2.0); armR = armL;
                outL = 0.2 + 0.25 * (0.5 + 0.5 * sin(t * 2.0)); outR = outL;
            }
            if (arm)
            {
                float a = lp.x < 0 ? armL : armR;
                float o = lp.x < 0 ? outL : outR;
                lp = TC2Roll(lp, side * 0.215, 1.5, side * o); n = TC2RollN(n, side * o);
                lp = TC2Rot(lp, 1.5, a); n = TC2RotN(n, a);
            }
            if (leg)
            {
                lp = TC2Rot(lp, 0.9, legA); n = TC2RotN(n, legA);
            }
            else
            {
                lp = TC2Rot(lp, 0.9, lean); n = TC2RotN(n, lean);
                lp = TC2Yaw(lp, twist); n = TC2Yaw(n, twist);
            }
            lp = TC2Yaw(lp, turn); n = TC2Yaw(n, turn);
            lp = TC2Roll(lp, 0.0, 0.0, sway); n = TC2RollN(n, sway);
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
            #include "TC2CloudForm.hlsl"

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
            float _TC2NightLift;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; half4 base : TEXCOORD2; half emission : TEXCOORD3; half4 wall : TEXCOORD4; half snow : TEXCOORD5; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.base = (half4)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                o.wall = (half4)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _WallColor);
                o.emission = (half)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Emission);
                float anim = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Anim);
                o.snow = (half)saturate(-anim);   // (session 27, W-FOREST: _Anim < 0 - a tree under snow, how much lies on its boughs)
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
                // (session 27, W-FOREST) snow lies on what faces the sky - the tiers of a spruce white above, its needles dark beneath (before, the whole
                // tree was paled toward white and the snowy woods read washed out)
                albedo = lerp(albedo, half3(0.84h, 0.86h, 0.90h), i.snow * smoothstep(0.30h, 0.72h, dot(n, up)));
                half wrap = saturate((dot(n, l.direction) + 0.2h) / 1.2h);
                // _Emission > 0 lights the whole object (fire); < 0 lights only its windows (vertex alpha 0.5)
                half window = step(0.4h, i.color.a) * step(i.color.a, 0.6h);
                half3 glow = emission > 0 ? albedo * emission : half3(1.0h, 0.52h, 0.18h) * (-emission) * window * 1.15h;
                half moon = saturate(dot(n, _TC2MoonDir.xyz) * 0.7h + 0.3h) * (1.0h - day) * (half)_TC2NightLift;
                // the planet hides a sun below the horizon (shadow maps reach only near the camera): round shapes such as
                // canopies would otherwise catch it from beneath at night
                half sunUp = saturate(dot(up, l.direction) * 4.0h + 0.35h);
                // (session 27) under a cloud's shadow as the ground round it is (TC2CloudForm.hlsl)
                sunUp *= 1.0h - TC2CloudShadow(i.positionWS, l.direction, length(fwidth(i.positionWS)), _TC2CloudScale.z);
                half3 c = albedo * (amb * lerp(half3(0.34h, 0.38h, 0.52h) * (half)_TC2NightLift, half3(1.0h, 1.0h, 1.0h), day) + l.color * wrap * lerp(0.3h, 1.0h, l.shadowAttenuation) * 1.15h * sunUp + half3(0.13h, 0.17h, 0.29h) * moon) + glow;
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
