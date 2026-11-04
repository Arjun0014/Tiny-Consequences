using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// (session 32: the slow powers read on the world) What the god's slower hand looks like while it lasts, from the world's state only: a drought, a rain, warm
    /// or cold air laid over the land it holds (`World.DrySpells`) - faint in the power's colour, its edge alive (heat shimmering, rain drifting in, frost
    /// pulsing), coming in over a second and going over its last day; a sickly haze over the homes of the sick; a brown-grey wash over a blighted village's
    /// fields for the rest of their season; a glint where the god's copper lies unseen; a green flush over fields renewed, a ring of dust where the hunters
    /// were culled and of green where game was brought. Words for the HUD to set beside them (<see cref="Labels"/>: Hud.Spells.cs). Writes nothing.
    /// </summary>
    public sealed class SpellView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        /// <summary>A word the HUD sets on the land this frame: where (a direction from the planet's centre), what, in what colour.</summary>
        public struct Label { public Vec3d Dir; public string Text; public Color Col; }
        public static readonly List<Label> Labels = new List<Label>();

        // the powers' colours (as the HUD's bar has them: Hud.Powers.cs)
        static readonly Color DroughtCol = new Color(0.95f, 0.70f, 0.30f), RainCol = new Color(0.40f, 0.68f, 1.00f), WarmCol = new Color(1.00f, 0.55f, 0.25f),
                              ColdCol = new Color(0.70f, 0.90f, 1.00f), BlightCol = new Color(0.46f, 0.40f, 0.27f), BountyCol = new Color(0.62f, 0.95f, 0.35f),
                              CullCol = new Color(0.85f, 0.35f, 0.30f), BringCol = new Color(0.50f, 0.88f, 0.55f), OreCol = new Color(0.40f, 0.85f, 0.70f),
                              SickCol = new Color(0.70f, 0.80f, 0.52f);

        Mesh _mesh;
        Material _mat;
        readonly List<Vector3> _v = new List<Vector3>(16384);
        readonly List<Color> _c = new List<Color>(16384);
        readonly List<int> _t = new List<int>(49152);
        World _world;
        long _seen;
        int _relief = -1;
        float _scanAt = -99f;

        // a spell's ground, laid once: rings out from its heart to past its edge, each vertex on the ground (with its up, its share of the reach, its angle)
        sealed class Geo { public Vector3[] Pos, Up; public float[] Rf, Ang; public int Rings, Seg; public float Born; public bool Live; public int DaysLeft = -1; public string Text; }
        readonly Dictionary<long, Geo> _geo = new Dictionary<long, Geo>();
        readonly List<long> _gone = new List<long>();
        static readonly float[] RingRf = BuildRings();
        const int SpellSeg = 160;

        // a village's field cells, each a small fan on the ground (the blight's wash, the bounty's flush)
        sealed class Fields { public Vector3[] Pos, Up; public int Cells; public float At; }
        readonly Dictionary<int, Fields> _fields = new Dictionary<int, Fields>();
        const int FanSeg = 10;
        double _cellR = 60.0;

        readonly Dictionary<int, long> _blightAt = new Dictionary<int, long>();   // the last blight of each village's crop (the ledger's time)
        readonly List<int> _blighted = new List<int>();
        readonly Dictionary<int, (int pct, string text)> _blightWords = new Dictionary<int, (int, string)>();
        struct Flush { public int Sid; public float Born; }
        readonly List<Flush> _flush = new List<Flush>();
        struct Ring { public Vec3d Dir; public double Reach; public Color Col; public float Born; public int Kind; }   // Kind 0 culled (outward, dust), 1 brought (inward), 2 renewed
        readonly List<Ring> _rings = new List<Ring>();
        struct Sick { public Vector3 At, Up; public float Seed; public bool Abed; }
        readonly List<Sick> _sick = new List<Sick>();
        readonly Dictionary<int, int> _sickAt = new Dictionary<int, int>();   // (a household's haze, thicker where someone lies abed)
        struct Ore { public Vector3 At, Up; public Vec3d Dir; }
        readonly List<Ore> _ore = new List<Ore>();
        const string OreWords = "Copper laid bare - nobody has seen it yet";

        static float[] BuildRings()
        {
            var r = new List<float>();
            for (int k = 0; k <= 28; k++) r.Add(0.9f * k / 28f);
            r.AddRange(new[] { 0.93f, 0.955f, 0.975f, 0.99f, 1.0f, 1.015f, 1.04f });
            return r.ToArray();
        }

        void Start()
        {
            _mat = Materials.Load("Overlay");
            _mesh = new Mesh { name = "tc2-spells", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
        }

        void LateUpdate()
        {
            Labels.Clear();
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _mat == null || Host.CatchingUp) return;
            var P = w.Planet;
            if (!ReferenceEquals(w, _world)) NewWorld(w);
            if (P.ReliefVersion != _relief) { _relief = P.ReliefVersion; _geo.Clear(); _fields.Clear(); }
            float now = Time.unscaledTime;
            Records(w, now);
            if (now - _scanAt > 0.5f) { _scanAt = now; Scan(w, now); }
            _v.Clear(); _c.Clear(); _t.Clear();
            Vector3 camPos = Rig.Cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            float lift = Mathf.Max(2f, alt * 0.0025f);
            Spells(w, now, lift);
            Blights(w, now, lift);
            Flushes(w, now, lift);
            Rings(w, now, lift, alt);
            Sickness(now, camPos, alt);
            Glints(now, camPos, alt);
            if (_v.Count == 0) return;
            _mesh.Clear();
            _mesh.SetVertices(_v); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            var rp = new RenderParams(_mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = Rig.Cam, worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e5f) };
            Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
        }

        void NewWorld(World w)
        {
            _world = w; _seen = w.History.LastId;
            _geo.Clear(); _fields.Clear(); _blightAt.Clear(); _blightWords.Clear(); _flush.Clear(); _rings.Clear(); _sick.Clear(); _ore.Clear(); _blighted.Clear();
            var P = w.Planet;
            _cellR = System.Math.Sqrt(4.0 * System.Math.PI * P.Radius * P.Radius / System.Math.Max(1, P.Cells)) * 0.62;
            // the blights of the last year (a loaded world's blighted fields show at once)
            var H = w.History;
            for (int i = H.Count - 1, k = 0; i >= 0 && k < 200000; i--, k++)
            {
                ref var e = ref H.RefAt(i);
                if (w.Now - e.Time > SimTime.Year) break;
                if (e.Kind == HistoryKind.CropsBlighted && e.Settlement > 0 && !_blightAt.ContainsKey(e.Settlement)) _blightAt[e.Settlement] = e.Time;
            }
        }

        /// <summary>What the ledger has recorded since the last frame: a renewal, a cull or a bringing cast just now (a ring), fields renewed (a flush), a blight.</summary>
        void Records(World w, float now)
        {
            var H = w.History;
            if (H.LastId == _seen) return;
            int from = H.FirstIndexAfter(_seen), to = H.Count;
            if (to - from > 60000) from = to - 60000;   // (a burst of years: the newest only)
            for (int i = from; i < to; i++)
            {
                ref var e = ref H.RefAt(i);
                bool fresh = w.Now - e.Time <= 2 * SimTime.Hour;
                switch (e.Kind)
                {
                    case HistoryKind.CropsBlighted: if (e.Settlement > 0) _blightAt[e.Settlement] = e.Time; break;
                    case HistoryKind.SoilRenewed: if (fresh && e.Settlement > 0 && _flush.Count < 16) _flush.Add(new Flush { Sid = e.Settlement, Born = now }); break;
                    case HistoryKind.GodCommand:
                        {
                            if (!fresh || e.Cell < 0 || e.Cell >= w.Planet.Cells || _rings.Count >= 8) break;
                            var k = (CommandKind)e.ArgA;
                            Vec3d at = w.Planet.Grid.Center[e.Cell];
                            if (k == CommandKind.GodCull) _rings.Add(new Ring { Dir = at, Reach = World.GodReach, Col = CullCol, Born = now, Kind = 0 });
                            else if (k == CommandKind.GodBring) _rings.Add(new Ring { Dir = at, Reach = World.GodReach, Col = BringCol, Born = now, Kind = 1 });
                            else if (k == CommandKind.GodBounty) _rings.Add(new Ring { Dir = at, Reach = World.GodReach, Col = BountyCol, Born = now, Kind = 2 });
                            break;
                        }
                }
            }
            _seen = H.LastId;
        }

        /// <summary>Twice a second: the homes of the sick, the god's copper no one has seen, the villages whose standing crop a blight took.</summary>
        void Scan(World w, float now)
        {
            var P = w.Planet;
            Vector3 f = Rig.FocusPoint;
            Vec3d look = new Vec3d(f.x, f.y, f.z).Normalized;
            float alt = Rig.Cam.transform.position.magnitude - (float)P.Radius;
            double near = 2500.0 + 2.0 * alt;
            // the sick: a haze over each home with someone sick in it (the carrier unknowing as well: the cast is seen where it broke out)
            _sick.Clear(); _sickAt.Clear();
            if (alt < 9000f)
            {
                for (int sid = w.Settlements.First(); sid != 0 && _sick.Count < 64; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (!s.Inhabited || Vec3d.ArcDistance(look, s.Anchor, P.Radius) > near) continue;
                    var res = w.Residents(sid);
                    for (int i = 0; i < res.Count && _sick.Count < 64; i++)
                    {
                        ref var p = ref w.People.Get(res[i]);
                        if (!p.Alive || p.Sick == 0 || !w.Households.Exists(p.Household)) continue;
                        bool abed = p.SickPhase == TC2.Core.Health.Disease.Infectious;
                        if (_sickAt.TryGetValue(p.Household, out int si)) { if (abed) { var sk = _sick[si]; sk.Abed = true; _sick[si] = sk; } continue; }
                        ref var hh = ref w.Households.Get(p.Household);
                        if (!w.Settlements.Exists(hh.Settlement)) continue;
                        Vec3d d = w.Settlements.Get(hh.Settlement).LocalToSphere(hh.HomeX, hh.HomeY, P.Radius);
                        Vector3 g = PlanetGeo.Surface(P, d, 0f);
                        _sickAt[p.Household] = _sick.Count;
                        _sick.Add(new Sick { At = g, Up = g.normalized, Seed = (p.Household * 0.6180339f) % 1f, Abed = abed });
                    }
                }
            }
            // the god's copper while no village knows it
            _ore.Clear();
            for (int i = 0; i < w.Deposits.Count && _ore.Count < 8; i++)
            {
                var d = w.Deposits.Get(i);
                if (d.Event == 0 || d.GaveOut != 0 || d.Cell < 0 || d.Cell >= P.Cells) continue;
                bool known = false;
                for (int sid = w.Settlements.First(); sid != 0 && !known; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.Inhabited && TC2.Core.Places.Mining.Known(in s) == d.Cell) known = true;
                }
                if (known) continue;
                Vec3d c = P.Grid.Center[d.Cell];
                Vector3 g = PlanetGeo.Surface(P, c, 0f);
                _ore.Add(new Ore { At = g, Up = g.normalized, Dir = c });
            }
            // the blighted: a standing crop the blight took this season (its record within the year, the crop still in the field and spoiled)
            _blighted.Clear();
            foreach (var kv in _blightAt)
            {
                int sid = kv.Key;
                if (!w.Settlements.Exists(sid) || w.Now - kv.Value > SimTime.Year) continue;
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.CropBurnt < 0.05) continue;
                if (s.Crop != TC2.Core.Places.CropStage.Sown && s.Crop != TC2.Core.Places.CropStage.Growing && s.Crop != TC2.Core.Places.CropStage.Ripe) continue;
                _blighted.Add(sid);
            }
            _blighted.Sort();
        }
        /// <summary>The spells the world holds: each its ground laid once, its colours each frame.</summary>
        void Spells(World w, float now, float lift)
        {
            foreach (var kv in _geo) kv.Value.Live = false;
            var P = w.Planet;
            for (int i = 0; i < w.DrySpells.Count; i++)
            {
                var sp = w.DrySpells[i];
                if (w.Now >= sp.Until || sp.Cell < 0 || sp.Cell >= P.Cells) continue;
                long key = sp.Event != 0 ? sp.Event : (sp.Cell * 8L + sp.Kind) ^ (sp.Until << 3);
                if (!_geo.TryGetValue(key, out var g)) { g = Lay(w, P.Grid.Center[sp.Cell], sp.Reach); g.Born = now; _geo[key] = g; }
                g.Live = true;
                float fade = Mathf.Clamp01((now - g.Born) / 1f) * Mathf.Clamp01((float)((sp.Until - w.Now) / (double)SimTime.Day));
                if (fade <= 0.002f) continue;
                Color col = sp.Kind == 0 ? DroughtCol : sp.Kind == 1 ? RainCol : sp.Kind == 2 ? WarmCol : ColdCol;
                Draw(g, sp.Kind, col, fade, now, lift);
                // its words: what it is and how long it has
                int left = (int)System.Math.Ceiling((sp.Until - w.Now) / (double)SimTime.Day);
                if (left != g.DaysLeft || g.Text == null)
                {
                    g.DaysLeft = left;
                    string what = sp.Kind == 0 ? "Drought" : sp.Kind == 1 ? "Your rain" : sp.Kind == 2 ? "Warm air" : "Cold air";
                    g.Text = what + " - " + (left <= 1 ? "its last day" : left.ToString(CultureInfo.InvariantCulture) + " days left");
                }
                Labels.Add(new Label { Dir = P.Grid.Center[sp.Cell], Text = g.Text, Col = Color.Lerp(col, Color.white, 0.35f) });
            }
            _gone.Clear();
            foreach (var kv in _geo) if (!kv.Value.Live) _gone.Add(kv.Key);
            for (int i = 0; i < _gone.Count; i++) _geo.Remove(_gone[i]);
        }

        /// <summary>A spell's ground: rings from its heart past its reach, each vertex on the ground.</summary>
        static Geo Lay(World w, Vec3d dir, double reach)
        {
            int rings = RingRf.Length, seg = SpellSeg, n = rings * (seg + 1);
            var g = new Geo { Pos = new Vector3[n], Up = new Vector3[n], Rf = new float[n], Ang = new float[n], Rings = rings, Seg = seg };
            Frame(dir, out var e, out var nn);
            for (int r = 0; r < rings; r++)
                for (int s = 0; s <= seg; s++)
                {
                    int k = r * (seg + 1) + s;
                    double a = 2 * System.Math.PI * s / seg;
                    var p = At(w, dir, e, nn, reach * RingRf[r], a, 0f);
                    g.Pos[k] = p; g.Up[k] = p.normalized; g.Rf[k] = RingRf[r]; g.Ang[k] = (float)a;
                }
            return g;
        }

        /// <summary>A spell's colours now: faint over the land it holds, stronger toward its edge, the edge alive in its way.</summary>
        void Draw(Geo g, byte kind, Color col, float fade, float t, float lift)
        {
            int b0 = _v.Count, n = g.Pos.Length;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
            for (int k = 0; k < n; k++)
            {
                float rf = g.Rf[k], a = g.Ang[k];
                float fill = 0.06f + 0.05f * rf * rf;
                float edge = rf < 0.9f ? 0f : rf <= 1f ? Mathf.SmoothStep(0f, 1f, (rf - 0.9f) / 0.1f) : 1f - Mathf.SmoothStep(0f, 1f, (rf - 1f) / 0.04f);
                if (rf > 1f) fill = 0f;
                float alpha, white = 0f;
                switch (kind)
                {
                    case 0:   // drought: the heat shimmering - ripples going slowly out, the edge flickering along its length
                        {
                            float rip = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (rf * 5f - t * 0.12f))), 6f);
                            float shim = 0.62f + 0.38f * Mathf.Sin(a * 7f + t * 0.9f) * Mathf.Sin(a * 3f - t * 0.55f + rf * 9f);
                            alpha = fill + 0.035f * rip * (rf < 1f ? 1f : 0f) + 0.40f * edge * shim;
                            white = 0.25f * rip;
                            break;
                        }
                    case 1:   // rain: bands drifting slowly in toward its heart, the edge breathing
                        {
                            float drift = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (rf * 4f + t * 0.10f))), 8f);
                            alpha = fill + 0.07f * drift * (rf < 1f ? 1f : 0f) + 0.36f * edge * (0.8f + 0.2f * Mathf.Sin(t * 0.8f + a * 2f));
                            white = 0.2f * drift;
                            break;
                        }
                    case 2:   // warm air: the shimmer quicker, ripples going out
                        {
                            float rip = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (rf * 6f - t * 0.25f))), 5f);
                            float shim = 0.6f + 0.4f * Mathf.Sin(a * 9f + t * 1.6f) * Mathf.Sin(a * 4f - t * 1.1f + rf * 11f);
                            alpha = fill + 0.03f * rip * (rf < 1f ? 1f : 0f) + 0.36f * edge * shim;
                            white = 0.15f * rip;
                            break;
                        }
                    default:   // cold air: a frost pulsing at the edge, glinting in points along it
                        {
                            float glint = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 18f + Mathf.Sin(t * 0.4f) * 2f)), 6f);
                            alpha = fill + edge * (0.26f + 0.16f * pulse) * (0.55f + 0.45f * glint);
                            white = edge * (0.25f + 0.45f * pulse * glint);
                            break;
                        }
                }
                Color c = Color.Lerp(col, Color.white, white); c.a = Mathf.Clamp01(alpha * fade);
                _v.Add(g.Pos[k] + g.Up[k] * lift); _c.Add(c);
            }
            int row = g.Seg + 1;
            for (int r = 0; r < g.Rings - 1; r++)
                for (int s = 0; s < g.Seg; s++)
                {
                    int k = b0 + r * row + s;
                    _t.Add(k); _t.Add(k + row); _t.Add(k + 1);
                    _t.Add(k + 1); _t.Add(k + row); _t.Add(k + row + 1);
                }
        }

        /// <summary>A village's field cells laid on the ground (a small fan each), kept a few seconds.</summary>
        Fields FieldsOf(World w, int sid, float now)
        {
            if (_fields.TryGetValue(sid, out var f) && now - f.At < 4f) return f;
            var P = w.Planet;
            var cells = new List<int>();
            for (int c = 0; c < P.Cells && cells.Count < 400; c++) if (P.FieldOwner[c] == sid) cells.Add(c);
            int per = FanSeg + 2;
            f = new Fields { Pos = new Vector3[cells.Count * per], Up = new Vector3[cells.Count * per], Cells = cells.Count, At = now };
            for (int i = 0; i < cells.Count; i++)
            {
                Vec3d d = P.Grid.Center[cells[i]];
                Frame(d, out var e, out var n);
                int o = i * per;
                f.Pos[o] = PlanetGeo.Surface(P, d, 0f); f.Up[o] = f.Pos[o].normalized;
                for (int s = 0; s <= FanSeg; s++)
                {
                    var p = At(w, d, e, n, _cellR, 2 * System.Math.PI * s / FanSeg, 0f);
                    f.Pos[o + 1 + s] = p; f.Up[o + 1 + s] = p.normalized;
                }
            }
            _fields[sid] = f;
            return f;
        }

        /// <summary>Fans over a village's field cells in a colour, each breathing on its own (a blight's patches), at an opacity.</summary>
        void Wash(Fields f, Color col, float alpha, float t, float lift, float mottle)
        {
            int per = FanSeg + 2;
            for (int i = 0; i < f.Cells; i++)
            {
                int o = i * per, b0 = _v.Count;
                float ph = (i * 0.7548777f) % 1f;
                float a = alpha * (1f - mottle + mottle * (0.5f + 0.5f * Mathf.Sin(t * 0.7f + ph * 6.283f)));
                Color heart = col; heart.a = Mathf.Clamp01(a);
                Color rim = col; rim.a = 0f;
                _v.Add(f.Pos[o] + f.Up[o] * lift); _c.Add(heart);
                for (int s = 0; s <= FanSeg; s++) { _v.Add(f.Pos[o + 1 + s] + f.Up[o + 1 + s] * lift); _c.Add(rim); }
                for (int s = 0; s < FanSeg; s++) { _t.Add(b0); _t.Add(b0 + 1 + s); _t.Add(b0 + 2 + s); }
            }
        }

        /// <summary>A blighted village's fields under a brown-grey wash in patches, for as long as the spoiled crop stands; its words.</summary>
        void Blights(World w, float now, float lift)
        {
            for (int i = 0; i < _blighted.Count; i++)
            {
                int sid = _blighted[i];
                if (!w.Settlements.Exists(sid)) continue;
                var f = FieldsOf(w, sid, now);
                Wash(f, BlightCol, 0.42f, now, lift, 0.45f);
                ref var s = ref w.Settlements.Get(sid);
                int pct = (int)System.Math.Round(s.CropBurnt * 100.0);
                if (!_blightWords.TryGetValue(sid, out var bw) || bw.pct != pct) { bw = (pct, "Blight - " + pct.ToString(CultureInfo.InvariantCulture) + "% of the crop spoiled"); _blightWords[sid] = bw; }
                Labels.Add(new Label { Dir = s.Anchor, Text = bw.text, Col = new Color(0.86f, 0.80f, 0.62f) });
            }
        }

        /// <summary>Fields renewed by the god: a green flush over them for some seconds.</summary>
        void Flushes(World w, float now, float lift)
        {
            for (int i = _flush.Count - 1; i >= 0; i--)
            {
                var fl = _flush[i];
                float age = now - fl.Born;
                if (age > 7f || !w.Settlements.Exists(fl.Sid)) { _flush.RemoveAt(i); continue; }
                float env = Mathf.Clamp01(age / 0.6f) * (1f - Mathf.SmoothStep(0f, 1f, (age - 3f) / 4f));
                Wash(FieldsOf(w, fl.Sid, now), BountyCol, 0.55f * env, now * 2f, lift, 0.3f);
            }
        }

        /// <summary>The cull's ring of dust going out over the land, the bringing's ring of green coming in, the renewal's ring going out: a few seconds each.</summary>
        void Rings(World w, float now, float lift, float alt)
        {
            const float life = 3.5f;
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i];
                float age = now - r.Born;
                if (age > life) { _rings.RemoveAt(i); continue; }
                float k = age / life;
                float s = r.Kind == 1 ? 1f - Mathf.Sqrt(k) * 0.95f : 0.06f + 0.94f * Mathf.Sqrt(k);
                double rad = System.Math.Round(r.Reach * s / 8.0) * 8.0;   // (steps of a few metres: the ground's height is kept for each)
                double wid = System.Math.Max(12.0, r.Reach * 0.035);
                float env = Mathf.Clamp01(age / 0.3f) * (1f - k);
                Color c = r.Col; c.a = 0.55f * env;
                Color clear = r.Col; clear.a = 0f;
                Band(w, r.Dir, System.Math.Max(0.0, rad - wid * 2.5), rad, clear, c, lift);
                Band(w, r.Dir, rad, rad + wid, c, clear, lift);
                if (r.Kind == 0 && alt < 12000f)
                {
                    // the dust the fleeing and the fallen raise along its front
                    Frame(r.Dir, out var e, out var n);
                    for (int q = 0; q < 28; q++)
                    {
                        double a = 2 * System.Math.PI * (q + 0.5 * KeyedRng.Unit((ulong)(q * 7919 + 13))) / 28;
                        Vector3 g = At(w, r.Dir, e, n, rad, a, 0f);
                        Puffs.Add(g + g.normalized * (6f + 4f * k), Mathf.Max(28f, (float)wid * 2.2f) * (0.8f + 0.5f * k), 0.42f * env, 0.45f, 0f, (q * 0.37f) % 1f, 1f);
                    }
                }
            }
        }

        /// <summary>A sickly haze over each home of the sick: pale green-grey, low and slow, thicker where someone lies abed.</summary>
        void Sickness(float now, Vector3 cam, float alt)
        {
            if (_sick.Count == 0 || alt > 9000f) return;
            float far = Mathf.InverseLerp(9000f, 4000f, alt);
            for (int i = 0; i < _sick.Count; i++)
            {
                var s = _sick[i];
                Vector3 east = Vector3.Cross(Vector3.up, s.Up); if (east.sqrMagnitude < 1e-6f) east = Vector3.right; east.Normalize();
                Vector3 north = Vector3.Cross(s.Up, east);
                float size = Mathf.Max(9f, alt * 0.01f);
                for (int q = 0; q < 3; q++)
                {
                    float ph = s.Seed * 6.283f + q * 2.1f;
                    Vector3 drift = (east * Mathf.Sin(now * 0.21f + ph) + north * Mathf.Cos(now * 0.17f + ph * 1.3f)) * (size * 0.45f);
                    Vector3 at = s.At + s.Up * (size * (0.45f + 0.25f * q + 0.08f * Mathf.Sin(now * 0.3f + ph))) + drift;
                    float a = (s.Abed ? 0.24f : 0.13f) * (0.75f + 0.25f * Mathf.Sin(now * 0.5f + ph)) * far;
                    Color c = SickCol; c.a = a;
                    Sprite(at, size * (1.1f + 0.3f * q), c, cam);
                }
                if (alt < 5000f) Puffs.Add(s.At + s.Up * (size * 0.6f), size * 1.6f, (s.Abed ? 0.10f : 0.06f) * far, 0.22f, 0f, s.Seed, 0.8f);
            }
        }

        /// <summary>The god's copper no one has seen: a glint standing over it, and a faint ring on the ground; its words.</summary>
        void Glints(float now, Vector3 cam, float alt)
        {
            for (int i = 0; i < _ore.Count; i++)
            {
                var o = _ore[i];
                float size = Mathf.Max(6f, alt * 0.012f);
                float tw = 0.65f + 0.35f * Mathf.Sin(now * 2.3f + i) * Mathf.Sin(now * 3.7f + i * 1.7f);
                Vector3 at = o.At + o.Up * (size * 0.9f);
                Color glow = OreCol; glow.a = 0.45f * tw;
                Sprite(at, size * 1.4f, glow, cam);
                Color core = Color.Lerp(OreCol, Color.white, 0.6f); core.a = 0.95f * tw;
                Vector3 view = (at - cam).normalized;
                Vector3 sx = Vector3.Cross(view, o.Up).normalized, sy = Vector3.Cross(sx, view);
                float spin = now * 0.4f + i;
                Vector3 ax = sx * Mathf.Cos(spin) + sy * Mathf.Sin(spin), ay = -sx * Mathf.Sin(spin) + sy * Mathf.Cos(spin);
                Segment(at - ax * size * 1.6f * tw, at + ax * size * 1.6f * tw, size * 0.16f, core, cam);
                Segment(at - ay * size * 1.1f * tw, at + ay * size * 1.1f * tw, size * 0.16f, core, cam);
                Sprite(at, size * 0.35f, core, cam);
                Labels.Add(new Label { Dir = o.Dir, Text = OreWords, Col = Color.Lerp(OreCol, Color.white, 0.4f) });
            }
        }

        // ---------------------------------------------------------------- the ground and the camera's kit (as AimView draws)

        /// <summary>A point at an angle and an arc's length (m) from a centre on the ground, lifted a little above it.</summary>
        static Vector3 At(World w, Vec3d up, Vec3d e, Vec3d n, double r, double a, float lift)
        {
            double ang = r / w.Planet.Radius;
            Vec3d p = (up * System.Math.Cos(ang) + (e * System.Math.Cos(a) + n * System.Math.Sin(a)) * System.Math.Sin(ang)).Normalized;
            return PlanetGeo.Surface(w.Planet, p, lift);
        }

        static void Frame(Vec3d up, out Vec3d e, out Vec3d n)
        {
            e = Vec3d.Cross(new Vec3d(0, 1, 0), up); if (e.Length < 1e-6) e = new Vec3d(1, 0, 0);
            e = e.Normalized; n = Vec3d.Cross(up, e).Normalized;
        }

        /// <summary>A band on the ground between two radii, its colour from the inner edge to the outer.</summary>
        void Band(World w, Vec3d dir, double r0, double r1, Color c0, Color c1, float lift)
        {
            const int seg = 96;
            Frame(dir, out var e, out var n);
            int b0 = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                double a = 2 * System.Math.PI * i / seg;
                _v.Add(At(w, dir, e, n, r0, a, lift)); _c.Add(c0);
                _v.Add(At(w, dir, e, n, r1, a, lift)); _c.Add(c1);
            }
            for (int i = 0; i < seg; i++)
            {
                int k = b0 + i * 2;
                _t.Add(k); _t.Add(k + 2); _t.Add(k + 1);
                _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
            }
        }

        /// <summary>A soft disc facing the camera: its colour at its heart, clear at its edge.</summary>
        void Sprite(Vector3 at, float r, Color col, Vector3 cam)
        {
            Vector3 view = (at - cam).normalized;
            Vector3 sx = Vector3.Cross(view, Vector3.up); if (sx.sqrMagnitude < 1e-6f) sx = Vector3.right; sx.Normalize();
            Vector3 sy = Vector3.Cross(sx, view);
            int c0 = _v.Count;
            _v.Add(at); _c.Add(col);
            Color clear = col; clear.a = 0f;
            const int n = 14;
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; _v.Add(at + (sx * Mathf.Cos(a) + sy * Mathf.Sin(a)) * r); _c.Add(clear); }
            for (int i = 0; i < n; i++) { _t.Add(c0); _t.Add(c0 + 1 + i); _t.Add(c0 + 2 + i); }
        }

        /// <summary>A straight band from a to b facing the camera.</summary>
        void Segment(Vector3 a, Vector3 b, float wdt, Color col, Vector3 cam)
        {
            Vector3 along = b - a;
            if (along.sqrMagnitude < 1e-4f) return;
            Vector3 side = Vector3.Cross(along, (a + b) * 0.5f - cam).normalized * (wdt * 0.5f);
            int k = _v.Count;
            _v.Add(a - side); _v.Add(a + side); _v.Add(b - side); _v.Add(b + side);
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            _t.Add(k); _t.Add(k + 2); _t.Add(k + 1); _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
        }
    }
}
