// TC2 underwater (D2-039, D2-040): things under the sea's surface - shoals of fish - lit by the sun through the water and
// veiled by its colour by their depth (as the sea floor is, Terrain.shader), with the silver flash of a turning fish;
// the thin sea surface is drawn over them. GPU instancing.
Shader "TC2/Underwater"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Float) = 0
        _Anim ("Walk", Float) = 0
        _Clear ("Clear water depth (m)", Float) = 3.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Clear;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
                UNITY_DEFINE_INSTANCED_PROP(float, _Anim)
            UNITY_INSTANCING_BUFFER_END(PerInstance)
            float4 _TC2FogColor;
            float _TC2FogDensity;
            float _TC2SeaRadius;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : COLOR; float flash : TEXCOORD2; half4 base : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.base = (half4)UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                // a fish catches the light as it turns: a flash of its own, at its own moments
                float3 origin = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                float ph = _Time.y * 3.1 + dot(origin, float3(1.37, 2.61, 0.83));
                o.flash = pow(saturate(sin(ph) * sin(ph * 0.37 + 1.3)), 6.0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light l = GetMainLight();
                float3 up = normalize(i.positionWS);
                half day = saturate(dot(up, l.direction) * 3.0h + 0.25h);
                half3 albedo = i.color.rgb * lerp(i.base.aaa, i.base.rgb, i.color.a);
                half wrap = saturate((dot(n, l.direction) + 0.4h) / 1.4h);
                half3 c = albedo * (half3(0.30h, 0.38h, 0.44h) + l.color * wrap * 0.9h) * (0.15h + 0.85h * day);
                c += half3(0.75h, 0.85h, 0.90h) * (half)i.flash * 0.9h * day;   // the flash of a turning flank
                // seen through the water above it, as the sea floor is
                float below = max(0.0, _TC2SeaRadius - length(i.positionWS));
                half through = (half)exp(-below / _Clear);
                half3 waterCol = lerp(half3(0.07h, 0.42h, 0.50h), half3(0.015h, 0.075h, 0.20h), (half)saturate(below / 28.0)) * (0.3h + 0.7h * day);
                c = lerp(waterCol, c, through);
                float dist = length(i.positionWS - _WorldSpaceCameraPos);
                half fog = 1.0h - exp(-dist * _TC2FogDensity);
                c = lerp(c, _TC2FogColor.rgb * (0.25h + 0.75h * day), fog * 0.85h);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
