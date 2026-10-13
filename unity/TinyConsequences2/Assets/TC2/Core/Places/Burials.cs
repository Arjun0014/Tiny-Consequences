using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The village's burial ground (`27` §6: famine and sickness made visible; history in the ground): its dead are buried where they lived - or
    /// in the village they died in - and the drowned, those the sea's wave or a mountain's fire took leave no grave. A grave is fresh earth for
    /// about a year, then grassed over, and sinks from sight over decades: two numbers a village (graves marked, graves fresh) decaying with time,
    /// brought up to date when a burial is added and read at any moment - a record the view draws (nothing in the simulation reads it). The k-th
    /// burial is dug in slot k mod `Slots` of the ground, laid out at the first burial beyond the village's homes on the side away from its fields:
    /// the oldest graves' ground is dug again, as in any long-used churchyard.
    /// </summary>
    public static class Burials
    {
        /// <summary>Years in which a grave sinks from sight to a third (a mound and its marker, unkept); years a grave's earth stays bare.</summary>
        public const double MarkedYears = 30.0, FreshYears = 1.0;
        /// <summary>The ground's slots (rows of graves; the oldest dug again).</summary>
        public const int Slots = 120, Columns = 10;
        /// <summary>m between graves along a row and between rows.</summary>
        public const double Along = 2.3, Across = 1.4;
        /// <summary>Tools (null in the game): burials, deaths with no grave (lost at sea, under the wave or the lava). Never read by the simulation.</summary>
        public static double[] Tally;

        /// <summary>Whether a death leaves a body to bury (the drowned, the wave's and the lava's dead do not: `AgentSystem.Kill`'s causes 12, 6, 4).</summary>
        public static bool Leaves(int why) => why != 4 && why != 6 && why != 12;

        /// <summary>A death (AgentSystem.Kill): the dead buried at the village they died in, else at home.</summary>
        public static void Bury(World w, in Person p, int why)
        {
            if (!Leaves(why)) { if (Tally != null) Tally[1]++; return; }
            int sid = p.At != 0 && p.At != p.Home && w.Settlements.Exists(p.At) ? p.At : p.Home;
            if (!w.Settlements.Exists(sid)) return;
            ref var s = ref w.Settlements.Get(sid);
            if (s.BurialCount == 0 && s.GraveX == 0f && s.GraveY == 0f) Lay(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Bring(ref s2, w.Now);
            s2.Graves += 1.0; s2.GravesFresh += 1.0; s2.BurialCount++;
            if (Tally != null) Tally[0]++;
        }

        static void Bring(ref Settlement s, long now)
        {
            if (s.GravesAt != 0 && now > s.GravesAt)
            {
                double y = (now - s.GravesAt) / (double)SimTime.Year;
                s.Graves *= DMath.Exp(-y / MarkedYears);
                s.GravesFresh *= DMath.Exp(-y / FreshYears);
            }
            s.GravesAt = now;
        }

        /// <summary>The graves marked and the fresh ones at time t (reads only).</summary>
        public static void Read(in Settlement s, long t, out double marked, out double fresh)
        {
            marked = s.Graves; fresh = s.GravesFresh;
            if (s.GravesAt != 0 && t > s.GravesAt)
            {
                double y = (t - s.GravesAt) / (double)SimTime.Year;
                marked *= DMath.Exp(-y / MarkedYears);
                fresh *= DMath.Exp(-y / FreshYears);
            }
        }

        /// <summary>
        /// The burial ground laid out at a village's first burial (its local frame, metres): beyond its farthest home, on the side away from its fields,
        /// where the ground is free (turned a little at a time from there, further out if need be).
        /// </summary>
        static void Lay(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            double far = 20.0;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                double dx = b.X - (double)s.SquareX, dy = b.Y - (double)s.SquareY;
                double d = DMath.Sqrt(dx * dx + dy * dy);
                if (d > far) far = d;
            }
            double a0 = DMath.Atan2((double)s.SquareY - s.FieldY, (double)s.SquareX - s.FieldX);   // (away from the fields)
            double half = 0.5 * DMath.Sqrt(Columns * Along * Columns * Along + (Slots / Columns) * Across * (Slots / Columns) * Across);
            for (int ring = 0; ring < 4; ring++)
                for (int k = 0; k < 12; k++)
                {
                    double a = a0 + ((k & 1) == 0 ? 1 : -1) * ((k + 1) >> 1) * 0.45;
                    double r = far + 10.0 + half + 12.0 * ring;
                    double x = s.SquareX + r * DMath.Cos(a), y = s.SquareY + r * DMath.Sin(a);
                    if (!Footprints.DiscFree(w, sid, 0, x, y, half)) continue;
                    s.GraveX = (float)x; s.GraveY = (float)y;
                    return;
                }
            s.GraveX = (float)(s.SquareX + (far + 10.0 + half) * DMath.Cos(a0)); s.GraveY = (float)(s.SquareY + (far + 10.0 + half) * DMath.Sin(a0));
        }

        /// <summary>Slot j's place on the ground (the village's frame, metres): rows east to west, ten graves a row.</summary>
        public static void SlotAt(in Settlement s, int j, out double x, out double y)
        {
            int col = j % Columns, row = j / Columns;
            x = s.GraveX + (col - 0.5 * (Columns - 1)) * Along;
            y = s.GraveY + (row - 0.5 * (Slots / Columns - 1)) * Across;
        }

        /// <summary>
        /// Whether slot j holds a grave that still shows at time t, and how fresh: the last `marked` burials show (the k-th burial lies in slot k mod
        /// Slots); the last `fresh` of them are bare earth. `age`: 0 the latest burial, rising.
        /// </summary>
        public static bool Shows(in Settlement s, long t, int j, out bool fresh, out int age)
        {
            fresh = false; age = 0;
            if (s.BurialCount <= 0) return false;
            Read(in s, t, out double marked, out double fr);
            int last = s.BurialCount - 1;                       // the latest burial's number
            int n = last - ((last - j) % Slots + Slots) % Slots; // the latest burial dug in slot j
            if (n < 0) return false;
            age = last - n;
            if (age >= (int)DMath.RoundHalfUp(DMath.Min(Slots, marked))) return false;
            fresh = age < (int)DMath.RoundHalfUp(fr);
            return true;
        }
    }
}
