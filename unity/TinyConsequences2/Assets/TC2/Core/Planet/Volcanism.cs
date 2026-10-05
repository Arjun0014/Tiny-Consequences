using System;
using System.Collections.Generic;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>A volcano: a vent over a supply of magma, its pressure, and what it has done.</summary>
    public struct Vent
    {
        public int Id;            // entity id (history names it)
        public int Cell;
        public Vec3d At;          // where it is: it rides its plate
        public int Plate;         // the plate it rides
        public byte Kind;         // 0 plate boundary (an arc over subduction, or a rift), 1 hotspot
        public byte Flags;        // 1: has risen from the sea as an island
        public double Pressure;   // eruptible magma gathered (units of magma)
        public long Born, LastEruption, Next;   // Next: the scheduled eruption (0 none)
        public int Eruptions;
        public float Supply;      // magma reaching it per year lately (smoothed)
        public float Seal;        // how hard the vent holds (x the critical pressure; lognormal at birth)
        public float Cone;        // metres the last eruptions piled up, laid on the relief at the next tectonic step
    }

    /// <summary>A mantle plume: fixed under the moving plates.</summary>
    public struct Hotspot
    {
        public Vec3d At;
        public double Rate;       // magma per year
    }

    /// <summary>
    /// Volcanoes (05 §3b, D2-028). Magma rises where ocean crust dives under another plate (arcs, fed by the boundary's
    /// convergence from the tectonic step), over fixed mantle plumes (hotspots: the plates drift over them, so their
    /// vents form chains of islands), and a little where continents rift. Each vent draws the magma of the ground
    /// around it; where magma gathers beyond any vent's reach, a new vent opens. A vent pours part of its magma out
    /// quietly (plume basalt mostly, arcs little) and builds its mountain; the rest is pressure held by the vent's seal
    /// (lognormal per vent: most vents erupt often and small, a few stay plugged and blow big). Eruptions come by a
    /// hazard that rises steeply with pressure; their size (VEI) follows the pressure released. An eruption is an event
    /// (World.Erupt): lava down the steepest way sets the land alight, ash falls on woods and fields, near a great one
    /// people die, a veil cools the climate - each with the eruption as its cause; the cone it piles up is laid on the
    /// relief at the next tectonic step (relief changes only where the drainage is rebuilt). Outgassing feeds the
    /// carbon cycle in Climate. Vents ride their plates; one whose crust went under, or long quiet with nothing feeding
    /// it, dies.
    /// </summary>
    public static class Volcanism
    {
        public const double ArcGain = 0.9;           // magma per year per m/yr of subduction, per boundary edge (Tectonics)
        public const double VentOpen = 100.0;        // magma a cell must gather (beyond any vent's reach) to open a vent
        public const double ReachCells = 5.5;        // a vent draws magma from this far (in cell spacings)
        public const double DrawShare = 0.6;         // ... this share of it each step
        public const double MagmaYears = 400.0;      // magma nobody draws cools away over this time
        public const double PressureCrit = 600.0;    // pressure at which an eruption is ~7 % likely in a decade
        public const long ExtinctAfter = 600L * SimTime.Year;
        public const double EffusiveHotspot = 0.6;   // share of the magma a hotspot vent pours out quietly (shield-building)
        public const double EffusiveBoundary = 0.2;  // ... an arc or rift vent (most of its magma stays to build pressure)
        public const double BuildPerMagma = 0.3;     // m of new rock at the vent per unit poured out (0.4 x round it)
        public const double HotspotSeal = 0.3;       // plume basalt is runny: its vents hold little pressure

        [ThreadStatic] static List<int> _cells;

        /// <summary>Worldgen: a few mantle plumes (fixed directions), keyed by the seed.</summary>
        public static Hotspot[] MakeHotspots(ulong seed)
        {
            var rs = new RandomSequence(KeyedRng.Hash(seed, RngStream.Tectonics, 777));
            int n = 2 + (int)(rs.NextUnit() * 3);
            var h = new Hotspot[n];
            for (int i = 0; i < n; i++)
                h[i] = new Hotspot { At = new Vec3d(rs.NextNormal(), rs.NextNormal(), rs.NextNormal()).Normalized, Rate = 4.0 + 8.0 * rs.NextUnit() };
            return h;
        }

        /// <summary>
        /// Worldgen: the volcanoes the planet already has - magma gathered from the present plate boundaries and plumes
        /// for some millennia without eruptions, history or new rock; each vent then holds part of its critical
        /// pressure (spread so they do not all erupt at once), and the supply they draw is the carbon cycle's reference.
        /// </summary>
        public static void SpinUp(Simulation.World w)
        {
            var P = w.Planet;
            w.Tect.Forcing(P);
            for (int i = 0; i < 100; i++) Gather(w, Tectonics.StepYears, w.Tect.ArcSupply, false);
            double supply = 0;
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                double u = KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)vt.Id, 43);
                vt.Pressure = PressureCrit * vt.Seal * u;
                vt.Born = 0; vt.LastEruption = 0;
                P.Vents[v] = vt;
                supply += vt.Supply;
                w.History.Register(new EntityRef(EntityKind.Place, vt.Id), Worldgen.Names.Mountain(w.Seed, vt.Id), 0);
            }
            P.SupplyRef = DMath.Max(1.0, supply);
        }

        /// <summary>Vents ride their plates (before the tectonic step, as villages do).</summary>
        public static void Ride(Simulation.World w, double dt)
        {
            var P = w.Planet;
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                var plate = P.Plates[vt.Plate];
                double a = plate.Omega * dt;
                vt.At = Tectonics.Rotate(vt.At, plate.Pole, DMath.Cos(a), DMath.Sin(a));
                vt.Cell = P.Grid.CellOf(vt.At);
                P.Vents[v] = vt;
            }
        }

        /// <summary>A step of the plates' magma (dt years, inside the tectonic step): build cones, gather, schedule.</summary>
        public static void Step(Simulation.World w, double dt, float[] arcSupply)
        {
            var P = w.Planet;
            // the cones of the last eruptions join the relief (the drainage is rebuilt after this step)
            for (int v = 0; v < P.Vents.Count; v++)
            {
                var vt = P.Vents[v];
                if (vt.Cone <= 0f) continue;
                double cone = vt.Cone;
                vt.Cone = 0f;
                P.Vents[v] = vt;
                Build(w, v, cone);
            }
            Gather(w, dt, arcSupply, true);
            // the hazard of an eruption rises steeply with pressure; its moment falls somewhere in the coming step
            var vents = P.Vents;
            for (int v = 0; v < vents.Count; v++)
            {
                var vt = vents[v];
                if (vt.Next != 0) continue;
                double x = vt.Pressure / (PressureCrit * vt.Seal);
                double rate = 0.0069 * x * x * x;                     // per year: ~7 % a decade at the critical pressure
                double p = 1.0 - DMath.Exp(-rate * dt);
                double u = KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)vt.Id, (ulong)w.Now, 31);
                if (u >= p) continue;
                double when = KeyedRng.Unit(w.Seed, RngStream.Tectonics, (ulong)vt.Id, (ulong)w.Now, 32);
                vt.Next = w.Now + 1 + DMath.ToLong(when * dt * SimTime.Year);
                vents[v] = vt;
                w.Queue.Push(vt.Next, Scheduling.Phase.Natural, Scheduling.EventKind.Eruption, 0, 0, vt.Id);
            }
        }

        /// <summary>Magma rises, vents draw it (pouring part out, holding the rest), dead vents go, new ones open.</summary>
        static void Gather(Simulation.World w, double dt, float[] arcSupply, bool live)
        {
            var P = w.Planet;
            var g = P.Grid;
            double keep = DMath.Exp(-dt / MagmaYears);
            for (int c = 0; c < P.Cells; c++)
                if (P.Magma[c] != 0f || arcSupply[c] != 0f) P.Magma[c] = (float)(P.Magma[c] * keep + arcSupply[c] * dt);
            for (int i = 0; i < P.Hotspots.Length; i++)
            {
                int c = g.CellOf(P.Hotspots[i].At);
                P.Magma[c] = (float)(P.Magma[c] + P.Hotspots[i].Rate * dt);
            }
            var cells = _cells ?? (_cells = new List<int>());
            double reach = ReachCells * P.CellSize;
            var vents = P.Vents;
            for (int v = 0; v < vents.Count; v++)
            {
                var vt = vents[v];
                cells.Clear();
                w.Hazards.CellsWithin(w, vt.Cell, reach, cells);
                double take = 0;
                for (int k = 0; k < cells.Count; k++)
                {
                    int c = cells[k];
                    float m = P.Magma[c];
                    if (m == 0f) continue;
                    float d = (float)(m * DrawShare);
                    take += d;
                    P.Magma[c] = m - d;
                }
                double quiet = take * (vt.Kind == 1 ? EffusiveHotspot : EffusiveBoundary);
                vt.Pressure += take - quiet;
                vt.Supply = (float)(vt.Supply + (take / dt - vt.Supply) * 0.2);
                vents[v] = vt;
                if (live && quiet > 0) Build(w, v, quiet * BuildPerMagma);
            }
            // a vent whose crust went under another plate, or long quiet with nothing feeding it, dies
            for (int v = vents.Count - 1; v >= 0; v--)
            {
                var vt = vents[v];
                bool subducted = P.Plate[vt.Cell] != vt.Plate && P.Plate[g.Nbr4[vt.Cell * 4]] != vt.Plate && P.Plate[g.Nbr4[vt.Cell * 4 + 2]] != vt.Plate;
                bool spent = vt.Supply < 0.01f && w.Now - DMath.Max(vt.LastEruption, vt.Born) > ExtinctAfter;
                if (vt.Next == 0 && (subducted || (live && spent)))
                {
                    if (live) w.RecordVentExtinct(vt);
                    vents.RemoveAt(v);
                }
            }
            // new vents where magma gathers beyond any vent's reach
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.Magma[c] < VentOpen) continue;
                if (NearVent(P, c, ReachCells)) continue;
                int id = w.Ids.Next(EntityKind.Place);
                double z = KeyedRng.Normal(KeyedRng.Hash(w.Seed, RngStream.Tectonics, (ulong)id, 41), KeyedRng.Hash(w.Seed, RngStream.Tectonics, (ulong)id, 42));
                byte kind = KindAt(P, c);
                var vt = new Vent
                {
                    Id = id, Cell = c, At = g.Center[c], Plate = P.Plate[c], Kind = kind, Pressure = P.Magma[c], Born = w.Now,
                    Seal = (float)(DMath.Exp(0.9 * z) * (kind == 1 ? HotspotSeal : 1.0)),
                };
                P.Magma[c] = 0f;
                vents.Add(vt);
                if (live) w.RecordVentBorn(vt);
            }
        }

        /// <summary>New rock at a vent (lava poured out, or a cone): the mountain grows; a sea vent may become an island.</summary>
        static void Build(Simulation.World w, int v, double metres)
        {
            var P = w.Planet;
            var g = P.Grid;
            var vt = P.Vents[v];
            int c = vt.Cell;
            P.Elev[c] = (float)DMath.Min(4500.0, P.Elev[c] + metres);
            for (int q = 0; q < 8; q++) { int nb = g.Nbr8[c * 8 + q]; P.Elev[nb] = (float)DMath.Min(4500.0, P.Elev[nb] + 0.4 * metres); }
            if ((vt.Flags & 1) == 0 && P.IsLand(c) && P.Crust[c] == 0)
            {
                vt.Flags |= 1;
                P.Vents[v] = vt;
                w.RecordIslandRose(vt);
            }
        }

        static byte KindAt(PlanetState P, int c)
        {
            double lim = DMath.Cos(ReachCells * P.Grid.MeanSpacing);
            for (int i = 0; i < P.Hotspots.Length; i++)
                if (Vec3d.Dot(P.Grid.Center[c], P.Hotspots[i].At) > lim) return 1;
            return 0;
        }

        /// <summary>Is a vent within this many cell spacings of cell c?</summary>
        public static bool NearVent(PlanetState P, int c, double cells)
        {
            double lim = DMath.Cos(cells * P.Grid.MeanSpacing);
            Vec3d at = P.Grid.Center[c];
            for (int v = 0; v < P.Vents.Count; v++)
                if (Vec3d.Dot(P.Vents[v].At, at) > lim) return true;
            return false;
        }

        public static int IndexOf(PlanetState P, int ventId)
        {
            for (int v = 0; v < P.Vents.Count; v++) if (P.Vents[v].Id == ventId) return v;
            return -1;
        }

        /// <summary>
        /// The size of an eruption from the pressure it releases: VEI 1-7 (each a tenfold step). A vent erupting at its
        /// typical critical pressure gives VEI 2; one holding four times that, VEI 4; twenty times, VEI 6.
        /// </summary>
        public static int Vei(double pressure) =>
            (int)DMath.Clamp(DMath.Floor(2.0 + DMath.Log(DMath.Max(1e-6, pressure / (1.2 * PressureCrit))) / DMath.Log(10.0) * 3.1), 1, 7);
    }
}
