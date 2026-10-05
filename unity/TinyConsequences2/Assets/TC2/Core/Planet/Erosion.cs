using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Rivers cut their valleys and carry the rock away (05 §3b, D2-031). Every tectonic step, on the drainage tree just
    /// rebuilt: stream-power incision (Braun &amp; Willett 2013, n = 1), implicit in stack order so it is stable for any
    /// step, driven by the river's own smoothed flow (`RiverFlow`, so a wetter climate cuts faster and an ice age's low
    /// sea deepens the lower valleys) and by the drop to the receiver; no rivers under ice sheets, none inside lakes
    /// (still water). The eroded rock travels down the same tree and settles where the water slows: in lakes (which fill
    /// over millennia), on flats (floodplains), and at the river's mouth, where it spreads into the shallows until the
    /// sea floor reaches the surface (a delta grows into new land). Mass is kept: eroded = settled + carried past the
    /// shallows. The drainage is rebuilt after (World.TectonicStep), so the next rivers follow the new valleys.
    /// </summary>
    public static class Erosion
    {
        // the land wears as fast as the plates raise it (D2-043): the plates move ~100x Earth's pace, and so must the rivers,
        // the slopes and the sea's carrying-off of what they bring down, or the continents rise without end
        public const double K = 5.5e-3;           // incision per sqrt(m3/day of flow) per year, over the drop per metre (D2-031: 1.1e-3)
        public const double Critical = 2.5;       // gradient beyond which slopes fail (rockfall; the relief is Earth-tall on 245 m cells)
        public const double ShelfKeep = 0.15;     // share of a river's load the shelf at its mouth keeps; the rest goes down the slope to the deep
        public const double Kh = 2e-4;            // per year: every slope creeps and washes toward the way the water goes (joins the load)
        public const double MinFlow = 300.0;      // m3/day: smaller trickles are the hillslopes' work (Tectonics wear)
        public const double FlatSlope = 0.004;    // a river slower than this drops a third of its load (floodplain)
        public const double MouthDepth = 0.6;     // a delta's top stands this far below the sea until it emerges

        // diagnostics of the last step (m3)
        public static double Eroded, Settled, ToDeep, Failed;

        [ThreadStatic] static float[] _ero;
        [ThreadStatic] static double[] _load;

        public static void Step(Simulation.World w, double dt)
        {
            var P = w.Planet;
            var d = w.Drainage;
            var g = P.Grid;
            int n = P.Cells;
            if (_ero == null || _ero.Length != n) { _ero = new float[n]; _load = new double[n]; }
            Array.Clear(_ero, 0, n); Array.Clear(_load, 0, n);
            double sea = P.SeaLevel;
            Eroded = Settled = ToDeep = Failed = 0;
            // 1. incision, receivers first: h' = (h + F h_r') / (1 + F), F = K dt sqrt(Q) / L
            for (int k = 0; k < d.StackCount; k++)
            {
                int c = d.Stack[k];
                int r = d.Receiver[c];
                if (r < 0 || P.Ice[c] > 5f || d.BasinOf[c] >= 0) continue;
                double hr = d.BuiltLand(r) ? P.Elev[r] : sea;   // land and sea as the tree was built (a delta may rise meanwhile)
                double h = P.Elev[c];
                if (h <= hr) continue;
                double L = Vec3d.ArcDistance(g.Center[c], g.Center[r], P.Radius);
                double q = P.RiverFlow[c];
                if (q >= MinFlow || Kh > 0)
                {
                    // rivers cut by their flow; every slope also creeps and washes toward the way the water goes
                    double F = ((q >= MinFlow ? K * DMath.Sqrt(q) / L : 0.0) + Kh) * dt;
                    double hn = (h + F * hr) / (1.0 + F);
                    if (hn < sea + 0.2) hn = DMath.Min(h, sea + 0.2);   // rivers cut down to the sea, not below it
                    _ero[c] = (float)(h - hn);
                    P.Elev[c] = (float)hn;
                    h = hn;
                }
                // a slope steeper than the critical gradient fails toward the way the water goes: the rock joins the load
                double top = hr + Critical * L;
                if (h > top)
                {
                    double fall = h - top;
                    _ero[c] = (float)(_ero[c] + fall);
                    P.Elev[c] = (float)top;
                    Failed += fall * P.AreaHa(c) * 1e4;
                }
            }
            // 2. the load travels down, tributaries first; lakes, flats and the mouth keep it
            for (int k = d.StackCount - 1; k >= 0; k--)
            {
                int c = d.Stack[k];
                double area = P.AreaHa(c) * 1e4;
                double load = _load[c] + _ero[c] * area;
                Eroded += _ero[c] * area;
                if (load <= 0) continue;
                int r = d.Receiver[c];
                if (d.BasinOf[c] >= 0)
                {
                    // still water: the lake bed rises toward the lake's surface; what does not fit flows on
                    double room = DMath.Max(0.0, (d.Filled[c] - FilledGap - P.Elev[c]) * area);
                    double keep = DMath.Min(load, room);
                    P.Elev[c] = (float)(P.Elev[c] + keep / area);
                    Settled += keep; load -= keep;
                }
                else if (r >= 0 && d.BuiltLand(r))
                {
                    double slope = ((double)P.Elev[c] - P.Elev[r]) / Vec3d.ArcDistance(g.Center[c], g.Center[r], P.Radius);
                    if (slope < FlatSlope)
                    {
                        double keep = load / 3.0;
                        P.Elev[c] = (float)(P.Elev[c] + keep / area);
                        Settled += keep; load -= keep;
                    }
                }
                if (load <= 0) continue;
                if (r < 0) { ToDeep += load; continue; }
                if (d.BuiltLand(r)) _load[r] += load;
                else Mouth(P, d, g, r, load, sea);
            }
        }

        const double FilledGap = 0.4;   // a lake keeps a little water over its sediment

        /// <summary>A river's load at its mouth: it fills the shallows to just under the surface, then spills on outward.</summary>
        static void Mouth(PlanetState P, Drainage d, CubeSphere g, int c, double load, double sea)
        {
            // most of what a river carries is carried past its mouth down the shelf's slope (D2-043); the shelf keeps a share
            double past = load * (1.0 - ShelfKeep);
            ToDeep += past; load -= past;
            for (int hop = 0; hop < 8 && load > 0; hop++)
            {
                double area = P.AreaHa(c) * 1e4;
                double room = DMath.Max(0.0, (sea - MouthDepth - P.Elev[c]) * area);
                double keep = DMath.Min(load, room);
                P.Elev[c] = (float)(P.Elev[c] + keep / area);
                Settled += keep; load -= keep;
                if (load <= 0) return;
                // the next sea cell out: the deepest neighbour (the slope of the shelf)
                int next = -1; float low = P.Elev[c];
                for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (!d.BuiltLand(nb) && P.Elev[nb] < low) { low = P.Elev[nb]; next = nb; } }
                if (next < 0) break;
                c = next;
            }
            // a delta at the surface: what is left raises the mouth just above the sea (new land), then is lost to the deep
            if (load > 0)
            {
                double area = P.AreaHa(c) * 1e4;
                double up = DMath.Min(load / area, sea + 0.5 - P.Elev[c]);
                if (up > 0) { P.Elev[c] = (float)(P.Elev[c] + up); Settled += up * area; load -= up * area; }
                ToDeep += DMath.Max(0.0, load);
            }
        }
    }
}
