// TC2 sea: translucent, Fresnel sky reflection, sun glint, gentle procedural ripples.
Shader "TC2/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.03, 0.12, 0.26, 1)
        _Shallow ("Shallow", Color) = (0.10, 0.38, 0.52, 1)
        _Sky ("Sky", Color) = (0.55, 0.70, 0.90, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.82
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep; half4 _Shallow; half4 _Sky; half _Alpha;
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
                float3 n = normalize(i.normalWS);
                float t = _Time.y;
                float3 q = i.positionWS * 0.02;
                float dist = length(_WorldSpaceCameraPos - i.positionWS);
                float near = saturate(1.0 - dist / 6000.0);
                n = normalize(n + 0.035 * near * float3(sin(q.x * 3.1 + t * 0.7) + sin(q.z * 2.3 - t * 0.5), sin(q.y * 2.7 + t * 0.6), sin(q.z * 3.3 + t * 0.4) + sin(q.x * 1.9 - t * 0.8)));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light l = GetMainLight();
                float3 up = normalize(i.positionWS);
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half fres = pow(1.0h - saturate(dot(n, v)), 4.0h);
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, saturate(dot(n, v)));
                float3 h = normalize(l.direction + v);
                half spec = pow(saturate(dot(n, h)), lerp(48.0h, 180.0h, near)) * lerp(0.16h, 3.0h, near);
                half3 c = (body * (0.25h + 0.75h * saturate(dot(n, l.direction) * 0.6h + 0.4h)) + _Sky.rgb * fres * 0.6h) * (0.15h + 0.85h * day) + l.color * spec * day;
                return half4(c, saturate(_Alpha + fres * 0.3h));
            }
            ENDHLSL
        }
    }
}
