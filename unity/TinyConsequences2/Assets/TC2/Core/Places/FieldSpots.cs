using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Where a household's hands work the village's fields (session 20): its own part of them - one of the village's field cells beyond its houses,
    /// chosen by the household and kept all the year (in the record a household's strips lay scattered through the open fields; here a cell a
    /// household) - for the crop's work and for carting the muck out. Before, every field hand and every carter of a village went to its one field
    /// spot (`Settlement.FieldX/FieldY`, +-60 m): a town's hundred carters stood in one crowd on a few plots (`act:Muck`: 126 within 60 m).
    /// A household's cell is one its hands can walk to dry-shod from home (session 21): a lived walk to a work spot is a straight line, and a cell
    /// across a bay or a lake was walked to through the water (the real shots' carter in the sea; `wet` audit: carters 96-224 m from dry ground).
    /// </summary>
    public static class FieldSpots
    {
        /// <summary>Tools: the village's one field spot as before (`--onefieldspot`), on the same binary.</summary>
        public static bool One;
        /// <summary>Tools: a household's cell without asking whether its hands can walk there dry-shod (`--wetfieldspot`: session 20's rule).</summary>
        public static bool WetWay;

        static readonly List<int> _cells = new List<int>();

        /// <summary>A spot on household `hid`'s part of the village's fields (`jitter` m about its cell's middle): its own cell by the household, or
        /// the next of the village's cells beyond its houses its hands can walk to from home without crossing water; the one field spot with
        /// `--onefieldspot` or where none is. Two draws of `rs` either way.</summary>
        public static void Spot(World w, in Settlement s, int hid, double jitter, ref RandomSequence rs, out double x, out double y)
        {
            double dx = rs.Range(-jitter, jitter), dy = rs.Range(-jitter, jitter);
            x = s.FieldX + dx; y = s.FieldY + dy;
            if (One) return;
            var P = w.Planet;
            var fc = w.FieldCellsOf(s.Id);
            _cells.Clear();
            for (int i = 0; i < fc.Count; i++) if (Beyond(w, in s, fc[i])) _cells.Add(fc[i]);
            int n = _cells.Count;
            if (n == 0) return;
            int k = (int)(KeyedRng.Mix((ulong)hid * 0x9E3779B97F4A7C15UL ^ 0xF1E1D5UL) % (ulong)n);
            double hx = s.SquareX, hy = s.SquareY;
            if (w.Households.Exists(hid)) { ref var h = ref w.Households.Get(hid); hx = h.HomeX; hy = h.HomeY; }
            for (int tries = 0; tries < n; tries++, k = (k + 1) % n)
            {
                s.SphereToLocal(P.Grid.Center[_cells[k]], P.Radius, out double cx, out double cy);
                if (!WetWay && !DryWay(w, in s, hx, hy, cx + dx, cy + dy)) continue;
                x = cx + dx; y = cy + dy;
                return;
            }
        }

        /// <summary>A field cell beyond the village's houses and on dry ground (as the herds' grounds are).</summary>
        static bool Beyond(World w, in Settlement s, int c)
        {
            var P = w.Planet;
            return P.IsLand(c) && P.LakeDepth[c] <= 0.3f && Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius) >= 140.0;
        }

        /// <summary>Whether the straight way from (ax, ay) to (bx, by) in the village's frame stays out of the water (looked at every 15 m and at its end).</summary>
        public static bool DryWay(World w, in Settlement s, double ax, double ay, double bx, double by)
        {
            var P = w.Planet;
            double ex = bx - ax, ey = by - ay;
            int steps = 1 + (int)(DMath.Sqrt(ex * ex + ey * ey) / 15.0);
            for (int i = 1; i <= steps; i++)
            {
                double f = i / (double)steps;
                if (Boats.Afloat(P, s.LocalToSphere(ax + ex * f, ay + ey * f, P.Radius))) return false;
            }
            return true;
        }
    }
}
