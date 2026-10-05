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
        const int Pieces = 4;
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
            _riverMat = Materials.Load("Terrain");
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

        void BuildRivers(TC2.Core.Simulation.World w)
        {
            var P = w.Planet; var d = w.Drainage;
            _v.Clear(); _n.Clear(); _c.Clear(); _t.Clear();
            Color water = new Color(0.20f, 0.42f, 0.62f).linear;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || !P.IsStream(c)) continue;
                int r = d.Receiver[c];
                if (r < 0) continue;
                Vec3d a = P.Grid.Center[c];
                Vec3d b = P.IsLand(r) ? P.Grid.Center[r] : (a * 0.35 + P.Grid.Center[r] * 0.65).Normalized; // reach into the sea
                float width = Mathf.Clamp(Mathf.Sqrt(P.RiverFlow[c]) * 0.09f, 6f, 70f);
                // a few pieces laid on the ground, so the ribbon follows the curve and the relief between centres
                Vector3 prev = PlanetGeo.Surface(P, a, 1.2f), sidePrev = Vector3.zero;
                for (int s = 1; s <= Pieces; s++)
                {
                    Vector3 cur = PlanetGeo.Surface(P, Vec3d.Slerp(a, b, s / (double)Pieces), 1.2f);
                    Vector3 up = (prev + cur).normalized;
                    Vector3 side = Vector3.Cross(up, cur - prev).normalized * (width * 0.5f);
                    if (s == 1) sidePrev = side;
                    int i = _v.Count;
                    _v.Add(prev - sidePrev); _v.Add(prev + sidePrev); _v.Add(cur + side); _v.Add(cur - side);
                    for (int k = 0; k < 4; k++) { _n.Add(up); _c.Add(water); }
                    _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); _t.Add(i); _t.Add(i + 3); _t.Add(i + 2);
                    prev = cur; sidePrev = side;
                }
                // a round joint at the cell so bends do not gap
                Vector3 pa = PlanetGeo.Surface(P, a, 1.25f), upA = pa.normalized;
                Vector3 e1 = Vector3.Cross(upA, Vector3.up).sqrMagnitude > 1e-6f ? Vector3.Cross(upA, Vector3.up).normalized : Vector3.right;
                Vector3 e2 = Vector3.Cross(upA, e1);
                int j = _v.Count;
                for (int k = 0; k < 6; k++)
                {
                    float ang = k * Mathf.PI / 3f;
                    _v.Add(pa + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * (width * 0.5f)); _n.Add(upA); _c.Add(water);
                }
                _v.Add(pa); _n.Add(upA); _c.Add(water);
                for (int k = 0; k < 6; k++) { _t.Add(j + 6); _t.Add(j + k); _t.Add(j + (k + 1) % 6); }
            }
            _rivers.Clear();
            _rivers.SetVertices(_v); _rivers.SetNormals(_n); _rivers.SetColors(_c); _rivers.SetTriangles(_t, 0);
            _rivers.RecalculateBounds();
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
                double r = P.Radius + (P.Elev[c] + P.LakeDepth[c]) * PlanetGeo.VScale + 0.4;
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
