// TC2 stars (session 31): the night behind the planet from high up: small soft points of light (each a quad facing the camera, its size in the mesh), never over the planet (drawn first, the air and the ground over them).
Shader "TC2/Stars"
{
    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background+1" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _TC2StarFade;
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                // (a direction and a size in the mesh: the star stands at infinity, its quad a constant size on the screen whatever the camera's height)
                float3 d = normalize(v.positionOS.xyz);
                float4 cp = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos + d * 1000.0, 1.0));
                float2 px = 2.0 / _ScreenParams.xy;
                cp.xy += v.uv * v.color.a * 2.6 * px * cp.w;
                #if UNITY_REVERSED_Z
                    cp.z = 0.00001 * cp.w;
                #else
                    cp.z = 0.99999 * cp.w;
                #endif
                o.positionCS = cp;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half d = (half)dot(i.uv, i.uv);
                half a = saturate(1.0h - d);
                return half4(i.color.rgb, a * a * (half)_TC2StarFade);
            }
            ENDHLSL
        }
    }
}
