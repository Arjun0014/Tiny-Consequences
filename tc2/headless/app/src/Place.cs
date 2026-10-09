using System;
using System.IO;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// The settlement page in text (C12.2): `place --load SAVE [--sid N | --pick big|iron|claims]` - the same page the HUD shows, from the
        /// world's state and its ledger.
        /// </summary>
        public static int PlacePage(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            if (a.Has("list")) { ListPlaces(w); return 0; }
            if (a.Has("standing")) { Standing(w); return 0; }
            if (a.Has("stores")) { Stores(w); return 0; }
            if (a.Has("haul")) { Haul(w); return 0; }
            if (a.Has("crossings")) { Crossings(w); return 0; }
            if (a.Has("overlaps")) { Overlaps(w, a.Int("sid", 0)); return 0; }
            if (a.Has("layout")) { Layout(w, a.Int("sid", 0) != 0 ? a.Int("sid", 0) : PickPlace(w, "big")); return 0; }
            if (a.Has("leaders"))
            {
                // (C10.2) each village: its households, its first household's following and grain (days of its food), how long it has held the place,
                // whether the village is a lord (and of how many) and the first household's house
                for (int lsid = w.Settlements.First(); lsid != 0; lsid = w.Settlements.Next(lsid))
                {
                    var s = w.Settlements.Get(lsid);
                    if (!s.Inhabited) continue;
                    int tribs = 0;
                    for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (w.Settlements.Get(o).Inhabited && w.Settlements.Get(o).Lord == lsid) tribs++;
                    string line = "  #" + lsid + " " + w.History.NameOf(EntityRef.Settlement(lsid)).PadRight(14) + " " + w.HouseholdsOf(lsid).Count.ToString().PadLeft(3) + " households" + (tribs > 0 ? ", lord of " + tribs : "") + (s.Lord != 0 ? ", pays tribute" : "");
                    if (s.HeadHh != 0 && w.Households.Exists(s.HeadHh))
                    {
                        int f = TC2.Core.Society.Leaders.Following(w, lsid, s.HeadHh, out int kin, out int owing, out int fed);
                        var hh = w.Households.Get(s.HeadHh);
                        double need = System.Math.Max(1e-9, w.SettlementSys.HouseholdNeed(s.HeadHh));
                        string form = hh.Building != 0 && w.Buildings.Exists(hh.Building) ? Architecture.Describe(w.Buildings.Get(hh.Building).Form, false) : "(no house)";
                        line += ": first the " + TC2.Core.History.HistoryText.Plural(w.History.NameOf(EntityRef.Household(s.HeadHh))) + ", following " + f + " (kin " + kin + ", owing " + owing + ", fed " + fed + "), grain " + (hh.Grain / need).ToString("0") + " d, held " + ((w.Now - s.HeadSince) / (double)SimTime.Year).ToString("0.0") + " y, means " + Architecture.Means(w, s.HeadHh).ToString("0.00") + ", " + form;
                    }
                    Console.WriteLine(line);
                }
                return 0;
            }
            if (a.Has("splits")) { Splits(w); return 0; }
            if (a.Has("marriage")) { Marriage(w); return 0; }
            if (a.Has("binding"))
            {
                // (C10.3 survey) each village: households and people; bound to its first household (the household itself and its following); the
                // largest following among the households not bound to it, counted over the unbound only (a rival faction), and that rival's kin/debt mix
                Console.WriteLine("sid,households,people,bound,rival,rival_hid,lord,tributary");
                for (int bsid = w.Settlements.First(); bsid != 0; bsid = w.Settlements.Next(bsid))
                {
                    var s = w.Settlements.Get(bsid);
                    if (!s.Inhabited || w.HouseholdsOf(bsid).Count == 0) continue;
                    var all = TC2.Core.Society.Leaders.All(w, bsid);
                    var bound = new System.Collections.Generic.HashSet<int>();
                    if (s.HeadHh != 0) { bound.Add(s.HeadHh); if (all.TryGetValue(s.HeadHh, out var fs)) foreach (var h in fs) bound.Add(h); }
                    int rival = 0, rhid = 0;
                    var hl = w.HouseholdsOf(bsid);
                    for (int i = 0; i < hl.Count; i++)
                    {
                        if (bound.Contains(hl[i]) || !all.TryGetValue(hl[i], out var f2)) continue;
                        int n = 1; foreach (var h in f2) if (!bound.Contains(h)) n++;
                        if (n > rival) { rival = n; rhid = hl[i]; }
                    }
                    int lordOf = 0; for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (w.Settlements.Get(o).Inhabited && w.Settlements.Get(o).Lord == bsid) lordOf++;
                    Console.WriteLine(bsid + "," + hl.Count + "," + s.Population + "," + bound.Count + "," + rival + "," + rhid + "," + lordOf + "," + (s.Lord != 0 ? 1 : 0));
                }
                return 0;
            }
            if (a.Has("debts"))
            {
                // (C3.3) every debt: debtor and creditor, their villages, whether they still stand, how much and since when
                for (int i = 0; i < w.Holdings.DebtCount; i++)
                {
                    var d = w.Holdings.DebtAt(i);
                    string H(int h) { if (!w.Households.Exists(h)) return "#" + h + " (gone)"; var x = w.Households.Get(h); return "#" + h + " v" + x.Settlement + (x.Dissolved != 0 ? " dissolved" : "") + (x.Party != 0 ? " on the move" : "") + " n" + w.Members(h).Count + " grain " + x.Grain.ToString("0"); }
                    Console.WriteLine("  " + H(d.Debtor) + " owes " + H(d.Creditor) + ": " + d.Fu.ToString("0") + " FU since year " + (d.Since / (double)SimTime.Year).ToString("0.0"));
                }
                return 0;
            }
            if (a.Has("households")) { ListHouseholds(w, a.Int("sid", 0) != 0 ? a.Int("sid", 0) : PickPlace(w, "big")); return 0; }
            int sid = a.Int("sid", 0);
            if (sid == 0) sid = PickPlace(w, a.Get("pick", "big"));
            if (sid == 0) { Console.WriteLine("no place to pick"); return 1; }
            Console.Write(Gazetteer.All(w, sid));
            return 0;
        }

        /// <summary>
        /// `place --load SAVE --list` (D2-114, picking villages to look at): each inhabited place - its people, age, wall, lord, tradition, and its
        /// standing houses by plan (small/cottage/long/two/hall/row), roof cover and walls, and its works.
        /// </summary>
        static void ListPlaces(World w)
        {
            var order = new System.Collections.Generic.List<int>();
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) if (w.Settlements.Get(id).Inhabited) order.Add(id);
            order.Sort((x, y) => { int c = w.Settlements.Get(y).Population.CompareTo(w.Settlements.Get(x).Population); return c != 0 ? c : x.CompareTo(y); });
            int year = (int)(w.Now / SimTime.Year);
            foreach (int id in order)
            {
                var s = w.Settlements.Get(id);
                int[] plan = new int[6], cover = new int[5], walls = new int[5]; int huts = 0, works = 0, lords = 0;
                var kinds = new System.Text.StringBuilder();
                var bl = w.BuildingsOf(id);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) continue;
                    if (!Structures.IsHome(b.Kind)) { works++; kinds.Append(kinds.Length > 0 ? "," : "").Append(Structures.Name(b.Kind)).Append(b.Use > 0 ? "(" + b.Use.ToString("0") + " h)" : ""); continue; }
                    if (b.Kind < Construction.House) { huts++; continue; }
                    if (!Architecture.Has(b.Form)) continue;
                    plan[Architecture.PlanOf(b.Form)]++; cover[Architecture.CoverOf(b.Form)]++; walls[Architecture.WallsOf(b.Form)]++;
                }
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (w.Settlements.Get(o).Inhabited && w.Settlements.Get(o).Lord == id) lords++;
                // the households' means (Architecture.Means): under 1, 1-1.5, 1.5-3, 3 and over; with cattle; their sizes
                int[] mb = new int[4]; int cattle = 0, big = 0;
                var hl = w.HouseholdsOf(id);
                for (int i = 0; i < hl.Count; i++)
                {
                    double m = Architecture.Means(w, hl[i]);
                    mb[m < 1.0 ? 0 : m < 1.5 ? 1 : m < 3.0 ? 2 : 3]++;
                    var bs = w.BeastsOf(hl[i]); bool c = false;
                    for (int j = 0; j < bs.Count; j++) { var be = w.Beasts.Get(bs[j]); if (be.Alive && be.Kind == Livestock.Cattle) c = true; }
                    if (c) cattle++;
                    if (w.Members(hl[i]).Count >= 8) big++;
                }
                string J(int[] v) => string.Join("/", v);
                double gear = 0; for (int i = 0; i < hl.Count; i++) gear += w.Households.Get(hl[i]).Gear;
                Console.WriteLine("#" + id + " " + w.History.NameOf(EntityRef.Settlement(id)) + ": " + s.Population + " people, " + (year - (int)(s.Founded / SimTime.Year)) + " y old, wall " + s.Wall +
                                  ", food " + (s.Granary / Math.Max(1.0, s.Population * w.Config.FoodPerAdultDay * 0.8)).ToString("0") + " d, fibre " + (s.Fibre / Math.Max(1, s.Population)).ToString("0.0") + " kg/hd, hides " + (s.Hides / Math.Max(1, s.Population)).ToString("0.0") + "/hd, goods " + (gear / Math.Max(1, s.Population)).ToString("0.00") + "/hd, loom " + s.BestLoom.ToString("0.00") +
                                  (TC2.Core.Places.Weaving.Shed(w, id) != 0 ? (s.Sheds > 1 ? ", " + s.Sheds + " WEAVER'S SHEDS" : ", WEAVER'S SHED") : "") + ", wares " + s.Wares.ToString("0") + " (worn " + (100.0 * s.WaresHeld / Math.Max(1.0, TC2.Core.Places.Goods.Target * s.Population)).ToString("0") + "%, sold " + s.WaresSold.ToString("0") + ")" +
                                  (Pottery.Kiln(w, id) != 0 ? ", KILN" : "") + (s.Smithy != 0 ? (s.Smithies > 1 ? ", " + s.Smithies + " SMITHIES" : ", SMITHY") : "") + ", jars seal " + (100 * Pottery.Sealed(in s)).ToString("0") + "% (ware " + s.Pots.ToString("0") + " kg, sold " + s.PotsSold.ToString("0") + ")" +
                                  (s.Lord != 0 ? ", tributary of #" + s.Lord : "") + (lords > 0 ? ", lord of " + lords : "") + ", traders " + s.TradeIn.ToString("0.0") + (s.Market != 0 ? " market" : "") + ", line " + Architecture.Line(s.Tradition) + " roof " + Architecture.RoofName(Architecture.TraditionRoof(s.Tradition)) +
                                  " shade " + Architecture.TraditionPalette(s.Tradition) + "; huts " + huts + ", plans " + J(plan) + ", covers " + J(cover) + ", walls " + J(walls) + "; works " + works + " (" + kinds + ")" + "; households " + hl.Count + " means <1/1-1.5/1.5-3/3+ " + J(mb) + ", with cattle " + cattle + ", of 8+ " + big);
            }
        }

        /// <summary>
        /// `place --load SAVE --stores` (C3.2 survey): each inhabited place's store - grain in FU and in years of its need, its granaries' and barns'
        /// capacity, its last harvest in years of need, what rots a year at today's rate - its pottery (best, known, clay within reach, potters,
        /// households cooking in pots) and its wood (in store, a day's burning and the cooking's share, the woodlot's distance); then the world's sums.
        /// </summary>
        static void Stores(World w)
        {
            var order = new System.Collections.Generic.List<int>();
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) if (w.Settlements.Get(id).Inhabited) order.Add(id);
            order.Sort((x, y) => { int c = w.Settlements.Get(y).Population.CompareTo(w.Settlements.Get(x).Population); return c != 0 ? c : x.CompareTo(y); });
            double tStore = 0, tCap = 0, tRot = 0, tHarv = 0, tNeed = 0, tCook = 0, tFuel = 0; int nKnow = 0, nClay = 0, nKnowClay = 0, nPeople = 0, potters = 0, potHh = 0, allHh = 0, pClay = 0, pKnow = 0;
            int[] yb = new int[5];
            foreach (int id in order)
            {
                var s = w.Settlements.Get(id);
                var hl = w.HouseholdsOf(id);
                double need = 0, cook = 0; int hhPots = 0;
                for (int i = 0; i < hl.Count; i++)
                {
                    double n = w.SettlementSys.HouseholdNeed(hl[i]); need += n;
                    var mem = w.Members(hl[i]); double best = 0;
                    for (int j = 0; j < mem.Count; j++) { double k = w.People.Get(mem[j]).KPottery; if (k > best) best = k; }
                    double cf = Pottery.CookFactor(w, hl[i]);
                    if (cf < 0.9) hhPots++;
                    cook += n * Hearth.CookFuel * cf;
                }
                int pots = 0; var res = w.Residents(id);
                for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.KPottery >= 0.5f) pots++; }
                double cap = Structures.StoreCapacity(in s), year = Math.Max(1e-9, need * SimTime.DaysPerYear);
                double r = 0.12 * Pottery.RotFactor(in s);
                double rotYr = Math.Min(s.Granary, cap) * r + Math.Max(0.0, s.Granary - cap) * Math.Min(0.9 * SimTime.DaysPerYear, r * Structures.HeapRot);
                bool clay = w.Learning.Clay(s.Cell), know = (s.TechKnown & (1 << TC2.Core.Knowledge.Tech.Pottery)) != 0;
                double sy = s.Granary / year; yb[sy < 0.25 ? 0 : sy < 0.5 ? 1 : sy < 1 ? 2 : sy < 2 ? 3 : 4]++;
                Console.WriteLine("#" + id + " " + w.History.NameOf(EntityRef.Settlement(id)) + ": " + s.Population + " people; store " + s.Granary.ToString("0") + " FU = " + sy.ToString("0.00") + " y of need (cap " + cap.ToString("0") + " in " + s.Granaries + "g/" + s.Barns + "b)" +
                                  ", harvest " + (s.LastHarvest / year).ToString("0.00") + " y, rots " + rotYr.ToString("0") + " FU/y (" + (100 * rotYr / Math.Max(1.0, s.LastHarvest)).ToString("0") + " % of the harvest); pottery " + s.BestPottery.ToString("0.00") + (know ? " known" : "") + (clay ? " CLAY" : " no clay") +
                                  ", potters " + pots + (Pottery.Kiln(w, id) != 0 ? ", KILN" : "") + ", jars " + s.Jars.ToString("0") + " kg (sealed " + (100 * Pottery.Sealed(in s)).ToString("0") + "%), ware " + s.Pots.ToString("0") + " kg, sold " + s.PotsSold.ToString("0") + ", households cooking in pots " + hhPots + "/" + hl.Count + "; wood " + s.Woodstore.ToString("0") + " (" + (s.Woodstore / Math.Max(0.01, s.FuelDay)).ToString("0") + " d), burns " + s.FuelDay.ToString("0.0") + "/d, cooking " + (100 * cook / Math.Max(0.01, s.FuelDay)).ToString("0") + " %, woodlot " + s.WoodDist.ToString("0") + " m (far " + s.WoodFar + "), ration " + s.Ration.ToString("0.00"));
                tStore += s.Granary; tCap += cap; tRot += rotYr; tHarv += s.LastHarvest; tNeed += year; tCook += cook; tFuel += s.FuelDay; nPeople += s.Population;
                if (know) { nKnow++; pKnow += s.Population; }
                if (clay) { nClay++; pClay += s.Population; }
                if (know && clay) nKnowClay++;
                potters += pots; potHh += hhPots; allHh += hl.Count;
            }
            // clay by the ground's shape (C3.2 survey): the largest drainage within the clay's reach, a depression (a lake bed) within it - against
            // today's test (a stream or lake by the day's flows)
            var near = new System.Collections.Generic.List<int>();
            int[] th = { 10, 50, 200, 1000, 5000 }; int[] byTh = new int[th.Length]; int basin = 0, daily = 0, either200 = 0;
            var D = w.Drainage; var Pl = w.Planet;
            var line = new System.Text.StringBuilder("CLAY max drainage ha (basin) by village:");
            foreach (int id in order)
            {
                var s = w.Settlements.Get(id);
                w.Hazards.CellsWithin(w, s.Cell, TC2.Core.Knowledge.Learning.ClayReach, near);
                double maxA = 0; bool bas = false;
                for (int i = 0; i < near.Count; i++)
                {
                    int c = near[i]; if (!Pl.IsLand(c)) continue;
                    if (D != null && D.BasinOf[c] < 0 && D.AreaHa[c] > maxA) maxA = D.AreaHa[c];
                    if (Pl.InBasin != null && Pl.InBasin[c] != 0) bas = true;
                }
                for (int k = 0; k < th.Length; k++) if (maxA >= th[k]) byTh[k]++;
                if (bas) basin++; if (w.Learning.Clay(s.Cell)) daily++; if (bas || maxA >= 200) either200++;
                line.Append(" " + maxA.ToString("0") + (bas ? "b" : ""));
            }
            Console.WriteLine(line.ToString());
            Console.WriteLine("CLAY villages with a reach draining >= 10/50/200/1000/5000 ha within reach " + string.Join("/", byTh) + ", a depression " + basin + ", either (200 ha) " + either200 + ", today's test " + daily + " of " + order.Count);
            Console.WriteLine("WORLD " + order.Count + " villages, " + nPeople + " people: store " + tStore.ToString("0") + " FU = " + (tStore / Math.Max(1, tNeed)).ToString("0.00") + " y of need (capacity " + tCap.ToString("0") + "); villages by years in store <0.25/0.5/1/2/2+ " + string.Join("/", yb) +
                              "; last harvests " + (tHarv / Math.Max(1, tNeed)).ToString("0.00") + " y; rots " + tRot.ToString("0") + " FU/y (" + (100 * tRot / Math.Max(1, tHarv)).ToString("0.0") + " % of the harvests); pottery known in " + nKnow + " (" + pKnow + " people), clay at " + nClay + " (" + pClay + " people), both " + nKnowClay +
                              "; potters " + potters + "; households cooking in pots " + potHh + "/" + allHh + "; cooking " + (100 * tCook / Math.Max(0.01, tFuel)).ToString("0") + " % of the wood burnt");
        }

        /// <summary>`place --load SAVE --haul` (C4.2 survey): each village's hauling - its woodlot's and quarry's distance, its households with a grown
        /// ox and with a woodworker - and the world's sums, with the ways between villages by state.</summary>
        static void Haul(World w)
        {
            int hh = 0, ox = 0, oxWood = 0, villages = 0, nq = 0; double wood = 0, quarry = 0;
            var ways = new System.Collections.Generic.List<TC2.Core.Agents.Way>();
            w.Ways.Collect(w.Now, ways);
            int[] byState = new int[4];
            foreach (var wy in ways) byState[TC2.Core.Agents.Ways.StateOf(w.Ways.UseOf(w.Now, wy.A, wy.B))]++;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                var s = w.Settlements.Get(id);
                if (!s.Inhabited) continue;
                villages++;
                var hl = w.HouseholdsOf(id); int o = 0, ow = 0;
                for (int i = 0; i < hl.Count; i++)
                {
                    if (!w.Stock.HasOx(hl[i])) continue;
                    o++;
                    var mem = w.Members(hl[i]); bool wd = false;
                    for (int j = 0; j < mem.Count; j++) { var p = w.People.Get(mem[j]); if (p.Alive && p.SkWood >= 0.4f && p.AgeYears(w.Now) >= 16) wd = true; }
                    if (wd) ow++;
                }
                hh += hl.Count; ox += o; oxWood += ow; wood += s.WoodDist; if (s.Quarry != 0) { quarry += s.StoneDist; nq++; }
                Console.WriteLine("#" + id + " " + w.History.NameOf(EntityRef.Settlement(id)) + ": " + s.Population + " people, " + hl.Count + " households, " + o + " with a grown ox (" + ow + " with a woodworker too); woodlot " + s.WoodDist.ToString("0") + " m, quarry " + (s.Quarry != 0 ? s.StoneDist.ToString("0") + " m" : "none") + ", stone " + s.Stone.ToString("0") + " / " + s.StoneWant.ToString("0") + " loads");
            }
            int lots = 0, wetLots = 0, seaLots = 0;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                var s = w.Settlements.Get(id);
                if (!s.Inhabited || TC2.Core.Places.Woods.Lot(in s) < 0) continue;
                var c0 = w.Planet.Grid.Center[TC2.Core.Places.Woods.Lot(in s)];
                lots++;
                if (!TC2.Core.Planet.Shore.Dry(w.Planet, c0)) wetLots++;
                if (Boats.AtSea(w.Planet, c0)) seaLots++;
            }
            Console.WriteLine("woodlots " + lots + ": centre not on dry ground " + wetLots + " (at the sea's surface " + seaLots + ")");
            Console.WriteLine("WORLD " + villages + " villages, " + hh + " households, " + ox + " with a grown ox (" + oxWood + " with a woodworker too); woodlots " + (wood / Math.Max(1, villages)).ToString("0") + " m out on average, quarries " + (quarry / Math.Max(1, nq)).ToString("0") + " m (" + nq + "); ways none/track/path/road " + string.Join("/", byState));
        }

        /// <summary>`place --load SAVE --marriage` (household formation): the living women by age - the share who ever married (a Marriage event of
        /// theirs in the ledger, kept while they live), their age at first marriage; the marriages of the last 50 years by the days after their village's
        /// harvest; and the women 18-36 unmarried now.</summary>
        static void Marriage(World w)
        {
            var H = w.History;
            int[] women = new int[6], wed = new int[6];   // ages 18-21, 22-25, 26-29, 30-36, 37-44, 45-60
            double ageSum = 0; int ageN = 0;
            int[] season = new int[4];   // marriages by days after the harvest: 0-11, 12-23, 24-35, 36-47
            int unwed1836 = 0, n1836 = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || !p.IsFemale) continue;
                double age = p.AgeYears(w.Now);
                if (age < 18 || age >= 60) continue;
                int band = age < 22 ? 0 : age < 26 ? 1 : age < 30 ? 2 : age < 37 ? 3 : age < 45 ? 4 : 5;
                women[band]++;
                long first = long.MaxValue;
                var evs = H.EventsOf(EntityRef.Person(id));
                for (int i = 0; evs != null && i < evs.Count; i++)
                {
                    if (!H.TryGet(evs[i], out var e) || e.Kind != HistoryKind.Marriage || e.Subject.Id != id) continue;
                    if (e.Time < first) first = e.Time;
                    if (w.Now - e.Time <= 50 * SimTime.Year && w.Settlements.Exists(e.Settlement))
                    {
                        var s = w.Settlements.Get(e.Settlement);
                        int doy = SimTime.DayOfYear(e.Time + TC2.Core.Planet.PlanetState.LocalOffset(s.Anchor));
                        if (s.Anchor.Y < 0) doy = (doy + 24) % SimTime.DaysPerYear;
                        int after = ((doy - 39) % SimTime.DaysPerYear + SimTime.DaysPerYear) % SimTime.DaysPerYear;   // (the harvest is day 39)
                        season[Math.Min(3, after / 12)]++;
                    }
                }
                if (first != long.MaxValue)
                {
                    wed[band]++;
                    if (w.Now - first <= 50 * SimTime.Year) { ageSum += (first - p.Born) / (double)SimTime.Year; ageN++; }
                }
                if (age < 37) { n1836++; if (p.Partner == 0 && first == long.MaxValue) unwed1836++; }
            }
            string[] names = { "18-21", "22-25", "26-29", "30-36", "37-44", "45-59" };
            var sb = new System.Text.StringBuilder("marriage: women ever married by age");
            for (int b = 0; b < 6; b++) sb.Append(' ').Append(names[b]).Append(' ').Append(women[b] > 0 ? (100.0 * wed[b] / women[b]).ToString("0") : "-").Append(" % (").Append(women[b]).Append(')');
            sb.Append("; age at first marriage (living women wed in the last 50 years) ").Append(ageN > 0 ? (ageSum / ageN).ToString("0.0") : "-").Append(" (").Append(ageN).Append(')');
            int ns = season[0] + season[1] + season[2] + season[3];
            sb.Append("; their marriages by days after the harvest 0-11/12-23/24-35/36-47 ");
            for (int k = 0; k < 4; k++) sb.Append(k > 0 ? "/" : "").Append(ns > 0 ? (100.0 * season[k] / ns).ToString("0") : "-");
            sb.Append(" %; women 18-36 never married ").Append(unwed1836).Append(" of ").Append(n1836);
            Console.WriteLine(sb.ToString());
        }

        /// <summary>`place --load SAVE --splits` (C10.3): each village split at a contested succession (from the ledger) - when, from where, the faction's
        /// households and people, the village it founded and both villages now; then every founding in the ledger by what sent its party (a split or
        /// not) and how many of those villages still stand and how large they are.</summary>
        static void Splits(World w)
        {
            var H = w.History;
            var setOutBySplit = new System.Collections.Generic.Dictionary<long, long>();   // a split party's setting out -> the split
            var foundedBy = new System.Collections.Generic.Dictionary<long, int>();       // a split -> the village it founded
            var splits = new System.Collections.Generic.List<HistoryEvent>();
            int[] n = new int[2], alive = new int[2]; long[] pop = new long[2];
            for (int i = 0; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Kind == HistoryKind.VillageSplit) splits.Add(e);
                else if (e.Kind == HistoryKind.PartySetOut && e.Cause1 != 0 && H.TryGet(e.Cause1, out var c) && c.Kind == HistoryKind.VillageSplit) setOutBySplit[e.Id] = c.Id;
                else if (e.Kind == HistoryKind.SettlementFounded && e.Cause1 != 0 && !e.Object.IsNone)   // (a party's founding: the first villages have no origin)
                {
                    int sid = e.Settlement;
                    bool bySplit = setOutBySplit.TryGetValue(e.Cause1, out long sp);
                    if (bySplit) foundedBy[sp] = sid;
                    int k = bySplit ? 1 : 0;
                    n[k]++;
                    if (w.Settlements.Exists(sid) && w.Settlements.Get(sid).Inhabited) { alive[k]++; pop[k] += w.Settlements.Get(sid).Population; }
                }
            }
            string V(int sid)
            {
                if (sid == 0 || !w.Settlements.Exists(sid)) return "-";
                var s = w.Settlements.Get(sid);
                return "#" + sid + " " + H.NameOf(EntityRef.Settlement(sid)) + (s.Inhabited ? " " + s.Population + " people now" : " abandoned year " + (s.Abandoned / (double)SimTime.Year).ToString("0"));
            }
            for (int i = 0; i < splits.Count; i++)
            {
                var e = splits[i];
                foundedBy.TryGetValue(e.Id, out int to);
                Console.WriteLine("  year " + (e.Time / (double)SimTime.Year).ToString("0.0") + " from " + V(e.Settlement) + ": " + e.ArgA + " households, " + e.ArgB + " people -> " + (to != 0 ? V(to) : "(no founding kept)"));
            }
            // raids between villages a split made kin (the village left and the village founded, or two founded from one) against all raids
            var mother = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < splits.Count; i++) if (foundedBy.TryGetValue(splits[i].Id, out int to)) mother[to] = splits[i].Settlement;
            int raids = 0, kinRaids = 0;
            for (int i = 0; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Kind != HistoryKind.Raid) continue;
                raids++;
                int a = e.Subject.Id, b = e.Object.Id;
                mother.TryGetValue(a, out int ma); mother.TryGetValue(b, out int mb);
                if (ma == b || mb == a || (ma != 0 && ma == mb)) { kinRaids++; Console.WriteLine("  raid year " + (e.Time / (double)SimTime.Year).ToString("0.0") + ": " + V(a) + " on " + V(b) + " (split kin)"); }
            }
            Console.WriteLine("raids " + raids + ", between villages a split made kin " + kinRaids);
            Console.WriteLine("splits " + splits.Count + "; foundings in the ledger: by a split " + n[1] + " (standing " + alive[1] + ", " + (pop[1] / (double)Math.Max(1, alive[1])).ToString("0") + " people a village), others " + n[0] + " (standing " + alive[0] + ", " + (pop[0] / (double)Math.Max(1, alive[0])).ToString("0") + " people a village)");
        }

        /// <summary>`place --load SAVE --crossings` (C4.3 survey): each way between villages worn to a track or more - the streams and rivers its route
        /// crosses (runs of cells whose smoothed flow makes a stream, `PlanetState.IsStream`), the largest flow crossed and the drainage above it, by the
        /// way's state; and the world's largest flows.</summary>
        static void Crossings(World w)
        {
            var P = w.Planet;
            var ways = new System.Collections.Generic.List<TC2.Core.Agents.Way>();
            w.Ways.Collect(w.Now, ways);
            // classes of the largest flow crossed (m3 a day): none, < 6,000 (a brook), < 30,000, < 150,000, more
            double[] bounds = { 6000, 30000, 150000 };
            int[,] byState = new int[4, 5]; double[,] useBy = new double[4, 5];
            int crossings = 0;
            foreach (var wy in ways)
            {
                byte st = TC2.Core.Agents.Ways.StateOf(w.Ways.UseOf(w.Now, wy.A, wy.B));
                if (st == 0) continue;
                var r = w.Routes.Get(w, wy.A, wy.B);
                if (r == null) continue;
                double maxQ = 0, maxHa = 0; int runs = 0; bool inRun = false;
                for (int k = 0; k < r.Cell.Length; k++)
                {
                    int c = r.Cell[k];
                    bool water = c >= 0 && P.IsStream(c);
                    if (water && !inRun) runs++;
                    inRun = water;
                    if (water && P.RiverFlow[c] > maxQ) { maxQ = P.RiverFlow[c]; maxHa = w.Drainage.AreaHa[c]; }
                }
                int cls = runs == 0 ? 0 : maxQ < bounds[0] ? 1 : maxQ < bounds[1] ? 2 : maxQ < bounds[2] ? 3 : 4;
                byState[st, cls]++; useBy[st, cls] += w.Ways.UseOf(w.Now, wy.A, wy.B);
                crossings += runs;
                if (st >= 2 && cls >= 2)
                    Console.WriteLine(w.History.NameOf(EntityRef.Settlement(wy.A)) + " - " + w.History.NameOf(EntityRef.Settlement(wy.B)) + ": " + (st == 3 ? "road" : "path") + " " + r.Length.ToString("0") + " m, use " + w.Ways.UseOf(w.Now, wy.A, wy.B).ToString("0") +
                                      "; crosses " + runs + " streams, the largest " + maxQ.ToString("0") + " m3/day (" + (maxQ / 86400.0).ToString("0.00") + " m3/s), draining " + maxHa.ToString("0") + " ha");
            }
            string[] cn = { "no stream", "brooks < 6k", "6k-30k", "30k-150k", "150k+" };
            string[] sn = { "-", "tracks", "paths", "roads" };
            for (int st = 1; st < 4; st++)
            {
                var line = new System.Text.StringBuilder(sn[st] + ":");
                for (int c = 0; c < 5; c++) line.Append("  " + cn[c] + " " + byState[st, c] + " (use " + useBy[st, c].ToString("0") + ")");
                Console.WriteLine(line.ToString());
            }
            // the world's flows: how many land cells carry each class
            int[] cells = new int[5]; double top = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsStream(c)) continue;
                double q = P.RiverFlow[c];
                cells[q < bounds[0] ? 1 : q < bounds[1] ? 2 : q < bounds[2] ? 3 : 4]++;
                if (q > top) top = q;
            }
            Console.WriteLine("WORLD stream cells: brooks " + cells[1] + ", 6k-30k " + cells[2] + ", 30k-150k " + cells[3] + ", 150k+ " + cells[4] + "; the largest flow " + top.ToString("0") + " m3/day (" + (top / 86400.0).ToString("0.0") + " m3/s); stream crossings on the worn ways " + crossings);
        }

        /// <summary>`place --load SAVE --layout [--sid N]` (C13.1): a village's plan for plotting - its square, ring, streets, and every standing
        /// building (kind, x, y, plan, lived in) and camp, in metres of its local frame.</summary>
        static void Layout(World w, int sid)
        {
            var s = w.Settlements.Get(sid);
            Console.WriteLine("V " + sid + " " + w.History.NameOf(EntityRef.Settlement(sid)) + " " + s.Population + " " + s.SquareX.ToString("0.0") + " " + s.SquareY.ToString("0.0") + " " + s.Wall + " " + s.WallR.ToString("0.0") + " " + (int)((w.Now - s.Founded) / SimTime.Year)
                              + " " + Footprints.Green(in s).ToString("0.0") + " " + s.CoreR.ToString("0.0") + " " + s.TradeIn.ToString("0.0") + " " + s.OldR.ToString("0.0"));
            for (int i = 0; i < s.StreetN; i++) Console.WriteLine("S " + s.StreetAt(i).ToString("0.0000"));
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                var b = w.Buildings.Get(bl[i]);
                if (Homes.Fallen(in b, w.Now)) continue;
                // B kind x y plan lived face, then each of its rectangles (homes: cx cz hx hz in its frame) or its disc's radius (works)
                bool home = Structures.IsHome(b.Kind);
                int plan = home ? Footprints.PlanOf(in b) : -1;
                var line = new System.Text.StringBuilder();
                double face = Footprints.FaceOf(in s, in b);
                bool open = !home || Footprints.Open(in s, b.X, b.Y, face, plan) && !Footprints.Wet(w, in s, b.X, b.Y, face, plan);
                line.Append("B " + b.Kind + " " + b.X.ToString("0.00") + " " + b.Y.ToString("0.00") + " " + (Architecture.Has(b.Form) ? Architecture.PlanOf(b.Form) : -1) + " " + (b.Household != 0 ? 1 : 0) + " " + face.ToString("0.0000") + " " + (open ? 0 : 1));
                if (home)
                    for (int r = 0; r < Footprints.Count(plan); r++)
                    {
                        Footprints.Rect(plan, r, out double cx, out double cz, out double hx, out double hz);
                        line.Append(" " + cx.ToString("0.00") + " " + cz.ToString("0.00") + " " + hx.ToString("0.00") + " " + hz.ToString("0.00"));
                    }
                else line.Append(" R " + Footprints.WorkR(b.Kind).ToString("0.00"));
                Console.WriteLine(line.ToString());
            }
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { var h = w.Households.Get(hl[i]); if (h.Building == 0) Console.WriteLine("C " + h.HomeX.ToString("0.0") + " " + h.HomeY.ToString("0.0")); }
        }

        /// <summary>`place --load SAVE --overlaps [--sid N]` (C13.3): each inhabited village's ground - buildings whose ground overlaps (home and home,
        /// home and work, work and work) and homes in a street, the green or astride the ring - and the world's sums.</summary>
        static void Overlaps(World w, int only)
        {
            int th = 0, tw = 0, tk = 0, to = 0, homes = 0;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                var s = w.Settlements.Get(id);
                if (!s.Inhabited || (only != 0 && id != only)) continue;
                Footprints.Audit(w, id, out int hh, out int hw, out int ww, out int open);
                int n = 0; var bl = w.BuildingsOf(id);
                for (int i = 0; i < bl.Count; i++) { var b = w.Buildings.Get(bl[i]); if (Structures.IsHome(b.Kind) && !Homes.Fallen(in b, w.Now)) n++; }
                th += hh; tw += hw; tk += ww; to += open; homes += n;
                if (hh + hw + ww + open > 0 || only != 0)
                    Console.WriteLine("#" + id + " " + w.History.NameOf(EntityRef.Settlement(id)) + ": " + s.Population + " people, " + n + " homes; overlapping home/home " + hh + ", home/work " + hw + ", work/work " + ww + "; homes in a street, the green or a ring " + open);
            }
            Console.WriteLine("overlaps: homes " + homes + "; home/home " + th + ", home/work " + tw + ", work/work " + tk + "; homes in the open " + to);
            if (only == 0) return;
            // (one village: each home in the open and why - a street or lane, the green or market place, a ring - and each pair that overlaps)
            var s1 = w.Settlements.Get(only);
            var bl1 = w.BuildingsOf(only);
            Console.WriteLine("  ring: wall " + s1.Wall + " at " + s1.WallR.ToString("0.0") + " m, old " + s1.OldR.ToString("0.0") + " m; one laid now would stand at " + w.Conflict.WallRadius(only).ToString("0.0") + " m");
            for (int i = 0; i < bl1.Count; i++)
            {
                var b = w.Buildings.Get(bl1[i]);
                if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) continue;
                bool home = Structures.IsHome(b.Kind);
                double f = Footprints.FaceOf(in s1, in b);
                double dx = b.X - s1.SquareX, dy = b.Y - s1.SquareY, d = Math.Sqrt(dx * dx + dy * dy);
                if (home && Footprints.Open(in s1, b.X, b.Y, f, Footprints.PlanOf(in b)) && Footprints.Wet(w, in s1, b.X, b.Y, f, Footprints.PlanOf(in b)))
                    Console.WriteLine("  open: b" + bl1[i] + " kind " + b.Kind + " plan " + Footprints.PlanOf(in b) + " at " + d.ToString("0.0") + " m, built y" + (b.Built / SimTime.Year) + " RIVER");
                if (home && !Footprints.Open(in s1, b.X, b.Y, f, Footprints.PlanOf(in b)))
                {
                    int plan = Footprints.PlanOf(in b);
                    bool green = false;
                    for (int r = 0; r < Footprints.Count(plan); r++) { Footprints.Rect(plan, r, out double cx, out double cz, out double hx, out double hz); green |= Footprints.BoxDisc(b.X, b.Y, f, cx, cz, hx, hz, s1.SquareX, s1.SquareY, Footprints.Green(in s1), 0.0); }
                    bool ring = s1.OldR > 0f && Math.Abs(d - s1.OldR) < Footprints.Reach(plan) + 1.5 || (s1.Wall == 1 || s1.Wall == 2) && Math.Abs(d - s1.WallR) < Footprints.Reach(plan) + 1.5;
                    Console.WriteLine("  open: b" + bl1[i] + " kind " + b.Kind + " plan " + plan + " at " + d.ToString("0.0") + " m, built y" + (b.Built / SimTime.Year) + ", faced " + b.Faced + (green ? " GREEN" : "") + (ring ? " RING" : "") + (!green && !ring ? " STREET" : ""));
                }
                if (home)
                    for (int j = i + 1; j < bl1.Count; j++)
                    {
                        var o = w.Buildings.Get(bl1[j]);
                        if (Homes.Fallen(in o, w.Now) || !Structures.IsHome(o.Kind)) continue;
                        int pa = Footprints.PlanOf(in b), pb = Footprints.PlanOf(in o);
                        double of = Footprints.FaceOf(in s1, in o);
                        bool hit = false;
                        for (int r = 0; r < Footprints.Count(pa); r++)
                            for (int q = 0; q < Footprints.Count(pb); q++)
                            {
                                Footprints.Rect(pa, r, out double cx, out double cz, out double hx, out double hz);
                                Footprints.Rect(pb, q, out double qx, out double qz, out double qhx, out double qhz);
                                hit |= Footprints.Boxes(b.X, b.Y, f, cx, cz, hx, hz, o.X, o.Y, of, qx, qz, qhx, qhz, 0.0);
                            }
                        if (hit) Console.WriteLine("  OVERLAP: b" + bl1[i] + " kind " + b.Kind + " plan " + pa + " (built y" + (b.Built / SimTime.Year) + ", left " + (b.Left != 0 ? "y" + (b.Left / SimTime.Year) : "-") + ") and b" + bl1[j] + " kind " + o.Kind + " plan " + pb + " (built y" + (o.Built / SimTime.Year) + ", left " + (o.Left != 0 ? "y" + (o.Left / SimTime.Year) : "-") + ") at " + b.X.ToString("0.0") + "," + b.Y.ToString("0.0") + " / " + o.X.ToString("0.0") + "," + o.Y.ToString("0.0"));
                    }
                if (!home)
                    for (int j = 0; j < bl1.Count; j++)
                    {
                        if (j == i) continue;
                        var o = w.Buildings.Get(bl1[j]);
                        if (Homes.Fallen(in o, w.Now) || Boats.IsBoat(o.Kind)) continue;
                        double ex = o.X - b.X, ey = o.Y - b.Y, dd = Math.Sqrt(ex * ex + ey * ey);
                        double need = Footprints.WorkR(b.Kind) + (Structures.IsHome(o.Kind) ? Footprints.Reach(Footprints.PlanOf(in o)) : Footprints.WorkR(o.Kind));
                        if (dd < need && (Structures.IsHome(o.Kind) || j > i))
                            Console.WriteLine("  near: work b" + bl1[i] + " kind " + b.Kind + " (built y" + (b.Built / SimTime.Year) + ") and b" + bl1[j] + " kind " + o.Kind + " (built y" + (o.Built / SimTime.Year) + ") " + dd.ToString("0.0") + " m apart, discs " + need.ToString("0.0"));
                    }
            }
        }

        /// <summary>`place --load SAVE --standing`: over every household of the world, quantiles of what could set one apart (D2-114 means).</summary>
        static void Standing(World w)
        {
            var goods = new System.Collections.Generic.List<double>(); var hands = new System.Collections.Generic.List<double>(); var small = new System.Collections.Generic.List<double>();
            var cattle = new System.Collections.Generic.List<double>(); var years = new System.Collections.Generic.List<double>(); var store = new System.Collections.Generic.List<double>(); var size = new System.Collections.Generic.List<double>();
            var craft = new System.Collections.Generic.List<double>(); int boats = 0, planks = 0, n0 = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                var hl = w.HouseholdsOf(sid);
                double st = s.Granary / System.Math.Max(1.0, s.Population * w.Config.FoodPerAdultDay * 365.0);
                for (int i = 0; i < hl.Count; i++)
                {
                    var hh = w.Households.Get(hl[i]);
                    var mem = w.Members(hl[i]);
                    int n = mem.Count; if (n == 0) { n0++; continue; }
                    int work = 0; double best = 0;
                    for (int j = 0; j < mem.Count; j++)
                    {
                        var p = w.People.Get(mem[j]); double age = p.AgeYears(w.Now);
                        if (age >= 15 && age < 60) work++;
                        best = System.Math.Max(best, System.Math.Max(System.Math.Max(p.KMetal, p.KIron), System.Math.Max(p.KBoat, System.Math.Max(p.KPottery, p.KLoom))));
                    }
                    int c = 0, sm = 0;
                    var bs = w.BeastsOf(hl[i]);
                    for (int j = 0; j < bs.Count; j++) { var be = w.Beasts.Get(bs[j]); if (!be.Alive) continue; if (be.Kind == Livestock.Cattle) c++; else sm++; }
                    goods.Add(hh.Gear / n); hands.Add(work); small.Add(sm); cattle.Add(c); size.Add(n); craft.Add(best);
                    years.Add((w.Now - hh.Formed) / (double)SimTime.Year); store.Add(st);
                    if (hh.Boat != 0 && w.Buildings.Exists(hh.Boat)) { boats++; if (Boats.IsPlank(w.Buildings.Get(hh.Boat).Kind)) planks++; }
                }
            }
            string Q(System.Collections.Generic.List<double> v) { v.Sort(); if (v.Count == 0) return "-"; string r = ""; foreach (double q in new[] { 0.1, 0.25, 0.5, 0.75, 0.9, 0.97 }) r += (r.Length > 0 ? " " : "") + v[(int)(q * (v.Count - 1))].ToString("0.00"); return r + " max " + v[v.Count - 1].ToString("0.00"); }
            Console.WriteLine("households " + goods.Count + " (empty " + n0 + "), with a boat " + boats + " (planks " + planks + ")  quantiles 10/25/50/75/90/97 %:");
            Console.WriteLine("  gear a head  " + Q(goods)); Console.WriteLine("  people       " + Q(size)); Console.WriteLine("  working 15-59 " + Q(hands));
            Console.WriteLine("  cattle       " + Q(cattle)); Console.WriteLine("  small stock  " + Q(small)); Console.WriteLine("  years formed " + Q(years));
            Console.WriteLine("  store years  " + Q(store)); Console.WriteLine("  best craft   " + Q(craft));
        }

        /// <summary>`place --load SAVE --households [--sid N]`: each household of a place - its people, its larder in months, gear a head, beasts, its house's form.</summary>
        static void ListHouseholds(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            ref var s = ref w.Settlements.Get(sid);
            Console.WriteLine("#" + sid + " " + w.History.NameOf(EntityRef.Settlement(sid)) + " " + s.Population + " people, granary " + s.Granary.ToString("0") + " FU (common " + s.Common.ToString("0") + ")");
            for (int i = 0; i < hl.Count; i++)
            {
                var hh = w.Households.Get(hl[i]);
                var mem = w.Members(hl[i]);
                int n = mem.Count, adults = 0, kids = 0;
                for (int j = 0; j < mem.Count; j++) { double age = w.People.Get(mem[j]).AgeYears(w.Now); if (age >= 15) adults++; else kids++; }
                int cattle = 0, small = 0;
                var bs = w.BeastsOf(hl[i]);
                for (int j = 0; j < bs.Count; j++) { var be = w.Beasts.Get(bs[j]); if (!be.Alive) continue; if (be.Kind == Livestock.Cattle) cattle++; else small++; }
                double month = System.Math.Max(1, n) * w.Config.FoodPerAdultDay * 30.0;
                string form = hh.Building != 0 && w.Buildings.Exists(hh.Building) ? Architecture.Describe(w.Buildings.Get(hh.Building).Form, true) : "(no house)";
                double dx = hh.HomeX - s.SquareX, dy = hh.HomeY - s.SquareY;
                Console.WriteLine("  hh " + hl[i] + " n " + n + " (" + adults + "+" + kids + ") larder " + (hh.Larder / month).ToString("0.00") + " mo, gear/n " + (hh.Gear / System.Math.Max(1, n)).ToString("0.00") +
                                  ", grain " + hh.Grain.ToString("0") + " (" + (hh.Grain / System.Math.Max(1.0, w.SettlementSys.HouseholdNeed(hl[i]))).ToString("0") + " d)" + Debts(w, hl[i]) +
                                  ", cattle " + cattle + " small " + small + ", boat " + (hh.Boat != 0 ? 1 : 0) + ", formed " + ((w.Now - hh.Formed) / (double)SimTime.Year).ToString("0") + " y, from square " + System.Math.Sqrt(dx * dx + dy * dy).ToString("0") + " m, means " + Architecture.Means(w, hl[i]).ToString("0.00") + ": " + form);
            }
        }

        static string Debts(World w, int hid)
        {
            double owes = w.Holdings.Owes(hid, out int to), owed = w.Holdings.Owed(hid, out int by);
            return (owes > 0 ? ", owes " + owes.ToString("0") + " to " + to : "") + (owed > 0 ? ", owed " + owed.ToString("0") + " by " + by : "");
        }

        /// <summary>An inhabited place by a kind: the most people, the most iron sold lately, the most wrongs held over its ore.</summary>
        public static int PickPlace(World w, string kind)
        {
            int best = 0; double bv = double.MinValue;
            for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id))
            {
                var s = w.Settlements.Get(id);
                if (!s.Inhabited) continue;
                double v = kind == "iron" ? s.IronSold * 1000.0 + s.Iron : kind == "claims" ? (s.ClaimOn != 0 ? 1e6 - (w.Now - s.ClaimAt) / 1e6 : -1) : s.Population;
                if (v > bv) { bv = v; best = id; }
            }
            return best;
        }
    }
}
