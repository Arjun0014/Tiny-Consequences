using System;
using System.Diagnostics;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        public static int Bench(Args a)
        {
            string name = a.Get("name", "all");
            if (name == "all" || name == "queue") BenchQueue();
            if (name == "all" || name == "dmath") BenchDMath();
            if (name == "all" || name == "poc") BenchPoc(a);
            if (name == "all" || name == "save") BenchSave(a);
            return 0;
        }

        static void BenchQueue()
        {
            foreach (int pending in new[] { 10_000, 100_000, 1_000_000 })
            {
                var q = new EventQueue();
                var rs = new RandomSequence(1);
                for (int i = 0; i < pending; i++) q.Push(rs.NextBelow(1_000_000), 30, EventKind.AgentStep, i);
                int ops = 2_000_000;
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < ops; i++)
                {
                    var e = q.Pop();
                    q.Push(e.Time + 1 + rs.NextBelow(100_000), 30, EventKind.AgentStep, e.Target);
                }
                double ns = sw.Elapsed.TotalMilliseconds * 1e6 / ops;
                Console.WriteLine("queue  pending " + pending.ToString("N0", CultureInfo.InvariantCulture) + ": " + ns.ToString("0.0", CultureInfo.InvariantCulture) + " ns per pop+push");
            }
        }

        static void BenchDMath()
        {
            int n = 5_000_000; double acc = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) acc += DMath.Sin(i * 0.001) + DMath.Exp(-i * 1e-7);
            double ns = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            Console.WriteLine("dmath  sin+exp: " + ns.ToString("0.0", CultureInfo.InvariantCulture) + " ns  (" + acc.ToString("0.0", CultureInfo.InvariantCulture) + ")");
        }

        static void BenchPoc(Args a)
        {
            foreach (int founders in new[] { a.Int("founders", 24) })
            {
                var cfg = ConfigFrom(a);
                cfg.FoundersPerSettlement = founders;
                var sw = Stopwatch.StartNew();
                var w = WorldGenerator.Create(cfg);
                double gen = sw.Elapsed.TotalMilliseconds;
                if (a.Has("coarse")) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                long span = SimTime.Years(a.Double("years", 1));
                sw.Restart();
                long ev0 = w.Metrics.Events;
                w.AdvanceTo(w.Now + span);
                double ms = sw.Elapsed.TotalMilliseconds;
                double years = span / (double)SimTime.Year;
                double personDays = w.LivingPeople() * span / (double)SimTime.Day;
                Console.WriteLine("poc    " + (a.Has("coarse") ? "COARSE " : "ACTIVITY ") + w.InhabitedSettlements() + " places, " + w.LivingPeople() + " people: gen " + gen.ToString("0", CultureInfo.InvariantCulture) + " ms; " +
                                  years.ToString("0.##", CultureInfo.InvariantCulture) + " y in " + ms.ToString("0", CultureInfo.InvariantCulture) + " ms = " +
                                  (years / (ms / 1000)).ToString("0.00", CultureInfo.InvariantCulture) + " y/s; " +
                                  ((w.Metrics.Events - ev0) / (ms / 1000)).ToString("0", CultureInfo.InvariantCulture) + " events/s; " +
                                  (ms * 1000 / Math.Max(1, personDays)).ToString("0.00", CultureInfo.InvariantCulture) + " us/person-day");
            }
        }

        static void BenchSave(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            w.AdvanceTo(w.Now + SimTime.Years(a.Double("years", 1)));
            var sw = Stopwatch.StartNew();
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            double save = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var w2 = SaveFile.Load(bytes, out var h);
            double load = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine("save   " + bytes.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes (raw " + h.UncompressedLength.ToString("N0", CultureInfo.InvariantCulture) +
                              "), save " + save.ToString("0.0", CultureInfo.InvariantCulture) + " ms, load+verify " + load.ToString("0.0", CultureInfo.InvariantCulture) + " ms, hash ok " + (w2.StateHash() == w.StateHash()));
        }
    }
}
