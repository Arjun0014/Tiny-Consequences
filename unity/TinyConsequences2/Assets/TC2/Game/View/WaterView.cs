using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Places;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Rivers and lakes from the drainage (F1): a ribbon from every river cell to the cell it drains into, wider
    /// with more water; lake surfaces at the basin's filled level. Rebuilt when the drainage changes and every few
    /// seconds as flows change. Reads the world only.
    /// </summary>
    public sealed class WaterView : MonoBehaviour
    {
        public SimHost Host;
        const int Pieces = 8;
        Mesh _rivers, _lakes;
        Material _riverMat, _lakeMat;
        int _builtWorld = -1, _builtDrain = -1;
        float _timer;
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();

        void Start()
        {
            _riverMat = Materials.Load("River");   // (D2-084: the river's own water)
            _lakeMat = Materials.Load("Water");
            _lakeMat.SetColor("_Deep", new Color(0.10f, 0.30f, 0.42f));
            _lakeMat.SetColor("_Shallow", new Color(0.22f, 0.48f, 0.58f));
            _lakeMat.SetFloat("_Alpha", 0.9f);
            _rivers = new Mesh { name = "tc2-rivers", indexFormat = IndexFormat.UInt32 };
            _lakes = new Mesh { name = "tc2-lakes", indexFormat = IndexFormat.UInt32 };
            var r = new GameObject("Rivers"); r.transform.SetParent(transform, false);
            r.AddComponent<MeshFilter>().sharedMesh = _rivers;
            var rr = r.AddComponent<MeshRenderer>(); rr.sharedMaterial = _riverMat; rr.shadowCastingMode = ShadowCastingMode.Off;
            var l = new GameObject("Lakes"); l.transform.SetParent(transform, false);
            l.AddComponent<MeshFilter>().sharedMesh = _lakes;
            var lr = l.AddComponent<MeshRenderer>(); lr.sharedMaterial = _lakeMat; lr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Update()
        {
            var w = Host.World;
            if (w == null || w.Drainage == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_builtWorld == Host.WorldVersion && _builtDrain == w.Drainage.Version && _timer > 0) return;
            _timer = 5f;
            _builtWorld = Host.WorldVersion; _builtDrain = w.Drainage.Version;
            BuildRivers(w);
            BuildLakes(w);
        }

        readonly List<Vector4> _u0 = new List<Vector4>(), _u1 = new List<Vector4>();

        /// <summary>
        /// The rivers' water (D2-084): a ribbon on each piece of the rivers' shape (<see cref="Rivers"/>) - its centre line on the
        /// ground with the side and half width as vertex data, so the river shader can widen the planet's larger rivers to a few pixels
        /// far off - and a round joint where each reach begins, so bends do not gap.
        /// </summary>
        void BuildRivers(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            Rivers.Refresh(w, Host.WorldVersion);
            _v.Clear(); _n.Clear(); _u0.Clear(); _u1.Clear(); _t.Clear();
            var all = Rivers.All;
            Vector3 sidePrev = Vector3.zero;
            for (int q = 0; q < all.Count; q++)
            {
                var pc = all[q];
                bool first = q % Rivers.Pieces == 0;
                Vector3 a = PlanetGeo.Surface(P, pc.A, 0.6f), b = PlanetGeo.Surface(P, pc.B, 0.6f);
                Vector3 up = (a + b).normalized;
                Vector3 side = Vector3.Cross(up, b - a).normalized;
                if (first) sidePrev = side;
                var u1 = new Vector4(pc.Half, pc.Rank, 0f, 0f);
                int i = _v.Count;
                _v.Add(a); _v.Add(a); _v.Add(b); _v.Add(b);
                _u0.Add(new Vector4(sidePrev.x, sidePrev.y, sidePrev.z, -1f)); _u0.Add(new Vector4(sidePrev.x, sidePrev.y, sidePrev.z, 1f));
                _u0.Add(new Vector4(side.x, side.y, side.z, 1f)); _u0.Add(new Vector4(side.x, side.y, side.z, -1f));
                for (int k = 0; k < 4; k++) { _n.Add(up); _u1.Add(u1); }
                _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); _t.Add(i); _t.Add(i + 3); _t.Add(i + 2);
                sidePrev = side;
                if (!first) continue;
                // the joint where the reach leaves its cell's centre (where the reaches above it end)
                Vector3 e1 = Vector3.Cross(up, Vector3.up).sqrMagnitude > 1e-6f ? Vector3.Cross(up, Vector3.up).normalized : Vector3.right;
                Vector3 e2 = Vector3.Cross(up, e1);
                int j = _v.Count;
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4f;
                    Vector3 rad = e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang);
                    _v.Add(a); _n.Add(up); _u0.Add(new Vector4(rad.x, rad.y, rad.z, 1f)); _u1.Add(u1);
                }
                _v.Add(a); _n.Add(up); _u0.Add(new Vector4(e1.x, e1.y, e1.z, 0f)); _u1.Add(u1);
                for (int k = 0; k < 8; k++) { _t.Add(j + 8); _t.Add(j + k); _t.Add(j + (k + 1) % 8); }
            }
            _rivers.Clear();
            _rivers.SetVertices(_v); _rivers.SetNormals(_n); _rivers.SetUVs(0, _u0); _rivers.SetUVs(1, _u1); _rivers.SetTriangles(_t, 0);
            // (the ribbons widen in the shader: bounds a little larger than the centre lines)
            var bd = new Bounds(Vector3.zero, Vector3.zero);
            if (_v.Count > 0) { bd = new Bounds(_v[0], Vector3.zero); for (int k = 1; k < _v.Count; k++) bd.Encapsulate(_v[k]); bd.Expand(400f); }
            _rivers.bounds = bd;
        }

        void BuildLakes(TC2.Core.Simulation.World w)
        {
            var P = w.Planet; var d = w.Drainage;
            _v.Clear(); _n.Clear(); _c.Clear(); _t.Clear();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLake(c)) continue;
                Vec3d ctr = P.Grid.Center[c];
                Settlement.Frame(ctr, out Vec3d east, out Vec3d north);
                double half = P.CellSize * 0.62;
                double r = P.Radius + Ground.WaterHeight(P, P.Elev[c] + P.LakeDepth[c]) + 0.4;
                int i = _v.Count;
                for (int k = 0; k < 4; k++)
                {
                    double sx = (k == 0 || k == 3) ? -half : half, sy = k < 2 ? -half : half;
                    Vec3d p = (ctr * P.Radius + east * sx + north * sy).Normalized;
                    _v.Add(new Vector3((float)(p.X * r), (float)(p.Y * r), (float)(p.Z * r)));
                    _n.Add(PlanetGeo.ToVector(p)); _c.Add(Color.white);
                }
                _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); _t.Add(i); _t.Add(i + 3); _t.Add(i + 2);
            }
            _lakes.Clear();
            _lakes.SetVertices(_v); _lakes.SetNormals(_n); _lakes.SetColors(_c); _lakes.SetTriangles(_t, 0);
            _lakes.RecalculateBounds();
        }
    }
}
