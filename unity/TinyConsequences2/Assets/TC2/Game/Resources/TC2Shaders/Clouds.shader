// TC2 clouds (D2-027): a soft shell whose coverage comes from the world's weather (_TC2Clouds cubemap), broken up by
// drifting noise; sunlit tops, darker bellies, a rim of light at the terminator, nothing on the night side but a faint
// grey. Fades out as the camera comes under the layer (_TC2CloudFade).
Shader "TC2/Clouds"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURECUBE(_TC2Clouds); SAMPLER(sampler_TC2Clouds);
            TEXTURE2D(_TC2Detail); SAMPLER(sampler_TC2Detail);
            float _TC2CloudFade;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                return o;
            }

            half Tri(float3 p, float3 bw, float s, float2 drift)
            {
                half x = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.yz * s + drift).r;
                half y = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.xz * s + drift).r;
                half z = SAMPLE_TEXTURE2D(_TC2Detail, sampler_TC2Detail, p.xy * s + drift).r;
                return x * bw.x + y * bw.y + z * bw.z;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 up = normalize(i.positionWS);
                float3 bw = abs(up); bw = bw * bw; bw /= (bw.x + bw.y + bw.z);
                float t = _Time.y;
                half n1 = Tri(i.positionWS, bw, 1.0 / 2600.0, float2(t * 0.0021, t * 0.0013));
                half n2 = Tri(i.positionWS, bw, 1.0 / 620.0, float2(-t * 0.0047, t * 0.0031));
                half n3 = Tri(i.positionWS.zxy, bw.zxy, 1.0 / 1900.0, float2(t * 0.0017, -t * 0.0023));
                // the weather's cover, looked up through a noise warp so its cells never show as blocks
                float3 warp = (float3(n1, n3, n2) - 0.5) * 0.06;
                half cover = SAMPLE_TEXTURECUBE(_TC2Clouds, sampler_TC2Clouds, normalize(up + warp)).r;
                half density = smoothstep(0.40h, 0.80h, cover * (0.50h + 0.80h * n1) + (n2 - 0.5h) * 0.30h + (n3 - 0.5h) * 0.18h);
                Light l = GetMainLight();
                half sun = saturate(dot(up, l.direction));
                half day = saturate(dot(up, l.direction) * 3.0h + 0.2h);
                half rim = saturate(1.0h - abs(dot(up, l.direction)) * 4.0h) * 0.35h;
                half3 lit = lerp(half3(0.55h, 0.58h, 0.66h), half3(1.0h, 0.98h, 0.95h), sun) * (0.15h + 0.85h * day);
                half3 c = lit * (0.78h + 0.22h * n2) + half3(1.0h, 0.65h, 0.45h) * rim * day;
                half alpha = density * 0.88h * _TC2CloudFade * (0.35h + 0.65h * day);
                return half4(c, alpha);
            }
            ENDHLSL
        }
    }
}
