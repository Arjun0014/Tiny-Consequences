using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// Everything standing on the planet (13 §2, D2-026), drawn with the asset kit: each household's house (four types
    /// by a stable hash), the village granary and well, haystacks once the harvest is in, orchard trees and bushes in the
    /// yards; forests by species (conifers where it is cold, broadleaf and birch where it is mild) in groves and glades;
    /// fire in burning cells; people at their analytic positions at render time, in natural dyed clothes. Things stay
    /// close to their real size near the camera and grow a little with distance so villages read from afar.
    /// Reads the world only.
    /// </summary>
    public sealed class WorldObjectsView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        Instancer _people, _granaries, _wells, _hay, _flames, _huts, _tents, _ruins, _beached, _afloat, _walls, _woodpiles;
        Instancer _beachedP, _afloatP;   // plank boats on the beach and out on the water (C9.2b); _beached/_afloat are the dugouts
        // the boats out this frame (C9.2): a boat's hull is drawn once, its crew seated in it
        readonly System.Collections.Generic.Dictionary<int, int> _hulls = new System.Collections.Generic.Dictionary<int, int>();
        readonly System.Collections.Generic.List<(Vector3 pos, Quaternion rotation, float scale)> _hullAt = new System.Collections.Generic.List<(Vector3 pos, Quaternion rotation, float scale)>();
        readonly System.Collections.Generic.List<int> _hullSeats = new System.Collections.Generic.List<int>();
        /// <summary>The village works by structure kind (C1.1; the civilisation kit, D2-089); null where a kind has no model yet.</summary>
        readonly Instancer[] _works = new Instancer[25];   // (by structure kind: the kiln is 24, C3.2)
        Instancer[] _homes, _frames;   // homes by plan and roof form; the timber framing of framed ones (D2-114)
        Instancer _chimneys, _plinths, _outshuts, _pens, _stalls, _cross, _gardens, _gardensBare;   // overlays, pens, the market, kitchen gardens (D2-114; out of leaf: C3.7a)
        Instancer _tenters;   // cloth stretched to dry by a weaver's shed while its village has wares (C3.1)
        Instancer _clayPits, _jars, _ware;   // the clay pit by a kiln, sealed jars by a granary, ware set out for sale (C3.2)
        Instancer _handcarts, _oxcarts, _cartLoads, _oxen;   // households' carts out with their people, a load on them, the ox before an ox cart (C4.2)
        Instancer _parked, _parkedOx;                        // and parked in their yards (rebuilt with the places, not every frame)
        Instancer _stonePiles;     // stone carried home and stacked by the store (C1.3)
        Instancer[] _trees;
        Material _mat;
        float _treeTimer, _placeTimer;
        public int PeopleDrawn, PeopleBusy, TreesDrawn, BoatsAfloat;   // (PeopleBusy: drawn at work or on the move - Presence.Busy)
        public static int Off;   // tools (webperf): parts not drawn - 1 trees, 2 people, 4 buildings and yards
        public static bool ShowGround;   // tools (shots -tc2ground): each home's ground as the simulation holds it (C13.3), a red plate under it

        // homes by their recorded form (D2-114): a model a plan and roof form, overlays composed per house; dimensions as the kit builds them
        // (and the narrow town house of a divided plot, gable to the street: C13.3, D2-118)
        static readonly string[] PlanNames = { "small", "cottage", "long", "two", "hall", "row" };
        static readonly string[] RoofNames = { "g", "h", "j" };
        internal static readonly float[] PlanL = { 4.8f, 7.0f, 12.0f, 6.6f, 10.0f, 4.6f }, PlanW = { 3.8f, 5.0f, 5.4f, 5.4f, 6.2f, 6.2f }, PlanRidge = { 4.3f, 5.0f, 5.2f, 8.6f, 7.1f, 9.2f };
        static readonly int[] PlanRoom = { 3, 6, 8, 8, 10, 8 };   // people a plan houses before they build on a lean-to
        // (where a plan's chimney stands, kit metres: x along the house, y across it (+y the back), and its top)
        static readonly Vector3[] ChimneyAt = { new Vector3(1.2f, 0.3f, 4.9f), new Vector3(2.0f, 0.4f, 5.7f), new Vector3(-2.4f, 0.4f, 5.9f), new Vector3(-2.0f, 0.3f, 9.3f), new Vector3(-1.5f, 3.45f, 7.7f), new Vector3(0f, 1.9f, 9.9f) };
        // the roof covers and the walls, by what they are made of (sRGB albedo; the kit paints them a neutral 0.86 and 0.90)
        static readonly Color[] CoverCol = { new Color(0.74f, 0.61f, 0.36f), new Color(0.60f, 0.53f, 0.40f), new Color(0.46f, 0.38f, 0.30f), new Color(0.74f, 0.35f, 0.21f), new Color(0.42f, 0.44f, 0.48f) };
        static readonly Color[][] WallCol =
        {
            new[] { new Color(0.82f, 0.71f, 0.53f), new Color(0.77f, 0.68f, 0.56f), new Color(0.85f, 0.75f, 0.59f), new Color(0.74f, 0.63f, 0.49f) },   // daub
            new[] { new Color(0.96f, 0.94f, 0.89f), new Color(0.95f, 0.89f, 0.75f), new Color(0.93f, 0.81f, 0.77f), new Color(0.94f, 0.85f, 0.63f), new Color(0.85f, 0.88f, 0.89f), new Color(0.87f, 0.89f, 0.81f) },   // limewash
            new[] { new Color(0.95f, 0.92f, 0.84f), new Color(0.94f, 0.88f, 0.74f), new Color(0.92f, 0.82f, 0.78f), new Color(0.93f, 0.86f, 0.66f), new Color(0.86f, 0.88f, 0.88f), new Color(0.90f, 0.88f, 0.80f) },   // framed: the infill
            new[] { new Color(0.50f, 0.37f, 0.25f), new Color(0.44f, 0.32f, 0.22f), new Color(0.54f, 0.41f, 0.29f) },   // logs
            new[] { new Color(0.71f, 0.69f, 0.63f), new Color(0.65f, 0.63f, 0.59f), new Color(0.75f, 0.71f, 0.63f) },   // stone
        };
        const int Conifer = 0, ConiferSmall = 1, Broadleaf = 2, BroadleafRound = 3, Birch = 4, Bush = 5;
        static readonly string[] TreeModels = { "tree_conifer", "tree_conifer_small", "tree_broadleaf", "tree_broadleaf_round", "tree_birch", "bush" };

        // natural dyes: undyed linen, madder, woad, weld, walnut, sage, ochre, slate
        static readonly Color[] Dyes =
        {
            new Color(0.86f, 0.82f, 0.72f), new Color(0.66f, 0.27f, 0.22f), new Color(0.30f, 0.40f, 0.60f), new Color(0.82f, 0.72f, 0.36f),
            new Color(0.45f, 0.33f, 0.24f), new Color(0.50f, 0.58f, 0.42f), new Color(0.76f, 0.55f, 0.30f), new Color(0.42f, 0.44f, 0.48f),
        };

        /// <summary>The view (one in the scene): LivestockView pens the animals in the yard pens drawn here.</summary>
        public static WorldObjectsView Instance;

        /// <summary>The yard pen behind a home as drawn (D2-118: only on ground its neighbours, the streets and the green leave free), and how much
        /// larger than life its home may be drawn far off (its room); false where the home has no free ground for one or is not read yet.</summary>
        public bool YardPen(int bid, out float room)
        {
            var pl = PlotOf(bid);
            room = pl != null ? pl.Room : 1f;
            return pl != null && pl.Pen;
        }

        void Start()
        {
            Instance = this;
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _people = new Instancer(Models.Get("person", ProcMeshes.Person), _mat, true);
            _homes = new Instancer[PlanNames.Length * RoofNames.Length];
            for (int p = 0; p < PlanNames.Length; p++)
                for (int r = 0; r < RoofNames.Length; r++) _homes[p * RoofNames.Length + r] = new Instancer(Models.Get("home_" + PlanNames[p] + "_" + RoofNames[r], ProcMeshes.House), _mat, true);
            _frames = new Instancer[PlanNames.Length];
            for (int p = 0; p < PlanNames.Length; p++) _frames[p] = new Instancer(Models.Get("frame_" + PlanNames[p], ProcMeshes.House), _mat, true);
            _chimneys = new Instancer(Models.Get("chimney", ProcMeshes.Granary), _mat, true);
            _plinths = new Instancer(Models.Get("plinth", ProcMeshes.Granary), _mat, true);
            _outshuts = new Instancer(Models.Get("outshut", ProcMeshes.House), _mat, true);
            _pens = new Instancer(Models.Get("fence", ProcMeshes.Granary), _mat, true);
            _gardens = new Instancer(Models.Get("garden", ProcMeshes.Granary), _mat, true);
            _gardensBare = new Instancer(Models.Get("garden_bare", ProcMeshes.Granary), _mat, true);
            _stalls = new Instancer(Models.Get("market_stall", ProcMeshes.Granary), _mat, true);
            _cross = new Instancer(Models.Get("market_cross", ProcMeshes.Granary), _mat, true);
            _stonePiles = new Instancer(Models.Get("stone_pile", ProcMeshes.Granary), _mat, true);
            _granaries = new Instancer(Models.Get("granary", ProcMeshes.Granary), _mat, true);
            _walls = new Instancer(Models.Get("palisade", ProcMeshes.Palisade), _mat, true);   // walled villages (D2-074)
            _huts = new Instancer(Models.Get("hut_round", ProcMeshes.House), _mat, true);
            _tents = new Instancer(Models.Get("tent", ProcMeshes.House), _mat, true);   // camps: hides over poles
            _ruins = new Instancer(Models.Get("ruin", ProcMeshes.House), _mat, true);   // the houses of abandoned villages (D2-052)
            _beached = new Instancer(Models.Get("dugout", ProcMeshes.Granary), _mat, true);   // dugouts drawn up on the shore (C9.1)
            _afloat = new Instancer(Models.Get("dugout", ProcMeshes.Granary), _mat, true);    // and out on the water with their crews
            _beachedP = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);    // plank boats (C9.2b)
            _afloatP = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);
            _wells = new Instancer(Models.Get("well", ProcMeshes.Granary), _mat, true);
            _works[Structures.Well] = _wells; _works[Structures.Granary] = _granaries;
            _works[Structures.Barn] = new Instancer(Models.Get("barn", ProcMeshes.House), _mat, true);
            _works[Structures.Smithy] = new Instancer(Models.Get("smithy", ProcMeshes.House), _mat, true);
            _forgeTop = new Vector3(-3.0f, 6.6f, -0.6f);   // (the top of the smithy's stone chimney, as the kit builds it: D2-114)
            _works[Structures.Quarry] = new Instancer(Models.Get("quarry_face", ProcMeshes.Granary), _mat, true);
            _works[Structures.Mine] = new Instancer(Models.Get("mine_pit", ProcMeshes.Granary), _mat, true);
            _works[Structures.CharHearth] = new Instancer(Models.Get("charcoal_clamp", ProcMeshes.Granary), _mat, true);
            _works[Structures.BogPits] = new Instancer(Models.Get("bog_pits", ProcMeshes.Granary), _mat, true);
            _works[Structures.Ground] = new Instancer(Models.Get("training_ground", ProcMeshes.Granary), _mat, true);
            _works[Structures.Market] = _cross;   // (the market: its cross, and stalls round it - DrawWork)
            _works[Structures.BoatShed] = new Instancer(Models.Get("boat_shed", ProcMeshes.House), _mat, true);   // (C9.2b)
            _works[Structures.Workshop] = new Instancer(Models.Get("weavers_shed", ProcMeshes.House), _mat, true);   // (C3.1)
            _tenters = new Instancer(Models.Get("tenter", ProcMeshes.Granary), _mat, true);
            _works[Structures.Kiln] = new Instancer(Models.Get("kiln", ProcMeshes.House), _mat, true);   // (C3.2)
            _kilnTop = new Vector3(2.1f, 2.4f, -0.2f);   // (the vent at the dome's crown, as the kit builds it - its x mirrored, its y to -z)
            _clayPits = new Instancer(Models.Get("clay_pit", ProcMeshes.Granary), _mat, true);
            _jars = new Instancer(Models.Get("storage_jars", ProcMeshes.Granary), _mat, true);
            _ware = new Instancer(Models.Get("ware_stack", ProcMeshes.Granary), _mat, true);
            _handcarts = new Instancer(Models.Get("handcart", ProcMeshes.Granary), _mat, true);   // (C4.2)
            _oxcarts = new Instancer(Models.Get("ox_cart", ProcMeshes.Granary), _mat, true);
            _cartLoads = new Instancer(Models.Get("cart_load", ProcMeshes.Granary), _mat, true);
            _oxen = new Instancer(Models.Get("animal_cow", ProcMeshes.Granary), _mat, true);
            _parked = new Instancer(Models.Get("handcart", ProcMeshes.Granary), _mat, true);
            _parkedOx = new Instancer(Models.Get("ox_cart", ProcMeshes.Granary), _mat, true);
            _hay = new Instancer(Models.Get("haystack", ProcMeshes.Granary), _mat, true);
            _woodpiles = new Instancer(ProcMeshes.Woodpile(), _mat, true);   // the village's woodstore, by the homes (D2-081)
            _trees = new Instancer[TreeModels.Length];
            for (int i = 0; i < TreeModels.Length; i++) _trees[i] = new Instancer(Models.Get(TreeModels[i], i <= ConiferSmall ? (System.Func<Mesh>)ProcMeshes.Conifer : ProcMeshes.Broadleaf), _mat, true);
            _flames = new Instancer(ProcMeshes.Flame(), _mat, false);
        }

        Vector3 _forgeTop;   // the smithy's chimney top in its model's frame (C1.4): smoke while a smith works there
        Vector3 _kilnTop;    // the kiln's vent in its model's frame (C3.2): smoke while a potter works at it

        readonly List<Vector4> _smoke = new List<Vector4>();   // chimney tops with smoke (xyz) and how much (w)
        readonly List<Vector4> _lights = new List<Vector4>();  // villages at night (xyz) and how many of their homes are lit (w)
        readonly List<Vector4> _watch = new List<Vector4>();   // the watch fires at village stores tonight (xyz)

        /// <summary>
        /// A village's lights at night seen from high above (D2-081): a warm glow over the village, as wide and as bright as the homes
        /// whose people are awake inside (lit windows, hearths) - the same homes whose windows the near view lights; nothing when all
        /// sleep. Drawn only when the camera is too high to see the windows themselves.
        /// </summary>
        void EmitLights()
        {
            if (_lights.Count == 0 || Rig == null || Rig.Cam == null) return;
            Vector3 cam = Rig.Cam.transform.position;
            float R = (float)Host.World.Planet.Radius, alt = cam.magnitude - R;
            float show = Mathf.InverseLerp(1500f, 4000f, alt);
            if (show <= 0f) return;
            for (int i = 0; i < _lights.Count; i++)
            {
                var v = _lights[i];
                Vector3 at = new Vector3(v.x, v.y, v.z);
                float lit = v.w;
                // (a glow some pixels across at any height: wider as the camera rises, by the village's lit homes)
                float size = Mathf.Max(60f, alt * 0.012f) * (0.6f + 0.25f * Mathf.Sqrt(lit));
                float glow = show * Mathf.Clamp01(0.35f + lit / 12f);
                Puffs.Add(at + at.normalized * (size * 0.1f), size, 0.05f * show, 0f, glow * 1.6f, Mathf.Repeat(i * 0.618f, 1f), 0f);
            }
        }

        /// <summary>The watch fires (D2-071): a small fire by a village's store while someone keeps the watch there at night.</summary>
        void EmitWatch()
        {
            if (_watch.Count == 0 || Rig == null || Rig.Cam == null) return;
            Vector3 cam = Rig.Cam.transform.position;
            float t = Time.unscaledTime;
            for (int i = 0; i < _watch.Count; i++)
            {
                var v = _watch[i];
                Vector3 at = new Vector3(v.x, v.y, v.z);
                if ((at - cam).sqrMagnitude > 2500f * 2500f) continue;
                Vector3 up = at.normalized;
                float ex = Exaggeration(at, 2.5f);
                for (int k = 0; k < 3; k++)
                {
                    float flicker = 0.75f + 0.25f * Mathf.Sin(t * 8f + k * 2.1f + i);
                    float h = 1.1f * flicker * ex;
                    _flames.Add(k % 2, k % 2 == 0 ? new Color(1f, 0.45f, 0.08f) : new Color(1f, 0.75f, 0.2f),
                                Matrix4x4.TRS(at + Quaternion.AngleAxis(k * 120f, up) * Vector3.Cross(up, Vector3.right).normalized * 0.25f * ex, PlanetGeo.Upright(at, k * 60f), new Vector3(h * 0.45f, h, h * 0.45f)), 2.5f);
                }
                Puffs.Add(at + up * (1.2f * ex), 7f * ex, 0.04f, 0f, 1.1f, Mathf.Repeat(i * 0.37f, 1f), 0f);
            }
        }

        /// <summary>
        /// Smoke from the chimneys of houses whose people are home and awake: while one of them cooks, and all day while the
        /// hearth burns against the cold (the simulation's own cooking and fires, D2-056). Soft puffs that rise and drift with
        /// the wind; only near the camera.
        /// </summary>
        void EmitSmoke(TC2.Core.Simulation.World w)
        {
            if (_smoke.Count == 0 || Rig == null || Rig.Cam == null) return;
            Vector3 cam = Rig.Cam.transform.position;
            float now = Time.unscaledTime;
            for (int i = 0; i < _smoke.Count; i++)
            {
                var c = _smoke[i];
                Vector3 top = new Vector3(c.x, c.y, c.z);
                if ((top - cam).sqrMagnitude > 1100f * 1100f) continue;
                Vector3 up = top.normalized;
                Vector3 east = Vector3.Cross(Vector3.up, up);
                if (east.sqrMagnitude < 1e-6f) east = Vector3.right;
                east.Normalize();
                float seed = Mathf.Repeat(i * 0.6180339f, 1f);
                Vector3 drift = (east * Mathf.Cos(seed * 2f + now * 0.05f) + Vector3.Cross(up, east) * Mathf.Sin(seed * 2f + now * 0.05f) * 0.4f).normalized;
                const int n = 14;
                const float life = 11f;
                for (int k = 0; k < n; k++)
                {
                    float ph = Mathf.Repeat(now / life + k / (float)n + seed, 1f);
                    Vector3 p = top + up * (0.4f + 8f * ph) + drift * (9f * ph * Mathf.Sqrt(ph));
                    float size = 0.9f + 5.2f * ph;
                    float a = c.w * 0.24f * Mathf.Pow(1f - ph, 1.3f) * Mathf.Min(1f, ph * 8f);
                    Puffs.Add(p, size, a, 0.28f + 0.12f * ph, 0f, Mathf.Repeat(seed * 3f + k * 0.37f, 1f), 1f);
                }
            }
        }

        /// <summary>
        /// People's presentation scale (D2-084): a little larger than life, more with distance, and never under ~4 px tall on screen so
        /// a village's activity reads from the village view (at 900 m true size is ~3 px); at most 4x. Presentation only.
        /// </summary>
        float PeopleScale(Vector3 at)
        {
            if (Rig == null || Rig.Cam == null) return 1.35f;
            float d = Vector3.Distance(Rig.Cam.transform.position, at);
            float h = Rig.Cam.pixelHeight > 100 ? Rig.Cam.pixelHeight : 900f;
            float mpp = 2f * d * Mathf.Tan(Rig.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / h;   // metres a pixel there
            float floor = 4f * mpp / (1.35f * 1.72f);
            return 1.35f * Mathf.Clamp(Mathf.Max(d / 900f, floor), 1f, 4f);
        }

        /// <summary>Presentation scale: real size close in, a little larger with distance so things stay readable.</summary>
        float Exaggeration(Vector3 at, float max)
        {
            if (Rig == null || Rig.Cam == null) return 1f;
            float d = Vector3.Distance(Rig.Cam.transform.position, at);
            return Mathf.Clamp(d / 900f, 1f, max);
        }

        void LateUpdate()
        {
            var w = Host.World;
            if (w == null || Host.CatchingUp) return;
            _placeTimer -= Time.unscaledDeltaTime;
            if (_placeTimer <= 0) { _placeTimer = 0.5f; RebuildPlaces(w); }
            _treeTimer -= Time.unscaledDeltaTime;
            if (_treeTimer <= 0) { _treeTimer = 1.5f; RebuildTrees(w); }
            RebuildFire(w);
            EmitWatch();
            if ((Off & 2) == 0) RebuildPeople(w);
            var cam = Rig != null ? Rig.Cam : Camera.main;
            if ((Off & 4) == 0)
            {
                for (int i = 0; i < _homes.Length; i++) _homes[i].Draw(cam);
                for (int i = 0; i < _frames.Length; i++) _frames[i].Draw(cam);
                _chimneys.Draw(cam); _plinths.Draw(cam); _outshuts.Draw(cam); _pens.Draw(cam); _stalls.Draw(cam); _gardens.Draw(cam); _gardensBare.Draw(cam); _tenters.Draw(cam); _clayPits.Draw(cam); _jars.Draw(cam); _ware.Draw(cam); _handcarts.Draw(cam); _oxcarts.Draw(cam); _cartLoads.Draw(cam); _oxen.Draw(cam); _parked.Draw(cam); _parkedOx.Draw(cam);
                _stonePiles.Draw(cam);
                for (int i = 0; i < _works.Length; i++) _works[i]?.Draw(cam);
                _hay.Draw(cam); _huts.Draw(cam); _tents.Draw(cam); _ruins.Draw(cam); _walls.Draw(cam); _woodpiles.Draw(cam);
            }
            if ((Off & 1) == 0) for (int i = 0; i < _trees.Length; i++) _trees[i].Draw(cam);
            _flames.Draw(cam);
            if ((Off & 2) == 0) { _people.Draw(cam); _afloat.Draw(cam); _afloatP.Draw(cam); }
            if ((Off & 4) == 0) { _beached.Draw(cam); _beachedP.Draw(cam); }
            EmitSmoke(w);
            EmitLights();
        }

        static bool SomeoneAwakeHome(TC2.Core.Simulation.World w, int household, out bool cooking)
        {
            var m = w.Members(household);
            bool awake = false; cooking = false;
            for (int i = 0; i < m.Count; i++)
            {
                ref var p = ref w.People.Get(m[i]);
                if (!p.Alive || p.Act == ActivityKind.Sleep || p.At != p.Home || p.Step == StepKind.Travel) continue;
                awake = true;
                if (p.Act == ActivityKind.Cook) cooking = true;
            }
            return awake;
        }

        readonly System.Collections.Generic.List<double> _gates = new System.Collections.Generic.List<double>();
        readonly System.Collections.Generic.List<TC2.Core.Agents.Way> _wallWays = new System.Collections.Generic.List<TC2.Core.Agents.Way>();
        int _wallWaysAt = -1;

        /// <summary>
        /// A village's palisade (D2-074): stretches of posts round the ring its people laid out, with gates where the roads leave
        /// for its neighbours; while being built, as many stretches as the work has set; the timber greys as it rots.
        /// </summary>
        void DrawWall(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int sid, float ex)
        {
            double share = 1.0;
            if (s.Wall == 1)
            {
                Construction.WorkCost(Construction.Palisade, in s, out double need, out _);
                share = Construction.NextWork(in s) == Construction.Palisade && need > 0 ? Mathf.Clamp01((float)(s.WorkBuild / need)) : 0.0;
            }
            if (s.Wall == 1 || s.Wall == 2) DrawRing(w, P, in s, sid, ex, s.WallR, s.Wall == 2 ? s.WallCond : 1f, share);
            if (s.OldR > 0f) DrawRing(w, P, in s, sid, ex, s.OldR, s.OldCond, 1.0);   // (the old ring inside a grown town, rotting: C7.1)
        }

        void DrawRing(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int sid, float ex, double r, float condition, double share)
        {
            if (r < 5.0) return;
            int stretches = Mathf.Max(8, (int)(2.0 * System.Math.PI * r / 2.0));
            int set = (int)(stretches * share);
            if (set == 0) return;
            // the gates: where its streets run out through the ring, and where each worn way out of it crosses the ring as it is drawn
            // (WayLines; session 17 - before, toward each neighbour's centre, and a road could run into the posts); a village no way reaches
            // keeps a gate toward each neighbour within a day's walk
            _gates.Clear();
            for (int k = 0; k < s.StreetN; k++) _gates.Add(s.StreetAt(k));
            if (_wallWaysAt != Time.frameCount) { _wallWaysAt = Time.frameCount; w.Ways.Collect(w.Now, _wallWays); }
            for (int i = 0; i < _wallWays.Count; i++)
            {
                var way = _wallWays[i];
                if (way.A != sid && way.B != sid) continue;
                var route = w.Routes.Get(w, way.A, way.B);
                if (route == null) continue;
                var line = TC2.Core.Agents.WayLines.Of(w, route);
                for (int k = 0; k + 1 < line.Length; k++)
                {
                    s.SphereToLocal(line[k], P.Radius, out double ax, out double ay);
                    s.SphereToLocal(line[k + 1], P.Radius, out double bx, out double by);
                    double da = System.Math.Sqrt((ax - s.SquareX) * (ax - s.SquareX) + (ay - s.SquareY) * (ay - s.SquareY));
                    double db = System.Math.Sqrt((bx - s.SquareX) * (bx - s.SquareX) + (by - s.SquareY) * (by - s.SquareY));
                    if ((da - r) * (db - r) > 0) continue;
                    double f = System.Math.Abs(db - da) > 1e-9 ? (r - da) / (db - da) : 0.0;
                    double cx = ax + (bx - ax) * f, cy = ay + (by - ay) * f;
                    _gates.Add(System.Math.Atan2(cy - s.SquareY, cx - s.SquareX));
                }
            }
            if (_gates.Count == 0)
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                {
                    if (o == sid || !w.Settlements.Get(o).Inhabited) continue;
                    ref var t = ref w.Settlements.Get(o);
                    if (Vec3d.ArcDistance(s.Anchor, t.Anchor, P.Radius) > 6000) continue;
                    s.SphereToLocal(t.Anchor, P.Radius, out double ox, out double oy);
                    _gates.Add(System.Math.Atan2(oy - s.SquareY, ox - s.SquareX));
                }
            double gateHalf = 3.0 / r;   // (a gate some six metres wide)
            float cond = condition;
            Color timber = Color.Lerp(new Color(0.62f, 0.62f, 0.60f, 1f), Color.white, cond);
            for (int k = 0; k < set; k++)
            {
                double a = 2.0 * System.Math.PI * (k + 0.5) / stretches;
                bool gate = false;
                for (int g = 0; g < _gates.Count && !gate; g++)
                {
                    double d = System.Math.Abs(System.Math.IEEERemainder(a - _gates[g], 2.0 * System.Math.PI));
                    if (d < gateHalf) gate = true;
                }
                if (gate) continue;
                if (!Structures.RingDry(w, in s, r, a)) continue;   // (no posts in the sea or a lake: C1.6)
                double x = s.SquareX + r * System.Math.Cos(a), y = s.SquareY + r * System.Math.Sin(a);
                float yaw = Mathf.Atan2((float)(s.SquareX - x), (float)(s.SquareY - y)) * Mathf.Rad2Deg;
                // (old palisades lean a little as the posts rot)
                float lean = (1f - cond) * 0.25f * (((k * 13) % 5) - 2);
                _walls.Add(0, timber, Place(P, s, x, y, yaw, ex) * Matrix4x4.Rotate(Quaternion.Euler(lean * 10f, 0f, 0f)));
            }
        }

        Matrix4x4 Place(PlanetState P, in Settlement s, double x, double y, float yaw, float scale, float lift = 0f)
        {
            Vec3d d = s.LocalToSphere(x, y, P.Radius);
            Vector3 pos = PlanetGeo.Surface(P, d, lift);
            return Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * scale);
        }

        /// <summary>
        /// A model at (x, y) of a village's frame, its front (+z) turned to the bearing `face` (radians in that frame: C13.3, D2-118) - the village's
        /// own east and north carried onto the ground, so a house is drawn exactly as the simulation laid it out (`Footprints`). Before, a yaw read in
        /// the village's frame was turned about a world-fixed forward, and houses stood a few degrees to a few tens off their streets by where the
        /// village lay on the globe.
        /// </summary>
        static Matrix4x4 PlaceFaced(PlanetState P, in Settlement s, double x, double y, double face, float scale, float lift = 0f)
        {
            Vec3d d = s.LocalToSphere(x, y, P.Radius);
            Vector3 pos = PlanetGeo.Surface(P, d, lift);
            Vector3 up = pos.normalized;
            Vector3 f = Vector3.ProjectOnPlane(PlanetGeo.ToVector(s.East * DMath.Cos(face) + s.North * DMath.Sin(face)), up);
            if (f.sqrMagnitude < 1e-10f) f = Vector3.Cross(up, Vector3.right);
            return Matrix4x4.TRS(pos, Quaternion.LookRotation(f.normalized, up), Vector3.one * scale);
        }

        /// <summary>A point of a building's frame (mx along its front, mz toward it) in its village's frame.</summary>
        static void Local(double x, double y, double face, double mx, double mz, out double lx, out double ly)
        {
            double fx = DMath.Cos(face), fy = DMath.Sin(face);
            lx = x - fy * mx + fx * mz; ly = y + fx * mx + fy * mz;
        }

        /// <summary>
        /// What the view knows of each building's ground (D2-118), read again only when a village's buildings change: how much larger than life it
        /// may be drawn before it meets a neighbour (`Footprints.Room`), and where its yard has free ground - a kitchen garden behind or beside, a
        /// pen behind, a woodpile at its end or its back - so no garden, pen or woodpile is drawn into another house, a street or the green.
        /// </summary>
        sealed class Plot { public float Room; public bool Back, Pen, SideL, SideR, WoodEnd, WoodBack, CartEnd, OxEnd; public System.Collections.Generic.List<(double cx, double cz, double hx, double hz)> Beds; }
        readonly Dictionary<int, Plot> _plots = new Dictionary<int, Plot>();
        readonly Dictionary<int, long> _plotSig = new Dictionary<int, long>();
        readonly Dictionary<int, float> _plotAt = new Dictionary<int, float>();

        void ReadPlots(TC2.Core.Simulation.World w, int sid)
        {
            // (the ground changes when a building is laid out, rebuilt to another form or gone into the land, the market place widens or a street
            // is laid; all of a village again at most every two seconds - a building new since is read at once on its own)
            var bl = w.BuildingsOf(sid);
            long sig = bl.Count;
            for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); sig = sig * 1000003L + bl[i] * 31L + b.Form * 7L + b.Kind; }
            ref var s = ref w.Settlements.Get(sid);
            sig = sig * 31L + (long)(s.MarketR * 10f) + s.StreetN * 7L;
            if (_plotSig.TryGetValue(sid, out long old) && old == sig) return;
            bool all = !_plotAt.TryGetValue(sid, out float at) || Time.unscaledTime - at >= 2f;
            if (all) { _plotSig[sid] = sig; _plotAt[sid] = Time.unscaledTime; }
            for (int i = 0; i < bl.Count; i++)
            {
                if (Boats.IsBoat(w.Buildings.Get(bl[i]).Kind)) continue;
                if (!all && _plots.ContainsKey(bl[i])) continue;
                ReadPlot(w, sid, bl[i]);
            }
        }

        void ReadPlot(TC2.Core.Simulation.World w, int sid, int bid)
        {
            ref var b = ref w.Buildings.Get(bid);
            if (!_plots.TryGetValue(bid, out var pl)) { pl = new Plot(); _plots[bid] = pl; }
            pl.Room = (float)Footprints.Room(w, sid, bid, 2.5);
            if (!Structures.IsHome(b.Kind)) return;
            bool hut = b.Kind < Construction.House;
            int plan = hut ? 0 : Footprints.PlanOf(in b);
            float L = hut ? 4.6f : PlanL[plan], W = hut ? 4.6f : PlanW[plan];   // (a hut: 2.3 m round)
            // (the zones, in the house's frame: the garden 6 x 4.4 behind past a lean-to's depth, or beside; the pen behind; the woodpile at its end
            // or behind a back corner)
            pl.Back = Footprints.YardFree(w, sid, bid, 0.0, -W / 2.0 - 2.4 - 1.5 - 2.3, 3.8, 2.4);
            pl.Pen = Footprints.YardFree(w, sid, bid, 0.0, -W / 2.0 - 3.2 - 2.1, 4.4, 2.3);
            pl.SideL = Footprints.YardFree(w, sid, bid, -(L / 2.0 + 4.6), -W / 2.0 - 0.6, 3.9, 2.4);
            pl.SideR = Footprints.YardFree(w, sid, bid, L / 2.0 + 4.6, -W / 2.0 - 0.6, 3.9, 2.4);
            pl.WoodEnd = Footprints.YardFree(w, sid, bid, L / 2.0 + 1.6, 0.0, 1.0, 1.3);
            pl.WoodBack = Footprints.YardFree(w, sid, bid, L / 2.0 - 1.2, -W / 2.0 - 1.4, 1.3, 1.0);
            pl.CartEnd = Footprints.YardFree(w, sid, bid, -(L / 2.0 + 1.9), -0.3, 1.0, 1.7);   // (a handcart by the other end wall, its shafts along it: C4.2)
            pl.OxEnd = Footprints.YardFree(w, sid, bid, -(L / 2.0 + 2.1), -0.8, 1.2, 2.8);     // (an ox cart and its pole)
            if (pl.Beds == null) pl.Beds = new System.Collections.Generic.List<(double, double, double, double)>();
            pl.Beds.Clear();
            Gardens.Strips(w, sid, bid, pl.Beds);   // (its croft's strips, as the simulation measures them: C3.7a)
        }

        Plot PlotOf(int bid) => _plots.TryGetValue(bid, out var p) ? p : null;

        void RebuildPlaces(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            for (int i = 0; i < _homes.Length; i++) _homes[i].Clear();
            for (int i = 0; i < _frames.Length; i++) _frames[i].Clear();
            _chimneys.Clear(); _plinths.Clear(); _outshuts.Clear(); _pens.Clear(); _stalls.Clear(); _gardens.Clear(); _gardensBare.Clear(); _tenters.Clear(); _clayPits.Clear(); _jars.Clear(); _ware.Clear(); _parked.Clear(); _parkedOx.Clear();
            _stonePiles.Clear();
            for (int i = 0; i < _works.Length; i++) _works[i]?.Clear();
            _hay.Clear(); _huts.Clear(); _tents.Clear(); _ruins.Clear(); _beached.Clear(); _beachedP.Clear(); _walls.Clear(); _woodpiles.Clear();
            _smoke.Clear(); _lights.Clear(); _watch.Clear();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                Vector3 center = PlanetGeo.Surface(P, s.Anchor);
                double gone = s.Inhabited ? 0.0 : (w.Now - s.Abandoned) / (double)SimTime.Year;
                if (!s.Inhabited && (gone > 600 || (Rig != null && Rig.Cam != null && (center - Rig.Cam.transform.position).sqrMagnitude > 6000f * 6000f))) continue;
                float ex = Exaggeration(center, 1.6f);
                // after dark, a house whose people are home and awake shows its windows
                Vector3 toSun = Rig != null && Rig.Sun != null ? -Rig.Sun.transform.forward : Vector3.up;
                bool dark = Vector3.Dot(PlanetGeo.ToVector(s.Anchor), toSun) < 0.06f;
                double tOut = w.PlanetSys.DayMean(s.Cell, PlanetSystems.Season(w.Now));   // the day the hearths burn against
                // its buildings (D2-059): lived in, standing empty, fallen in - an abandoned village's too, as they decay
                // (and how many days its woodstore holds against what its hearths burn: the woodpiles by the homes, D2-081)
                double woodDays = s.FuelDay > 1e-6f ? s.Woodstore / s.FuelDay : (s.Woodstore > 0 ? 30.0 : 0.0);
                int litHomes = 0;
                ReadPlots(w, sid);   // (what each building's ground leaves it: C13.3)
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    // its works are structures too (C1.1): wells and granaries by the table, standing, being raised or fallen
                    byte bk = w.Buildings.Get(bl[i]).Kind;
                    if (Boats.IsBoat(bk)) { DrawBoat(w, P, in s, bl[i], ex); continue; }   // (a household's dugout at the landing: C9.1)
                    if (!Structures.IsHome(bk)) { DrawWork(w, P, in s, bl[i], ex); continue; }
                    if (DrawHome(w, P, in s, bl[i], ex, dark, tOut, woodDays)) litHomes++;
                }
                // households without a home camp on their lot (not those on the road to another village), a fire smoking by the tent
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var h = ref w.Households.Get(hhs[i]);
                    if (h.Building != 0 || h.Party != 0) continue;
                    double tface = Footprints.FaceAt(in s, h.HomeX, h.HomeY, hhs[i]);   // (as the home to come will face: C13.3)
                    float age = 0.88f + 0.2f * (float)KeyedRng.Unit(KeyedRng.Mix((ulong)hhs[i] + 77));
                    bool awake = SomeoneAwakeHome(w, hhs[i], out bool cooking);
                    bool lit = dark && awake;
                    if (lit) litHomes++;
                    var tm = PlaceFaced(P, s, h.HomeX, h.HomeY, tface, Mathf.Min(ex, 1.3f));
                    _tents.Add((int)(age * 10) + (lit ? 100 : 0), new Color(age, age * 0.97f, age * 0.92f, 1f), tm, lit ? -1f : 0f);
                    if (awake && (cooking || h.Indoor - Hearth.Indoors(tOut, h.Shelter, 0.0) > 0.5))
                    {
                        Vector3 top = tm.MultiplyPoint3x4(new Vector3(0f, 2.7f, 0f));
                        _smoke.Add(new Vector4(top.x, top.y, top.z, cooking ? 0.8f : 0.45f));
                    }
                }
                // after dark, the village's lit homes seen from high above; the watch fire by its store
                if (s.Inhabited && dark && litHomes > 0) _lights.Add(new Vector4(center.x, center.y, center.z, litHomes));
                if (s.Inhabited && dark && s.Watcher != 0 && (s.Works & Construction.Granary) != 0)
                {
                    Construction.GranarySpot(in s, out double wx, out double wy);
                    Vector3 fireAt = PlanetGeo.Surface(P, s.LocalToSphere(wx + 4.5, wy - 3.0, P.Radius));
                    _watch.Add(new Vector4(fireAt.x, fireAt.y, fireAt.z, 1f));
                }
                // its palisade (D2-074): standing (an abandoned village's too, as it rots), or as far as its people have set it
                if (s.Wall == 1 || s.Wall == 2 || s.OldR > 0f) DrawWall(w, P, in s, sid, ex);
                if (!s.Inhabited) continue;   // (an abandoned village's well and granary decay as structures: C1.1)
                // the stone in its store, stacked by the store: a pile for every ~40 loads, up to four (C1.3)
                // (round the first granary, on ground no building, stall or street takes: C13.3 - the haystacks stood 22-35 m out, where homes stand)
                Construction.GranarySpot(in s, out double gsx, out double gsy);
                int spot = 0;
                if (s.Stone >= 5)
                {
                    int piles = Mathf.Clamp(Mathf.CeilToInt((float)(s.Stone / 40.0)), 1, 4);
                    for (int k = 0; k < piles; k++)
                        if (StoreSpot(w, in s, sid, gsx, gsy, ref spot, out double px, out double py)) _stonePiles.Add(0, Color.white, PlaceFaced(P, s, px, py, 0.35 * k, Mathf.Min(ex, 1.3f) * 1.1f));
                }
                // haystacks once the harvest is in
                float fill = Mathf.Clamp01((float)(s.Granary / System.Math.Max(1.0, s.Population * 30.0)));
                if (s.Crop == CropStage.Harvested || s.Crop == CropStage.Fallow)
                    for (int k = 0; k < Mathf.RoundToInt(fill * 4); k++)
                        if (StoreSpot(w, in s, sid, gsx, gsy, ref spot, out double hx, out double hy)) _hay.Add(0, Color.white, PlaceFaced(P, s, hx, hy, 0.7 * k, Mathf.Min(ex, 1.3f) * (0.8f + 0.1f * k)));
            }
        }

        /// <summary>The next free spot round the village's store for a stack (stone, hay): on a ring 5.5 m round the first granary, from the side
        /// toward the square, clear of every building's ground, the market's stalls and the streets.</summary>
        static bool StoreSpot(TC2.Core.Simulation.World w, in Settlement s, int sid, double gx, double gy, ref int spot, out double x, out double y)
        {
            double a0 = DMath.Atan2(s.SquareY - gy, s.SquareX - gx);
            var bl = w.BuildingsOf(sid);
            for (; spot < 12; spot++)
            {
                double a = a0 + ((spot & 1) == 0 ? 1 : -1) * ((spot + 1) >> 1) * 0.55;
                x = gx + 5.5 * DMath.Cos(a); y = gy + 5.5 * DMath.Sin(a);
                bool clear = !Streets.On(in s, x, y, Streets.Width);
                for (int j = 0; j < bl.Count && clear; j++)
                {
                    ref var o = ref w.Buildings.Get(bl[j]);
                    if (Homes.RuinLeft(in o, w.Now) <= 0 || Boats.IsBoat(o.Kind)) continue;
                    double ox = o.X - x, oy = o.Y - y;
                    double need = (Structures.IsHome(o.Kind) ? Footprints.Reach(Footprints.PlanOf(in o)) : o.Kind == Structures.Market ? 11.0 : Footprints.WorkR(o.Kind)) + 1.6;
                    if (ox * ox + oy * oy < need * need) clear = false;
                }
                if (clear) { spot++; return true; }
            }
            x = gx; y = gy;
            return false;
        }

        /// <summary>
        /// A village work (C1.1): a well or a granary where its people raised it - while being raised a pale frame rising with the hours put
        /// in, standing as it was built and weathering with its condition, fallen a low ruin (a well's stone ring outlasts the rest, D2-052).
        /// </summary>
        void DrawWork(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int bid, float ex)
        {
            ref var b = ref w.Buildings.Get(bid);
            var k = Structures.Of(b.Kind);
            double face = Footprints.FaceOf(in s, in b);   // (as it was laid out: C13.3)
            var plot = PlotOf(bid);
            if (plot != null) ex = Mathf.Min(ex, plot.Room);
            var inst = b.Kind < _works.Length && _works[b.Kind] != null ? _works[b.Kind] : _granaries;
            var m = PlaceFaced(P, s, b.X, b.Y, face, ex);
            if (b.Kind == Structures.BoatShed && s.LandAt != 0)
            {
                // its mouth down the beach, to the water (C9.2b; the kit's mouth at +y comes into the game at -z)
                Vector3 at = PlanetGeo.Surface(P, s.LocalToSphere(b.X, b.Y, P.Radius)), up = at.normalized;
                Vector3 down = PlanetGeo.Surface(P, s.LocalToSphere(b.X - s.LandUx, b.Y - s.LandUy, P.Radius)) - at;
                down -= up * Vector3.Dot(down, up);
                if (down.sqrMagnitude > 1e-6f) m = Matrix4x4.TRS(at, Quaternion.LookRotation(-down.normalized, up), Vector3.one * ex);
            }
            if (b.Done == 0)
            {
                float up = Mathf.Clamp01((float)(b.Hours / System.Math.Max(1.0, k.Hours)));
                if (up <= 0.01f) return;
                inst.Add(2, new Color(1.08f, 1.0f, 0.86f, 1f), m * Matrix4x4.Scale(new Vector3(1f, 0.15f + 0.85f * up, 1f)));   // new timber
                return;
            }
            Color moss = new Color(0.55f, 0.62f, 0.45f, 1f);
            if (Homes.Fallen(in b, w.Now))
            {
                float c = (float)Homes.RuinLeft(in b, w.Now);
                if (c <= 0.03f) return;
                if (b.Kind == Structures.Well) inst.Add(1, Color.Lerp(moss, new Color(0.75f, 0.80f, 0.70f, 1f), c), m * Matrix4x4.Scale(new Vector3(1f, 0.4f, 1f)));
                else _ruins.Add((int)(c * 4), Color.Lerp(moss, Color.white, c), PlaceFaced(P, s, b.X, b.Y, face, 1f) * Matrix4x4.Scale(new Vector3(ex * 0.7f, ex * 0.7f * (0.25f + 0.75f * c), ex * 0.7f)));
                return;
            }
            float cond = (float)Structures.Condition(in b, w.Now);
            if (b.Kind == Structures.Smithy)
            {
                // smoke from its chimney while a smith of the village works at it (C1.4: the simulation's own work, never for show)
                var res = w.Residents(b.Settlement);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var q = ref w.People.Get(res[i]);
                    if (q.Act != ActivityKind.Smith || q.At != b.Settlement || q.Step == StepKind.Travel) continue;
                    if (q.WhyArg != 0 && q.WhyArg != bid) continue;   // (at another of its smithies: C1.4b)
                    Vector3 top = (m * Matrix4x4.Scale(new Vector3(1.2f, 1.2f, 1.2f))).MultiplyPoint3x4(_forgeTop);   // (the smithy is drawn 1.2 times life)
                    _smoke.Add(new Vector4(top.x, top.y, top.z, 1.2f));
                    break;
                }
            }
            if (b.Kind == Structures.Quarry)
            {
                // the face cut back as the stone is taken: larger with every thousand loads (C1.3)
                float cut = 0.55f + 0.45f * Mathf.Clamp01(b.Stock / 1500f) + 0.25f * Mathf.Clamp01((b.Stock - 1500f) / 6000f);
                m = m * Matrix4x4.Scale(new Vector3(cut, cut, cut));
            }
            if (b.Kind == Structures.CharHearth)
            {
                // smoke from the clamp's vent while it smoulders - for three days after its wood was fired (C5.2a: the simulation's own state)
                m = m * Matrix4x4.Scale(new Vector3(1.4f, 1.4f, 1.4f));   // (drawn larger than life to read at the village view, as the mine)
                if (s.ClampUntil > w.Now && b.Left == 0)
                {
                    Vector3 top = m.MultiplyPoint3x4(new Vector3(0f, 1.5f, 0f));
                    _smoke.Add(new Vector4(top.x, top.y, top.z, 1.6f));
                }
            }
            if (b.Kind == Structures.BogPits) m = m * Matrix4x4.Scale(new Vector3(1.4f, 1.4f, 1.4f));   // (larger than life, as the mine: C5.2b)
            if (b.Kind == Structures.Kiln) m = m * Matrix4x4.Scale(new Vector3(1.15f, 1.15f, 1.15f));   // (a little larger than life to read, as the smithy: C3.2)
            if (b.Kind == Structures.Kiln && b.Left == 0) Kiln(w, in s, in b, m);
            if ((b.Kind == Structures.Granary || b.Kind == Structures.Barn) && b.Left == 0) Jars(in s, in b, m);
            if (b.Kind == Structures.Mine)
            {
                // the spoil heap and the workings grow with the ore dug (C5.1: kg carried out of the ground)
                // (drawn half as large again as life: a 7 m pit is lost on its broken ground at the views people read a village from)
                float dug = 1.5f * (0.7f + 0.3f * Mathf.Clamp01(b.Stock / 3000f) + 0.25f * Mathf.Clamp01((b.Stock - 3000f) / 15000f));
                m = m * Matrix4x4.Scale(new Vector3(dug, dug, dug));
            }
            Color look = Color.Lerp(moss, Color.white, 0.45f + 0.55f * cond);
            if (Architecture.Has(b.Form) && (b.Kind == Structures.Barn || b.Kind == Structures.Granary || b.Kind == Structures.Smithy || b.Kind == Structures.Workshop || b.Kind == Structures.Kiln))
            {
                // (D2-114) roofed with what its village roofs with, the smithy's walls of its village's walls
                Color roof = Color.Lerp(moss, RoofColour(w, in b, Architecture.CoverOf(b.Form), bid), 0.45f + 0.55f * cond);
                Color wallC = Color.Lerp(moss, WallTint(b.Form), 0.45f + 0.55f * cond);
                if (b.Kind == Structures.Smithy)
                {
                    // a forge's walls blacken with the smoke of the hours worked at it (b.Use); drawn a little larger than life to read
                    float soot = 0.55f * Mathf.Clamp01(b.Use / 800f);
                    wallC = Color.Lerp(wallC, new Color(0.30f, 0.27f, 0.25f, 1f), soot);
                    roof = Color.Lerp(roof, new Color(0.28f, 0.26f, 0.25f, 1f), soot * 0.6f);
                    m = m * Matrix4x4.Scale(new Vector3(1.2f, 1.2f, 1.2f));
                }
                inst.Add(0, roof, wallC, m);
                if (b.Kind == Structures.Workshop && b.Left == 0) Tenters(in s, bid, m, cond);
                return;
            }
            inst.Add(0, look, m);
            if (b.Kind == Structures.Market) Stalls(w, P, in s, bid, ex);
        }

        /// <summary>
        /// The kiln's place (C3.2): smoke from its vent while a potter of the village works at it (the simulation's own work, never for show); the clay
        /// pit dug behind it, wider as the ware fired there grows (kg made: the clay dug); ware set out in straw while the village holds more than it
        /// keeps - ware to sell.
        /// </summary>
        void Kiln(TC2.Core.Simulation.World w, in Settlement s, in Building b, Matrix4x4 m)
        {
            var res = w.Residents(b.Settlement);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (q.Act != ActivityKind.Pot || q.At != b.Settlement || q.Step == StepKind.Travel || w.Now < q.TA) continue;   // (there, not on the way)
                Vector3 top = m.MultiplyPoint3x4(_kilnTop);
                _smoke.Add(new Vector4(top.x, top.y, top.z, 1.4f));
                break;
            }
            float dug = 0.75f + 0.35f * Mathf.Clamp01(b.Stock / 20000f) + 0.3f * Mathf.Clamp01((b.Stock - 20000f) / 150000f);
            _clayPits.Add(0, Color.white, m * Matrix4x4.Translate(new Vector3(0f, 0f, -7.2f)) * Matrix4x4.Scale(new Vector3(dug, dug, dug)));   // (behind it: the kit's +y)
            if (s.Pots > Pottery.Kept * s.Population + 20.0)
                _ware.Add(0, Color.white, m * Matrix4x4.Translate(new Vector3(-3.4f, 0f, 3.9f)) * Matrix4x4.Rotate(Quaternion.Euler(0f, 20f, 0f)));
        }

        /// <summary>Storage jars standing by a granary or barn while its village's jars keep a good part of its grain sealed (C3.2: `Pottery.Sealed`).</summary>
        void Jars(in Settlement s, in Building b, Matrix4x4 m)
        {
            if (s.Jars < 25.0 || Pottery.Sealed(in s) < 0.3) return;
            Vector3 at = b.Kind == Structures.Barn ? new Vector3(8.6f, 0f, 1.6f) : new Vector3(2.6f, 0f, 0.5f);
            _jars.Add(0, Color.white, m * Matrix4x4.Translate(at));
        }

        static readonly Color[] ClothDyes = { new Color(0.42f, 0.52f, 0.78f, 1f), new Color(0.86f, 0.46f, 0.36f, 1f), new Color(1.0f, 0.86f, 0.46f, 1f), new Color(1.0f, 0.98f, 0.92f, 1f) };

        /// <summary>
        /// Cloth on the tenters behind a weaver's shed (C3.1): one length while its village has wares in store, a second when it holds a store
        /// beyond what its makers keep (wares to sell) - the simulation's own wares, never for show; each length its dye.
        /// </summary>
        void Tenters(in Settlement s, int bid, Matrix4x4 m, float cond)
        {
            if (s.Wares < 1.0) return;
            int rows = s.Wares > Weaving.Target(in s) * 0.8 ? 2 : 1;
            // (the first at the shed's free gable end - the vat's end, the lean-to's is the other - seen from most sides; the second behind it)
            _tenters.Add(0, ClothDyes[bid % ClothDyes.Length], m * Matrix4x4.Translate(new Vector3(5.9f, 0f, -0.7f)) * Matrix4x4.Rotate(Quaternion.Euler(0f, 90f, 0f)));
            if (rows > 1) _tenters.Add(0, ClothDyes[(bid + 3) % ClothDyes.Length], m * Matrix4x4.Translate(new Vector3(0f, 0f, -4.4f)));   // (behind the shed: the kit's +y)
        }

        /// <summary>
        /// The market's stalls (C1.6, C13.3): as many as its traders keep coming (traders a year), each awning its own dye - set out in a ring round
        /// the cross and, in a wide market place (`Settlement.MarketR`), another along its edge, on ground no building stands on, facing in.
        /// </summary>
        void Stalls(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int bid, float ex)
        {
            ref var b = ref w.Buildings.Get(bid);
            int stalls = Mathf.Clamp(Mathf.RoundToInt(s.TradeIn / 1.5f), 2, 16);
            double place = Footprints.Green(in s);
            int sid = b.Settlement, put = 0;
            var bl = w.BuildingsOf(sid);
            for (int ring = 0; ring < 2 && put < stalls; ring++)
            {
                double r = ring == 0 ? 8.5 : place - 4.0;
                if (ring == 1 && r < 15.0) break;
                int slots = (int)(2.0 * DMath.PI * r / 4.2);
                for (int i = 0; i < slots && put < stalls; i++)
                {
                    double a = (i + 0.5) / slots * 2.0 * DMath.PI + (bid % 7) * 0.13 + ring * 0.21;
                    double cx = ring == 0 ? b.X : s.SquareX, cy = ring == 0 ? b.Y : s.SquareY;
                    double x = cx + r * DMath.Cos(a), y = cy + r * DMath.Sin(a);
                    bool clear = true;
                    for (int j = 0; j < bl.Count && clear; j++)
                    {
                        if (bl[j] == bid) continue;
                        ref var o = ref w.Buildings.Get(bl[j]);
                        if (Homes.RuinLeft(in o, w.Now) <= 0) continue;
                        double ox = o.X - x, oy = o.Y - y;
                        double need = (Structures.IsHome(o.Kind) ? Footprints.Reach(Footprints.PlanOf(in o)) : Footprints.WorkR(o.Kind)) + 2.0;
                        if (ox * ox + oy * oy < need * need) clear = false;
                    }
                    if (!clear || Streets.On(in s, x, y, Streets.Width * 0.5)) continue;
                    double facing = DMath.Atan2(b.Y - y, b.X - x);   // (its front to the cross)
                    _stalls.Add(0, Dyes[(bid + put * 3) % Dyes.Length], PlaceFaced(P, s, x, y, facing, ex));
                    put++;
                }
            }
        }

        static readonly Color BedDug = new Color(0.42f, 0.30f, 0.20f, 1f), BedRough = new Color(0.55f, 0.57f, 0.38f, 1f);

        /// <summary>
        /// A household's garden (C3.7a): beds on its croft's own strips as the simulation measures them - only where it has one (none in a town's
        /// built-up core) - each column or row tiled with beds of the kit's size (6 x 4.4 m), the last ones shortened to fit; behind a house, the beds
        /// within its pen's ground give way to the pen. Each bed as the croft is this season (`Gardens.Shown`): dug in the order its gardener works
        /// (its middle column behind first) - fresh earth in the sowing, in leaf from the growing on, cleared as it is gathered - and a weedy rough
        /// where nobody dug it this year.
        /// </summary>
        void GardenBeds(TC2.Core.Simulation.World w, in Settlement s, int hid, Plot plot, Matrix4x4 m, bool pen, float penZ)
        {
            if (plot == null || plot.Beds == null || plot.Beds.Count == 0 || hid == 0 || !w.Households.Exists(hid)) return;
            ref var hh = ref w.Households.Get(hid);
            if (hh.Garden <= 0f) return;
            int doy = SimTime.DayOfYear(w.Now + PlanetState.LocalOffset(s.Anchor));
            bool leaf = Gardens.Shown(in hh, doy, s.CropGrowth, out double dug, out double cleared);
            for (int pass = 0, n = 0, k = 0, nDug = 0, nCleared = 0; pass < 2; pass++)
            {
                if (pass == 1) { nDug = Mathf.RoundToInt((float)(dug * n)); nCleared = Mathf.RoundToInt((float)(cleared * n)); }
                for (int gi = 0; gi < plot.Beds.Count; gi++)
                {
                    var bd = plot.Beds[gi];
                    float bw = (float)(2.0 * bd.hx), bl = (float)(2.0 * bd.hz);
                    int nx = Mathf.Max(1, Mathf.RoundToInt(bw / 6f)), nz = Mathf.Max(1, Mathf.RoundToInt(bl / 4.4f));
                    float tw = bw / nx, tl = bl / nz;
                    // (from the house outward: the gardener digs from the back wall)
                    for (int iz = nz - 1; iz >= 0; iz--)
                        for (int ix = 0; ix < nx; ix++)
                        {
                            float tx = (float)bd.cx - bw * 0.5f + tw * (ix + 0.5f), tz = (float)bd.cz - bl * 0.5f + tl * (iz + 0.5f);
                            if (pen && Mathf.Abs(tx) < 2.2f + tw * 0.5f && Mathf.Abs(tz - penZ) < 2.3f + tl * 0.5f) continue;
                            if (pass == 0) { n++; continue; }
                            var tm = m * Matrix4x4.TRS(new Vector3(tx, 0f, tz), Quaternion.identity, new Vector3(tw * 0.92f / 6f, 1f, tl * 0.92f / 4.4f));
                            if (k >= nDug) _gardensBare.Add(0, BedRough, tm);
                            else if (leaf && k >= nCleared) _gardens.Add(0, Color.white, tm);
                            else _gardensBare.Add(0, BedDug, tm);
                            k++;
                        }
                }
            }
        }

        /// <summary>
        /// One building (D2-059): lived in - its windows lit after dark while its people are awake, smoke while one of them cooks
        /// or the hearth burns against the cold (D2-056); standing empty - greying and greening as it decays; fallen in - a ruin
        /// whose walls sink and green over as the land takes it back (D2-052). It faces the village square; its look is the one
        /// it was raised with.
        /// </summary>
        bool DrawHome(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int bid, float ex, bool dark, double tOut, double woodDays)
        {
            ref var b = ref w.Buildings.Get(bid);
            // it faces its street where it fronts one (C13.1), square to it; else the square, a little askew - as it was laid out (C13.3: `Building.Face`);
            // drawn larger far off only as far as its neighbours leave it room (D2-118)
            double face = Footprints.FaceOf(in s, in b);
            var plot = PlotOf(bid);
            if (plot != null) ex = Mathf.Min(ex, plot.Room);
            float age = 0.88f + 0.2f * (float)KeyedRng.Unit(KeyedRng.Mix((ulong)b.Style + 77));   // weathering of roofs and walls
            if (Homes.Fallen(in b, w.Now) || (b.Household == 0 && Homes.Condition(in b, w.Now) < Homes.Habitable))
            {
                // past mending its roof is gone (the walls stand roofless), then it falls in and sinks (RuinLeft: 1 while it stands)
                float c = (float)Homes.RuinLeft(in b, w.Now);
                if (c <= 0.03f) return false;
                var mr = PlaceFaced(P, s, b.X, b.Y, face, 1f);
                float size = ex * (b.Kind >= Construction.House ? 1f : 0.6f);
                mr = mr * Matrix4x4.Scale(new Vector3(size, size * (0.25f + 0.75f * c), size));   // the walls fall lower
                Color moss = new Color(0.50f, 0.60f, 0.40f, 1f);
                _ruins.Add((int)(c * 4), Color.Lerp(moss, Color.white, c), mr);
                return false;
            }
            bool lived = b.Household != 0 && w.Households.Exists(b.Household);
            bool cooking = false;
            bool awake = lived && SomeoneAwakeHome(w, b.Household, out cooking);
            bool lit = dark && awake;
            Color tint = new Color(age, age * 0.98f, age * 0.94f, 1f);
            if (!lived) tint = Color.Lerp(new Color(0.55f, 0.58f, 0.48f, 1f), tint, 0.45f + 0.55f * (float)Homes.Condition(in b, w.Now));   // an empty home weathers
            int key = (int)(age * 10) + (lit ? 100 : 0) + (lived ? 0 : 200);
            var m = PlaceFaced(P, s, b.X, b.Y, face, ex);
            bool hut = b.Kind < Construction.House;
            int form = Architecture.Has(b.Form) ? b.Form : Architecture.Pack(Architecture.Cottage, Architecture.Gable, Architecture.Straw, Architecture.Daub, false, 0);
            int plan = Architecture.PlanOf(form);
            Vector3 smokeAt = Vector3.zero;
            if (ShowGround)
            {
                int gp = Footprints.PlanOf(in b);
                for (int r = 0; r < Footprints.Count(gp); r++)
                {
                    Footprints.Rect(gp, r, out double gcx, out double gcz, out double ghx, out double ghz);
                    _plinths.Add(0, new Color(1f, 0.1f, 0.1f, 1f), m * Matrix4x4.TRS(new Vector3((float)gcx, 0f, (float)gcz), Quaternion.identity, new Vector3(2f * (float)ghx, 0.3f, 2f * (float)ghz)));
                }
            }
            if (hut) _huts.Add(300 + key, tint, m, lit ? -1f : 0f);
            else
            {
                // (D2-114) the home as its builders built it: its plan and roof form, its cover weathered by its years, its walls, its framing,
                // its footing, its chimney; a lean-to where its household has outgrown it, a pen where it keeps beasts
                float shade = 0.95f + 0.1f * (age - 0.88f) / 0.2f;   // (a little lighter or darker house by house)
                Color roof = RoofColour(w, in b, Architecture.CoverOf(form), bid) * new Color(shade, shade, shade, 1f);
                Color walls = WallTint(form);
                if (!lived) { float c0 = (float)Homes.Condition(in b, w.Now); roof = Color.Lerp(new Color(0.55f, 0.58f, 0.48f, 1f), roof, 0.45f + 0.55f * c0); walls = Color.Lerp(new Color(0.62f, 0.64f, 0.56f, 1f), walls, 0.45f + 0.55f * c0); }
                roof.a = tint.a;
                _homes[plan * RoofNames.Length + Architecture.RoofOf(form)].Add(key, roof, walls, m, lit ? -1f : 0f);
                if (Architecture.WallsOf(form) == Architecture.Framed) _frames[plan].Add(0, tint, m);
                float L = PlanL[plan], W = PlanW[plan];
                if (b.Mat >= 1)
                {
                    // on stone footings (C1.3): a course of stone round its walls (and its wing's)
                    _plinths.Add(0, Color.white, m * Matrix4x4.Scale(new Vector3(L + 0.3f, 1f, W + 0.3f)));
                    if (plan == Architecture.Hall) _plinths.Add(0, Color.white, m * Matrix4x4.TRS(new Vector3((float)Footprints.WingSide * (L / 2f + 2.3f), 0f, 0f), Quaternion.identity, new Vector3(5.5f, 1f, 8.9f)));   // (its wing's side as the model comes in: D2-118)
                }
                var ch = ChimneyAt[plan];
                if (Architecture.Chimney(form))
                {
                    _chimneys.Add(0, Color.white, m * Matrix4x4.TRS(new Vector3(ch.x, 0f, -ch.y), Quaternion.identity, new Vector3(1f, ch.z, 1f)));
                    smokeAt = m.MultiplyPoint3x4(new Vector3(ch.x, ch.z + 0.15f, -ch.y));
                }
                else smokeAt = m.MultiplyPoint3x4(new Vector3(plan == Architecture.Long ? -L * 0.25f : 0f, PlanRidge[plan] + 0.1f, 0f));   // (a smoke-hole in the roof)
                if (lived)
                {
                    int n = w.Members(b.Household).Count;
                    if (n > PlanRoom[plan]) _outshuts.Add(0, roof, walls, m * Matrix4x4.TRS(new Vector3(0f, 0f, -W / 2f), Quaternion.identity, new Vector3(L * 0.55f, 1f, 1f)));
                    // its pen (D2-114), only on ground its neighbours, the streets and the green leave free (D2-118), behind; and its garden (C3.7a): beds
                    // on the croft's own strips as the simulation measures them - only where the household has one (none in a town's built-up core), the
                    // beds behind giving way to the pen
                    bool beasts = HasBeasts(w, b.Household);
                    bool pen = beasts && plot != null && plot.Pen;
                    if (pen) Pen(m, W, plan == Architecture.Hall ? 4 : 3);
                    GardenBeds(w, in s, b.Household, plot, m, pen, -W / 2f - 3.2f - 2.1f);
                }
            }
            if (hut && lived) GardenBeds(w, in s, b.Household, plot, m, false, 0f);   // (a hut's croft is dug as a house's: C3.7a)
            if (!lived) return false;
            // its cart in the yard (C4.2: the household's own, while nobody is out with it) - by the other end wall from the woodpile
            if (plot != null && w.Households.Exists(b.Household))
            {
                ref var hc = ref w.Households.Get(b.Household);
                if (hc.CartState == Carts.Made && !Carts.WornOut(in hc, w.Now) && !CartOut(w, b.Household))
                {
                    bool ox = hc.CartKind == Carts.Ox;
                    if (ox ? plot.OxEnd : plot.CartEnd)
                    {
                        double L1 = hut ? 4.6 : PlanL[plan];
                        Local(b.X, b.Y, face, -(L1 / 2.0 + (ox ? 2.1 : 1.9)) * ex, (ox ? -0.8 : -0.3) * ex, out double cx, out double cy);
                        (ox ? _parkedOx : _parked).Add(0, Color.white, PlaceFaced(P, s, cx, cy, face, ex));
                    }
                }
            }
            // its share of the village's woodstore stacked by the wall (D2-081): none when the store is all but empty, a bigger pile the
            // more days of the hearths' wood the village holds - by its end wall, or behind a back corner where a neighbour stands close (D2-118)
            if (woodDays >= 2.0 && plot != null && (plot.WoodEnd || plot.WoodBack))
            {
                float pile = Mathf.Clamp(0.5f + (float)woodDays / 50f, 0.5f, 1.25f);
                double L0 = hut ? 4.6 : PlanL[plan], W0 = hut ? 4.6 : PlanW[plan];
                double mx = plot.WoodEnd ? L0 / 2.0 + 1.6 : L0 / 2.0 - 1.2, mz = plot.WoodEnd ? 0.0 : -W0 / 2.0 - 1.4;
                Local(b.X, b.Y, face, mx * ex, mz * ex, out double wx, out double wy);
                _woodpiles.Add(0, Color.white, PlaceFaced(P, s, wx, wy, face + (plot.WoodEnd ? 0.0 : DMath.HalfPi), ex * pile));
            }
            if (!awake) return lit;
            // a fire in the hearth: while someone cooks, and all day while it burns against the cold (what today's fire adds to the
            // home's warmth, D2-056) - from the chimney, or a hut's thatch at its peak (D2-081)
            ref var h = ref w.Households.Get(b.Household);
            double heat = h.Indoor - Hearth.Indoors(tOut, h.Shelter, 0.0);
            if (cooking || heat > 0.5)
            {
                Vector3 top = hut ? m.MultiplyPoint3x4(new Vector3(0f, 4.5f, 0f)) : smokeAt;
                _smoke.Add(new Vector4(top.x, top.y, top.z, cooking ? 1f : (float)(0.35 + 0.4 * System.Math.Min(1.0, heat / 8.0))));
            }
            return lit;
        }

        /// <summary>
        /// A roof's cover as it stands now (D2-114): its material's colour, weathered by the years since it was last laid. A lived-in house (a
        /// work in use) is kept - its thatch re-laid every ~28 years (straw) or ~45 (reed), its shingles every ~40, each house on its own turn -
        /// so a village shows fresh golden thatch beside greyed and mossy; tiles and slates are not re-laid and darken and gather lichen over a
        /// century; a building left empty weathers from the day it was left. Tiles and slates come in batches (a kiln's firing, a quarry's bed).
        /// </summary>
        static Color RoofColour(TC2.Core.Simulation.World w, in Building b, int cover, int bid)
        {
            float years = (float)((w.Now - b.Built) / (double)SimTime.Year);
            float t = years;   // years since the cover was laid
            ulong h = KeyedRng.Mix((ulong)bid * 2654435761UL + 13UL);
            float period = cover == Architecture.Straw ? 28f : cover == Architecture.Reed ? 45f : cover == Architecture.Shingle ? 40f : 0f;
            if (period > 0f)
            {
                float first = period * (0.7f + 0.6f * (float)((h >> 8) % 1000UL) / 1000f);   // (its first re-laying, then every period)
                float kept = b.Left != 0 ? (float)((b.Left - b.Built) / (double)SimTime.Year) : years;
                float laid = kept < first ? kept : (kept - first) % period;
                t = laid + (years - kept);
            }
            Color c;
            switch (cover)
            {
                case Architecture.Straw:
                    c = Color.Lerp(new Color(0.81f, 0.69f, 0.43f), new Color(0.74f, 0.62f, 0.40f), Mathf.Clamp01(t / 5f));
                    c = Color.Lerp(c, new Color(0.58f, 0.51f, 0.38f), Mathf.Clamp01((t - 5f) / 13f));
                    c = Color.Lerp(c, new Color(0.47f, 0.46f, 0.35f), Mathf.Clamp01((t - 18f) / 16f));   // (greyed, a green bloom of moss)
                    break;
                case Architecture.Reed:
                    c = Color.Lerp(new Color(0.74f, 0.63f, 0.44f), new Color(0.57f, 0.51f, 0.40f), Mathf.Clamp01(t / 18f));
                    c = Color.Lerp(c, new Color(0.46f, 0.44f, 0.37f), Mathf.Clamp01((t - 18f) / 25f));
                    break;
                case Architecture.Shingle:
                    c = Color.Lerp(new Color(0.66f, 0.49f, 0.33f), new Color(0.58f, 0.56f, 0.53f), Mathf.Clamp01(t / 14f));   // (oak silvers)
                    c = Color.Lerp(c, new Color(0.47f, 0.47f, 0.46f), Mathf.Clamp01((t - 14f) / 26f));
                    break;
                case Architecture.Tile:
                {
                    int batch = (int)((h >> 20) % 3UL);
                    c = batch == 0 ? new Color(0.66f, 0.36f, 0.25f) : batch == 1 ? new Color(0.59f, 0.31f, 0.22f) : new Color(0.70f, 0.45f, 0.31f);
                    c = Color.Lerp(c, new Color(0.47f, 0.35f, 0.27f), 0.45f * Mathf.Clamp01(t / 120f));   // (darkened, lichen)
                    break;
                }
                default:
                {
                    int batch = (int)((h >> 20) % 3UL);
                    c = batch == 0 ? new Color(0.40f, 0.42f, 0.46f) : batch == 1 ? new Color(0.35f, 0.36f, 0.39f) : new Color(0.46f, 0.45f, 0.45f);
                    c = Color.Lerp(c, new Color(0.47f, 0.47f, 0.40f), 0.3f * Mathf.Clamp01(t / 150f));
                    break;
                }
            }
            return new Color(c.r / 0.86f, c.g / 0.86f, c.b / 0.86f, 1f);   // (the kit paints covers 0.86)
        }

        /// <summary>A building's walls by their material and its tradition's shade (D2-114).</summary>
        static Color WallTint(int form)
        {
            var set = WallCol[Architecture.WallsOf(form)];
            Color c = set[Architecture.PaletteOf(form) % set.Length];
            return new Color(c.r / 0.90f, c.g / 0.90f, c.b / 0.90f, 1f);   // (the kit paints walls 0.90)
        }

        static bool HasBeasts(TC2.Core.Simulation.World w, int hid)
        {
            var bl = w.BeastsOf(hid);
            for (int i = 0; i < bl.Count; i++) if (w.Beasts.Get(bl[i]).Alive) return true;
            return false;
        }

        /// <summary>A fenced pen behind a house whose household keeps beasts (D2-114): its back and two sides of hurdles, so many sections wide.</summary>
        void Pen(Matrix4x4 m, float W, int across)
        {
            const float sect = 2.1f;
            const int deep = 2;
            float z0 = -W / 2f - 3.2f, z1 = z0 - deep * sect, x0 = -across * sect / 2f;
            for (int i = 0; i < across; i++) _pens.Add(0, Color.white, m * Matrix4x4.TRS(new Vector3(x0 + (i + 0.5f) * sect, 0f, z1), Quaternion.identity, Vector3.one));
            for (int j = 0; j < deep; j++)
                for (int e = -1; e <= 1; e += 2)
                    _pens.Add(0, Color.white, m * Matrix4x4.TRS(new Vector3(e * -x0, 0f, z0 - (j + 0.5f) * sect), Quaternion.Euler(0f, 90f, 0f), Vector3.one));
        }

        /// <summary>
        /// A household's dugout (C9.1, from the simulation's own boats): drawn up on the beach where it was made, its bow to the water -
        /// while being hollowed a pale hull growing with the hours put in; not there while its people are out in it (they are drawn at sea
        /// in it); split with age, a grey hulk sinking into the sand; overturned at sea, gone.
        /// </summary>
        void DrawBoat(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int bid, float ex)
        {
            ref var b = ref w.Buildings.Get(bid);
            if (b.Cond <= 0f) return;   // (lost at sea)
            Vec3d at = s.LocalToSphere(b.X, b.Y, P.Radius);
            Vector3 pos = PlanetGeo.Surface(P, at);
            Vector3 up = pos.normalized;
            // the bow down the beach toward the water (the landing's slope, C9.2; away from the square before a landing was kept)
            Vector3 toSea = s.LandAt != 0 ? PlanetGeo.Surface(P, s.LocalToSphere(b.X - s.LandUx, b.Y - s.LandUy, P.Radius)) - pos
                                          : pos - PlanetGeo.Surface(P, s.LocalToSphere(s.SquareX, s.SquareY, P.Radius));
            toSea -= up * Vector3.Dot(toSea, up);
            if (toSea.sqrMagnitude < 1e-6f) toSea = Vector3.Cross(up, Vector3.right);
            var rot = Quaternion.LookRotation(toSea.normalized, up) * Quaternion.Euler(0f, (bid * 37) % 13 - 6, 0f);
            float bex = ex * 1.15f;
            var inst = Boats.IsPlank(b.Kind) ? _beachedP : _beached;
            if (b.Done == 0)
            {
                if (b.Left != 0) return;   // (left half-made when its household went)
                float done = Mathf.Clamp01((float)(b.Hours / Boats.HoursOf(b.Kind)));
                if (done <= 0.05f) return;
                // the hollowing trunk; a plank boat rising on the stocks in the shed (C9.2b)
                inst.Add(0, new Color(1.10f, 1.02f, 0.86f, 1f), Matrix4x4.TRS(pos, rot, new Vector3(bex, bex * (0.4f + 0.6f * done), bex * (0.5f + 0.5f * done))));
                return;
            }
            if (b.Left != 0)
            {
                float c = (float)Homes.RuinLeft(in b, w.Now);
                if (c <= 0.05f) return;
                inst.Add(0, Color.Lerp(new Color(0.45f, 0.45f, 0.42f, 1f), new Color(0.8f, 0.8f, 0.78f, 1f), c), Matrix4x4.TRS(pos - up * (0.3f * (1f - c)), rot, new Vector3(bex, bex * (0.4f + 0.6f * c), bex)));
                return;
            }
            if (b.Household != 0 && w.Households.Exists(b.Household) && Away(w, b.Household, bid)) return;   // (out at sea, or at another landing)
            inst.Add(0, Color.white, Matrix4x4.TRS(pos, rot, Vector3.one * bex));
        }

        /// <summary>
        /// Whether a household's boat is away from its beach (C9.3): out fishing in it, or on a voyage past its landing - rowing the way or
        /// drawn up at the other landing (while its crew gather at the landing at home it still lies there).
        /// </summary>
        static bool Away(TC2.Core.Simulation.World w, int hid, int bid)
        {
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                if (!q.Alive) continue;
                if (q.Act == ActivityKind.Sail && q.WhyArg == bid) return true;
                if (q.Voyage == bid && (q.Step == StepKind.Travel || q.At != q.Home)) return true;
            }
            return false;
        }

        /// <summary>A voyage's boat drawn up at the other village's landing while its crew stay there (C9.3), once a boat.</summary>
        void DrawVisitingBoat(TC2.Core.Simulation.World w, PlanetState P, int boat, int at)
        {
            if (_hulls.ContainsKey(boat) || !w.Settlements.Exists(at)) return;
            ref var s = ref w.Settlements.Get(at);
            if (s.LandAt == 0) return;
            // along the beach beside the landing's own boats, bow to the water
            double side = 6.0 + (boat % 5) * 3.5;
            double x = s.LandX - s.LandUy * side, y = s.LandY + s.LandUx * side;
            Vector3 pos = PlanetGeo.Surface(P, s.LocalToSphere(x, y, P.Radius));
            Vector3 up = pos.normalized;
            Vector3 toSea = PlanetGeo.Surface(P, s.LocalToSphere(x - s.LandUx, y - s.LandUy, P.Radius)) - pos;
            toSea -= up * Vector3.Dot(toSea, up);
            if (toSea.sqrMagnitude < 1e-6f) toSea = Vector3.Cross(up, Vector3.right);
            var rot = Quaternion.LookRotation(toSea.normalized, up);
            float bex = Exaggeration(pos, 1.6f) * 1.15f;
            (w.Buildings.Exists(boat) && Boats.IsPlank(w.Buildings.Get(boat).Kind) ? _afloatP : _afloat).Add(0, Color.white, Matrix4x4.TRS(pos, rot, Vector3.one * bex));
            if (_hullAt.Count == _hulls.Count) { _hullAt.Add(default); _hullSeats.Add(0); }
            _hullAt[_hulls.Count] = (pos, rot, bex); _hullSeats[_hulls.Count] = 99;   // (ashore: nobody seated in it)
            _hulls[boat] = _hulls.Count;
        }

        /// <summary>The orchard trees and bushes in the yards of the villages near the camera (the woods are the terrain's: D2-079).</summary>
        void RebuildTrees(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            if (Rig == null) return;
            float camDist = Rig.Cam != null ? Vector3.Distance(Rig.Cam.transform.position, Rig.FocusPoint) : 5000f;
            for (int i = 0; i < _trees.Length; i++) _trees[i].Clear();
            TreesDrawn = 0;
            if (camDist > 3200f) return;
            float ex = Mathf.Clamp(camDist / 1600f, 1f, 1.5f);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                if (Vector3.Distance(PlanetGeo.Surface(P, s.Anchor), Rig.FocusPoint) > 2500f) continue;
                // (the village's works and their ground: no yard tree is drawn on a work - one grew out of a kiln's dome, C3.2)
                _workDiscs.Clear();
                var wl = w.BuildingsOf(sid);
                for (int i = 0; i < wl.Count; i++)
                {
                    ref var wb = ref w.Buildings.Get(wl[i]);
                    if (Structures.IsHome(wb.Kind) || Boats.IsBoat(wb.Kind) || Homes.Fallen(in wb, w.Now)) continue;
                    _workDiscs.Add(new Vector3(wb.X, wb.Y, (float)Footprints.WorkR(wb.Kind) * 1.2f + 4f));   // (a yard tree's crown is ~3 m across; works are drawn a little larger than life)
                }
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var hd = ref w.Households.Get(hhs[i]);
                    ulong hh = KeyedRng.Mix((ulong)hhs[i] * 31 + 5);
                    double away = System.Math.Atan2(hd.HomeX - s.SquareX, hd.HomeY - s.SquareY); // behind the house
                    for (int k = 0; k < 3; k++)
                    {
                        double a = away + (KeyedRng.Unit(KeyedRng.Mix(hh + (ulong)k)) - 0.5) * 1.6;
                        double r = 9.0 + 6.0 * KeyedRng.Unit(KeyedRng.Mix(hh + 11 + (ulong)k));
                        double x = hd.HomeX + System.Math.Sin(a) * r, y = hd.HomeY + System.Math.Cos(a) * r;
                        if (OnWork(x, y)) continue;
                        int kind = k == 2 ? Bush : BroadleafRound;
                        float sc = k == 2 ? 0.8f : 0.55f + 0.2f * k;
                        _trees[kind].Add(1, new Color(0.98f, 0.98f, 0.98f, 0.98f), Place(P, s, x, y, k * 70f, sc * ex));
                        TreesDrawn++;
                    }
                }
            }
        }

        /// <summary>
        /// The household's cart a person has out (C4.2: the simulation's own carts, never for show): a trader carrying more than a back's load on a
        /// trip out or home, or someone of a household with a cart at its woods or quarry - its kind, or None.
        /// </summary>
        static byte WithCart(TC2.Core.Simulation.World w, in Person p)
        {
            if (p.Household == 0) return Carts.None;
            bool trade = (p.Act == ActivityKind.TripOut || p.Act == ActivityKind.TripHome) && p.Cargo != Trade.None && Trade.KgOf(p.Cargo) * p.CargoQty > Trade.LoadKg + 1e-6;
            bool haul = p.Act == ActivityKind.Wood || p.Act == ActivityKind.Quarry;
            if (!trade && !haul) return Carts.None;
            return Carts.Usable(w, p.Household);
        }

        /// <summary>A member of the household is out with its cart.</summary>
        internal static bool CartOut(TC2.Core.Simulation.World w, int hid)
        {
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count; i++) { ref var q = ref w.People.Get(mem[i]); if (q.Alive && WithCart(w, in q) != Carts.None) return true; }
            return false;
        }

        /// <summary>
        /// A cart with its person: walking, a handcart pulled behind them by its shafts, an ox cart behind its ox with the person at the ox's head;
        /// standing at the woods or a quarry, beside them. A load of sacks on it while it carries more than a back's load.
        /// </summary>
        void DrawCart(in Person p, Vector3 pos, float yaw, float sc, bool walking, byte kind)
        {
            Vector3 up = pos.normalized;
            Quaternion rot = PlanetGeo.Upright(pos, yaw);
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;
            bool ox = kind == Carts.Ox;
            Vector3 at;
            if (walking) at = pos - fwd * ((ox ? 4.2f : 1.9f) * sc) + (ox ? right * (0.9f * sc) : Vector3.zero);
            else { at = pos + right * (1.6f * sc); rot = PlanetGeo.Upright(pos, yaw + 70f); }
            (ox ? _oxcarts : _handcarts).Add(0, Color.white, Matrix4x4.TRS(at, rot, Vector3.one * sc));
            if (ox) _oxen.Add(0, new Color(0.42f, 0.34f, 0.28f, 1f), Matrix4x4.TRS(at + (rot * Vector3.forward) * (3.4f * sc), rot, Vector3.one * sc));
            if (p.Cargo != Trade.None && Trade.KgOf(p.Cargo) * p.CargoQty > Trade.LoadKg + 1e-6)
                _cartLoads.Add(0, Color.white, Matrix4x4.TRS(at + up * ((ox ? 1.0f : 0.6f) * sc) + (rot * Vector3.forward) * (0.1f * sc), rot, Vector3.one * (ox ? 1.6f * sc : sc)));
        }

        readonly List<Vector3> _workDiscs = new List<Vector3>();   // a village's works (x, y, radius) while its yard trees are laid out

        bool OnWork(double x, double y)
        {
            for (int i = 0; i < _workDiscs.Count; i++)
            {
                var d = _workDiscs[i];
                double dx = x - d.x, dy = y - d.y;
                if (dx * dx + dy * dy < (double)d.z * d.z) return true;
            }
            return false;
        }

        void RebuildFire(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            var fire = w.Fire;
            _flames.Clear();
            float t = Time.time;
            for (int i = 0; i < fire.Active.Count; i++)
            {
                int c = fire.Active[i];
                float inten = fire.Intensity[c];
                Vec3d ctr = P.Grid.Center[c];
                Settlement.Frame(ctr, out Vec3d east, out Vec3d north);
                for (int k = 0; k < 6; k++)
                {
                    ulong h = KeyedRng.Hash(11, RngStream.None, (ulong)c, (ulong)k);
                    double ox = (KeyedRng.Unit(h) * 2 - 1) * P.CellSize * 0.45, oy = (KeyedRng.Unit(KeyedRng.Mix(h)) * 2 - 1) * P.CellSize * 0.45;
                    Vec3d d = (ctr * P.Radius + east * ox + north * oy).Normalized;
                    Vector3 pos = PlanetGeo.Surface(P, d);
                    float flicker = 0.75f + 0.25f * Mathf.Sin(t * 7f + k * 1.7f + c);
                    float hgt = (8f + 22f * inten) * flicker * Exaggeration(pos, 6f);
                    _flames.Add(k % 2, k % 2 == 0 ? new Color(1f, 0.45f, 0.08f) : new Color(1f, 0.75f, 0.2f), Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, k * 60), new Vector3(hgt * 0.5f, hgt, hgt * 0.5f)), 2.5f);
                }
            }
        }

        void RebuildPeople(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            _people.Clear(); _afloat.Clear(); _afloatP.Clear(); _hulls.Clear(); _handcarts.Clear(); _oxcarts.Clear(); _cartLoads.Clear(); _oxen.Clear();
            PeopleDrawn = 0; PeopleBusy = 0; BoatsAfloat = 0;
            float waterR = (float)(P.Radius + Ground.WaterHeight(P, P.SeaLevel));
            double t = Host.RenderTime;
            float camDist = Rig != null && Rig.Cam != null ? Vector3.Distance(Rig.Cam.transform.position, Rig.FocusPoint) : 5000f;
            if (camDist > 6000f) return;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                if (p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant || p.Act == ActivityKind.Ill) continue; // indoors
                Vec3d d = w.Agents.PositionAt(p, t);
                Vector3 pos = PlanetGeo.Surface(P, d);
                float age = (float)p.AgeYears((long)t);
                // out in the household's boat (C9.1): drawn in it wherever they are on the water - rowing out, fishing, rowing home. A boat
                // goes out with its crew (C9.2): one hull for the boat, where the first of its crew on the water is, the others seated in it
                // on a voyage (C9.3): rowing the way over the sea in the household's boat - its bow along the way; ashore at the other landing,
                // the boat drawn up on its beach
                bool voyaging = p.Act == ActivityKind.Voyage && p.Step == StepKind.Travel;
                if (p.Voyage != 0 && !voyaging && p.At != 0 && p.At != p.Home) DrawVisitingBoat(w, P, p.Voyage, p.At);
                if (p.Act == ActivityKind.Sail || voyaging)
                {
                    Vec3d onWater = d;
                    bool wet = Ground.Height(P, onWater) < P.SeaLevel - 0.3;
                    if (wet || voyaging)
                    {
                        int boat = p.WhyArg;
                        int dyeF = (int)(KeyedRng.Mix((ulong)id * 0x2545F4914F6CDD1DUL) % (ulong)Dyes.Length);
                        if (_hulls.TryGetValue(boat, out int hi) && _hullSeats[hi] < 99)
                        {
                            var hull = _hullAt[hi];
                            int seat = ++_hullSeats[hi];
                            Vector3 aft = hull.rotation * Vector3.back * ((seat & 1) == 1 ? 0.9f : -0.9f) * ((seat + 1) >> 1) * hull.scale;
                            _people.Add(dyeF, Dyes[dyeF], Matrix4x4.TRS(hull.pos + hull.pos.normalized * (0.25f * hull.scale) + aft, hull.rotation, Vector3.one * (1.2f * hull.scale)), 0f, Presence.Row);
                            PeopleDrawn++;
                            continue;
                        }
                        ulong fk = KeyedRng.Mix((ulong)boat * 0x9E3779B97F4A7C15UL ^ 0xB0A7);
                        Vector3 bpos = wet ? PlanetGeo.ToVector(onWater) * waterR : pos;   // (a voyage's first and last metres are over the beach)
                        Vector3 bup = bpos.normalized;
                        float drift = (float)(KeyedRng.Unit(KeyedRng.Mix(fk + 1)) * 360.0 + t / 600.0);   // swinging slowly on the water
                        var brot = PlanetGeo.Upright(bpos, drift) * Quaternion.Euler(Mathf.Sin((float)(t / 7.0)) * 2f, 0, Mathf.Sin((float)(t / 5.0)) * 3f);
                        if (voyaging)
                        {
                            // bow along the way it is rowed
                            Vector3 ahead = PlanetGeo.Surface(P, w.Agents.PositionAt(p, t + 30.0)) - PlanetGeo.Surface(P, d);
                            ahead -= bup * Vector3.Dot(ahead, bup);
                            if (ahead.sqrMagnitude > 1e-6f) brot = Quaternion.LookRotation(ahead.normalized, bup) * Quaternion.Euler(Mathf.Sin((float)(t / 7.0)) * 2f, 0, Mathf.Sin((float)(t / 5.0)) * 3f);
                        }
                        float bex = Exaggeration(bpos, 2.5f);
                        (w.Buildings.Exists(boat) && Boats.IsPlank(w.Buildings.Get(boat).Kind) ? _afloatP : _afloat).Add(0, Color.white, Matrix4x4.TRS(bpos, brot, Vector3.one * bex));
                        BoatsAfloat++;
                        if (_hullAt.Count == _hulls.Count) { _hullAt.Add(default); _hullSeats.Add(0); }
                        _hullAt[_hulls.Count] = (bpos, brot, bex); _hullSeats[_hulls.Count] = 0;
                        _hulls[boat] = _hulls.Count;
                        _people.Add(dyeF, Dyes[dyeF], Matrix4x4.TRS(bpos + bup * (0.25f * bex), brot, Vector3.one * (1.2f * bex)), 0f, Presence.Row);
                        PeopleDrawn++;
                        continue;
                    }
                }
                // where they are drawn, which way they face and what their body does - from what they are doing (Presence: the field hand hoes
                // along the rows, the woodcutter swings, the herder walks with the herd, neighbours turn to each other; indoors, not drawn)
                if (!Presence.Of(w, in p, t, out d, out Vec3d face, out byte pose, out bool moving)) continue;
                pos = PlanetGeo.Surface(P, d);
                float sc = (age < 12 ? 0.55f + age * 0.038f : 1f) * PeopleScale(pos);   // a little larger than life, to be read
                int dye = (int)(KeyedRng.Mix((ulong)id * 0x2545F4914F6CDD1DUL) % (ulong)Dyes.Length);
                float yaw = (id * 53) % 360;
                {
                    Vector3 up = pos.normalized;
                    Vector3 flat;
                    if (face.X == 0 && face.Y == 0 && face.Z == 0) flat = Vector3.ProjectOnPlane(PlanetGeo.Surface(P, w.Agents.PositionAt(p, t + 20.0)) - pos, up);   // (on the move: where they go)
                    else flat = Vector3.ProjectOnPlane(PlanetGeo.ToVector(face), up);
                    if (flat.sqrMagnitude > 1e-6f) yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(PlanetGeo.Upright(pos, 0) * Vector3.forward, up), flat, up);
                }
                _people.Add(dye + (moving ? 100 : 0), Dyes[dye], Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * sc), 0f, pose);
                PeopleDrawn++;
                if (Presence.Busy(pose) || moving) PeopleBusy++;
                byte cart = WithCart(w, in p);
                if (cart != Carts.None) DrawCart(in p, pos, yaw, sc, moving && (pose == Presence.Walk || pose == Presence.Carry), cart);
            }
        }
    }
}
