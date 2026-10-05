// TC2 rain and snow (D2-033): a cylinder of falling streaks (or flakes) round the camera, procedural (no texture),
// strength from the day's rain where the camera is. _Rain: x = strength 0..1, y = snow 0..1, z = time, w = wind lean.
Shader "TC2/Rain"
{
    Properties
    {
        _Rain ("Rain", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Rain;
            CBUFFER_END
            float4 _TC2FogColor;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.uv = v.uv;
                return o;
            }

            float hash11(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            half4 frag(Varyings i) : SV_Target
            {
                float strength = _Rain.x, snow = _Rain.y, t = _Rain.z;
                if (strength <= 0.001) discard;
                // columns round the cylinder, each with its own offset, speed and length
                float cols = lerp(220.0, 140.0, snow);
                float x = i.uv.x * cols + i.uv.y * _Rain.w * 6.0;   // the wind leans the streaks
                float col = floor(x);
                float fx = frac(x);
                float h = hash11(col), h2 = hash11(col + 31.7);
                float speed = lerp(lerp(1.6, 2.4, h2), lerp(0.10, 0.18, h2), snow);
                float y = i.uv.y * lerp(5.0, 9.0, snow) + t * speed + h * 7.0;
                float fy = frac(y);
                // rain: a thin bright streak; snow: a soft round flake
                float len = lerp(0.20 + 0.15 * h2, 0.035, snow);
                float width = lerp(0.07, 0.22, snow);
                float sx = 1.0 - smoothstep(0.0, width, abs(fx - 0.5) * 2.0 * lerp(1.0, 0.5, snow));
                float sy = smoothstep(0.0, len * 0.3, fy) * (1.0 - smoothstep(len * 0.5, len, fy));
                // only some columns carry a drop at a time: denser in a downpour
                float live = step(hash11(col + floor(y) * 13.1), lerp(0.25, 0.85, strength));
                float a = sx * sy * live * lerp(0.55, 0.8, snow) * saturate(strength * 1.4);
                // fade at the cylinder's top and bottom
                a *= smoothstep(0.0, 0.15, i.uv.y) * (1.0 - smoothstep(0.85, 1.0, i.uv.y));
                Light l = GetMainLight();
                half day = saturate(dot(normalize(i.positionWS), l.direction) * 3.0h + 0.25h);
                half3 c = lerp(half3(0.62h, 0.66h, 0.72h), half3(0.95h, 0.96h, 1.0h), snow) * (0.25h + 0.75h * day);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
