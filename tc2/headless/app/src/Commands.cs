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
            while (w.Now < end || (snapAt.Count > 0 && snapAt[0] <= w.Now))
            {
                long next = Math.Min(end, w.Now + report);
                bool snapNow = snapAt.Count > 0 && snapAt[0] <= next;
                if (snapNow) next = snapAt[0];
                w.AdvanceTo(next);
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
                                          "  fibre " + F(st.Fibre, "0") + " hides " + F(st.Hides, "0") + " metal " + F(st.Metal, "0.0") + (w.Learning.Clay(st.Cell) ? " clay" : "") + (w.Learning.Rock(st.Cell) ? " rock" : "") + (w.Learning.Ore(st.Cell) ? " ore" : "") +
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
                        Console.WriteLine("force: " + smithies + " smithies; " + fearing + " villages fear their neighbours, " + armed + " hold arms (" + F(arms, "0") + "); arms forged " + F(w.Metrics.ArmsMade, "0") +
                                          ", hours at the smithies " + F(w.Metrics.SmithyHours, "0") + ", metal smelted " + F(w.Metrics.MetalKg, "0") + " kg");
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
                Console.WriteLine("raids: bands raised " + m.Bands + ", raids " + m.Raids + " (driven off " + m.RaidsBeaten + "), loot " + F(m.RaidLoot, "0") + " FU");
                Console.WriteLine("answers: amends asked " + m.Claims + ", made " + m.Amends + ", refused " + m.Refusals + "; loads taken back " + m.TakenBack);
                Console.WriteLine("trade: trades " + m.TradeTrips + ", gain " + F(m.TradeValue, "0") + ", metal moved " + F(m.MetalTraded, "0") + " kg; relief " + F(m.ReliefFu, "0") + " FU");
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
            if (a.Has("civ")) CivTimelines(w, a.Int("civ", 4));   // (C1: each village's works, woods, rock, arms, walls, raids - and why)
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
        static void CivTimelines(World w, int n)
        {
            var sids = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) sids.Add(sid);
            sids.Sort((x, y) => { int c = w.Settlements.Get(y).Population.CompareTo(w.Settlements.Get(x).Population); return c != 0 ? c : x.CompareTo(y); });
            for (int k = 0; k < sids.Count && k < n; k++)
            {
                int sid = sids[k];
                var st = w.Settlements.Get(sid);
                Console.WriteLine("CIV " + w.History.NameOf(EntityRef.Settlement(sid)) + " #" + sid + " (" + st.Population + " people; granaries " + st.Granaries + ", barns " + st.Barns +
                                  ", stone " + F(st.Stone, "0") + ", arms " + F(st.Arms, "0") + ", metal " + F(st.Metal, "0") + " kg, wall " + st.Wall + (st.Lord != 0 ? ", pays tribute to " + w.History.NameOf(EntityRef.Settlement(st.Lord)) : "") + "):");
                int lines = 0;
                for (int i = 0; i < w.History.Count && lines < 60; i++)
                {
                    var e = w.History.At(i);
                    bool mine = e.Settlement == sid || (e.Kind == HistoryKind.BandRaised && e.Subject.Kind == EntityKind.Person && w.People.Exists(e.Subject.Id) && w.People.Get(e.Subject.Id).Home == sid);
                    if (!mine) continue;
                    switch (e.Kind)
                    {
                        case HistoryKind.SettlementFounded: case HistoryKind.WorkBegun: case HistoryKind.WorkBuilt: case HistoryKind.WoodsReceded:
                        case HistoryKind.StoneFound: case HistoryKind.ArmsMade: case HistoryKind.WatchSet: case HistoryKind.WallPlanned:
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
