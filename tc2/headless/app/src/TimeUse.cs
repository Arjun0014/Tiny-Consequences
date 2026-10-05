using System;
using System.Collections.Generic;
using System.Globalization;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// How people spend their days in ACTIVITY time: every living person sampled every few minutes for N days, hours a day
        /// by activity for each age group (0-5, 6-11, 12-15, 16-59, 60+) - the evidence that a lived day is believable. Reads only.
        /// Usage: timeuse [--seed N] [--from-day D] [--days N] [--every S]
        /// </summary>
        public static int TimeUse(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            w.AdvanceTo(w.Now + SimTime.Days(a.Double("from-day", 1)));
            long end = w.Now + SimTime.Days(a.Double("days", 4));
            long every = a.Int("every", 300);
            string[] groups = { "0-5", "6-11", "12-15", "16-59", "60+" };
            int kinds = 32;
            var tally = new double[groups.Length, kinds];
            var samples = new double[groups.Length];
            while (w.Now < end)
            {
                w.AdvanceTo(Math.Min(end, w.Now + every));
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive) continue;
                    double age = p.AgeYears(w.Now);
                    int g = age < 6 ? 0 : age < 12 ? 1 : age < 16 ? 2 : age < 60 ? 3 : 4;
                    int k = (int)p.Act; if (k < 0 || k >= kinds) k = 0;
                    tally[g, k]++; samples[g]++;
                }
            }
            Console.WriteLine("hours a day by activity and age (" + F(a.Double("days", 4), "0") + " days from day " + F(a.Double("from-day", 1), "0") + ", seed " + w.Seed + "):");
            for (int g = 0; g < groups.Length; g++)
            {
                if (samples[g] == 0) continue;
                var row = new List<(double h, string name)>();
                for (int k = 0; k < kinds; k++) if (tally[g, k] > 0) row.Add((24.0 * tally[g, k] / samples[g], ((ActivityKind)k).ToString()));
                row.Sort((x, y) => y.h.CompareTo(x.h));
                var sb = new System.Text.StringBuilder("  " + groups[g].PadRight(6));
                foreach (var r in row) if (r.h >= 0.05) sb.Append(" " + r.name + " " + r.h.ToString("0.0", CultureInfo.InvariantCulture));
                Console.WriteLine(sb.ToString());
            }
            return 0;
        }
    }
}
