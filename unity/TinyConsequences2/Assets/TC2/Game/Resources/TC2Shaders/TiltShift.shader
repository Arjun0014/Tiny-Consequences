// TC2 tilt-shift (D2-027): a separable blur whose radius grows from a sharp band across the middle of the frame
// toward its top and bottom - the look of a photographed miniature. Strength from _TC2TiltShift.
Shader "TC2/TiltShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float _TC2TiltShift;

        half4 Blur(float2 uv, float2 axis)
        {
            float band = abs(uv.y - 0.52) * 2.0;                      // 0 on the sharp line, 1 at the edges
            float radius = smoothstep(0.35, 1.0, band) * _TC2TiltShift * 7.0;   // in texels (a sharp middle: D2-084)
            float2 stepUv = axis * radius / _ScreenParams.xy;
            // 9 taps, gaussian weights
            half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv * 1.3846) * 0.3162162h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv * 1.3846) * 0.3162162h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv * 3.2308) * 0.0702703h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv * 3.2308) * 0.0702703h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv * 0.6923) * 0.3162162h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv * 0.6923) * 0.3162162h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + stepUv * 2.3077) * 0.0702703h * 0.5h;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - stepUv * 2.3077) * 0.0702703h * 0.5h;
            return c / (0.2270270h + 0.3162162h * 2.0h + 0.0702703h * 2.0h);
        }
        ENDHLSL
        Pass
        {
            Name "TiltShiftH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragH
            half4 FragH(Varyings i) : SV_Target { return Blur(i.texcoord, float2(1, 0)); }
            ENDHLSL
        }
        Pass
        {
            Name "TiltShiftV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragV
            half4 FragV(Varyings i) : SV_Target { return Blur(i.texcoord, float2(0, 1)); }
            ENDHLSL
        }
    }
}
