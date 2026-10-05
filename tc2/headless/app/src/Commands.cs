using System.Collections.Generic;
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

        static string PastureLine(int sid)
        {
            var d = TC2.Core.Places.Livestock.Diag;
            if (d == null || sid * 4 + 3 >= d.Length) return "";
            return "  pasture " + F(d[sid * 4], "0") + "/" + F(d[sid * 4 + 1], "0") + " kg mast " + F(d[sid * 4 + 2], "0") + "/" + F(d[sid * 4 + 3], "0");
        }

        // fires that ended, by century: how many, the cells they burnt, the largest, their hours (tool only; read from FireOut)
        static readonly SortedDictionary<int, double[]> _fires = new SortedDictionary<int, double[]>();

        static void TallyFire(in HistoryEvent e)
        {
            int century = (int)(e.Time / (100 * SimTime.Year));
            if (!_fires.TryGetValue(century, out var t)) _fires[century] = t = new double[6];
            double burnt = e.Magnitude, hours = e.ArgA;
            t[0]++; t[1] += burnt; t[2] = Math.Max(t[2], burnt); t[3] += hours; t[4] = Math.Max(t[4], hours);
            if (burnt >= 100) t[5]++;
        }

        static void PrintFires()
        {
            if (_fires.Count == 0) return;
            Console.WriteLine("fires ended by century (count, cells burnt, mean, largest, of 100+ cells, mean and longest hours):");
            foreach (var kv in _fires)
            {
                var t = kv.Value;
                Console.WriteLine("  " + (kv.Key * 100).ToString().PadLeft(5) + "  " + t[0].ToString("0").PadLeft(5) + "  burnt " + t[1].ToString("0").PadLeft(7) +
                                  "  mean " + (t[1] / t[0]).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6) + "  largest " + t[2].ToString("0").PadLeft(5) +
                                  "  100+ " + t[5].ToString("0").PadLeft(3) + "  hours " + (t[3] / t[0]).ToString("0", CultureInfo.InvariantCulture).PadLeft(4) + " / " + t[4].ToString("0"));
            }
        }

        public static int Run(Args a)
        {
            var sw = Stopwatch.StartNew();
            if (a.Has("places")) { TC2.Core.Places.Livestock.Diag = new double[4 * 4096]; TC2.Core.Places.Livestock.DiagEnds = new long[4 * TC2.Core.Places.Livestock.Causes]; }
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
                                          "  fields " + F(st.FieldHa, "0.0") + "/" + F(st.ArableHa, "0") + " ha (" + w.FieldCellsOf(sid).Count + " cells)  granary " + F(st.Granary, "0") + "  ration " + F(st.Ration, "0.00") +
                                          "  last harvest " + F(st.LastHarvest, "0") + "  T " + F(w.Planet.TMean[st.Cell], "0.0") +
                                          "  mem " + F(st.Memory, "0.00") + "  wild FU/h hunt " + F(st.Hunt.Expect, "0.00") + " (" + F(st.Hunt.Va, "0") + " kg) fish " + F(st.Fish.Expect, "0.00") +
                                          " (" + F(st.Fish.Va, "0") + ") gather " + F(st.Gath.Expect, "0.00") + " (" + F(st.Gath.Va, "0") + ")" +
                                          "  knows " + ((st.TechKnown & 1) != 0 ? "N" : "-") + ((st.TechKnown & 2) != 0 ? "L" : "-") + ((st.TechKnown & 4) != 0 ? "Q" : "-") +
                                          ((st.TechKnown & 8) != 0 ? "P" : "-") + ((st.TechKnown & 16) != 0 ? "A" : "-") +
                                          " (" + F(st.BestNets, "0.00") + "/" + F(st.BestLoom, "0.00") + "/" + F(st.BestQuern, "0.00") + "/" + F(st.BestPottery, "0.00") + "/" + F(st.BestArd, "0.00") + ")" +
                                          "  fibre " + F(st.Fibre, "0") + " hides " + F(st.Hides, "0") + (w.Learning.Clay(st.Cell) ? " clay" : "") + (w.Learning.Rock(st.Cell) ? " rock" : "") +
                                          "  stock cattle " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Cattle) + " goats " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Goat) + " pigs " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Pig) + PastureLine(sid) + HerdLine(w, sid));
                    }
                }
                int from = w.History.FirstIndexAfter(histCursor);
                for (int i = from; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind == HistoryKind.FireOut) TallyFire(e);
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
            Console.WriteLine("household work: making food ready " + F(m.PrepHours, "0") + " h for " + F(m.PrepFu, "0") + " FU, water " + m.WaterTrips + " trips (" + F(m.WaterHours, "0") +
                              " h)  making and mending " + F(m.CraftHours, "0") + " h for " + F(m.GearMade, "0") + " goods  cold: spells " + m.ColdSpells + ", deaths " + m.ColdDeaths);
            {
                int lived = 0, empty = 0, fallen = 0;
                for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id))
                {
                    var bu = w.Buildings.Get(id);
                    if (TC2.Core.Places.Homes.Fallen(in bu, w.Now)) fallen++; else if (bu.Household != 0) lived++; else empty++;
                }
                Console.WriteLine("techniques: found " + m.TechniquesFound + ", lost by villages " + m.TechniquesLost);
                Console.WriteLine("homes: lived in " + lived + ", standing empty " + empty + ", fallen " + fallen + "  taken empty " + m.HomesTaken + "  households died out " + m.DiedOut);
            }
            var live = new int[3];
            for (int b = w.Beasts.First(); b != 0; b = w.Beasts.Next(b)) { ref var be = ref w.Beasts.Get(b); if (be.Alive) live[be.Kind]++; }
            Console.WriteLine("stock: cattle " + live[0] + " goats " + live[1] + " pigs " + live[2] + "  born " + m.BeastBirths + "  died " + m.BeastDeaths + "  taken by wolves " + m.BeastsTaken +
                              "  slaughtered " + m.BeastsEaten + " (" + F(m.BeastFu, "0") + " FU)  gone wild " + m.BeastsFeral +
                              "  parties " + m.Parties + "  foundings " + m.Foundings + "  scouts " + m.Scouts);
            var ends = TC2.Core.Places.Livestock.DiagEnds;
            if (ends != null)
                for (int k = 0; k < TC2.Core.Places.Livestock.Kinds; k++)
                {
                    var sb = new System.Text.StringBuilder("  " + TC2.Core.Places.Livestock.Laws[k].Name + ": born " + ends[TC2.Core.Places.Livestock.Kinds * TC2.Core.Places.Livestock.Causes + k] + ", ended");
                    for (int c = 0; c < TC2.Core.Places.Livestock.Causes; c++) sb.Append(" " + TC2.Core.Places.Livestock.CauseNames[c] + " " + ends[k * TC2.Core.Places.Livestock.Causes + c] + ",");
                    Console.WriteLine(sb.ToString());
                }
            {
                // the ledger by kind and tier (what history keeps after compaction)
                var byKind = new Dictionary<string, int>();
                for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); string k = e.Kind + "/" + e.Tier; byKind[k] = byKind.TryGetValue(k, out int c) ? c + 1 : 1; }
                var top = new List<KeyValuePair<string, int>>(byKind); top.Sort((x, y) => y.Value.CompareTo(x.Value));
                var sbh = new System.Text.StringBuilder("history " + w.History.Count + " kept, " + w.History.Dropped + " dropped:");
                for (int i = 0; i < top.Count && i < 14; i++) sbh.Append(" " + top[i].Key + " " + top[i].Value + ",");
                Console.WriteLine(sbh.ToString());
                // why deaths stay in the ledger (tools: compaction's reasons)
                int imp = 0, young = 0, recent = 0, gone = 0, other = 0, tiers = 0;
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != TC2.Core.History.HistoryKind.PersonDied) continue;
                    if (e.Tier != TC2.Core.History.Tier.Personal) { tiers++; continue; }
                    if (e.Importance >= 5f) { imp++; continue; }
                    if (w.Now - e.Time < 2 * SimTime.Year) { young++; continue; }
                    if (!w.People.Exists(e.Subject.Id)) { gone++; continue; }
                    var dp = w.People.Get(e.Subject.Id);
                    if (dp.Alive || w.Now - dp.Died < 30 * SimTime.Year) recent++; else other++;
                }
                Console.WriteLine("deaths kept: important " + imp + ", under two years " + young + ", subject recent " + recent + ", subject pruned " + gone + ", other " + other + ", other tiers " + tiers);
            }
            Console.WriteLine("state hash " + w.StateHash().ToString("x16"));
            Console.WriteLine("process ids used " + w.Ids.Peek(EntityKind.Process) + "  (herds now " + w.Herds.Count + ", fires' highest id " + w.Fire.Procs.MaxId +
                              ", fire pages held " + w.Fire.Procs.PagesHeld + ")  managed heap " + F(GC.GetTotalMemory(true) / 1048576.0, "0") + " MB");
            PrintFires();
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
