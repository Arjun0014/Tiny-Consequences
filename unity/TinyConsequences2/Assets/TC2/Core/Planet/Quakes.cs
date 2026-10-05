using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Earthquakes (05 §3b, D2-030). Faults are the plate boundaries: each boundary cell loads with the plates' relative
    /// motion across it (`Tectonics.Slip`, m/yr; parting plates a third as much) into a slip deficit `P.Stress`. A patch
    /// past the threshold ruptures at a moment inside the tectonic step; a rupture passes a share of its stress to its
    /// fault neighbours, which may rupture in turn (Olami-Feder-Christensen): most quakes stay one patch, a few run along
    /// the boundary, and their sizes follow a power law without being drawn from one. Magnitude from the stress released;
    /// shaking (a Mercalli-like intensity) from the magnitude and the distance to the rupture. Consequences, each caused
    /// by the quake (World.Quake): people indoors die where houses fall (most at night), stores spill and spoil, steep
    /// shaken slopes slide (woods and pasture stripped, rock moved downhill: a slide can dam a river), and a great quake
    /// under the sea sends a wave over low coasts (crops lost, people on the shore drown). Faults that stop moving heal.
    /// </summary>
    public static class Quakes
    {
        public const double Threshold = 250.0;   // slip deficit (m) a fault patch holds before it ruptures
        public const double Pass = 0.22;         // share of a rupture's stress passed to each fault neighbour (0.25 is near-critical: an M 8 every few decades)
        public const double HealYears = 400.0;   // a fault no plate loads any more relaxes over this time (and leaves the fault network)
        public const double MinRecord = 6.5;     // smaller quakes enter the history only when they do harm

        /// <summary>Worldgen: every fault patch somewhere in its cycle (keyed), so the first centuries are not quiet.</summary>
        public static void Init(Simulation.World w)
        {
            var P = w.Planet;
            float[] slip = w.Tect.Slip;
            for (int c = 0; c < P.Cells; c++)
                P.Stress[c] = slip[c] > 0f ? (float)(Threshold * KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)c, 71)) : 0f;
        }

        /// <summary>A tectonic step: faults load; patches past the threshold are scheduled to rupture inside the step.</summary>
        public static void Step(Simulation.World w, double dt, float[] slip)
        {
            var P = w.Planet;
            double heal = DMath.Exp(-dt / HealYears);
            for (int c = 0; c < P.Cells; c++)
            {
                if (slip[c] > 0f) P.Stress[c] = (float)(P.Stress[c] + slip[c] * dt);
                else if (P.Stress[c] > 0f) { double v = P.Stress[c] * heal; P.Stress[c] = v < 2.0 ? 0f : (float)v; }
            }
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.Stress[c] < Threshold) continue;
                double u = KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)c, (ulong)w.Now, 72);
                long at = w.Now + 1 + DMath.ToLong(u * dt * SimTime.Year);
                w.Queue.Push(at, Scheduling.Phase.Natural, Scheduling.EventKind.Quake, 0, 0, c);
            }
        }

        /// <summary>
        /// The rupture from cell c0 (if it still holds its stress): the cascade along the fault, returning the ruptured
        /// cells and the stress released (in units of the threshold). Fault cells are those holding stress (state).
        /// </summary>
        public static double Cascade(Simulation.World w, int c0, List<int> ruptured)
        {
            var P = w.Planet;
            var g = P.Grid;
            ruptured.Clear();
            if (P.Stress[c0] < Threshold) return 0;
            double released = 0;
            var stack = new List<int> { c0 };
            while (stack.Count > 0)
            {
                int c = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                double s = P.Stress[c];
                if (s < Threshold) continue;
                // a patch keeps a little stress (and stays part of the fault)
                double rest = Threshold * (0.02 + 0.08 * KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)c, (ulong)w.Now, 73));
                P.Stress[c] = (float)rest;
                released += s - rest;
                ruptured.Add(c);
                for (int q = 0; q < 4; q++)
                {
                    int nb = g.Nbr4[c * 4 + q];
                    if (P.Stress[nb] <= 0f) continue;                      // not a fault
                    P.Stress[nb] = (float)(P.Stress[nb] + Pass * (s - rest));
                    if (P.Stress[nb] >= Threshold) stack.Add(nb);
                }
                if (ruptured.Count > 600) break;                          // a planet-scale rupture is enough
            }
            return released / Threshold;
        }

        /// <summary>Moment magnitude from the stress released (in thresholds): one patch ~5.5, ten ~6.9, a hundred ~8.3.</summary>
        public static double Magnitude(double released) => 5.5 + 1.4 * DMath.Log(DMath.Max(0.05, released)) / DMath.Log(10.0);

        /// <summary>
        /// Shaking (Mercalli-like) at a distance (m) from the rupture: ~7 over a one-patch quake, 9 over an M 7 (8 a km
        /// away, 7 at 3 km), ~10 over a great one - a small world feels its quakes across much of itself.
        /// </summary>
        public static double Intensity(double magnitude, double distance) => 6.42 + 1.333 * magnitude - 2.5 * DMath.Log(distance + 500.0) / DMath.Log(10.0);

        /// <summary>How far (m) a quake shakes at intensity 6 or more (at most 15 km).</summary>
        public static double Reach(double magnitude) => DMath.Clamp(DMath.Exp((6.42 + 1.333 * magnitude - 6.0) / 2.5 * DMath.Log(10.0)) - 500.0, 300.0, 15000.0);

        /// <summary>The distance (m) from a cell to the nearest ruptured cell.</summary>
        public static double DistanceTo(PlanetState P, int c, List<int> ruptured)
        {
            double best = double.MaxValue;
            Vec3d at = P.Grid.Center[c];
            for (int i = 0; i < ruptured.Count; i++)
            {
                double d = Vec3d.ArcDistance(at, P.Grid.Center[ruptured[i]], P.Radius);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
