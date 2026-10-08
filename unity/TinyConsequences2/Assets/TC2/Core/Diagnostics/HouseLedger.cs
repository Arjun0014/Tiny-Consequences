using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// Tools (C3.3 survey): what each household brings into its village and takes out of it - its share of each harvest by its field work that season,
    /// the wild food and herd produce it keeps in its larder and what it puts beyond that into the village's store, what it draws from the store,
    /// its hours at the village's workshops, and its person-days and grown workers' days - from `From` on. Null in the game; never read by the
    /// simulation (a run with it is the same run).
    /// </summary>
    public static class HouseLedger
    {
        /// <summary>hid -> [0 harvest share FU, 1 wild and herd to the store FU, 2 wild and herd kept in its larder FU, 3 drawn from the store FU,
        /// 4 workshop hours, 5 person-days, 6 workers' days (16-59), 7 days counted, 8 village id].</summary>
        public static Dictionary<int, double[]> By;
        /// <summary>hid -> crop work since its village's last harvest.</summary>
        static readonly Dictionary<int, double> Season = new Dictionary<int, double>();
        /// <summary>The moment the tally begins.</summary>
        public static long From;

        static double[] Row(int hid, int sid)
        {
            if (!By.TryGetValue(hid, out var r)) { r = new double[9]; By[hid] = r; }
            r[8] = sid;
            return r;
        }

        /// <summary>Crop work a household's member did (lived or COARSE).</summary>
        public static void Work(int hid, double units)
        {
            if (By == null || hid == 0 || units <= 0) return;
            Season.TryGetValue(hid, out double v);
            Season[hid] = v + units;
        }

        /// <summary>A village's harvest, shared out (for the tally) by its households' crop work this season.</summary>
        public static void Harvest(World w, int sid, double yield)
        {
            if (By == null) return;
            var hhs = w.HouseholdsOf(sid);
            double total = 0;
            for (int i = 0; i < hhs.Count; i++) if (Season.TryGetValue(hhs[i], out double v)) total += v;
            for (int i = 0; i < hhs.Count; i++)
            {
                if (!Season.TryGetValue(hhs[i], out double v)) continue;
                if (w.Now >= From && total > 0) Row(hhs[i], sid)[0] += yield * v / total;
                Season.Remove(hhs[i]);
            }
        }

        /// <summary>Wild food or herd produce a household brought home: kept in its larder, and the rest put into the village's store.</summary>
        public static void Bring(World w, int hid, int sid, double kept, double toStore)
        {
            if (By == null || hid == 0 || w.Now < From) return;
            var r = Row(hid, sid); r[1] += toStore; r[2] += kept;
        }

        /// <summary>What a household's larder drew from the village's store.</summary>
        public static void Draw(World w, int hid, int sid, double fu)
        {
            if (By == null || hid == 0 || w.Now < From || fu <= 0) return;
            Row(hid, sid)[3] += fu;
        }

        /// <summary>Hours a household's member worked at one of the village's workshops (shed, kiln, smithy).</summary>
        public static void Craft(World w, int hid, double hours)
        {
            if (By == null || hid == 0 || w.Now < From || hours <= 0) return;
            if (w.Households.Exists(hid)) Row(hid, w.Households.Get(hid).Settlement)[4] += hours;
        }

        /// <summary>A household's person-days and workers' days over a step.</summary>
        public static void Days(World w, int hid, int sid, int days)
        {
            if (By == null || w.Now < From) return;
            var mem = w.Members(hid);
            int n = 0, workers = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var p = ref w.People.Get(mem[i]);
                if (!p.Alive) continue;
                n++;
                double a = p.AgeYears(w.Now);
                if (a >= 16 && a < 60) workers++;
            }
            var r = Row(hid, sid); r[5] += n * days; r[6] += workers * days; r[7] += days;
        }
    }
}
