using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// Timber from the woods (C1.2, D2-091; 22 §1, §4): every load of wood a village burns or builds with is felled somewhere. Its people
    /// cut at a woodlot - the nearest cell within reach whose woods are worth going to - and each 25 kg load takes its mass from that
    /// cell's forest share (a full cover stands ~120 t of wood a hectare); when the woodlot is cut down to scrub they go to the next, and
    /// the walk out and every load carried home cost hours, so the woods' retreat is paid in labour. The woods regrow by the vegetation's
    /// own law where people stop cutting (`PlanetSystems.GrowVegetation`), and fields keep them off the share they farm. One law for
    /// lived days and COARSE/DEEP steps.
    /// </summary>
    public static class Woods
    {
        /// <summary>kg of wood standing on a hectare of full forest cover (temperate woods: 100-200 t of dry wood a hectare).</summary>
        public const double KgPerHa = 120000.0;
        /// <summary>A load: what a grown person carries (Trade.LoadKg; Construction's wood unit).</summary>
        public const double LoadKg = 25.0;
        /// <summary>m: the farthest woods a village fetches from (an hour's walk there and back with a load is already most of a load's work).</summary>
        public const double Reach = 1500.0;
        /// <summary>A woodlot is worth going to while its forest share is at least this.</summary>
        public const double Worth = 0.12;
        /// <summary>What felling leaves of a cell's cover: stumps, scrub and the trees too poor to take.</summary>
        public const double Floor = 0.02;
        /// <summary>m: the near woods (the old woodlot's distance, D2-003): loads felled within it cost no more walking than they always did.</summary>
        public const double Near = 210.0;
        /// <summary>Hours of woodcutting a lived stint gives between walking out and home (COARSE charges the walk once a stint).</summary>
        public const double Stint = 3.25;

        /// <summary>Loads standing on a cell above the felling floor.</summary>
        public static double LoadsIn(PlanetState P, int c) => DMath.Max(0.0, (double)P.Forest[c] - Floor) * KgPerHa / LoadKg * P.AreaHa(c);

        /// <summary>The woodlot's cell (-1 none chosen).</summary>
        public static int Lot(in Settlement s) => s.WoodLot - 1;

        /// <summary>
        /// Extra hours a load costs beyond the near woods' (carrying it home from farther off: both ways at a walk), for a woodlot this far.
        /// </summary>
        public static double ExtraHours(double dist, double walk) => DMath.Max(0.0, 2.0 * (dist - Near) / walk / 3600.0);

        /// <summary>Loads an hour of woodcutting brings home, for a cutter who fells and splits r0 loads an hour, at a woodlot this far.</summary>
        public static double Rate(double r0, double dist, double walk) => r0 / (1.0 + r0 * ExtraHours(dist, walk));

        /// <summary>The share of a COARSE day's woodcutting hours left after walking out to the woodlot and home once a stint.</summary>
        public static double AfterWalk(double dist, double walk) => Stint / (Stint + ExtraHours(dist, walk));

        static readonly List<int> _cells = new List<int>();

        /// <summary>
        /// The woodlot a village goes to now: among the cells within reach of its own - land, not under ice or fields - with woods worth
        /// going to, the one that is nearest for the woods it holds ((d + 200 m) / (forest + 0.2): a little farther for much denser woods).
        /// Sets WoodX/WoodY (where its woodcutters walk) and its distance; history when the woods have receded past a mark. -1 none.
        /// </summary>
        public static int Choose(World w, int sid)
        {
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            w.Hazards.CellsWithin(w, s.Cell, Reach, _cells);
            int best = -1; double bv = double.MaxValue, bd = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                int c = _cells[i];
                if (!P.IsLand(c) || P.Ice[c] > 0.5f || P.FieldOwner[c] != 0 || P.Forest[c] < Worth) continue;
                double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                if (d > Reach) continue;
                double v = (d + 200.0) / ((double)P.Forest[c] + 0.2);
                if (v < bv) { bv = v; best = c; bd = d; }
            }
            ref var s2 = ref w.Settlements.Get(sid);
            s2.WoodLot = best + 1;
            if (best < 0) { Receded(w, sid, 4, 0); return -1; }
            s2.WoodDist = (float)bd;
            Stone.Saw(w, sid, 0, best);   // (the woodcutters see what rock shows there: C1.3)
            s2.SphereToLocal(P.Grid.Center[best], P.Radius, out double x, out double y);
            s2.WoodX = (float)x; s2.WoodY = (float)y;
            byte far = bd >= 1500 ? (byte)3 : bd >= 1000 ? (byte)2 : bd >= 600 ? (byte)1 : (byte)0;
            if (far > s2.WoodFar) Receded(w, sid, far, bd);
            return best;
        }

        /// <summary>The woods have receded past a mark (600 m, 1 km, 1.5 km, or none left within reach): once a village, each mark.</summary>
        static void Receded(World w, int sid, byte far, double dist)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (far <= s.WoodFar) return;
            s.WoodFar = far;
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.WoodsReceded, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Magnitude = (float)dist, ArgA = far, RootKind = RootKind.People,
            });
            w.Metrics.WoodsReceded++;
        }

        /// <summary>
        /// Loads felled at the village's woodlot (and the next, as one is cut down): each takes its mass from the cell's forest share.
        /// Returns the loads there were (fewer than asked when the woods within reach are gone).
        /// </summary>
        public static double Fell(World w, int sid, double loads)
        {
            if (loads <= 0) return 0;
            var P = w.Planet;
            double had = 0;
            for (int guard = 0; guard < 8 && loads - had > 1e-9; guard++)
            {
                int c = Ensure(w, sid);
                if (c < 0) break;
                double per = KgPerHa / LoadKg * P.AreaHa(c);   // loads in a whole share of the cell
                double take = DMath.Min(loads - had, LoadsIn(P, c));
                P.Forest[c] = (float)DMath.Max(Floor, (double)P.Forest[c] - take / per);
                had += take;
                w.Metrics.WoodFelled += take;
                if (P.Forest[c] < Worth) Choose(w, sid);   // (cut down: the next woodlot for the next loads)
            }
            return had;
        }

        /// <summary>The woodlot to cut at now: the chosen one while it is worth going to, else the next; with none within reach a village
        /// looks again only every twelve days (the woods regrow slowly).</summary>
        public static int Ensure(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            int c = Lot(in s);
            if (c >= 0 && w.Planet.Forest[c] >= Worth) return c;
            if (c < 0 && s.WoodLook != 0 && w.Now - s.WoodLook < 12 * SimTime.Day) return -1;
            s.WoodLook = w.Now;
            return Choose(w, sid);
        }

        /// <summary>Loads an hour of woodcutting brings home now at the village's woodlot (0 with none within reach).</summary>
        public static double HourRate(World w, int sid, double r0, bool coarse)
        {
            if (Ensure(w, sid) < 0) return 0;
            ref var s = ref w.Settlements.Get(sid);
            double walk = w.Config.WalkSpeed;
            return Rate(r0, s.WoodDist, walk) * (coarse ? AfterWalk(s.WoodDist, walk) : 1.0);
        }

        /// <summary>
        /// Woodcutting hours given at a village: the loads they bring home at its woodlot's distance, felled from the woods, into its store.
        /// `coarse`: a COARSE/DEEP share of a day also pays the walk out and home once a stint (a lived stint walks it). Returns loads.
        /// </summary>
        public static double Cut(World w, int sid, double hours, double r0, bool coarse)
        {
            if (hours <= 0 || r0 <= 0) return 0;
            if (Ensure(w, sid) < 0) return 0;
            ref var s2 = ref w.Settlements.Get(sid);
            double walk = w.Config.WalkSpeed;
            double eff = hours * (coarse ? AfterWalk(s2.WoodDist, walk) : 1.0);
            double loads = Fell(w, sid, eff * Rate(r0, s2.WoodDist, walk));
            ref var s3 = ref w.Settlements.Get(sid);
            s3.Woodstore += loads;
            return loads;
        }

        /// <summary>
        /// The most woods a field cell keeps (C1.2): the village's fields lie over its claimed cells by their arable land (a share
        /// `Settlement.FieldHa` / `ArableHa` of each cell's arable share), and what is farmed is open ground. A cell's arable share itself
        /// grows as its woods are cleared (`PlanetState.Arability`: x (1 - 0.6 forest)), so the cover the fields leave is the fixed point
        /// F = 1 - a (1 - 0.6 F), a = used x arable-once-cleared: F = (1 - a) / (1 - 0.6 a). 1 off the fields.
        /// </summary>
        public static double Open(World w, int c, double meanTemp)
        {
            var P = w.Planet;
            int owner = P.FieldOwner[c];
            if (owner == 0 || !w.Settlements.Exists(owner)) return 1.0;
            ref var s = ref w.Settlements.Get(owner);
            if (s.ArableHa <= 0) return 1.0;
            double a = DMath.Min(0.95, DMath.Clamp01(s.FieldHa / s.ArableHa) * P.ArabilityOpen(c, meanTemp));
            return (1.0 - a) / (1.0 - 0.6 * a);
        }

        /// <summary>The share of a cell under fields (1 - the cover they leave).</summary>
        public static double Farmed(World w, int c, double meanTemp) => 1.0 - Open(w, c, meanTemp);
    }
}
