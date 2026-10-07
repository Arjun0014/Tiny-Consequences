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
        public static double VScale = 0.4;   // the simulation's relief as drawn, about sea level (detail is drawn at its own size)
        static double _crags = 1.0;          // the mountains' drawn crags, with the relief

        /// <summary>
        /// The drawn relief follows the planet's size (D2-085): a smaller world shows the same ground with its ranges in the same
        /// proportion to the globe (0.4 of the simulated heights on the 10 km world), the fine relief - hills, gullies, beaches - at its
        /// own size. Called when a world is set.
        /// </summary>
        public static void ScaleTo(PlanetState P) { VScale = 0.4 * P.GeoScale; _crags = P.GeoScale; PlanetGeo.VScale = (float)(0.5 * P.GeoScale); }
        static double _r2 = -1, _r2c;        // kernel radii squared (chord^2): elevation (exact at centres), colour (softer)

        static void Init(PlanetState P)
        {
            double s = P.Grid.MeanSpacing;   // radians between cell centres
            _r2 = (1.0 * s) * (1.0 * s);
            _r2c = (1.45 * s) * (1.45 * s);
        }

        /// <summary>A cell's elevation as drawn (sim metres; the shore ramp applied).</summary>
        public static double DrawnElev(PlanetState P, int c) => Shore.Drawn(P, c);   // (one rule with the simulation: Core's Shore, D2-088)

        static float[] _relief;
        static PlanetState _rp;
        static int _rRelief = -1, _rCoast = -1;

        /// <summary>
        /// Where a cell lies in its land's relief (D2-084, presentation): its drawn height against the mean of the 24 cells round it
        /// (two rings, ~1.2 km across), softly scaled to -1 (a valley floor) .. +1 (a ridge or hilltop); the sea's own cells 0. The
        /// valleys are the drainage's (erosion cut them), so their floors gather water and its life; ridges are dry and thin-soiled.
        /// </summary>
        public static float Relief(PlanetState P, int c)
        {
            var _drawn = Shore.DrawnAll(P);
            if (_rp != P || _rRelief != P.ReliefVersion || _rCoast != P.CoastVersion || _relief == null || _relief.Length != P.Cells)
            {
                _rp = P; _rRelief = P.ReliefVersion; _rCoast = P.CoastVersion;
                int n = P.Cells;
                if (_relief == null || _relief.Length != n) _relief = new float[n];
                var g = P.Grid;
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int k = 0; k < n; k++)
                {
                    if (!P.IsLand(k)) { _relief[k] = 0f; continue; }
                    seen.Clear(); seen.Add(k);
                    double sum = 0; int cnt = 0;
                    for (int a = 0; a < 8; a++)
                    {
                        int nb = g.Nbr8[k * 8 + a];
                        if (seen.Add(nb)) { sum += System.Math.Max(_drawn[nb], P.SeaLevel); cnt++; }
                        for (int b = 0; b < 8; b++)
                        {
                            int nn = g.Nbr8[nb * 8 + b];
                            if (seen.Add(nn)) { sum += System.Math.Max(_drawn[nn], P.SeaLevel); cnt++; }
                        }
                    }
                    double d = cnt > 0 ? _drawn[k] - sum / cnt : 0.0;
                    _relief[k] = (float)(d / (System.Math.Abs(d) + 35.0));
                }
            }
            return _relief[c];
        }

        /// <summary>The simulation's surface (rock + ice) interpolated at a direction; also returns the containing cell.</summary>
        public static double SimElevation(PlanetState P, Vec3d dir, out int cell) => SimElevation(P, dir, out cell, out _);

        /// <summary>As above, and how much lake water stands around (kernel-weighted depth, m).</summary>
        public static double SimElevation(PlanetState P, Vec3d dir, out int cell, out double lake)
        {
            if (_r2 < 0) Init(P);
            double rock = Shore.At(P, dir, out int c, out lake);   // (the ground between centres is Core's: D2-088)
            cell = c;
            var g = P.Grid;
            double si = 0, swi = 0;
            AccumIce(g.Center[c], P.Ice[c], dir, ref si, ref swi);
            for (int k = 0; k < 8; k++) { int n = g.Nbr8[c * 8 + k]; AccumIce(g.Center[n], P.Ice[n], dir, ref si, ref swi); }
            double ice = swi > 1e-12 ? si / swi : P.Ice[c];
            return rock + ice;
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
            double crags = mount * 230.0 * _crags * Noise.Ridged(x * 1.7 + 11.3, y * 1.7 - 4.1, z * 1.7 + 7.7, 4);
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
