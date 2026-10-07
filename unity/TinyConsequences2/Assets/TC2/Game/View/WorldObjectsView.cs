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
        /// <summary>The village works by structure kind (C1.1; the civilisation kit, D2-089); null where a kind has no model yet.</summary>
        readonly Instancer[] _works = new Instancer[24];
        Instancer[] _housePosts;   // the same homes on earth-fast posts: no stone footings (C1.3)
        Instancer _stonePiles;     // stone carried home and stacked by the store (C1.3)
        Instancer[] _houseKinds, _trees;
        Material _mat;
        float _treeTimer, _placeTimer;
        public int PeopleDrawn, TreesDrawn, BoatsAfloat;
        public static int Off;   // tools (webperf): parts not drawn - 1 trees, 2 people, 4 buildings and yards

        static readonly string[] HouseModels = { "house_cottage", "house_timber", "house_long", "house_small" };
        static readonly float[] HouseShare = { 0.40f, 0.22f, 0.18f, 0.20f };
        const int Conifer = 0, ConiferSmall = 1, Broadleaf = 2, BroadleafRound = 3, Birch = 4, Bush = 5;
        static readonly string[] TreeModels = { "tree_conifer", "tree_conifer_small", "tree_broadleaf", "tree_broadleaf_round", "tree_birch", "bush" };

        // natural dyes: undyed linen, madder, woad, weld, walnut, sage, ochre, slate
        static readonly Color[] Dyes =
        {
            new Color(0.86f, 0.82f, 0.72f), new Color(0.66f, 0.27f, 0.22f), new Color(0.30f, 0.40f, 0.60f), new Color(0.82f, 0.72f, 0.36f),
            new Color(0.45f, 0.33f, 0.24f), new Color(0.50f, 0.58f, 0.42f), new Color(0.76f, 0.55f, 0.30f), new Color(0.42f, 0.44f, 0.48f),
        };

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _people = new Instancer(Models.Get("person", ProcMeshes.Person), _mat, true);
            _houseKinds = new Instancer[HouseModels.Length];
            for (int i = 0; i < HouseModels.Length; i++) _houseKinds[i] = new Instancer(Models.Get(HouseModels[i], ProcMeshes.House), _mat, true);
            _housePosts = new Instancer[HouseModels.Length];
            for (int i = 0; i < HouseModels.Length; i++) _housePosts[i] = new Instancer(Models.Get(HouseModels[i] + "_posts", ProcMeshes.House), _mat, true);
            _stonePiles = new Instancer(Models.Get("stone_pile", ProcMeshes.Granary), _mat, true);
            _granaries = new Instancer(Models.Get("granary", ProcMeshes.Granary), _mat, true);
            _walls = new Instancer(Models.Get("palisade", ProcMeshes.Palisade), _mat, true);   // walled villages (D2-074)
            _huts = new Instancer(Models.Get("hut_round", ProcMeshes.House), _mat, true);
            _tents = new Instancer(Models.Get("tent", ProcMeshes.House), _mat, true);   // camps: hides over poles
            _ruins = new Instancer(Models.Get("ruin", ProcMeshes.House), _mat, true);   // the houses of abandoned villages (D2-052)
            _beached = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);   // fishing boats drawn up on the shore
            _afloat = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);    // and out on the water with their fishers
            _wells = new Instancer(Models.Get("well", ProcMeshes.Granary), _mat, true);
            _works[Structures.Well] = _wells; _works[Structures.Granary] = _granaries;
            _works[Structures.Barn] = new Instancer(Models.Get("barn", ProcMeshes.House), _mat, true);
            _works[Structures.Smithy] = new Instancer(Models.Get("smithy", ProcMeshes.House), _mat, true);
            _forgeTop = PartTop(Models.Get("smithy", ProcMeshes.House), new Color(0.274f, 0.234f, 0.205f));   // (the kit's CHIMNEY colour, linear)
            _works[Structures.Quarry] = new Instancer(Models.Get("quarry_face", ProcMeshes.Granary), _mat, true);
            _works[Structures.Mine] = new Instancer(Models.Get("mine_pit", ProcMeshes.Granary), _mat, true);
            _works[Structures.CharHearth] = new Instancer(Models.Get("charcoal_clamp", ProcMeshes.Granary), _mat, true);
            _works[Structures.Ground] = new Instancer(Models.Get("training_ground", ProcMeshes.Granary), _mat, true);
            _works[Structures.Market] = new Instancer(Models.Get("market_stalls", ProcMeshes.Granary), _mat, true);
            _hay = new Instancer(Models.Get("haystack", ProcMeshes.Granary), _mat, true);
            _woodpiles = new Instancer(ProcMeshes.Woodpile(), _mat, true);   // the village's woodstore, by the homes (D2-081)
            _trees = new Instancer[TreeModels.Length];
            for (int i = 0; i < TreeModels.Length; i++) _trees[i] = new Instancer(Models.Get(TreeModels[i], i <= ConiferSmall ? (System.Func<Mesh>)ProcMeshes.Conifer : ProcMeshes.Broadleaf), _mat, true);
            _flames = new Instancer(ProcMeshes.Flame(), _mat, false);
            // where each house's chimney stands (the top of its tallest part); the small house has none
            _chimneyTop = new Vector3[HouseModels.Length];
            _hasChimney = new bool[HouseModels.Length];
            for (int i = 0; i < HouseModels.Length; i++)
            {
                if (HouseModels[i] == "house_small") continue;
                var mesh = Models.Get(HouseModels[i], ProcMeshes.House);
                var v = mesh.vertices;
                if (v.Length == 0) continue;
                float top = float.MinValue;
                foreach (var p in v) if (p.y > top) top = p.y;
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (var p in v) if (p.y > top - 0.12f) { sum += p; n++; }
                _chimneyTop[i] = sum / Mathf.Max(1, n);
                _hasChimney[i] = true;
            }
        }

        Vector3[] _chimneyTop;
        bool[] _hasChimney;
        Vector3 _forgeTop;   // the smithy's chimney top in its model's frame (C1.4): smoke while a smith works there

        /// <summary>The top of a model's part painted in one colour (the smithy's chimney), or the model's top.</summary>
        static Vector3 PartTop(Mesh mesh, Color part)
        {
            var v = mesh.vertices; var c = mesh.colors;
            float top = float.MinValue; Vector3 at = Vector3.zero;
            for (int i = 0; i < v.Length; i++)
            {
                bool ofPart = c != null && c.Length == v.Length && Mathf.Abs(c[i].r - part.r) < 0.03f && Mathf.Abs(c[i].g - part.g) < 0.03f && Mathf.Abs(c[i].b - part.b) < 0.03f;
                if (ofPart && v[i].y > top) { top = v[i].y; at = v[i]; }
            }
            if (top == float.MinValue) foreach (var p in v) if (p.y > top) { top = p.y; at = p; }
            return at;
        }
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
                for (int i = 0; i < _houseKinds.Length; i++) { _houseKinds[i].Draw(cam); _housePosts[i].Draw(cam); }
                _stonePiles.Draw(cam);
                for (int i = 0; i < _works.Length; i++) _works[i]?.Draw(cam);
                _hay.Draw(cam); _huts.Draw(cam); _tents.Draw(cam); _ruins.Draw(cam); _walls.Draw(cam); _woodpiles.Draw(cam);
            }
            if ((Off & 1) == 0) for (int i = 0; i < _trees.Length; i++) _trees[i].Draw(cam);
            _flames.Draw(cam);
            if ((Off & 2) == 0) { _people.Draw(cam); _afloat.Draw(cam); }
            if ((Off & 4) == 0) _beached.Draw(cam);
            EmitSmoke(w);
            EmitLights();
        }

        static int HouseKind(int household)
        {
            float u = (float)KeyedRng.Unit(KeyedRng.Mix((ulong)household * 0x9E3779B97F4A7C15UL));
            float acc = 0;
            for (int i = 0; i < HouseShare.Length; i++) { acc += HouseShare[i]; if (u < acc) return i; }
            return 0;
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
            // the gates: toward each neighbour within a day's walk, where its road leaves
            _gates.Clear();
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

        void RebuildPlaces(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            for (int i = 0; i < _houseKinds.Length; i++) { _houseKinds[i].Clear(); _housePosts[i].Clear(); }
            _stonePiles.Clear();
            for (int i = 0; i < _works.Length; i++) _works[i]?.Clear();
            _hay.Clear(); _huts.Clear(); _tents.Clear(); _ruins.Clear(); _beached.Clear(); _walls.Clear(); _woodpiles.Clear();
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
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    // its works are structures too (C1.1): wells and granaries by the table, standing, being raised or fallen
                    if (!Structures.IsHome(w.Buildings.Get(bl[i]).Kind)) { DrawWork(w, P, in s, bl[i], ex); continue; }
                    if (DrawHome(w, P, in s, bl[i], ex, dark, tOut, woodDays)) litHomes++;
                }
                // households without a home camp on their lot (not those on the road to another village), a fire smoking by the tent
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var h = ref w.Households.Get(hhs[i]);
                    if (h.Building != 0 || h.Party != 0) continue;
                    float yaw = Mathf.Atan2(s.SquareX - h.HomeX, s.SquareY - h.HomeY) * Mathf.Rad2Deg + 180f + ((hhs[i] * 37) % 21 - 10);
                    float age = 0.88f + 0.2f * (float)KeyedRng.Unit(KeyedRng.Mix((ulong)hhs[i] + 77));
                    bool awake = SomeoneAwakeHome(w, hhs[i], out bool cooking);
                    bool lit = dark && awake;
                    if (lit) litHomes++;
                    var tm = Place(P, s, h.HomeX, h.HomeY, yaw, ex);
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
                Beached(w, P, in s, sid, ex);
                // the stone in its store, stacked by the store: a pile for every ~40 loads, up to four (C1.3)
                if (s.Stone >= 5)
                {
                    Construction.GranarySpot(in s, out double sx, out double sy);
                    int piles = Mathf.Clamp(Mathf.CeilToInt((float)(s.Stone / 40.0)), 1, 4);
                    for (int k = 0; k < piles; k++) _stonePiles.Add(0, Color.white, Place(P, s, sx - 7.0 - k * 2.4, sy + 5.0 + (k % 2) * 1.8, 20f * k, ex * 1.1f));
                }
                // haystacks once the harvest is in
                float fill = Mathf.Clamp01((float)(s.Granary / System.Math.Max(1.0, s.Population * 30.0)));
                if (s.Crop == CropStage.Harvested || s.Crop == CropStage.Fallow)
                    for (int k = 0; k < Mathf.RoundToInt(fill * 4); k++)
                        _hay.Add(0, Color.white, Place(P, s, s.SquareX + 22 + k * 4.5, s.SquareY - 16 + (k % 2) * 3.0, k * 40f, ex * (0.8f + 0.1f * k)));
            }
        }

        /// <summary>
        /// A village work (C1.1): a well or a granary where its people raised it - while being raised a pale frame rising with the hours put
        /// in, standing as it was built and weathering with its condition, fallen a low ruin (a well's stone ring outlasts the rest, D2-052).
        /// </summary>
        void DrawWork(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int bid, float ex)
        {
            ref var b = ref w.Buildings.Get(bid);
            var k = Structures.Of(b.Kind);
            float yaw = Mathf.Atan2(s.SquareX - b.X, s.SquareY - b.Y) * Mathf.Rad2Deg + 180f + ((bid * 37) % 21 - 10);
            var inst = b.Kind < _works.Length && _works[b.Kind] != null ? _works[b.Kind] : _granaries;
            var m = Place(P, s, b.X, b.Y, yaw, ex);
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
                else _ruins.Add((int)(c * 4), Color.Lerp(moss, Color.white, c), Place(P, s, b.X, b.Y, yaw, 1f) * Matrix4x4.Scale(new Vector3(ex * 0.7f, ex * 0.7f * (0.25f + 0.75f * c), ex * 0.7f)));
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
                    Vector3 top = m.MultiplyPoint3x4(_forgeTop);
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
            if (b.Kind == Structures.Mine)
            {
                // the spoil heap and the workings grow with the ore dug (C5.1: kg carried out of the ground)
                // (drawn half as large again as life: a 7 m pit is lost on its broken ground at the views people read a village from)
                float dug = 1.5f * (0.7f + 0.3f * Mathf.Clamp01(b.Stock / 3000f) + 0.25f * Mathf.Clamp01((b.Stock - 3000f) / 15000f));
                m = m * Matrix4x4.Scale(new Vector3(dug, dug, dug));
            }
            inst.Add(0, Color.Lerp(moss, Color.white, 0.45f + 0.55f * cond), m);
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
            float yaw = Mathf.Atan2(s.SquareX - b.X, s.SquareY - b.Y) * Mathf.Rad2Deg + 180f + ((b.Style * 37) % 21 - 10);
            float age = 0.88f + 0.2f * (float)KeyedRng.Unit(KeyedRng.Mix((ulong)b.Style + 77));   // weathering of roofs and walls
            if (Homes.Fallen(in b, w.Now) || (b.Household == 0 && Homes.Condition(in b, w.Now) < Homes.Habitable))
            {
                // past mending its roof is gone (the walls stand roofless), then it falls in and sinks (RuinLeft: 1 while it stands)
                float c = (float)Homes.RuinLeft(in b, w.Now);
                if (c <= 0.03f) return false;
                var mr = Place(P, s, b.X, b.Y, yaw, 1f);
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
            var m = Place(P, s, b.X, b.Y, yaw, ex);
            bool hut = b.Kind < Construction.House;
            int kind = hut ? -1 : HouseKind(b.Style);
            if (hut) _huts.Add(300 + key, tint, m, lit ? -1f : 0f);
            else (b.Mat >= 1 ? _houseKinds[kind] : _housePosts[kind]).Add(key, tint, m, lit ? -1f : 0f);   // (on stone footings or on posts: C1.3)
            if (!lived) return false;
            // its share of the village's woodstore stacked by the wall (D2-081): none when the store is all but empty, a bigger pile the
            // more days of the hearths' wood the village holds
            if (woodDays >= 2.0)
            {
                float fx = s.SquareX - b.X, fy = s.SquareY - b.Y, fl = Mathf.Max(0.01f, Mathf.Sqrt(fx * fx + fy * fy));
                fx /= fl; fy /= fl;
                float side = hut ? 3.4f : 5.6f;
                float pile = Mathf.Clamp(0.5f + (float)woodDays / 50f, 0.5f, 1.25f);
                _woodpiles.Add(0, Color.white, Place(P, s, b.X + fy * side - fx * 1.2f, b.Y - fx * side - fy * 1.2f, yaw + 90f, ex * pile));
            }
            if (!awake) return lit;
            // a fire in the hearth: while someone cooks, and all day while it burns against the cold (what today's fire adds to the
            // home's warmth, D2-056) - from the chimney, or a hut's thatch at its peak (D2-081)
            ref var h = ref w.Households.Get(b.Household);
            double heat = h.Indoor - Hearth.Indoors(tOut, h.Shelter, 0.0);
            if (cooking || heat > 0.5)
            {
                Vector3 top;
                if (hut) top = m.MultiplyPoint3x4(new Vector3(0f, 4.5f, 0f));
                else if (_hasChimney != null && _hasChimney[kind]) top = m.MultiplyPoint3x4(_chimneyTop[kind]);
                else return lit;
                _smoke.Add(new Vector4(top.x, top.y, top.z, cooking ? 1f : (float)(0.35 + 0.4 * System.Math.Min(1.0, heat / 8.0))));
            }
            return lit;
        }

        /// <summary>
        /// The water's edge at a fishing ground (sea cell): halfway to its land neighbour nearest the village, as the fishers go
        /// (WildFood.GroundPoint without its jitter); also that land cell. False if the ground has no land beside it.
        /// </summary>
        static bool FishingShore(PlanetState P, in Settlement s, int ground, out Vec3d edge, out Vec3d land)
        {
            var g = P.Grid;
            int best = -1; double bd = double.MaxValue;
            for (int j = 0; j < 8; j++)
            {
                int nb = g.Nbr8[ground * 8 + j];
                if (!P.IsLand(nb)) continue;
                double d = Vec3d.ArcDistance(g.Center[nb], s.Anchor, P.Radius);
                if (d < bd) { bd = d; best = nb; }
            }
            land = best >= 0 ? g.Center[best] : g.Center[ground];
            edge = (land + g.Center[ground]).Normalized;
            return best >= 0;
        }

        /// <summary>
        /// A coastal village's fishing boats drawn up on the shore of its best fishing ground: one for every two of its people who
        /// fish with skill, fewer by those out on the water now, bows to the land, side by side along the shore.
        /// </summary>
        void Beached(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int sid, float ex)
        {
            if (s.Fish.A < 0 || P.IsLand(s.Fish.A)) return;
            var res = w.Residents(sid);
            int fishers = 0, afloat = 0;
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                if (p.SkFish > 0.25f) fishers++;
                if (p.Act == ActivityKind.Fish) afloat++;
            }
            int boats = Mathf.Min(6, (fishers + 1) / 2) - afloat;
            if (boats <= 0 || !FishingShore(P, in s, s.Fish.A, out Vec3d edge, out Vec3d land)) return;
            Vec3d inland = (land - edge).Normalized, along = Vec3d.Cross(edge, inland).Normalized;
            for (int i = 0; i < boats; i++)
            {
                // a little up the beach from the water's edge, a boat's breadth apart
                Vec3d at = (edge * P.Radius + inland * (0.06 * P.CellSize + 2.0 * ex) + along * ((i - (boats - 1) * 0.5) * 3.2 * ex)).Normalized;
                Vector3 pos = PlanetGeo.Surface(P, at);
                Vector3 up = pos.normalized;
                Vector3 bow = PlanetGeo.Surface(P, (at * P.Radius + inland * 5.0).Normalized) - pos;
                bow -= up * Vector3.Dot(bow, up);
                if (bow.sqrMagnitude < 1e-8f) continue;
                _beached.Add(0, Color.white, Matrix4x4.TRS(pos, Quaternion.LookRotation(bow.normalized, up) * Quaternion.Euler(0, (i * 37) % 13 - 6, 0), Vector3.one * ex));
            }
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
                        int kind = k == 2 ? Bush : BroadleafRound;
                        float sc = k == 2 ? 0.8f : 0.55f + 0.2f * k;
                        _trees[kind].Add(1, new Color(0.98f, 0.98f, 0.98f, 0.98f), Place(P, s, x, y, k * 70f, sc * ex));
                        TreesDrawn++;
                    }
                }
            }
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
            _people.Clear(); _afloat.Clear();
            PeopleDrawn = 0; BoatsAfloat = 0;
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
                // a fisher at the water's edge rows out a little toward the ground and fishes from a boat
                if (p.Act == ActivityKind.Fish && p.Ground >= 0 && !P.IsLand(p.Ground) && !(p.Step == StepKind.Local && t < p.TA))
                {
                    ulong fk = KeyedRng.Mix((ulong)id * 0x9E3779B97F4A7C15UL ^ 0xB0A7);
                    double outBy = 0.35 + 0.3 * KeyedRng.Unit(fk);
                    Vec3d onWater = (d * (1.0 - outBy) + P.Grid.Center[p.Ground] * outBy).Normalized;
                    if (Ground.Height(P, onWater) < P.SeaLevel - 0.3)
                    {
                        Vector3 bpos = PlanetGeo.ToVector(onWater) * waterR;
                        Vector3 bup = bpos.normalized;
                        float drift = (float)(KeyedRng.Unit(KeyedRng.Mix(fk + 1)) * 360.0 + t / 600.0);   // swinging slowly on the water
                        var brot = PlanetGeo.Upright(bpos, drift) * Quaternion.Euler(Mathf.Sin((float)(t / 7.0)) * 2f, 0, Mathf.Sin((float)(t / 5.0)) * 3f);
                        float bex = Exaggeration(bpos, 2.5f);
                        _afloat.Add(0, Color.white, Matrix4x4.TRS(bpos, brot, Vector3.one * bex));
                        BoatsAfloat++;
                        int dyeF = (int)(KeyedRng.Mix((ulong)id * 0x2545F4914F6CDD1DUL) % (ulong)Dyes.Length);
                        _people.Add(dyeF, Dyes[dyeF], Matrix4x4.TRS(bpos + bup * (0.25f * bex), brot, Vector3.one * (1.2f * bex)), 0f, 0f);
                        PeopleDrawn++;
                        continue;
                    }
                }
                float sc = (age < 12 ? 0.55f + age * 0.038f : 1f) * PeopleScale(pos);   // a little larger than life, to be read
                int dye = (int)(KeyedRng.Mix((ulong)id * 0x2545F4914F6CDD1DUL) % (ulong)Dyes.Length);
                // walking: on a road, or the walk at the start of a step; they face where they go
                bool walking = p.Step == StepKind.Travel || (p.Step == StepKind.Local && t < p.TA);
                float yaw = (id * 53) % 360;
                if (walking)
                {
                    Vec3d ahead = w.Agents.PositionAt(p, t + 20.0);
                    Vector3 dirTo = PlanetGeo.Surface(P, ahead) - pos;
                    Vector3 up = pos.normalized;
                    Vector3 flat = Vector3.ProjectOnPlane(dirTo, up);
                    if (flat.sqrMagnitude > 1e-6f) yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(PlanetGeo.Upright(pos, 0) * Vector3.forward, up), flat, up);
                }
                _people.Add(dye + (walking ? 100 : 0), Dyes[dye], Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * sc), 0f, walking ? 1f : 0f);
                PeopleDrawn++;
            }
        }
    }
}
