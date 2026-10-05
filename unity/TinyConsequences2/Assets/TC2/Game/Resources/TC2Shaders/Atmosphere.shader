// TC2 atmosphere: an additive rim around the planet, brighter on the day side, warm at the terminator.
Shader "TC2/Atmosphere"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.60, 1.0, 1)
        _Sunset ("Sunset", Color) = (1.0, 0.55, 0.30, 1)
        _Power ("Power", Float) = 3.0
        _Strength ("Strength", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half4 _Sunset; half _Power; half _Strength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = -normalize(i.normalWS); // inner side faces the camera
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light l = GetMainLight();
                half rim = pow(saturate(1.0h - abs(dot(n, v))), _Power);
                float3 up = normalize(i.positionWS);
                half sun = dot(up, l.direction);
                half day = saturate(sun * 2.0h + 0.35h);
                half terminator = saturate(1.0h - abs(sun) * 4.0h);
                half3 c = (_Color.rgb * day + _Sunset.rgb * terminator * 0.6h) * rim * _Strength;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
