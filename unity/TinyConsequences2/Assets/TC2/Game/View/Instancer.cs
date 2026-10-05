using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// GPU-instanced drawing of one mesh in colour groups (one draw per group of up to 1,023 instances),
    /// using Graphics.RenderMeshInstanced - available on WebGL2 and WebGPU (no BRG / Entities Graphics on the Web).
    /// </summary>
    public sealed class Instancer
    {
        readonly Mesh _mesh;
        readonly Material _mat;
        readonly ShadowCastingMode _shadows;
        readonly Dictionary<int, List<Matrix4x4>> _groups = new Dictionary<int, List<Matrix4x4>>();
        readonly List<int> _keys = new List<int>();
        readonly Dictionary<int, Color> _colors = new Dictionary<int, Color>();
        readonly Dictionary<int, float> _emission = new Dictionary<int, float>();
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_Emission");
        public int Count;

        public Instancer(Mesh mesh, Material mat, bool shadows = true)
        {
            _mesh = mesh; _mat = mat; _shadows = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        public void Clear()
        {
            foreach (var k in _keys) _groups[k].Clear();
            Count = 0;
        }

        /// <summary>Adds an instance in colour group <paramref name="group"/> (colour set once per group).</summary>
        public void Add(int group, Color color, Matrix4x4 m, float emission = 0f)
        {
            if (!_groups.TryGetValue(group, out var list))
            {
                list = new List<Matrix4x4>(256);
                _groups[group] = list;
                _keys.Add(group);
            }
            _colors[group] = color;
            _emission[group] = emission;
            list.Add(m);
            Count++;
        }

        public void Draw(Camera cam)
        {
            var rp = new RenderParams(_mat) { shadowCastingMode = _shadows, receiveShadows = true, matProps = _mpb, camera = null, worldBounds = new Bounds(Vector3.zero, Vector3.one * 60000f) };
            for (int g = 0; g < _keys.Count; g++)
            {
                int key = _keys[g];
                var list = _groups[key];
                if (list.Count == 0) continue;
                _mpb.SetColor(BaseColor, _colors[key]);
                _mpb.SetFloat(Emission, _emission[key]);
                rp.matProps = _mpb;
                for (int start = 0; start < list.Count; start += _batch.Length)
                {
                    int n = Mathf.Min(_batch.Length, list.Count - start);
                    list.CopyTo(start, _batch, 0, n);
                    Graphics.RenderMeshInstanced(rp, _mesh, 0, _batch, n);
                }
            }
        }
    }
}
