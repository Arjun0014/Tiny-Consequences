// TC2 roads by use (C4.1, D2-097): a way worn between two villages as a ribbon on its route (WaterView.BuildRoads) - as wide as it is worn
// near the camera (a track, a path, a beaten road), never thinner than a pixel or two far off for the much-walked roads so the network
// reads from the region view, a faint track fading as it thins below a pixel; packed earth lit by the sun, darker at its edges where the
// grass begins; haze like the land. The same ribbon as the rivers (River.shader), drawn just above the ground toward the camera.
Shader "TC2/Road"
{
    Properties
    {
        _Earth ("Earth", Color) = (0.62, 0.52, 0.38, 1)
        _Edge ("Edge", Color) = (0.42, 0.40, 0.26, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-30" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Earth; half4 _Edge;
            CBUFFER_END
            float4 _TC2FogColor;
            float _TC2FogDensity;

            // uv0.xyz: the unit side vector, uv0.w: where across (-1 .. 1); uv1.x: half the drawn width near the camera (m), uv1.y: how
            // worn (0 a faint track .. 1 a beaten road)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 road : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 c = TransformObjectToWorld(v.positionOS.xyz);
                float d = distance(c, _WorldSpaceCameraPos);
                float px = 2.0 * d / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);      // metres a pixel spans there
                float minPx = 1.2 * smoothstep(0.45, 1.0, v.uv1.y);                     // the beaten roads: ~2.4 px across at least
                float hw = max(v.uv1.x, minPx * px);
                float3 wpos = c + v.uv0.xyz * (hw * v.uv0.w);
                wpos += normalize(_WorldSpaceCameraPos - wpos) * min(0.0015 * d, 30.0);
                o.positionWS = wpos;
                o.positionCS = TransformWorldToHClip(wpos);
                o.road = float4(v.uv0.w, saturate(hw / max(0.35 * px, 1e-4)), v.uv1.y, hw);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 up = normalize(i.positionWS);
                float3 camRel = i.positionWS - _WorldSpaceCameraPos;
                float dist = length(camRel);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half across = (half)saturate(abs(i.road.x));
                half lit = saturate(dot(up, l.direction) * 0.5h + 0.5h) * lerp(0.5h, 1.0h, l.shadowAttenuation);
                half3 c = lerp(_Earth.rgb, _Edge.rgb, smoothstep(0.55h, 1.0h, across)) * (0.25h + 0.75h * lit) * (0.12h + 0.88h * day);
                half worn = (half)i.road.z;
                half alpha = (0.35h + 0.6h * worn) * (1.0h - smoothstep(0.75h, 1.0h, across)) * (half)i.road.y;
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h);
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
