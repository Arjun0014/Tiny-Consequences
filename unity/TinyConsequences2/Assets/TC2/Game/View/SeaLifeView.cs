using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The sea's life near the camera (D2-039), elaborated from the simulation's fields: shoals of herring and sardines
    /// swirl just under the surface where the fields hold them (more shoals where there are more fish), now and then a
    /// fish leaps and splashes (a tuna where tuna hunt), and seals rest on the shore beside the waters that feed them or
    /// swim off it. Presentation only: what is drawn follows the fields and stable keys, so watching never changes the
    /// world. Shoals are placed a few times a second; their fish swim every frame.
    /// </summary>
    public sealed class SeaLifeView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        Instancer _fish, _seals;
        Material _mat, _under;
        readonly List<int> _cells = new List<int>();
        readonly HashSet<int> _seen = new HashSet<int>();
        public int Drawn;
        public int Shoals => _shoals.Count;
        public int SealsDrawn => _sealsDrawn;
        public string Why = "";   // tools: what the last rebuild saw (cells, fish there, shoals skipped as too shallow)
        int _sealsDrawn;
        float _timer;

        struct Shoal
        {
            public Vector3 Centre, East, North, Up;   // the shoal's centre just under the surface, and its local frame
            public Quaternion Base;                   // upright at the shoal, facing south (a fish turns from it about Up)
            public int Group, Count;                  // colour group (0 herring, 1 sardine), fish in it
            public float Radius, Spin, Phase, Scale;  // m, rad/s, rad, fish size
            public ulong Key;
            public bool Tuna;                         // tuna hunt here: the leaper is a tuna
            public bool Leaps;                        // near enough to see a fish leap
        }
        readonly List<Shoal> _shoals = new List<Shoal>();
        // seen from above a fish shows its dark back (the flash of a turning flank is the shader's)
        static readonly Color[] Tints = { new Color(0.55f, 0.6f, 0.72f, 1f), new Color(0.48f, 0.66f, 0.6f, 1f), new Color(0.26f, 0.3f, 0.5f, 1f) };
        const float FishLength = 0.3f;   // the model's length (m)
        const int MaxFish = 2400;          // a frame's budget of fish (the nearest shoals first)
        static readonly float[] FishU = Table(0xF15Au), FishV = Table(0xF15Bu);
        static float[] Table(ulong seed) { var a = new float[64]; for (int i = 0; i < 64; i++) a[i] = (float)KeyedRng.Unit(KeyedRng.Mix(seed + (ulong)i * 0x9E37UL)); return a; }

        void Start()
        {
            _mat = Materials.Load("Instanced");
            _mat.enableInstancing = true;
            // the fish are seen through the water: drawn after it, veiled by its colour (Underwater.shader)
            _under = Materials.Load("Underwater");
            _under.enableInstancing = true;
            _fish = new Instancer(TC2.Game.Models.Get("fish_shoal", ProcMeshes.Person), _under, false);
            _seals = new Instancer(TC2.Game.Models.Get("animal_seal", ProcMeshes.Person), _mat, true);
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _fish == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) { _timer = 0.25f; Rebuild(w); }
            SwimShoals(w.Planet, Time.unscaledTime);
            _fish.Draw(Rig.Cam);
            _seals.Draw(Rig.Cam);
        }

        void Rebuild(TC2.Core.Simulation.World w)
        {
            _shoals.Clear();
            _seals.Clear();
            _sealsDrawn = 0;
            var P = w.Planet;
            if (P.Species.Count == 0 || P.Abund.Length == 0) return;
            Vector3 camPos = Rig.Cam.transform.position;
            float alt = camPos.magnitude - (float)P.Radius;
            if (alt > 1800f) return;
            float reach = Mathf.Clamp(alt * 2.6f, 350f, 2400f);
            int herring = -1, sardine = -1, tuna = -1, seal = -1;
            int n = P.Cells;
            // the shoaling fish and the seals of this world (and the kinds descended from them)
            Vector3 f = Rig.FocusPoint;
            var focus = new Vec3d(f.x, f.y, f.z).Normalized;
            var g = P.Grid;
            int c0 = g.CellOf(focus);
            _cells.Clear(); _seen.Clear();
            _cells.Add(c0); _seen.Add(c0);
            double cosR = System.Math.Cos(reach / P.Radius);
            for (int head = 0; head < _cells.Count && _cells.Count < 900; head++)
            {
                int c = _cells[head];
                for (int k = 0; k < 4; k++)
                {
                    int nb = g.Nbr4[c * 4 + k];
                    if (_seen.Contains(nb)) continue;
                    _seen.Add(nb);
                    if (Vec3d.Dot(g.Center[nb], focus) >= cosR) _cells.Add(nb);
                }
            }
            double gameT = Host.RenderTime;
            double surfaceR = P.Radius + P.SeaLevel;
            int seaCells = 0, fishCells = 0, shallow = 0, sealCells = 0, sealsShown = 0, noShore = 0; double fishSum = 0, sealSum = 0;
            foreach (int c in _cells)
            {
                if (P.IsLand(c)) continue;
                Vec3d cc = g.Center[c];
                Storms.Frame(cc, out Vec3d east, out Vec3d north);
                // the fish of this cell, by kind: shoalers of the plankton, tuna, seals
                double shoalers = 0, sard = 0, tunas = 0, seals = 0;
                for (int si = 0; si < P.Species.Count; si++)
                {
                    var sp = P.Species[si];
                    if (sp.Habitat != 1) continue;
                    double N = P.Abund[si * n + c];
                    if (sp.Plankton > 0) { shoalers += N; if (sp.Kind == 9) sard += N; }
                    else if (sp.Kind == 11) tunas += N;
                    else if (sp.Kind == 12) seals += N;
                }
                seaCells++; fishSum += shoalers; if (shoalers >= 60) fishCells++;
                ulong key = KeyedRng.Mix((ulong)c * 0x9E3779B97F4A7C15UL ^ 0x5EA11FEUL);
                // shoals: one for every ~500 fish the cell holds (up to ten), each somewhere in the cell
                int nShoals = shoalers < 60 ? 0 : Mathf.Min(10, 1 + (int)(shoalers / 500.0));
                for (int s = 0; s < nShoals; s++)
                {
                    ulong sk = KeyedRng.Mix(key + 101 + (ulong)s * 7);
                    // the shoal's centre drifts round its own place in the cell on a slow loop of the world's time
                    double period = 5.0 * SimTime.Hour * (1.0 + KeyedRng.Unit(sk));
                    double ph = gameT / period + KeyedRng.Unit(KeyedRng.Mix(sk + 1)) * 6.28318;
                    double bx = P.CellSize * 0.8 * (KeyedRng.Unit(KeyedRng.Mix(sk + 12)) - 0.5), by = P.CellSize * 0.8 * (KeyedRng.Unit(KeyedRng.Mix(sk + 13)) - 0.5);
                    double rx = bx + P.CellSize * 0.12 * System.Math.Sin(ph), ry = by + P.CellSize * 0.12 * System.Math.Sin(ph * 0.71 + 2.1);
                    Vec3d d = (cc * P.Radius + east * rx + north * ry).Normalized;
                    var shoal = new Shoal { Key = sk, Group = KeyedRng.Unit(KeyedRng.Mix(sk + 2)) * shoalers < sard ? 1 : 0, Tuna = tunas > 0.05 };
                    float dist = Vector3.Distance(camPos, Dir(d, surfaceR));
                    // far off, fewer shoals and no bigger: a glitter, not confetti
                    if (dist > 700f && KeyedRng.Unit(KeyedRng.Mix(sk + 14)) < Mathf.Clamp01((dist - 700f) / 900f) * 0.7f) continue;
                    shoal.Scale = Mathf.Clamp(6f * dist / 500f, 6f, 9f);
                    shoal.Leaps = dist < 900f;
                    // in the shallows a shoal swims higher, and its fish are drawn smaller, never into the sand
                    float water = (float)(P.SeaLevel - Ground.Height(P, d));
                    if (water < 0.9f) { shallow++; continue; }
                    shoal.Scale = Mathf.Min(shoal.Scale, Mathf.Max(2f, water * 0.55f / FishLength));
                    float len0 = FishLength * shoal.Scale;
                    float depth = Mathf.Min(len0 * (0.55f + 0.25f * (float)KeyedRng.Unit(KeyedRng.Mix(sk + 3))) + 0.2f, water * 0.45f);
                    shoal.Centre = Dir(d, surfaceR - depth);
                    shoal.Up = shoal.Centre.normalized;
                    shoal.Base = PlanetGeo.Upright(shoal.Up, 0f);
                    shoal.East = ToV(east); shoal.North = ToV(north);
                    shoal.Count = 15 + (int)(KeyedRng.Unit(KeyedRng.Mix(sk + 4)) * 25);
                    shoal.Radius = FishLength * shoal.Scale * (1.6f + 1.2f * (float)KeyedRng.Unit(KeyedRng.Mix(sk + 5)));
                    shoal.Spin = (0.25f + 0.2f * (float)KeyedRng.Unit(KeyedRng.Mix(sk + 6))) * (KeyedRng.Unit(KeyedRng.Mix(sk + 7)) < 0.5 ? 1f : -1f);
                    shoal.Phase = (float)(KeyedRng.Unit(KeyedRng.Mix(sk + 8)) * 6.28318);
                    _shoals.Add(shoal);
                }
                // seals: a stretch of coast's seals hauled out together at its best shore (the field's count, D2-053), resting
                // on the shore or swimming off it, by turns
                int hauled = seals > 0.01 || OnShore(P, c) ? HaulOut(P, c) : 0;
                if (hauled > 0)
                {
                    int count = hauled;
                    sealCells++; sealSum += seals; sealsShown += count;
                    for (int m = 0; m < count; m++)
                    {
                        ulong mk = KeyedRng.Mix(key + 300 + (ulong)m * 13);
                        if (!Shore(P, c, mk, out Vec3d land, out Vec3d sea)) { noShore++; continue; }
                        double turn = 7.0 * SimTime.Hour * (1.0 + 0.6 * KeyedRng.Unit(KeyedRng.Mix(mk + 1)));
                        bool ashore = ((gameT / turn + KeyedRng.Unit(KeyedRng.Mix(mk + 2))) % 1.0) < 0.55;
                        Vec3d toSea = (sea - land);
                        float headingSea = Mathf.Atan2((float)Vec3d.Dot(toSea, east), (float)Vec3d.Dot(toSea, north)) * Mathf.Rad2Deg;
                        Vector3 pos;
                        float yaw;
                        float scale = Mathf.Clamp(1.4f * Vector3.Distance(camPos, Dir(land, surfaceR)) / 500f, 1.4f, 3f);
                        if (ashore)
                        {
                            // hauled out at the water's edge, side by side, looking out to sea
                            double off = ((m % 6) - 2.5) * 1.6 * scale;   // side by side, a second row behind the first
                            if (m >= 6) land = (land * P.Radius - (sea - land).Normalized * ((m / 6) * 2.2 * scale)).Normalized;
                            Vec3d side = Vec3d.Cross(land, toSea).Normalized;
                            Vec3d at = (land * P.Radius + side * off).Normalized;
                            pos = Ground.Surface(P, at);
                            yaw = headingSea + 180f + (float)(KeyedRng.Unit(KeyedRng.Mix(mk + 3)) - 0.5) * 50f;
                        }
                        else
                        {
                            // swimming off the shore: only the head and back show above the water
                            double swim = 2.0 * SimTime.Hour;
                            double sp2 = gameT / swim + KeyedRng.Unit(KeyedRng.Mix(mk + 4)) * 6.28318;
                            Vec3d at = (sea * P.Radius + east * (System.Math.Sin(sp2) * P.CellSize * 0.25) + north * (System.Math.Cos(sp2 * 0.8) * P.CellSize * 0.25)).Normalized;
                            if (Ground.Height(P, at) > P.SeaLevel - 1.0) at = sea;
                            pos = Dir(at, surfaceR - 0.45 * scale);
                            float vx = (float)System.Math.Cos(sp2), vy = -(float)System.Math.Sin(sp2 * 0.8) * 0.8f;
                            yaw = Mathf.Atan2(vx, vy) * Mathf.Rad2Deg + 180f;
                        }
                        _seals.Add(0, Color.white, Matrix4x4.TRS(pos, PlanetGeo.Upright(pos, yaw), Vector3.one * scale));
                        _sealsDrawn++;
                    }
                }
                if (_shoals.Count >= 120) break;
            }
            Why = "cells " + _cells.Count + " sea " + seaCells + " with fish " + fishCells + " mean " + (seaCells > 0 ? fishSum / seaCells : 0).ToString("0") + " too shallow " + shallow + " alt " + alt.ToString("0") + " | seal cells " + sealCells + " (" + sealSum.ToString("0.00") + ") shown " + sealsShown + " no shore " + noShore;
        }

        /// <summary>Every frame: the shoals' fish swirl about their centres; now and then one leaps and splashes.</summary>
        void SwimShoals(PlanetState P, float t)
        {
            _fish.Clear();
            int budget = MaxFish;
            for (int s = 0; s < _shoals.Count && budget > 0; s++)
            {
                var sh = _shoals[s];
                float len = FishLength * sh.Scale;
                int count = Mathf.Min(sh.Count, budget);
                budget -= count;
                Vector3 fishScale = Vector3.one * sh.Scale;
                for (int i = 0; i < count; i++)
                {
                    // the fish's own place in the shoal (two keyed numbers from a table: no hashing every frame)
                    float u = FishU[i & 63], v = FishV[(i + (int)(sh.Key & 63)) & 63];
                    float r = sh.Radius * (0.35f + 0.65f * Mathf.Sqrt(u));
                    float a = sh.Phase + i * 2.39996f + sh.Spin * t * (1.15f - 0.3f * u);   // inner fish turn a little faster
                    float wob = Mathf.Sin(t * 2.3f + i * 1.7f) * 0.12f * len;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 pos = sh.Centre + sh.East * (ca * r) + sh.North * (sa * r * 0.62f) + sh.Up * ((v - 0.5f) * 0.5f * len + wob);
                    // they face along the swirl (the tangent of their ellipse)
                    float dx = -sa * Mathf.Sign(sh.Spin), dy = ca * 0.62f * Mathf.Sign(sh.Spin);
                    float yaw = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 180f;
                    _fish.Add(sh.Group, Tints[sh.Group], Matrix4x4.TRS(pos, Quaternion.AngleAxis(yaw, sh.Up) * sh.Base, fishScale));
                }
                // a leap: every 15-40 s one fish of the shoal (a tuna where tuna hunt) arcs out of the water
                float every = 15f + 25f * (float)KeyedRng.Unit(KeyedRng.Mix(sh.Key + 9));
                float lt = t / every + (float)KeyedRng.Unit(KeyedRng.Mix(sh.Key + 10));
                float since = (lt - Mathf.Floor(lt)) * every;   // seconds since this shoal's last leap began
                const float Leap = 1.1f;
                if (sh.Leaps && since < Leap + 0.6f)
                {
                    bool tuna = sh.Tuna && KeyedRng.Unit(KeyedRng.Mix(sh.Key + (ulong)Mathf.Floor(lt))) < 0.6;
                    float scale = sh.Scale * (tuna ? 3.2f : 1.3f), l = FishLength * scale;
                    float ang = (float)(KeyedRng.Unit(KeyedRng.Mix(sh.Key + 11 + (ulong)Mathf.Floor(lt))) * 6.28318);
                    Vector3 dir = sh.East * Mathf.Cos(ang) + sh.North * Mathf.Sin(ang);
                    Vector3 surface = sh.Centre + sh.Up * (float)(P.Radius + P.SeaLevel - sh.Centre.magnitude);
                    if (since < Leap)
                    {
                        float x = since / Leap;                  // 0..1 along the arc
                        float h = 4f * x * (1f - x) * l * 1.1f;  // up and down again
                        Vector3 pos = surface + dir * ((x - 0.5f) * 2.4f * l) + sh.Up * (h - 0.15f * l);
                        float yaw = Mathf.Atan2(Vector3.Dot(dir, sh.East), Vector3.Dot(dir, sh.North)) * Mathf.Rad2Deg + 180f;
                        float pitch = -Mathf.Atan2((1f - 2f * x) * 4f * 1.1f, 2.4f) * Mathf.Rad2Deg;   // nose up rising, down falling
                        var rot = PlanetGeo.Upright(sh.Up, yaw) * Quaternion.Euler(pitch, 0f, 0f);
                        _fish.Add(tuna ? 2 : sh.Group, Tints[tuna ? 2 : sh.Group], Matrix4x4.TRS(pos, rot, Vector3.one * scale));
                        if (x < 0.12f) Puffs.Add(surface - dir * (1.2f * l) + sh.Up * 0.1f, l * 0.9f, 0.45f * (1f - x / 0.12f), 0.01f, 0f, 0.31f, 1f);
                    }
                    else
                    {
                        // the splash where it falls back
                        float x = (since - Leap) / 0.6f;
                        Puffs.Add(surface + dir * (1.2f * l) + sh.Up * (0.2f + 0.6f * x) * l, l * (0.8f + 1.2f * x), 0.55f * (1f - x), 0.01f, 0f, 0.67f, 1f);
                    }
                }
            }
            Drawn = _sealsDrawn + _fish.Count;
        }

        /// <summary>How many of a sea cell's seals are shown: the whole part, and the fraction as a stable chance of the cell.</summary>
        /// <summary>
        /// Seals lie up in colonies (D2-053): the field spreads them thinly along a coast, so a shore sea cell whose seals with
        /// its eight neighbours' are the most within two cells' reach gathers them - the field's count (its fraction as a stable
        /// chance) drawn hauled out there; other cells show none. Haul-outs are three cells apart or more, so no seal is drawn
        /// twice. How many to draw at c (0 if it is not a haul-out). Reads only.
        /// </summary>
        public static int HaulOut(PlanetState P, int c)
        {
            if (P.IsLand(c) || !OnShore(P, c)) return 0;
            double here = SealsAround(P, c);
            if (here < 0.02) return 0;
            var g = P.Grid;
            for (int k = 0; k < 8; k++)
            {
                int nb = g.Nbr8[c * 8 + k];
                if (Beats(P, nb, c, here)) return 0;
                for (int j = 0; j < 8; j++) { int nn = g.Nbr8[nb * 8 + j]; if (nn != c && Beats(P, nn, c, here)) return 0; }
            }
            int count = (int)here;   // the whole part, and the fraction as a stable chance of the cell (the count kept on average)
            if (KeyedRng.Unit(KeyedRng.Mix(KeyedRng.Mix((ulong)c * 0x9E3779B97F4A7C15UL ^ 0x5EA11FEUL) + 3)) < here - count) count++;
            return System.Math.Min(18, count);
        }

        static bool Beats(PlanetState P, int o, int c, double here)
        {
            if (P.IsLand(o) || !OnShore(P, o)) return false;
            double v = SealsAround(P, o);
            return v > here || (v == here && o < c);
        }

        static bool OnShore(PlanetState P, int c)
        {
            for (int q = 0; q < 4; q++) if (P.IsLand(P.Grid.Nbr4[c * 4 + q])) return true;
            return false;
        }

        static double SealsAround(PlanetState P, int c)
        {
            int n = P.Cells; var g = P.Grid;
            double v = 0;
            for (int s = 0; s < P.Species.Count; s++)
            {
                var sp = P.Species[s];
                if (sp.Habitat != 1 || sp.Kind != 12) continue;
                v += P.Abund[s * n + c];
                for (int k = 0; k < 8; k++) v += P.Abund[s * n + g.Nbr8[c * 8 + k]];
            }
            return v;
        }

        /// <summary>A shore beside sea cell c for a seal (keyed): a point at the water's edge on a neighbouring land cell.</summary>
        static bool Shore(PlanetState P, int c, ulong key, out Vec3d land, out Vec3d sea)
        {
            var g = P.Grid;
            land = sea = g.Center[c];
            int start = (int)(KeyedRng.Unit(key) * 4);
            for (int q = 0; q < 4; q++)
            {
                int nb = g.Nbr4[c * 4 + (start + q) % 4];
                if (!P.IsLand(nb) || P.Ice[nb] > 5f) continue;
                // walk from the sea toward the land until the ground stands just clear of the water
                Vec3d a = g.Center[c], b = g.Center[nb];
                double along = 0.15 * (KeyedRng.Unit(KeyedRng.Mix(key + 5)) - 0.5);
                for (int i = 1; i <= 10; i++)
                {
                    double tt = i / 10.0;
                    Vec3d p = (a * (1 - tt) + b * tt).Normalized;
                    p = (p * P.Radius + Vec3d.Cross(p, b - a).Normalized * (along * P.CellSize)).Normalized;
                    if (Ground.Height(P, p) > P.SeaLevel + 0.25)
                    {
                        land = p;
                        sea = (a * (1 - tt + 0.25) + b * (tt - 0.25)).Normalized;
                        return true;
                    }
                }
            }
            return false;
        }

        static Vector3 Dir(Vec3d d, double r) => new Vector3((float)(d.X * r), (float)(d.Y * r), (float)(d.Z * r));
        static Vector3 ToV(Vec3d d) => new Vector3((float)d.X, (float)d.Y, (float)d.Z);
    }
}
