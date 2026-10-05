using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        static string F(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);

        public static WorldConfig ConfigFrom(Args a)
        {
            var c = new WorldConfig();
            c.Seed = (ulong)a.Long("seed", 42);
            c.Settlements = a.Int("settlements", c.Settlements);
            c.FoundersPerSettlement = a.Int("founders", c.FoundersPerSettlement);
            c.SurfaceN = a.Int("n", c.SurfaceN);
            if (a.Has("lightning")) c.LightningPerCellDay = a.Double("lightning", c.LightningPerCellDay);
            if (a.Has("trips")) c.TripChancePerDecision = a.Double("trips", c.TripChancePerDecision);
            return c;
        }

        static long SpanFrom(Args a, long defDays)
        {
            if (a.Has("years")) return SimTime.Years(a.Double("years", 1));
            if (a.Has("hours")) return SimTime.Hours(a.Double("hours", 1));
            return SimTime.Days(a.Double("days", defDays));
        }

        /// <summary>A village's herds: "herds deer 8, boar 5+4, wolves 3".</summary>
        static string HerdLine(World w, int sid)
        {
            var sb = new StringBuilder();
            for (int h = w.Herds.First(); h != 0; h = w.Herds.Next(h))
            {
                var hd = w.Herds.Get(h);
                if (hd.Village != sid) continue;
                sb.Append(sb.Length == 0 ? "  herds " : ", ").Append(TC2.Core.Planet.Ecology.KindName(hd.Kind)).Append(' ').Append(hd.Count.ToString("0"));
            }
            return sb.ToString();
        }

        public static int Run(Args a)
        {
            var sw = Stopwatch.StartNew();
            World w;
            if (a.Has("load"))
            {
                w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out var hdr);
                Console.WriteLine("loaded " + a.Get("load") + " at " + SimTime.Format(w.Now) + " (saved " + hdr.SavedRealUtcMs + ")");
            }
            else w = WorldGenerator.Create(ConfigFrom(a));
            long genMs = sw.ElapsedMilliseconds;
            KindProfiler prof = a.Has("profile") ? new KindProfiler() : null;
            w.Profiler = prof;
            Console.WriteLine("world seed " + w.Seed + "  cells " + w.Planet.Cells + "  settlements " + w.InhabitedSettlements() + "  people " + w.LivingPeople() + "  (generated in " + genMs + " ms)");
            long span = SpanFrom(a, 2);
            long end = w.Now + span;
            if (a.Has("coarse")) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            long report = a.Has("report-days") ? SimTime.Days(a.Double("report-days", 1)) : (span > 3 * SimTime.Year ? SimTime.Year : (span > 20 * SimTime.Day ? 4 * SimTime.Day : SimTime.Day));
            long histCursor = 0;
            bool verbose = a.Has("v");
            int minTier = a.Int("tier", 3);
            sw.Restart();
            long lastEvents = w.Metrics.Events;
            var lastSw = Stopwatch.StartNew();
            while (w.Now < end)
            {
                long next = Math.Min(end, w.Now + report);
                w.AdvanceTo(next);
                long ev = w.Metrics.Events - lastEvents; lastEvents = w.Metrics.Events;
                double ms = lastSw.Elapsed.TotalMilliseconds; lastSw.Restart();
                Console.WriteLine(SimTime.FormatShort(w.Now) + "  pop " + w.LivingPeople() + "  places " + w.InhabitedSettlements() + "  granary " + F(TotalGranary(w), "0") +
                                  "  fires " + w.Fire.Active.Count + "  events " + ev + "  hist " + w.History.Count + "  " + F(ms, "0.0") + " ms");
                if (a.Has("places"))
                {
                    // each village: people, fields (ha of arable), granary (FU) and its ration, last harvest
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        ref var st = ref w.Settlements.Get(sid);
                        if (!st.Inhabited) continue;
                        Console.WriteLine("      " + w.History.NameOf(new EntityRef(EntityKind.Settlement, sid)).PadRight(14) + " pop " + st.Population.ToString().PadLeft(4) +
                                          "  fields " + F(st.FieldHa, "0.0") + "/" + F(st.ArableHa, "0") + " ha  granary " + F(st.Granary, "0") + "  ration " + F(st.Ration, "0.00") +
                                          "  last harvest " + F(st.LastHarvest, "0") + "  T " + F(w.Planet.TMean[st.Cell], "0.0") +
                                          "  mem " + F(st.Memory, "0.00") + "  wild FU/h hunt " + F(st.Hunt.Expect, "0.00") + " (" + F(st.Hunt.Va, "0") + " kg) fish " + F(st.Fish.Expect, "0.00") +
                                          " (" + F(st.Fish.Va, "0") + ") gather " + F(st.Gath.Expect, "0.00") + " (" + F(st.Gath.Va, "0") + ")" + HerdLine(w, sid));
                    }
                }
                int from = w.History.FirstIndexAfter(histCursor);
                for (int i = from; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if ((int)e.Tier >= minTier || verbose || e.Kind == HistoryKind.JourneyInterrupted || e.Kind == HistoryKind.FieldsBurned)
                        Console.WriteLine("    " + HistoryText.Line(w, e));
                }
                histCursor = w.History.LastId;
            }
            double total = sw.Elapsed.TotalMilliseconds;
            double years = span / (double)SimTime.Year;
            Console.WriteLine();
            Console.WriteLine("simulated " + SimTime.FormatDuration(span) + " in " + F(total, "0") + " ms  = " + F(years / (total / 1000.0), "0.000") + " world years / s");
            var m = w.Metrics;
            Console.WriteLine("events " + m.Events + "  decisions " + m.Decisions + "  steps " + m.Steps + "  stale " + m.Stale + "  journeys " + m.Journeys +
                              "  interrupted " + m.JourneysInterrupted + "  interrupts " + m.Interrupts + "/" + m.InterruptsHandled + "  meals " + m.Meals +
                              "  births " + m.Births + "  deaths " + m.Deaths + "  marriages " + m.Marriages + "  lightning " + m.Lightning + "  fire steps " + m.FireSteps);
            Console.WriteLine("wild: trips hunt " + m.HuntTrips + " fish " + m.FishTrips + " gather " + m.GatherTrips + "  hours " + F(m.WildHours, "0") + "  kg game " + F(m.GameKg, "0") +
                              " fish " + F(m.FishKg, "0") + " mast " + F(m.GatherKg, "0") + "  FU " + F(m.WildFu, "0") + " (" + F(m.WildFu / Math.Max(1, m.WildHours), "0.00") + "/h)" +
                              "  hunger deaths " + m.HungerDeaths + "  famines " + m.Famines);
            Console.WriteLine("state hash " + w.StateHash().ToString("x16"));
            prof?.Print();
            string outDir = a.Get("out");
            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                var sb = new StringBuilder();
                for (int i = 0; i < w.History.Count; i++) sb.Append(HistoryText.Line(w, w.History.At(i))).Append('\n');
                File.WriteAllText(Path.Combine(outDir, "history.txt"), sb.ToString());
                var save = SaveFile.Save(w, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), OfflineMode.Living);
                File.WriteAllBytes(Path.Combine(outDir, "world.tc2"), save);
                Console.WriteLine("wrote " + outDir + "/history.txt (" + w.History.Count + " events) and world.tc2 (" + save.Length + " bytes)");
            }
            if (a.Has("why"))
            {
                // explain every interrupted journey and failed harvest
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind == HistoryKind.JourneyInterrupted || e.Kind == HistoryKind.HarvestFailed || e.Kind == HistoryKind.FieldsBurned)
                    {
                        Console.WriteLine("WHY: " + HistoryText.Describe(w, e));
                        Console.Write(HistoryText.Why(w, e.Id));
                    }
                }
            }
            return 0;
        }

        static double TotalGranary(World w)
        {
            double t = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) t += w.Settlements.Get(s).Granary;
            return t;
        }

        public static int Hash(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            Console.WriteLine("gen    " + w.StateHash().ToString("x16"));
            long span = SpanFrom(a, 2);
            int checkpoints = a.Int("checkpoints", 4);
            for (int k = 1; k <= checkpoints; k++)
            {
                w.AdvanceTo(span * k / checkpoints);
                Console.WriteLine(SimTime.FormatShort(w.Now) + " " + w.StateHash().ToString("x16"));
            }
            return 0;
        }

        public static int Inspect(Args a)
        {
            var data = File.ReadAllBytes(a.Get("save"));
            var h = SaveFile.PeekHeader(data);
            Console.WriteLine("save format " + h.FormatVersion + " sim " + h.SimVersion + " seed " + h.Seed + " time " + SimTime.Format(h.WorldTime) +
                              " pop " + h.Population + " places " + h.Settlements + " bytes " + data.Length + " raw " + h.UncompressedLength);
            var w = SaveFile.Load(data, out _);
            Console.WriteLine("loaded and verified: state hash " + w.StateHash().ToString("x16"));
            return 0;
        }
    }
}
