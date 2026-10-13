using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// The threshing floor (C3.7b, `26` §2i): the reaped grain comes into the village's store in the sheaf (`Settlement.Sheaves`, a part of its
    /// `Granary`) - stacked in the barns' bays and in ricks in the stackyard - and only grain threshed out of the sheaf is drawn for the larders, the
    /// hearths and a meal from the store. Its people thresh it with flails at the floor - before the barn's doors, else beside the granary - a little
    /// ahead of their need through the year: a few days' worth in the sowing and the reaping, more in the slack, and at once when nothing threshed is
    /// left (a hungry hand threshes before it forages). Everything else that counts the store counts it whole (rationing, trade's spare, relief, the
    /// store's fill); what leaves it otherwise (trade, relief, tribute, takings, migrants' food) leaves threshed first, and rot and losses take the
    /// sheaves in proportion. One law for lived days, COARSE days and DEEP steps.
    /// </summary>
    public static class Threshing
    {
        /// <summary>Tools: the reaped grain comes into the store threshed, as before (`--nothresh`: the worlds before C3.7b).</summary>
        public static bool Off;
        /// <summary>FU of grain a grown hand threshes with a flail and winnows out of the sheaf in an hour: 4-8 bushels of wheat a man-day, more of
        /// barley and oats (Britannica; the Leicestershire records) - ~25 FU an hour, 0.04 h a FU.</summary>
        public const double PerHour = 25.0;
        /// <summary>Days of their need a village's people keep threshed ahead: a few in the sowing and the reaping, more in the slack.</summary>
        public const double AheadBusy = 3.0, AheadSlack = 8.0;
        /// <summary>FU of sheaves a barn's bays hold (grain in the sheaf takes ~4 times the room of threshed grain: a quarter of what the barn keeps).</summary>
        public const double BarnSheaves = Structures.BarnHolds / 4.0;
        /// <summary>FU of grain in the sheaf one rick of the stackyard holds (a round rick ~3.6 m across: ~3 t of sheaves, ~1 t of grain).</summary>
        public const double RickFu = 1500.0;
        /// <summary>Tools (null in the game): FU threshed and its hours by the stage of the village's year - [2k] FU, [2k + 1] hours; k 0 sown,
        /// 1 growing, 2 ripe, 3 harvested. Never read by the simulation.</summary>
        public static double[] Tally;

        /// <summary>The store's grain out of the sheaf: what its larders, its hearths and a meal from the store may draw. The whole store when no
        /// sheaves are in it (as before).</summary>
        public static double Threshed(in Settlement s) => s.Sheaves > 0 ? DMath.Max(0.0, s.Granary - s.Sheaves) : s.Granary;

        /// <summary>The days of their need a village's people keep threshed ahead in this stage of its year.</summary>
        public static double AheadDays(CropStage c) => c == CropStage.Sown || c == CropStage.Ripe ? AheadBusy : AheadSlack;

        /// <summary>A village's people's need a day for the floor's reckoning: a FU a head (children eat less, guests and the grown more).</summary>
        public static double DayNeed(World w, in Settlement s) => s.Population * w.Config.FoodPerAdultDay;

        /// <summary>FU of its sheaves a village would thresh now: its threshed grain short of `days` of its need and its days ahead.</summary>
        public static double Want(World w, in Settlement s, double days)
        {
            if (Off || s.Sheaves <= 1e-9) return 0.0;
            double want = DayNeed(w, in s) * (days + AheadDays(s.Crop)) - Threshed(in s);
            return want <= 0 ? 0.0 : DMath.Min(want, s.Sheaves);
        }

        /// <summary>The grain reaped comes into the store in the sheaf (the first sheaves and the harvest's last day).</summary>
        public static void In(ref Settlement s, double fu)
        {
            if (!Off && fu > 0) s.Sheaves += fu;
        }

        /// <summary>Threshes `fu` of the village's sheaves in `hours` of work at its floor. Returns the FU threshed.</summary>
        public static double Thresh(World w, ref Settlement s, double fu, double hours)
        {
            if (fu > s.Sheaves) fu = s.Sheaves;
            if (fu <= 0) return 0.0;
            s.Sheaves -= fu;
            if (s.Sheaves < 1e-9) s.Sheaves = 0;
            w.Metrics.ThreshFu += fu; w.Metrics.ThreshHours += hours;
            if (Tally != null) { int k = s.Crop == CropStage.Sown ? 0 : s.Crop == CropStage.Growing ? 1 : s.Crop == CropStage.Ripe ? 2 : 3; Tally[2 * k] += fu; Tally[2 * k + 1] += hours; }
            return fu;
        }

        /// <summary>The village's day: what left the store since went threshed first (the sheaves never exceed it).</summary>
        public static void Day(ref Settlement s)
        {
            if (s.Sheaves > s.Granary) s.Sheaves = DMath.Max(0.0, s.Granary);
        }

        /// <summary>The village's first standing barn (0: none).</summary>
        public static int Barn(World w, int sid)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                ref var b = ref w.Buildings.Get(bl[i]);
                if (b.Kind == Structures.Barn && Structures.Working(in b, w.Now)) return bl[i];
            }
            return 0;
        }

        /// <summary>
        /// The village's threshing floor (its local frame, metres): before the first barn's doors (9 m from it toward the square: its porch faces
        /// the square), else on the far side of the first granary from the square, where the ground is free. Reads only.
        /// </summary>
        public static void Floor(World w, int sid, out double x, out double y)
        {
            ref var s = ref w.Settlements.Get(sid);
            int barn = Barn(w, sid);
            if (barn != 0)
            {
                ref var b = ref w.Buildings.Get(barn);
                double ab = DMath.Atan2(s.SquareY - (double)b.Y, s.SquareX - (double)b.X);
                x = b.X + 9.0 * DMath.Cos(ab); y = b.Y + 9.0 * DMath.Sin(ab);
                return;
            }
            Construction.GranarySpot(in s, out double gx, out double gy);
            double a0 = DMath.Atan2(gy - s.SquareY, gx - s.SquareX);
            for (int k = 0; k < 9; k++)
            {
                double a = a0 + ((k & 1) == 0 ? 1 : -1) * ((k + 1) >> 1) * 0.6;
                x = gx + 7.5 * DMath.Cos(a); y = gy + 7.5 * DMath.Sin(a);
                if (Footprints.DiscFree(w, sid, 0, x, y, 2.5)) return;
            }
            x = gx + 7.5 * DMath.Cos(a0); y = gy + 7.5 * DMath.Sin(a0);
        }

        /// <summary>Where a thresher stands on the floor: in a ring 1.6 m round its middle, at their own place in it (the person's hash, as
        /// `Presence` reads it - they face the middle).</summary>
        public static double RingAngle(int pid) => KeyedRng.Unit(KeyedRng.Mix((ulong)pid * 0x9E3779B97F4A7C15UL ^ 0x7A8E)) * DMath.TwoPi;
        public const double Ring = 1.6;
    }
}
