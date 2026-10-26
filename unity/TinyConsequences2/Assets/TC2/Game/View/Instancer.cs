using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// GPU-instanced drawing of one mesh, using Graphics.RenderMeshInstanced - available on WebGL2 and WebGPU (no BRG / Entities
    /// Graphics on the Web). Each instance carries its own colour, emission and walk (per-instance properties of TC2/Instanced and
    /// TC2/Underwater), so the instances go in batches of up to 1,023 in the order they were added - one draw a batch, whatever their
    /// colours - and each draw carries the bounds of its own instances, so the camera and every shadow cascade cull what they cannot
    /// see (D2-109: one draw per colour group, with the whole world as its bounds, drawn into every cascade, bounded the Web build's
    /// frame by the main thread's draw submission).
    /// </summary>
    public sealed class Instancer
    {
        /// <summary>Tools (webperf, the bridge's `cull`): whether draws carry their instances' bounds (else the whole world's).</summary>
        public static bool Cull = true;
        /// <summary>Tools: draws issued since the frame began (FrameClock resets it).</summary>
        public static int Draws;

        readonly Mesh _mesh;
        readonly Material _mat;
        readonly float _meshRadius;
        ShadowCastingMode _shadows;
        /// <summary>Whether the instances cast shadows (tools may switch it).</summary>
        public bool Shadows { get => _shadows == ShadowCastingMode.On; set => _shadows = value ? ShadowCastingMode.On : ShadowCastingMode.Off; }

        const int BatchSize = 1023;
        readonly List<Matrix4x4> _m = new List<Matrix4x4>(256);
        readonly List<Vector4> _c = new List<Vector4>(256), _w = new List<Vector4>(256);
        readonly List<float> _e = new List<float>(256), _a = new List<float>(256);
        readonly List<Vector3> _min = new List<Vector3>(), _max = new List<Vector3>();   // per batch
        readonly Matrix4x4[] _bm = new Matrix4x4[BatchSize];
        // (a property block's arrays keep the length they were first set with - so one block a size, the smallest that holds the batch: session 27,
        // context 4 - every draw had set and sent four arrays of 1,023 instances, five chimneys' as a town's homes', ~40 KB a draw: s27prof1's
        // writeBuffer 4.5 ms a frame)
        static readonly int[] Sizes = { 16, 64, 256, BatchSize };
        readonly Vector4[][] _bc = new Vector4[Sizes.Length][], _bw = new Vector4[Sizes.Length][];
        readonly float[][] _be = new float[Sizes.Length][], _ba = new float[Sizes.Length][];
        readonly MaterialPropertyBlock[] _mpb = new MaterialPropertyBlock[Sizes.Length];
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int WallColor = Shader.PropertyToID("_WallColor");   // (the kit's walls: vertex alpha 0.25, D2-114)
        static readonly int Emission = Shader.PropertyToID("_Emission");
        static readonly int Anim = Shader.PropertyToID("_Anim");
        static readonly Bounds World = new Bounds(Vector3.zero, Vector3.one * 60000f);
        static readonly bool Linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        public int Count => _m.Count;

        public Instancer(Mesh mesh, Material mat, bool shadows = true)
        {
            _mesh = mesh; _mat = mat; _shadows = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var b = mesh.bounds;
            _meshRadius = b.center.magnitude + b.extents.magnitude;
        }

        public void Clear()
        {
            _m.Clear(); _c.Clear(); _w.Clear(); _e.Clear(); _a.Clear(); _min.Clear(); _max.Clear();
            _lastColor = new Color(-1f, -1f, -1f, -1f); _lastWall = new Color(-1f, -1f, -1f, -1f);
        }

        // (colours come in runs: convert each only when it changes)
        Color _lastColor = new Color(-1f, -1f, -1f, -1f);
        Vector4 _lastLinear;

        /// <summary>Adds an instance with its colour (sRGB, as a material colour), emission and walk. <paramref name="group"/> is
        /// the caller's own grouping (it orders nothing here: kept so callers read as before).</summary>
        public void Add(int group, Color color, Matrix4x4 m, float emission = 0f, float anim = 0f) => Add(group, color, Color.white, m, emission, anim);

        Color _lastWall = new Color(-1f, -1f, -1f, -1f);
        Vector4 _lastWallLinear;

        /// <summary>Adds an instance with its colour (the kit's tinted parts: roofs, clothes) and its walls' colour (D2-114).</summary>
        public void Add(int group, Color color, Color wall, Matrix4x4 m, float emission = 0f, float anim = 0f)
        {
            if (color != _lastColor) { _lastColor = color; _lastLinear = Linear ? (Vector4)color.linear : (Vector4)color; }
            if (wall != _lastWall) { _lastWall = wall; _lastWallLinear = Linear ? (Vector4)wall.linear : (Vector4)wall; }
            // its batch's bounds: where it stands, padded by its mesh at its largest scale
            float sx = m.m00 * m.m00 + m.m10 * m.m10 + m.m20 * m.m20, sy = m.m01 * m.m01 + m.m11 * m.m11 + m.m21 * m.m21, sz = m.m02 * m.m02 + m.m12 * m.m12 + m.m22 * m.m22;
            float pad = Mathf.Sqrt(Mathf.Max(sx, Mathf.Max(sy, sz))) * _meshRadius;
            Vector3 lo = new Vector3(m.m03 - pad, m.m13 - pad, m.m23 - pad), hi = new Vector3(m.m03 + pad, m.m13 + pad, m.m23 + pad);
            int bi = _m.Count / BatchSize;
            if (bi == _min.Count) { _min.Add(lo); _max.Add(hi); }
            else { _min[bi] = Vector3.Min(_min[bi], lo); _max[bi] = Vector3.Max(_max[bi], hi); }
            _m.Add(m); _c.Add(_lastLinear); _w.Add(_lastWallLinear); _e.Add(emission); _a.Add(anim);
        }

        public void Draw(Camera cam)
        {
            int total = _m.Count;
            if (total == 0) return;
            var rp = new RenderParams(_mat) { shadowCastingMode = _shadows, receiveShadows = true, camera = null };
            for (int start = 0, bi = 0; start < total; start += BatchSize, bi++)
            {
                int n = Mathf.Min(BatchSize, total - start);
                int s = 0; while (Sizes[s] < n) s++;
                if (_mpb[s] == null)
                {
                    _mpb[s] = new MaterialPropertyBlock();
                    _bc[s] = new Vector4[Sizes[s]]; _bw[s] = new Vector4[Sizes[s]]; _be[s] = new float[Sizes[s]]; _ba[s] = new float[Sizes[s]];
                }
                var mpb = _mpb[s];
                _m.CopyTo(start, _bm, 0, n); _c.CopyTo(start, _bc[s], 0, n); _w.CopyTo(start, _bw[s], 0, n); _e.CopyTo(start, _be[s], 0, n); _a.CopyTo(start, _ba[s], 0, n);
                mpb.SetVectorArray(BaseColor, _bc[s]);
                mpb.SetVectorArray(WallColor, _bw[s]);
                mpb.SetFloatArray(Emission, _be[s]);
                mpb.SetFloatArray(Anim, _ba[s]);
                rp.matProps = mpb;
                var bounds = World;
                if (Cull) bounds.SetMinMax(_min[bi], _max[bi]);
                rp.worldBounds = bounds;
                Graphics.RenderMeshInstanced(rp, _mesh, 0, _bm, n);
                Draws++;
            }
        }
    }
}
