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
            c.Radius = a.Double("radius", c.Radius);
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
            if (a.Has("places")) { TC2.Core.Places.Livestock.Diag = new double[4 * 4096]; TC2.Core.Places.Livestock.DiagEnds = new long[4 * TC2.Core.Places.Livestock.Causes]; TC2.Core.Places.SettlementSystem.Unrests = new long[7]; TC2.Core.Agents.AgentSystem.Hours = new double[44]; TC2.Core.Places.SettlementSystem.Harvests = new double[6]; TC2.Core.Places.Migration.Weighs = new double[8]; TC2.Core.Places.SettlementSystem.Demo = new double[10]; }
            if (a.Has("nosea")) TC2.Core.Places.Trade.NoSea = true;
            if (a.Has("seashort")) TC2.Core.Places.Trade.SeaShort = new double[3];   // (C9.4 survey: relief by sea bound by the boats' room)
            if (a.Has("noweave")) TC2.Core.Places.Weaving.Off = true;
            if (a.Has("waresflow")) TC2.Core.Places.Trade.WaresFor = new double[6];
            if (a.Has("rot")) TC2.Core.Places.Structures.RotTally = new double[3];   // (C3.2 survey: grain rotted against harvested)   // (C3.1: what wares were traded for)   // (C3.1 against C13.3 in one binary: no weaver's sheds)
            if (a.Has("nostreets")) TC2.Core.Places.Streets.None = true;   // (C13.1 against D2-114 in one binary: no streets laid)   // (C9.3 against C9.2b in one binary: no voyages, no relief by sea)
            string vtrace = a.Get("vtrace");   // (each village each report: people, store, famine, hunger deaths, relief in and out by land and sea, who left)
            System.IO.StreamWriter vt = null;
            if (vtrace != null)
            {
                TC2.Core.Places.Trade.Diag = new double[4 * 4096]; TC2.Core.Places.SettlementSystem.HungerBy = new double[4096]; TC2.Core.Places.Migration.LeftBy = new double[4096];
                vt = new System.IO.StreamWriter(vtrace);
                vt.WriteLine("year,sid,name,pop,granary,days,ration,memory,pressure,arable_ha,famine,hunger,land_out,sea_out,land_in,sea_in,left,planks,lord");
            }
            var voySeen = new HashSet<long>();
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
            bool scale = a.Has("scale");   // (D2-085: the planet's size seen from the inside)
            if (scale) ScaleReport.Begin(w);
            if (a.Has("lived"))
            {
                // (a world saved by a COARSE or DEEP run back to lived days at its next midnight - as the game's LoadWorld does)
                long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
                if (w.StepDays > 1) w.RequestStepDays(1, next0);
                if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
            }
            if (a.Has("coarse") || a.Has("deep")) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            if (a.Has("deep")) w.RequestStepDays(a.Int("deep", 12), SimTime.StartOfDay(w.Now) + SimTime.Day);   // DEEP (F8): calm villages live K days at a step
            long report = a.Has("report-days") ? SimTime.Days(a.Double("report-days", 1)) : (span > 3 * SimTime.Year ? SimTime.Year : (span > 20 * SimTime.Day ? 4 * SimTime.Day : SimTime.Day));
            long histCursor = 0;
            bool verbose = a.Has("v");
            int minTier = a.Int("tier", 3);
            sw.Restart();
            long lastEvents = w.Metrics.Events;
            var lastSw = Stopwatch.StartNew();
            // (--snap Y1,Y2,..: the world saved Y years from its start as DIR/world_yY.tc2 - DIR is --snapdir, default tc2/out/ages/sSEED;
            // look-dev and acceptance shots of one world at several ages load them: unity.mjs shots --load F)
            var snapAt = new List<long>(); var snapName = new List<string>();
            long start0 = w.Now;
            if (a.Has("snap"))
                foreach (var ys in a.Get("snap").Split(','))
                {
                    double y = double.Parse(ys, CultureInfo.InvariantCulture);
                    snapAt.Add(start0 + SimTime.Years(y)); snapName.Add(ys.Trim());
                }
            string snapDir = a.Get("snapdir", Path.Combine("tc2", "out", "ages", "s" + w.Seed));
            // (--nudge Y: a placebo - one FU more in the first village's store Y years in, so the world's chaos alone can be measured)
            long nudgeAt = a.Has("nudge") ? start0 + SimTime.Years(a.Double("nudge", 150)) : 0;
            while (w.Now < end || (snapAt.Count > 0 && snapAt[0] <= w.Now))
            {
                long next = Math.Min(end, w.Now + report);
                bool snapNow = snapAt.Count > 0 && snapAt[0] <= next;
                if (snapNow) next = snapAt[0];
                w.AdvanceTo(next);
                if (nudgeAt > 0 && w.Now >= nudgeAt)
                {
                    for (int ns = w.Settlements.First(); ns != 0; ns = w.Settlements.Next(ns)) if (w.Settlements.Get(ns).Inhabited) { w.Settlements.Get(ns).Granary += 1.0; break; }
                    nudgeAt = 0;
                }
                if (snapNow)
                {
                    Directory.CreateDirectory(snapDir);
                    string sf = Path.Combine(snapDir, "world_y" + snapName[0] + ".tc2");
                    File.WriteAllBytes(sf, SaveFile.Save(w, 0, OfflineMode.Living));
                    Console.WriteLine("saved " + sf + " at " + SimTime.Format(w.Now) + "  pop " + w.LivingPeople() + "  places " + w.InhabitedSettlements());
                    snapAt.RemoveAt(0); snapName.RemoveAt(0);
                }
                long ev = w.Metrics.Events - lastEvents; lastEvents = w.Metrics.Events;
                double ms = lastSw.Elapsed.TotalMilliseconds; lastSw.Restart();
                Console.WriteLine(SimTime.FormatShort(w.Now) + "  pop " + w.LivingPeople() + "  places " + w.InhabitedSettlements() + "  granary " + F(TotalGranary(w), "0") +
                                  "  fires " + w.Fire.Active.Count + "  events " + ev + "  hist " + w.History.Count + "  " + F(ms, "0.0") + " ms");
                if (scale) Console.WriteLine(ScaleReport.Line(w));
                if (vt != null) VTrace(w, vt);
                if (a.Has("voyagers")) Voyagers(w, voySeen);   // (each boat seen on a voyage, once a leg: C9.3)
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
                                          ((st.TechKnown & 8) != 0 ? "P" : "-") + ((st.TechKnown & 16) != 0 ? "A" : "-") + ((st.TechKnown & 32) != 0 ? "M" : "-") +
                                          " (" + F(st.BestNets, "0.00") + "/" + F(st.BestLoom, "0.00") + "/" + F(st.BestQuern, "0.00") + "/" + F(st.BestPottery, "0.00") + "/" + F(st.BestArd, "0.00") + ")" +
                                          "  fibre " + F(st.Fibre, "0") + " hides " + F(st.Hides, "0") + " metal " + F(st.Metal, "0.0") + (w.Learning.Clay(st.Cell) ? " clay" : "") + (w.Learning.Rock(st.Cell) ? " rock" : "") + (st.OreCell != 0 ? " copper " + F(st.OreDist, "0") + " m" : "") +
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
            vt?.Close();
            double total = sw.Elapsed.TotalMilliseconds;
            double years = span / (double)SimTime.Year;
            Console.WriteLine();
            Console.WriteLine("simulated " + SimTime.FormatDuration(span) + " in " + F(total, "0") + " ms  = " + F(years / (total / 1000.0), "0.000") + " world years / s");
            if (scale) ScaleReport.Final(w, total, years);
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
                var works = new int[32]; var raising = new int[32]; var worksFallen = new int[32];
                for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id))
                {
                    var bu = w.Buildings.Get(id);
                    if (!TC2.Core.Places.Structures.IsHome(bu.Kind))
                    {
                        // the village's works (C1.1): standing, being raised, fallen
                        if (bu.Done == 0) raising[bu.Kind]++; else if (TC2.Core.Places.Structures.Working(in bu, w.Now)) works[bu.Kind]++; else worksFallen[bu.Kind]++;
                        continue;
                    }
                    if (TC2.Core.Places.Homes.Fallen(in bu, w.Now)) fallen++; else if (bu.Household != 0) lived++; else empty++;
                }
                {
                    var sb = new System.Text.StringBuilder("works:");
                    for (int k = 0; k < works.Length; k++)
                        if (works[k] + raising[k] + worksFallen[k] > 0)
                            sb.Append(' ').Append(TC2.Core.Places.Structures.Name((byte)k)).Append(' ').Append(works[k]).Append(" standing, ").Append(raising[k]).Append(" being raised, ").Append(worksFallen[k]).Append(" fallen;");
                    int most = 0, mostSid = 0; double over = 0; int villages = 0;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        var st = w.Settlements.Get(sid);
                        if (!st.Inhabited) continue;
                        villages++;
                        if (st.Granaries > most) { most = st.Granaries; mostSid = sid; }
                        double cap = TC2.Core.Places.Structures.StoreCapacity(in st);
                        if (st.Granary > cap) over += st.Granary - cap;
                    }
                    // timber from the woods (C1.2): what was felled, how far the woodlots are now, the fields and the arable land
                    {
                        double fieldHa = 0, arableHa = 0, dsum = 0, dmax = 0; int lots = 0, none = 0;
                        for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                        {
                            var st2 = w.Settlements.Get(sid2);
                            if (!st2.Inhabited) continue;
                            fieldHa += st2.FieldHa; arableHa += st2.ArableHa;
                            if (st2.WoodLot > 0) { lots++; dsum += st2.WoodDist; if (st2.WoodDist > dmax) dmax = st2.WoodDist; } else none++;
                        }
                        Console.WriteLine("woods: felled " + F(w.Metrics.WoodFelled, "0") + " loads (" + F(w.Metrics.WoodFelled * 25.0 / 1000.0, "0") + " t), receded past a mark " + w.Metrics.WoodsReceded +
                                          " times; woodlots " + F(lots > 0 ? dsum / lots : 0, "0") + " m from the square on average, the farthest " + F(dmax, "0") + " m, " + none + " villages with none in reach;" +
                                          " fields " + F(fieldHa, "0") + " of " + F(arableHa, "0") + " arable ha");
                    }
                    // stone (C1.3): who knows rock, quarries, what was cut, houses on footings
                    {
                        int know = 0, quarries = 0, villages2 = 0, footed = 0, houses = 0; double dq = 0;
                        for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                        {
                            var st2 = w.Settlements.Get(sid2);
                            if (!st2.Inhabited) continue;
                            villages2++;
                            if (st2.StoneCell > 0) know++;
                            if (st2.Quarry != 0) { quarries++; dq += st2.StoneDist; }
                        }
                        for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id))
                        {
                            var bu = w.Buildings.Get(id);
                            if (bu.Kind != TC2.Core.Places.Structures.House || TC2.Core.Places.Homes.Fallen(in bu, w.Now)) continue;
                            houses++; if (bu.Mat >= 1) footed++;
                        }
                        Console.WriteLine("stone: " + know + " of " + villages2 + " villages know rock, " + quarries + " quarries (" + F(quarries > 0 ? dq / quarries : 0, "0") + " m out on average); cut " + F(w.Metrics.StoneCut, "0") +
                                          " loads in " + F(w.Metrics.QuarryHours, "0") + " h; houses on stone footings " + footed + " of " + houses + " standing (" + w.Metrics.Footings + " raised so)");
                    }
                    // copper ore and the mines (C5.1)
                    {
                        int know = 0, mines = 0, villages3 = 0; double dm = 0, left = 0, taken = 0; int bodies = w.Deposits.Count, spent = 0;
                        for (int sid3 = w.Settlements.First(); sid3 != 0; sid3 = w.Settlements.Next(sid3))
                        {
                            var st3 = w.Settlements.Get(sid3);
                            if (!st3.Inhabited) continue;
                            villages3++;
                            if (st3.OreCell > 0) know++;
                            if (TC2.Core.Places.Mining.Mine(w, sid3) != 0) { mines++; dm += st3.OreDist; }
                        }
                        int cuBodies = 0;
                        for (int i = 0; i < bodies; i++) { var d = w.Deposits.Get(i); if (d.Kind != TC2.Core.Places.Mining.Copper) continue; cuBodies++; left += d.Ore; taken += d.Taken; if (d.GaveOut != 0) spent++; }
                        bodies = cuBodies;
                        Console.WriteLine("ores: " + know + " of " + villages3 + " villages know copper, " + mines + " mines (" + F(mines > 0 ? dm / mines : 0, "0") + " m out on average); " + bodies + " bodies seen, " + spent +
                                          " dug out; copper ore dug " + F((w.Metrics.OreDug - w.Metrics.BogOreDug) / 1000.0, "0.0") + " t (all mines and pits " + F(w.Metrics.MineHours, "0") + " h; left in the bodies seen " + F(left / 1000.0, "0") + " t); copper smelted " + F(w.Metrics.MetalKg, "0") + " kg");
                    }
                    // iron (C5.2b)
                    {
                        int knowBog = 0, pits = 0, smelt = 0, villages5 = 0, topSeller = 0; double topSold = 0; double iron = 0, ironArms = 0, arms5 = 0, share = 0, beds = 0, bedsSpent = 0;
                        for (int sid5 = w.Settlements.First(); sid5 != 0; sid5 = w.Settlements.Next(sid5))
                        {
                            var st5 = w.Settlements.Get(sid5);
                            if (!st5.Inhabited) continue;
                            villages5++;
                            if (st5.FeCell > 0) knowBog++;
                            if (TC2.Core.Places.Mining.Mine(w, sid5, TC2.Core.Places.Mining.Iron) != 0) pits++;
                            if (st5.BestIron >= TC2.Core.Places.Mining.Smelter) smelt++;
                            iron += st5.Iron; ironArms += st5.IronArms; arms5 += st5.Arms; share += TC2.Core.Knowledge.Tech.IronShare(in st5) * st5.Population;
                            if (st5.IronSold > topSold) { topSold = st5.IronSold; topSeller = sid5; }
                        }
                        for (int i = 0; i < w.Deposits.Count; i++) { var d = w.Deposits.Get(i); if (d.Kind != TC2.Core.Places.Mining.Iron) continue; beds++; if (d.GaveOut != 0) bedsSpent++; }
                        int pop5 = w.LivingPeople();
                        Console.WriteLine("iron: " + knowBog + " of " + villages5 + " villages know bog ore (" + beds + " beds seen, " + bedsSpent + " dug out), " + pits + " pits; " + smelt + " villages smelt iron; bog ore dug " +
                                          F(w.Metrics.BogOreDug / 1000.0, "0.0") + " t, iron smelted " + F(w.Metrics.IronKg, "0") + " kg, traded " + F(w.Metrics.IronTraded, "0") + " kg; iron tools for " + F(pop5 > 0 ? 100.0 * share / pop5 : 0, "0") + " % of the people (" + F(iron, "0") + " kg); iron arms " + F(ironArms, "0") + " of " + F(arms5, "0") +
                                          (topSeller != 0 ? "; most sold lately: " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(topSeller)) + " #" + topSeller + " (" + F(topSold, "0") + " kg a year, " + w.Settlements.Get(topSeller).Population + " people)" : ""));
                        Console.WriteLine("claims: ore dug at bodies other villages work " + F(w.Metrics.OreClaimedKg / 1000.0, "0.0") + " t in " + w.Metrics.OreClaims + " takings");
                        // boats (C9.1): the coast's villages, the boats at their landings, what the sea gave
                        {
                            int coast = 0, withBoats = 0, sound = 0, making = 0, topBoats = 0, topBoatSid = 0, wet = 0, inland = 0;
                            var P6 = w.Planet; var badAt = new System.Text.StringBuilder();
                            int landings = 0, keptN = 0, planks = 0, sheds = 0, knowPlanks = 0; double landMin = double.MaxValue, landMax = 0, landSum = 0, keptUp = 0;
                            for (int sid6 = w.Settlements.First(); sid6 != 0; sid6 = w.Settlements.Next(sid6))
                            {
                                ref var st6 = ref w.Settlements.Get(sid6);
                                if (!st6.Inhabited || st6.Fish.A < 0) continue;
                                coast++;
                                int here = 0; var bl6 = w.BuildingsOf(sid6);
                                for (int i = 0; i < bl6.Count; i++)
                                {
                                    ref var b6 = ref w.Buildings.Get(bl6[i]);
                                    if (!TC2.Core.Places.Boats.IsBoat(b6.Kind)) continue;
                                    if (TC2.Core.Places.Boats.Sound(in b6, w.Now)) { here++; if (TC2.Core.Places.Boats.IsPlank(b6.Kind)) planks++; } else if (b6.Done == 0 && b6.Left == 0) making++;
                                    if (b6.Left != 0) continue;
                                    // (where it lies: never in the water, never far from it - sampled 40 m round)
                                    keptN++; keptUp += TC2.Core.Planet.Shore.At(P6, st6.LocalToSphere(b6.X, b6.Y, P6.Radius), out _, out _) - P6.SeaLevel;
                                    if (TC2.Core.Places.Boats.Afloat(P6, st6.LocalToSphere(b6.X, b6.Y, P6.Radius))) { wet++; badAt.Append(" #").Append(sid6).Append(" wet"); continue; }
                                    if (!TC2.Core.Places.Boats.AtWater(w, in st6, b6.X, b6.Y)) { inland++; badAt.Append(" #").Append(sid6).Append(" dry"); }
                                }
                                sound += here; if (here > 0) withBoats++;
                                if (st6.Shed != 0) sheds++;
                                if ((st6.TechKnown & TC2.Core.Places.Boats.Bit) != 0) knowPlanks++;
                                if (st6.LandAt != 0) { double ldx = (double)st6.LandX - st6.SquareX, ldy = (double)st6.LandY - st6.SquareY, ld = System.Math.Sqrt(ldx * ldx + ldy * ldy); landings++; landMin = System.Math.Min(landMin, ld); landMax = System.Math.Max(landMax, ld); landSum += ld; }
                                if (here > topBoats) { topBoats = here; topBoatSid = sid6; }
                            }
                            int drownedSquares = 0; var dsq = new System.Text.StringBuilder();
                            for (int sid7 = w.Settlements.First(); sid7 != 0; sid7 = w.Settlements.Next(sid7))
                            {
                                ref var st7 = ref w.Settlements.Get(sid7);
                                if (!st7.Inhabited) continue;
                                if (TC2.Core.Places.Boats.Afloat(P6, st7.LocalToSphere(st7.SquareX, st7.SquareY, P6.Radius))) { drownedSquares++; dsq.Append(" #").Append(sid7); }
                            }
                            Console.WriteLine("squares under the sea: " + drownedSquares + (dsq.Length > 0 ? " (" + dsq.ToString().Trim() + ")" : ""));
                            Console.WriteLine("boats: " + withBoats + " of " + coast + " fishing villages have boats (" + (sound - planks) + " dugouts, " + planks + " plank boats, " + making + " being made); " + knowPlanks + " build boats of planks, " + sheds + " boat sheds, plank boats made " + w.Metrics.PlanksBuilt + ", fish from them " + F(w.Metrics.PlankFishKg / 1000.0, "0.0") + " t; begun " + w.Metrics.BoatsBegun + ", made " + w.Metrics.BoatsBuilt + ", given up half-made " + w.Metrics.BoatsGivenUp +
                                              ", worn out " + w.Metrics.BoatsWorn + ", lost at sea " + w.Metrics.BoatsLost + " (" + w.Metrics.Drowned + " drowned); trips " + w.Metrics.BoatTrips + " of " + w.Metrics.FishTrips + " fishing; fish from boats " +
                                              F(w.Metrics.BoatFishKg / 1000.0, "0.0") + " t of " + F(w.Metrics.FishKg / 1000.0, "0.0") + " t; lying in the water " + wet + ", away from it " + inland + (keptN > 0 ? ", " + F(keptUp / keptN, "0.0") + " m above the sea on average" : "") + (badAt.Length > 0 ? " (at" + badAt + ")" : "") +
                                              (landings > 0 ? "; " + landings + " landings " + F(landMin, "0") + "-" + F(landMax, "0") + " m from their squares (mean " + F(landSum / landings, "0") + ")" : "") +
                                              "; outings " + w.Metrics.BoatOutings + ", crew seats " + w.Metrics.CrewSeats + (topBoatSid != 0 ? "; most at " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(topBoatSid)) + " #" + topBoatSid + " (" + topBoats + ")" : ""));
                        }
                        Console.WriteLine("ore sites: land told as by the ore wanted " + w.Metrics.ScoutedForOre + " times; villages founded knowing ore round their site " + w.Metrics.FoundedByOre + " of " + w.Metrics.Foundings);
                    }
                    // charcoal (C5.2a)
                    {
                        int hearths = 0, smouldering = 0; double held = 0;
                        for (int sid4 = w.Settlements.First(); sid4 != 0; sid4 = w.Settlements.Next(sid4))
                        {
                            var st4 = w.Settlements.Get(sid4);
                            if (!st4.Inhabited) continue;
                            if (TC2.Core.Places.Charcoal.Site(w, sid4) != 0) hearths++;
                            if (st4.ClampUntil > w.Now) smouldering++;
                            held += st4.Charcoal;
                        }
                        Console.WriteLine("charcoal: " + hearths + " hearths at the woods (" + smouldering + " clamps smouldering); burnt " + F(w.Metrics.CharcoalKg / 1000.0, "0.0") + " t from " +
                                          F(w.Metrics.CharcoalKg / TC2.Core.Places.Charcoal.Yield / 1000.0, "0") + " t of wood felled for it, in " + F(w.Metrics.BurnHours, "0") + " h; in store " + F(held, "0") + " kg");
                    }
                    // the smithy and arms (C1.4)
                    {
                        int smithies = 0, armed = 0, fearing = 0; double arms = 0;
                        for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                        {
                            var st2 = w.Settlements.Get(sid2);
                            if (!st2.Inhabited) continue;
                            if (TC2.Core.Places.Forge.Smithy(w, sid2) != 0) smithies++;
                            if (st2.Arms >= 1) { armed++; arms += st2.Arms; }
                            if (TC2.Core.Places.Forge.Threatened(in st2)) fearing++;
                        }
                        int grounds = 0, trainedPeople = 0, veterans = 0; double best = 0;
                        for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2)) { var st2 = w.Settlements.Get(sid2); if (st2.Inhabited && TC2.Core.Places.Training.Ground(w, sid2) != 0) grounds++; }
                        for (int pid = w.People.First(); pid != 0; pid = w.People.Next(pid)) { var q = w.People.Get(pid); if (!q.Alive) continue; if (q.SkFight >= 0.1f) trainedPeople++; if (q.Fights > 0) veterans++; if (q.SkFight > best) best = q.SkFight; }
                        {
                            int markets = 0; double busiest = 0; int bsid = 0;
                            for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                            {
                                var st2 = w.Settlements.Get(sid2);
                                if (!st2.Inhabited) continue;
                                if (st2.Market != 0) markets++;
                                if (st2.TradeIn > busiest) { busiest = st2.TradeIn; bsid = sid2; }
                            }
                            Console.WriteLine("markets: " + markets + "; the busiest " + (bsid != 0 ? w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(bsid)) + " (" + F(busiest, "0") + " traders a year, " + w.Settlements.Get(bsid).Population + " people" + (w.Settlements.Get(bsid).Market != 0 ? ", a market" : "") + ")" : "none"));
                        }
                        {
                            var ways = new List<TC2.Core.Agents.Way>();
                            w.Ways.Collect(w.Now, ways);
                            int tracks = 0, paths = 0, roads = 0;
                            foreach (var wy in ways) { byte st = TC2.Core.Agents.Ways.StateOf(wy.Use); if (st >= 3) roads++; else if (st == 2) paths++; else tracks++; }
                            Console.WriteLine("ways: " + w.Ways.Count + " walked; " + tracks + " tracks, " + paths + " paths, " + roads + " roads");
                        }
                        Console.WriteLine("training: " + grounds + " grounds, " + F(w.Metrics.TrainHours, "0") + " h of practice; " + trainedPeople + " living people with some skill, " + veterans + " who have fought, the best " + F(best, "0.00"));
                        {
                            int walled = 0, inner = 0, rising = 0;
                            for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2)) { var st2 = w.Settlements.Get(sid2); if (!st2.Inhabited) continue; if (st2.Wall == 2) walled++; if (st2.Wall == 1) rising++; if (st2.OldR > 0f) inner++; }
                            Console.WriteLine("walls: " + walled + " walled, " + rising + " rising; larger rings begun " + w.Metrics.RingsGrown + ", old rings standing inside " + inner + "; palisades begun " + w.Metrics.WallsPlanned);
                        }
                        Console.WriteLine("force: " + smithies + " smithies; " + fearing + " villages fear their neighbours, " + armed + " hold arms (" + F(arms, "0") + "); arms forged " + F(w.Metrics.ArmsMade, "0") +
                                          ", hours at the smithies " + F(w.Metrics.SmithyHours, "0") + ", metal smelted " + F(w.Metrics.MetalKg, "0") + " kg");
                        {
                            // cloth and goods (C3.1): the weaver's sheds, their makers, wares made, taken, traded; who wears them
                            int sheds = 0, makers = 0, skilled = 0, wearing = 0, bought = 0, places = 0, buildings = 0, more = 0; int smallShed = int.MaxValue, bigNone = 0; double store = 0, held = 0, people = 0, fibre = 0;
                            for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                            {
                                var st2 = w.Settlements.Get(sid2);
                                if (!st2.Inhabited) continue;
                                places++; store += st2.Wares; held += st2.WaresHeld; people += st2.Population; fibre += st2.Fibre;
                                bool shed = TC2.Core.Places.Weaving.Shed(w, sid2) != 0;
                                if (shed) { sheds++; buildings += st2.Sheds; if (st2.Sheds > 1) more++; if (st2.Population < smallShed) smallShed = st2.Population; }
                                else if (st2.Population > bigNone) bigNone = st2.Population;
                                if (st2.WaresHeld > 0.1f * st2.Population) { wearing++; if (!shed) bought++; }
                                if (!shed) continue;
                                var res2 = w.Residents(sid2);
                                for (int i = 0; i < res2.Count; i++) { var q = w.People.Get(res2[i]); if (TC2.Core.Places.Weaving.Maker(in st2, in q, w.Now)) { makers++; if (q.SkCraft >= 0.3f) skilled++; } }
                            }
                            Console.WriteLine("wares: " + sheds + " weaver's sheds of " + places + " villages (" + buildings + " sheds, " + more + " villages with more than one; smallest with one " + (sheds > 0 ? smallShed.ToString() : "-") + " people, largest without " + bigNone + "); makers " + makers + " (" + skilled + " practised); made " + F(w.Metrics.WaresMade, "0") +
                                              " in " + F(w.Metrics.ShedHours, "0") + " h, taken by households " + F(w.Metrics.WaresDrawn, "0") + ", traded " + F(w.Metrics.WaresTraded, "0") + "; in stores " + F(store, "0") + ", worn " + F(held / Math.Max(1.0, people * TC2.Core.Places.Goods.Target) * 100, "0") +
                                              " % of the people; villages wearing them " + wearing + " (" + bought + " without a shed); fibre in store " + F(fibre / Math.Max(1.0, people), "0.00") + " kg a head; home making " + F(w.Metrics.CraftHours, "0") + " h");
                            var rt = TC2.Core.Places.Structures.RotTally;
                            if (rt != null) Console.WriteLine("grain rotted: " + F(rt[0], "0") + " FU in the granaries, " + F(rt[1], "0") + " FU in heaps beyond them, against " + F(rt[2], "0") + " FU harvested (" + F(100 * (rt[0] + rt[1]) / Math.Max(1.0, rt[2]), "0.0") + " %)");
                            var wf = TC2.Core.Places.Trade.WaresFor;
                            if (wf != null) Console.WriteLine("wares traded for: " + F(wf[0], "0") + " wares given (" + F(wf[5], "0") + " from shed villages to villages without) for grain " + F(wf[1], "0") + " FU, fibre " + F(wf[2], "0") + " kg, hides " + F(wf[3], "0") + ", metal/iron " + F(wf[4], "0") + " kg");
                            Console.WriteLine("water caches: days routed " + w.Planet.WaterDay + ", drainage rebuilt " + w.Planet.BasinVersion + "; drawn shore rebuilt " + TC2.Core.Planet.Shore.Refreshes + ", river lines rebuilt " + TC2.Core.Planet.RiverLines.Version + " (" + TC2.Core.Planet.RiverLines.All.Count + " pieces)");
                        }
                    }
                    sb.Append("  most granaries ").Append(most).Append(mostSid != 0 ? " (" + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(mostSid)) + ", " + w.Settlements.Get(mostSid).Population + " people)" : "").Append("; grain beyond the granaries now ").Append(F(over, "0")).Append(" FU");
                    Console.WriteLine(sb.ToString());
                }
                Console.WriteLine("sickness: infections " + m.Infections + ", deaths " + m.DiseaseDeaths + ", epidemics " + m.Epidemics + ", new from animals " + m.PathogensBorn + ", mutations " + m.Mutations);
                {
                    // the sicknesses that killed most: their traits, when they came, what they did
                    var killers = new List<int>();
                    for (int id = w.Pathogens.First(); id != 0; id = w.Pathogens.Next(id)) killers.Add(id);
                    killers.Sort((x, y) => { int c = w.Pathogens.Get(y).Deaths.CompareTo(w.Pathogens.Get(x).Deaths); return c != 0 ? c : x.CompareTo(y); });
                    for (int i = 0; i < killers.Count && i < 8; i++)
                    {
                        var g = w.Pathogens.Get(killers[i]);
                        Console.WriteLine("  " + w.History.NameOf(new EntityRef(EntityKind.Pathogen, g.Id)) + " (year " + F((double)g.Born / SimTime.Year, "0") + (g.Parent != 0 ? ", a strain" : "") + "): cases " + g.Cases + ", dead " + g.Deaths +
                                          "; beta " + F(g.Beta, "0.00") + ", latent " + F(g.Latent, "0") + " d, infectious " + F(g.Infectious, "0") + " d, virulence " + F(g.Virulence, "0.000") + ", immunity " + (g.Immunity >= 99f ? "for life" : F(g.Immunity, "0") + " y"));
                    }
                }
                Console.WriteLine("taking: loads taken " + m.Takings + " (" + F(m.TakenFu, "0") + " FU), takers seen " + m.TakingsSeen + "; deeds kept " + w.Deeds.Count);
                Console.WriteLine("blows: " + m.Blows + " struck, " + m.WoundDeaths + " dead of their wounds; watch kept " + m.WatchNights + " village-nights");
                { int walled = 0, building = 0, wallsDown = 0; for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { var st = w.Settlements.Get(sid); if (st.Wall == 2) walled++; else if (st.Wall == 1) building++; else if (st.Wall == 3) wallsDown++; }
                  Console.WriteLine("walls: palisades begun " + m.WallsPlanned + "; standing " + walled + ", being built " + building + ", fallen " + wallsDown);
                  // why the first wall was raised and why the first fell (the history explains them: F6's done-when)
                  long firstBuilt = 0, firstFallen = 0;
                  for (int i = 0; i < w.History.Count && (firstBuilt == 0 || firstFallen == 0); i++)
                  {
                      var e = w.History.At(i);
                      if (e.Kind == HistoryKind.WallBuilt && firstBuilt == 0) firstBuilt = e.Id;
                      if (e.Kind == HistoryKind.WallFallen && firstFallen == 0) firstFallen = e.Id;
                  }
                  if (firstBuilt != 0) Console.WriteLine("  the first wall raised:"); Console.Write(HistoryText.Why(w, firstBuilt));
                  if (firstFallen != 0) Console.WriteLine("  the first wall fallen:"); Console.Write(HistoryText.Why(w, firstFallen)); }
                {
                    // lordships: the villages each lord has paying it, directly or through their own lords (D2-075)
                    int lords = 0, biggest = 0, biggestRoot = 0, deepest = 0;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        if (!w.Settlements.Get(sid).Inhabited || w.Conflict.LordOf(sid) != 0) continue;
                        int n = w.Conflict.PolitySize(sid);
                        if (n == 0) continue;
                        lords++;
                        if (n > biggest) { biggest = n; biggestRoot = sid; }
                    }
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { int d = 0, r = sid; while (w.Conflict.LordOf(r) != 0 && d < 16) { r = w.Conflict.LordOf(r); d++; } if (d > deepest) deepest = d; }
                    Console.WriteLine("lordships: tribute agreed " + m.Tributes + ", ended " + m.TributesEnded + ", paid " + F(m.TributeFu, "0") + " FU; lords now " + lords +
                                      (biggest > 0 ? ", the greatest " + w.History.NameOf(EntityRef.Settlement(biggestRoot)) + " with " + biggest + " paying, depth " + deepest : ""));
                    long firstLord = 0, firstEnd = 0;
                    for (int i = 0; i < w.History.Count && (firstLord == 0 || firstEnd == 0); i++)
                    {
                        var e = w.History.At(i);
                        if (e.Kind == HistoryKind.TributeAgreed && firstLord == 0) firstLord = e.Id;
                        if (e.Kind == HistoryKind.TributeEnded && firstEnd == 0) firstEnd = e.Id;
                    }
                    if (firstLord != 0) { Console.WriteLine("  the first tribute:"); Console.Write(HistoryText.Why(w, firstLord)); }
                    if (firstEnd != 0) { Console.WriteLine("  the first tribute ended:"); Console.Write(HistoryText.Why(w, firstEnd)); }
                }
                Console.WriteLine("raids: bands raised " + m.Bands + ", raids " + m.Raids + " (driven off " + m.RaidsBeaten + "), loot " + F(m.RaidLoot, "0") + " FU; bands joined by tributaries " + m.CalledBands + " (" + m.CalledMen + " men answered the call), bands lords raised over their tributaries' wrongs " + m.LordsAnswered);
                Console.WriteLine("answers: amends asked " + m.Claims + ", made " + m.Amends + ", refused " + m.Refusals + "; loads taken back " + m.TakenBack);
                Console.WriteLine("trade: trades " + m.TradeTrips + ", gain " + F(m.TradeValue, "0") + ", metal moved " + F(m.MetalTraded, "0") + " kg; relief " + F(m.ReliefFu, "0") + " FU, given back " + F(m.ReliefReturnedFu, "0") + " FU");
                {
                    // by sea (C9.3): voyages, cargo, and the villages traders come to most (a port's custom)
                    int topIn = 0; float most = 0f; int markets = 0;
                    for (int sid8 = w.Settlements.First(); sid8 != 0; sid8 = w.Settlements.Next(sid8)) { var st8 = w.Settlements.Get(sid8); if (!st8.Inhabited) continue; if (st8.Market != 0) markets++; if (st8.LandAt != 0 && st8.TradeIn > most) { most = st8.TradeIn; topIn = sid8; } }
                    Console.WriteLine("sea trade: voyages " + m.Voyages + " (" + m.VoyageSeats + " seats, " + m.VoyagesLapsed + " booked and not sailed), cargo " + F(m.SeaCargoKg / 1000.0, "0.0") + " t; relief by sea " + F(m.ReliefBySeaFu, "0") + " FU; markets " + markets +
                                      (topIn != 0 ? "; the landing traders come to most: " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(topIn)) + " #" + topIn + " (" + F(most, "0") + " a year)" : ""));
                }
                Console.WriteLine("techniques: found " + m.TechniquesFound + ", lost by villages " + m.TechniquesLost + "  metal smelted " + F(m.MetalKg, "0") + " kg");
                Console.WriteLine("homes: lived in " + lived + ", standing empty " + empty + ", fallen " + fallen + "  taken empty " + m.HomesTaken + "  households died out " + m.DiedOut);
            }
            var live = new int[3];
            for (int b = w.Beasts.First(); b != 0; b = w.Beasts.Next(b)) { ref var be = ref w.Beasts.Get(b); if (be.Alive) live[be.Kind]++; }
            {
                var un = TC2.Core.Places.SettlementSystem.Unrests;
                var hv = TC2.Core.Places.SettlementSystem.Harvests;
                if (hv != null && hv[2] > 0)
                    Console.WriteLine("harvests " + F(hv[2], "0") + ": " + F(hv[0] / Math.Max(1e-9, hv[1]), "0.0") + " FU a hectare, crop grown " + F(hv[3] / hv[2], "0.000") +
                                      ", sown " + F(hv[4] / hv[2], "0.000") + ", brought in " + F(hv[5] / hv[2], "0.000") + "  (" + F(hv[0], "0") + " FU on " + F(hv[1], "0") + " ha)");
                var dm = TC2.Core.Places.SettlementSystem.Demo;
                if (dm != null && dm[0] > 0)
                    Console.WriteLine("demography: deaths " + F(dm[1] / dm[0] * 1000 * SimTime.DaysPerYear, "0.00") + " a 1000 person-years, infants " + F(dm[3] > 0 ? dm[4] / dm[3] * 1000 * SimTime.DaysPerYear : 0, "0.0") +
                                      " a 1000 infant-years; conceptions " + F(dm[5] > 0 ? dm[6] / dm[5] : 0, "0.00000") + " a weighted woman-day (" + F(dm[9] > 0 ? dm[6] / dm[9] : 0, "0.00000") + " a woman-day), marriages " +
                                      F(dm[7] > 0 ? dm[8] / dm[7] : 0, "0.00000") + " a weighted woman-day  (" + F(dm[0], "0") + " person-days, " + F(dm[9], "0") + " woman-days open to conceive)");
                var wg = TC2.Core.Places.Migration.Weighs;
                if (wg != null && wg[0] > 0)
                    Console.WriteLine("weighing leaving: " + F(wg[0], "0") + " village-years, pressure " + F(wg[1] / wg[0], "0.000") + " (crowding " + F(wg[2] / wg[0], "0.000") +
                                      ", short rations " + F(wg[3] / wg[0], "0.000") + ", famine memory " + F(wg[4] / wg[0], "0.000") + "), pressed " + F(wg[5] / wg[0], "0.000") +
                                      "; households able " + F(wg[6], "0") + ", chose to go " + F(wg[7], "0"));
                var hb = TC2.Core.Agents.AgentSystem.Hours;
                if (hb != null && hb[7] > 0)
                {
                    double pd = hb[7];
                    Console.WriteLine("COARSE hours a working person-day: had " + F(hb[6] / pd, "0.000") + "  hearth wood " + F(hb[0] / pd, "0.000") + "  crops " + F(hb[1] / pd, "0.000") +
                                      "  building " + F((hb[2] + hb[4]) / pd, "0.000") + "  wild " + F(m.WildHours / pd, "0.000") + "  store wood " + F(hb[5] / pd, "0.000") +
                                      "  craft " + F(m.CraftHours / pd, "0.000") + "  (" + F(pd, "0") + " person-days)");
                    if (hb[9] > 0)
                        Console.WriteLine("  the wild weighed on " + F(hb[9], "0") + " person-days: urge " + F(hb[10] / hb[9], "0.0000") + ", free " + F(hb[12] / hb[9], "0.000") +
                                          " h, planned " + F(hb[8] / hb[9], "0.000") + " h a day, below the least trip " + F(hb[11] / hb[9], "0.000") + " h a day; chose hunting " + F(hb[13] / hb[9], "0.000") + ", fishing " + F(hb[14] / hb[9], "0.000") + ", gathering " + F(hb[15] / hb[9], "0.000"));
                    if (hb[9] > 0)
                    {
                        Console.WriteLine("  rations short by " + F(hb[40] / hb[9], "0.0000") + " on average; the store seen empty on " + F(hb[41] / hb[9], "0.0000") +
                                          " of those days, short (out for need) on " + F(hb[42] / hb[9], "0.0000"));
                        var sbc = new StringBuilder("  hunting chosen by day of the world's 12-day plant cycle:");
                        for (int i = 0; i < 12; i++) sbc.Append(" " + (hb[16 + i] > 0 ? F(hb[28 + i] / hb[16 + i], "0.000") : "-"));
                        Console.WriteLine(sbc.ToString());
                    }
                }
                Console.WriteLine("village steps " + m.VillageSteps + " for " + m.VillageDays + " village-days" +
                                  (un != null && w.StepDays > 1 ? "  not calm (days): hunger " + un[0] + ", epidemic " + un[1] + ", sick " + un[2] + ", hurt " + un[3] + ", away " + un[4] + ", leaving " + un[5] + ", band " + un[6] : ""));
            }
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
            Forms(w);   // (how the homes and works were built: D2-114)
            if (TC2.Core.Places.Trade.SeaShort != null)
                Console.WriteLine("sea short: relief by sea bound by the boats' room " + TC2.Core.Places.Trade.SeaShort[1] + " of " + TC2.Core.Places.Trade.SeaShort[2] + " gifts, " + F(TC2.Core.Places.Trade.SeaShort[0], "0") + " FU more would have gone");
            if (a.Has("civ")) CivTimelines(w, a.Int("civ", 4));
            if (a.Has("civ-of")) CivTimelines(w, 1, a.Int("civ-of", 0));   // (one village's timeline by its id: C5.3)
            if (a.Has("boats"))
            {
                // a village's boats (C9.1): each row of kind Boat - its owner, where it lies, whether it is made, kept, lost, and whether its spot is dry
                int bsid = a.Int("boats", 0);
                var P = w.Planet;
                ref var bs = ref w.Settlements.Get(bsid);
                Console.WriteLine("boats of " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(bsid)) + " #" + bsid + ": sea ground " + bs.Sea.A + ", shore ground " + bs.Fish.A + ", sea worth " + F(TC2.Core.Places.Boats.SeaExpect(w, in bs), "0.00") + " against " + F(bs.Fish.Expect, "0.00"));
                if (bs.Fish.A >= 0 || bs.Sea.A >= 0)
                {
                    // the landing (C9.2) and where a boat begun now would be laid down
                    TC2.Core.Places.Boats.Landing(w, bsid, out float lx, out float ly);
                    ref var bs2 = ref w.Settlements.Get(bsid);
                    double cdx = (double)bs2.LandX - bs2.SquareX, cdy = (double)bs2.LandY - bs2.SquareY;
                    Console.WriteLine("  the landing at " + F(bs2.LandX, "0") + "," + F(bs2.LandY, "0") + " (" + F(System.Math.Sqrt(cdx * cdx + cdy * cdy), "0") + " m from the square) since " + SimTime.Format(bs2.LandAt));
                    double ldx = (double)lx - bs.SquareX, ldy = (double)ly - bs.SquareY;
                    Console.WriteLine("  next boat at " + F(lx, "0") + "," + F(ly, "0") + " (" + F(System.Math.Sqrt(ldx * ldx + ldy * ldy), "0") + " m from the square)" + (TC2.Core.Planet.Shore.Dry(P, bs.LocalToSphere(lx, ly, P.Radius)) ? " dry" : " WET"));
                }
                var bl = w.BuildingsOf(bsid);
                for (int i = 0; i < bl.Count; i++)
                {
                    ref var b = ref w.Buildings.Get(bl[i]);
                    if (!TC2.Core.Places.Boats.IsBoat(b.Kind)) continue;
                    double dx = (double)b.X - bs.SquareX, dy = (double)b.Y - bs.SquareY;
                    bool dry = TC2.Core.Planet.Shore.Dry(P, bs.LocalToSphere(b.X, b.Y, P.Radius));
                    double ground = TC2.Core.Planet.Shore.At(P, bs.LocalToSphere(b.X, b.Y, P.Radius), out _, out double lakeAt) - P.SeaLevel;
                    Console.WriteLine("  #" + bl[i] + " hh " + b.Household + " (last " + b.Last + ") at " + F(b.X, "0") + "," + F(b.Y, "0") + " (" + F(System.Math.Sqrt(dx * dx + dy * dy), "0") + " m from the square) done " + b.Done +
                                      " hours " + F(b.Hours, "0") + " left " + (b.Left != 0 ? SimTime.Format(b.Left) : "-") + " cond " + F(b.Cond, "0.0") + " built " + SimTime.Format(b.Built) + (dry ? " dry" : TC2.Core.Places.Boats.Afloat(P, bs.LocalToSphere(b.X, b.Y, P.Radius)) ? " WET" : " on the waterline") + " (" + F(ground, "0.0") + " m above the sea, lake " + F(lakeAt, "0.0") + " m)" +
                                      (TC2.Core.Places.Boats.Sound(in b, w.Now) ? " sound" : ""));
                }
            }
            if (a.Has("events"))
            {
                // every kept record of one kind, with its cause (C5.4: `--events OreTaken`)
                if (System.Enum.TryParse(a.Get("events"), out HistoryKind ek))
                    for (int i = 0; i < w.History.Count; i++)
                    {
                        var e = w.History.At(i);
                        if (e.Kind != ek) continue;
                        string why = e.Cause1 != 0 && w.History.TryGet(e.Cause1, out var c) ? "   <- " + HistoryText.Line(w, c) : "";
                        Console.WriteLine("EVENT " + HistoryText.Line(w, e) + why);
                    }
                else Console.WriteLine("no such history kind: " + a.Get("events"));
            }   // (C1: each village's works, woods, rock, arms, walls, raids - and why)
            string annals = a.Get("annals");
            if (annals != null)
            {
                // each year in numbers (calibration: rates per person-year, by year)
                var sb = new StringBuilder("year,people,places,births,deaths,hunger,sick,wounds,disaster,famines,epidemics,raids,walls,tributes,founded,abandoned,found,lost\n");
                for (int i = 0; i < w.Annals.Count; i++)
                {
                    var y = w.Annals[i];
                    sb.Append(y.Year).Append(',').Append(y.People).Append(',').Append(y.Places).Append(',').Append(y.Births).Append(',').Append(y.Deaths).Append(',')
                      .Append(y.Hunger).Append(',').Append(y.Sick).Append(',').Append(y.Wounds).Append(',').Append(y.Disaster).Append(',').Append(y.Famines).Append(',')
                      .Append(y.Epidemics).Append(',').Append(y.Raids).Append(',').Append(y.Walls).Append(',').Append(y.Tributes).Append(',').Append(y.Founded).Append(',')
                      .Append(y.Abandoned).Append(',').Append(y.Found).Append(',').Append(y.Lost).Append('\n');
                }
                File.WriteAllText(annals, sb.ToString());
            }
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

        /// <summary>Each boat rowing a way over the sea now (C9.3), once a leg: when, whose, from and to, its crew and load.</summary>
        static void Voyagers(World w, HashSet<long> seen)
        {
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || p.Act != TC2.Core.Agents.ActivityKind.Voyage || p.Step != TC2.Core.Agents.StepKind.Travel) continue;
                long key = ((long)p.WhyArg << 32) ^ p.T1;
                if (!seen.Add(key)) continue;
                int crew = 0; var mem = w.Members(p.Household);
                for (int i = 0; i < mem.Count; i++) { var q = w.People.Get(mem[i]); if (q.Alive && q.Voyage == p.Voyage && q.Step == TC2.Core.Agents.StepKind.Travel) crew++; }
                var r = w.Agents.RouteOf(in p);
                int from = r == null ? 0 : (p.S0 <= 0.5f ? r.A : r.B);
                Console.WriteLine("VOYAGE " + SimTime.Format(w.Now) + " boat " + p.WhyArg + " from " + w.History.NameOf(EntityRef.Settlement(from)) + " #" + from + " to " + w.History.NameOf(EntityRef.Settlement(p.Dest)) + " #" + p.Dest +
                                  " crew " + crew + " load " + p.Cargo + " " + F(p.CargoQty, "0") + " (as " + p.CargoTo + ") leg " + SimTime.Format(p.T0) + " -> " + SimTime.Format(p.T1) + (r != null ? " way " + F(r.Length, "0") + " m" : ""));
            }
        }

        /// <summary>How the world's standing homes were built (D2-114): plans, roofs, covers, walls, chimneys - overall and by the size of their village.</summary>
        static void Forms(World w)
        {
            var A = typeof(TC2.Core.Places.Architecture);
            int[] plan = new int[6], roof = new int[3], cover = new int[5], walls = new int[5]; int chim = 0, n = 0, works = 0, huts = 0, fronting = 0, streets = 0, streeted = 0;
            int[,] bySize = new int[3, 6];   // villages under 100, 100-249, 250 and more: their plans
            int[] coverWorks = new int[5];
            int traditions = 0; var seen = new HashSet<int>(); var lines = new HashSet<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                if (seen.Add(s.Tradition)) traditions++;
                streets += s.StreetN; if (s.StreetN > 0) streeted++;
                lines.Add(TC2.Core.Places.Architecture.Line(s.Tradition));
                int size = s.Population < 100 ? 0 : s.Population < 250 ? 1 : 2;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (!TC2.Core.Places.Architecture.Has(b.Form) || TC2.Core.Places.Homes.Fallen(in b, w.Now)) continue;
                    if (!TC2.Core.Places.Structures.IsHome(b.Kind)) { works++; coverWorks[TC2.Core.Places.Architecture.CoverOf(b.Form)]++; continue; }
                    if (b.Kind < TC2.Core.Places.Construction.House) { if (b.Household != 0) huts++; continue; }
                    n++;
                    if (TC2.Core.Places.Streets.Facing(in s, b.X, b.Y, out _, out _)) fronting++;
                    int pl = TC2.Core.Places.Architecture.PlanOf(b.Form);
                    plan[pl]++; roof[TC2.Core.Places.Architecture.RoofOf(b.Form)]++; cover[TC2.Core.Places.Architecture.CoverOf(b.Form)]++; walls[TC2.Core.Places.Architecture.WallsOf(b.Form)]++;
                    if (TC2.Core.Places.Architecture.Chimney(b.Form)) chim++;
                    bySize[size, pl]++;
                }
            }
            string P(int[] a) { var sb = new StringBuilder(); for (int i = 0; i < a.Length; i++) sb.Append(i > 0 ? "/" : "").Append(a[i]); return sb.ToString(); }
            Console.WriteLine("forms: " + n + " houses in " + traditions + " traditions of " + lines.Count + " lines; plans small/cottage/long/two/hall/row " + P(plan) + "; roofs gable/hipped/half-hip " + P(roof) +
                              "; covers straw/reed/shingle/tile/slate " + P(cover) + "; walls daub/limewash/framed/logs/stone " + P(walls) + "; chimneys " + chim + "; rebuilt " + w.Metrics.HomesRebuilt + "; works " + works + " (covers " + P(coverWorks) + ")");
            double seenSum = 0; int villages = 0, nearStore = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                villages++; seenSum += w.Conflict.SeenChance(sid);
                TC2.Core.Places.Construction.GranarySpot(in s, out double gx, out double gy);
                var hh = w.HouseholdsOf(sid);
                for (int i = 0; i < hh.Count; i++) { var h = w.Households.Get(hh[i]); double dx = h.HomeX - gx, dy = h.HomeY - gy; if (dx * dx + dy * dy <= 2500.0) nearStore++; }
            }
            Console.WriteLine("streets: " + streeted + " villages with streets, " + streets + " streets; houses fronting a street " + fronting + " of " + n + "; huts lived in " + huts +
                              "; homes within 50 m of the store " + F(villages > 0 ? (double)nearStore / villages : 0, "0.0") + " a village, a taker seen there " + F(villages > 0 ? seenSum / villages : 0, "0.000"));
            for (int z = 0; z < 3; z++)
                Console.WriteLine("forms: villages of " + (z == 0 ? "<100" : z == 1 ? "100-249" : "250+") + " people: plans " + bySize[z, 0] + "/" + bySize[z, 1] + "/" + bySize[z, 2] + "/" + bySize[z, 3] + "/" + bySize[z, 4] + "/" + bySize[z, 5]);
            // (C13.3) the ground: buildings whose ground overlaps, homes in the open, towns with a divided core and its rows, market places
            int hh0 = 0, hw0 = 0, ww0 = 0, open0 = 0, towns = 0, rows = 0, markets = 0; double coreSum = 0, marketSum = 0, marketMost = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                TC2.Core.Places.Footprints.Audit(w, sid, out int a1, out int a2, out int a3, out int a4);
                hh0 += a1; hw0 += a2; ww0 += a3; open0 += a4;
                if (s.CoreR > 0f) { towns++; coreSum += s.CoreR; }
                if (s.MarketR > 0f) { markets++; marketSum += s.MarketR; if (s.MarketR > marketMost) marketMost = s.MarketR; }
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++) { var b = w.Buildings.Get(bl[i]); if (b.Kind >= TC2.Core.Places.Construction.House && TC2.Core.Places.Structures.IsHome(b.Kind) && !TC2.Core.Places.Homes.Fallen(in b, w.Now) && TC2.Core.Places.Architecture.PlanOf(b.Form) == TC2.Core.Places.Architecture.Row) rows++; }
            }
            Console.WriteLine("town: overlapping home/home " + hh0 + ", home/work " + hw0 + ", work/work " + ww0 + "; homes in the open " + open0 + "; homes taken down " + w.Metrics.HomesCleared +
                              "; towns with a divided core " + towns + " (reach " + F(towns > 0 ? coreSum / towns : 0, "0") + " m), narrow town houses standing " + rows + " (raised " + w.Metrics.TownHouses + ")" +
                              "; market places " + markets + " (radius " + F(markets > 0 ? marketSum / markets : 0, "0.0") + " m, most " + F(marketMost, "0.0") + ")");
        }

        /// <summary>One line a village (D2-113's tracing): its people and store now, and what the tallies counted since the last line.</summary>
        static void VTrace(World w, System.IO.StreamWriter vt)
        {
            var d = TC2.Core.Places.Trade.Diag; var hb = TC2.Core.Places.SettlementSystem.HungerBy; var lb = TC2.Core.Places.Migration.LeftBy;
            int year = (int)(w.Now / SimTime.Year);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var st = ref w.Settlements.Get(sid);
                if (sid >= hb.Length) continue;
                bool any = d[sid * 4] + d[sid * 4 + 1] + d[sid * 4 + 2] + d[sid * 4 + 3] + hb[sid] + lb[sid] > 0;
                if (!st.Inhabited && !any) continue;
                double days = TC2.Core.Places.Trade.DaysOfFood(w, in st, out _);
                bool famine = st.FamineAt != 0 && w.Now - st.FamineAt <= SimTime.Year;
                int planks = 0;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (TC2.Core.Places.Boats.IsPlank(b.Kind) && TC2.Core.Places.Boats.Sound(in b, w.Now) && b.Household != 0) planks++; }
                vt.WriteLine(year + "," + sid + "," + w.History.NameOf(EntityRef.Settlement(sid)) + "," + st.Population + "," + F(st.Granary, "0") + "," + F(days, "0.0") + "," + F(st.Ration, "0.00") + "," +
                             F(st.Memory, "0.00") + "," + F(w.Migration.Pressure(in st), "0.00") + "," + F(st.ArableHa, "0") + "," + (famine ? 1 : 0) + "," + hb[sid] + "," +
                             F(d[sid * 4], "0") + "," + F(d[sid * 4 + 1], "0") + "," + F(d[sid * 4 + 2], "0") + "," + F(d[sid * 4 + 3], "0") + "," + lb[sid] + "," + planks + "," + st.Lord);
                d[sid * 4] = d[sid * 4 + 1] = d[sid * 4 + 2] = d[sid * 4 + 3] = 0; hb[sid] = 0; lb[sid] = 0;
            }
            vt.Flush();
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
            /// <summary>
        /// C1 tools (`run --civ N`): the N largest inhabited villages' civilisation in the order it happened - founded, works begun and raised,
        /// woods receded, rock found, the first arms, the watch, walls, bands and raids, tribute, famines - each line with its cause.
        /// </summary>
        static void CivTimelines(World w, int n, int only = 0)
        {
            var sids = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited && (only == 0 || sid == only)) sids.Add(sid);
            sids.Sort((x, y) => { int c = w.Settlements.Get(y).Population.CompareTo(w.Settlements.Get(x).Population); return c != 0 ? c : x.CompareTo(y); });
            for (int k = 0; k < sids.Count && k < n; k++)
            {
                int sid = sids[k];
                var st = w.Settlements.Get(sid);
                Console.WriteLine("CIV " + w.History.NameOf(EntityRef.Settlement(sid)) + " #" + sid + " (" + st.Population + " people; granaries " + st.Granaries + ", barns " + st.Barns +
                                  ", stone " + F(st.Stone, "0") + ", arms " + F(st.Arms, "0") + ", metal " + F(st.Metal, "0") + " kg, wall " + st.Wall + (st.Wall == 1 ? " (" + F(st.WorkBuild, "0") + " h in, ring " + F(st.WallR, "0") + " m)" : "") +
                                  (st.OldR > 0f ? ", an old ring of " + F(st.OldR, "0") + " m inside (" + F(st.OldCond, "0.00") + ")" : "") + ", wood " + F(st.Woodstore, "0") + " (woodlot " + F(st.WoodDist, "0") + " m), project " + (st.Project != 0 ? TC2.Core.Places.Structures.Name(st.ProjectKind) : "none") + (st.Lord != 0 ? ", pays tribute to " + w.History.NameOf(EntityRef.Settlement(st.Lord)) : "") + "):");
                int lines = 0;
                for (int i = 0; i < w.History.Count && lines < 60; i++)
                {
                    var e = w.History.At(i);
                    bool mine = e.Settlement == sid || (e.Kind == HistoryKind.BandRaised && e.Subject.Kind == EntityKind.Person && w.People.Exists(e.Subject.Id) && w.People.Get(e.Subject.Id).Home == sid);
                    if (!mine) continue;
                    switch (e.Kind)
                    {
                        case HistoryKind.SettlementFounded: case HistoryKind.WorkBegun: case HistoryKind.WorkBuilt: case HistoryKind.WoodsReceded:
                        case HistoryKind.StoneFound: case HistoryKind.OreFound: case HistoryKind.OreGaveOut: case HistoryKind.OreTaken: case HistoryKind.ArmsMade: case HistoryKind.WatchSet: case HistoryKind.WallPlanned:
                        case HistoryKind.WallBuilt: case HistoryKind.WallFallen: case HistoryKind.BandRaised: case HistoryKind.Raid:
                        case HistoryKind.TributeAgreed: case HistoryKind.TributeEnded: case HistoryKind.Famine: case HistoryKind.TechniqueKnown:
                        case HistoryKind.HomesDrowned: case HistoryKind.SettlementAbandoned:
                            break;
                        default: continue;
                    }
                    string why = "";
                    if (e.Cause1 != 0 && w.History.TryGet(e.Cause1, out var c)) why = "   <- " + HistoryText.Line(w, c);
                    Console.WriteLine("  " + HistoryText.Line(w, e) + why);
                    lines++;
                }
            }
        }
    }
}
