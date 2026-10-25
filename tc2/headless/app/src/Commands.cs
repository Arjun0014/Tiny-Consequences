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

        /// <summary>Tools (reads only): a village's herd of one kind by household - households keeping it, grown females (mean condition, carrying or in
        /// milk), grown males, young.</summary>
        static string KindLine(World w, int sid, byte kind)
        {
            var law = TC2.Core.Places.Livestock.Laws[kind];
            int hh = 0, fem = 0, carrying = 0, males = 0, young = 0, hhNoMale = 0; double cond = 0;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                var mine = w.BeastsOf(hhs[i]);
                int n = 0, m = 0, f = 0;
                for (int j = 0; j < mine.Count; j++)
                {
                    ref var b = ref w.Beasts.Get(mine[j]);
                    if (!b.Alive || b.Kind != kind) continue;
                    n++;
                    if (b.AgeYears(w.Now) < law.Mature) { young++; continue; }
                    if (b.Sex == TC2.Core.Agents.Sex.Male) { males++; m++; }
                    else { fem++; f++; cond += b.Cond; if (b.Due != 0 || b.MilkUntil > w.Now) carrying++; }
                }
                if (n > 0) hh++;
                if (f > 0 && m == 0) hhNoMale++;
            }
            if (hh == 0) return "";
            return "  " + law.Name + "s: hh " + hh + ", F " + fem + " (c " + F(fem > 0 ? cond / fem : 0, "0.00") + ", carrying " + carrying + "), M " + males + ", young " + young + ", hh with F but no M " + hhNoMale;
        }

        static readonly string[] EndShort = { "age", "wolf", "hung", "sire", "old", "fail", "eaten", "hands", "past", "mast", "HARDp", "HARDm" };

        /// <summary>Tools: a village's births and ends by kind and cause since the last report (and resets them).</summary>
        static string VillageEnds(int sid)
        {
            var d = TC2.Core.Places.Livestock.DiagVillage;
            if (d == null || sid >= 4096) return "";
            int K = TC2.Core.Places.Livestock.Kinds, C = TC2.Core.Places.Livestock.Causes;
            var sb = new StringBuilder();
            for (int k = 0; k < K; k++)
            {
                int o = (sid * K + k) * (C + 1);
                long ended = 0;
                for (int c = 0; c < C; c++) ended += d[o + c];
                if (d[o + C] == 0 && ended == 0) continue;
                sb.Append(sb.Length == 0 ? "  since: " : "; ").Append(TC2.Core.Places.Livestock.Laws[k].Name).Append(" +").Append(d[o + C]);
                for (int c = 0; c < C; c++) if (d[o + c] > 0) sb.Append(' ').Append(EndShort[c]).Append(d[o + c]);
                for (int c = 0; c <= C; c++) d[o + c] = 0;
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

        /// <summary>`run --hot N`: the N people who began the most activities since the last report (each new activity raises `ActVer`), with where
        /// they are, where they live and what they do - a person caught in a loop of zero-length steps shows at once. Reads only.</summary>
        static void Hot(World w, Dictionary<int, int> last, int n)
        {
            var rows = new List<(int d, int id)>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                int d = last.TryGetValue(id, out int v) ? p.ActVer - v : 0;
                last[id] = p.ActVer;
                if (d > 0) rows.Add((d, id));
            }
            rows.Sort((x, y) => x.d != y.d ? y.d.CompareTo(x.d) : x.id.CompareTo(y.id));
            for (int i = 0; i < Math.Min(n, rows.Count); i++)
            {
                ref var p = ref w.People.Get(rows[i].id);
                int hh = p.Household;
                bool hx = hh != 0 && w.Households.Exists(hh);
                Console.WriteLine("    hot " + p.Id + " +" + rows[i].d + " acts  age " + F(p.AgeYears(w.Now), "0") + "  act " + p.Act + " step " + p.Step + " why " + p.Why +
                                  "  at " + p.At + " home " + p.Home + " dest " + p.Dest + " route " + p.RouteA + "-" + p.RouteB + " s " + F(p.S0, "0") + "->" + F(p.S1, "0") +
                                  " t " + (p.T1 - p.T0) + " s  household " + hh + (hx ? " (of " + w.Households.Get(hh).Settlement + ", party " + w.Households.Get(hh).Party + ", dissolved " + w.Households.Get(hh).Dissolved + ")" : " (gone)") +
                                  "  home inhabited " + (w.Settlements.Exists(p.Home) && w.Settlements.Get(p.Home).Inhabited) + "  travel index " + w.Travel.LiveEntries);
            }
        }

        /// <summary>`run --crafttally` (roles from practice): for each craft, the people its role reads today (the person page's test) against those who
        /// worked at it since the tally began - by village, summed over the world.</summary>
        static void CraftReport(World w)
        {
            string[] names = { "smithy (a smith: SkSmith 0.3)", "plank boats (a boatwright: led a plank boat's building)", "weaver's shed (a weaver: SkCraft 0.3)", "kiln (a potter: SkPot 0.3)" };
            for (int k = 0; k < TC2.Core.Diagnostics.CraftTally.Kinds; k++)
            {
                var by = TC2.Core.Diagnostics.CraftTally.By[k];
                int role = 0, roleWorked = 0, worked = 0, worked50 = 0, villages = 0; double hours = 0, topSum = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    var s = w.Settlements.Get(sid);
                    if (!s.Inhabited) continue;
                    var res = w.Residents(sid);
                    int r = 0, rw = 0, wk = 0, w50 = 0; double h = 0, top = 0;
                    for (int i = 0; i < res.Count; i++)
                    {
                        var p = w.People.Get(res[i]);
                        if (!p.Alive || p.AgeYears(w.Now) < 16) continue;
                        bool isRole = k == 0 ? p.SkSmith >= 0.3f : k == 1 ? TC2.Core.History.Biography.PlankBoats(w, p.Id) > 0 : k == 2 ? p.SkCraft >= 0.3f : p.SkPot >= 0.3f;   // (the person page's roles, C1.4b)
                        by.TryGetValue(p.Id, out double ph);
                        if (isRole) r++;
                        if (ph >= 1) { wk++; if (isRole) rw++; }
                        if (ph >= 50) w50++;
                        h += ph; if (ph > top) top = ph;
                    }
                    if (h <= 0 && r == 0) continue;
                    if (h > 0) villages++;
                    role += r; roleWorked += rw; worked += wk; worked50 += w50; hours += h; topSum += top;
                }
                var vy = TC2.Core.Diagnostics.CraftTally.VillageYear[k];
                if (vy.Count > 0)
                {
                    var hrs = new System.Collections.Generic.List<double>(); var hands = new System.Collections.Generic.List<int>();
                    foreach (var kv in vy) { hrs.Add(kv.Value); hands.Add(TC2.Core.Diagnostics.CraftTally.HandsOf(k, kv.Key)); }
                    hrs.Sort(); hands.Sort();
                    Func<System.Collections.Generic.List<double>, double, double> q = (l, f) => l[Math.Min(l.Count - 1, (int)(f * l.Count))];
                    Console.WriteLine("craft " + k + " village-years " + hrs.Count + ": hours median " + q(hrs, 0.5).ToString("0") + ", p90 " + q(hrs, 0.9).ToString("0") + ", p99 " + q(hrs, 0.99).ToString("0") + ", max " + hrs[hrs.Count - 1].ToString("0") +
                                      "; hands median " + hands[hands.Count / 2] + ", p90 " + hands[Math.Min(hands.Count - 1, (int)(0.9 * hands.Count))] + ", max " + hands[hands.Count - 1]);
                }
                Console.WriteLine("craft " + names[k] + ": the role read today " + role + " (of them worked " + roleWorked + "); worked 1 h+ " + worked + ", 50 h+ " + worked50 +
                                  "; in " + villages + " villages, " + hours.ToString("0") + " h (the busiest hand's share " + (100 * topSum / Math.Max(1, hours)).ToString("0") + " %)");
            }
        }

        /// <summary>The newest rules' switches (session 17), for `run` and `test` alike (a test that fails on a new rule is run again without it):
        /// `--noniche` (D2-140 against D2-139: C3.5's rule alone - a couple marries on its grain), `--nowallworks` (D2-141: a ring laid clear of homes
        /// only), `--noworkclear` (D2-141: works 1 m off a street's line).</summary>
        public static void Switches(Args a)
        {
            if (a.Has("noniche")) TC2.Core.Places.SettlementSystem.NicheOn = false;
            if (a.Has("nowallworks")) TC2.Core.Society.Conflict.WallWorksOff = true;
            if (a.Has("noworkclear")) TC2.Core.Places.Footprints.WorkClearOff = true;
            if (a.Has("oldrain")) TC2.Core.Planet.PlanetSystems.OldRain = true;   // (W-FOREST pass 2b: the rain's seasons alike at every latitude)
            if (a.Has("oldwoods")) TC2.Core.Planet.PlanetSystems.OldWoods = true;   // (D2-142 against D2-140/141 in one binary: trees by the annual mean)
            if (a.Has("nogarden")) TC2.Core.Places.Gardens.Off = true;
            if (a.Has("nothresh")) TC2.Core.Places.Threshing.Off = true;   // (C3.7b against C3.7a in one binary: the reaped grain in the store threshed)
            if (a.Has("oldgraves")) TC2.Core.Places.Burials.Unkept = true;   // (D2-154 against D2-151 in one binary: homes, works and crofts laid over the graves)
            if (a.Has("nomuck")) TC2.Core.Places.Muck.Off = true;   // (C3.7c off: the herds' dung reaches the fields carted or not, as D2-139 built it)
            if (a.Has("mucktally")) TC2.Core.Places.Muck.Tally = new double[4];   // (C3.7c: muck made, carted, leached; hours)
            if (a.Has("nostubble")) TC2.Core.Places.Stubble.Off = true;   // (C3.7d off: the herds keep off the fields, as before)
            if (a.Has("onefieldspot")) TC2.Core.Places.FieldSpots.One = true;   // (every field hand and carter of a village on its one field spot, as before session 20)
            if (a.Has("wetfieldspot")) TC2.Core.Places.FieldSpots.WetWay = true;   // (a household's field cell whether or not its hands can walk there dry-shod, as session 20 built it)
            if (a.Has("oldspare")) TC2.Core.Places.Livestock.OldSpare = true;   // (a food's cull from the kind with the most spares, as before session 20)
            if (a.Has("stocktally")) TC2.Core.Places.Livestock.StockYears = new double[TC2.Core.Places.Livestock.Kinds];   // (tools: head x years of each kind over the run)
            if (a.Has("oldhands")) TC2.Core.Places.Livestock.OldHands = true;   // (the hands' cull from the kind with the most spares, as session 20's branch)
            if (a.Has("stubbletally")) TC2.Core.Places.Stubble.Tally = new double[3];   // (C3.7d: kg eaten on the stubble and the fallow, day dung on the fields)
            if (a.Has("nobread")) TC2.Core.Society.Conflict.BreadOff = true;   // (C10.4 off: lordship by conquest alone, as D2-075 built it)
            if (a.Has("breadtally")) TC2.Core.Society.Conflict.BreadTally = new long[15];   // (C10.4: village-harvests by the relief still owed, bonds)
            if (a.Has("oldmight")) TC2.Core.Society.Conflict.MightOff = true;   // (C10.5 off: the day's fit grown people at home, a lord by its own village - D2-075's weighing)
            if (a.Has("mighttally")) TC2.Core.Society.Conflict.MightTally = new double[14];   // (C10.5: each weighing by the old count and by might)
            if (a.Has("oldmightarms")) TC2.Core.Society.Conflict.MightArmsOff = true;   // (C10.7a off: might counts people of fighting age by heads, as C10.5 weighed them)
            if (a.Has("armstally")) TC2.Core.Society.Conflict.ArmsTally = new double[12];   // (C10.7a: each weighing by heads and by lasting strength)
            if (a.Has("oldsworn")) TC2.Core.Society.Retinue.Off = true;   // (C10.7b off: nobody is sworn to a first household)
            if (a.Has("sworntally")) TC2.Core.Society.Retinue.Tally = new double[TC2.Core.Society.Retinue.TallyN];   // (C10.7b: men sworn and released, fed, their practice and bands)
            if (a.Has("olddemand")) TC2.Core.Society.Conflict.DemandOff = true;   // (C10.8 off: no village asks a tenth of another)
            if (a.Has("oldlapse")) TC2.Core.Society.Conflict.LapseOff = true;     // (C10.9 off: bonds for peace outlive their lord's death)
            if (a.Has("lapsetally")) TC2.Core.Society.Conflict.LapseTally = new double[TC2.Core.Society.Conflict.LapseTallyN];
            if (a.Has("jointally")) TC2.Core.Places.Migration.JoinTally = new double[TC2.Core.Places.Migration.JoinTallyN];   // (the ceiling measured: households looking for a village to join)
            if (a.Has("oldnames")) TC2.Core.Places.Migration.TonguesOff = true;   // (C11.1 off: every name from the one speech of before)
            if (a.Has("oldhouseways")) TC2.Core.Places.Architecture.WaysOff = true;   // (C11.1b off: every house in its village's way of building)
            if (a.Has("oldmargins")) TC2.Core.Places.Migration.MarginsOff = true;   // (C3.6g off: no daughter villages on the margins)
            if (a.Has("margintally")) TC2.Core.Places.Migration.MarginTally = new double[4];   // (C3.6g: the leavers who looked to their village's margins)
            if (a.Has("oldburn")) TC2.Core.Society.Conflict.BurnOff = true;   // (C8.0 off: no band fires a home)
            if (a.Has("oldharry")) TC2.Core.Society.Conflict.HarryOff = true;   // (C8.1a off: no band harries the fields outside the walls)
            if (a.Has("harrytally")) TC2.Core.Society.Conflict.HarryTally = new double[7];   // (C8.1a: bands broken on walls and the crops outside them)
            if (a.Has("burntally")) TC2.Core.Society.Conflict.BurnTally = new double[TC2.Core.Society.Conflict.BurnTallyN];   // (ravaging, measured first)
            if (a.Has("sizetally")) TC2.Core.Diagnostics.SizeTally.Sums = new double[TC2.Core.Diagnostics.SizeTally.Classes * TC2.Core.Diagnostics.SizeTally.Stride];   // (villages deserted, measured first: how villages of each size live)
            if (a.Has("demandtally")) TC2.Core.Society.Conflict.DemandTally = new double[TC2.Core.Society.Conflict.DemandTallyN];   // (C10.8: the askings and their answers)
            if (a.Has("raidtally")) TC2.Core.Society.Conflict.RaidTally = new double[TC2.Core.Society.Conflict.RaidTallyN];   // (C8.1 measured first: bands weighed over a wrong)
            if (a.Has("oldpeace")) TC2.Core.Society.Conflict.PeaceOff = true;   // (C10.10 off: villages of one realm feud as free villages do)
            if (a.Has("peacetally")) TC2.Core.Society.Conflict.PeaceTally = new double[TC2.Core.Society.Conflict.PeaceTallyN];   // (C10.10: claims judged, bands kept home)
            if (a.Has("oldways")) TC2.Core.Agents.RouteCache.LiveForest = true;   // (D2-160 off: routes from the woods as they are when built)
            if (a.Has("oldtribute")) TC2.Core.Society.Conflict.CarryOff = true;   // (C10.6 off: the tenth into the lord's store at the harvest, carried by nobody)
            if (a.Has("carrytally")) TC2.Core.Society.Conflict.CarryTally = new double[TC2.Core.Society.Conflict.CarryTallyN];   // (C10.6: loads carried, FU, lived, set aside, given back, by cart, km, a year uncarried, no way on foot; by sea)
            if (a.Has("oldacross")) TC2.Core.Places.Trade.AcrossOff = true;   // (C9.5a off: grain within a day's walk across water at once, the tenth across water kept at home)
            if (a.Has("acrosstally")) TC2.Core.Places.Trade.AcrossTally = new double[TC2.Core.Places.Trade.AcrossTallyN];   // (C9.5 survey: grain and trade across water at once between villages within a day's walk)
            if (a.Has("oldcross")) TC2.Core.Places.Boats.CrossOff = true;   // (C9.5b off: dugouts carry no loads, no boat is made to cross the water)
            if (a.Has("visittally")) TC2.Core.Health.Disease.VisitTally = new double[TC2.Core.Health.Disease.VisitTallyN];
            if (a.Has("oldvisits")) TC2.Core.Health.Disease.OldVisits = true;   // (C9.5c off: a COARSE day's visits to every village within a day's walk, across water too)   // (C9.5c survey: a COARSE day's visits, news and ways across water)
            if (a.Has("crosstally")) TC2.Core.Places.Boats.CrossTally = new double[TC2.Core.Places.Boats.CrossTallyN];   // (C9.5b: boats made to cross, dugouts' crossings)
            if (a.Has("wetways")) TC2.Core.Places.Afoot.Off = true;   // (D2-156 off: lived walks straight across the water, the well's and the idle's spots and crofts in it, the woods by the straight line)
            if (a.Has("waytally")) TC2.Core.Places.Afoot.Tally = new long[128];   // (tools: the walks bent round the water and those no bend kept dry, by what was done at their end)
            if (a.Has("oldmuckorder")) TC2.Core.Agents.AgentSystem.CoarseMuckOld = true;   // (D2-158: a COARSE day's muck before the household's own roof, as C3.7c built it)
            if (a.Has("oldgardenorder")) TC2.Core.Agents.AgentSystem.CoarseGardenOld = true;   // (D2-146: a COARSE day's garden before all but the fields, as C3.7a built it)   // (C3.7a against D2-142 in one binary: no gardens)
        }

        public static int Run(Args a)
        {
            var sw = Stopwatch.StartNew();
            if (a.Has("places")) { TC2.Core.Places.Livestock.Diag = new double[4 * 4096]; TC2.Core.Places.Livestock.DiagEnds = new long[4 * TC2.Core.Places.Livestock.Causes]; TC2.Core.Places.Livestock.DiagVillage = new long[4096 * TC2.Core.Places.Livestock.Kinds * (TC2.Core.Places.Livestock.Causes + 1)]; TC2.Core.Places.Livestock.DiagDoy = new long[TC2.Core.Places.Livestock.Kinds * (TC2.Core.Places.Livestock.Causes + 1) * SimTime.DaysPerYear]; TC2.Core.Places.Livestock.DiagReach = new double[10]; TC2.Core.Places.SettlementSystem.Unrests = new long[7]; TC2.Core.Agents.AgentSystem.Hours = new double[44]; TC2.Core.Places.SettlementSystem.Harvests = new double[6]; TC2.Core.Places.Migration.Weighs = new double[8]; TC2.Core.Places.SettlementSystem.Demo = new double[10]; }
            if (a.Has("nosea")) TC2.Core.Places.Trade.NoSea = true;
            if (a.Has("seashort")) TC2.Core.Places.Trade.SeaShort = new double[3];   // (C9.4 survey: relief by sea bound by the boats' room)
            if (a.Has("noweave")) TC2.Core.Places.Weaving.Off = true;
            if (a.Has("waresflow")) TC2.Core.Places.Trade.WaresFor = new double[6];
            if (a.Has("ledger")) { TC2.Core.Diagnostics.HouseLedger.By = new System.Collections.Generic.Dictionary<int, double[]>(); TC2.Core.Diagnostics.HouseLedger.From = (long)a.Int("ledger", 250) * TC2.Core.Foundation.SimTime.Year; }
            if (a.Has("ledgeryears")) TC2.Core.Diagnostics.HouseLedger.Years = new System.Collections.Generic.List<double[]>();   // (C3.3 design: household-years with their kin, written to the named csv)   // (C3.3 survey: what each household brings in and draws)
            if (a.Has("nojoin")) TC2.Core.Places.Migration.JoinOff = true;
            if (a.Has("nostanding")) TC2.Core.Society.Leaders.StandingOff = true;
            if (a.Has("nosoil")) TC2.Core.Places.SettlementSystem.SoilOff = true;   // (C3.6f against D2-138 in one binary: the fields never tire)
            if (a.Has("norest")) TC2.Core.Places.SettlementSystem.RestOff = true;   // (the fields tire, nobody rests them)
            if (a.Has("nodung")) TC2.Core.Places.SettlementSystem.DungOff = true;   // (the herds' dung gives nothing back)
            Switches(a);   // (--noniche, --nowallworks, --noworkclear: shared with `test`)
            if (a.Has("nicheyears")) TC2.Core.Places.SettlementSystem.NicheYears = a.Double("nicheyears", 4);   // (C3.5b: the years of a shortfall a couple's grain must carry)
            if (a.Has("nichetally")) TC2.Core.Places.SettlementSystem.NicheTally = new double[13];   // (C3.5b survey: marriages by the couple's living and what it brought)
            if (a.Has("nocarts")) TC2.Core.Places.Carts.Off = true;   // (C4.2 against C3.2 in one binary: no carts)
            if (a.Has("nohold")) TC2.Core.Places.Holdings.Off = true;   // (C3.3 against C1.4b in one binary: the village's store as before - no bins, no debts)
            if (a.Has("holdmeansoff")) TC2.Core.Places.Holdings.MeansOff = true;
            if (a.Has("nopay")) TC2.Core.Places.Holdings.PayOff = true;   // (C3.3a alone: makers are not paid)
            if (a.Has("nohard")) TC2.Core.Places.Holdings.HardOff = true;   // (C3.3a-b: sharing never contracts in hard times)
            if (a.Has("nohead")) TC2.Core.Society.Leaders.Off = true;   // (C3.3 alone: no first household, no headman)
            if (a.Has("notribhead")) TC2.Core.Society.Leaders.TributeOff = true;   // (C10.2 headmen alone: tribute to the lord village's common grain)
            if (a.Has("nosplit")) TC2.Core.Society.Leaders.SplitOff = true;   // (C10.2 alone: a contested succession never splits a village)
            if (a.Has("nomeans")) TC2.Core.Places.SettlementSystem.MeansOff = true;
            if (a.Has("notilth")) TC2.Core.Places.SettlementSystem.TilthOff = true;   // (C3.6 off: villages work their land as before whatever its want)
            if (a.Has("gaptally")) { TC2.Core.Places.SettlementSystem.GapTally = new double[TC2.Core.Places.SettlementSystem.GapK]; TC2.Core.Places.SettlementSystem.SheafTally = new double[3]; }   // (the hungry gap survey)
            if (a.Has("nosheaves")) TC2.Core.Places.SettlementSystem.SheavesOff = true;   // (D2-132 off: the whole harvest comes in on its last day)
            if (a.Has("noland")) TC2.Core.Places.Holdings.LandOff = true;   // (C3.6b off: no holdings kept)
            if (a.Has("landholds")) { TC2.Core.Places.Holdings.LandTally = new double[8]; TC2.Core.Places.Holdings.ShareTally = new double[5]; }   // (C3.6b: where land went; C3.6c: the harvest's shares)
            if (a.Has("bymouths")) TC2.Core.Places.Holdings.ByMouths = true;   // (C3.6c off: the harvest shared by mouths)
            if (a.Has("villageleave")) TC2.Core.Places.Migration.VillageLeave = true;   // (C3.6d off: households weigh leaving by their village's crowding)
            if (a.Has("noretire")) TC2.Core.Places.SettlementSystem.RetireOff = true;   // (C3.6e off: the old keep their land)
            if (a.Has("landholds")) TC2.Core.Places.SettlementSystem.RetireTally = new double[5];
            if (a.Has("landholds")) TC2.Core.Places.Migration.LeaveTally = new double[3];
            if (a.Has("landtally")) { TC2.Core.Agents.AgentSystem.LandHours = new double[2 * 5 * TC2.Core.Agents.AgentSystem.LandK]; TC2.Core.Places.SettlementSystem.LandTally = new double[2 * TC2.Core.Places.SettlementSystem.LandTallyK]; }   // (C3.6 survey: hours by crop stage and harvests where land binds)   // (C3.5 off: a couple marries whatever its means)
            if (a.Has("contests")) TC2.Core.Society.Leaders.ContestTally = new double[11];   // (C10.3: why contested successions did not split)
            TC2.Core.Places.Holdings.HungerTally = new double[16]; TC2.Core.Places.SettlementSystem.MarriageTally = new double[8];   // (C3.3c: who dies of hunger - by the household's own grain, debts and kin)   // (C3.3: the accounting alone - Means reads the village's granary as before)
            if (a.Has("nosmithcrew")) TC2.Core.Places.Forge.CrewOff = true;   // (C1.4b against the smithy before it in one binary: every smith at it, no practice)
            if (a.Has("carttally")) TC2.Core.Diagnostics.CartTally.T = new double[12];   // (C4.2 closing: why carts begun are given up)
            if (a.Has("gardentally")) TC2.Core.Places.Gardens.Tally = new double[8];   // (D2-144: the gardens' work by hemisphere and by their village's crop)
            if (a.Has("threshtally")) TC2.Core.Places.Threshing.Tally = new double[8];   // (C3.7b: the floor's grain and hours by the stage of the year)
            TC2.Core.Places.Burials.Tally = new double[2];   // (D2-151: burials and deaths with no grave - read by the report only)
            if (a.Has("crafttally")) { var ct = new System.Collections.Generic.Dictionary<int, double>[TC2.Core.Diagnostics.CraftTally.Kinds]; for (int k = 0; k < ct.Length; k++) ct[k] = new System.Collections.Generic.Dictionary<int, double>(); TC2.Core.Diagnostics.CraftTally.By = ct; var vy = new System.Collections.Generic.Dictionary<long, double>[TC2.Core.Diagnostics.CraftTally.Kinds]; for (int k = 0; k < vy.Length; k++) vy[k] = new System.Collections.Generic.Dictionary<long, double>(); TC2.Core.Diagnostics.CraftTally.VillageYear = vy; }   // (roles from practice: whose hours at each craft)
            if (a.Has("loads")) TC2.Core.Places.Trade.LoadTally = new double[4];   // (C4.2 survey: trading loads bound by what one person carries)
            if (a.Has("nokiln")) TC2.Core.Places.Pottery.Off = true;   // (C3.2 against D2-063 in one binary: no kilns - pots and jars from knowing how)
            if (a.Has("potsflow")) TC2.Core.Places.Trade.PotsFor = new double[8];   // (C3.2: what ware was traded for)
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
            var hotVer = new Dictionary<int, int>();
            World w;
            if (a.Has("load"))
            {
                w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out var hdr);
                Console.WriteLine("loaded " + a.Get("load") + " at " + SimTime.Format(w.Now) + " (saved " + hdr.SavedRealUtcMs + ")");
                if (a.Has("statesizes")) { StateSizes.Print(w); return 0; }   // (the memory pass, 14 §7: what the state holds, part by part)
                if (a.Has("warmroutes"))
                {
                    // (session 23, a determinism probe: the routes the view builds at its first frame for the ways it draws - TerrainLod, WaterView,
                    // WorldObjectsView - built here at load; if history then differs from a run without, watching changes history)
                    var ways = new List<TC2.Core.Agents.Way>();
                    w.Ways.Collect(w.Now, ways);
                    int built = 0;
                    foreach (var wy in ways) if (w.Routes.Get(w, wy.A, wy.B) != null) built++;
                    Console.WriteLine("warmroutes: " + built + " routes of " + ways.Count + " ways built at load");
                }
            }
            else w = WorldGenerator.Create(ConfigFrom(a));
            long genMs = sw.ElapsedMilliseconds;
            KindProfiler prof = a.Has("profile") ? new KindProfiler() : null;
            w.Profiler = prof;
            if (a.Get("trace") != null) w.Profiler = new TraceProfiler(a.Get("trace"), w);   // (tools: where a hanging run stopped)
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
                if (a.Has("hot")) Hot(w, hotVer, a.Int("hot", 5));   // (who began the most activities since the last report: a person caught in a loop)
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
                                          "  stock cattle " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Cattle) + " goats " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Goat) + " pigs " + w.Stock.CountOf(sid, TC2.Core.Places.Livestock.Pig) + PastureLine(sid) + HerdLine(w, sid) + VillageEnds(sid) + KindLine(w, sid, TC2.Core.Places.Livestock.Pig) + KindLine(w, sid, TC2.Core.Places.Livestock.Cattle));
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
                            {
                                // pots and jars (C3.2): kilns, their potters, ware made, taken by hearths, stood as jars, traded; the grain jars keep sealed; hearths
                                int kilns = 0, clay = 0, potters = 0, practised = 0, sealedV = 0, sealedNoKiln = 0, hhOwn = 0, hhKiln = 0, hhNone = 0; double jars = 0, grain = 0, sealedFu = 0, ware = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    var st2 = w.Settlements.Get(sid2);
                                    if (!st2.Inhabited) continue;
                                    bool kiln = TC2.Core.Places.Pottery.Kiln(w, sid2) != 0;
                                    if (kiln) kilns++;
                                    if (w.Learning.Clay(st2.Cell)) clay++;
                                    double held2 = Math.Min(st2.Granary, TC2.Core.Places.Structures.StoreCapacity(in st2));
                                    jars += st2.Jars; ware += st2.Pots; grain += held2; sealedFu += Math.Min(held2, st2.Jars * TC2.Core.Places.Pottery.JarHolds);
                                    if (TC2.Core.Places.Pottery.Sealed(in st2) >= 0.5) { sealedV++; if (!kiln) sealedNoKiln++; }
                                    var hl2 = w.HouseholdsOf(sid2);
                                    for (int i = 0; i < hl2.Count; i++)
                                    {
                                        var h2 = w.Households.Get(hl2[i]);
                                        if (TC2.Core.Places.Pottery.FiresOwn(w, sid2, TC2.Core.Places.Pottery.Best(w, hl2[i]))) hhOwn++; else if (h2.Pots > 0.5f * w.Members(hl2[i]).Count) hhKiln++; else hhNone++;
                                    }
                                    if (!kiln) continue;
                                    var res2 = w.Residents(sid2);
                                    for (int i = 0; i < res2.Count; i++) { var q = w.People.Get(res2[i]); if (TC2.Core.Places.Pottery.Maker(in st2, in q, w.Now)) { potters++; if (q.SkPot >= 0.3f) practised++; } }
                                }
                                Console.WriteLine("pots: " + kilns + " kilns of " + places + " villages (" + clay + " with clay within reach); potters " + potters + " (" + practised + " practised); fired " + F(w.Metrics.PotsMade, "0") + " kg in " + F(w.Metrics.KilnHours, "0") +
                                                  " h, taken by hearths " + F(w.Metrics.PotsDrawn, "0") + " kg, stood as jars " + F(w.Metrics.JarsSet, "0") + " kg, traded " + F(w.Metrics.PotsTraded, "0") + " kg; jars standing " + F(jars, "0") + " kg sealing " + F(100 * sealedFu / Math.Max(1.0, grain), "0") +
                                                  " % of the grain in granaries; villages half sealed or more " + sealedV + " (" + sealedNoKiln + " without a kiln); ware in stores " + F(ware, "0") + " kg; hearths firing their own pots " + hhOwn + ", in kiln pots " + hhKiln + ", without " + hhNone);
                                var pf = TC2.Core.Places.Trade.PotsFor;
                                if (pf != null) Console.WriteLine("pots traded for: " + F(pf[0], "0") + " kg (" + F(pf[6], "0") + " from kiln villages to villages without, " + F(pf[7], "0") + " to villages with no clay) for grain " + F(pf[1], "0") + " FU, fibre " + F(pf[2], "0") + " kg, hides " + F(pf[3], "0") + ", metal/iron " + F(pf[4], "0") + " kg, wares " + F(pf[5], "0"));
                            }
                            {
                                // the household's grain and what households owe (C3.3): grain held a member in days of a grown person's food, the share of the
                                // richest tenth, the Gini of grain a member over households, debts, and the year's flows since the start
                                var per = new System.Collections.Generic.List<double>(); double binSum = 0, commonSum = 0, granSum = 0; int hhN = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    var st2 = w.Settlements.Get(sid2);
                                    if (!st2.Inhabited) continue;
                                    commonSum += st2.Common; granSum += st2.Granary;
                                    var hl2 = w.HouseholdsOf(sid2);
                                    for (int i = 0; i < hl2.Count; i++) { var h2 = w.Households.Get(hl2[i]); int n2 = w.Members(hl2[i]).Count; if (n2 == 0) continue; hhN++; binSum += h2.Grain; per.Add(h2.Grain / n2); }
                                }
                                per.Sort();
                                double gini = 0, tot = 0; for (int i = 0; i < per.Count; i++) { tot += per[i]; gini += (2.0 * (i + 1) - per.Count - 1) * per[i]; }
                                gini = tot > 0 ? gini / (per.Count * tot) : 0;
                                double top = 0; for (int i = (int)(0.9 * per.Count); i < per.Count; i++) top += per[i];
                                string PQ(double q) => per.Count > 0 ? F(per[Math.Min(per.Count - 1, (int)(q * per.Count))], "0") : "-";
                                int debtors = 0, creditors = 0, maxBy = 0; double debt = 0; var by = new System.Collections.Generic.Dictionary<int, int>(); int lastD = 0;
                                for (int i = 0; i < w.Holdings.DebtCount; i++) { var d = w.Holdings.DebtAt(i); debt += d.Fu; if (d.Debtor != lastD) { debtors++; lastD = d.Debtor; } by.TryGetValue(d.Creditor, out int c0); by[d.Creditor] = c0 + 1; }
                                creditors = by.Count; foreach (var kv in by) if (kv.Value > maxBy) maxBy = kv.Value;
                                var mt = w.Metrics;
                                Console.WriteLine("hold: " + hhN + " households hold " + F(binSum, "0") + " FU, the villages' common grain " + F(commonSum, "0") + " (granaries " + F(granSum, "0") + "); grain a member p10/50/90 " + PQ(0.1) + "/" + PQ(0.5) + "/" + PQ(0.9) +
                                                  " FU, richest tenth " + F(100 * top / Math.Max(1e-9, tot), "0") + " %, Gini " + F(gini, "0.00") + "; debts " + w.Holdings.DebtCount + " (" + F(debt, "0") + " FU) of " + debtors + " households to " + creditors + " (most debtors to one " + maxBy + ")" +
                                                  "; fed from common grain " + F(mt.CommonFedFu, "0") + ", given by kin " + F(mt.KinFu, "0") + ", lent " + F(mt.LoanFu, "0") + ", repaid " + F(mt.RepaidFu, "0") + ", lapsed " + F(mt.DebtVoidFu, "0") + " FU; unshared in hard times " + F(mt.UnsharedFu, "0") + " FU");
                                if (!TC2.Core.Places.Holdings.LandOff)
                                {
                                    // land (C3.6b): households with and without land, land a head p10/50/90 and its Gini, against the want; unheld; where land went
                                    var lph = new System.Collections.Generic.List<double>(); int landless = 0, short2 = 0; double heldAll = 0, unheld = 0, wantAll = 0;
                                    for (int sid3 = w.Settlements.First(); sid3 != 0; sid3 = w.Settlements.Next(sid3))
                                    {
                                        var st3 = w.Settlements.Get(sid3);
                                        if (!st3.Inhabited) continue;
                                        unheld += w.Holdings.Unheld(sid3);
                                        var hl3 = w.HouseholdsOf(sid3);
                                        for (int i = 0; i < hl3.Count; i++)
                                        {
                                            int n3 = w.Members(hl3[i]).Count; if (n3 == 0) continue;
                                            double l3 = w.Households.Get(hl3[i]).Land, want3 = w.Holdings.LandWant(hl3[i]);
                                            heldAll += l3; wantAll += want3; lph.Add(l3 / n3);
                                            if (l3 < 0.05) landless++; else if (l3 < 0.5 * want3) short2++;
                                        }
                                    }
                                    lph.Sort();
                                    double lg = 0, lsum = 0; for (int i = 0; i < lph.Count; i++) { lsum += lph[i]; lg += (2.0 * (i + 1) - lph.Count - 1) * lph[i]; }
                                    lg = lsum > 0 ? lg / (lph.Count * lsum) : 0;
                                    string LQ(double q) => lph.Count > 0 ? F(lph[Math.Min(lph.Count - 1, (int)(q * lph.Count))], "0.00") : "-";
                                    var ltl = TC2.Core.Places.Holdings.LandTally;
                                    Console.WriteLine("land: " + lph.Count + " households hold " + F(heldAll, "0") + " ha against their want " + F(wantAll, "0") + " ha, unheld " + F(unheld, "0") + " ha; landless " + landless + ", under half their want " + short2 +
                                                      "; ha a head p10/50/90 " + LQ(0.1) + "/" + LQ(0.5) + "/" + LQ(0.9) + ", Gini " + F(lg, "0.00") +
                                                      (TC2.Core.Places.Holdings.ShareTally != null && TC2.Core.Places.Holdings.ShareTally[0] > 0 ? "; of the harvests the landless took " + F(TC2.Core.Places.Holdings.ShareTally[1] / Math.Max(1e-9, TC2.Core.Places.Holdings.ShareTally[2]), "0.00") + " of their mouths' share, those with their want " + F(TC2.Core.Places.Holdings.ShareTally[3] / Math.Max(1e-9, TC2.Core.Places.Holdings.ShareTally[4]), "0.00") : "") +
                                                      (TC2.Core.Places.Migration.LeaveTally != null ? "; households that left holding none/less than their want/their want " + F(TC2.Core.Places.Migration.LeaveTally[0], "0") + "/" + F(TC2.Core.Places.Migration.LeaveTally[1], "0") + "/" + F(TC2.Core.Places.Migration.LeaveTally[2], "0") : "") +
                                                      (TC2.Core.Places.SettlementSystem.RetireTally != null ? "; the old who handed on their land " + F(TC2.Core.Places.SettlementSystem.RetireTally[0], "0") + " (to a child's household " + F(TC2.Core.Places.SettlementSystem.RetireTally[1], "0") + ", " + F(TC2.Core.Places.SettlementSystem.RetireTally[2], "0") + " ha, " + F(TC2.Core.Places.SettlementSystem.RetireTally[3], "0") + " FU; nobody to go to " + F(TC2.Core.Places.SettlementSystem.RetireTally[4], "0") + ")" : "") +
                                                      (ltl != null ? "; portions " + F(ltl[0], "0") + " (" + F(ltl[1], "0") + " ha), the last out took " + F(ltl[2], "0") + " ha, heirs " + F(ltl[3], "0") + " ha, left by those who went " + F(ltl[4], "0") + " ha, given out " + F(ltl[5], "0") + " ha to " + F(ltl[6], "0") + ", lost " + F(ltl[7], "0") + " ha" : ""));
                                }
                                // makers paid in kind (C3.3b): what households paid makers, what makers earned (and were owed), smiths' pay, the villages' levies; and
                                // the makers' households against the others: grain a member
                                double mkG = 0, mkN = 0, otG = 0, otN = 0; int mkH = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    if (!w.Settlements.Get(sid2).Inhabited) continue;
                                    var hl2 = w.HouseholdsOf(sid2);
                                    for (int i = 0; i < hl2.Count; i++)
                                    {
                                        var mem2 = w.Members(hl2[i]); bool maker = false;
                                        for (int j = 0; j < mem2.Count; j++) { var q2 = w.People.Get(mem2[j]); if (q2.Alive && (q2.SkCraft >= 0.3f || q2.SkPot >= 0.3f || q2.SkSmith >= 0.3f)) maker = true; }
                                        if (mem2.Count == 0) continue;
                                        if (maker) { mkH++; mkG += w.Households.Get(hl2[i]).Grain; mkN += mem2.Count; } else { otG += w.Households.Get(hl2[i]).Grain; otN += mem2.Count; }
                                    }
                                }
                                {
                                    // the first households and their headmen (C10.2): villages led, the following of the first household against the village's
                                    // households, how long the place has been held, how it changed hands over the run, grain the first households fed the hungry with
                                    int led = 0, lvVillages = 0; double fol = 0, hhs = 0, lvYears = 0, lvMost = 0; var shares = new System.Collections.Generic.List<double>();
                                    for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                    {
                                        var st2 = w.Settlements.Get(sid2);
                                        if (!st2.Inhabited || w.HouseholdsOf(sid2).Count == 0) continue;
                                        lvVillages++;
                                        if (st2.HeadHh == 0) continue;
                                        led++;
                                        int f2 = TC2.Core.Society.Leaders.Following(w, sid2, st2.HeadHh, out _, out _, out _);
                                        fol += f2; hhs += w.HouseholdsOf(sid2).Count; lvYears += (w.Now - st2.HeadSince) / (double)TC2.Core.Foundation.SimTime.Year;
                                        shares.Add(f2 / (double)w.HouseholdsOf(sid2).Count); if (f2 > lvMost) lvMost = f2;
                                    }
                                    shares.Sort();
                                    var mt2 = w.Metrics;
                                    Console.WriteLine("leaders: " + led + " of " + lvVillages + " villages have a headman; the first household's following " + F(fol / Math.Max(1, led), "0.0") + " households of " + F(hhs / Math.Max(1, led), "0.0") +
                                                      " (share median " + (shares.Count > 0 ? F(100 * shares[shares.Count / 2], "0") : "-") + " %, most " + F(lvMost, "0") + "), held " + F(lvYears / Math.Max(1, led), "0.0") + " years; headmen risen " + mt2.Headmen +
                                                      " (within the household " + mt2.HeadsWithin + ", another household " + mt2.HeadsRisen + "); fed the hungry in hard times " + F(mt2.HeadFedFu, "0") + " FU; splits " + mt2.Splits + " (" + mt2.SplitHouseholds + " households)");
                                }
                                var ct = TC2.Core.Society.Leaders.ContestTally;
                                if (ct != null) Console.WriteLine("contests: vacancies " + F(ct[0] - ct[10], "0") + " (and " + F(ct[10], "0") + " factions weighed again when a place was found); no unbound rival of 6+ households " + F(ct[1], "0") + ", rival under half the first's following " + F(ct[2], "0") + ", too few people " + F(ct[3], "0") + ", looked for land " + F(ct[4], "0") + ", splits " + F(ct[5], "0") + "; looking for land: gave up " + F(ct[6], "0") + ", gone " + F(ct[9], "0") + ", a place found " + F(ct[10], "0") + "; mean rival " + F(ct[7] / Math.Max(1, ct[0]), "0.0") + " vs first " + F(ct[8] / Math.Max(1, ct[0]), "0.0") + " households");
                                var ht = TC2.Core.Places.Holdings.HungerTally;
                                var mtl = TC2.Core.Places.SettlementSystem.MarriageTally;
                                var ntl = TC2.Core.Places.SettlementSystem.NicheTally;
                                if (ntl != null)
                                {
                                    // (C3.5b survey: marriages by the couple's living at the village's last harvest and what it brought)
                                    string[] liv = { "living under a half", "to three quarters", "to one", "one or more" };
                                    var nsb = new StringBuilder("marriages by the couple's living (of a year's bread) and what it brought (under a year / to two / more):");
                                    for (int li = 0; li < 4; li++) nsb.Append("  ").Append(liv[li]).Append(' ').Append(F(ntl[li * 3], "0")).Append('/').Append(F(ntl[li * 3 + 1], "0")).Append('/').Append(F(ntl[li * 3 + 2], "0"));
                                    nsb.Append(";  matches a living turned away ").Append(F(ntl[12], "0"));
                                    Console.WriteLine(nsb.ToString());
                                }
                                if (mtl != null) { double mn = mtl[0] + mtl[1] + mtl[2] + mtl[3] + mtl[4]; Console.WriteLine("marriages by what the couple brought (years of their bread): under a quarter " + F(mtl[0], "0") + ", to a half " + F(mtl[1], "0") + ", to one " + F(mtl[2], "0") + ", to two " + F(mtl[3], "0") + ", more " + F(mtl[4], "0") + " (mean " + F(mtl[6] / Math.Max(1.0, mn), "0.00") + "); in villages whose sharing had contracted " + F(mtl[5], "0") + "; matches that waited for the means of a household " + F(mtl[7], "0")); }
                                if (ht != null && ht.Length > 15) Console.WriteLine("hunger deaths by the household's land: none " + F(ht[13], "0") + ", short of its want " + F(ht[14], "0") + ", its want " + F(ht[15], "0"));
                                if (ht != null) Console.WriteLine("hunger by household: own grain none " + F(ht[0], "0") + ", under 5 days " + F(ht[1], "0") + ", 5-20 " + F(ht[2], "0") + ", 20+ " + F(ht[3], "0") + "; in villages whose sharing had contracted " + F(ht[4], "0") + ", in households in debt " + F(ht[5], "0") + ", with no close kin in the village " + F(ht[6], "0") + "; in those villages what the households with grain spared beyond their own needs at the village's ration to the harvest would carry all the households without grain to it " + F(ht[7], "0") + ", a quarter to all of it " + F(ht[8], "0") + ", less " + F(ht[9], "0") + " (mean " + F(ht[10] / Math.Max(1.0, ht[4]), "0.00") + " of their need), with under a day of food in the store " + F(ht[11], "0") + ", " + F(ht[12] / Math.Max(1.0, ht[4]), "0.0") + " days to the harvest on average");
                                Console.WriteLine("makers paid: households paid makers " + F(mt.MakersPaidFu, "0") + " FU, makers earned " + F(mt.MakersEarnedFu, "0") + " (owed besides " + F(mt.MakersOwedFu, "0") + "), smiths " + F(mt.SmithsEarnedFu, "0") +
                                                  "; the villages paid from common grain " + F(mt.CommonPaidFu, "0") + " and levies " + F(mt.LevyFu, "0") + " FU; households of practised makers " + mkH + ": grain a member " + F(mkG / Math.Max(1, mkN), "0") + " FU against " + F(otG / Math.Max(1, otN), "0") + " for the others");
                            }
                            var hy0 = TC2.Core.Diagnostics.HouseLedger.Years;
                            if (hy0 != null && a.Get("ledgeryears") != null)
                            {
                                using (var lf = new System.IO.StreamWriter(a.Get("ledgeryears")))
                                {
                                    lf.WriteLine("year,sid,hid,harvest,brought,ate,members,workers,kids,old,craft,kin");
                                    foreach (var r in hy0)
                                    {
                                        var lsb = new System.Text.StringBuilder();
                                        for (int k = 0; k < 11; k++) lsb.Append(r[k].ToString(k == 0 ? "0.000" : k <= 2 || (k >= 6 && k <= 9) ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                                        for (int k = 11; k < r.Length; k++) { if (k > 11) lsb.Append(';'); lsb.Append(((int)r[k]).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                                        lf.WriteLine(lsb.ToString());
                                    }
                                }
                                Console.WriteLine("ledger years: " + hy0.Count + " household-years written to " + a.Get("ledgeryears"));
                            }
                            var hl0 = TC2.Core.Diagnostics.HouseLedger.By;
                            if (hl0 != null)
                            {
                                // (C3.3 survey) what each household brought in against what it drew: its share of the harvests by its crop work, wild food
                                // and herd produce, against what its larder took from the village's store - over households that lived a year or more
                                var nets = new System.Collections.Generic.List<double>(); var depPos = new System.Collections.Generic.List<double>(); var depNeg = new System.Collections.Generic.List<double>();
                                double P = 0, C = 0, moved = 0, py = 0, makerNet = 0, makerPy = 0, otherNet = 0, otherPy = 0, posYear = 0; int hh = 0, pos = 0, lmakers = 0;
                                foreach (var kv in hl0)
                                {
                                    var r = kv.Value;
                                    if (r[7] < TC2.Core.Foundation.SimTime.DaysPerYear || r[5] <= 0) continue;
                                    hh++;
                                    double lyears = r[7] / TC2.Core.Foundation.SimTime.DaysPerYear, pyr = r[5] / TC2.Core.Foundation.SimTime.DaysPerYear;
                                    double prod = r[0] + r[1] + r[2], cons = r[3] + r[2], net = prod - cons;
                                    P += prod; C += cons; py += pyr; if (net > 0) { moved += net; pos++; posYear += net / lyears; depPos.Add(r[6] / r[5]); } else depNeg.Add(r[6] / r[5]);
                                    nets.Add(net / pyr);
                                    if (r[4] / lyears > 100) { lmakers++; makerNet += net; makerPy += pyr; } else { otherNet += net; otherPy += pyr; }
                                }
                                nets.Sort();
                                string Q(double q) => nets.Count > 0 ? F(nets[(int)Math.Min(nets.Count - 1, Math.Floor(q * nets.Count))], "0") : "-";
                                double Mean(System.Collections.Generic.List<double> v) { double s2 = 0; foreach (var x in v) s2 += x; return v.Count > 0 ? s2 / v.Count : 0; }
                                Console.WriteLine("ledger: " + hh + " households a year or more since year " + (TC2.Core.Diagnostics.HouseLedger.From / TC2.Core.Foundation.SimTime.Year) + ": brought in " + F(P, "0") + " FU, drew " + F(C, "0") + " FU; moved between them by the store " + F(moved, "0") + " FU (" + F(100 * moved / Math.Max(1, C), "0") +
                                                  " % of what they drew); " + pos + " net givers (workers a member " + F(Mean(depPos), "0.00") + "), " + (hh - pos) + " net takers (" + F(Mean(depNeg), "0.00") + "); net a person-year by decile 10/30/50/70/90 % " + Q(0.1) + "/" + Q(0.3) + "/" + Q(0.5) + "/" + Q(0.7) + "/" + Q(0.9) +
                                                  " FU; a giver's surplus " + F(posYear / Math.Max(1, pos), "0") + " FU a year; households of workshop makers " + lmakers + " (net " + F(makerNet / Math.Max(1, makerPy), "0") + " FU a person-year, others " + F(otherNet / Math.Max(1, otherPy), "0") + ")");
                            }
                            {
                                // carts (C4.2): households with a handcart or an ox cart, being made, wanted; trading trips with a cart and what they carried
                                int hand = 0, ox = 0, making = 0, wanted = 0, hhs = 0, villagesWith = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    if (!w.Settlements.Get(sid2).Inhabited) continue;
                                    var hl2 = w.HouseholdsOf(sid2); bool any = false;
                                    for (int i = 0; i < hl2.Count; i++)
                                    {
                                        var h2 = w.Households.Get(hl2[i]); hhs++;
                                        if (h2.CartState == TC2.Core.Places.Carts.Made && !TC2.Core.Places.Carts.WornOut(in h2, w.Now)) { any = true; if (h2.CartKind == TC2.Core.Places.Carts.Ox) ox++; else hand++; }
                                        else if (h2.CartState == TC2.Core.Places.Carts.Making) making++;
                                        else if (h2.CartState == TC2.Core.Places.Carts.Wanted) wanted++;
                                    }
                                    if (any) villagesWith++;
                                }
                                Console.WriteLine("carts: " + hand + " handcarts and " + ox + " ox carts among " + hhs + " households (in " + villagesWith + " villages), " + making + " being made, " + wanted + " wanted; begun " + w.Metrics.CartsBegun + ", made " + w.Metrics.CartsMade +
                                                  ", worn out " + w.Metrics.CartsWorn + ", given up " + w.Metrics.CartsGivenUp + "; trading trips with a cart " + w.Metrics.CartTrips + " carrying out " + F(w.Metrics.CartKg / 1000, "0") + " t");
                            }
                            {
                                // the households' gardens (C3.7a): their crofts now, and what was worked and gathered over the run
                                int gh = 0, gWith = 0; double gM2 = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    if (!w.Settlements.Get(sid2).Inhabited) continue;
                                    var hl3 = w.HouseholdsOf(sid2);
                                    for (int i = 0; i < hl3.Count; i++) { var h3 = w.Households.Get(hl3[i]); gh++; if (h3.Garden > 0f) { gWith++; gM2 += h3.Garden; } }
                                }
                                Console.WriteLine("gardens: " + gWith + " of " + gh + " households have one (mean " + F(gWith > 0 ? gM2 / gWith : 0, "0") + " m2); worked " + F(w.Metrics.GardenHours, "0") + " h over the run, gathered " + F(w.Metrics.GardenFu, "0") + " FU");
                                var gTal = TC2.Core.Places.Gardens.Tally;
                                if (gTal != null)
                                    for (int gq = 0; gq < 8; gq += 4)
                                        Console.WriteLine("garden tally " + (gq == 0 ? "north" : "south") + ": " + F(gTal[gq], "0") + " h, " + F(gTal[gq + 1], "0") + " FU (" + F(gTal[gq] > 0 ? gTal[gq + 1] / gTal[gq] : 0, "0.000") + " FU an hour); dug or weeded while the crop was ripe or in " + F(gTal[gq + 2], "0") + " h, gathered while it was sown or growing " + F(gTal[gq + 3], "0") + " FU");
                            }
                            {
                                // the threshing floor (C3.7b): the sheaves standing now, what was threshed over the run and its hours
                                double shv = 0, grn = 0; int stacked = 0, vil = 0;
                                for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                {
                                    var st2 = w.Settlements.Get(sid2);
                                    if (!st2.Inhabited) continue;
                                    vil++; shv += st2.Sheaves; grn += st2.Granary; if (st2.Sheaves > 1.0) stacked++;
                                }
                                Console.WriteLine("threshing: " + F(w.Metrics.ThreshFu, "0") + " FU threshed in " + F(w.Metrics.ThreshHours, "0") + " h over the run (" + F(w.Metrics.ThreshFu > 0 ? w.Metrics.ThreshHours / w.Metrics.ThreshFu : 0, "0.000") + " h a FU); in the sheaf now " + F(shv, "0") + " FU of " + F(grn, "0") + " in the stores, " + stacked + " of " + vil + " villages with sheaves");
                                {
                                    // the woods' felling remembered (W-FELL): cells with stumps standing, their felled share
                                    var Pf = w.Planet; int fc = 0; double fs = 0, fmax = 0;
                                    for (int c = 0; c < Pf.Cells; c++) if (Pf.Felled[c] > 0.02f) { fc++; fs += Pf.Felled[c]; if (Pf.Felled[c] > fmax) fmax = Pf.Felled[c]; }
                                    Console.WriteLine("felled woods: " + fc + " cells with stumps standing (mean felled share " + F(fc > 0 ? fs / fc : 0, "0.00") + ", most " + F(fmax, "0.00") + ")");
                                }
                                {
                                    // the burial grounds (D2-151): burials over the run, deaths with no grave, the graves marked and fresh now
                                    double mk = 0, fr = 0; int burialGrounds = 0, overGrounds = 0, overSlots = 0, overStreet = 0, overSquare = 0, overBuilt = 0;
                                    for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2))
                                    {
                                        var st3 = w.Settlements.Get(sid2);
                                        if (st3.BurialCount == 0) continue;
                                        burialGrounds++; TC2.Core.Places.Burials.Read(in st3, w.Now, out double m3, out double f3); mk += m3; fr += f3;
                                        // (D2-154) the ground's slots a building, the square's earth or a street covers now - the view's rule (graves not drawn there)
                                        int slotsOver = 0;
                                        for (int jg = 0; jg < TC2.Core.Places.Burials.Slots; jg++)
                                        {
                                            TC2.Core.Places.Burials.SlotAt(in st3, jg, out double gx3, out double gy3);
                                            if (TC2.Core.Places.Footprints.DiscFree(w, sid2, 0, gx3, gy3, 0.9)) continue;
                                            slotsOver++;
                                            double sqx = gx3 - st3.SquareX, sqy = gy3 - st3.SquareY, sqr = TC2.Core.Places.Footprints.SquareEarth(in st3) + 0.9;
                                            if (TC2.Core.Places.Streets.On(in st3, gx3, gy3, 0.9 + TC2.Core.Places.Footprints.DrawnLine)) overStreet++;
                                            else if (sqx * sqx + sqy * sqy < sqr * sqr) overSquare++;
                                            else overBuilt++;
                                        }
                                        if (slotsOver > 0) overGrounds++;
                                        overSlots += slotsOver;
                                    }
                                    var bt = TC2.Core.Places.Burials.Tally;
                                    Console.WriteLine("burials: " + F(bt != null ? bt[0] : 0, "0") + " over the run, " + F(bt != null ? bt[1] : 0, "0") + " dead with no grave (lost at sea, under the wave or the lava); " + burialGrounds + " burial grounds, graves marked now " + F(mk, "0") + ", fresh " + F(fr, "0") +
                                                      "; grounds built over " + overGrounds + " (slots covered " + overSlots + " of " + burialGrounds * TC2.Core.Places.Burials.Slots + ": a street " + overStreet + ", the square " + overSquare + ", a building " + overBuilt + ")");
                                }
                                {
                                    // the muck (C3.7c): in the yards now, carted out over the run and its hours
                                    double myard = 0;
                                    for (int sid2 = w.Settlements.First(); sid2 != 0; sid2 = w.Settlements.Next(sid2)) { var stm = w.Settlements.Get(sid2); if (stm.Inhabited) myard += stm.Muck; }
                                    var mt = TC2.Core.Places.Muck.Tally;
                                    var stt = TC2.Core.Places.Stubble.Tally;
                                    if (stt != null)
                                        Console.WriteLine("stubble: the herds ate " + F(stt[0] / 1000.0, "0") + " t on the stubble and " + F(stt[1] / 1000.0, "0") + " t on the fallow over the run; their day dung on the fields " + F(stt[2] / 1000.0, "0") + " t (carted muck " + F(w.Metrics.MuckKg / 1000.0, "0") + " t)");
                                    Console.WriteLine("muck: " + F(w.Metrics.MuckKg / 1000.0, "0") + " t carted out onto the fields in " + F(w.Metrics.MuckHours, "0") + " h over the run (" + F(w.Metrics.MuckKg > 0 ? w.Metrics.MuckHours / (w.Metrics.MuckKg / 1000.0) : 0, "0.00") + " h a tonne); in the yards now " + F(myard / 1000.0, "0") + " t" +
                                                      (mt != null ? "; made " + F(mt[0] / 1000.0, "0") + " t, leached " + F(mt[2] / 1000.0, "0") + " t" : ""));
                                }
                                var tt = TC2.Core.Places.Threshing.Tally;
                                if (tt != null)
                                {
                                    string[] stn = { "sown", "growing", "ripe", "harvested" };
                                    var line = "thresh tally:";
                                    for (int k = 0; k < 4; k++) line += " " + stn[k] + " " + F(tt[2 * k], "0") + " FU " + F(tt[2 * k + 1], "0") + " h;";
                                    Console.WriteLine(line);
                                }
                            }
                            if (TC2.Core.Diagnostics.CraftTally.By != null) CraftReport(w);
                            var ctt = TC2.Core.Diagnostics.CartTally.T;
                            if (ctt != null) Console.WriteLine("cart tally: begun " + F(ctt[0], "0") + " (" + F(ctt[1], "0") + " while short, " + F(ctt[10], "0") + " in villages under 60); given up " + F(ctt[2], "0") + ": " + F(ctt[3], "0") + " with no hours in, " + F(ctt[4], "0") + " under half made, " + F(ctt[5], "0") + " short then, " + F(ctt[6], "0") + " with no able wright, " + F(ctt[7], "0") + " with one able member or none (mean able " + F(ctt[8] / Math.Max(1, ctt[2]), "0.0") + ", hours in " + F(ctt[11] / Math.Max(1, ctt[2]), "0.0") + "), " + F(ctt[9], "0") + " in villages under 60");
                            var lt = TC2.Core.Places.Trade.LoadTally;
                            if (lt != null) Console.WriteLine("loads: " + F(lt[0], "0") + " trading loads set out (" + F(lt[3] / 1000, "0") + " t), " + F(100 * lt[1] / Math.Max(1, lt[0]), "0") + " % of them bound by what one carries (more was spared and wanted), return loads so bound " + F(100 * lt[2] / Math.Max(1, lt[0]), "0") + " %");
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
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        if (!w.Settlements.Get(sid).Inhabited) continue;   // (an abandoned village's last lord is no lordship's depth)
                        int d = 0, r = sid;
                        while (d < 16) { int l = w.Conflict.LordOf(r); if (l == 0 || !w.Settlements.Get(l).Inhabited) break; r = l; d++; }
                        if (d > deepest) deepest = d;
                    }
                    Console.WriteLine("lordships: tribute agreed " + m.Tributes + (m.BreadBonds > 0 ? " (for bread " + m.BreadBonds + ")" : "") + ", ended " + m.TributesEnded + ", paid " + F(m.TributeFu, "0") + " FU; lords now " + lords +
                                      (biggest > 0 ? ", the greatest " + w.History.NameOf(EntityRef.Settlement(biggestRoot)) + " with " + biggest + " paying, depth " + deepest : ""));
                    var bt = TC2.Core.Society.Conflict.BreadTally;
                    if (bt != null)   // (C10.4: village-harvests with no lord by the relief they still owed their main giver beyond the harvest's spare, in days of their food)
                        Console.WriteLine("bread: village-harvests owing none " + bt[0] + ", 0-5 d " + bt[1] + ", 5-10 d " + bt[2] + ", 10-20 d " + bt[3] + ", 20-30 d " + bt[4] + ", 30-60 d " + bt[5] + ", 60+ d " + bt[6] +
                                          "; to a stronger giver within reach: 0-5 d " + bt[8] + ", 5-10 d " + bt[9] + ", 10-20 d " + bt[10] + ", 20-30 d " + bt[11] + ", 30-60 d " + bt[12] + ", 60+ d " + bt[13] + "; bonds " + bt[14]);
                    var mt = TC2.Core.Society.Conflict.MightTally;
                    if (mt != null)   // (C10.5: each weighing of the stronger - weighed, carried by the old count, carried by might, by both)
                        Console.WriteLine("might: bowing to raiders weighed " + mt[0] + " (the old count " + mt[1] + ", might " + mt[2] + ", both " + mt[3] + "); bowing for bread " + mt[4] + " (" + mt[5] + ", " + mt[6] + ", " + mt[7] +
                                          "); a lord no longer the stronger " + mt[8] + " (" + mt[9] + ", " + mt[10] + ", " + mt[11] + "); lords' calls counted " + mt[12] + " times, " + F(mt[13], "0") + " men");
                    var visitT = TC2.Core.Health.Disease.VisitTally;
                    if (visitT != null)   // (C9.5c survey: a COARSE day's visits between neighbours within a day's walk, along a way on foot and across water)
                        Console.WriteLine("visits: COARSE visits along a way on foot " + F(visitT[0], "0") + ", across water " + F(visitT[1], "0") + "; caught on them " + visitT[2] + " and " + visitT[3] +
                                          "; news met " + visitT[4] + " and " + visitT[5] + "; ways told worn to a path or a road with no way on foot " + visitT[6]);
                    var armt = TC2.Core.Society.Conflict.ArmsTally;
                    if (armt != null)   // (C10.7a: each weighing of the stronger - weighed, carried by heads, carried by lasting strength, by both)
                        Console.WriteLine("arms: bowing to raiders weighed " + armt[0] + " (by heads " + armt[1] + ", by lasting strength " + armt[2] + ", both " + armt[3] + "); bowing for bread " + armt[4] + " (" + armt[5] + ", " + armt[6] + ", " + armt[7] +
                                          "); a lord no longer the stronger " + armt[8] + " (" + armt[9] + ", " + armt[10] + ", " + armt[11] + ")");
                    var swt = TC2.Core.Society.Retinue.Tally;
                    if (swt != null)   // (C10.7b: men sworn to their villages' first households)
                    {
                        int swornNow = 0, keeping = 0;
                        for (int ps = w.People.First(); ps != 0; ps = w.People.Next(ps)) { ref var pq = ref w.People.Get(ps); if (pq.Alive && TC2.Core.Society.Retinue.SwornHere(w, in pq)) swornNow++; }
                        for (int ks = w.Settlements.First(); ks != 0; ks = w.Settlements.Next(ks)) if (w.Settlements.Get(ks).Inhabited && TC2.Core.Society.Retinue.Count(w, ks) > 0) keeping++;
                        Console.WriteLine("sworn: men sworn " + swt[0] + "; released - the grain short " + swt[1] + ", another first among the people " + swt[2] + ", past forty " + swt[3] + ", lord and man of two villages " + swt[4] + ", the lord dead " + swt[12] +
                                          "; fed " + F(swt[5], "0") + " FU (" + F(swt[6], "0") + " man-years); their practice " + F(swt[7], "0") + " h; bands with sworn men " + swt[8] + " (sworn men in them " + swt[9] +
                                          "); village-harvests keeping men " + swt[10] + ", the most sworn to one lord " + swt[11] + "; sworn now " + swornNow + " in " + keeping + " villages");
                    }
                    var demandTallyC108 = TC2.Core.Society.Conflict.DemandTally;
                    if (demandTallyC108 != null)   // (C10.8: the askings of a tenth and their answers)
                        Console.WriteLine("demands: free village-harvests weighed " + demandTallyC108[0] + ", asked " + demandTallyC108[1] + " - bowed " + demandTallyC108[2] + ", refused " + demandTallyC108[3] +
                                          "; asked by a village keeping fewer sworn men than the asked's own " + demandTallyC108[4]);
                    var rt = TC2.Core.Society.Conflict.RaidTally;
                    if (rt != null)   // (C8.1 measured first: bands weighed over a wrong, and the walls that kept them home)
                        Console.WriteLine("raidtally: bands weighed over a wrong " + rt[0] + " (against walled villages " + rt[1] + "); too few would go " + rt[2] + "; too weak for the defence they expect " + rt[3] +
                                          " (against walled villages " + rt[4] + ", of which would have dared without the wall " + rt[5] + "); raised " + rt[6] + " (against walled villages " + rt[7] + "), " + rt[8] + " people");
                    var hyt = TC2.Core.Society.Conflict.HarryTally;
                    if (hyt != null)   // (C8.1a: the harrying)
                        Console.WriteLine("harry: bands driven off " + hyt[0] + ", by a standing wall " + hyt[1] + ", with a crop standing " + hyt[2] + " (the harvest at stake " + F(hyt[3], "0") + " FU, " + hyt[4] +
                                          " raiders); harried " + hyt[5] + " (" + F(hyt[6], "0") + " FU of harvest burnt)");
                    var bnt = TC2.Core.Society.Conflict.BurnTally;
                    if (bnt != null)   // (ravaging, measured first: whose homes a band holding the field could fire)
                        Console.WriteLine("burn: raids " + bnt[0] + " (unseen " + bnt[1] + "), the band holding the field " + bnt[2] + "; of those with blood the band's village knows against the raided's people " + bnt[3] +
                                          " (" + bnt[9] + " deeds; on walled villages " + bnt[7] + "); the doers' households there " + bnt[4] + ", with a home standing " + bnt[5] + " - of " + bnt[6] +
                                          " homes standing in those villages; raids where they would be every home " + bnt[8] + "; held raids with a killing known " + bnt[10] + " (" + bnt[11] + " killings; the killers' households there " + bnt[13] + ", with a home standing " + bnt[12] + ")");
                    if (bnt != null)
                    {
                        int burnt = 0; var byV = new System.Collections.Generic.Dictionary<int, int>();
                        for (int hi = 0; hi < w.History.Count; hi++) { var he = w.History.At(hi); if (he.Kind != TC2.Core.History.HistoryKind.BuildingBurned || he.Subject.Kind != TC2.Core.Foundation.EntityKind.Household) continue; burnt++; byV.TryGetValue(he.Settlement, out int nb); byV[he.Settlement] = nb + 1; }
                        int most = 0; foreach (var kv in byV) if (kv.Value > most) most = kv.Value;
                        Console.WriteLine("burnt: homes burnt in the burning-in " + burnt + " in " + byV.Count + " villages (the most in one village " + most + ")");
                    }
                    var szt = TC2.Core.Diagnostics.SizeTally.Sums;
                    if (szt != null)   // (villages deserted, measured first: how villages of each size live)
                    {
                        Console.WriteLine("sizes: village size   village-years  mean people  births  deaths  hunger  sickness  wounds  (a thousand person-years)  hh out  hh in  parties (a thousand village-years: hh moving, parties)  famines /100 vy  ration  land/need  store y  short years");
                        for (int k = 0; k < TC2.Core.Diagnostics.SizeTally.Classes; k++)
                        {
                            int o = k * TC2.Core.Diagnostics.SizeTally.Stride;
                            double vy = szt[o], py = szt[o + 1];
                            if (vy <= 0) continue;
                            double kpy = py / 1000.0, kvy = vy / 1000.0;
                            Console.WriteLine("sizes:   " + TC2.Core.Diagnostics.SizeTally.Names[k].PadRight(12) + F(vy, "0").PadLeft(14) + F(py / vy, "0").PadLeft(13) + F(szt[o + 6] / kpy, "0.0").PadLeft(8) + F(szt[o + 7] / kpy, "0.0").PadLeft(8) +
                                              F(szt[o + 8] / kpy, "0.0").PadLeft(8) + F(szt[o + 9] / kpy, "0.0").PadLeft(10) + F(szt[o + 10] / kpy, "0.0").PadLeft(8) + "".PadLeft(26) + F(szt[o + 13] / kvy, "0").PadLeft(8) + F(szt[o + 14] / kvy, "0").PadLeft(7) +
                                              F(szt[o + 15] / kvy, "0").PadLeft(9) + "".PadLeft(48) + F(100.0 * szt[o + 17] / vy, "0.0").PadLeft(10) + F(szt[o + 2] / vy, "0.00").PadLeft(8) + F(szt[o + 3] / vy, "0.00").PadLeft(11) + F(szt[o + 4] / vy, "0.00").PadLeft(9) + F(szt[o + 5] / vy, "0.00").PadLeft(13));
                        }
                        Console.WriteLine("sizes: villages abandoned " + szt[TC2.Core.Diagnostics.SizeTally.Abandoned]);
                    }
                    var mgt = TC2.Core.Places.Migration.MarginTally;
                    if (mgt != null)   // (C3.6g: daughter villages on the land between villages)
                    {
                        int daughters = 0, living = 0, peopleNow = 0;
                        for (int hi = 0; hi < w.History.Count; hi++)
                        {
                            var he = w.History.At(hi);
                            if (he.Kind != TC2.Core.History.HistoryKind.SettlementFounded || he.ArgA != 1 || !w.Settlements.Exists(he.Settlement)) continue;
                            daughters++;
                            ref var ds = ref w.Settlements.Get(he.Settlement);
                            if (ds.Inhabited) { living++; peopleNow += ds.Population; }
                        }
                        Console.WriteLine("margins: villages whose leavers found no village to join and looked to their own margins " + mgt[0] + ", found a place there " + mgt[1] + " (" + mgt[2] + " households, " + mgt[3] +
                                          " people set out); daughter villages founded " + daughters + ", living " + living + " (" + peopleNow + " people), abandoned " + (daughters - living));
                    }
                    var jt = TC2.Core.Places.Migration.JoinTally;
                    if (jt != null)   // (the ceiling measured: why the households that choose to leave find no village to join)
                        Console.WriteLine("join: households looking for a village to join " + jt[0] + " (their pressure at home " + F(jt[0] > 0 ? jt[8] / jt[0] : 0, "0.00") + " on average); candidates within marriage reach " + jt[1] +
                                          " - pressed no less than home " + jt[2] + ", no land to spare for them " + jt[3] + ", no way on foot " + jt[4] + ", fit " + jt[5] + "; found one " + jt[6] + ", none " + jt[7]);
                    var pt = TC2.Core.Society.Conflict.PeaceTally;
                    if (pt != null)   // (C10.10: the lord's peace)
                        Console.WriteLine("peace: claims within a realm before a common lord " + pt[0] + ", judged " + pt[1] + " (" + F(pt[2], "0") + " FU paid; not in full " + pt[5] + "); bands kept home " + pt[3] +
                                          " (weighed instead against the foe beyond the realm " + pt[6] + "); tributaries' wrongs within the realm not answered by their lord " + pt[4]);
                    var lapseTallyC109 = TC2.Core.Society.Conflict.LapseTally;
                    if (lapseTallyC109 != null)   // (C10.9: the lord's death and its bonds)
                        Console.WriteLine("lapses: deaths of the first at a lord's seat " + lapseTallyC109[0] + ", with bonds for peace " + lapseTallyC109[1] + "; bonds for peace lapsed " + lapseTallyC109[2] + ", bonds for bread kept " + lapseTallyC109[3]);
                    var ct = TC2.Core.Society.Conflict.CarryTally;
                    if (ct != null)   // (C10.6: the lord's tenth carried to its store)
                        Console.WriteLine("carried: set aside " + F(ct[3], "0") + " FU; loads " + ct[0] + " (lived " + ct[2] + "), " + F(ct[1], "0") + " FU (by cart " + F(ct[5], "0") + "), carriers' km " + F(ct[6], "0") +
                                          "; given back at a bond's end " + F(ct[4], "0") + " FU, a year uncarried " + F(ct[7], "0") + " FU; tenths not set aside (no way on foot to the lord) " + ct[8] + ", " + F(ct[9], "0") + " FU" +
                                          " (a way by sea between their landings " + ct[10] + ", " + F(ct[13], "0") + " FU; the tributary with plank boats " + ct[11] + ", the lord " + ct[12] + "); by sea " + ct[14] + " loads, " + F(ct[15], "0") + " FU" +
                                          " (fetched home in the lord's boats " + ct[16] + " loads, " + F(ct[17], "0") + " FU)");
                    var at = TC2.Core.Places.Trade.AcrossTally;
                    if (at != null)   // (C9.5 survey)
                        Console.WriteLine("across: between villages within a day's walk no way on foot joins - relief " + at[0] + " gifts, " + F(at[1], "0") + " FU; given back " + at[2] + ", " +
                                          F(at[3], "0") + " FU; bows for bread " + at[4] + "; traders dealing across it at once " + at[5] + " (COARSE), lived traders finding no road to their partner " + at[6]);
                    var xt = TC2.Core.Places.Boats.CrossTally;
                    if (xt != null)   // (C9.5b: boats to cross the water)
                        Console.WriteLine("cross: boats laid down to cross the water - dugouts " + xt[0] + ", of planks " + xt[1] + "; made " + xt[2] + "; crossings in dugouts lived " + xt[3] +
                                          ", COARSE trade " + xt[4] + " (" + F(xt[5], "0") + " kg)");
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
                var gt = TC2.Core.Places.SettlementSystem.GapTally;
                if (gt != null)
                {
                    double gn = 0; for (int i = 0; i < 48; i++) gn += gt[i];
                    var sbg = new System.Text.StringBuilder("hungry gap: hunger deaths " + F(gn, "0") + " by days to the harvest (0..47):");
                    for (int i = 0; i < 48; i++) sbg.Append(' ').Append(F(gt[i], "0"));
                    Console.WriteLine(sbg.ToString());
                    double q = Math.Max(1e-9, gn);
                    Console.WriteLine("  by the crop: after harvest " + F(gt[48] / q * 100, "0") + " %, sowing " + F(gt[49] / q * 100, "0") + " %, growing " + F(gt[50] / q * 100, "0") + " %, ripe " + F(gt[51] / q * 100, "0") + " %, harvested " + F(gt[52] / q * 100, "0") +
                                      " %;  by age: under 2 " + F(gt[53] / q * 100, "0") + " %, 2-11 " + F(gt[54] / q * 100, "0") + " %, 12-59 " + F(gt[55] / q * 100, "0") + " %, 60+ " + F(gt[56] / q * 100, "0") + " %");
                    var sht = TC2.Core.Places.SettlementSystem.SheafTally;
                    if (sht != null && sht[0] > 0) Console.WriteLine("  harvests " + F(sht[0], "0") + ", grain " + F(sht[1], "0") + " FU, of it in the store before the harvest's day " + F(sht[2] / Math.Max(1e-9, sht[1]) * 100, "0.0") + " %");
                    Console.WriteLine("  the store (days of its people's food): under 1 " + F(gt[57] / q * 100, "0") + " %, 1-5 " + F(gt[58] / q * 100, "0") + " %, 5-20 " + F(gt[59] / q * 100, "0") + " %, 20+ " + F(gt[60] / q * 100, "0") +
                                      " %;  hungry for: under 6 days " + F(gt[61] / q * 100, "0") + " %, 6-12 " + F(gt[62] / q * 100, "0") + " %, 12-24 " + F(gt[63] / q * 100, "0") + " %, 24+ " + F(gt[64] / q * 100, "0") +
                                      " %;  land bound " + F(gt[66] / q * 100, "0") + " %;  mean last harvest " + F(gt[67] / q, "0.00") + " years' bread, ration " + F(gt[68] / q, "0.00") + ", beasts a person " + F(gt[70] / q, "0.00"));
                }
                var lt = TC2.Core.Places.SettlementSystem.LandTally;
                if (lt != null)
                {
                    int K = TC2.Core.Places.SettlementSystem.LandTallyK;
                    for (int g = 0; g < 2; g++)
                    {
                        int o = g * K; double n = Math.Max(1e-9, lt[o]);
                        Console.WriteLine("land " + (g == 1 ? "bound" : "open ") + ": harvests " + F(lt[o], "0") + "  arable/need " + F(lt[o + 10] / Math.Max(1e-9, lt[o + 3]), "0.000") + "  fields/need " + F(lt[o + 2] / Math.Max(1e-9, lt[o + 3]), "0.000") +
                                          "  FU a ha " + F(lt[o + 1] / Math.Max(1e-9, lt[o + 2]), "0.0") + "  harvest/bread " + F(lt[o + 1] / Math.Max(1e-9, lt[o + 8]), "0.000") + "  grown " + F(lt[o + 4] / n, "0.000") + "  sown " + F(lt[o + 5] / n, "0.000") +
                                          "  brought in " + F(lt[o + 6] / n, "0.000") + "  burnt " + F(lt[o + 7] / n, "0.000") + "  beasts a field ha " + F(lt[o + 11] / Math.Max(1e-9, lt[o + 2]), "0.00") + "  their intake kg a field ha a day " + F(lt[o + 9] / Math.Max(1e-9, lt[o + 2]), "0.00") +
                                          "  worked harder " + F(lt[o + 15], "0") + " (extra planned " + F(lt[o + 12] / n, "0.000") + ", done " + F(lt[o + 13] / n, "0.000") + " of the season's hours; added " + F(lt[o + 14], "0") + " FU, " + F(lt[o + 14] / Math.Max(1e-9, lt[o + 1]) * 100.0, "0.0") + " % of the harvests)" +
                                          "  soil: tired " + F(lt[o + 16] / n, "0.000") + ", resting " + F(lt[o + 17] / n, "0.000") + " of the arable, a sown ha bears " + F(lt[o + 18] / n, "0.000") + " of a fresh crop, herds' grazing " + F(lt[o + 19] / n, "0.00") + " kg an arable ha a day");
                    }
                    var lh = TC2.Core.Agents.AgentSystem.LandHours;
                    string[] stn = { "after harvest", "sowing", "growing", "ripe", "harvested" };
                    int LK = TC2.Core.Agents.AgentSystem.LandK;
                    for (int g = 0; g < 2; g++)
                        for (int st = 0; st < 5; st++)
                        {
                            int o = (g * 5 + st) * LK; if (lh[o] <= 0) continue;
                            Console.WriteLine("  land hours " + (g == 1 ? "bound" : "open ") + " " + stn[st] + ": person-days " + F(lh[o], "0") + "  crops " + F(lh[o + 1] / lh[o], "0.000") + "  left after crops " + F(lh[o + 2] / lh[o], "0.000") +
                                              "  wild " + F(lh[o + 3] / lh[o], "0.000") + " h a day (" + F(lh[o + 4] / Math.Max(1e-9, lh[o + 3]), "0.000") + " FU an hour)");
                        }
                }
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
            var sty = TC2.Core.Places.Livestock.StockYears;
            if (sty != null)
                Console.WriteLine("stock-years: cattle " + F(sty[0], "0") + " goats " + F(sty[1], "0") + " pigs " + F(sty[2], "0") + " (head x years kept at the villages over the run)");
            var drc = TC2.Core.Places.Livestock.DiagReach;
            if (drc != null && drc[0] > 0)
                Console.WriteLine("herds with pigs: " + F(drc[0], "0") + " village-days, out far " + F(drc[1] / drc[0], "0.000") + " (for the grass alone " + F(drc[2] / drc[0], "0.000") + ", the mast alone " + F(drc[3] / drc[0], "0.000") +
                                  "); fed mast " + F(drc[4] / drc[0], "0.000") + ", grass " + F(drc[5] / drc[0], "0.000") + ", scraps " + F(drc[6] / drc[0], "0.000") + "; mast found " + F(drc[7] / drc[0], "0.0") + " of " + F(drc[8] / drc[0], "0.0") + " kg wanted a village-day; " + F(drc[9] / drc[0], "0.0") + " pigs a village-day");
            var dd = TC2.Core.Places.Livestock.DiagDoy;
            if (dd != null)
            {
                int K = TC2.Core.Places.Livestock.Kinds, C = TC2.Core.Places.Livestock.Causes, D = SimTime.DaysPerYear;
                Console.WriteLine("stock by the village's own day of the year, 4-day bins from day 0 (the sowing; growing 12, ripe 30, harvest 39):");
                for (int k = 0; k < K; k++)
                    for (int c = 0; c <= C; c++)
                    {
                        long tot = 0;
                        for (int d = 0; d < D; d++) tot += dd[(k * (C + 1) + c) * D + d];
                        if (tot == 0) continue;
                        var sb = new System.Text.StringBuilder("  " + TC2.Core.Places.Livestock.Laws[k].Name.PadRight(7) + (c == C ? "born" : TC2.Core.Places.Livestock.CauseNames[c]).PadRight(13) + tot.ToString().PadLeft(7) + " :");
                        for (int b = 0; b < D / 4; b++) { long n = 0; for (int d = b * 4; d < b * 4 + 4; d++) n += dd[(k * (C + 1) + c) * D + d]; sb.Append(n.ToString().PadLeft(6)); }
                        Console.WriteLine(sb.ToString());
                    }
            }
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
