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

        Instancer _people, _granaries, _wells, _hay, _flames, _huts, _tents, _ruins, _beached, _afloat;
        struct TreeInst { public Vector3 Pos; public Quaternion Rot; public float Scale; public byte Kind, Shade; }
        sealed class CellTrees { public float Forest; public TreeInst[] Trees; }
        readonly System.Collections.Generic.Dictionary<int, CellTrees> _treeCache = new System.Collections.Generic.Dictionary<int, CellTrees>();
        readonly System.Collections.Generic.List<TreeInst> _scratch = new System.Collections.Generic.List<TreeInst>();
        readonly System.Collections.Generic.List<int> _cells = new System.Collections.Generic.List<int>();
        int _treeWorld = -1, _treeRelief = -1;
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
            _granaries = new Instancer(Models.Get("granary", ProcMeshes.Granary), _mat, true);
            _huts = new Instancer(Models.Get("hut_round", ProcMeshes.House), _mat, true);
            _tents = new Instancer(Models.Get("tent", ProcMeshes.House), _mat, true);   // camps: hides over poles
            _ruins = new Instancer(Models.Get("ruin", ProcMeshes.House), _mat, true);   // the houses of abandoned villages (D2-052)
            _beached = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);   // fishing boats drawn up on the shore
            _afloat = new Instancer(Models.Get("boat", ProcMeshes.Granary), _mat, true);    // and out on the water with their fishers
            _wells = new Instancer(Models.Get("well", ProcMeshes.Granary), _mat, true);
            _hay = new Instancer(Models.Get("haystack", ProcMeshes.Granary), _mat, true);
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
        readonly List<Vector4> _smoke = new List<Vector4>();   // chimney tops with smoke (xyz) and how much (w)

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
            if ((Off & 2) == 0) RebuildPeople(w);
            var cam = Rig != null ? Rig.Cam : Camera.main;
            if ((Off & 4) == 0)
            {
                for (int i = 0; i < _houseKinds.Length; i++) _houseKinds[i].Draw(cam);
                _granaries.Draw(cam); _wells.Draw(cam); _hay.Draw(cam); _huts.Draw(cam); _tents.Draw(cam); _ruins.Draw(cam);
            }
            if ((Off & 1) == 0) for (int i = 0; i < _trees.Length; i++) _trees[i].Draw(cam);
            _flames.Draw(cam);
            if ((Off & 2) == 0) { _people.Draw(cam); _afloat.Draw(cam); }
            if ((Off & 4) == 0) _beached.Draw(cam);
            EmitSmoke(w);
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

        Matrix4x4 Place(PlanetState P, in Settlement s, double x, double y, float yaw, float scale, float lift = 0f)
        {
            Vec3d d = s.LocalToSphere(x, y, P.Radius);
            Vector3 pos = PlanetGeo.Surface(P, d, lift);
            return Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * scale);
        }

        void RebuildPlaces(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            for (int i = 0; i < _houseKinds.Length; i++) _houseKinds[i].Clear();
            _granaries.Clear(); _wells.Clear(); _hay.Clear(); _huts.Clear(); _tents.Clear(); _ruins.Clear(); _beached.Clear();
            _smoke.Clear();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) { Ruins(w, P, in s, sid); continue; }
                Vector3 center = PlanetGeo.Surface(P, s.Anchor);
                float ex = Exaggeration(center, 1.6f);
                var hhs = w.HouseholdsOf(sid);
                // after dark, a house whose people are home and awake shows its windows
                Vector3 toSun = Rig != null && Rig.Sun != null ? -Rig.Sun.transform.forward : Vector3.up;
                bool dark = Vector3.Dot(PlanetGeo.ToVector(s.Anchor), toSun) < 0.06f;
                double tOut = w.PlanetSys.DayMean(s.Cell, PlanetSystems.Season(w.Now));   // the day the hearths burn against
                for (int i = 0; i < hhs.Count; i++)
                {
                    ref var h = ref w.Households.Get(hhs[i]);
                    int kind = HouseKind(hhs[i]);
                    // the house faces the village square
                    float yaw = Mathf.Atan2(s.SquareX - h.HomeX, s.SquareY - h.HomeY) * Mathf.Rad2Deg + 180f + ((hhs[i] * 37) % 21 - 10);
                    ulong hh = KeyedRng.Mix((ulong)hhs[i] + 77);
                    float age = 0.88f + 0.2f * (float)KeyedRng.Unit(hh);           // weathering of roofs and walls
                    bool awake = SomeoneAwakeHome(w, hhs[i], out bool cooking);
                    bool lit = dark && awake;
                    // what its people have raised (D2-045): a camp of hides, a round hut, a house
                    if (h.Shelter == Construction.Camp)
                    {
                        _tents.Add((int)(age * 10) + (lit ? 100 : 0), new Color(age, age * 0.97f, age * 0.92f, 1f), Place(P, s, h.HomeX, h.HomeY, yaw, ex), lit ? -1f : 0f);
                        continue;
                    }
                    if (h.Shelter < Construction.House)
                    {
                        _huts.Add(300 + (int)(age * 10) + (lit ? 100 : 0), new Color(age, age * 0.97f, age * 0.9f, 1f), Place(P, s, h.HomeX, h.HomeY, yaw, ex), lit ? -1f : 0f);
                        continue;
                    }
                    var m = Place(P, s, h.HomeX, h.HomeY, yaw, ex);
                    _houseKinds[kind].Add((int)(age * 10) + (lit ? 100 : 0), new Color(age, age * 0.98f, age * 0.94f, 1f), m, lit ? -1f : 0f);
                    // a fire in the hearth: while someone cooks, and all day while it burns against the cold (what today's
                    // fire adds to the house's warmth, D2-056)
                    double heat = h.Indoor - Hearth.Indoors(tOut, h.Shelter, 0.0);
                    if (awake && _hasChimney != null && _hasChimney[kind] && (cooking || heat > 0.5))
                    {
                        Vector3 top = m.MultiplyPoint3x4(_chimneyTop[kind]);
                        _smoke.Add(new Vector4(top.x, top.y, top.z, cooking ? 1f : (float)(0.35 + 0.4 * System.Math.Min(1.0, heat / 8.0))));
                    }
                }
                Beached(w, P, in s, sid, ex);
                // the village store and its well, once its people have built them (D2-045); haystacks once the harvest is in
                if ((s.Works & Construction.Granary) != 0) _granaries.Add(0, Color.white, Place(P, s, s.SquareX + 14, s.SquareY - 10, 12f, ex));
                if ((s.Works & Construction.Well) != 0) _wells.Add(0, Color.white, Place(P, s, s.WellX, s.WellY, 30f, ex));
                float fill = Mathf.Clamp01((float)(s.Granary / System.Math.Max(1.0, s.Population * 30.0)));
                if (s.Crop == CropStage.Harvested || s.Crop == CropStage.Fallow)
                    for (int k = 0; k < Mathf.RoundToInt(fill * 4); k++)
                        _hay.Add(0, Color.white, Place(P, s, s.SquareX + 22 + k * 4.5, s.SquareY - 16 + (k % 2) * 3.0, k * 40f, ex * (0.8f + 0.1f * k)));
            }
        }

        /// <summary>
        /// An abandoned village (D2-052): the houses its last households lived in stand roofless and sink and green over the
        /// centuries (a house's stone footings ~300 years, a hut ~80; a camp leaves nothing), its well's stone ring longer.
        /// </summary>
        void Ruins(TC2.Core.Simulation.World w, PlanetState P, in Settlement s, int sid)
        {
            double years = (w.Now - s.Abandoned) / (double)SimTime.Year;
            if (years > 600) return;
            Vector3 center = PlanetGeo.Surface(P, s.Anchor);
            if (Rig != null && Rig.Cam != null && (center - Rig.Cam.transform.position).sqrMagnitude > 6000f * 6000f) return;
            float ex = Exaggeration(center, 1.6f);
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var h = ref w.Households.Get(hhs[i]);
                if (h.Shelter == Construction.Camp) continue;
                double life = h.Shelter == Construction.House ? 300.0 : 80.0;
                float c = (float)(1.0 - years / life);
                if (c <= 0.03f) continue;
                float yaw = Mathf.Atan2(s.SquareX - h.HomeX, s.SquareY - h.HomeY) * Mathf.Rad2Deg + 180f + ((hhs[i] * 37) % 21 - 10);
                var m = Place(P, s, h.HomeX, h.HomeY, yaw, 1f);
                float size = ex * (h.Shelter == Construction.House ? 1f : 0.6f);
                m = m * Matrix4x4.Scale(new Vector3(size, size * (0.25f + 0.75f * c), size));   // the walls fall lower
                Color moss = new Color(0.50f, 0.60f, 0.40f, 1f);
                _ruins.Add((int)(c * 4), Color.Lerp(moss, Color.white, c), m);
            }
            if ((s.Works & Construction.Well) != 0 && years < 600)
            {
                var m = Place(P, s, s.WellX, s.WellY, 30f, ex) * Matrix4x4.Scale(new Vector3(1f, 0.4f, 1f));   // its stone ring
                _wells.Add(1, new Color(0.75f, 0.80f, 0.70f, 1f), m);
            }
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

        void RebuildTrees(TC2.Core.Simulation.World w)
        {
            var P = w.Planet;
            if (Rig == null) return;
            int focus = P.Grid.CellOf(PlanetGeo.ToVec(Rig.FocusPoint));
            float camDist = Rig.Cam != null ? Vector3.Distance(Rig.Cam.transform.position, Rig.FocusPoint) : 5000f;
            for (int i = 0; i < _trees.Length; i++) _trees[i].Clear();
            TreesDrawn = 0;
            if (camDist > 3200f) return; // further out the forest is the ground's colour
            // trees are placed once per cell (stable keyed positions on the drawn ground) and kept until the forest there
            // changes, a fire passes, or the relief moves; each rebuild only assembles them at the current scale
            if (_treeWorld != Host.WorldVersion || P.ReliefVersion - _treeRelief >= Tectonics.ResampleEvery || _treeCache.Count > 6000)
            { _treeCache.Clear(); _treeWorld = Host.WorldVersion; _treeRelief = P.ReliefVersion; }
            _cells.Clear();
            w.Hazards.CellsWithin(w, focus, camDist < 2500f ? 1600 : 3200, _cells);
            float ex = Mathf.Clamp(camDist / 1600f, 1f, 1.5f);
            int perCell = 30;
            foreach (int c in _cells)
            {
                if (!P.IsLand(c)) continue;
                float forest = P.Forest[c];
                bool burning = w.Fire.Intensity[c] > 0.5f;
                if (!_treeCache.TryGetValue(c, out var entry) || System.Math.Abs(entry.Forest - forest) > 0.04f || burning)
                {
                    entry = new CellTrees { Forest = forest, Trees = burning || forest < 0.04f ? System.Array.Empty<TreeInst>() : PlaceTrees(w, c, forest, perCell) };
                    _treeCache[c] = entry;
                }
                var list = entry.Trees;
                Color leaf = SeasonTint(w, c);
                for (int k = 0; k < list.Length; k++)
                {
                    var t = list[k];
                    float b = 0.88f + 0.10f * t.Shade;
                    bool deciduous = t.Kind >= Broadleaf;
                    Color tint = deciduous ? new Color(leaf.r * b, leaf.g * b, leaf.b * b, b) : new Color(b, b, b, b);
                    _trees[t.Kind].Add(t.Shade + (deciduous ? 10 * SeasonBucket(w, c) : 0), tint, Matrix4x4.TRS(t.Pos, t.Rot, Vector3.one * (t.Scale * ex)));
                }
                TreesDrawn += list.Length;
            }
            // orchards and yard bushes in the villages near the camera
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
                    }
                }
            }
        }

        /// <summary>Where the year stands at a cell (0 spring .. 1 end of winter; the south has the opposite seasons).</summary>
        static float YearAt(TC2.Core.Simulation.World w, int c)
        {
            float f = (float)SimTime.YearFraction(w.Now);
            if (w.Planet.SinLat[c] < 0) f = (f + 0.5f) % 1f;
            return f;
        }

        static int SeasonBucket(TC2.Core.Simulation.World w, int c) => Mathf.Clamp((int)(YearAt(w, c) * 8f), 0, 7);

        /// <summary>The colour leaves take through the year (deciduous canopies): fresh, summer, gold and rust, bare.</summary>
        static Color SeasonTint(TC2.Core.Simulation.World w, int c)
        {
            if (w.Planet.TMean[c] > 21f) return Color.white; // no autumn in the tropics
            switch (SeasonBucket(w, c))
            {
                case 0: return new Color(1.08f, 1.12f, 0.86f);      // early spring: fresh
                case 1: case 2: case 3: return Color.white;         // late spring, summer
                case 4: return new Color(1.12f, 1.0f, 0.70f);       // early autumn: turning
                case 5: return new Color(1.45f, 0.86f, 0.40f);      // autumn: gold and rust
                case 6: return new Color(0.78f, 0.60f, 0.45f);      // late autumn: falling
                default: return new Color(0.58f, 0.50f, 0.44f);     // winter: bare
            }
        }

        /// <summary>The trees of one cell: groves and glades by a slow noise, species by the climate, keyed positions.</summary>
        TreeInst[] PlaceTrees(TC2.Core.Simulation.World w, int c, float forest, int perCell)
        {
            var P = w.Planet;
            _scratch.Clear();
            int n = Mathf.RoundToInt(Mathf.Sqrt(forest) * perCell * 2.2f);
            Vec3d ctr = P.Grid.Center[c];
            Settlement.Frame(ctr, out Vec3d east, out Vec3d north);
            double half = P.CellSize * 0.5;
            float tm = P.TMean[c];
            for (int k = 0; k < n; k++)
            {
                ulong h = KeyedRng.Hash(w.Seed, RngStream.None, (ulong)c, (ulong)k);
                double ox = (KeyedRng.Unit(h) * 2 - 1) * half, oy = (KeyedRng.Unit(KeyedRng.Mix(h)) * 2 - 1) * half;
                Vec3d d = (ctr * P.Radius + east * ox + north * oy).Normalized;
                double clump = Noise.Fbm(d.X * 60.0, d.Y * 60.0, d.Z * 60.0, 2);
                if (KeyedRng.Unit(KeyedRng.Mix(h + 3)) > forest * (0.15 + 1.9 * System.Math.Max(0.0, clump + 0.15))) continue;
                int under = P.Grid.CellOf(d);
                if (w.SettlementAtCell(under) != 0 || P.FieldOwner[under] != 0) continue; // fields and houses are cleared ground
                if (Ground.Height(P, d) < P.SeaLevel + 1.5) continue;                 // not on the beach
                Vector3 pos = PlanetGeo.Surface(P, d);
                double u = KeyedRng.Unit(KeyedRng.Mix(h + 5));
                int kind;
                if (tm < 3) kind = u < 0.65 ? Conifer : ConiferSmall;
                else if (tm < 9) kind = u < 0.45 ? Conifer : (u < 0.62 ? ConiferSmall : (u < 0.82 ? Birch : Broadleaf));
                else if (tm < 19) kind = u < 0.10 ? Conifer : (u < 0.50 ? Broadleaf : (u < 0.80 ? BroadleafRound : (u < 0.90 ? Birch : Bush)));
                else kind = u < 0.55 ? BroadleafRound : (u < 0.85 ? Broadleaf : Bush);
                float sc = 0.62f + 0.75f * (float)KeyedRng.Unit(KeyedRng.Mix(h + 7));
                byte shade = (byte)((h >> 8) % 3);
                _scratch.Add(new TreeInst { Pos = pos, Rot = PlanetGeo.Upright(pos, (float)(h % 360)), Scale = sc, Kind = (byte)kind, Shade = shade });
                if (clump < 0.0 && (h & 7) == 0)
                {
                    Vector3 bp = pos + (Vector3)(PlanetGeo.ToVector(east) * 2.5f);
                    _scratch.Add(new TreeInst { Pos = bp, Rot = PlanetGeo.Upright(bp, (float)(h % 97)), Scale = sc * 0.9f, Kind = Bush, Shade = shade });
                }
            }
            return _scratch.ToArray();
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
                if (p.Act == ActivityKind.Sleep || p.Act == ActivityKind.Infant) continue; // indoors
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
                float sc = (age < 12 ? 0.55f + age * 0.038f : 1f) * 1.35f * Exaggeration(pos, 2.5f); // a little larger than life, to be read
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
