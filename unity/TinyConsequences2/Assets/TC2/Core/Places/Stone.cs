using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Stone from the ground (C1.3, D2-092; 22 §1-§2): rock shows where the land breaks steeply (a cell 40 m above or below a neighbour -
    /// the quern's rule, D2-063). A village knows rock only where its people have seen it - the grounds its hunters, fishers and gatherers
    /// go to, its woodlot, the land its scouts look over - and keeps the nearest it knows. When its people want stone (a house raised on stone
    /// footings stands twice as long) and know rock within reach, they open a quarry there (a structure at the rock) and cut it: ~3 loads an
    /// hour of rubble, every load carried home - at a quarry some hundreds of metres off the walk is most of the work. Villages with no rock
    /// near build on earth-fast posts. One law for lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Stone
    {
        /// <summary>m: the farthest rock a village quarries - stone "at hand" (beyond it, it builds on posts as its neighbours do).</summary>
        public const double Reach = 1000.0;
        /// <summary>m: the step between neighbouring land cells where rock shows through (land to land: a coast's drop to the sea bed is no rock).</summary>
        public const double Step = 80.0;
        /// <summary>Loads of rubble an hour of quarrying cuts (prising, breaking, dressing to a carrying size), by a fit grown person.</summary>
        public const double CutPerHour = 3.0;
        /// <summary>Loads a house's footings take: pad stones and a low sill under its frame (~1 t).</summary>
        public const double FootingLoads = 40.0;
        /// <summary>How many houses' footings a village keeps stone ahead for.</summary>
        public const int AheadHouses = 3;

        /// <summary>
        /// Rock showing on a cell: land that breaks 80 m or more from a neighbouring land cell (a crag, a scarp, a gorge side: ~9-16 % of
        /// the land on the 6 km worlds, so some villages have rock at hand and others none).
        /// </summary>
        public static bool Outcrop(PlanetState P, int c)
        {
            if (!P.IsLand(c) || P.Ice[c] > 0.5f) return false;
            for (int k = 0; k < 4; k++)
            {
                int nb = P.Grid.Nbr4[c * 4 + k];
                if (P.IsLand(nb) && DMath.Abs((double)P.Elev[nb] - P.Elev[c]) >= Step) return true;
            }
            return false;
        }

        /// <summary>The rock the village knows (-1 none).</summary>
        public static int Known(in Settlement s) => s.StoneCell - 1;

        /// <summary>
        /// Someone of a village (0: its people at large) was at a cell - a hunting or gathering ground, a fishing shore, the woodlot, a scout's
        /// look-out - and saw what rock shows there or beside it. The nearest rock within reach that the village knows is kept; the first is
        /// history.
        /// </summary>
        public static void Saw(World w, int sid, int pid, int cell)
        {
            if (cell < 0 || !w.Settlements.Exists(sid)) return;
            Mining.Saw(w, sid, pid, cell);   // (and copper, where it shows: C5.1)
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            int best = -1; double bd = s.StoneCell > 0 ? s.StoneDist : Reach + 1;
            for (int k = -1; k < 8; k++)
            {
                int c = k < 0 ? cell : P.Grid.Nbr8[cell * 8 + k];
                if (!Outcrop(P, c) || !Shore.Dry(P, P.Grid.Center[c])) continue;   // (rock above the shore: one in the shallows was drowned and opened again and again)
                double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                if (d < bd - 1e-6) { bd = d; best = c; }
            }
            if (best < 0) return;
            bool first = s.StoneCell == 0;
            s.StoneCell = best + 1; s.StoneDist = (float)bd;
            if (first)
                s.StoneEvent = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.StoneFound, Time = w.Now, Cell = best, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Other = pid != 0 ? EntityRef.Person(pid) : default, Magnitude = (float)bd, RootKind = RootKind.People,
                });
        }

        /// <summary>
        /// Loads of stone the village wants in store (read once a day): footings for its next houses - the households living in huts that will
        /// raise houses, a few ahead - while it knows rock within reach. 0 without.
        /// </summary>
        public static double Want(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.StoneCell == 0 || !s.Inhabited) return 0;
            int huts = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Shelter < Construction.House) huts++;
            return FootingLoads * DMath.Min(huts + 1, AheadHouses);
        }

        /// <summary>The village's standing quarry (0 none).</summary>
        public static int Quarry(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Quarry != 0 && w.Buildings.Exists(s.Quarry) && Structures.Working(in w.Buildings.Get(s.Quarry), w.Now)) return s.Quarry;
            return 0;
        }

        /// <summary>The village wants stone cut now: a quarry stands and its store is short of what it wants.</summary>
        public static bool Wanted(World w, in Settlement s, out int quarry)
        {
            quarry = 0;
            if (s.StoneWant <= 0 || s.Stone >= s.StoneWant) return false;
            quarry = Quarry(w, s.Id);
            return quarry != 0;
        }

        /// <summary>Loads of stone a quarrier cuts in an hour (a fit grown person ~3; worn tools and a weak body less; metal tools more).</summary>
        public static double CutRate(in Agents.Person p, double tools) => CutPerHour * (0.5 + 0.5 * p.Health) * tools;

        /// <summary>Loads an hour of quarrying brings home from a quarry this far: every load carried both ways.</summary>
        public static double Rate(double r0, double dist, double walk) => r0 / (1.0 + r0 * 2.0 * dist / walk / 3600.0);

        /// <summary>The share of a COARSE day's quarrying hours left after the walk out and home once a stint.</summary>
        public static double AfterWalk(double dist, double walk) => Woods.Stint / (Woods.Stint + 2.0 * dist / walk / 3600.0);

        /// <summary>Loads an hour of quarrying brings home at the village's quarry now.</summary>
        public static double HourRate(World w, int sid, int quarry, double r0, bool coarse)
        {
            if (quarry == 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            double walk = w.Config.WalkSpeed, d = s.StoneDist;
            return Rate(r0, d, walk) * (coarse ? AfterWalk(d, walk) : 1.0);
        }

        /// <summary>
        /// Quarrying hours given at the village's quarry: the loads cut and carried home into its store; the quarry's face grows by them.
        /// Returns loads.
        /// </summary>
        public static double Cut(World w, int sid, int quarry, double hours, double r0, bool coarse)
        {
            if (hours <= 0 || quarry == 0) return 0;
            double loads = hours * HourRate(w, sid, quarry, r0, coarse);
            ref var s = ref w.Settlements.Get(sid);
            s.Stone += loads;
            ref var q = ref w.Buildings.Get(quarry);
            q.Stock = (float)(q.Stock + loads); q.Use = (float)(q.Use + hours);
            w.Metrics.StoneCut += loads; w.Metrics.QuarryHours += hours;
            return loads;
        }

        /// <summary>
        /// A household raises a house (Construction): with stone in the village's store for its footings, it stands on them (`Building.Mat`
        /// 1: twice as long before it falls once left, D2-059's 60 years -> 120). Returns whether it did.
        /// </summary>
        public static bool Footings(World w, int sid, int bid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Stone < FootingLoads || !w.Buildings.Exists(bid)) return false;
            s.Stone -= FootingLoads;
            ref var b = ref w.Buildings.Get(bid);
            b.Mat = 1; b.Stone = (float)(b.Stone + FootingLoads);
            w.Metrics.Footings++;
            return true;
        }
    }
}
