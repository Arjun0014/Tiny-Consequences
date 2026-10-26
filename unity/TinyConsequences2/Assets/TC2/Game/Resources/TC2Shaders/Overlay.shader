// TC2 overlay (session 27: the powers as play): marks laid on the ground for the player - the ring where an aimed power falls, the burst when it
// is cast. Unlit vertex colours (their alpha the mark's), drawn over the ground just above it, through no other surface; a soft glow on the
// brightest parts. Presentation only.
Shader "TC2/Overlay"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // the brighter the mark, the more it glows (HDR: the bloom takes the band's edge)
                return half4(i.color.rgb * (1.0h + 0.8h * i.color.a), i.color.a);
            }
            ENDHLSL
        }
    }
}
