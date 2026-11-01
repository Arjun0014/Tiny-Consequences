// TC2 sky (session 31): the sky seen from near the ground, behind everything: the horizon's colour rising to the zenith's, a soft glow toward the sun (warm when it is low).
// It fades out as the drawn air takes over from higher up (_TC2SkyFade). Colours from Sky.cs: the world's hour, nothing else.
Shader "TC2/SkyDome"
{
    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off ZTest Always Cull Front
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _TC2SunDir;
            float4 _TC2SkyHorizon;   // rgb, a: the sky's share of the picture (1 near the ground, 0 once the air's shell is drawn)
            float4 _TC2SkyZenith;    // rgb, a: how low the sun is (0 high, 1 at the horizon)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 view : TEXCOORD0; float3 up : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(wp);
                o.view = wp - _WorldSpaceCameraPos;
                o.up = normalize(_WorldSpaceCameraPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.view);
                float h = dot(d, normalize(i.up));
                // on a small planet the true horizon lies below the eye's level: the band of pale air hugs it, the blue deepens quickly above it
                half k = (half)pow(saturate((h + 0.10) / 0.95), 0.62);
                half3 c = lerp(_TC2SkyHorizon.rgb, _TC2SkyZenith.rgb, k);
                float mu = saturate(dot(d, _TC2SunDir.xyz));
                half low = (half)_TC2SkyZenith.a;
                half glow = (half)(pow(mu, 24.0) * 0.28 + pow(mu, 6.0) * 0.10);
                c += lerp(half3(0.95h, 0.92h, 0.82h), half3(1.0h, 0.58h, 0.32h), low) * glow * saturate(_TC2SunDir.w);
                return half4(c, _TC2SkyHorizon.a);
            }
            ENDHLSL
        }
    }
}
