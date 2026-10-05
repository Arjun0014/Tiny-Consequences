using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TC2.Game
{
    /// <summary>
    /// Tilt-shift (13 §1, D2-027): the miniature cue of a photographed diorama - a sharp band across the middle of the
    /// frame, a blur growing toward the top and bottom. Two separable blur passes before post-processing; strength
    /// from the global <c>_TC2TiltShift</c> (0 from orbit, up to 1 close to the ground), set by <see cref="Sky"/>.
    /// </summary>
    public sealed class TiltShiftFeature : ScriptableRendererFeature
    {
        public Material Material;
        public static bool Off;   // (tools: the frame cost measured without it)
        Pass _pass;
        static readonly int Strength = Shader.PropertyToID("_TC2TiltShift");

        public override void Create()
        {
            _pass = new Pass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing, requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Off || Material == null || Shader.GetGlobalFloat(Strength) < 0.01f) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            _pass.Material = Material;
            renderer.EnqueuePass(_pass);
        }

        sealed class Pass : ScriptableRenderPass
        {
            public Material Material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var res = frameData.Get<UniversalResourceData>();
                if (res.isActiveTargetBackBuffer) return;
                TextureHandle src = res.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(src);
                desc.name = "TC2TiltShift";
                desc.clearBuffer = false;
                TextureHandle tmp = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, tmp, Material, 0), "TC2 tilt-shift H");
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(tmp, src, Material, 1), "TC2 tilt-shift V");
            }
        }
    }
}
