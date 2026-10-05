using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Soft camera-facing puffs shared by every view that makes smoke, steam, dust, foam or glow (D2-029): views add
    /// them during the frame, the renderer sorts them far to near and draws them last. A billboard needs no rotation,
    /// so the instance matrix carries the puff's data (see Puff.shader): size, opacity, ash, glow, seed, sun share.
    /// </summary>
    public static class Puffs
    {
        internal static readonly List<Matrix4x4> Frame = new List<Matrix4x4>(4096);

        public static void Add(Vector3 at, float size, float alpha, float ash, float glow, float seed, float sun)
        {
            var m = Matrix4x4.identity;
            m.m03 = at.x; m.m13 = at.y; m.m23 = at.z;
            m.m00 = size; m.m11 = Mathf.Max(0.002f, alpha); m.m22 = Mathf.Max(0.002f, ash);
            m.m01 = glow; m.m02 = seed; m.m10 = sun;
            Frame.Add(m);
        }
    }

    [DefaultExecutionOrder(900)]
    public sealed class PuffRenderer : MonoBehaviour
    {
        public CameraRig Rig;
        Material _mat;
        Mesh _quad;
        float[] _keys = new float[4096];
        int[] _order = new int[4096];
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        public int Drawn;

        void Start()
        {
            _mat = Materials.Load("Puff");
            _mat.enableInstancing = true;
            _quad = new Mesh { name = "tc2-puff" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
        }

        void LateUpdate()
        {
            var list = Puffs.Frame;
            int n = list.Count;
            Drawn = n;
            if (n == 0 || _mat == null || Rig == null || Rig.Cam == null) { list.Clear(); return; }
            Vector3 cam = Rig.Cam.transform.position;
            if (_keys.Length < n) { _keys = new float[n * 2]; _order = new int[n * 2]; }
            for (int i = 0; i < n; i++)
            {
                var m = list[i];
                float dx = m.m03 - cam.x, dy = m.m13 - cam.y, dz = m.m23 - cam.z;
                _keys[i] = -(dx * dx + dy * dy + dz * dz); // far first
                _order[i] = i;
            }
            System.Array.Sort(_keys, _order, 0, n);
            var rp = new RenderParams(_mat)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e5f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                camera = Rig.Cam,
            };
            int done = 0;
            while (done < n)
            {
                int k = Mathf.Min(_batch.Length, n - done);
                for (int i = 0; i < k; i++) _batch[i] = list[_order[done + i]];
                Graphics.RenderMeshInstanced(rp, _quad, 0, _batch, k);
                done += k;
            }
            list.Clear();
        }
    }
}
