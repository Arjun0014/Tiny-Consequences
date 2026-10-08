using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// A village's streets (C13.1, D2-116). Villages grow along their roads: the ways out of a village that its people wear to paths become
    /// its streets, and the frontage along them is the lot a household seeks first - closer to the comings and goings, the carts and the
    /// neighbours' traffic - so new houses line the streets and face them, and a market village strings out along its roads (the street
    /// villages and market streets of medieval Europe). A hamlet whose people wear no way to a neighbour keeps its houses round its green
    /// (the spiral of lots, D2-084). A street is durable: laid once, at the bearing at which the way actually leaves the village (read along
    /// its route), it stays while houses line it even when the way falls out of use; ways leaving within 35 degrees of each other are one
    /// street; four at most. Nothing is built in a street (its half-width is kept clear of houses and works).
    /// A town's core (C13.3, D2-118): once a village is a town (`TownPeople`), the frontage of its streets near the green is dear and its plots
    /// are divided - lots at half the step (`Spacing` / 2) out to `CoreR` along each street, each a narrow plot that takes only a narrow house
    /// (the burgage plots of medieval towns, split as the town filled): rows of tall narrow town houses along the main streets. The reach grows
    /// with the town and is never given back (a divided plot stays divided).
    /// Back lanes (C13.2, D2-117): behind each street's frontage, a lane runs parallel to it on either side (`LaneOff` out, so the
    /// frontage's gardens and the lane's meet back to back), lined from `LaneStart` out - the two-row village with its back lanes, the
    /// town's blocks between street and lane. A lane's lots are sought less than the street's frontage (`LaneLean`, by their distance
    /// from the green) and more than the scatter round the green, so a village fills its street first, then its lanes; houses face the
    /// street or lane they front; nothing is built in a lane. Streets and lanes are a village's lines: line 3k is street k, 3k+1 and 3k+2
    /// its lanes.
    /// </summary>
    public static class Streets
    {
        /// <summary>Streets a village keeps at most.</summary>
        public const int Most = 4;
        /// <summary>Radians within which two ways leave by one street (35 degrees).</summary>
        public const double Merge = 0.6108652381980153;
        /// <summary>m along the way at which a street's bearing is read (where it leaves the village).</summary>
        public const double Look = 80.0;
        /// <summary>m between frontage lots along a street (a longhouse's length and a gap), the house's set back from the street's line,
        /// and the first frontage lot's distance from the square.</summary>
        public const double Spacing = 13.0, Setback = 8.0, Start = 22.0;
        /// <summary>m either side of a street's line kept clear of houses and works.</summary>
        public const double Width = 4.0;
        /// <summary>A frontage lot is weighed as though it lay this share of its distance from the square (frontage is sought).</summary>
        public const double Lean = 0.7;
        /// <summary>m from a street's line to each of its back lanes (C13.2); where a lane's frontage begins along it; a lane's lot is
        /// weighed as this share of its distance from the square.</summary>
        public const double LaneOff = 36.0, LaneStart = 30.0, LaneLean = 0.85;

        /// <summary>People at which a village is a town whose core frontage is divided (C13.3); the reach of its divided core along its streets:
        /// `CoreFrom` + people / `CorePer` m, at most `CoreMost`.</summary>
        public const int TownPeople = 250;
        public const double CoreFrom = 25.0, CorePer = 12.0, CoreMost = 110.0;

        static readonly List<Way> _ways = new List<Way>();

        /// <summary>Tools: no streets are laid (C13.1 against D2-114 in one binary); false in the game.</summary>
        public static bool None;

        /// <summary>
        /// The village's ways worn to paths that are not yet its streets are laid as streets (read once a lot is sought): the bearing at which
        /// each leaves, merged with a street within 35 degrees.
        /// </summary>
        public static void Refresh(World w, int sid)
        {
            if (None || w.Settlements.Get(sid).StreetN >= Most) return;
            w.Ways.PathsOf(sid, _ways);
            for (int i = 0; i < _ways.Count; i++)
            {
                if (w.Settlements.Get(sid).StreetN >= Most) return;
                int other = _ways[i].A == sid ? _ways[i].B : _ways[i].A;
                if (!w.Settlements.Exists(other)) continue;
                var r = w.Routes.Get(w, sid, other);
                if (r == null || r.Length < 1.0) continue;
                double along = DMath.Min(Look, 0.5 * r.Length);
                Vec3d p = r.PointAt(r.A == sid ? along : r.Length - along);
                ref var s = ref w.Settlements.Get(sid);
                s.SphereToLocal(p, w.Planet.Radius, out double x, out double y);
                double dx = x - s.SquareX, dy = y - s.SquareY;
                if (dx * dx + dy * dy < 1.0) continue;
                double a = DMath.Atan2(dy, dx);
                bool same = false;
                for (int k = 0; k < s.StreetN; k++) if (Apart(a, s.StreetAt(k)) < Merge) { same = true; break; }
                if (same) continue;
                // (C13.3) laid where it runs clearest of what stands: of the bearings within `Bend` of the way's, the one whose street runs through
                // fewest buildings (at most `MostCuts`: those are never renewed in it and come down when left - Homes.Clear - so the street opens over
                // the years), then whose lanes cut fewest, ties to the way's own; a lane that would cut one stays shut until its line is clear. A way
                // that threads between more houses than that is no street yet - it is laid when they have gone
                double best = 0; int least = int.MaxValue, shut = 0;
                for (int k = 0; k <= 2 * BendSteps && least > 0; k++)
                {
                    double b = a + ((k & 1) == 0 ? 1 : -1) * ((k + 1) >> 1) * (Bend / BendSteps);
                    int c = StreetCuts(w, sid, b);
                    if (c > MostCuts) continue;
                    int l1 = LaneCuts(w, sid, b, 1), l2 = LaneCuts(w, sid, b, -1);
                    int score = c * 1000 + l1 + l2;
                    if (score < least) { least = score; best = b; shut = (l1 > 0 ? 1 : 0) | (l2 > 0 ? 2 : 0); }
                }
                if (least == int.MaxValue) continue;
                ref var s2 = ref w.Settlements.Get(sid);
                same = false;
                for (int k = 0; k < s2.StreetN; k++) if (Apart(best, s2.StreetAt(k)) < Merge) { same = true; break; }
                if (same) continue;
                s2.SetStreet(s2.StreetN, best);
                s2.LanesShut = (byte)((s2.LanesShut & ~(3 << (2 * s2.StreetN))) | (shut << (2 * s2.StreetN)));
                s2.StreetN++;
            }
        }

        /// <summary>A shut lane opens once nothing stands in its line (C13.3: the houses it would have run through gone - left and taken down, fallen,
        /// rebuilt elsewhere). Read when a lot is sought.</summary>
        public static void OpenLanes(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.LanesShut == 0) return;
            for (int k = 0; k < s.StreetN; k++)
                for (int side = 0; side < 2; side++)
                {
                    int bit = 1 << (2 * k + side);
                    if ((w.Settlements.Get(sid).LanesShut & bit) == 0) continue;
                    if (LaneCuts(w, sid, w.Settlements.Get(sid).StreetAt(k), side == 0 ? 1 : -1) > 0) continue;
                    ref var s2 = ref w.Settlements.Get(sid);
                    s2.LanesShut = (byte)(s2.LanesShut & ~bit);
                }
        }

        /// <summary>Radians a street may be laid off its way's bearing to run clear of the houses standing (20 degrees), in steps of 2; the most
        /// standing buildings a street is laid through.</summary>
        public const double Bend = 0.3490658503988659;
        public const int BendSteps = 10, MostCuts = 3;

        /// <summary>The standing buildings and camps a street at bearing b would run through (C13.3).</summary>
        public static int StreetCuts(World w, int sid, double b)
        {
            ref var s = ref w.Settlements.Get(sid);
            return Footprints.LineCuts(w, sid, s.SquareX, s.SquareY, DMath.Cos(b), DMath.Sin(b), 12.0);
        }

        /// <summary>The same for the lane on one side (+1 line 3k+1, -1 line 3k+2) of a street at bearing b.</summary>
        public static int LaneCuts(World w, int sid, double b, int side)
        {
            ref var s = ref w.Settlements.Get(sid);
            double ux = DMath.Cos(b), uy = DMath.Sin(b);
            return Footprints.LineCuts(w, sid, s.SquareX - uy * LaneOff * side, s.SquareY + ux * LaneOff * side, ux, uy, LaneStart - 4.0);
        }

        /// <summary>The angle between two bearings (0 to pi).</summary>
        public static double Apart(double a, double b)
        {
            double d = a - b;
            while (d > DMath.PI) d -= 2.0 * DMath.PI;
            while (d < -DMath.PI) d += 2.0 * DMath.PI;
            return DMath.Abs(d);
        }

        /// <summary>The village's lines - its streets and their back lanes (C13.1, C13.2): three for each street.</summary>
        public static int Lines(in Settlement s) => s.StreetN * 3;

        /// <summary>Whether line i is a back lane (else a street).</summary>
        public static bool IsLane(int i) => i % 3 != 0;

        /// <summary>Line i's origin, direction, and the distance along it from which it is lined (a street from the green's edge, a lane from
        /// `LaneStart`).</summary>
        public static void Line(in Settlement s, int i, out double ox, out double oy, out double ux, out double uy, out double from)
        {
            double a = s.StreetAt(i / 3);
            ux = DMath.Cos(a); uy = DMath.Sin(a);
            int m = i % 3;
            double off = m == 0 ? 0.0 : m == 1 ? LaneOff : -LaneOff;
            ox = s.SquareX - uy * off; oy = s.SquareY + ux * off;
            from = m == 0 ? 12.0 : Shut(in s, i) ? Closed : LaneStart - 4.0;   // (a lane not yet open is lined nowhere: C13.3)
        }

        /// <summary>m along a lane not yet open from which it would be lined: beyond any village.</summary>
        public const double Closed = 1e9;

        /// <summary>Whether line i is a back lane not yet open (C13.3: it would run through houses that stood when its street was laid).</summary>
        public static bool Shut(in Settlement s, int i) => i % 3 != 0 && (s.LanesShut & (1 << (2 * (i / 3) + (i % 3) - 1))) != 0;

        /// <summary>A lot along line i: the j-th (from 0; a half step is a divided plot in a town's core), on side -1 or +1, set back from the line.</summary>
        public static void Lot(in Settlement s, int i, double j, int side, out double x, out double y)
        {
            Line(in s, i, out double ox, out double oy, out double ux, out double uy, out _);
            double d = (IsLane(i) ? LaneStart : Start) + j * Spacing;
            x = ox + ux * d - uy * Setback * side;
            y = oy + uy * d + ux * Setback * side;
        }

        /// <summary>How a lot along line i is weighed against the scatter round the green (lower is sought first).</summary>
        public static double Weigh(in Settlement s, int i, double j, double x, double y)
        {
            if (!IsLane(i)) return Lean * (Start + j * Spacing);
            double dx = x - s.SquareX, dy = y - s.SquareY;
            return LaneLean * DMath.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>A town's divided core reaches further as it grows (C13.3): read when a lot is sought; never shrinks.</summary>
        public static void Grow(ref Settlement s)
        {
            if (None || s.Population < TownPeople || s.StreetN == 0) return;
            double r = DMath.Min(CoreMost, CoreFrom + s.Population / CorePer);
            if (r > s.CoreR) s.CoreR = (float)r;
        }

        /// <summary>
        /// Whether a lot at (x, y) is a divided plot of a town's core (C13.3): on a street's frontage (not a lane's) within the core's reach of the
        /// green - a plot half a lot wide, that takes a narrow house.
        /// </summary>
        public static bool CorePlot(in Settlement s, double x, double y)
        {
            if (s.CoreR <= 0f) return false;
            if (!Facing(in s, x, y, out double fx, out double fy, out int line) || IsLane(line)) return false;
            double dx = fx - s.SquareX, dy = fy - s.SquareY;
            return dx * dx + dy * dy <= (double)s.CoreR * s.CoreR + 1e-6;
        }

        /// <summary>Whether a point lies in a street or a lane (within `half` of its line, where it is lined).</summary>
        public static bool On(in Settlement s, double x, double y, double half)
        {
            int n = Lines(in s);
            for (int i = 0; i < n; i++)
            {
                Line(in s, i, out double ox, out double oy, out double ux, out double uy, out double from);
                double px = x - ox, py = y - oy;
                double along = px * ux + py * uy;
                if (along < from) continue;
                double off = DMath.Abs(-px * uy + py * ux);
                if (off < half) return true;
            }
            return false;
        }

        /// <summary>
        /// What a house at (x, y) faces: the nearest point of a street or lane within its frontage (C13.1, C13.2), else the square. True when
        /// it fronts one.
        /// </summary>
        public static bool Facing(in Settlement s, double x, double y, out double fx, out double fy) => Facing(in s, x, y, out fx, out fy, out _);

        /// <summary>The same, with the line it fronts (-1 none).</summary>
        public static bool Facing(in Settlement s, double x, double y, out double fx, out double fy, out int line)
        {
            fx = s.SquareX; fy = s.SquareY; line = -1;
            double best = Setback + 6.0;
            int n = Lines(in s);
            for (int i = 0; i < n; i++)
            {
                Line(in s, i, out double ox, out double oy, out double ux, out double uy, out double from);
                double px = x - ox, py = y - oy;
                double along = px * ux + py * uy;
                if (along < from) continue;
                double off = DMath.Abs(-px * uy + py * ux);
                if (off >= best) continue;
                best = off; line = i;
                fx = ox + ux * along; fy = oy + uy * along;
            }
            return line >= 0;
        }
    }
}
