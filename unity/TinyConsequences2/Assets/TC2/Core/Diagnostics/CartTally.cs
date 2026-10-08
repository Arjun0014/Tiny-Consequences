using TC2.Core.Agents;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// Tools (C4.2 closing): why carts begun are given up - at each beginning and each giving up, the village's rations (short and sent to the
    /// wild), the hours already in the cart, whether the household still has an able wright, and its able grown members. Null in the game; never
    /// read by the simulation (a run with it is the same run).
    /// </summary>
    public static class CartTally
    {
        /// <summary>[0 begun, 1 begun while short, 2 given up, 3 given up with no hours in, 4 given up under half made, 5 given up short now,
        /// 6 given up with no able wright, 7 given up with one able member, 8 able members at giving up (sum), 9 given up in a village under 60,
        /// 10 begun in a village under 60, 11 hours in at giving up (sum)].</summary>
        public static double[] T;

        static int Able(World w, int hid)
        {
            int n = 0;
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count; i++)
            {
                ref var q = ref w.People.Get(mem[i]);
                double age = q.AgeYears(w.Now);
                if (q.Alive && age >= 16 && age < 60 && q.Health > 0.6) n++;
            }
            return n;
        }

        public static void Begun(World w, int sid, int hid)
        {
            if (T == null) return;
            ref var s = ref w.Settlements.Get(sid);
            T[0]++;
            if (AgentSystem.WildNeed(in s, w.Now) > 0.42) T[1]++;
            if (s.Population < 60) T[10]++;
        }

        public static void GivenUp(World w, int sid, int hid)
        {
            if (T == null) return;
            ref var s = ref w.Settlements.Get(sid);
            ref var hh = ref w.Households.Get(hid);
            T[2]++;
            if (hh.CartHours <= 0f) T[3]++;
            else if (hh.CartHours < 0.5 * Carts.HoursOf(hh.CartKind)) T[4]++;
            if (AgentSystem.WildNeed(in s, w.Now) > 0.42) T[5]++;
            if (!Carts.HasWright(w, hid)) T[6]++;
            int a = Able(w, hid);
            if (a <= 1) T[7]++;
            T[8] += a;
            if (s.Population < 60) T[9]++;
            T[11] += hh.CartHours;
        }
    }
}
