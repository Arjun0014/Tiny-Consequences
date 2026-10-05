using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The ground as drawn (13 §2, D2-026): the simulation's relief interpolated smoothly across cells and cube faces
    /// (a kernel over the containing cell and its 8 neighbours: exact at cell centres, seamless at face edges), plus
    /// the fine relief the 245 m cells cannot hold - rolling hills, gullies, crags on mountains, quiet near the shore.
    /// The simulation's heights are Earth-tall on a 10 km planet, so the drawn relief scales them down about sea level
    /// (a globe from orbit, not a spiky ball) while the fine relief is authored at its drawn size. Presentation only
    /// (never read by the simulation); the terrain and
    /// everything standing on it use this one function, so houses, trees and people sit on what is drawn.
    /// </summary>
    public static class Ground
    {
        public const double VScale = 0.4;    // the simulation's relief as drawn, about sea level (detail is drawn at its own size)
        static double _r2 = -1, _r2c;        // kernel radii squared (chord^2): elevation (exact at centres), colour (softer)

        static void Init(PlanetState P)
        {
            double s = P.Grid.MeanSpacing;   // radians between cell centres
            _r2 = (1.0 * s) * (1.0 * s);
            _r2c = (1.45 * s) * (1.45 * s);
        }

        // the drawn elevation per cell: the simulation's, but shores ramp gently - land near the sea no higher than
        // 45 m + 110 m per cell inland, sea near land no deeper than 60 m + 400 m per cell out (sim metres). Coarse
        // cells next to a deep trench would otherwise draw as cliffs; the coastline itself stays where it is.
        static float[] _drawn;
        static PlanetState _dp;
        static int _dRelief = -1, _dCoast = -1;
        static double _dSea = double.NaN;
        static int[] _dist;

        static void RefreshDrawn(PlanetState P)
        {
            if (P == _dp && P.ReliefVersion == _dRelief && P.CoastVersion == _dCoast && P.SeaLevel == _dSea) return;
            _dp = P; _dRelief = P.ReliefVersion; _dCoast = P.CoastVersion; _dSea = P.SeaLevel;
            int n = P.Cells;
            var g = P.Grid;
            if (_drawn == null || _drawn.Length != n) { _drawn = new float[n]; _dist = new int[n]; }
            var queue = new System.Collections.Generic.List<int>(n / 4);
            for (int c = 0; c < n; c++)
            {
                _dist[c] = int.MaxValue;
                bool land = P.IsLand(c);
                for (int k = 0; k < 8; k++)
                    if (P.IsLand(g.Nbr8[c * 8 + k]) != land) { _dist[c] = 0; queue.Add(c); break; }
            }
            for (int head = 0; head < queue.Count; head++)
            {
                int c = queue[head];
                int d = _dist[c];
                if (d >= 4) continue;
                bool land = P.IsLand(c);
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (P.IsLand(nb) != land || _dist[nb] <= d + 1) continue;
                    _dist[nb] = d + 1;
                    queue.Add(nb);
                }
            }
            double sea = P.SeaLevel;
            for (int c = 0; c < n; c++)
            {
                double e = P.Elev[c];
                int d = _dist[c] == int.MaxValue ? 9 : _dist[c];
                if (P.LakeDepth[c] > 0f) _drawn[c] = (float)e;                       // lakes keep their beds and shores
                else if (e > sea) _drawn[c] = (float)System.Math.Min(e, sea + 45.0 + 110.0 * d);
                else _drawn[c] = (float)System.Math.Max(e, sea - 60.0 - 400.0 * d);
            }
        }

        /// <summary>A cell's elevation as drawn (sim metres; the shore ramp applied).</summary>
        public static double DrawnElev(PlanetState P, int c) { RefreshDrawn(P); return _drawn[c]; }

        /// <summary>The simulation's surface (rock + ice) interpolated at a direction; also returns the containing cell.</summary>
        public static double SimElevation(PlanetState P, Vec3d dir, out int cell) => SimElevation(P, dir, out cell, out _);

        /// <summary>As above, and how much lake water stands around (kernel-weighted depth, m).</summary>
        public static double SimElevation(PlanetState P, Vec3d dir, out int cell, out double lake)
        {
            if (_r2 < 0) Init(P);
            RefreshDrawn(P);
            var E = _drawn;
            var g = P.Grid;
            int c = g.CellOf(dir);
            cell = c;
            double sw = 0, s = 0, sl = 0, si = 0, swi = 0;
            Accum(g.Center[c], E[c], P.LakeDepth[c], dir, _r2, ref s, ref sl, ref sw);
            AccumIce(g.Center[c], P.Ice[c], dir, ref si, ref swi);
            for (int k = 0; k < 8; k++)
            {
                int n = g.Nbr8[c * 8 + k];
                Accum(g.Center[n], E[n], P.LakeDepth[n], dir, _r2, ref s, ref sl, ref sw);
                AccumIce(g.Center[n], P.Ice[n], dir, ref si, ref swi);
            }
            double ice = swi > 1e-12 ? si / swi : P.Ice[c];
            if (sw <= 1e-12) { lake = P.LakeDepth[c]; return E[c] + ice; }
            lake = sl / sw;
            return s / sw + ice;
        }

        static void AccumIce(Vec3d centre, double v, Vec3d dir, ref double s, ref double sw)
        {
            double dx = dir.X - centre.X, dy = dir.Y - centre.Y, dz = dir.Z - centre.Z;
            double d2 = dx * dx + dy * dy + dz * dz;
            if (d2 >= _r2c) return;
            double u = 1.0 - d2 / _r2c;
            double w = u * u;
            s += w * v; sw += w;
        }

        static void Accum(Vec3d centre, double v, double lv, Vec3d dir, double r2, ref double s, ref double sl, ref double sw)
        {
            double dx = dir.X - centre.X, dy = dir.Y - centre.Y, dz = dir.Z - centre.Z;
            double d2 = dx * dx + dy * dy + dz * dz;
            if (d2 >= r2) return;
            double u = 1.0 - d2 / r2;
            double w = u * u;
            s += w * v; sl += w * lv; sw += w;
        }

        /// <summary>Kernel weights of the containing cell and its neighbours for blending per-cell colours.</summary>
        public static int ColourWeights(PlanetState P, Vec3d dir, int c, int[] cells, float[] weights) => Weights(P, dir, c, cells, weights, false);

        /// <summary>The same with the exact kernel (1 at a cell's centre): sharper masks such as farmland.</summary>
        public static int MaskWeights(PlanetState P, Vec3d dir, int c, int[] cells, float[] weights) => Weights(P, dir, c, cells, weights, true);

        static int Weights(PlanetState P, Vec3d dir, int c, int[] cells, float[] weights, bool exact)
        {
            if (_r2 < 0) Init(P);
            double r2 = exact ? _r2 * 1.21 : _r2c;
            var g = P.Grid;
            int n = 0; double sw = 0;
            for (int k = -1; k < 8; k++)
            {
                int cc = k < 0 ? c : g.Nbr8[c * 8 + k];
                Vec3d ce = g.Center[cc];
                double dx = dir.X - ce.X, dy = dir.Y - ce.Y, dz = dir.Z - ce.Z;
                double d2 = dx * dx + dy * dy + dz * dz;
                if (d2 >= r2) continue;
                double u = 1.0 - d2 / r2;
                cells[n] = cc; weights[n] = (float)(u * u); sw += u * u; n++;
            }
            for (int i = 0; i < n; i++) weights[i] = (float)(weights[i] / sw);
            return n;
        }

        /// <summary>Fine relief (m) at a direction over ground of simulated height e (relative to the sea).</summary>
        public static double Detail(Vec3d dir, double rel)
        {
            double x = dir.X * 16.7, y = dir.Y * 16.7, z = dir.Z * 16.7; // ~600 m features at R = 10 km
            if (rel < 0)
                return 12.0 * Noise.Fbm(x * 1.3, y * 1.3, z * 1.3, 3);  // the sea floor: soft swells
            double shore = Smooth01(rel / 22.0);                         // flat beaches, quiet coasts
            double hills = 15.0 * Noise.Fbm(x, y, z, 4);
            double mount = Smooth01((rel - 350.0) / 1300.0);
            double crags = mount * 230.0 * Noise.Ridged(x * 1.7 + 11.3, y * 1.7 - 4.1, z * 1.7 + 7.7, 4);
            double gullies = 6.0 * Noise.Fbm(x * 5.1 + 3.7, y * 5.1, z * 5.1 - 1.9, 2) * (0.4 + mount);
            return shore * (hills + crags + gullies);
        }

        static double Smooth01(double t) { t = t < 0 ? 0 : (t > 1 ? 1 : t); return t * t * (3 - 2 * t); }

        /// <summary>Drawn height above the reference sphere (m): simulated relief plus fine relief, exaggerated about the sea.</summary>
        public static double Height(PlanetState P, Vec3d dir, out int cell)
        {
            double e = SimElevation(P, dir, out cell, out double lake);
            double sea = P.SeaLevel;
            double rel = e - sea;
            double quiet = lake > 0 ? 1.0 - Smooth01(lake / 1.5) * 0.85 : 1.0; // lake beds and shores stay smooth
            double cone = VolcanoShape.At(P, dir, cell, out float coneRock, out _);
            quiet *= 1.0 - 0.75 * coneRock;                                    // a cone's flanks are its own shape
            if (rel < 0)
            {
                // a gentle shelf: clear shallows along every coast, the deep further out
                double depth = -rel;
                double drawn = 26.0 * (1.0 - System.Math.Exp(-depth / 170.0)) + 0.11 * depth;
                return sea - drawn + Detail(dir, rel) * quiet * System.Math.Min(1.0, depth / 60.0) + cone; // a cone runs on into the sea
            }
            return sea + rel * VScale + Detail(dir, rel) * quiet + cone;
        }

        public static double Height(PlanetState P, Vec3d dir) => Height(P, dir, out _);

        /// <summary>Scene position of the ground at a direction (never below the sea surface for things standing on it).</summary>
        public static Vector3 Surface(PlanetState P, Vec3d dir, float lift = 0f)
        {
            double h = Height(P, dir);
            if (h < P.SeaLevel) h = P.SeaLevel;
            double r = P.Radius + h + lift;
            return new Vector3((float)(dir.X * r), (float)(dir.Y * r), (float)(dir.Z * r));
        }

        /// <summary>Height of open water (the sea, or a lake's level) as drawn.</summary>
        public static double WaterHeight(PlanetState P, double level) => P.SeaLevel + (level - P.SeaLevel) * VScale;
    }

    /// <summary>Fast deterministic 3-D gradient noise (Perlin's improved noise) for presentation.</summary>
    public static class Noise
    {
        static readonly int[] Perm = Build();

        static int[] Build()
        {
            var p = new int[512];
            var src = new int[256];
            for (int i = 0; i < 256; i++) src[i] = i;
            ulong h = 0x9E3779B97F4A7C15UL;
            for (int i = 255; i > 0; i--)
            {
                h = KeyedRng.Mix(h + (ulong)i);
                int j = (int)(h % (ulong)(i + 1));
                int t = src[i]; src[i] = src[j]; src[j] = t;
            }
            for (int i = 0; i < 512; i++) p[i] = src[i & 255];
            return p;
        }

        static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        static double Lerp(double t, double a, double b) => a + t * (b - a);

        static double Grad(int hash, double x, double y, double z)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>Gradient noise in about [-1, 1].</summary>
        public static double Perlin(double x, double y, double z)
        {
            double fx = System.Math.Floor(x), fy = System.Math.Floor(y), fz = System.Math.Floor(z);
            int X = (int)fx & 255, Y = (int)fy & 255, Z = (int)fz & 255;
            x -= fx; y -= fy; z -= fz;
            double u = Fade(x), v = Fade(y), w = Fade(z);
            var p = Perm;
            int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z, B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;
            return Lerp(w, Lerp(v, Lerp(u, Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z)),
                                   Lerp(u, Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z))),
                           Lerp(v, Lerp(u, Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1)),
                                   Lerp(u, Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1))));
        }

        public static double Fbm(double x, double y, double z, int octaves)
        {
            double s = 0, a = 1, n = 0;
            for (int i = 0; i < octaves; i++) { s += a * Perlin(x, y, z); n += a; a *= 0.5; x *= 2.03; y *= 2.03; z *= 2.03; }
            return s / n;
        }

        /// <summary>Ridged multifractal in about [0, 1]: sharp crests, soft valleys.</summary>
        public static double Ridged(double x, double y, double z, int octaves)
        {
            double s = 0, a = 1, n = 0, weight = 1;
            for (int i = 0; i < octaves; i++)
            {
                double r = 1.0 - System.Math.Abs(Perlin(x, y, z));
                r *= r * weight;
                weight = r < 0 ? 0 : (r > 1 ? 1 : r);
                s += a * r; n += a; a *= 0.5; x *= 2.07; y *= 2.07; z *= 2.07;
            }
            return s / n;
        }
    }
}
