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
        Mesh _rivers, _lakes, _roads;
        Material _riverMat, _lakeMat, _roadMat;
        float _roadTimer;
        readonly List<TC2.Core.Agents.Way> _ways = new List<TC2.Core.Agents.Way>();
        readonly List<Vec3d> _pts = new List<Vec3d>(), _tmp = new List<Vec3d>();
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
            _roadMat = Materials.Load("Road");   // (C4.1: the ways people wore between villages)
            _roads = new Mesh { name = "tc2-roads", indexFormat = IndexFormat.UInt32 };
            var rd = new GameObject("Roads"); rd.transform.SetParent(transform, false);
            rd.AddComponent<MeshFilter>().sharedMesh = _roads;
            var rdr = rd.AddComponent<MeshRenderer>(); rdr.sharedMaterial = _roadMat; rdr.shadowCastingMode = ShadowCastingMode.Off;
            var l = new GameObject("Lakes"); l.transform.SetParent(transform, false);
            l.AddComponent<MeshFilter>().sharedMesh = _lakes;
            var lr = l.AddComponent<MeshRenderer>(); lr.sharedMaterial = _lakeMat; lr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Update()
        {
            var w = Host.World;
            if (w == null || w.Drainage == null) return;
            _roadTimer -= Time.unscaledDeltaTime;
            if (_roadTimer <= 0f || _builtWorld != Host.WorldVersion) { _roadTimer = 6f; BuildRoads(w); }
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
                bool first = q == 0 || all[q].Cell != all[q - 1].Cell;   // (a reach's first piece: reaches end at the water, D2-118)
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

        /// <summary>
        /// The ways between villages as their people have worn them (C4.1, D2-097): a ribbon along each worn way's route - a faint track 1 m
        /// wide, a path 1.8 m, a beaten road 3 m near the camera, and the roads kept a pixel or two wide far off so the network reads from
        /// the region view. A way nobody walks is not drawn.
        /// </summary>
        void BuildRoads(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            w.Ways.Collect(w.Now, _ways);
            _v.Clear(); _n.Clear(); _u0.Clear(); _u1.Clear(); _t.Clear();
            for (int i = 0; i < _ways.Count; i++)
            {
                var way = _ways[i];
                if (!w.Settlements.Exists(way.A) || !w.Settlements.Exists(way.B)) continue;
                var r = w.Routes.Get(w, way.A, way.B);
                if (r == null || r.Pts == null || r.Pts.Length < 2) continue;
                byte state = TC2.Core.Agents.Ways.StateOf(way.Use);
                float worn = state >= 3 ? 1f : state == 2 ? 0.55f : 0.15f;
                float half = state >= 3 ? 1.5f : state == 2 ? 0.9f : 0.5f;
                _pts.Clear(); _pts.AddRange(r.Pts);
                for (int it = 0; it < 2; it++)   // (Chaikin twice: a way that bends)
                {
                    _tmp.Clear(); _tmp.Add(_pts[0]);
                    for (int k = 0; k + 1 < _pts.Count; k++) { _tmp.Add((_pts[k] * 0.75 + _pts[k + 1] * 0.25).Normalized); _tmp.Add((_pts[k] * 0.25 + _pts[k + 1] * 0.75).Normalized); }
                    _tmp.Add(_pts[_pts.Count - 1]);
                    _pts.Clear(); _pts.AddRange(_tmp);
                }
                var u1 = new Vector4(half, worn, 0f, 0f);
                Vector3 sidePrev = Vector3.zero;
                for (int k = 0; k + 1 < _pts.Count; k++)
                {
                    if (!P.IsLand(P.Grid.CellOf(_pts[k])) && !P.IsLand(P.Grid.CellOf(_pts[k + 1]))) { sidePrev = Vector3.zero; continue; }   // (no road over the sea)
                    Vector3 a = PlanetGeo.Surface(P, _pts[k], 0.4f), b = PlanetGeo.Surface(P, _pts[k + 1], 0.4f);
                    Vector3 up = (a + b).normalized;
                    Vector3 side = Vector3.Cross(up, b - a).normalized;
                    if (sidePrev == Vector3.zero) sidePrev = side;
                    int j = _v.Count;
                    _v.Add(a); _v.Add(a); _v.Add(b); _v.Add(b);
                    _u0.Add(new Vector4(sidePrev.x, sidePrev.y, sidePrev.z, -1f)); _u0.Add(new Vector4(sidePrev.x, sidePrev.y, sidePrev.z, 1f));
                    _u0.Add(new Vector4(side.x, side.y, side.z, 1f)); _u0.Add(new Vector4(side.x, side.y, side.z, -1f));
                    for (int q = 0; q < 4; q++) { _n.Add(up); _u1.Add(u1); }
                    _t.Add(j); _t.Add(j + 2); _t.Add(j + 1); _t.Add(j); _t.Add(j + 3); _t.Add(j + 2);
                    sidePrev = side;
                }
            }
            _roads.Clear();
            _roads.SetVertices(_v); _roads.SetNormals(_n); _roads.SetUVs(0, _u0); _roads.SetUVs(1, _u1); _roads.SetTriangles(_t, 0);
            var bd = new Bounds(Vector3.zero, Vector3.zero);
            if (_v.Count > 0) { bd = new Bounds(_v[0], Vector3.zero); for (int k = 1; k < _v.Count; k++) bd.Encapsulate(_v[k]); bd.Expand(200f); }
            _roads.bounds = bd;
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
