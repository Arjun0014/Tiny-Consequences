using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// The ground between cell centres (D2-088): one rule for the simulation and the view. A cell's elevation with its shore ramped -
    /// land near the sea no higher than 45 m + 110 m per cell inland, sea near land no deeper than 60 m + 400 m per cell out (coarse cells
    /// next to a deep trench would otherwise make cliffs; the coastline stays where it is) - interpolated by a kernel over the containing
    /// cell and its eight neighbours (exact at cell centres, seamless across cube faces). The view draws this ground; the simulation asks it
    /// whether a lot is dry, so no home is ever drawn standing in the water of a coast its cell's centre is clear of.
    /// Derived from the planet's state (cached by relief, coast and sea level; never saved).
    /// </summary>
    public static class Shore
    {
        /// <summary>A cell's elevation with the shore ramp applied (sim metres).</summary>
        public static double Drawn(PlanetState P, int c) { Refresh(P); return P.ShoreDrawn[c]; }

        /// <summary>The cached per-cell drawn elevations (refreshed when the relief, the coast or the sea moved).</summary>
        public static float[] DrawnAll(PlanetState P) { Refresh(P); return P.ShoreDrawn; }

        static void Refresh(PlanetState P)
        {
            if (P.ShoreDrawn != null && P.ShoreRelief == P.ReliefVersion && P.ShoreCoast == P.CoastVersion && P.ShoreSea == P.SeaLevel) return;
            P.ShoreRelief = P.ReliefVersion; P.ShoreCoast = P.CoastVersion; P.ShoreSea = P.SeaLevel;
            int n = P.Cells;
            var g = P.Grid;
            if (P.ShoreDrawn == null || P.ShoreDrawn.Length != n) P.ShoreDrawn = new float[n];
            var dist = new int[n];
            var queue = new System.Collections.Generic.List<int>(n / 4);
            for (int c = 0; c < n; c++)
            {
                dist[c] = int.MaxValue;
                bool land = P.IsLand(c);
                for (int k = 0; k < 8; k++)
                    if (P.IsLand(g.Nbr8[c * 8 + k]) != land) { dist[c] = 0; queue.Add(c); break; }
            }
            for (int head = 0; head < queue.Count; head++)
            {
                int c = queue[head];
                int d = dist[c];
                if (d >= 4) continue;
                bool land = P.IsLand(c);
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (P.IsLand(nb) != land || dist[nb] <= d + 1) continue;
                    dist[nb] = d + 1;
                    queue.Add(nb);
                }
            }
            double sea = P.SeaLevel;
            var drawn = P.ShoreDrawn;
            for (int c = 0; c < n; c++)
            {
                double e = P.Elev[c];
                int d = dist[c] == int.MaxValue ? 9 : dist[c];
                if (P.LakeDepth[c] > 0f) drawn[c] = (float)e;                       // lakes keep their beds and shores
                else if (e > sea) drawn[c] = (float)DMath.Min(e, sea + 45.0 + 110.0 * d);
                else drawn[c] = (float)DMath.Max(e, sea - 60.0 - 400.0 * d);
            }
        }

        /// <summary>The kernel's radius, squared, as a chord (one cell spacing): exact at centres, seamless across faces.</summary>
        static double R2(PlanetState P) { double s = P.Grid.MeanSpacing; return s * s; }

        /// <summary>The surface (rock and the shore ramp, without ice) at a direction, the containing cell, and the lake water standing round (m).</summary>
        public static double At(PlanetState P, Vec3d dir, out int cell, out double lake)
        {
            Refresh(P);
            var E = P.ShoreDrawn;
            var g = P.Grid;
            double r2 = R2(P);
            int c = g.CellOf(dir);
            cell = c;
            double sw = 0, s = 0, sl = 0;
            Accum(g.Center[c], E[c], P.LakeDepth[c], dir, r2, ref s, ref sl, ref sw);
            for (int k = 0; k < 8; k++)
            {
                int nb = g.Nbr8[c * 8 + k];
                Accum(g.Center[nb], E[nb], P.LakeDepth[nb], dir, r2, ref s, ref sl, ref sw);
            }
            if (sw <= 1e-12) { lake = P.LakeDepth[c]; return E[c]; }
            lake = sl / sw;
            return s / sw;
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

        /// <summary>The ground at a direction stands above the sea and no lake covers it (with a margin of a metre or two: a home is never on the waterline).</summary>
        public static bool Dry(PlanetState P, Vec3d dir)
        {
            double e = At(P, dir, out int c, out double lake);
            return P.IsLand(c) && e > P.SeaLevel + 2.0 && lake <= 0.3;
        }
    }
}
