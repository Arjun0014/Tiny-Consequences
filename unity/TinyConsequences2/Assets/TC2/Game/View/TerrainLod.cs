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
        const int MaxLevel10 = 7;         // 15.7 km / 2^7 = 123 m patches (3.8 m quads) on the 10 km world
        int MaxLevel = MaxLevel10;        // (the same finest quads on a smaller planet: one level less at 5 km, D2-085)
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
            public Vector4[] Wood;    // the woods here (D2-079): x forest share, y conifers' share, z the leaves' season, w snow
            public Vector4[] Land;    // the land's own (D2-081): x dryness of the climate, y meadow (grass where no wood), z bare earth or rock, w beach
            public Vector3[] Pos, Nrm; // the vertices as drawn and their normals (where the woods' trees and the ground cover stand)
            public Matrix4x4[] Trees; public byte[] TreeKind; public ushort[] TreeGroup; public int TreeCount;   // the woods' trees (finest level)
            public Matrix4x4[] Tufts; public byte[] TuftKind, TuftGroup; public int TuftCount; // ground cover (finest level)
            public int ColourCycle;
            public long PlacedDay = long.MinValue;   // the world day its trees and cover were last placed (D2-081: once a day at most)
        }

        readonly Dictionary<long, Patch> _cache = new Dictionary<long, Patch>();
        readonly List<Patch> _draw = new List<Patch>();
        readonly List<Patch> _wanted = new List<Patch>();
        readonly List<Patch> _shownLast = new List<Patch>();
        Material _mat;
        Instancer[] _cover;           // grass, flowers, reeds, boulders on the finest patches near the camera
        Instancer[] _forest;          // the woods' trees near the camera, where the canopy shows their crowns (D2-079)
        // near: one instancer per kind and variant (ProcMeshes.ForestTree, D2-084: index kind x variants + variant); then the far forms
        const int NearForms = ProcMeshes.TreeKinds * ProcMeshes.TreeVariants;
        const int BroadFar = NearForms, ConFar = NearForms + 1, BareBroad = NearForms + 2;
        const int MaxTrees = 640;
        /// <summary>The trees stand within this distance of the camera; the canopy hands over to them (the shader fades its crowns
        /// from 400 m to 300 m); the finest patches always reach past it.</summary>
        const float ForestReach = 440f, ForestNear = 190f;
        public static int ForestDrawn;
        /// <summary>Tools (webperf): the ground cover not drawn.</summary>
        public static bool CoverOff;
        /// <summary>Tools (webperf): whether shrubs and boulders cast shadows.</summary>
        public static bool CoverShadows = true;
        readonly Plane[] _planes = new Plane[6];
        static readonly Color[] LeafTints = new Color[512], ConiferTints = new Color[512], BareTints = new Color[512];
        const int Grass = 0, Flowers = 1, Reeds = 2, Boulder = 3, Shrub = 4, Fern = 5, Cushion = 6, Stone = 7;
        // ground cover colours by kind and group (sRGB): grass from straw to lush (5 dry, 6 dune grass), petals, reeds, boulders,
        // shrubs (summer dark and light, olive, autumn russet and gold, winter twigs, evergreen), ferns (green, light, autumn bracken),
        // cushions (heather green-brown, heather in bloom, tundra cushion, lichen), stones (grey, dark, warm)
        static readonly Color[][] CoverTints =
        {
            new[] { new Color(0.80f, 0.80f, 0.50f), new Color(0.70f, 0.80f, 0.44f), new Color(0.60f, 0.78f, 0.40f), new Color(0.52f, 0.72f, 0.36f),
                    new Color(0.46f, 0.64f, 0.32f), new Color(0.86f, 0.80f, 0.56f), new Color(0.84f, 0.82f, 0.58f) },
            new[] { new Color(0.97f, 0.96f, 0.90f), new Color(0.98f, 0.84f, 0.26f), new Color(0.66f, 0.46f, 0.84f), new Color(0.90f, 0.32f, 0.30f),
                    new Color(0.52f, 0.62f, 0.95f), new Color(0.98f, 0.62f, 0.80f) },
            new[] { Color.white },
            new[] { Color.white },
            new[] { new Color(0.22f, 0.35f, 0.16f), new Color(0.31f, 0.44f, 0.19f), new Color(0.46f, 0.48f, 0.25f), new Color(0.60f, 0.33f, 0.16f),
                    new Color(0.74f, 0.56f, 0.22f), new Color(0.44f, 0.36f, 0.29f), new Color(0.22f, 0.37f, 0.24f) },
            new[] { new Color(0.36f, 0.56f, 0.22f), new Color(0.48f, 0.64f, 0.27f), new Color(0.66f, 0.42f, 0.20f) },
            new[] { new Color(0.40f, 0.37f, 0.25f), new Color(0.62f, 0.38f, 0.60f), new Color(0.52f, 0.56f, 0.40f), new Color(0.74f, 0.76f, 0.60f) },
            new[] { new Color(0.66f, 0.64f, 0.60f), new Color(0.48f, 0.46f, 0.44f), new Color(0.70f, 0.61f, 0.50f) },
        };
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
            var inst = Materials.Load("Instanced");
            inst.enableInstancing = true;
            _cover = new Instancer[8];
            _cover[Grass] = new Instancer(Models.Get("grass_tuft", ProcMeshes.Person), inst, false);
            _cover[Flowers] = new Instancer(Models.Get("flowers", ProcMeshes.Person), inst, false);
            _cover[Reeds] = new Instancer(Models.Get("reeds", ProcMeshes.Person), inst, false);
            _cover[Boulder] = new Instancer(Models.Get("boulder", ProcMeshes.Person), inst, true);
            _cover[Shrub] = new Instancer(ProcMeshes.Shrub(), inst, true);
            _cover[Fern] = new Instancer(ProcMeshes.Fern(), inst, false);
            _cover[Cushion] = new Instancer(ProcMeshes.Cushion(), inst, false);
            _cover[Stone] = new Instancer(ProcMeshes.Stone(), inst, false);
            _forest = new Instancer[NearForms + 3];
            for (int k = 0; k < ProcMeshes.TreeKinds; k++)
                for (int v = 0; v < ProcMeshes.TreeVariants; v++)
                    _forest[k * ProcMeshes.TreeVariants + v] = new Instancer(ProcMeshes.ForestTree(k, v), inst, true);
            _forest[BroadFar] = new Instancer(ProcMeshes.ForestBroad(1), inst, true);
            _forest[ConFar] = new Instancer(ProcMeshes.ForestConifer(1), inst, true);
            _forest[BareBroad] = new Instancer(ProcMeshes.ForestBare(), inst, true);
            Tints();
        }

        /// <summary>
        /// The trees' colours by group (shade 0-3, the leaves' season 0-7, snow 0-3, the climate's dryness 0-3): summer greens - olive
        /// where the land is dry -, autumn's gold and rust, the bare boughs of winter, conifers dark and snow-dusted - the canopy
        /// shader's own colours, so the woods hand over unseen.
        /// </summary>
        static void Tints()
        {
            var summer = new[] { new Color(0.25f, 0.40f, 0.21f), new Color(0.31f, 0.47f, 0.21f), new Color(0.38f, 0.51f, 0.22f), new Color(0.45f, 0.56f, 0.23f) };
            var autumn = new[] { new Color(0.78f, 0.60f, 0.21f), new Color(0.69f, 0.37f, 0.17f), new Color(0.80f, 0.50f, 0.16f), new Color(0.66f, 0.30f, 0.15f) };
            var needles = new[] { new Color(0.16f, 0.29f, 0.19f), new Color(0.19f, 0.33f, 0.21f), new Color(0.14f, 0.26f, 0.17f), new Color(0.21f, 0.35f, 0.22f) };
            var snow = new Color(0.88f, 0.90f, 0.94f);
            for (int g = 0; g < 512; g++)
            {
                int shade = g & 3, leaf = (g >> 2) & 7, sn = (g >> 5) & 3;
                float dry = ((g >> 7) & 3) / 3f;
                float leaves = (leaf + 0.5f) / 8f;
                float turn = Mathf.Clamp01((leaves - 0.1f) / 0.55f);
                LeafTints[g] = Color.Lerp(Dry(summer[shade], dry * 0.8f, 1.6f, 0.015f, 0.90f, 0.60f), autumn[shade], turn);
                ConiferTints[g] = Color.Lerp(Dry(needles[shade], dry * 0.5f, 1.35f, 0f, 0.95f, 0.75f), snow, sn / 3f * 0.55f);
                BareTints[g] = Color.Lerp(new Color(0.42f + 0.03f * shade, 0.35f + 0.02f * shade, 0.29f), snow, sn / 3f * 0.35f);
            }
        }

        /// <summary>The shader's dry-land shift of a leaf colour (sRGB in and out, the shift and the blend made on linear values).</summary>
        static Color Dry(Color c, float k, float r, float add, float g, float b)
        {
            Color lin = c.linear;
            return Color.Lerp(lin, new Color(lin.r * r + add, lin.g * g, lin.b * b), k).gamma;
        }

        static long Key(int f, int level, int i, int j) => ((long)f << 56) | ((long)level << 48) | ((long)i << 24) | (long)(uint)j;

        void Update()
        {
            var w = Host.World;
            if (w == null || Rig == null || Rig.Cam == null) return;
            var P = w.Planet;
            _frame++;
            Rivers.Refresh(w, Host.WorldVersion);   // (the rivers' shape: the ground draws their corridors, D2-084)
            // a new world, a new coast, or relief moved past a hand-over: everything is built again
            bool reliefMoved = P.ReliefVersion - _builtRelief >= Tectonics.ResampleEvery;
            if (_builtWorld != Host.WorldVersion || _builtCoast != P.CoastVersion || reliefMoved)
            {
                foreach (var p in _cache.Values) { p.Built = false; }
                if (_builtWorld != Host.WorldVersion)
                {
                    RefreshCellColours(w);
                    MaxLevel = System.Math.Max(4, MaxLevel10 + (int)System.Math.Round(System.Math.Log(P.GeoScale, 2.0)));
                }
                _builtWorld = Host.WorldVersion; _builtCoast = P.CoastVersion; _builtRelief = P.ReliefVersion;
                for (int f = 0; f < 6; f++) Build(w, Get(f, 0, 0, 0)); // roots now: never a hole
            }
            _roadTimer -= Time.unscaledDeltaTime;
            if (_roadTimer <= 0 || _roadWorld != Host.WorldVersion) { _roadTimer = 10f; _roadWorld = Host.WorldVersion; BuildRoads(w); }
            VolcanoShape.SetTime(w.Now); // cone ages (the cones change only with the relief)
            Select(w);
            BuildSome(w);
            Show();
            if (NeedGather())
            {
                GeometryUtility.CalculateFrustumPlanes(Rig.Cam, _planes);
                GatherCover();
                GatherForest();
                Gathered();
            }
            DrawCover();
            DrawForest();
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
            foreach (var p in _shownLast) if (!p.Shown && p.Go != null) { p.Go.SetActive(false); _shownVersion++; }
            _shownLast.Clear();
            for (int k = 0; k < _draw.Count; k++)
            {
                var p = _draw[k];
                if (!p.Go.activeSelf) { p.Go.SetActive(true); _shownVersion++; }
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
            if (p.Dirs == null || p.Dirs.Length != count) { p.Dirs = new Vector3[count]; p.Cells = new int[count]; p.Rel = new float[count]; p.Slope = new float[count]; p.Colours = new Color[count]; p.Plots = new Vector2[count]; p.Farm = new Vector2[count]; p.Wood = new Vector4[count]; p.Land = new Vector4[count]; }
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
            p.Pos = pos; p.Nrm = nrm;
            if (p.Level >= MaxLevel) { PlaceCover(w, p, pos, nrm); SortCover(p); PlaceForest(w, p); _contentVersion++; p.PlacedDay = SimTime.FloorDiv(w.Now, SimTime.Day); } else { p.TuftCount = 0; p.TreeCount = 0; }
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
            m.SetUVs(2, p.Wood);
            m.SetUVs(3, p.Land);
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

        // ------------------------------------------------------------------ ground cover
        /// <summary>
        /// The ground's cover on a finest patch (D2-081), chosen by what the land is (on the patch's own surface, interpolated
        /// between its vertices: no extra height lookups; none on fields, paths, snow or water): meadows of grass, patchy - thick in
        /// hollows, thin on the rises - with wildflowers in their season and a few shrubs; the woods' floor with ferns and
        /// undergrowth in the gaps and shrubs thick at their edges; heather where the climate is cool and wet and the land open;
        /// cushions and stones on the tundra; straw-dry tufts and stones where the climate is dry; dune grass on the sand above the
        /// tide; reeds by fresh water; boulders on rocky slopes.
        /// </summary>
        void PlaceCover(TC2.Core.Simulation.World w, Patch p, Vector3[] pos, Vector3[] nrm)
        {
            var P = w.Planet;
            int V = N + 1;
            int max = N * N * 3;
            if (p.Tufts == null || p.Tufts.Length < max) { p.Tufts = new Matrix4x4[max]; p.TuftKind = new byte[max]; p.TuftGroup = new byte[max]; }
            int count = 0;
            float yearNorth = (float)SimTime.YearFraction(w.Now);
            ulong key = (ulong)p.Key * 0x9E3779B97F4A7C15UL;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * V + i, b = a + 1, c = a + V, d = c + 1;
                    float rel = Mathf.Min(Mathf.Min(p.Rel[a], p.Rel[b]), Mathf.Min(p.Rel[c], p.Rel[d]));
                    if (rel < 0.9f) continue;
                    Color col = p.Colours[a];
                    if (col.a > 0.15f) continue;                                  // farmland: the crop is the shader's
                    float slope = p.Slope[a];
                    int cell = p.Cells[a];
                    float wood = 0.25f * (p.Wood[a].x + p.Wood[b].x + p.Wood[c].x + p.Wood[d].x);
                    float riverNear = 0.25f * (p.Land[a].y + p.Land[b].y + p.Land[c].y + p.Land[d].y);
                    wood = Canopy.Stands(wood, 0.25f * (pos[a] + pos[b] + pos[c] + pos[d]), riverNear);   // (a stand's floor, or a glade's meadow: D2-084)
                    float dry = p.Land[a].x;
                    float tm = P.TMean[cell];
                    // the season where this is (the leaves' year: 0 spring ... 0.5 late summer ... 0.875 winter)
                    float f = P.SinLat[cell] < 0 ? (yearNorth + 0.5f) % 1f : yearNorth;
                    bool bloom = f > 0.04f && f < 0.48f && tm > -2f, heatherBloom = f > 0.38f && f < 0.62f;
                    int leafGroup = f < 0.5f || tm > 20f ? -1 : (f < 0.875f ? 3 : 5);   // (shrubs: summer, autumn, winter twigs)
                    // what the ground is: snow (its colour), packed earth and rock, beach sand (the land's own, from Colourise)
                    float g = col.g, r = col.r, bl = col.b;
                    bool snowy = bl > 0.55f && g > 0.55f;
                    float bare = p.Land[a].z;
                    bool sandy = BeachAt(rel, p.Land[a].w) > 0.2f || P.Biome[cell] == (byte)Biome.Desert;
                    if (snowy || (slope <= 0.17f && bare > 0.35f)) continue;
                    // by fresh water: a lake's shore, or a river's banks (D2-084: its edge, not the whole cell); nothing in its channel
                    float rbank = float.MaxValue;
                    if (riverNear > 0.05f) { Vector3 da = p.Dirs[a]; float rd = Rivers.Nearest(P, new Vec3d(da.x, da.y, da.z), cell, out float rh); rbank = rd - rh; }
                    if (rbank < -1.5f) continue;
                    bool water = P.LakeDepth[cell] > 0.3f || rbank < 7f;
                    bool tundra = tm < 0f || P.Biome[cell] == (byte)Biome.Tundra;
                    bool heath = !tundra && tm < 9f && P.Moist[cell] > 0.45f && wood < 0.2f;
                    ulong h = KeyedRng.Mix(key ^ ((ulong)(i * 73856093) ^ (ulong)(j * 19349663)));
                    // meadows are patchy: thick in hollows, thin on the rises (a slow noise of where they are)
                    Vector3 dq = p.Dirs[a];
                    float lushness = 0.40f + 0.9f * (0.5f + (float)Noise.Perlin(dq.x * 420.0, dq.y * 420.0, dq.z * 420.0));
                    if (dry > 0.5f) lushness *= 1.4f - dry;
                    for (int t = 0; t < 3; t++)
                    {
                        h = KeyedRng.Mix(h + (ulong)t);
                        double pick = KeyedRng.Unit(KeyedRng.Mix(h + 99));
                        float grow = (float)KeyedRng.Unit(KeyedRng.Mix(h ^ 0x77UL));
                        int kind = Grass, group = 0;
                        float scale = 1f;
                        if (slope > 0.17f) { if (pick > 0.30) continue; kind = Boulder; }
                        else if (sandy)
                        {
                            // the sand above the tide: tufts of dune grass, more of them further up the beach
                            if (rel < 1.5f || grow > 0.12f + 0.22f * Mathf.Clamp01((rel - 1.5f) / 3f)) continue;
                            group = 6; scale = 1.1f;
                        }
                        else if (water) { if (grow > lushness) continue; if (pick < 0.45) kind = Reeds; }   // by fresh water
                        else if (wood > 0.55f)
                        {
                            // under the woods: ferns and undergrowth where the light comes through, little grass
                            if (pick < 0.30) { kind = Fern; group = leafGroup >= 0 ? 2 : (pick < 0.15 ? 0 : 1); scale = 0.50f + 0.45f * grow; }
                            else if (pick < 0.40) { kind = Shrub; group = leafGroup >= 0 ? leafGroup + (pick < 0.35 ? 0 : 1) : (pick < 0.35 ? 0 : 6); scale = 0.8f + 0.7f * grow; }
                            else continue;
                        }
                        else if (wood > 0.10f)
                        {
                            // the woods' edges and open woodland: shrubs and ferns among the grass
                            if (pick < 0.28) { kind = Shrub; group = leafGroup >= 0 ? leafGroup + (pick < 0.14 ? 0 : 1) : (dry > 0.5f ? 2 : (pick < 0.14 ? 0 : 1)); scale = 0.9f + 1.1f * grow; }
                            else if (pick < 0.40 && dry < 0.5f) { kind = Fern; group = leafGroup >= 0 ? 2 : 1; scale = 0.45f + 0.40f * grow; }
                            else if (grow > lushness) continue;
                        }
                        else if (tundra)
                        {
                            // the tundra: low cushions, lichen and stones; a little grass
                            if (pick < 0.35) { kind = Cushion; group = pick < 0.22 ? 2 : 3; scale = 0.45f + 0.5f * grow; }
                            else if (pick < 0.50) { kind = Stone; group = pick < 0.43 ? 0 : 1; scale = 0.5f + 1.3f * grow; }
                            else if (grow > lushness * 0.6f) continue;
                        }
                        else if (heath)
                        {
                            // heath: heather, purple in late summer, with grass between
                            if (pick < 0.45) { kind = Cushion; group = heatherBloom ? 1 : 0; scale = 0.5f + 0.55f * grow; }
                            else if (grow > lushness) continue;
                        }
                        else
                        {
                            // meadow: grass, flowers in their season, a shrub here and there; in dry lands straw tufts and stones
                            if (grow > lushness) continue;
                            if (dry > 0.55f && pick < 0.10) { kind = Stone; group = 2; scale = 0.4f + 1.0f * grow; }
                            else if (bloom && pick < 0.16 * (1f - dry)) { kind = Flowers; group = (int)((h >> 20) % 6UL); }
                            else if (pick > 0.975) { kind = Shrub; group = leafGroup >= 0 ? leafGroup : (dry > 0.5f ? 2 : 1); scale = 0.8f + 0.8f * grow; }
                        }
                        float u = (float)KeyedRng.Unit(h), v = (float)KeyedRng.Unit(KeyedRng.Mix(h ^ 0x51UL));
                        Vector3 at = Vector3.Lerp(Vector3.Lerp(pos[a], pos[b], u), Vector3.Lerp(pos[c], pos[d], u), v);
                        Vector3 up = Vector3.Lerp(Vector3.Lerp(nrm[a], nrm[b], u), Vector3.Lerp(nrm[c], nrm[d], u), v).normalized;
                        float s = kind == Boulder ? 0.6f + 1.2f * (float)KeyedRng.Unit(KeyedRng.Mix(h + 7))
                                : (kind == Grass ? 2.5f : kind == Flowers ? 1.8f : 1.0f) * scale * (0.75f + 0.5f * (float)KeyedRng.Unit(KeyedRng.Mix(h + 7)));
                        var rot = Quaternion.AngleAxis((float)(h % 360), up) * Quaternion.FromToRotation(Vector3.up, up);
                        p.Tufts[count] = Matrix4x4.TRS(at, rot, Vector3.one * s);
                        p.TuftKind[count] = (byte)kind;
                        if (kind == Grass && group == 0)
                        {
                            // grass from dry and yellow to lush, by how green the ground is and how dry the climate
                            float lush = Mathf.Clamp01((g - r) * 6f + 0.5f - dry * 0.6f + 0.35f * (float)Noise.Perlin(dq.x * 1900.0, dq.y * 1900.0, dq.z * 1900.0));
                            group = r > g ? 5 : Mathf.Clamp((int)(lush * 4.99f), 0, 4);
                        }
                        p.TuftGroup[count] = (byte)group;
                        count++;
                    }
                }
            p.TuftCount = count;
        }

        // the cover's and the trees' instance lists are gathered again only when the patches shown or what stands on them change, the
        // camera moves or turns by much, or a few times a second; between, the lists already gathered are drawn (D2-081: gathering
        // them every frame was the near views' largest cost in the browser)
        int _shownVersion, _contentVersion, _gatheredShown = -1, _gatheredContent = -1, _gatheredFrame = -999;
        Vector3 _gatheredPos, _gatheredFwd;

        bool NeedGather()
        {
            var t = Rig.Cam.transform;
            if (_gatheredShown != _shownVersion || _gatheredContent != _contentVersion || _frame - _gatheredFrame >= 15) return true;
            if ((t.position - _gatheredPos).sqrMagnitude > 12f * 12f) return true;
            return Vector3.Dot(t.forward, _gatheredFwd) < 0.9986f;   // (about three degrees)
        }

        void Gathered()
        {
            var t = Rig.Cam.transform;
            _gatheredShown = _shownVersion; _gatheredContent = _contentVersion; _gatheredFrame = _frame;
            _gatheredPos = t.position; _gatheredFwd = t.forward;
        }

        void GatherCover()
        {
            for (int k = 0; k < _cover.Length; k++) _cover[k].Clear();
            Vector3 cam = Rig.Cam.transform.position;
            float alt = cam.magnitude - (float)Host.World.Planet.Radius;
            if (alt > 600f || CoverOff) return;
            for (int q = 0; q < _shownLast.Count; q++)
            {
                var p = _shownLast[q];
                if (p.TuftCount == 0 || p.Renderer == null) continue;
                float d2 = (p.Tufts[0].GetColumn(3) - (Vector4)cam).sqrMagnitude;
                if (d2 > 480f * 480f) continue;
                var bounds = p.Renderer.bounds;
                bounds.Expand(30f);
                if (!GeometryUtility.TestPlanesAABB(_planes, bounds)) continue;
                bool onlyBig = d2 > 260f * 260f;   // (further off, only what stands out: shrubs and boulders - the woods' edges)
                for (int t = 0; t < p.TuftCount; t++)
                {
                    int kind = p.TuftKind[t], gi = p.TuftGroup[t];
                    if (onlyBig && kind != Shrub && kind != Boulder) continue;
                    var tints = CoverTints[kind];
                    _cover[kind].Add(gi, tints[gi < tints.Length ? gi : 0], p.Tufts[t]);
                }
            }
        }

        void DrawCover()
        {
            var c = Rig.Cam;
            _cover[Shrub].Shadows = CoverShadows; _cover[Boulder].Shadows = CoverShadows;
            for (int k = 0; k < _cover.Length; k++) _cover[k].Draw(c);
        }

        /// <summary>A patch's cover in order of kind and colour, so a gathering adds it in long runs of one group.</summary>
        static void SortCover(Patch p)
        {
            int n = p.TuftCount;
            if (n < 2) return;
            if (_sortKeys == null || _sortKeys.Length < n) { _sortKeys = new int[p.Tufts.Length]; _sortIdx = new int[p.Tufts.Length]; _sortM = new Matrix4x4[p.Tufts.Length]; _sortK = new byte[p.Tufts.Length]; _sortG = new byte[p.Tufts.Length]; }
            for (int t = 0; t < n; t++) { _sortKeys[t] = (p.TuftKind[t] << 8 | p.TuftGroup[t]) * 4096 + t; _sortIdx[t] = t; }
            System.Array.Sort(_sortKeys, _sortIdx, 0, n);
            for (int t = 0; t < n; t++) { int s = _sortIdx[t]; _sortM[t] = p.Tufts[s]; _sortK[t] = p.TuftKind[s]; _sortG[t] = p.TuftGroup[s]; }
            System.Array.Copy(_sortM, p.Tufts, n); System.Array.Copy(_sortK, p.TuftKind, n); System.Array.Copy(_sortG, p.TuftGroup, n);
        }
        static int[] _sortKeys, _sortIdx;
        static Matrix4x4[] _sortM;
        static byte[] _sortK, _sortG;

        /// <summary>
        /// The woods' trees on a finest patch (D2-079): every crown of the canopy's layout that falls on the patch, in the projection
        /// the shader uses on this face, stands as a tree where its number is below the threshold of the forest share there (the
        /// patch's own vertices, interpolated: the shader's value) - a conifer or a broadleaf as the canopy decides, as wide as its
        /// crown and as tall as its number says. Broadleaves come in kinds by their number (D2-081): round, tall and narrow, broad
        /// and spreading, and birches where the woods are mixed with conifers; where a fire went through in the last decades the
        /// trees are young and short.
        /// </summary>
        void PlaceForest(TC2.Core.Simulation.World w, Patch p)
        {
            p.TreeCount = 0;
            var pos = p.Pos; var wood = p.Wood; var land = p.Land;
            if (pos == null || wood == null || land == null) return;
            int V = N + 1;
            bool any = false;
            for (int o = 0; o < V * V && !any; o++) any = wood[o].x > 0.004f;
            if (!any) return;
            if (p.Trees == null) { p.Trees = new Matrix4x4[MaxTrees]; p.TreeKind = new byte[MaxTrees]; p.TreeGroup = new ushort[MaxTrees]; }
            Vector3 cd = p.Dirs[(N / 2) * V + N / 2];
            float axx = Mathf.Abs(cd.x), axy = Mathf.Abs(cd.y), axz = Mathf.Abs(cd.z);
            int ax = axx >= axy && axx >= axz ? 0 : (axy >= axz ? 1 : 2);
            float sc = Canopy.Side / Canopy.TileMetres, off = (ax == 0 ? 0f : ax == 1 ? 0.37f : 0.71f) * Canopy.Side;
            int side = Canopy.Side;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * V + i, b = a + 1, c = a + V, d = c + 1;
                    if (wood[a].x <= 0.004f && wood[b].x <= 0.004f && wood[c].x <= 0.004f && wood[d].x <= 0.004f) continue;
                    Vector2 qa = Proj(pos[a], ax, sc, off), qb = Proj(pos[b], ax, sc, off), qc = Proj(pos[c], ax, sc, off), qd = Proj(pos[d], ax, sc, off);
                    Vector2 e1 = qb - qa, e2 = qc - qa;
                    float det = e1.x * e2.y - e1.y * e2.x;
                    if (Mathf.Abs(det) < 1e-9f) continue;
                    int u0 = Mathf.FloorToInt(Mathf.Min(Mathf.Min(qa.x, qb.x), Mathf.Min(qc.x, qd.x))), u1 = Mathf.FloorToInt(Mathf.Max(Mathf.Max(qa.x, qb.x), Mathf.Max(qc.x, qd.x)));
                    int v0 = Mathf.FloorToInt(Mathf.Min(Mathf.Min(qa.y, qb.y), Mathf.Min(qc.y, qd.y))), v1 = Mathf.FloorToInt(Mathf.Max(Mathf.Max(qa.y, qb.y), Mathf.Max(qc.y, qd.y)));
                    for (int gv = v0; gv <= v1; gv++)
                        for (int gu = u0; gu <= u1; gu++)
                        {
                            int mu = ((gu % side) + side) % side, mv = ((gv % side) + side) % side;
                            int k = mv * side + mu;
                            // the crown of this square of the layout: its centre lies in the square
                            float cu = gu - mu + Canopy.X[k], cv = gv - mv + Canopy.Y[k];
                            float s = ((cu - qa.x) * e2.y - (cv - qa.y) * e2.x) / det, t = (e1.x * (cv - qa.y) - e1.y * (cu - qa.x)) / det;
                            if (s < 0f || s >= 1f || t < 0f || t >= 1f) continue;
                            Vector4 wd = Vector4.Lerp(Vector4.Lerp(wood[a], wood[b], s), Vector4.Lerp(wood[c], wood[d], s), t);
                            float num = Canopy.Num[k];
                            if (p.TreeCount >= MaxTrees) continue;
                            Vector3 at = Vector3.Lerp(Vector3.Lerp(pos[a], pos[b], s), Vector3.Lerp(pos[c], pos[d], s), t);
                            float near = Mathf.Lerp(Mathf.Lerp(land[a].y, land[b].y, s), Mathf.Lerp(land[c].y, land[d].y, s), t);
                            if (num >= Canopy.Threshold(Canopy.Stands(wd.x, at, near))) continue;   // (the share as stands and glades: D2-084)
                            bool con = Canopy.Conifer(num, wd.y);
                            float r = Canopy.Radius[k] * Canopy.Spacing * (con ? 1f / 1.32f : 1f);
                            float h = r * Canopy.Top(num) * (con ? 1.05f : 1.1f);
                            // young trees where a fire burnt the woods in the last few decades: they grow back from short to full height
                            int cell = p.Cells[a];
                            long burnt = w.Fire.BurnedAt[cell];
                            if (burnt != 0) h *= Mathf.Clamp(0.35f + (float)((w.Now - burnt) / (double)SimTime.Year) / 40f, 0.35f, 1f);
                            // its kind (D2-084): conifers are spruces, or pines where the land is dry or sandy; broadleaves are willows and
                            // alders by a river's water, birches in the mixed woods of the cool lands, else tall, wide or round
                            float dryHere = Mathf.Lerp(Mathf.Lerp(land[a].x, land[b].x, s), Mathf.Lerp(land[c].x, land[d].x, s), t);
                            float v = num * 3.71f - Mathf.Floor(num * 3.71f);
                            int kind;
                            if (con) kind = dryHere > 0.30f || p.Rel[a] < 8f || num * 5.7f - Mathf.Floor(num * 5.7f) < 0.18f ? ProcMeshes.TreePine : ProcMeshes.TreeSpruce;
                            else if (near > 0.55f) kind = ProcMeshes.TreeWillow;
                            else if (wd.y > 0.12f && v < 0.40f) kind = ProcMeshes.TreeBirch;
                            else kind = v < 0.28f ? ProcMeshes.TreeTall : v < 0.52f ? ProcMeshes.TreeWide : ProcMeshes.TreeRound;
                            int variant = Mathf.Clamp((int)((num * 9.13f - Mathf.Floor(num * 9.13f)) * ProcMeshes.TreeVariants), 0, ProcMeshes.TreeVariants - 1);
                            byte form = (byte)(kind * ProcMeshes.TreeVariants + variant);
                            p.Trees[p.TreeCount] = Matrix4x4.TRS(at, PlanetGeo.Upright(at, num * 2617f), new Vector3(r, h, r));
                            p.TreeKind[p.TreeCount] = form;
                            int shade = (int)((num * 5.31f - Mathf.Floor(num * 5.31f)) * 4f) & 3;   // (the shader's hue)
                            int leaf = Mathf.Clamp((int)(wd.z * 8f), 0, 7), snowB = Mathf.Clamp((int)(wd.w * 4f), 0, 3);
                            float dry = Mathf.Lerp(Mathf.Lerp(land[a].x, land[b].x, s), Mathf.Lerp(land[c].x, land[d].x, s), t);
                            int dryB = Mathf.Clamp(Mathf.RoundToInt(dry * 3f), 0, 3);
                            p.TreeGroup[p.TreeCount] = (ushort)(shade | (leaf << 2) | (snowB << 5) | (dryB << 7));
                            p.TreeCount++;
                        }
                }
        }

        static Vector2 Proj(Vector3 p, int ax, float sc, float off) =>
            ax == 0 ? new Vector2(p.y * sc + off, p.z * sc + off) : ax == 1 ? new Vector2(p.x * sc + off, p.z * sc + off) : new Vector2(p.x * sc + off, p.y * sc + off);

        /// <summary>The woods' trees of the shown finest patches within reach and in view, near ones in full, further ones lighter.</summary>
        void GatherForest()
        {
            for (int k = 0; k < _forest.Length; k++) _forest[k].Clear();
            ForestDrawn = 0;
            var cam = Rig.Cam;
            Vector3 cp = cam.transform.position;
            if ((WorldObjectsView.Off & 1) == 0 && cp.magnitude - (float)Host.World.Planet.Radius < ForestReach)
            {
                for (int q = 0; q < _shownLast.Count; q++)
                {
                    var p = _shownLast[q];
                    if (p.TreeCount == 0 || p.Renderer == null) continue;
                    var bounds = p.Renderer.bounds;
                    bounds.Expand(36f);   // (the trees stand above the ground)
                    float near = Mathf.Sqrt(bounds.SqrDistance(cp));
                    if (near > ForestReach || !GeometryUtility.TestPlanesAABB(_planes, bounds)) continue;
                    bool close = near < ForestNear;
                    for (int t = 0; t < p.TreeCount; t++)
                    {
                        int g = p.TreeGroup[t];
                        int form = p.TreeKind[t], kind = form / ProcMeshes.TreeVariants;
                        if (kind == ProcMeshes.TreeSpruce || kind == ProcMeshes.TreePine) _forest[close ? form : ConFar].Add(g, ConiferTints[g], p.Trees[t]);
                        else if (((g >> 2) & 7) >= 7) _forest[BareBroad].Add(g, BareTints[g], p.Trees[t]);
                        else if (!close) _forest[BroadFar].Add(g, LeafTints[g], p.Trees[t]);
                        else _forest[form].Add(g, LeafTints[g], p.Trees[t]);
                    }
                    ForestDrawn += p.TreeCount;
                }
            }
        }

        void DrawForest()
        {
            var cam = Rig.Cam;
            for (int k = 0; k < _forest.Length; k++) _forest[k].Draw(cam);
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
                p.Mesh.SetUVs(2, p.Wood);
                p.Mesh.SetUVs(3, p.Land);
                long today = SimTime.FloorDiv(w.Now, SimTime.Day);
                if (p.Level >= MaxLevel && p.Pos != null && p.Nrm != null && p.PlacedDay != today)
                {
                    // the woods and the cover change with the forest and the seasons: placed again once a world day at most
                    PlaceForest(w, p); PlaceCover(w, p, p.Pos, p.Nrm); SortCover(p); _contentVersion++; p.PlacedDay = today;
                }
                p.ColourCycle = _cycle;
            }
        }

        static readonly Color Earth = new Color(0.56f, 0.47f, 0.35f), Trodden = new Color(0.52f, 0.54f, 0.34f), Road = new Color(0.58f, 0.50f, 0.38f);
        static readonly Color Beaten = new Color(0.69f, 0.62f, 0.49f);   // (a training ground's dust, paler than ploughland: D2-114)
        // roads: the villages' own routes, smoothed, indexed by the cells they pass (rebuilt now and then)
        readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector4>> _roads = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector4>>();   // (segment ends; w: how worn, C4.1)
        readonly System.Collections.Generic.List<TC2.Core.Agents.Way> _ways = new System.Collections.Generic.List<TC2.Core.Agents.Way>();
        float _roadTimer;
        int _roadWorld = -1;

        /// <summary>
        /// The ways between villages as their people have worn them (C4.1, D2-097): a faint track, a worn path, a broad beaten road - each
        /// drawn along its route by how worn it is now; a way nobody walks is not drawn (before, every pair of villages within 4.5 km had a road).
        /// </summary>
        void BuildRoads(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            _roads.Clear();
            var pts = new System.Collections.Generic.List<Vec3d>();
            var tmp = new System.Collections.Generic.List<Vec3d>();
            w.Ways.Collect(w.Now, _ways);
            for (int i = 0; i < _ways.Count; i++)
            {
                var way = _ways[i];
                if (!w.Settlements.Exists(way.A) || !w.Settlements.Exists(way.B)) continue;
                var r = w.Routes.Get(w, way.A, way.B);
                if (r == null || r.Pts == null || r.Pts.Length < 2) continue;
                byte state = TC2.Core.Agents.Ways.StateOf(way.Use);
                // how it is drawn: a track faint and narrow, a path, a road broad and dark (and a little more with every walk between)
                float worn = state >= 3 ? 1f : state == 2 ? 0.62f + 0.3f * Mathf.Clamp01((float)((way.Use - TC2.Core.Agents.Ways.Path) / (TC2.Core.Agents.Ways.Road - TC2.Core.Agents.Ways.Path)))
                                                       : 0.25f + 0.3f * Mathf.Clamp01((float)((way.Use - TC2.Core.Agents.Ways.Track) / (TC2.Core.Agents.Ways.Path - TC2.Core.Agents.Ways.Track)));
                // Chaikin twice: a track that bends, not a chain of straight cell-to-cell legs
                pts.Clear(); pts.AddRange(r.Pts);
                for (int it = 0; it < 2; it++)
                {
                    tmp.Clear(); tmp.Add(pts[0]);
                    for (int k = 0; k + 1 < pts.Count; k++)
                    {
                        tmp.Add((pts[k] * 0.75 + pts[k + 1] * 0.25).Normalized);
                        tmp.Add((pts[k] * 0.25 + pts[k + 1] * 0.75).Normalized);
                    }
                    tmp.Add(pts[pts.Count - 1]);
                    pts.Clear(); pts.AddRange(tmp);
                }
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    Vec3d p0 = pts[k], p1 = pts[k + 1];
                    int c0 = P.Grid.CellOf(p0);
                    AddRoad(P, c0, p0, p1, worn);
                    for (int q = 0; q < 8; q++) AddRoad(P, P.Grid.Nbr8[c0 * 8 + q], p0, p1, worn);
                }
            }
        }

        void AddRoad(PlanetState P, int cell, Vec3d a, Vec3d b, float worn)
        {
            if (!_roads.TryGetValue(cell, out var list)) { list = new System.Collections.Generic.List<Vector4>(); _roads[cell] = list; }
            Vector3 va = PlanetGeo.ToVector(a), vb = PlanetGeo.ToVector(b);
            list.Add(new Vector4(va.x, va.y, va.z, worn)); list.Add(new Vector4(vb.x, vb.y, vb.z, worn));
        }

        /// <summary>How much of a point is road (metres to the nearest track, on the sphere's chord).</summary>
        float RoadAt(PlanetState P, Vec3d d, int c, float quad)
        {
            if (!_roads.TryGetValue(c, out var list)) return 0f;
            Vector3 p = PlanetGeo.ToVector(d);
            float most = 0f;
            for (int k = 0; k + 1 < list.Count; k += 2)
            {
                Vector3 a = list[k], ab = (Vector3)list[k + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                float m = (a + ab * t - p).magnitude * (float)P.Radius;
                // ~2-6 m of packed earth by how worn it is (C4.1); where the vertices stand further apart than that, the road is drawn
                // wider and fainter (the same ink), so the grid samples a soft path instead of dots
                float worn = list[k].w;
                float half = Mathf.Max(1.2f + 2.0f * worn, 0.8f * quad);
                float v = (1f - Edge(half * 0.55f, half * 1.4f, m)) * Mathf.Min(1f, (1.6f + 2.0f * worn) / half) * (0.35f + 0.65f * worn);
                if (v > most) most = v;
            }
            return most;
        }
        readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();
        /// <summary>Works out on the land near this patch - quarries (C1.3): cut rock where they stand, no fields or woods over them.</summary>
        readonly System.Collections.Generic.List<Vec3d> _sites = new System.Collections.Generic.List<Vec3d>();
        readonly System.Collections.Generic.List<Vector4> _grounds = new System.Collections.Generic.List<Vector4>();   // training grounds (xyz) and how beaten (w)

        /// <summary>A village's ground read once a patch (C13.1): each household's home and where its door path goes (its street, or the
        /// square), each street's direction and how far homes line it - not again for every vertex.</summary>
        sealed class Ground1
        {
            public float[] Hx = new float[64], Hy = new float[64], Tx = new float[64], Ty = new float[64];
            public int N;
            public readonly double[] Ox = new double[12], Oy = new double[12], Ux = new double[12], Uy = new double[12], Reach = new double[12];
            public int Lines;   // (streets and their back lanes: C13.1, C13.2)
            public double Sx, Sy;
            public double Market;   // the market place's radius (C13.3; 0 none)
        }
        readonly System.Collections.Generic.List<Ground1> _ground = new System.Collections.Generic.List<Ground1>();

        static void ReadGround(TC2.Core.Simulation.World w, int sid, Ground1 g)
        {
            ref var s = ref w.Settlements.Get(sid);
            g.Sx = s.SquareX; g.Sy = s.SquareY; g.Market = s.MarketR;
            g.Lines = TC2.Core.Places.Streets.Lines(in s);
            for (int i = 0; i < g.Lines; i++)
            {
                TC2.Core.Places.Streets.Line(in s, i, out double ox, out double oy, out double ux, out double uy, out _);
                g.Ox[i] = ox; g.Oy[i] = oy; g.Ux[i] = ux; g.Uy[i] = uy; g.Reach[i] = 0;
            }
            var hhs = w.HouseholdsOf(sid);
            if (g.Hx.Length < hhs.Count) { int n = hhs.Count * 2; g.Hx = new float[n]; g.Hy = new float[n]; g.Tx = new float[n]; g.Ty = new float[n]; }
            g.N = hhs.Count;
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var h = ref w.Households.Get(hhs[i]);
                bool fronts = TC2.Core.Places.Streets.Facing(in s, h.HomeX, h.HomeY, out double tx, out double ty, out int line);
                g.Hx[i] = h.HomeX; g.Hy[i] = h.HomeY; g.Tx[i] = (float)tx; g.Ty[i] = (float)ty;
                if (fronts)
                {
                    double along = (tx - g.Ox[line]) * g.Ux[line] + (ty - g.Oy[line]) * g.Uy[line];
                    if (along > g.Reach[line]) g.Reach[line] = along;
                }
            }
        }

        /// <summary>Packed earth on the square, yards round the houses, footpaths from each door to its street or the square, the streets (and
        /// how much is bare earth).</summary>
        static Color VillageGround(Ground1 g, double x, double y, Color col, out float bare)
        {
            double dsq = System.Math.Sqrt((x - g.Sx) * (x - g.Sx) + (y - g.Sy) * (y - g.Sy));
            float green = Mathf.Clamp01(1f - (float)(dsq - 40.0) / 45f);
            col = Color.Lerp(col, Trodden, green * 0.35f);                                    // the village green, walked on
            // the square - and where a market is held, its market place, trodden bare by the traders and their beasts (C13.3)
            float earth = g.Market > 0 ? (1f - Edge((float)g.Market - 7f, (float)g.Market + 0.5f, (float)dsq)) * 0.92f : 1f - Edge(9f, 15f, (float)dsq);
            for (int i = 0; i < g.N; i++)
            {
                double hx0 = g.Hx[i], hy0 = g.Hy[i];
                double hx = hx0 - x, hy = hy0 - y;
                double dh = System.Math.Sqrt(hx * hx + hy * hy);
                earth = Mathf.Max(earth, (1f - Edge(3.5f, 6.5f, (float)dh)) * 0.6f);   // the yard by the door (D2-114: before 8.5 m - in a town the yards ran together into one bare disc)
                // the path from the door to its street (C13.1), or to the square
                double ax = g.Tx[i] - hx0, ay = g.Ty[i] - hy0;
                double len2 = ax * ax + ay * ay;
                double t = len2 > 0 ? ((x - hx0) * ax + (y - hy0) * ay) / len2 : 0;
                if (t < 0) t = 0; else if (t > 1) t = 1;
                double px = hx0 + ax * t - x, py = hy0 + ay * t - y;
                double dp = System.Math.Sqrt(px * px + py * py);
                earth = Mathf.Max(earth, (1f - Edge(1.0f, 2.6f, (float)dp)) * 0.85f);
            }
            // its streets and lanes (C13.1, C13.2): beaten by everyone living along them, from the green (a lane from its first house) out past
            // the last house that fronts each
            for (int i = 0; i < g.Lines; i++)
            {
                double reach = g.Reach[i];
                if (reach <= 0) continue;
                double ux = g.Ux[i], uy = g.Uy[i];
                double px = x - g.Ox[i], py = y - g.Oy[i];
                double along = px * ux + py * uy;
                if (along < (i % 3 == 0 ? 0.0 : TC2.Core.Places.Streets.LaneStart - 10.0) || along > reach + 8.0) continue;
                double off = System.Math.Abs(-px * uy + py * ux);
                earth = Mathf.Max(earth, (1f - Edge(2.2f, 3.8f, (float)off)) * 0.9f * (1f - Edge((float)reach, (float)reach + 8f, (float)along)));
            }
            bare = earth;
            return Color.Lerp(col, Earth, earth);
        }

        static readonly Color Rock = new Color(0.47f, 0.44f, 0.40f), RockDark = new Color(0.33f, 0.31f, 0.30f);

        static readonly Color VolcanoRock = new Color(0.25f, 0.22f, 0.21f), VolcanoAsh = new Color(0.46f, 0.41f, 0.36f), VolcanoCrater = new Color(0.17f, 0.14f, 0.13f);
        // (beach sand and wet sand: the terrain shader's, drawn per pixel since D2-081)
        static readonly Color Snow = new Color(0.95f, 0.96f, 0.98f), Glacier = new Color(0.86f, 0.92f, 0.97f);
        static readonly Color Shelf = new Color(0.82f, 0.78f, 0.60f), Abyss = new Color(0.05f, 0.11f, 0.17f);

        void Colourise(TC2.Core.Simulation.World w, Patch p)
        {
            var P = w.Planet;
            if (_cellColour.Length != P.Cells) RefreshCellColours(w);
            double sea = P.SeaLevel;
            double season = DMath.Sin(DMath.TwoPi * (SimTime.YearFraction(w.Now) - 0.25));
            float yearNorth = (float)SimTime.YearFraction(w.Now);
            var cols = p.Colours;
            // roads are a few metres wide: on patches whose vertices stand far apart they would alias into dashes
            float quad = (float)(DMath.HalfPi * P.Radius / (1 << p.Level) / N);
            float roadLod = Edge(48f, 20f, quad);
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
                // (each near village's ground read once for the patch: C13.1)
                for (int q = 0; q < _near.Count; q++)
                {
                    if (_ground.Count <= q) _ground.Add(new Ground1());
                    ReadGround(w, _near[q], _ground[q]);
                }
                _sites.Clear();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var st = ref w.Settlements.Get(sid);
                    for (int k = 0; k < 3; k++)
                    {
                        // the ground broken at a quarry, a mine and bog-ore pits (C1.3, C5.1, C5.2b)
                        int site = k == 0 ? st.Quarry : k == 1 ? st.Mine : st.FeMine;
                        if (site == 0 || !w.Buildings.Exists(site)) continue;
                        ref var qb = ref w.Buildings.Get(site);
                        Vec3d at = st.LocalToSphere(qb.X, qb.Y, P.Radius);
                        double dq = System.Math.Acos(System.Math.Min(1.0, at.X * pc.x + at.Y * pc.y + at.Z * pc.z));
                        if (dq < reach) _sites.Add(at);
                    }
                }
                // the beaten earth of training grounds (C1.5, D2-114): bare by the hours of practice put in there (b.Use) - none before
                _grounds.Clear();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var st = ref w.Settlements.Get(sid);
                    if (st.Ground == 0 || !w.Buildings.Exists(st.Ground)) continue;
                    ref var gb = ref w.Buildings.Get(st.Ground);
                    float worn = Mathf.Clamp01(gb.Use / 1500f) * (gb.Left == 0 ? 1f : (float)TC2.Core.Places.Homes.Condition(in gb, w.Now));
                    if (worn <= 0.02f) continue;
                    Vec3d at = st.LocalToSphere(gb.X, gb.Y, P.Radius);
                    double dq = System.Math.Acos(System.Math.Min(1.0, at.X * pc.x + at.Y * pc.y + at.Z * pc.z));
                    if (dq < reach) _grounds.Add(new Vector4((float)at.X, (float)at.Y, (float)at.Z, worn));
                }
            }
            for (int o = 0; o < cols.Length; o++)
            {
                Vector3 dv = p.Dirs[o];
                var d = new Vec3d(dv.x, dv.y, dv.z);
                int c = p.Cells[o];
                float rel = p.Rel[o], slope = p.Slope[o];
                Color col;
                p.Wood[o] = Vector4.zero;
                p.Land[o] = Vector4.zero;
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
                    float forest = 0, clear = 0;   // (the woods' share here, and how much of it is cleared ground)
                    float moist = 0, grass = 0;    // (the climate's moisture, the grass)
                    float relief = 0;              // (valley floor -1 .. ridge +1: Ground.Relief)
                    for (int q = 0; q < n; q++)
                    {
                        int cq = _wc[q]; var cc = _cellColour[cq]; float ww = _ww[q];
                        col.r += cc.r * ww; col.g += cc.g * ww; col.b += cc.b * ww;
                        forest += P.Forest[cq] * ww;
                        relief += Ground.Relief(P, cq) * ww;
                        moist += P.Moist[cq] * ww; grass += P.Grass[cq] * ww;
                        if (w.Fire.Intensity[cq] > 0) clear += ww * Mathf.Clamp01(w.Fire.Intensity[cq] * 2f);
                        int fo = P.FieldOwner[cq];
                        if (fo != 0) { farm += ww; if (ww > ow) { ow = ww; owner = fo; } }
                    }
                    // a quarry's ground (C1.3): cut rock and spoil within ~20 m of the face - never a field or a wood over it
                    float site = 0f;
                    for (int q = 0; q < _sites.Count; q++)
                    {
                        Vec3d sv = _sites[q];
                        double ang = System.Math.Acos(System.Math.Min(1.0, sv.X * d.X + sv.Y * d.Y + sv.Z * d.Z));
                        site = Mathf.Max(site, 1f - Edge(16f, 28f, (float)(ang * P.Radius)));
                    }
                    farm *= 1f - site;
                    float beaten = 0f;
                    for (int q = 0; q < _grounds.Count; q++)
                    {
                        Vector4 gv = _grounds[q];
                        double ang = System.Math.Acos(System.Math.Min(1.0, gv.x * d.X + gv.y * d.Y + gv.z * d.Z));
                        beaten = Mathf.Max(beaten, (1f - Edge(6.5f, 10f, (float)(ang * P.Radius))) * gv.w);
                    }
                    farm *= 1f - beaten;
                    // farmland: the shader draws plots, furrows and hedges in the village's own grid
                    p.Plots[o] = Vector2.zero; p.Farm[o] = Vector2.zero;
                    if (owner != 0 && w.Settlements.Exists(owner))
                    {
                        ref var st = ref w.Settlements.Get(owner);
                        st.SphereToLocal(d, P.Radius, out double fx, out double fy);
                        p.Plots[o] = new Vector2((float)fx, (float)fy);
                        farm *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(85f, 135f, Mathf.Sqrt((float)(fx * fx + fy * fy)))); // houses and yards
                        float used = st.ArableHa > 0 ? (float)System.Math.Min(1.0, st.FieldHa / st.ArableHa) : 1f;
                        p.Farm[o] = site > 0.4f ? Vector2.zero : new Vector2(used, (float)st.Crop + (float)System.Math.Min(0.99, st.CropGrowth));   // (no plots on a quarry's ground)
                        Color crop = CropColour(st);
                        col = Color.Lerp(col, crop, Mathf.SmoothStep(0f, 1f, farm));
                    }
                    float bare = 0f;   // (packed earth: the square, yards, paths and roads)
                    for (int q = 0; q < _near.Count; q++)
                    {
                        ref var st = ref w.Settlements.Get(_near[q]);
                        double dot = st.Anchor.X * d.X + st.Anchor.Y * d.Y + st.Anchor.Z * d.Z;
                        if (dot < System.Math.Cos(130.0 / P.Radius)) continue;
                        st.SphereToLocal(d, P.Radius, out double vx, out double vy);
                        col = VillageGround(_ground[q], vx, vy, col, out float earth);
                        bare = Mathf.Max(bare, earth);
                        clear = Mathf.Max(clear, 1f - Edge(95f, 150f, (float)System.Math.Sqrt(vx * vx + vy * vy)));   // (no wood in the village)
                        VillageVerts++;
                    }
                    if (beaten > 0f) { col = Color.Lerp(col, Beaten, beaten * 0.85f); bare = Mathf.Max(bare, beaten); clear = Mathf.Max(clear, beaten); }
                    float road = RoadAt(P, d, c, quad) * roadLod;
                    bare = Mathf.Max(bare, road);
                    if (road > 0) col = Color.Lerp(col, Road, road * 0.85f);
                    // the river's corridor (D2-084): wet earth at the water's edge and a strip of lusher green along the banks, wider by a
                    // larger river (the woods keep to the water there: the stands' rank, Canopy.Stands; reeds at its edge: PlaceCover)
                    float riverNear = 0f, bank = float.MaxValue;
                    {
                        float rd = Rivers.Nearest(P, d, c, out float rh);
                        if (rd < 400f)
                        {
                            bank = rd - rh;
                            riverNear = Edge(40f + 4f * rh, 0f, bank);
                            col = Color.Lerp(col, new Color(0.22f, 0.39f, 0.16f), riverNear * 0.45f * (1f - Mathf.SmoothStep(0f, 1f, farm) * 0.6f));
                            col = Color.Lerp(col, new Color(0.29f, 0.25f, 0.17f), Edge(5f, 0f, bank) * 0.6f);
                        }
                    }
                    // natural variation: meadow patches warmer or cooler, small brightness noise
                    float hue = (float)Noise.Fbm(d.X * 9.0, d.Y * 9.0, d.Z * 9.0, 2);
                    float fine = (float)Noise.Perlin(d.X * 140.0, d.Y * 140.0, d.Z * 140.0);
                    col = new Color(col.r * (1f + 0.10f * hue) + 0.015f * fine, col.g * (1f + 0.04f * hue) + 0.02f * fine, col.b * (1f - 0.08f * hue) + 0.01f * fine);
                    // beaches where the land meets the sea
                    float beachW = (float)(1.0 + 0.9 * Noise.Fbm(d.X * 24.0, d.Y * 24.0, d.Z * 24.0, 2)) * (1f - Edge(0.05f, 0.25f, slope));
                    float beachWidth = Mathf.Max(0.15f, beachW), beach = BeachAt(rel, beachWidth);
                    // (the sand itself is drawn per pixel by the shader from the ground's own height over the sea, D2-081: a vertex colour
                    // follows the triangles of coarse patches, a staircase along every coast)
                    // rock on steep ground
                    float rock = Mathf.Max(Edge(0.10f, 0.30f, slope), site * 0.9f);
                    if (rock > 0) col = Color.Lerp(col, Color.Lerp(Rock, RockDark, Mathf.Clamp01(slope * 1.6f - 0.3f)), rock);
                    // a volcano: dark lava rock at the summit, ash and cinders down the flanks, streaked by its gullies
                    double cone = VolcanoShape.At(P, d, c, out float vrock, out float summit);
                    if (vrock > 0)
                    {
                        float streak = (float)Noise.Perlin(d.X * 220.0, d.Y * 220.0, d.Z * 220.0);
                        Color ash = Color.Lerp(VolcanoAsh, VolcanoRock, Mathf.Clamp01(vrock * 1.3f - 0.2f + 0.25f * streak));
                        col = Color.Lerp(col, ash, Mathf.Clamp01(vrock * 1.15f));
                        if (summit > 0) col = Color.Lerp(col, VolcanoCrater, summit * 0.8f);
                    }
                    // snow: the cell's climate at this height (lapse), the season, and ice sheets
                    double t = P.TMean[c] - 0.0065 * (rel - (Ground.DrawnElev(P, c) - sea) * Ground.VScale + cone) / Ground.VScale + P.TAmp[c] * season * (P.SinLat[c] >= 0 ? 1 : -1);
                    float snow = Mathf.Clamp01((float)(-t / 4.0 + 0.2)) * (1f - rock * 0.6f);
                    if (P.Ice[c] > 2f) snow = 1f;
                    if (snow > 0) col = Color.Lerp(col, P.Ice[c] > 2f ? Glacier : Snow, snow);
                    // the woods (D2-079): the simulation's forest share, less what is fields, village, road, beach, rock, lava or ice;
                    // conifers in the cold, broadleaves in the warm (as the trees drawn near the camera); the leaves' season where
                    // there is an autumn; the snow lying on them
                    float wood = forest * (1f - Mathf.SmoothStep(0f, 1f, farm)) * (1f - Mathf.Clamp01(clear)) * (1f - road) * (1f - Edge(0f, 0.5f, beach)) * (1f - rock) * Edge(-1f, 3f, bank)
                                 * (1f - Mathf.Clamp01(vrock * 1.15f)) * (P.Ice[c] > 2f ? 0f : 1f);
                    // (the woods' stands and glades are drawn from this share per pixel, and per tree near the camera: Canopy.Stands, D2-084)
                    // clumps of trees with gaps between, some twenty to forty metres across, where the woods are open; a closed wood
                    // stays closed (D2-081: the canopy's own tile repeats, so the woods' pattern lives here, where nothing repeats)
                    float clump = (float)Noise.Fbm(d.X * 230.0 + 5.1, d.Y * 230.0 - 2.7, d.Z * 230.0 + 1.3, 2);
                    wood = Mathf.Clamp01(wood * (1f + 1.5f * clump * Mathf.Clamp01((1.05f - wood) * 1.4f)));
                    if (wood > 0.005f)
                    {
                        float tm = P.TMean[c];
                        float con = 1f - Edge(1.5f, 15f, tm);
                        // (D2-084) where the climate holds both, the conifers keep to the ridges and thin soils, the broadleaves to the
                        // valleys' deeper, wetter ground - the woods' make-up follows the land's relief
                        con = Mathf.Clamp01(con + (0.40f * Mathf.Clamp01(relief) - 0.25f * Mathf.Clamp01(-relief)) * (1f - Edge(15f, 21f, tm)) * Edge(-2f, 4f, tm));
                        float f = P.SinLat[c] < 0 ? (yearNorth + 0.5f) % 1f : yearNorth;
                        float leaves = f < 0.5f ? 0f : (f < 0.875f ? (f - 0.5f) / 0.375f * 0.8f : 1f);
                        leaves *= 1f - Edge(17f, 22f, tm);   // (no autumn in the tropics)
                        p.Wood[o] = new Vector4(wood, con, leaves, snow);
                    }
                    // the land's own (D2-081): how dry its climate is (leaves olive, grass straw), how near a river's water (D2-084: its
                    // corridor), bare earth or rock, the beach's width
                    p.Land[o] = new Vector4(Mathf.Clamp01((0.45f - moist) / 0.30f), riverNear, Mathf.Max(bare, rock), P.Ice[c] < 1f ? beachWidth : 0f);
                }
                var lin = col.linear;
                lin.a = rel < 0 || p.Farm[o].x <= 0 ? 0f : Mathf.Clamp01(FarmWeight(P, d, c) * CoreMask(p.Plots[o]));
                cols[o] = lin;
            }
        }

        /// <summary>GLSL smoothstep(edge0, edge1, x) - not Unity's Mathf.SmoothStep, which interpolates between its first two arguments.</summary>
        static float Edge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        /// <summary>How much of a point is beach (D2-081): sand from the water's edge to 0.6 + 4.2 x its width metres over the sea - the
        /// terrain shader's own rule, which draws it per pixel.</summary>
        static float BeachAt(float rel, float width) => width <= 0f ? 0f : 1f - Edge(0.6f, 0.6f + 4.2f * width, rel);

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
            if (!P.IsLand(c)) return PlanetGeo.BiomeColor((Biome)P.Biome[c]);
            float forest = P.Forest[c], grass = P.Grass[c];
            // the open land's own colour (D2-084), from the climate and the ground's water rather than the biome's class (a class
            // boundary drew as a step of colour): lush green meadow where it is moist and mild, gold and olive steppe where the climate
            // is dry, sand where little grows in a dry land, grey-green tundra in the cold, the grass browning in a dry spell (the soil's
            // water), dark wet meadow on valley floors (their relief: Ground.Relief); the woods' floor is the canopy's (the shader)
            float tm = P.TMean[c], soil = P.SoilWater[c];
            float aridity = Mathf.Clamp01((0.45f - P.Moist[c]) / 0.30f);
            Color lush = new Color(0.35f, 0.52f, 0.20f), steppe = new Color(0.70f, 0.62f, 0.35f), sand = new Color(0.83f, 0.71f, 0.49f);
            Color tundra = new Color(0.49f, 0.50f, 0.40f), browned = new Color(0.62f, 0.58f, 0.33f), wet = new Color(0.22f, 0.36f, 0.17f);
            Color col = Color.Lerp(lush, steppe, Edge(0.15f, 0.85f, aridity));
            col = Color.Lerp(col, sand, Edge(0.7f, 1.0f, aridity) * (1f - Mathf.Clamp01(grass * 2.5f)));
            col = Color.Lerp(col, browned, Mathf.Clamp01((0.42f - soil) * 2.2f) * (1f - aridity) * 0.8f);
            col = Color.Lerp(col, tundra, 1f - Edge(-5f, 2f, tm));
            col = Color.Lerp(col, new Color(0.53f, 0.52f, 0.45f), Edge(1300f, 2400f, P.Elev[c] - (float)P.SeaLevel) * 0.8f);   // thin alpine turf and stone
            float valley =Mathf.Clamp01(-Ground.Relief(P, c) * 1.8f) * Mathf.Clamp01(soil * 1.6f) * (1f - aridity);
            col = Color.Lerp(col, wet, valley * 0.65f);
            col = Color.Lerp(col, new Color(0.60f, 0.54f, 0.42f), Mathf.Clamp01(0.25f - grass - forest) * 2.5f * (1f - aridity)); // bare ground
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
