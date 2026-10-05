using System;
using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// Follows one person: in ACTIVITY time every change of what they do, as the inspector says it (HistoryText.Doing), at
        /// their village's local hour with their body and their household's larder; or, with --life, a whole life from the
        /// ledger after a COARSE run (someone born in the run who died old, and someone living). Reads only.
        /// Usage: follow [--seed N] [--from-day D] [--days N] [--pid P | --age A] [--every S]
        ///        follow --life --years Y [--seed N] [--lives K]
        /// </summary>
        public static int Follow(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            if (a.Has("life")) return Lives(w, a);
            w.AdvanceTo(w.Now + SimTime.Days(a.Double("from-day", 1)));
            int pid = a.Int("pid", 0);
            if (pid == 0)
            {
                double want = a.Double("age", 30), best = double.MaxValue;
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var q = ref w.People.Get(id);
                    if (!q.Alive) continue;
                    double d = Math.Abs(q.AgeYears(w.Now) - want);
                    if (d < best) { best = d; pid = id; }
                }
            }
            if (pid == 0 || !w.People.Exists(pid)) { Console.WriteLine("no such person"); return 1; }
            long start = w.Now, end = w.Now + SimTime.Days(a.Double("days", 2));
            long every = a.Int("every", 60);
            {
                ref var p = ref w.People.Get(pid);
                Console.WriteLine("following #" + pid + " " + w.History.NameOf(EntityRef.Person(pid)) + ", " + (p.SexKind == Sex.Female ? "a woman" : "a man") +
                                  " of " + F(p.AgeYears(w.Now), "0.0") + " of " + w.History.NameOf(EntityRef.Settlement(p.Home)) + " (household " + p.Household + ")" +
                                  "  skills farm " + F(p.SkFarm, "0.00") + " wood " + F(p.SkWood, "0.00") + " hunt " + F(p.SkHunt, "0.00") + " fish " + F(p.SkFish, "0.00"));
            }
            string last = null; long lastMeal = -1; int meals = 0;
            while (w.Now < end)
            {
                w.AdvanceTo(Math.Min(end, w.Now + every));
                ref var p = ref w.People.Get(pid);
                if (p.LastMeal != lastMeal) { if (lastMeal >= 0) meals++; lastMeal = p.LastMeal; }
                string doing = HistoryText.Doing(w, in p);
                string key = doing + "|" + p.Step;
                if (key == last && p.Alive) continue;
                last = key;
                int hid = p.Household;
                double larder = hid != 0 && w.Households.Exists(hid) ? w.Households.Get(hid).Larder : 0;
                Console.WriteLine("  " + Local(w, p.Home, w.Now) + "  " + doing + "  [" + p.Act + "/" + p.Step + "]" +
                                  "  reserve " + F(p.Reserve, "0.0") + " d  health " + F(p.Health, "0.00") + "  larder " + F(larder, "0.0") + "  meals " + meals);
                if (!p.Alive) break;
            }
            Console.WriteLine("events of #" + pid + " in the span:");
            PrintEvents(w, pid, start);
            return 0;
        }

        /// <summary>The village's local time of day (and the day number) at t: "d12 07:40".</summary>
        static string Local(World w, int sid, long t)
        {
            if (sid == 0 || !w.Settlements.Exists(sid)) return SimTime.FormatShort(t);
            long off = PlanetState.LocalOffset(w.Settlements.Get(sid).Anchor);
            long lt = t + off;
            long sod = SimTime.FloorMod(lt, SimTime.Day);
            return "d" + (lt / SimTime.Day) + " " + (sod / 3600).ToString("00") + ":" + (sod % 3600 / 60).ToString("00");
        }

        static void PrintEvents(World w, int pid, long from)
        {
            var list = w.History.EventsOf(EntityRef.Person(pid));
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (w.History.TryGet(list[i], out var e) && e.Time >= from) Console.WriteLine("    " + HistoryText.Line(w, e));
        }

        /// <summary>Whole lives from the ledger after a COARSE run: the dead born in the run who lived longest, and some living.</summary>
        static int Lives(World w, Args a)
        {
            long t0 = w.Now;
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SpanFrom(a, 48 * 50));
            int k = a.Int("lives", 3);
            var dead = new List<(double age, int id)>();
            var living = new List<(double age, int id)>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (p.Born <= t0) continue;
                if (p.Alive) living.Add((p.AgeYears(w.Now), id));
                else dead.Add(((p.Died - p.Born) / (double)SimTime.Year, id));
            }
            dead.Sort((x, y) => x.age != y.age ? y.age.CompareTo(x.age) : x.id.CompareTo(y.id));
            living.Sort((x, y) => x.age != y.age ? y.age.CompareTo(x.age) : x.id.CompareTo(y.id));
            Console.WriteLine("at " + SimTime.Format(w.Now) + ": " + w.LivingPeople() + " living; born in the run and dead (kept in the table) " + dead.Count);
            for (int i = 0; i < Math.Min(k, dead.Count); i++) Life(w, dead[i].id);
            // a life cut short: the dead of 20-45, the median of them
            var mid = dead.FindAll(d => d.age >= 20 && d.age < 45);
            if (mid.Count > 0) Life(w, mid[mid.Count / 2].id);
            for (int i = 0; i < Math.Min(k, living.Count); i++) Life(w, living[i].id);
            return 0;
        }

        static void Life(World w, int pid)
        {
            ref var p = ref w.People.Get(pid);
            Console.WriteLine();
            Console.WriteLine("#" + pid + " " + w.History.NameOf(EntityRef.Person(pid)) + ", " + (p.SexKind == Sex.Female ? "a woman" : "a man") + ", born " + SimTime.FormatShort(p.Born) +
                              (p.Alive ? ", living at " + F(p.AgeYears(w.Now), "0") : ", died at " + F((p.Died - p.Born) / (double)SimTime.Year, "0")) +
                              "  home " + w.History.NameOf(EntityRef.Settlement(p.Home)) + "  skills farm " + F(p.SkFarm, "0.00") + " wood " + F(p.SkWood, "0.00") +
                              " hunt " + F(p.SkHunt, "0.00") + " fish " + F(p.SkFish, "0.00"));
            PrintEvents(w, pid, 0);
        }
    }
}
