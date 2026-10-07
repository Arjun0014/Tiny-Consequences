using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Tools (webperf, D2-109): the main thread's time a frame from the first script's Update to the end of the frame's rendering -
    /// the CPU's part of a frame (smoothed). Near the frame's interval: the CPU bounds the frame; well below it: the GPU (or the display).
    /// Also the instanced draws the last frame issued.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class FrameClock : MonoBehaviour
    {
        public static float CpuMs;
        public static int LastDraws;
        double _t0;
        void OnEnable() { UnityEngine.Rendering.RenderPipelineManager.endContextRendering += Ended; }
        void OnDisable() { UnityEngine.Rendering.RenderPipelineManager.endContextRendering -= Ended; }
        void Update() { _t0 = Time.realtimeSinceStartupAsDouble; LastDraws = Instancer.Draws; Instancer.Draws = 0; }
        void Ended(UnityEngine.Rendering.ScriptableRenderContext ctx, System.Collections.Generic.List<Camera> cams)
        {
            if (_t0 <= 0) return;
            float ms = (float)((Time.realtimeSinceStartupAsDouble - _t0) * 1000.0);
            CpuMs = CpuMs <= 0f ? ms : CpuMs + (ms - CpuMs) * 0.1f;
        }
    }
}
