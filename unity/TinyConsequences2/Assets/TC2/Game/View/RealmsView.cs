using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The realms map layer (D2-167: the polity map layer the C10.6 shots asked for - a standard reads only up close, `10` §12h). With it on, the land
    /// each village of a lordship works - its fields (`PlanetState.FieldOwner`) and its built ground - is washed on the ground in its realm's colour (the
    /// map's brighter dye: `WorldObjectsView.PolityMapColour`), the lord's seat more strongly than its tributaries, with a band of that colour a few
    /// pixels wide along the edge of the land, fading out as the camera comes within a few hundred metres (`TerrainLod`, `Terrain.shader`); and chevrons in that
    /// colour run from each tributary toward its own lord along the way its tenth is carried (C10.6: the way on foot; over the water, where no way on foot
    /// runs, straight), drifting toward the lord. A village that pays nobody and is paid by nobody is left as it is. With them, the wars of the last ten
    /// years: dark spearheads from each village whose bands fell on another toward the raided, along the way the bands walked, larger the more raids the
    /// ledger holds (session 25: war was invisible on the map - where the feuds are, and whether they run inside realms or across their borders). Every
    /// mark is real state: the bonds (`Settlement.Lord`, `Conflict.Root`), the land a village works, the colours of the standards (D2-152), the ledger's
    /// raids. A map layer the player turns on and off (the HUD's Realms, R; shots `-tc2realms`): presentation only, it reads the world only.
    /// </summary>
    public sealed class RealmsView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;

        /// <summary>The layer is shown.</summary>
        public static bool On;
        /// <summary>Changes whenever what the ground shows changes (the layer turned on or off, a bond made or ended, a village in a realm founded or
        /// abandoned): `TerrainLod` recolours its patches when it does.</summary>
        public static int Version;

        /// <summary>How strongly a realm's colour fills its lord's seat's land and its tributaries' (the alpha of `WashOf`'s colour; the terrain's border
        /// band is drawn alike for both).</summary>
        const float SeatWash = 1.0f, MemberWash = 0.6f;
        /// <summary>A chevron's length as a share of its distance from the camera (~16 px tall on a 900-px screen at any distance), its least and most
        /// (m); none nearer than `NearestM` - up close the standards and the carriers tell the bond.</summary>
        const float ChevronK = 0.018f, ChevronMin = 14f, ChevronMax = 420f, NearestM = 450f;
        /// <summary>The wars shown: raids of the last this many years; their spearheads' ink (sable: a dye no realm takes, D2-167).</summary>
        const double FeudYears = 10.0;
        static readonly Color FeudInk = new Color(0.10f, 0.05f, 0.05f, 1f);
        /// <summary>(session 25, third context) the spears' pale edge: dark ink alone read weakly on a darker realm's wash (`s25m_s2_y1000r`) - a pale
        /// edge under the dark core reads on any dye, as heraldry sets a dark charge on a pale field.</summary>
        static readonly Color FeudEdge = new Color(0.97f, 0.93f, 0.82f, 1f);
        const float SpearScale = 1.5f;   // (the spears half again the bonds' chevrons: a war reads before a bond)

        // each realm village's wash (colour, alpha its strength), by village: read by TerrainLod as it colours the ground
        static readonly Dictionary<int, Color> _wash = new Dictionary<int, Color>();
        // the bonds: tributary, its lord, the line from one to the other on the sphere and its running length (m)
        sealed class Bond { public int T, L; public Color C; public Vec3d[] Pts; public double[] S; public double Len; }
        readonly List<Bond> _bonds = new List<Bond>();
        // the feuds: the raiders' village, the raided, the raids in `FeudYears`, the line of the way between them (as a bond's: `Line`)
        sealed class Feud { public int A, B, N; public Vec3d[] Pts; public double[] S; public double Len; }
        readonly List<Feud> _feuds = new List<Feud>();
        readonly Dictionary<long, int> _feudAt = new Dictionary<long, int>();
        Instancer _spears;
        readonly List<Vec3d> _pts = new List<Vec3d>(), _tmp = new List<Vec3d>();
        float _refresh;
        long _sig = -1;
        bool _wasOn;
        Instancer _chevrons;
        Material _mat;

        /// <summary>The realms (for the HUD's legend): each head of a lordship, its colour, the villages that pay it (directly or through their
        /// lords) and the people of them all, largest first.</summary>
        public static readonly List<(int sid, Color c, int villages, int people)> Legend = new List<(int, Color, int, int)>();
        /// <summary>Tools (the shots' log): the bonds read at the last refresh, those drawn along a way on foot (else straight over the water), the
        /// villages washed, and the chevrons drawn this frame.</summary>
        public static int Bonds, BondsAfoot, Washed, ChevronsDrawn;
        /// <summary>The wars shown (the HUD's legend, the shots' log): raids in the last `FeudYears`, between how many pairs of villages, the pairs of
        /// one realm now, and the spearheads drawn this frame.</summary>
        public static int FeudRaids, FeudPairs, FeudsWithin, SpearsDrawn;
        /// <summary>Tools: the realms that fly the same colour and how far apart their heads lie ("" none).</summary>
        public static string SameColour = "";

        /// <summary>A village's wash on the ground: its realm's colour and how strongly (false where it belongs to no realm, or the layer is off).</summary>
        public static bool WashOf(int sid, out Color c)
        {
            if (On && sid != 0 && _wash.TryGetValue(sid, out c)) return true;
            c = default;
            return false;
        }

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _chevrons = new Instancer(Chevron(), _mat, false);
            _spears = new Instancer(Spear(), _mat, false);
        }

        /// <summary>A spearhead lying flat, 1 m long, pointing along +z (toward the raided): a long narrow leaf, unlike a bond's open chevron.</summary>
        static Mesh Spear()
        {
            const float h = 0.05f;
            var v = new[] { new Vector3(0f, h, 0.5f), new Vector3(0.16f, h, -0.12f), new Vector3(0f, h, -0.5f), new Vector3(-0.16f, h, -0.12f) };
            var n = new Vector3[v.Length];
            var c = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) { n[i] = Vector3.up; c[i] = Color.white; }
            var m = new Mesh { name = "tc2-spear" };
            m.vertices = v; m.normals = n; m.colors = c;
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A chevron lying flat, 1 m long, pointing along +z (toward the lord), a little above the ground.</summary>
        static Mesh Chevron()
        {
            const float h = 0.04f;
            var v = new[]
            {
                new Vector3(0f, h, 0.5f), new Vector3(0.5f, h, 0f), new Vector3(0.5f, h, -0.38f), new Vector3(0f, h, 0.12f),   // the right arm
                new Vector3(0f, h, 0.5f), new Vector3(0f, h, 0.12f), new Vector3(-0.5f, h, -0.38f), new Vector3(-0.5f, h, 0f), // the left arm
            };
            var n = new Vector3[v.Length];
            var c = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) { n[i] = Vector3.up; c[i] = Color.white; }   // (alpha 1: the instance's colour all over)
            var m = new Mesh { name = "tc2-chevron" };
            m.vertices = v; m.normals = n; m.colors = c;
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Host.CatchingUp) return;
            if (On != _wasOn) { _wasOn = On; _refresh = 0f; Version++; if (!On) { _wash.Clear(); _bonds.Clear(); _feuds.Clear(); Legend.Clear(); _sig = -1; } }
            if (!On) return;
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f) { _refresh = 1f; Refresh(w); Feuds(w); }
            DrawChevrons(w);
            DrawSpears(w);
        }

        /// <summary>Reads the bonds, the realms' washes and the legend (every second: its people change); bumps `Version` when what the ground shows
        /// changed - every inhabited village, its own lord.</summary>
        void Refresh(TC2.Core.Simulation.World w)
        {
            var C = w.Conflict;
            long sig = 17;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                int lord = s.Lord != 0 && w.Settlements.Exists(s.Lord) && w.Settlements.Get(s.Lord).Inhabited ? s.Lord : 0;
                sig = sig * 1000003L + sid * 31L + lord;
            }
            bool changed = sig != _sig;
            _sig = sig;
            _wash.Clear(); _bonds.Clear(); Legend.Clear(); BondsAfoot = 0;
            var P = w.Planet;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                bool pays = s.Lord != 0 && w.Settlements.Exists(s.Lord) && w.Settlements.Get(s.Lord).Inhabited;
                int held = pays ? 0 : C.PolitySize(sid);
                if (!pays && held == 0) continue;
                Color col = WorldObjectsView.PolityMapColour(w, sid);   // (the realm's dye as the map draws it - brighter than the standard's cloth)
                col.a = pays ? MemberWash : SeatWash;
                _wash[sid] = col;
                if (!pays)
                {
                    // the head of a realm: its villages and their people (the legend)
                    int people = s.Population;
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                        if (o != sid && w.Settlements.Get(o).Inhabited && C.Root(o) == sid) people += w.Settlements.Get(o).Population;
                    Legend.Add((sid, col, held + 1, people));
                    continue;
                }
                // a tributary: the line to its own lord - the way on foot its tenth is carried (C10.6), else straight (over the water)
                Color chev = col; chev.a = 1f;   // (the instance's alpha: the chevron's colour all over)
                var b = new Bond { T = sid, L = s.Lord, C = chev };
                if (Line(w, sid, s.Lord, out b.Pts, out b.S, out b.Len)) BondsAfoot++;   // (the way its tenth is carried: C10.6 asks the same route)
                _bonds.Add(b);
            }
            Legend.Sort((x, y) => x.villages != y.villages ? y.villages.CompareTo(x.villages) : x.people != y.people ? y.people.CompareTo(x.people) : x.sid.CompareTo(y.sid));
            Bonds = _bonds.Count; Washed = _wash.Count;
            // (tools: realms flying the same colour - would the map read them as one? none while there are no more realms than dyes)
            SameColour = "";
            for (int i = 0; i < Legend.Count; i++)
                for (int j = i + 1; j < Legend.Count; j++)
                {
                    if (Legend[i].c.r != Legend[j].c.r || Legend[i].c.g != Legend[j].c.g || Legend[i].c.b != Legend[j].c.b) continue;
                    double km = Vec3d.ArcDistance(w.Settlements.Get(Legend[i].sid).Anchor, w.Settlements.Get(Legend[j].sid).Anchor, P.Radius) / 1000.0;
                    SameColour += (SameColour.Length > 0 ? "; " : "") + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(Legend[i].sid)) + " and " +
                                  w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(Legend[j].sid)) + " " + km.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km";
                }
            if (changed) Version++;
        }

        /// <summary>The line from one village to another as the map draws it: the way on foot between them as the roads are drawn (Chaikin twice:
        /// `TerrainLod.BuildRoads`), else straight over the water; its points and running lengths (m). True when it follows a way on foot.</summary>
        bool Line(TC2.Core.Simulation.World w, int from, int to, out Vec3d[] pts, out double[] runs, out double len)
        {
            var P = w.Planet;
            _pts.Clear();
            var r = w.Routes.Get(w, from, to);   // (cached by the world: its carriers and bands ask the same route)
            bool afoot = r != null && r.Pts != null && r.Pts.Length >= 2;
            if (afoot)
            {
                _pts.AddRange(TC2.Core.Agents.WayLines.Of(w, r));
                if (r.A != from) _pts.Reverse();   // (a route runs from the lower village id to the higher)
                for (int it = 0; it < 2; it++)
                {
                    _tmp.Clear(); _tmp.Add(_pts[0]);
                    for (int k = 0; k + 1 < _pts.Count; k++)
                    {
                        _tmp.Add((_pts[k] * 0.75 + _pts[k + 1] * 0.25).Normalized);
                        _tmp.Add((_pts[k] * 0.25 + _pts[k + 1] * 0.75).Normalized);
                    }
                    _tmp.Add(_pts[_pts.Count - 1]);
                    _pts.Clear(); _pts.AddRange(_tmp);
                }
            }
            else
            {
                Vec3d a = w.Settlements.Get(from).Anchor, z = w.Settlements.Get(to).Anchor;
                double ang = Vec3d.ArcDistance(a, z, 1.0);
                int n = Mathf.Clamp((int)(ang * P.Radius / 60.0), 2, 400);
                for (int k = 0; k <= n; k++) _pts.Add(Slerp(a, z, ang, k / (double)n));
            }
            pts = _pts.ToArray();
            runs = new double[pts.Length];
            for (int k = 1; k < pts.Length; k++) runs[k] = runs[k - 1] + Vec3d.ArcDistance(pts[k - 1], pts[k], P.Radius);
            len = runs[runs.Length - 1];
            return afoot;
        }

        /// <summary>The wars of the last `FeudYears` (every second, with the bonds): the ledger's raids read back from its end, by the raiders' village and
        /// the raided, both still lived in.</summary>
        void Feuds(TC2.Core.Simulation.World w)
        {
            _feuds.Clear(); _feudAt.Clear();
            FeudRaids = 0; FeudsWithin = 0;
            var H = w.History;
            long since = w.Now - (long)(FeudYears * TC2.Core.Foundation.SimTime.Year);
            for (int i = H.Count - 1; i >= 0; i--)
            {
                var e = H.At(i);
                if (e.Time < since) break;   // (the ledger keeps its events in time order)
                if (e.Kind != TC2.Core.History.HistoryKind.Raid) continue;
                int a = e.Subject.Id, b = e.Object.Id;
                if (a == b || !w.Settlements.Exists(a) || !w.Settlements.Exists(b) || !w.Settlements.Get(a).Inhabited || !w.Settlements.Get(b).Inhabited) continue;
                FeudRaids++;
                long key = (long)a * 1000003L + b;
                if (_feudAt.TryGetValue(key, out int k)) { _feuds[k].N++; continue; }
                var f = new Feud { A = a, B = b, N = 1 };
                Line(w, a, b, out f.Pts, out f.S, out f.Len);
                _feudAt[key] = _feuds.Count;
                _feuds.Add(f);
                if (w.Conflict.Root(a) == w.Conflict.Root(b)) FeudsWithin++;
            }
            FeudPairs = _feuds.Count;
        }

        /// <summary>The spearheads of every feud, spaced as the chevrons (the same steps), larger the more raids, advancing toward the raided - one spacing
        /// in two seconds; set a little to the side of the way, so that a bond's chevrons and a war on the same way both read.</summary>
        void DrawSpears(TC2.Core.Simulation.World w)
        {
            _spears.Clear();
            SpearsDrawn = 0;
            if (Rig == null || Rig.Cam == null || _feuds.Count == 0) return;
            var P = w.Planet;
            Vector3 cam = Rig.Cam.transform.position;
            float alt = Mathf.Max(50f, cam.magnitude - (float)P.Radius);
            float reach = Mathf.Max(3500f, 4f * alt);
            float want = 2.4f * Mathf.Clamp(ChevronK * alt * 1.4f, ChevronMin, ChevronMax);
            float spacing = 64f * Mathf.Pow(2f, Mathf.Round(Mathf.Log(Mathf.Max(want, 64f) / 64f, 2f)));
            float phase = Mathf.Repeat(Time.unscaledTime / 2f, 1f) * spacing;
            for (int i = 0; i < _feuds.Count; i++)
            {
                var f = _feuds[i];
                float grow = 1f + 0.12f * Mathf.Min(f.N - 1, 8);
                int seg = 0;
                for (double d = phase; d < f.Len; d += spacing)
                {
                    while (seg + 2 < f.S.Length && f.S[seg + 1] < d) seg++;
                    double sl = f.S[seg + 1] - f.S[seg];
                    double t = sl > 0 ? (d - f.S[seg]) / sl : 0;
                    Vec3d at = (f.Pts[seg] * (1 - t) + f.Pts[seg + 1] * t).Normalized;
                    Vector3 pos = PlanetGeo.Surface(P, at);
                    float dist = Vector3.Distance(cam, pos);
                    if (dist < NearestM || dist > reach) continue;
                    if (d < 120.0 || d > f.Len - 120.0) continue;
                    float size = Mathf.Clamp(ChevronK * dist, ChevronMin, ChevronMax) * grow * SpearScale;
                    Vector3 up = pos.normalized;
                    Vector3 ahead = PlanetGeo.Surface(P, f.Pts[seg + 1]) - PlanetGeo.Surface(P, f.Pts[seg]);
                    Vector3 fwd = Vector3.ProjectOnPlane(ahead, up);
                    if (fwd.sqrMagnitude < 1e-8f) continue;
                    fwd.Normalize();
                    Vector3 side = Vector3.Cross(up, fwd);
                    var rot = Quaternion.LookRotation(fwd, up);
                    Vector3 c0 = pos + side * (0.45f * size);
                    _spears.Add(0, FeudEdge, Matrix4x4.TRS(c0 + up * (0.12f * size) - fwd * (0.12f * size), rot, new Vector3(size * 1.45f, size * 0.7f, size * 1.3f * 1.4f)), 0.05f);   // (the pale edge, under)
                    _spears.Add(0, FeudInk, Matrix4x4.TRS(c0 + up * (0.2f * size), rot, new Vector3(size, size, size * 1.3f)), 0.05f);
                    SpearsDrawn++;
                }
            }
            _spears.Draw(Rig.Cam);
        }

        static Vec3d Slerp(Vec3d a, Vec3d b, double ang, double t)
        {
            if (ang < 1e-9) return a;
            double s = System.Math.Sin(ang);
            return (a * (System.Math.Sin((1 - t) * ang) / s) + b * (System.Math.Sin(t * ang) / s)).Normalized;
        }

        /// <summary>The chevrons of every bond, spaced and sized by the camera's distance (the spacing in steps of two, so they do not swim as it
        /// moves), drifting toward the lord - one spacing in four seconds.</summary>
        void DrawChevrons(TC2.Core.Simulation.World w)
        {
            _chevrons.Clear();
            ChevronsDrawn = 0;
            if (Rig == null || Rig.Cam == null || _bonds.Count == 0) return;
            var P = w.Planet;
            Vector3 cam = Rig.Cam.transform.position;
            float alt = Mathf.Max(50f, cam.magnitude - (float)P.Radius);
            float reach = Mathf.Max(3500f, 4f * alt);
            // the spacing: twice a chevron's length at the camera's height, in steps of two (from 64 m)
            float want = 2.4f * Mathf.Clamp(ChevronK * alt * 1.4f, ChevronMin, ChevronMax);
            float spacing = 64f * Mathf.Pow(2f, Mathf.Round(Mathf.Log(Mathf.Max(want, 64f) / 64f, 2f)));
            float phase = Mathf.Repeat(Time.unscaledTime / 4f, 1f) * spacing;
            for (int i = 0; i < _bonds.Count; i++)
            {
                var b = _bonds[i];
                int seg = 0;
                for (double d = phase; d < b.Len; d += spacing)
                {
                    while (seg + 2 < b.S.Length && b.S[seg + 1] < d) seg++;
                    double sl = b.S[seg + 1] - b.S[seg];
                    double f = sl > 0 ? (d - b.S[seg]) / sl : 0;
                    Vec3d at = (b.Pts[seg] * (1 - f) + b.Pts[seg + 1] * f).Normalized;
                    Vector3 pos = PlanetGeo.Surface(P, at);
                    float dist = Vector3.Distance(cam, pos);
                    if (dist < NearestM || dist > reach) continue;
                    // (the ends left clear: the villages themselves)
                    if (d < 120.0 || d > b.Len - 120.0) continue;
                    float size = Mathf.Clamp(ChevronK * dist, ChevronMin, ChevronMax);
                    Vector3 up = pos.normalized;
                    Vector3 ahead = PlanetGeo.Surface(P, b.Pts[seg + 1]) - PlanetGeo.Surface(P, b.Pts[seg]);
                    Vector3 fwd = Vector3.ProjectOnPlane(ahead, up);
                    if (fwd.sqrMagnitude < 1e-8f) continue;
                    Matrix4x4 m = Matrix4x4.TRS(pos + up * (0.18f * size), Quaternion.LookRotation(fwd.normalized, up), new Vector3(size, size, size));
                    _chevrons.Add(0, b.C, m, 0.55f);
                    ChevronsDrawn++;
                }
            }
            _chevrons.Draw(Rig.Cam);
        }
    }
}
