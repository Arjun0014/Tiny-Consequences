using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The households' animals (D2-048), elaborated from the simulation: every live animal of a village near the camera is
    /// drawn. By day each household's cattle and goats graze a ground of their own on the village's pasture (open land within
    /// reach, not under fields, chosen by a stable key) and its pigs root round its yard; at night they are penned behind
    /// its home, walking out after dawn and home before dusk; in a hard frost they stay in. The young are smaller. Pens are
    /// fenced. Positions come from keys and the world's time only: watching never changes the world.
    /// </summary>
    public sealed class LivestockView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        Instancer[] _kinds;
        Instancer _fences;
        Material _mat;
        static readonly string[] Models = { "animal_cow", "animal_goat", "animal_pig" };
        static readonly float[] Spacing = { 3.4f, 1.7f, 2.0f };   // m between animals grazing together
        static readonly float[] PenSpacing = { 1.9f, 0.95f, 1.2f };
        // coats: each kind in a few natural colours (tints over the model's own)
        static readonly Color[][] Coats =
        {
            new[] { Color.white, new Color(0.42f, 0.34f, 0.28f), new Color(0.30f, 0.27f, 0.25f), new Color(0.98f, 0.98f, 0.96f) },   // dun, brown, black, pale cattle
            new[] { Color.white, new Color(0.62f, 0.48f, 0.36f), new Color(0.34f, 0.31f, 0.29f) },   // white, brown, black goats
            new[] { Color.white, new Color(0.62f, 0.52f, 0.50f) },                                    // pink, grey pigs
        };
        readonly List<int> _cells = new List<int>(), _pasture = new List<int>();
        public int Drawn;
        public string Why = "";   // (shots: where the herds were)
        float _timer;

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            _kinds = new Instancer[Models.Length];
            for (int i = 0; i < Models.Length; i++) _kinds[i] = new Instancer(TC2.Game.Models.Get(Models[i], ProcMeshes.Person), _mat, true);
            _fences = new Instancer(TC2.Game.Models.Get("fence", ProcMeshes.Granary), _mat, true);
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _kinds == null || Host.CatchingUp) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) { _timer = 0.1f; Rebuild(w); }
            var cam = Rig.Cam;
            for (int i = 0; i < _kinds.Length; i++) _kinds[i].Draw(cam);
            _fences.Draw(cam);
        }

        float Exaggeration(Vector3 at)
        {
            // as the wild animals: a little larger than life, more so far away
            float d = Vector3.Distance(Rig.Cam.transform.position, at);
            return Mathf.Clamp(1.4f * d / 500f, 1.4f, 3f);
        }

        static double Smooth(double t) { t = t < 0 ? 0 : t > 1 ? 1 : t; return t * t * (3 - 2 * t); }

        void Rebuild(TC2.Core.Simulation.World w)
        {
            for (int i = 0; i < _kinds.Length; i++) _kinds[i].Clear();
            _fences.Clear();
            Drawn = 0;
            var why = Application.isBatchMode ? new System.Text.StringBuilder() : null;   // (shots only: no garbage in the game)
            var P = w.Planet;
            Vector3 camPos = Rig.Cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            if (alt > 1800f) return;
            Vec3d focus = PlanetGeo.ToVec(Rig.FocusPoint).Normalized;
            double reach = Mathf.Clamp(alt * 2.6f, 500f, 2600f);
            double t = Host.RenderTime;
            double season = PlanetSystems.Season((long)t);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || Vec3d.ArcDistance(s.Anchor, focus, P.Radius) > reach + Livestock.PastureReach) continue;
                // the pasture: open land within reach, not under fields, not water, beyond the houses
                w.Hazards.CellsWithin(w, s.Cell, Livestock.PastureReach, _cells);
                _pasture.Clear();
                for (int i = 0; i < _cells.Count; i++)
                {
                    int c = _cells[i];
                    if (!P.IsLand(c) || P.FieldOwner[c] != 0 || P.LakeDepth[c] > 0.3f || P.Ice[c] > 5f) continue;
                    if (Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius) < 160.0) continue;
                    _pasture.Add(c);
                }
                // herds graze near home first: the nearest grounds (BFS order is by distance, near enough)
                if (_pasture.Count > 10) _pasture.RemoveRange(10, _pasture.Count - 10);
                double hour = SimTime.FloorMod((long)t + w.LocalOffsetOf(sid), SimTime.Day) / (double)SimTime.Hour;
                // one presentation scale for the whole village (pens, fences and animals keep their proportions)
                float ex = Exaggeration(At(P, in s, s.SquareX, s.SquareY));
                bool frost = w.PlanetSys.DayMean(s.Cell, season) < -4.0;
                var hhs = w.HouseholdsOf(sid);
                for (int i = 0; i < hhs.Count; i++)
                {
                    int hid = hhs[i];
                    ref var h = ref w.Households.Get(hid);
                    var mine = w.BeastsOf(hid);
                    if (mine.Count == 0 || h.Party != 0) continue;
                    // the pen behind the home (away from the square), sized to the herd
                    double ax = h.HomeX - s.SquareX, ay = h.HomeY - s.SquareY;
                    double al = System.Math.Sqrt(ax * ax + ay * ay);
                    if (al < 1e-3) { ax = 0; ay = -1; al = 1; }
                    ax /= al; ay /= al;
                    double penR = (2.6 + 0.55 * System.Math.Sqrt(mine.Count)) * ex;
                    double px = h.HomeX + ax * (6.0 + penR), py = h.HomeY + ay * (6.0 + penR);
                    Fence(P, in s, px, py, penR, hid, ex);
                    // the household's grazing ground (a stable choice among the pasture's cells)
                    ulong hk = KeyedRng.Mix((ulong)hid * 0x9E3779B97F4A7C15UL + 0x5EED);
                    double gx = px, gy = py;
                    if (_pasture.Count > 0)
                    {
                        int gc = _pasture[(int)(KeyedRng.Unit(hk) * _pasture.Count) % _pasture.Count];
                        s.SphereToLocal(P.Grid.Center[gc], P.Radius, out gx, out gy);
                    }
                    // the herd's centre on its ground wanders on a slow loop of the day
                    double ph = t / (7.0 * SimTime.Hour) + KeyedRng.Unit(KeyedRng.Mix(hk + 1)) * 6.28318;
                    double hx = gx + 70.0 * System.Math.Sin(ph), hy = gy + 70.0 * System.Math.Sin(ph * 0.71 + 1.1);
                    for (int j = 0; j < mine.Count; j++)
                    {
                        var b = w.Beasts.Get(mine[j]);
                        int kind = b.Kind;
                        ulong bk = KeyedRng.Mix((ulong)b.Id * 0xC2B2AE3D27D4EB4FUL + 0xBEA57);
                        // where it stands at night (a spiral in the pen) and by day (round the herd, or the pigs round the yard)
                        double ang = j * 2.39996323, rr = PenSpacing[kind] * ex * System.Math.Sqrt(j + 0.5);
                        rr = System.Math.Min(rr, penR - 0.6 * ex);
                        double nx = px + System.Math.Cos(ang) * rr, ny = py + System.Math.Sin(ang) * rr;
                        double dx, dy;
                        if (kind == Livestock.Pig)
                        {
                            double a2 = KeyedRng.Unit(bk) * 6.28318 + t / (2.5 * SimTime.Hour) * (KeyedRng.Unit(KeyedRng.Mix(bk + 3)) < 0.5 ? 1 : -1);
                            double r2 = (7.0 + 9.0 * KeyedRng.Unit(KeyedRng.Mix(bk + 4))) * ex;
                            dx = h.HomeX + ax * 5.0 + System.Math.Cos(a2) * r2; dy = h.HomeY + ay * 5.0 + System.Math.Sin(a2) * r2;
                        }
                        else
                        {
                            double a2 = KeyedRng.Unit(bk) * 6.28318, r2 = Spacing[kind] * ex * System.Math.Sqrt(j + 0.5) * (0.7 + 0.6 * KeyedRng.Unit(KeyedRng.Mix(bk + 1)));
                            double wob = t / (1.3 * SimTime.Hour) + KeyedRng.Unit(KeyedRng.Mix(bk + 2)) * 6.28318;
                            dx = hx + System.Math.Cos(a2) * r2 + 1.5 * System.Math.Sin(wob); dy = hy + System.Math.Sin(a2) * r2 + 1.5 * System.Math.Cos(wob * 0.8);
                        }
                        // out after dawn, home before dusk (one after another), in all day in a hard frost
                        double lag = (j % 12) * 0.04;
                        double f = frost ? 0.0 : hour < 12.0 ? Smooth((hour - 6.6 - lag) / 0.9) : 1.0 - Smooth((hour - 17.9 - lag) / 0.9);
                        Vector3 pos = At(P, in s, nx + (dx - nx) * f, ny + (dy - ny) * f);
                        // facing where it goes (walking), else a slow turn of its own
                        Vector3 up = pos.normalized;
                        Vector3 fwd;
                        bool walking = f > 0.02 && f < 0.98;
                        if (walking)
                        {
                            Vector3 to = At(P, in s, dx, dy), from = At(P, in s, nx, ny);
                            fwd = hour < 12.0 ? to - from : from - to;
                        }
                        else
                        {
                            float yaw = (float)(KeyedRng.Unit(KeyedRng.Mix(bk + 5)) * 6.28318 + t / (0.9 * SimTime.Hour) * (0.3 + 0.4 * KeyedRng.Unit(KeyedRng.Mix(bk + 6))));
                            Vector3 e = Vector3.Cross(Vector3.up, up); if (e.sqrMagnitude < 1e-6f) e = Vector3.right; e.Normalize();
                            Vector3 n = Vector3.Cross(up, e);
                            fwd = e * Mathf.Cos(yaw) + n * Mathf.Sin(yaw);
                        }
                        fwd -= up * Vector3.Dot(fwd, up);
                        if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.Cross(up, Vector3.right);
                        var rot = Quaternion.LookRotation(fwd.normalized, up);
                        float scale = Mathf.Pow((float)Livestock.GrownShare(in b, w.Now), 1f / 3f) * ex;
                        var coats = Coats[kind];
                        int coat = (int)(KeyedRng.Unit(KeyedRng.Mix(bk + 7)) * coats.Length) % coats.Length;
                        _kinds[kind].Add(coat, coats[coat], Matrix4x4.TRS(pos, rot, Vector3.one * scale));
                        if (j == 0 && why != null && why.Length < 600) why.Append(" hh" + hid + " " + Models[kind] + " f " + f.ToString("0.00") + " at " + Vector3.Distance(pos, At(P, in s, s.SquareX, s.SquareY)).ToString("0") + " m");
                        if (++Drawn >= 3000) { Why = why?.ToString() ?? ""; return; }
                    }
                }
                why?.Append(" | " + sid + ": pasture " + _pasture.Count + " hour " + hour.ToString("0.0") + " ex " + ex.ToString("0.0"));
            }
            Why = why?.ToString() ?? "";
        }

        static Vector3 At(PlanetState P, in Settlement s, double x, double y) => Ground.Surface(P, s.LocalToSphere(x, y, P.Radius));

        /// <summary>A ring of fence lengths round a pen (the kit's fence is 2.1 m long).</summary>
        void Fence(PlanetState P, in Settlement s, double cx, double cy, double r, int hid, float ex)
        {
            int n = Mathf.Max(6, Mathf.RoundToInt((float)(2.0 * System.Math.PI * r / (2.05 * ex))));
            double gap = (hid * 0.61803) % 1.0;   // the gate faces a different way at each home
            for (int k = 0; k < n; k++)
            {
                if (k == (int)(gap * n)) continue;   // the gate
                double a = (k + 0.5) * 2.0 * System.Math.PI / n;
                double x = cx + System.Math.Cos(a) * r, y = cy + System.Math.Sin(a) * r;
                Vector3 pos = At(P, in s, x, y);
                Vector3 up = pos.normalized;
                // along the ring: the fence's length (its x axis) on the tangent, so its face (z) points out
                Vector3 outward = At(P, in s, x + System.Math.Cos(a), y + System.Math.Sin(a)) - pos;
                outward -= up * Vector3.Dot(outward, up);
                if (outward.sqrMagnitude < 1e-8f) continue;
                _fences.Add(0, new Color(0.95f, 0.92f, 0.88f), Matrix4x4.TRS(pos, Quaternion.LookRotation(outward.normalized, up), Vector3.one * ex));
            }
        }
    }
}
