using System.Collections.Generic;
using System.Diagnostics;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// The planet's ground as chunked level of detail (13 §2, D2-026): each cube face is a quadtree of patches of 32x32
    /// quads, split where the camera is near (down to ~4 m quads), built from <see cref="Ground"/> within a time budget
    /// per frame (a parent stays drawn until its four children are ready), with skirts hiding the cracks between levels.
    /// Colours come from the cells (biome, vegetation, fields, fire, scars) blended smoothly, plus slope rock, beaches,
    /// snow and the sea floor's sand; they are refreshed in turn as the world changes. Reads the world only.
    /// </summary>
    public sealed class TerrainLod : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        const int N = 32;                 // quads per patch side
        const int MaxLevel = 7;           // 15.7 km / 2^7 = 123 m patches (3.8 m quads)
        const float SplitK = 2.3f;        // split while the camera is nearer than this many patch sizes
        const int CacheMax = 520;
        const double BuildBudgetMs = 3.0;
        const float RecolourSeconds = 2.5f;

        sealed class Patch
        {
            public long Key; public int Face, Level, I, J;
            public Mesh Mesh; public GameObject Go; public MeshRenderer Renderer;
            public bool Built, Shown;
            public int UsedFrame;
            public Vector3[] Dirs; public int[] Cells; public float[] Rel, Slope;
            public Color[] Colours;
            public Vector2[] Plots;   // field plot coordinates in the owning village's frame (m)
            public Vector2[] Farm;    // x: share of the land the village works, y: crop stage
            public int ColourCycle;
        }

        readonly Dictionary<long, Patch> _cache = new Dictionary<long, Patch>();
        readonly List<Patch> _draw = new List<Patch>();
        readonly List<Patch> _wanted = new List<Patch>();
        readonly List<Patch> _shownLast = new List<Patch>();
        Material _mat;
        int _builtWorld = -1, _builtCoast = -1, _builtRelief = -1;
        int _frame, _cycle;
        float _recolourTimer;
        int _recolourIndex;
        readonly Stopwatch _sw = new Stopwatch();
        Color[] _cellColour = new Color[0];
        public int PatchesDrawn, PatchesBuilt, Builds, VillageVerts, NearPatches;

        // scratch for one patch
        const int G = N + 3;              // height grid with a one-cell border for normals
        readonly Vector3[] _gp = new Vector3[G * G];
        readonly Vector3[] _gd = new Vector3[G * G];
        readonly int[] _gc = new int[G * G];
        readonly float[] _gr = new float[G * G];
        static readonly int[] Tris = BuildTris();
        readonly int[] _wc = new int[9];
        readonly float[] _ww = new float[9];

        void Start()
        {
            _mat = Materials.Load("Terrain");
        }

        static long Key(int f, int level, int i, int j) => ((long)f << 56) | ((long)level << 48) | ((long)i << 24) | (long)(uint)j;

        void Update()
        {
            var w = Host.World;
            if (w == null || Rig == null || Rig.Cam == null) return;
            var P = w.Planet;
            _frame++;
            // a new world, a new coast, or relief moved past a hand-over: everything is built again
            bool reliefMoved = P.ReliefVersion - _builtRelief >= Tectonics.ResampleEvery;
            if (_builtWorld != Host.WorldVersion || _builtCoast != P.CoastVersion || reliefMoved)
            {
                foreach (var p in _cache.Values) { p.Built = false; }
                if (_builtWorld != Host.WorldVersion) RefreshCellColours(w);
                _builtWorld = Host.WorldVersion; _builtCoast = P.CoastVersion; _builtRelief = P.ReliefVersion;
                for (int f = 0; f < 6; f++) Build(w, Get(f, 0, 0, 0)); // roots now: never a hole
            }
            Select(w);
            BuildSome(w);
            Show();
            _recolourTimer -= Time.unscaledDeltaTime;
            if (_recolourTimer <= 0) { _recolourTimer = RecolourSeconds; RefreshCellColours(w); _cycle++; }
            RecolourSome(w);
            Evict();
        }

        Patch Get(int f, int level, int i, int j)
        {
            long k = Key(f, level, i, j);
            if (!_cache.TryGetValue(k, out var p))
            {
                p = new Patch { Key = k, Face = f, Level = level, I = i, J = j };
                _cache[k] = p;
            }
            p.UsedFrame = _frame;
            return p;
        }

        // ------------------------------------------------------------------ selection
        void Select(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            _draw.Clear(); _wanted.Clear();
            Vector3 cam = Rig.Cam.transform.position;
            for (int f = 0; f < 6; f++) Visit(P, cam, f, 0, 0, 0);
        }

        void Visit(PlanetState P, Vector3 cam, int f, int level, int i, int j)
        {
            var p = Get(f, level, i, j);
            double size = DMath.HalfPi / (1 << level);
            double a = -DMath.PI / 4 + (i + 0.5) * size, b = -DMath.PI / 4 + (j + 0.5) * size;
            Vec3d d = CubeSphere.FacePoint(f, a, b);
            double R = P.Radius;
            Vector3 centre = new Vector3((float)(d.X * R), (float)(d.Y * R), (float)(d.Z * R));
            double camDist = cam.magnitude;
            // beyond the horizon (with room for mountains and the patch's own size)?
            double cosToCam = Vector3.Dot(centre.normalized, cam / (float)camDist);
            double horizon = System.Math.Acos(System.Math.Min(1.0, R / camDist)) + 0.16 + size * 0.75;
            if (System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, cosToCam))) > horizon && level > 0) return;
            double extent = size * R * 1.15;
            double dist = Vector3.Distance(cam, centre);
            if (level < MaxLevel && dist < SplitK * extent)
            {
                var c0 = Get(f, level + 1, 2 * i, 2 * j); var c1 = Get(f, level + 1, 2 * i + 1, 2 * j);
                var c2 = Get(f, level + 1, 2 * i, 2 * j + 1); var c3 = Get(f, level + 1, 2 * i + 1, 2 * j + 1);
                if (c0.Built && c1.Built && c2.Built && c3.Built)
                {
                    Visit(P, cam, f, level + 1, 2 * i, 2 * j); Visit(P, cam, f, level + 1, 2 * i + 1, 2 * j);
                    Visit(P, cam, f, level + 1, 2 * i, 2 * j + 1); Visit(P, cam, f, level + 1, 2 * i + 1, 2 * j + 1);
                    return;
                }
                if (!c0.Built) _wanted.Add(c0); if (!c1.Built) _wanted.Add(c1);
                if (!c2.Built) _wanted.Add(c2); if (!c3.Built) _wanted.Add(c3);
            }
            if (p.Built) _draw.Add(p); else _wanted.Add(p);
        }

        void BuildSome(TC2.Core.Simulation.World w)
        {
            if (_wanted.Count == 0) return;
            Vector3 cam = Rig.Cam.transform.position;
            var P = w.Planet;
            // nearest first
            _wanted.Sort((x, y) => DistSq(P, x, cam).CompareTo(DistSq(P, y, cam)));
            _sw.Restart();
            for (int k = 0; k < _wanted.Count; k++)
            {
                if (_wanted[k].Built) continue;
                Build(w, _wanted[k]);
                if (_sw.Elapsed.TotalMilliseconds > BuildBudgetMs) break;
            }
        }

        static float DistSq(PlanetState P, Patch p, Vector3 cam)
        {
            double size = DMath.HalfPi / (1 << p.Level);
            Vec3d d = CubeSphere.FacePoint(p.Face, -DMath.PI / 4 + (p.I + 0.5) * size, -DMath.PI / 4 + (p.J + 0.5) * size);
            Vector3 c = new Vector3((float)(d.X * P.Radius), (float)(d.Y * P.Radius), (float)(d.Z * P.Radius));
            return (c - cam).sqrMagnitude;
        }

        void Show()
        {
            foreach (var p in _shownLast) p.Shown = false;
            for (int k = 0; k < _draw.Count; k++) _draw[k].Shown = true;
            foreach (var p in _shownLast) if (!p.Shown && p.Go != null) p.Go.SetActive(false);
            _shownLast.Clear();
            for (int k = 0; k < _draw.Count; k++)
            {
                var p = _draw[k];
                if (!p.Go.activeSelf) p.Go.SetActive(true);
                _shownLast.Add(p);
            }
            PatchesDrawn = _draw.Count;
        }

        void Evict()
        {
            if (_cache.Count <= CacheMax) return;
            var old = new List<Patch>();
            foreach (var p in _cache.Values) if (!p.Shown && p.Level > 0 && _frame - p.UsedFrame > 30) old.Add(p);
            old.Sort((x, y) => x.UsedFrame.CompareTo(y.UsedFrame));
            int remove = _cache.Count - CacheMax;
            for (int k = 0; k < old.Count && k < remove; k++)
            {
                var p = old[k];
                if (p.Go != null) Destroy(p.Go);
                if (p.Mesh != null) Destroy(p.Mesh);
                _cache.Remove(p.Key);
            }
        }

        // ------------------------------------------------------------------ building
        static int[] BuildTris()
        {
            var t = new List<int>();
            int V = N + 1;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a0 = j * V + i, a1 = a0 + 1, b0 = a0 + V, b1 = b0 + 1;
                    t.Add(a0); t.Add(b0); t.Add(a1);
                    t.Add(a1); t.Add(b0); t.Add(b1);
                }
            // skirts: four edges, each a strip of N quads from the edge down to its skirt row
            int baseS = V * V;
            for (int e = 0; e < 4; e++)
                for (int k = 0; k < N; k++)
                {
                    int u0 = EdgeIndex(e, k), u1 = EdgeIndex(e, k + 1);
                    int s0 = baseS + e * V + k, s1 = s0 + 1;
                    // both windings: the skirt hangs below and is seen from either side
                    t.Add(u0); t.Add(s0); t.Add(u1); t.Add(u1); t.Add(s0); t.Add(s1);
                    t.Add(u0); t.Add(u1); t.Add(s0); t.Add(u1); t.Add(s1); t.Add(s0);
                }
            return t.ToArray();
        }

        static int EdgeIndex(int e, int k)
        {
            int V = N + 1;
            switch (e)
            {
                case 0: return k;                       // j = 0
                case 1: return N * V + k;               // j = N
                case 2: return k * V;                   // i = 0
                default: return k * V + N;              // i = N
            }
        }

        void Build(TC2.Core.Simulation.World w, Patch p)
        {
            var P = w.Planet;
            double size = DMath.HalfPi / (1 << p.Level);
            double a0 = -DMath.PI / 4 + p.I * size, b0 = -DMath.PI / 4 + p.J * size;
            double R = P.Radius;
            // heights on the grid with a border
            for (int v = 0; v < G; v++)
                for (int u = 0; u < G; u++)
                {
                    int k = v * G + u;
                    double a = a0 + (u - 1) * size / N, b = b0 + (v - 1) * size / N;
                    Vec3d d = CubeSphere.FacePoint(p.Face, a, b);
                    double h = Ground.Height(P, d, out int cell);
                    double r = R + h;
                    _gp[k] = new Vector3((float)(d.X * r), (float)(d.Y * r), (float)(d.Z * r));
                    _gd[k] = new Vector3((float)d.X, (float)d.Y, (float)d.Z);
                    _gc[k] = cell;
                    _gr[k] = (float)(h - P.SeaLevel);
                }
            int V = N + 1;
            int count = V * V + 4 * V;
            var pos = new Vector3[count];
            var nrm = new Vector3[count];
            if (p.Dirs == null || p.Dirs.Length != count) { p.Dirs = new Vector3[count]; p.Cells = new int[count]; p.Rel = new float[count]; p.Slope = new float[count]; p.Colours = new Color[count]; p.Plots = new Vector2[count]; p.Farm = new Vector2[count]; }
            for (int j = 0; j < V; j++)
                for (int i = 0; i < V; i++)
                {
                    int k = (j + 1) * G + (i + 1);
                    int o = j * V + i;
                    pos[o] = _gp[k];
                    Vector3 du = _gp[k + 1] - _gp[k - 1], dv = _gp[k + G] - _gp[k - G];
                    Vector3 n = Vector3.Cross(dv, du).normalized;
                    if (Vector3.Dot(n, _gd[k]) < 0) n = -n;
                    nrm[o] = n;
                    p.Dirs[o] = _gd[k]; p.Cells[o] = _gc[k]; p.Rel[o] = _gr[k];
                    p.Slope[o] = 1f - Mathf.Clamp01(Vector3.Dot(n, _gd[k]));
                }
            // skirts hang below the edges
            float drop = (float)(size * R * 0.025 + 4.0);
            for (int e = 0; e < 4; e++)
                for (int k = 0; k < V; k++)
                {
                    int src = EdgeIndex(e, k);
                    int o = V * V + e * V + k;
                    pos[o] = pos[src] - p.Dirs[src] * drop;
                    nrm[o] = nrm[src];
                    p.Dirs[o] = p.Dirs[src]; p.Cells[o] = p.Cells[src]; p.Rel[o] = p.Rel[src]; p.Slope[o] = p.Slope[src];
                }
            Colourise(w, p);
            if (p.Mesh == null)
            {
                p.Mesh = new Mesh { name = "tc2-terrain", indexFormat = IndexFormat.UInt16 };
                p.Go = new GameObject("Patch");
                p.Go.transform.SetParent(transform, false);
                p.Go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                p.Renderer = p.Go.AddComponent<MeshRenderer>();
                p.Renderer.sharedMaterial = _mat;
                p.Renderer.shadowCastingMode = ShadowCastingMode.On;
                p.Renderer.receiveShadows = true;
                p.Go.SetActive(false);
            }
            var m = p.Mesh;
            m.Clear();
            m.vertices = pos;
            m.normals = nrm;
            m.colors = p.Colours;
            m.SetUVs(0, p.Plots);
            m.SetUVs(1, p.Farm);
            m.triangles = FixWinding(p.Face, pos, nrm);
            m.RecalculateBounds();
            p.Built = true;
            p.ColourCycle = _cycle;
            PatchesBuilt++;
            Builds++;
        }

        static int[] _trisFlipped;
        static readonly int[] _winding = new int[6]; // per face: 0 unknown, 1 as built, -1 flipped (each face's handedness)

        static int[] FixWinding(int face, Vector3[] pos, Vector3[] nrm)
        {
            if (_winding[face] == 0)
            {
                Vector3 a = pos[Tris[0]], b = pos[Tris[1]], c = pos[Tris[2]];
                _winding[face] = Vector3.Dot(Vector3.Cross(b - a, c - a), nrm[Tris[0]]) > 0 ? 1 : -1; // front faces: the cross product points out
            }
            if (_winding[face] == -1 && _trisFlipped == null)
            {
                _trisFlipped = (int[])Tris.Clone();
                for (int q = 0; q < _trisFlipped.Length; q += 3) { int t = _trisFlipped[q + 1]; _trisFlipped[q + 1] = _trisFlipped[q + 2]; _trisFlipped[q + 2] = t; }
            }
            return _winding[face] == 1 ? Tris : _trisFlipped;
        }

        // ------------------------------------------------------------------ colour
        void RefreshCellColours(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            if (_cellColour.Length != P.Cells) _cellColour = new Color[P.Cells];
            for (int c = 0; c < P.Cells; c++) _cellColour[c] = CellColour(w, c);
        }

        void RecolourSome(TC2.Core.Simulation.World w)
        {
            if (_shownLast.Count == 0) return;
            int n = Mathf.Max(1, Mathf.CeilToInt(_shownLast.Count / (RecolourSeconds * 60f)));
            for (int k = 0; k < n; k++)
            {
                _recolourIndex = (_recolourIndex + 1) % _shownLast.Count;
                var p = _shownLast[_recolourIndex];
                if (p.ColourCycle == _cycle || !p.Built) continue;
                Colourise(w, p);
                p.Mesh.colors = p.Colours;
                p.Mesh.SetUVs(0, p.Plots);
                p.Mesh.SetUVs(1, p.Farm);
                p.ColourCycle = _cycle;
            }
        }

        static readonly Color Earth = new Color(0.56f, 0.47f, 0.35f), Trodden = new Color(0.52f, 0.54f, 0.34f);
        readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();

        /// <summary>Packed earth on the square, yards round the houses, footpaths from each door to the square.</summary>
        static Color VillageGround(TC2.Core.Simulation.World w, in TC2.Core.Places.Settlement s, int sid, double x, double y, Color col)
        {
            double dsq = System.Math.Sqrt((x - s.SquareX) * (x - s.SquareX) + (y - s.SquareY) * (y - s.SquareY));
            float green = Mathf.Clamp01(1f - (float)(dsq - 40.0) / 45f);
            col = Color.Lerp(col, Trodden, green * 0.35f);                                    // the village green, walked on
            float earth = 1f - Edge(9f, 15f, (float)dsq);                         // the square
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var h = ref w.Households.Get(hhs[i]);
                double hx = h.HomeX - x, hy = h.HomeY - y;
                double dh = System.Math.Sqrt(hx * hx + hy * hy);
                earth = Mathf.Max(earth, (1f - Edge(4.5f, 8.5f, (float)dh)) * 0.75f);   // the yard
                // the path from the door to the square
                double ax = s.SquareX - h.HomeX, ay = s.SquareY - h.HomeY;
                double len2 = ax * ax + ay * ay;
                double t = len2 > 0 ? ((x - h.HomeX) * ax + (y - h.HomeY) * ay) / len2 : 0;
                if (t < 0) t = 0; else if (t > 1) t = 1;
                double px = h.HomeX + ax * t - x, py = h.HomeY + ay * t - y;
                double dp = System.Math.Sqrt(px * px + py * py);
                earth = Mathf.Max(earth, (1f - Edge(1.0f, 2.6f, (float)dp)) * 0.85f);
            }
            return Color.Lerp(col, Earth, earth);
        }

        static readonly Color Rock = new Color(0.47f, 0.44f, 0.40f), RockDark = new Color(0.33f, 0.31f, 0.30f);
        static readonly Color Sand = new Color(0.87f, 0.80f, 0.60f), WetSand = new Color(0.66f, 0.60f, 0.46f);
        static readonly Color Snow = new Color(0.95f, 0.96f, 0.98f), Glacier = new Color(0.86f, 0.92f, 0.97f);
        static readonly Color Shelf = new Color(0.82f, 0.78f, 0.60f), Abyss = new Color(0.05f, 0.11f, 0.17f);

        void Colourise(TC2.Core.Simulation.World w, Patch p)
        {
            var P = w.Planet;
            if (_cellColour.Length != P.Cells) RefreshCellColours(w);
            double sea = P.SeaLevel;
            double season = DMath.Sin(DMath.TwoPi * (SimTime.YearFraction(w.Now) - 0.25));
            var cols = p.Colours;
            // villages near this patch
            _near.Clear();
            {
                Vector3 pc = p.Dirs[cols.Length / 2];
                double reach = DMath.HalfPi / (1 << p.Level) * 0.9 + 220.0 / P.Radius;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var st = ref w.Settlements.Get(sid);
                    if (!st.Inhabited) continue;
                    double dd = System.Math.Acos(System.Math.Min(1.0, st.Anchor.X * pc.x + st.Anchor.Y * pc.y + st.Anchor.Z * pc.z));
                    if (dd < reach) { _near.Add(sid); NearPatches++; }
                }
            }
            for (int o = 0; o < cols.Length; o++)
            {
                Vector3 dv = p.Dirs[o];
                var d = new Vec3d(dv.x, dv.y, dv.z);
                int c = p.Cells[o];
                float rel = p.Rel[o], slope = p.Slope[o];
                Color col;
                if (rel < 0)
                {
                    // the sea floor as seen through the water: sand in the shallows, dark below
                    float t = Mathf.Clamp01(-rel / 60f);
                    col = Color.Lerp(Shelf, Abyss, Mathf.Sqrt(t));
                }
                else
                {
                    int n = Ground.ColourWeights(P, d, c, _wc, _ww);
                    col = new Color(0, 0, 0, 1);
                    float farm = 0; int owner = 0; float ow = 0;
                    for (int q = 0; q < n; q++)
                    {
                        int cq = _wc[q]; var cc = _cellColour[cq]; float ww = _ww[q];
                        col.r += cc.r * ww; col.g += cc.g * ww; col.b += cc.b * ww;
                        int fo = P.FieldOwner[cq];
                        if (fo != 0) { farm += ww; if (ww > ow) { ow = ww; owner = fo; } }
                    }
                    // farmland: the shader draws plots, furrows and hedges in the village's own grid
                    p.Plots[o] = Vector2.zero; p.Farm[o] = Vector2.zero;
                    if (owner != 0 && w.Settlements.Exists(owner))
                    {
                        ref var st = ref w.Settlements.Get(owner);
                        st.SphereToLocal(d, P.Radius, out double fx, out double fy);
                        p.Plots[o] = new Vector2((float)fx, (float)fy);
                        farm *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 135f, Mathf.Sqrt((float)(fx * fx + fy * fy)))); // houses and yards
                        float used = st.ArableHa > 0 ? (float)System.Math.Min(1.0, st.FieldHa / st.ArableHa) : 1f;
                        p.Farm[o] = new Vector2(used, (float)st.Crop + (float)System.Math.Min(0.99, st.CropGrowth));
                        Color crop = CropColour(st);
                        col = Color.Lerp(col, crop, Mathf.SmoothStep(0f, 1f, farm));
                    }
                    for (int q = 0; q < _near.Count; q++)
                    {
                        ref var st = ref w.Settlements.Get(_near[q]);
                        double dot = st.Anchor.X * d.X + st.Anchor.Y * d.Y + st.Anchor.Z * d.Z;
                        if (dot < System.Math.Cos(130.0 / P.Radius)) continue;
                        st.SphereToLocal(d, P.Radius, out double vx, out double vy);
                        col = VillageGround(w, st, _near[q], vx, vy, col);
                        VillageVerts++;
                    }
                    // natural variation: meadow patches warmer or cooler, small brightness noise
                    float hue = (float)Noise.Fbm(d.X * 9.0, d.Y * 9.0, d.Z * 9.0, 2);
                    float fine = (float)Noise.Perlin(d.X * 140.0, d.Y * 140.0, d.Z * 140.0);
                    col = new Color(col.r * (1f + 0.10f * hue) + 0.015f * fine, col.g * (1f + 0.04f * hue) + 0.02f * fine, col.b * (1f - 0.08f * hue) + 0.01f * fine);
                    // beaches where the land meets the sea
                    float beach = 1f - Edge(1.0f, 6.5f, rel);
                    if (beach > 0 && P.Ice[c] < 1f) col = Color.Lerp(col, rel < 1.2f ? WetSand : Sand, beach * 0.92f);
                    // rock on steep ground
                    float rock = Edge(0.10f, 0.30f, slope);
                    if (rock > 0) col = Color.Lerp(col, Color.Lerp(Rock, RockDark, Mathf.Clamp01(slope * 1.6f - 0.3f)), rock);
                    // snow: the cell's climate at this height (lapse), the season, and ice sheets
                    double t = P.TMean[c] - 0.0065 * (rel - (P.Elev[c] - sea) * Ground.VScale) / Ground.VScale + P.TAmp[c] * season * (P.SinLat[c] >= 0 ? 1 : -1);
                    float snow = Mathf.Clamp01((float)(-t / 4.0 + 0.2)) * (1f - rock * 0.6f);
                    if (P.Ice[c] > 2f) snow = 1f;
                    if (snow > 0) col = Color.Lerp(col, P.Ice[c] > 2f ? Glacier : Snow, snow);
                }
                var lin = col.linear;
                lin.a = rel < 0 || p.Farm[o].x <= 0 ? 0f : Mathf.Clamp01(FarmWeight(P, d, c) * CoreMask(p.Plots[o]));
                cols[o] = lin;
            }
        }

        /// <summary>GLSL smoothstep(edge0, edge1, x) - not Unity's Mathf.SmoothStep, which interpolates between its first two arguments.</summary>
        static float Edge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        static float CoreMask(Vector2 local) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 135f, local.magnitude));

        /// <summary>How much of a point is the village's farmland (soft at the edge of the claimed cells).</summary>
        float FarmWeight(PlanetState P, Vec3d d, int c)
        {
            int n = Ground.MaskWeights(P, d, c, _wc, _ww);
            float f = 0;
            for (int q = 0; q < n; q++) if (P.FieldOwner[_wc[q]] != 0) f += _ww[q];
            return Edge(0.25f, 0.75f, f);
        }

        /// <summary>The colour of a village's crop by its stage (sRGB): earth, young green, gold, stubble.</summary>
        public static Color CropColour(in TC2.Core.Places.Settlement s)
        {
            Color earth = new Color(0.47f, 0.36f, 0.25f), young = new Color(0.52f, 0.60f, 0.27f), lush = new Color(0.42f, 0.58f, 0.24f);
            Color gold = new Color(0.84f, 0.70f, 0.33f), stubble = new Color(0.70f, 0.62f, 0.42f);
            Color c;
            switch (s.Crop)
            {
                case TC2.Core.Places.CropStage.Sown: c = earth; break;
                case TC2.Core.Places.CropStage.Growing: c = Color.Lerp(Color.Lerp(earth, young, 0.6f), lush, (float)s.CropGrowth); break;
                case TC2.Core.Places.CropStage.Ripe: c = gold; break;
                case TC2.Core.Places.CropStage.Harvested: c = stubble; break;
                default: c = new Color(0.55f, 0.58f, 0.36f); break;
            }
            if (s.CropBurnt > 0.2) c = Color.Lerp(c, new Color(0.14f, 0.12f, 0.10f), (float)s.CropBurnt);
            return c;
        }

        /// <summary>A cell's colour: biome, vegetation density, fields, fire and scars (sRGB).</summary>
        public static Color CellColour(TC2.Core.Simulation.World w, int c)
        {
            var P = w.Planet;
            var fire = w.Fire;
            Color col = PlanetGeo.BiomeColor((Biome)P.Biome[c]);
            if (!P.IsLand(c)) return col;
            float forest = P.Forest[c], grass = P.Grass[c];
            col = Color.Lerp(col, new Color(0.22f, 0.33f, 0.16f), Mathf.Clamp01(forest - 0.2f) * 0.6f);   // the forest floor
            col = Color.Lerp(col, new Color(0.70f, 0.62f, 0.44f), Mathf.Clamp01(0.25f - grass - forest) * 2f); // bare ground
            col = Color.Lerp(col, new Color(0.62f, 0.62f, 0.40f), Mathf.Clamp01(0.5f - P.SoilWater[c]) * 0.35f); // dry grass yellows
            if (fire.Intensity[c] > 0) col = Color.Lerp(col, new Color(1f, 0.35f, 0.05f), 0.4f + 0.6f * fire.Intensity[c]);
            else if (fire.BurnedAt[c] != 0 && w.Now - fire.BurnedAt[c] < 2 * SimTime.Year)
            {
                float age = (float)((w.Now - fire.BurnedAt[c]) / (double)(2 * SimTime.Year));
                col = Color.Lerp(new Color(0.16f, 0.13f, 0.11f), col, age);
            }
            return col;
        }
    }
}
