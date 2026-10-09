using System;
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
        /// <summary>(C3.3 design) household-years, one row at each of its village's harvests: [0 year, 1 sid, 2 hid, 3 harvest share FU, 4 wild and herd
        /// brought FU, 5 eaten from its larder's draws and its own catch FU, 6 members, 7 workers 16-59, 8 children under 12, 9 the old 60+, 10 workshop
        /// hours, 11.. kin households in the village (its members' parents', children's and siblings' households)]. Null unless asked for.</summary>
        public static List<double[]> Years;
        /// <summary>hid -> what it brought, ate and worked at workshops since its village's last harvest (for `Years`).</summary>
        static readonly Dictionary<int, double[]> YearAcc = new Dictionary<int, double[]>();
        static double[] Acc(int hid) { if (!YearAcc.TryGetValue(hid, out var a)) { a = new double[3]; YearAcc[hid] = a; } return a; }

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
            if (Years != null && w.Now >= From) YearRows(w, sid, hhs, total, yield);
            for (int i = 0; i < hhs.Count; i++)
            {
                if (!Season.TryGetValue(hhs[i], out double v)) continue;
                if (w.Now >= From && total > 0) Row(hhs[i], sid)[0] += yield * v / total;
                Season.Remove(hhs[i]);
            }
        }

        static readonly Dictionary<int, List<int>> _kin = new Dictionary<int, List<int>>();
        static readonly Dictionary<int, List<int>> _sibs = new Dictionary<int, List<int>>();

        static void Link(int a, int b)
        {
            if (a == 0 || b == 0 || a == b) return;
            if (!_kin.TryGetValue(a, out var la)) { la = new List<int>(); _kin[a] = la; }
            if (!la.Contains(b)) la.Add(b);
            if (!_kin.TryGetValue(b, out var lb)) { lb = new List<int>(); _kin[b] = lb; }
            if (!lb.Contains(a)) lb.Add(a);
        }

        /// <summary>One row a household of the village at its harvest (the year that ends with it), with its kin households in the village.</summary>
        static void YearRows(World w, int sid, IntList hhs, double total, double yield)
        {
            _kin.Clear(); _sibs.Clear();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive || p.Household == 0) continue;
                for (int k = 0; k < 2; k++)
                {
                    int par = k == 0 ? p.Mother : p.Father;
                    if (par == 0 || !w.People.Exists(par)) continue;
                    ref var q = ref w.People.Get(par);
                    if (q.Alive && q.Home == sid) Link(p.Household, q.Household);
                    if (!_sibs.TryGetValue(par, out var l)) { l = new List<int>(); _sibs[par] = l; }
                    if (!l.Contains(p.Household)) l.Add(p.Household);
                }
            }
            foreach (var kv in _sibs) for (int a = 0; a < kv.Value.Count; a++) for (int b = a + 1; b < kv.Value.Count; b++) Link(kv.Value[a], kv.Value[b]);
            double year = w.Now / (double)SimTime.Year;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                Season.TryGetValue(hid, out double v);
                var acc = Acc(hid);
                var mem = w.Members(hid);
                int n = 0, workers = 0, kids = 0, old = 0;
                for (int k = 0; k < mem.Count; k++)
                {
                    ref var p = ref w.People.Get(mem[k]);
                    if (!p.Alive) continue;
                    n++;
                    double a = p.AgeYears(w.Now);
                    if (a >= 16 && a < 60) workers++; else if (a < 12) kids++; else if (a >= 60) old++;
                }
                _kin.TryGetValue(hid, out var kin);
                int nk = kin == null ? 0 : Math.Min(kin.Count, 8);
                var row = new double[11 + nk];
                row[0] = year; row[1] = sid; row[2] = hid; row[3] = total > 0 ? yield * v / total : 0; row[4] = acc[0]; row[5] = acc[1];
                row[6] = n; row[7] = workers; row[8] = kids; row[9] = old; row[10] = acc[2];
                for (int k = 0; k < nk; k++) row[11 + k] = kin[k];
                Years.Add(row);
                acc[0] = 0; acc[1] = 0; acc[2] = 0;
            }
        }

        /// <summary>Wild food or herd produce a household brought home: kept in its larder, and the rest put into the village's store.</summary>
        public static void Bring(World w, int hid, int sid, double kept, double toStore)
        {
            if (By == null || hid == 0 || w.Now < From) return;
            var r = Row(hid, sid); r[1] += toStore; r[2] += kept;
            if (Years != null) { var a = Acc(hid); a[0] += toStore + kept; a[1] += kept; }
        }

        /// <summary>What a household's larder drew from the village's store.</summary>
        public static void Draw(World w, int hid, int sid, double fu)
        {
            if (By == null || hid == 0 || w.Now < From || fu <= 0) return;
            Row(hid, sid)[3] += fu;
            if (Years != null) Acc(hid)[1] += fu;
        }

        /// <summary>Hours a household's member worked at one of the village's workshops (shed, kiln, smithy).</summary>
        public static void Craft(World w, int hid, double hours)
        {
            if (By == null || hid == 0 || w.Now < From || hours <= 0) return;
            if (w.Households.Exists(hid)) Row(hid, w.Households.Get(hid).Settlement)[4] += hours;
            if (Years != null) Acc(hid)[2] += hours;
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
