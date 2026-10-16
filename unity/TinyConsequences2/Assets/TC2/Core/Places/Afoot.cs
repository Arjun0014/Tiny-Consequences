using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Ways on foot round the water (D2-156). A lived walk is a straight line from where someone stands to where they go, and where that line
    /// crossed a lake or a bay they walked through the water: the `wet` audit found 0.10-0.14 % of a year-300 world's person-hours under the drawn
    /// water (0.22-0.28 % with C3.7d) - woodcutters and hunters walking across a bay to their work and home over it again, besides spots in the
    /// water (the well's under a lake's edge, crofts dug into it). Now a walk whose straight way meets the water bends once on the dry ground beside
    /// it - the shortest such way of the candidates - and takes the time its length takes; the woods are chosen by the length of the way their
    /// cutters walk (`Way`). A walk no single bend keeps dry (an island's far shore, a ring of water) goes straight as before, counted
    /// (`Metrics.WetWalks`).
    /// </summary>
    public static class Afoot
    {
        /// <summary>Tools: the rules before D2-156 (`--wetways`): straight walks across the water, the well's and the idle's spots and the crofts' beds in
        /// it, the woods chosen by the straight line.</summary>
        public static bool Off;
        /// <summary>m between the looks along a way.</summary>
        public const double Step = 15.0;
        /// <summary>m: walks shorter than this are not looked at (a few steps about a spot).</summary>
        public const double Short = 20.0;
        /// <summary>m out from the straight way a bend is looked for, either side (the farthest a little over half the woods' reach).</summary>
        static readonly double[] Out = { 30.0, 60.0, 120.0, 240.0, 480.0, 960.0 };
        const int Cands = 3 * 6 * 2;
        static readonly double[] _cx = new double[Cands], _cy = new double[Cands], _cl = new double[Cands];
        static readonly int[] _co = new int[Cands];

        /// <summary>Whether the straight way from (ax, ay) to (bx, by) in the village's frame stays out of the water (looked at every 15 m and at its end;
        /// not at its start, where the walker stands).</summary>
        public static bool Dry(World w, in Settlement s, double ax, double ay, double bx, double by)
        {
            var P = w.Planet;
            double ex = bx - ax, ey = by - ay;
            int steps = 1 + (int)(DMath.Sqrt(ex * ex + ey * ey) / Step);
            for (int i = 1; i <= steps; i++)
            {
                double f = i / (double)steps;
                if (Boats.Afloat(P, s.LocalToSphere(ax + ex * f, ay + ey * f, P.Radius))) return false;
            }
            return true;
        }

        /// <summary>
        /// A bend for a walk whose straight way meets the water: a point (vx, vy) on dry ground whose two straight legs stay out of the water - of the
        /// candidates beside the wet stretch's first and last wet looks and its middle, 30 to 960 m out to either side, the one with the shortest way.
        /// False when the straight way is dry after all, or no candidate keeps out of the water.
        /// </summary>
        public static bool Bend(World w, in Settlement s, double ax, double ay, double bx, double by, out double vx, out double vy)
        {
            vx = bx; vy = by;
            var P = w.Planet;
            double ex = bx - ax, ey = by - ay, L = DMath.Sqrt(ex * ex + ey * ey);
            if (L < 1.0) return false;
            double ux = ex / L, uy = ey / L, nx = -uy, ny = ux;
            int steps = 1 + (int)(L / Step);
            double t1 = -1.0, t2 = -1.0;
            for (int i = 1; i <= steps; i++)
            {
                double t = L * i / steps;
                if (!Boats.Afloat(P, s.LocalToSphere(ax + ux * t, ay + uy * t, P.Radius))) continue;
                if (t1 < 0) t1 = t;
                t2 = t;
            }
            if (t1 < 0) return false;
            int n = 0;
            for (int a = 0; a < 3; a++)
            {
                double t = DMath.Clamp(a == 0 ? t1 - Step : a == 1 ? 0.5 * (t1 + t2) : t2 + Step, 0.0, L);
                for (int o = 0; o < Out.Length; o++)
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        double cx = ax + ux * t + nx * sg * Out[o], cy = ay + uy * t + ny * sg * Out[o];
                        _cx[n] = cx; _cy[n] = cy; _cl[n] = Len(ax, ay, cx, cy) + Len(cx, cy, bx, by); _co[n] = n;
                        n++;
                    }
            }
            for (int i = 1; i < n; i++)   // (the shortest first; ties in the order made)
            {
                int k = _co[i], j = i - 1;
                while (j >= 0 && _cl[_co[j]] > _cl[k]) { _co[j + 1] = _co[j]; j--; }
                _co[j + 1] = k;
            }
            for (int i = 0; i < n; i++)
            {
                int k = _co[i];
                if (Boats.Afloat(P, s.LocalToSphere(_cx[k], _cy[k], P.Radius))) continue;
                if (!Dry(w, in s, ax, ay, _cx[k], _cy[k]) || !Dry(w, in s, _cx[k], _cy[k], bx, by)) continue;
                vx = _cx[k]; vy = _cy[k];
                return true;
            }
            return false;
        }

        /// <summary>The length of the way on foot from (ax, ay) to (bx, by): straight where it stays dry, else round the water by the bend `Bend` finds;
        /// -1 where none keeps out of the water.</summary>
        public static double Way(World w, in Settlement s, double ax, double ay, double bx, double by)
        {
            if (Dry(w, in s, ax, ay, bx, by)) return Len(ax, ay, bx, by);
            if (!Bend(w, in s, ax, ay, bx, by, out double vx, out double vy)) return -1.0;
            return Len(ax, ay, vx, vy) + Len(vx, vy, bx, by);
        }

        static double Len(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay;
            return DMath.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Tools: the bends walked and the walks no bend kept dry, by what was done at their end (index: ActivityKind).</summary>
        public static long[] Tally;

        /// <summary>A rectangle of a home's yard (its frame: `af` its facing, as `Gardens.Strips` lays it) whose middle or a corner lies under the drawn water.</summary>
        public static bool WetBox(World w, in Settlement s, double bx, double by, double af, double mx, double mz, double hx, double hz)
        {
            var P = w.Planet;
            double fx = DMath.Cos(af), fy = DMath.Sin(af), ux = -fy, uy = fx;
            for (int k = 0; k < 5; k++)
            {
                double cx = k == 0 ? mx : mx + ((k & 1) == 1 ? hx : -hx), cz = k == 0 ? mz : mz + (k <= 2 ? hz : -hz);
                if (Boats.Afloat(P, s.LocalToSphere(bx + ux * cx + fx * cz, by + uy * cx + fy * cz, P.Radius))) return true;
            }
            return false;
        }

        static readonly List<(double v, int c, double d)> _woods = new List<(double, int, double)>();
        static readonly System.Comparison<(double v, int c, double d)> ByValue = (a, b) => a.v != b.v ? a.v.CompareTo(b.v) : a.c.CompareTo(b.c);

        /// <summary>
        /// (`Woods.Choose`) Of woods within reach, given as (value by the straight distance, cell, straight distance) - the value (d + 200 m) / (forest +
        /// 0.2) - the one whose value is least by the way its cutters walk from the village's middle: the straight distance where that way stays dry (as
        /// before), else the way round the water; none where no way within reach keeps out of it. The straight distance is the least a way can be, so
        /// the woods are looked at by their straight value and the look stops where that already loses: where every way is dry the choice is the old
        /// one. Returns the cell (-1 none) and its way's length.
        /// </summary>
        public static int BestWoods(World w, in Settlement s, List<(double v, int c, double d)> woods, out double way)
        {
            var P = w.Planet;
            way = 0;
            woods.Sort(ByValue);
            double gx = 0.0, gy = 0.0;
            Boats.OutOfTheWater(w, in s, ref gx, ref gy);   // (where the water has come over the village's middle: the dry ground nearest it)
            int best = -1; double bv = double.MaxValue;
            for (int i = 0; i < woods.Count; i++)
            {
                var (v0, c, d) = woods[i];
                if (v0 >= bv) break;
                s.SphereToLocal(P.Grid.Center[c], P.Radius, out double cx, out double cy);
                double v = v0, dw = d;
                if (!Dry(w, in s, gx, gy, cx, cy))
                {
                    if (!Bend(w, in s, gx, gy, cx, cy, out double vx, out double vy)) continue;
                    dw = Len(gx, gy, vx, vy) + Len(vx, vy, cx, cy);
                    if (dw > Woods.Reach) continue;
                    v = (dw + 200.0) / ((double)P.Forest[c] + 0.2);
                }
                if (v < bv) { bv = v; best = c; way = dw; }
            }
            return best;
        }

        /// <summary>The list `Woods.Choose` fills for `BestWoods`.</summary>
        public static List<(double v, int c, double d)> WoodsList() { _woods.Clear(); return _woods; }
    }
}
