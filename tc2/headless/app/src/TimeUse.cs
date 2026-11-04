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
        /// Usage: timeuse [--seed N | --load SAVE] [--from-day D] [--days N] [--every S] [--daylight] [--stages]
        /// `--daylight`: only the samples in their village's daylight (7-19 h local) - the hours the view shows, as the stillness audit counts them
        /// (27 §1); hours then read as hours of the 12 daylight hours. Grown people (16-59) are told apart by sex (16-59F, 16-59M).
        /// `--stages`: the grown people's hours again by their home village's crop stage (sowing, growing, ripe and the harvest, after it) -
        /// which seasons leave them idle.
        /// </summary>
        public static int TimeUse(Args a)
        {
            World w;
            if (a.Has("load"))
            {
                // (a saved world - a mature one, C3.4 survey - lived from its next midnight, as the game loads it)
                w = TC2.Core.Persistence.SaveFile.Load(System.IO.File.ReadAllBytes(a.Get("load")), out _);
                long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
                if (w.StepDays > 1) w.RequestStepDays(1, next0);
                if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
                w.AdvanceTo(next0 + SimTime.Hour);
            }
            else w = WorldGenerator.Create(ConfigFrom(a));
            {
                // (session 32 QA) `--skip`: reach the first day as the player's Skip does (SimHost.BeginSkip: COARSE from the next midnight, lived again
                // for the last two days - OfflineProgress.PlanCatchUp, Living) - to see what people do just after a skip
                long target = w.Now + SimTime.Days(a.Double("from-day", 1));
                if (a.Has("skip")) Console.WriteLine("skip planned COARSE: " + TC2.Core.Persistence.OfflineProgress.PlanCatchUp(w, target, TC2.Core.Persistence.OfflineMode.Living));
                w.AdvanceTo(target);
            }
            long end = w.Now + SimTime.Days(a.Double("days", 4));
            long every = a.Int("every", 300);
            bool daylight = a.Has("daylight"), stages = a.Has("stages");
            string[] groups = daylight ? new[] { "0-5", "6-11", "12-15", "16-59F", "16-59M", "60+" } : new[] { "0-5", "6-11", "12-15", "16-59", "60+" };
            string[] stageNames = { "fallow", "sowing", "growing", "ripe", "after" };
            int kinds = 40;   // (every ActivityKind: Pot is 36)
            var tally = new double[groups.Length, kinds];
            var samples = new double[groups.Length];
            var stTally = new double[stageNames.Length, kinds];
            var stSamples = new double[stageNames.Length];
            while (w.Now < end)
            {
                w.AdvanceTo(Math.Min(end, w.Now + every));
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive) continue;
                    if (daylight)
                    {
                        int at = p.At != 0 ? p.At : p.Home;
                        if (at == 0 || !w.Settlements.Exists(at)) continue;
                        long lsec = SimTime.FloorMod(w.Now + w.LocalOffsetOf(at), SimTime.Day);
                        if (lsec < 7 * SimTime.Hour || lsec >= 19 * SimTime.Hour) continue;
                    }
                    double age = p.AgeYears(w.Now);
                    int g = age < 6 ? 0 : age < 12 ? 1 : age < 16 ? 2 : age < 60 ? 3 : 4;
                    if (daylight && g >= 3) g = g == 4 ? 5 : p.IsFemale ? 3 : 4;
                    int k = (int)p.Act; if (k < 0 || k >= kinds) k = 0;
                    tally[g, k]++; samples[g]++;
                    if (stages && age >= 16 && age < 60 && p.Home != 0 && w.Settlements.Exists(p.Home))
                    {
                        int st = (int)w.Settlements.Get(p.Home).Crop; if (st < 0 || st >= stageNames.Length) st = 0;
                        stTally[st, k]++; stSamples[st]++;
                    }
                }
            }
            double hours = daylight ? 12.0 : 24.0;
            Console.WriteLine("hours " + (daylight ? "of the 12 daylight hours" : "a day") + " by activity and age (" + F(a.Double("days", 4), "0") + " days from day " + F(a.Double("from-day", 1), "0") + ", seed " + w.Seed + "):");
            for (int g = 0; g < groups.Length; g++)
            {
                if (samples[g] == 0) continue;
                var row = new List<(double h, string name)>();
                for (int k = 0; k < kinds; k++) if (tally[g, k] > 0) row.Add((hours * tally[g, k] / samples[g], ((ActivityKind)k).ToString()));
                row.Sort((x, y) => y.h.CompareTo(x.h));
                var sb = new System.Text.StringBuilder("  " + groups[g].PadRight(7));
                foreach (var r in row) if (r.h >= 0.05) sb.Append(" " + r.name + " " + r.h.ToString("0.0", CultureInfo.InvariantCulture));
                Console.WriteLine(sb.ToString());
            }
            if (stages)
            {
                Console.WriteLine("grown people (16-59), hours " + (daylight ? "of the 12 daylight hours" : "a day") + " by their village's crop stage (share of samples):");
                double all = 0; for (int st = 0; st < stageNames.Length; st++) all += stSamples[st];
                for (int st = 0; st < stageNames.Length; st++)
                {
                    if (stSamples[st] == 0) continue;
                    var row = new List<(double h, string name)>();
                    for (int k = 0; k < kinds; k++) if (stTally[st, k] > 0) row.Add((hours * stTally[st, k] / stSamples[st], ((ActivityKind)k).ToString()));
                    row.Sort((x, y) => y.h.CompareTo(x.h));
                    var sb = new System.Text.StringBuilder("  " + stageNames[st].PadRight(8) + (100.0 * stSamples[st] / Math.Max(1.0, all)).ToString("0", CultureInfo.InvariantCulture).PadLeft(3) + "%");
                    foreach (var r in row) if (r.h >= 0.05) sb.Append(" " + r.name + " " + r.h.ToString("0.0", CultureInfo.InvariantCulture));
                    Console.WriteLine(sb.ToString());
                }
            }
            return 0;
        }
    }
}
