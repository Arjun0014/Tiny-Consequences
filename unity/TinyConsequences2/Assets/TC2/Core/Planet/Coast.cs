using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// The sea shapes its coasts (05 §3b, D2-034): waves wear down what juts out into them and longshore drift fills what
    /// cuts into the land. Every tectonic step: a land cell with six or more sea neighbours (a headland, a spit, a lone
    /// islet) loses up to 3 m a decade toward the sea floor, its rock spread over the shallows round it; a sea cell with
    /// six or more land neighbours (a narrow inlet) is filled from its shores, up to 3 m a decade. Mass is kept. High
    /// islands (volcanoes) stand for millennia; low ones and one-cell inlets go within a century or two, so coasts stay
    /// coasts as the plates move rather than a speckle of single cells.
    /// </summary>
    public static class Coast
    {
        public const double Rate = 0.3;        // m a year at the most exposed (eight-sided) cell
        public static double Moved;            // m3 moved in the last step (diagnostics)

        [ThreadStatic] static float[] _delta;

        public static void Step(Simulation.World w, double dt)
        {
            var P = w.Planet;
            var g = P.Grid;
            int n = P.Cells;
            if (_delta == null || _delta.Length != n) _delta = new float[n];
            Array.Clear(_delta, 0, n);
            double sea = P.SeaLevel;
            Moved = 0;
            Span<int> nbs = stackalloc int[8];
            for (int c = 0; c < n; c++)
            {
                bool land = P.IsLand(c);
                int other = 0;
                for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (P.IsLand(nb) != land) nbs[other++] = nb; }
                if (other < 6) continue;
                double area = P.GeoAreaHa(c) * 1e4;
                double exposure = (other - 5) / 3.0;                     // 1/3 .. 1
                if (land)
                {
                    // a headland or islet wears down toward the sea floor; the rock goes to the shallows round it
                    double room = P.Elev[c] - (sea - 2.0);
                    double drop = DMath.Min(room, Rate * dt * exposure);
                    if (drop <= 0) continue;
                    _delta[c] = (float)(_delta[c] - drop);
                    double each = drop * area / other;
                    for (int k = 0; k < other; k++) _delta[nbs[k]] = (float)(_delta[nbs[k]] + each / (P.GeoAreaHa(nbs[k]) * 1e4));
                    Moved += drop * area;
                }
                else
                {
                    // a narrow inlet fills from its shores
                    double room = (sea + 1.0) - P.Elev[c];
                    double rise = DMath.Min(room, Rate * dt * exposure);
                    if (rise <= 0) continue;
                    _delta[c] = (float)(_delta[c] + rise);
                    double each = rise * area / other;
                    for (int k = 0; k < other; k++) _delta[nbs[k]] = (float)(_delta[nbs[k]] - each / (P.GeoAreaHa(nbs[k]) * 1e4));
                    Moved += rise * area;
                }
            }
            for (int c = 0; c < n; c++) if (_delta[c] != 0f) P.Elev[c] = (float)((double)P.Elev[c] + _delta[c]);
        }
    }
}
