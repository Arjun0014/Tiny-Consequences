using System;
using System.Collections.Generic;
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
            if (a.Has("near")) { Near(w); return 0; }
            if (a.Has("roads")) { RoadAudit(w, a.Int("sid", 0), a.Double("ex", 1.6), a.Int("show", 0), a.Has("rawways"), a.Get("feature"), a.Has("works")); return 0; }
            if (a.Has("still")) { StillAudit(w, a); return 0; }
            if (a.Has("heads")) { Heads(w); return 0; }
            if (a.Has("marriage")) { Marriage(w); return 0; }
            if (a.Has("ways")) { PeoplesWays(w); return 0; }
            if (a.Has("stonewalls")) { StoneWalls(w); return 0; }
            if (a.Has("ceiling")) { Ceiling(w); return 0; }
            if (a.Has("margins")) { Margins(w, a.Int("show", 20)); return 0; }
            if (a.Has("heirs")) { Heirs(w); return 0; }
            if (a.Has("crofts")) { Crofts(w, a.Int("cell", 2)); return 0; }
            if (a.Has("seasons"))
            {
                // (W-FOREST pass 2b survey) the year's seasons in the soil: the world lived a year in COARSE days from the save, the land cells by the
                // view's kind of wood (as `--woods` reads it), each kind's mean rain and soil water in each four days of the year (its local season:
                // the southern half's year turned half round), and how many of its cells' soils fall under 0.1 for a fortnight or more
                var P = w.Planet;
                string[] kn = { "rain", "dry", "warm", "broadleaf", "mixed", "boreal", "treeline", "wet", "gallery" };
                var kindOf = new int[P.Cells];
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c)) { kindOf[c] = -1; continue; }
                    float t = P.TMean[c], m = P.Moist[c], wetv = Math.Max(P.SoilWater[c], P.LakeDepth[c] > 0f ? 1f : 0f);
                    kindOf[c] = t + 0.6f * P.TAmp[c] < 7f ? 6 : (P.IsStream(c) && t > 1f) ? 8 : (wetv > 0.8f && t > 0f) ? 7 : t >= 20f ? (m >= 0.55f ? 0 : 1) : t >= 14f ? (m < 0.40f ? 1 : 2) : t >= 8f ? 3 : t >= 3f ? 4 : 5;
                }
                w.RequestResolution(TC2.Core.Simulation.Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hour);
                const int bins = 12;
                var rain = new double[9, bins]; var soil = new double[9, bins]; var n = new double[9, bins];
                var dryRun = new int[P.Cells]; var dryLong = new bool[P.Cells];
                for (int d = 0; d < SimTime.DaysPerYear; d++)
                {
                    w.AdvanceTo(w.Now + SimTime.Day);
                    int doy = SimTime.DayOfYear(w.Now);
                    for (int c = 0; c < P.Cells; c++)
                    {
                        int k = kindOf[c]; if (k < 0) continue;
                        int ld = P.Lat[c] >= 0 ? doy : (doy + SimTime.DaysPerYear / 2) % SimTime.DaysPerYear;
                        int b = Math.Min(bins - 1, ld * bins / SimTime.DaysPerYear);
                        rain[k, b] += P.RainToday[c]; soil[k, b] += P.SoilWater[c]; n[k, b]++;
                        if (P.SoilWater[c] < 0.1f) { if (++dryRun[c] >= 14) dryLong[c] = true; } else dryRun[c] = 0;
                    }
                }
                Console.WriteLine("seasons in the soil over a year from " + SimTime.FormatShort(w.Now) + " (each kind: rain / soil water in each four days of its local year, spring first):");
                for (int k = 0; k < 9; k++)
                {
                    int cells = 0, longDry = 0; for (int c = 0; c < P.Cells; c++) if (kindOf[c] == k) { cells++; if (dryLong[c]) longDry++; }
                    if (cells == 0) continue;
                    var sb = new System.Text.StringBuilder("  " + kn[k].PadRight(10) + " cells " + cells.ToString().PadLeft(5) + ", dry a fortnight " + longDry.ToString().PadLeft(5) + " |");
                    for (int b = 0; b < bins; b++) sb.Append(" " + (n[k, b] > 0 ? (rain[k, b] / n[k, b]).ToString("0.00") + "/" + (soil[k, b] / n[k, b]).ToString("0.00") : "-"));
                    Console.WriteLine(sb.ToString());
                }
                return 0;
            }
            if (a.Has("housing"))
            {
                // (D2-141 survey) how the world is housed: households by shelter (a house, a hut, a camp), in villages with streets or none, and the
                // works standing within a street's width of a street's or lane's line (the session-17 clearance keeps them off)
                int hh = 0, house = 0, hut = 0, camp = 0, hhStreets = 0, campStreets = 0, works = 0, worksOn = 0;
                for (int hsid = w.Settlements.First(); hsid != 0; hsid = w.Settlements.Next(hsid))
                {
                    ref var s = ref w.Settlements.Get(hsid);
                    if (!s.Inhabited || s.Population == 0) continue;
                    var hl = w.HouseholdsOf(hsid);
                    for (int i = 0; i < hl.Count; i++)
                    {
                        ref var h = ref w.Households.Get(hl[i]);
                        hh++; if (s.StreetN > 0) hhStreets++;
                        if (h.Building == 0) { camp++; if (s.StreetN > 0) campStreets++; }
                        else if (h.Shelter >= TC2.Core.Places.Construction.House) house++; else hut++;
                    }
                    var bl = w.BuildingsOf(hsid);
                    for (int i = 0; i < bl.Count; i++)
                    {
                        ref var b = ref w.Buildings.Get(bl[i]);
                        if (Structures.IsHome(b.Kind) || Boats.IsBoat(b.Kind) || Homes.RuinLeft(in b, w.Now) <= 0) continue;
                        works++;
                        if (Streets.On(in s, b.X, b.Y, Footprints.WorkR(b.Kind) + Streets.Width)) worksOn++;
                    }
                }
                Console.WriteLine("housing at " + SimTime.FormatShort(w.Now) + ": households " + hh + " - in a house " + house + ", a hut " + hut + ", camping " + camp +
                                  " (" + (100.0 * camp / Math.Max(1, hh)).ToString("0.0") + " %); in villages with streets " + hhStreets + " (camping " + campStreets +
                                  "); works " + works + ", within a street's width of a street or lane " + worksOn);
                return 0;
            }
            if (a.Has("sheds"))
            {
                // (session 17, D2-141) where a boat shed would go at each village's landing (Boats.ShedSpot), with works kept a street's width off its
                // line (now) and a metre off it (`--noworkclear`, before): villages with a landing, how many find a spot each way, and those that have one
                int land = 0, fitNow = 0, fitBefore = 0, lostFit = 0, gainedFit = 0, has = 0, hasNoFit = 0;
                var shedOf = new System.Collections.Generic.HashSet<int>();
                for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
                {
                    ref var bb = ref w.Buildings.Get(b);
                    if (bb.Kind == TC2.Core.Places.Structures.BoatShed && bb.Done != 0 && TC2.Core.Places.Homes.RuinLeft(in bb, w.Now) > 0) shedOf.Add(bb.Settlement);
                }
                for (int shs = w.Settlements.First(); shs != 0; shs = w.Settlements.Next(shs))
                {
                    ref var s = ref w.Settlements.Get(shs);
                    if (!s.Inhabited || s.Population == 0) continue;
                    if (!TC2.Core.Places.Boats.EnsureLanding(w, shs)) continue;
                    land++;
                    bool keep = TC2.Core.Places.Footprints.WorkClearOff;
                    TC2.Core.Places.Footprints.WorkClearOff = false;
                    bool now = TC2.Core.Places.Boats.ShedSpot(w, shs, out float x1, out float y1);
                    TC2.Core.Places.Footprints.WorkClearOff = true;
                    bool before = TC2.Core.Places.Boats.ShedSpot(w, shs, out float x0, out float y0);
                    TC2.Core.Places.Footprints.WorkClearOff = keep;
                    if (now) fitNow++; if (before) fitBefore++; if (before && !now) lostFit++; if (now && !before) gainedFit++;
                    bool hs = shedOf.Contains(shs); if (hs) has++; if (hs && !now) hasNoFit++;
                    if (before != now) Console.WriteLine("  " + w.History.NameOf(EntityRef.Settlement(shs)) + " #" + shs + " people " + s.Population + " streets " + s.StreetN + ": a shed fits " + (before ? "before" : "now") + " only" + (hs ? " (has a shed)" : ""));
                }
                Console.WriteLine("sheds at " + SimTime.FormatShort(w.Now) + ": villages with a landing " + land + "; a shed spot now " + fitNow + ", before (1 m off streets) " + fitBefore + "; lost " + lostFit + ", gained " + gainedFit + "; with a shed standing " + has + " (no spot now: " + hasNoFit + ")");
                return 0;
            }
            if (a.Has("landless")) { Landless(w); return 0; }
            if (a.Has("woods"))
            {
                // (W-FOREST survey) the wooded land's climate: cells with forest over 0.25 by annual mean temperature and by moisture, and how
                // many lie by a stream or on wet ground; the hectares in each
                var P = w.Planet;
                int[] tb = new int[10], mb = new int[10]; double[] tha = new double[10];
                int n = 0, stream = 0, wet = 0, cold = 0, warm = 0, dry = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c) || P.Forest[c] <= 0.25f) continue;
                    n++;
                    double t = P.TMean[c];
                    int ti = Math.Max(0, Math.Min(9, (int)Math.Floor((t + 10.0) / 4.0)));   // (-10..30 degC in 4-degree bands)
                    tb[ti]++; tha[ti] += P.AreaHa(c) * P.Forest[c];
                    mb[Math.Max(0, Math.Min(9, (int)(P.Moist[c] * 10)))]++;
                    if (P.IsStream(c)) stream++;
                    if (P.SoilWater[c] > 0.8f || P.LakeDepth[c] > 0f) wet++;
                    if (t < 3) cold++; if (t >= 14) warm++; if (P.Moist[c] < 0.3f) dry++;
                }
                Console.WriteLine("wooded cells " + n + "; by mean temperature -10..30 in 4-degree bands: " + string.Join(" ", tb) + "; forest ha " + string.Join(" ", System.Array.ConvertAll(tha, x => x.ToString("0"))));
                Console.WriteLine("  by moisture 0..1 in tenths: " + string.Join(" ", mb) + "; by a stream " + stream + ", wet ground " + wet + "; under 3 degC " + cold + ", 14 degC or more " + warm + ", moisture under 0.3 " + dry);
                // (W-FOREST pass 2) by the view's kind of wood at the cell's own height (Game/View/ForestTypes.Classify): land cells, how many carry
                // woods (share over 0.05), the woods' share among them (quartiles), wooded ha, burnt in the last 30 years
                string[] kn = { "rain", "dry", "warm", "broadleaf", "mixed", "boreal", "treeline", "wet", "gallery" };
                var shares = new System.Collections.Generic.List<float>[9]; var land = new int[9]; var ha = new double[9]; var burnt = new int[9];
                for (int k = 0; k < 9; k++) shares[k] = new System.Collections.Generic.List<float>();
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c)) continue;
                    float t = P.TMean[c], m = P.Moist[c], wetv = Math.Max(P.SoilWater[c], P.LakeDepth[c] > 0f ? 1f : 0f);
                    int kind = t + 0.6f * P.TAmp[c] < 7f ? 6 : (P.IsStream(c) && t > 1f) ? 8 : (wetv > 0.8f && t > 0f) ? 7 : t >= 20f ? (m >= 0.55f ? 0 : 1) : t >= 14f ? (m < 0.40f ? 1 : 2) : t >= 8f ? 3 : t >= 3f ? 4 : 5;
                    land[kind]++;
                    if (P.Forest[c] > 0.05f) { shares[kind].Add(P.Forest[c]); ha[kind] += P.AreaHa(c) * P.Forest[c]; if (w.Fire.BurnedAt[c] != 0 && w.Now - w.Fire.BurnedAt[c] < 30 * SimTime.Year) burnt[kind]++; }
                }
                Console.WriteLine("  by kind of wood (land cells; wooded over 0.05; the woods' share q25/median/q75 among them; wooded ha; burnt in 30 y):");
                for (int k = 0; k < 9; k++)
                {
                    var l = shares[k]; l.Sort();
                    string q(double f) => l.Count > 0 ? l[Math.Min(l.Count - 1, (int)(f * l.Count))].ToString("0.00") : "-";
                    Console.WriteLine("    " + kn[k].PadRight(10) + " land " + land[k].ToString().PadLeft(6) + "  wooded " + l.Count.ToString().PadLeft(6) + "  share " + q(0.25) + "/" + q(0.5) + "/" + q(0.75) + "  ha " + ha[k].ToString("0").PadLeft(8) + "  burnt " + burnt[k]);
                }
                // the climate of the cool kinds: annual mean, seasonal swing, the summer (mean + swing), moisture - and the share by the summer's warmth
                var byW = new double[8]; var nW = new int[8];
                double[] tm2 = new double[9], amp = new double[9], mo = new double[9]; int[] nk = new int[9];
                for (int c = 0; c < P.Cells; c++)
                {
                    if (!P.IsLand(c)) continue;
                    float t = P.TMean[c], m = P.Moist[c], wetv = Math.Max(P.SoilWater[c], P.LakeDepth[c] > 0f ? 1f : 0f);
                    int kind = t + 0.6f * P.TAmp[c] < 7f ? 6 : (P.IsStream(c) && t > 1f) ? 8 : (wetv > 0.8f && t > 0f) ? 7 : t >= 20f ? (m >= 0.55f ? 0 : 1) : t >= 14f ? (m < 0.40f ? 1 : 2) : t >= 8f ? 3 : t >= 3f ? 4 : 5;
                    tm2[kind] += t; amp[kind] += P.TAmp[c]; mo[kind] += m; nk[kind]++;
                    double summer = t + P.TAmp[c];
                    int b = Math.Max(0, Math.Min(7, (int)Math.Floor((summer - 2.0) / 4.0)));   // (summer 2-34 degC in 4-degree bands)
                    if (P.FieldOwner[c] == 0 && m > 0.45f) { byW[b] += P.Forest[c]; nW[b]++; }
                }
                var sbk = new System.Text.StringBuilder("  climate by kind (mean degC / swing / moisture):");
                for (int k = 0; k < 9; k++) if (nk[k] > 0) sbk.Append(" " + kn[k] + " " + (tm2[k] / nk[k]).ToString("0.0") + "/" + (amp[k] / nk[k]).ToString("0.0") + "/" + (mo[k] / nk[k]).ToString("0.00"));
                Console.WriteLine(sbk.ToString());
                var sbw = new System.Text.StringBuilder("  woods' mean share on unfarmed land with moisture over 0.45, by the summer (mean + swing) 2-34 degC in 4-degree bands:");
                for (int b = 0; b < 8; b++) sbw.Append(" " + (nW[b] > 0 ? (byW[b] / nW[b]).ToString("0.00") : "-") + "(" + nW[b] + ")");
                Console.WriteLine(sbw.ToString());
                return 0;
            }
            if (a.Has("land"))
            {
                // (household land survey) each village: people, households, the hectares its people need (at the base field a head), its arable
                // within reach, its fields this year, and its last harvest in years of its people's bread; then the world by how hard land binds
                Console.WriteLine("sid,people,households,need_ha,arable_ha,field_ha,harvest_years,hard");
                for (int lsid = w.Settlements.First(); lsid != 0; lsid = w.Settlements.Next(lsid))
                {
                    var s = w.Settlements.Get(lsid);
                    if (!s.Inhabited || s.Population == 0) continue;
                    double need = s.Population * w.Config.FieldHaPerPerson;
                    double years = s.LastHarvest / (s.Population * (double)SimTime.DaysPerYear);
                    Console.WriteLine(lsid + "," + s.Population + "," + w.HouseholdsOf(lsid).Count + "," + need.ToString("0.0") + "," + s.ArableHa.ToString("0.0") + "," + s.FieldHa.ToString("0.0") + "," + years.ToString("0.00") + "," + (Holdings.Hard(in s) ? 1 : 0));
                }
                return 0;
            }
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
        /// `place --load SAVE --landless` (service survey, C3.6e): households by their land - none, short of their want, their want - with their people,
        /// the unmarried of 15-29 among them, children, hands, and their own grain a mouth in days; and the landed households' hands against their land.
        /// </summary>
        static void Landless(World w)
        {
            long now = w.Now;
            string[] cls = { "landless", "short", "their want" };
            var n = new double[3]; var people = new double[3]; var young = new double[3]; var youngM = new double[3]; var kids = new double[3]; var hands = new double[3]; var grainDays = new double[3]; var land = new double[3]; var want = new double[3];
            var hpaList = new System.Collections.Generic.List<double>();
            var rebuild = new double[3]; var carts = new double[3]; var shelter = new double[3];
            var old55 = new double[3]; var oldWithChild = new double[3]; var oldLand = new double[3]; var oldGrain = new double[3];
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                if (!w.Settlements.Get(sid).Inhabited) continue;
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++)
                {
                    int hid = hl[i];
                    var mem = w.Members(hid);
                    if (mem.Count == 0) continue;
                    var hh = w.Households.Get(hid);
                    double wt = w.Holdings.LandWant(hid);
                    int c = hh.Land < 0.05 ? 0 : hh.Land < wt - 0.05 ? 1 : 2;
                    n[c]++; people[c] += mem.Count; land[c] += hh.Land; want[c] += wt;
                    double hd = w.Holdings.Hands(hid); hands[c] += hd;
                    double need = w.SettlementSys.HouseholdNeed(hid);
                    grainDays[c] += need > 0 ? hh.Grain / need : 0;
                    for (int k = 0; k < mem.Count; k++)
                    {
                        var p = w.People.Get(mem[k]);
                        double age = p.AgeYears(now);
                        if (age < 15) kids[c]++;
                        else if (age < 30 && p.Partner == 0) { young[c]++; if (!p.IsFemale) youngM[c]++; }
                    }
                    if (c > 0 && hh.Land > 0) hpaList.Add(hd / hh.Land);
                    { double youngest = 999; for (int k = 0; k < mem.Count; k++) { double ag = w.People.Get(mem[k]).AgeYears(now); if (ag < youngest) youngest = ag; }
                      if (youngest >= 55) { old55[c]++; if (ChildHousehold(w, sid, hid) != 0) oldWithChild[c]++; oldLand[c] += hh.Land; oldGrain[c] += hh.Grain; } }
                    if (hh.Rebuild != 0) rebuild[c]++; if (hh.CartState == TC2.Core.Places.Carts.Wanted || hh.CartState == TC2.Core.Places.Carts.Making) carts[c]++; if (hh.Shelter < TC2.Core.Places.Construction.House) shelter[c]++;
                }
            }
            double all = n[0] + n[1] + n[2];
            for (int c = 0; c < 3; c++)
                Console.WriteLine("households " + cls[c] + ": " + n[c] + " (" + (100 * n[c] / Math.Max(1, all)).ToString("0") + " %), people " + people[c] + " (" + (people[c] / Math.Max(1, n[c])).ToString("0.00") + " each), unmarried 15-29 " + young[c] + " (men " + youngM[c] + "), children under 15 " + kids[c] +
                                  ", hands " + (hands[c] / Math.Max(1, n[c])).ToString("0.00") + " each, own grain " + (grainDays[c] / Math.Max(1, n[c])).ToString("0") + " days a household's food, land " + (land[c] / Math.Max(1e-9, want[c])).ToString("0.00") + " of their want");
            for (int c = 0; c < 3; c++) Console.WriteLine("  " + cls[c] + ": households whose youngest is 55+ " + old55[c] + " (with a child's household in the village " + oldWithChild[c] + "), holding " + oldLand[c].ToString("0") + " ha of the class's " + land[c].ToString("0") + " and " + oldGrain[c].ToString("0") + " FU");
            for (int c = 0; c < 3; c++) Console.WriteLine("  " + cls[c] + ": rebuilding their house " + rebuild[c] + ", a cart wanted or being made " + carts[c] + ", in a camp or hut " + shelter[c]);
            hpaList.Sort();
            if (hpaList.Count > 0) Console.WriteLine("  landed households' hands a hectare p10/50/90 " + hpaList[hpaList.Count / 10].ToString("0.00") + "/" + hpaList[hpaList.Count / 2].ToString("0.00") + "/" + hpaList[9 * hpaList.Count / 10].ToString("0.00"));
        }

        /// <summary>A household of the village one of whose members is a child of a member of <paramref name="hid"/> (0 none).</summary>
        static int ChildHousehold(World w, int sid, int hid)
        {
            var mem = w.Members(hid); var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                if (hl[i] == hid) continue;
                var m2 = w.Members(hl[i]);
                for (int k = 0; k < m2.Count; k++) { var q = w.People.Get(m2[k]); for (int j = 0; j < mem.Count; j++) if (q.Mother == mem[j] || q.Father == mem[j]) return hl[i]; }
            }
            return 0;
        }

        /// <summary>
        /// `place --load SAVE --heirs` (household land survey, C3.6b): what holdings passed from father to son would be in this world - the men who
        /// married in the last 40 years (their household's founding) by their age then and whether their father was alive; when the fathers who
        /// died did (the son's age then); how many sons of a family reach 20; and each village's fields a household now.
        /// </summary>
        static void Heirs(World w)
        {
            long now = w.Now, yr = SimTime.Year;
            int married = 0, fatherAlive = 0, fatherDeadBefore = 0, fatherUnknown = 0;
            var ageAtMarriage = new System.Collections.Generic.List<double>();
            var womenAge = new System.Collections.Generic.List<double>();
            var sonAgeAtFatherDeath = new System.Collections.Generic.List<double>();
            int[] waitBins = new int[6];   // years from marriage to the father's death: <0 (dead before), 0-5, 5-10, 10-20, 20+, alive now
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid))
            {
                var hh = w.Households.Get(hid);
                if (hh.Formed <= 0 || now - hh.Formed > 40 * yr || hh.Formed < 2 * yr) continue;
                // the couple who founded it: a man and a woman among its members or the dead who belonged to it are not kept; use living members married
                var mem = w.Members(hid);
                for (int i = 0; i < mem.Count; i++)
                {
                    var p = w.People.Get(mem[i]);
                    if (!p.Alive || p.Partner == 0) continue;
                    double age = (hh.Formed - p.Born) / (double)yr;
                    if (age < 14 || age > 60) continue;
                    if (p.IsFemale) { womenAge.Add(age); continue; }
                    married++; ageAtMarriage.Add(age);
                    if (p.Father == 0 || !w.People.Exists(p.Father)) { fatherUnknown++; continue; }
                    var f = w.People.Get(p.Father);
                    if (f.Alive) { fatherAlive++; waitBins[5]++; continue; }
                    double gap = (f.Died - hh.Formed) / (double)yr;
                    if (gap < 0) { fatherDeadBefore++; waitBins[0]++; }
                    else waitBins[gap < 5 ? 1 : gap < 10 ? 2 : gap < 20 ? 3 : 4]++;
                }
            }
            // every man alive now of 20 or more: his age when his father died (if he did)
            int men = 0, orphan = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || p.IsFemale || p.AgeYears(now) < 20) continue;
                men++;
                if (p.Father == 0 || !w.People.Exists(p.Father)) continue;
                var f = w.People.Get(p.Father);
                if (f.Alive) continue;
                orphan++; sonAgeAtFatherDeath.Add((f.Died - p.Born) / (double)yr);
            }
            // sons of a family reaching 20: fathers dead at 45 or more within the last 60 years (their sons mostly grown), counting the sons kept
            var sonsBy = new System.Collections.Generic.Dictionary<int, int>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (p.IsFemale || p.Father == 0) continue;
                double reached = ((p.Alive ? now : p.Died) - p.Born) / (double)yr;
                if (reached < 20) continue;
                sonsBy[p.Father] = (sonsBy.TryGetValue(p.Father, out int n) ? n : 0) + 1;
            }
            int[] sonsBins = new int[5]; int fathers = 0; double sonsSum = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var f = w.People.Get(id);
                if (f.Alive || f.IsFemale || now - f.Died > 60 * yr || (f.Died - f.Born) / (double)yr < 45 || f.Partner == 0 && !sonsBy.ContainsKey(id)) continue;
                int n = sonsBy.TryGetValue(id, out int k) ? k : 0;
                fathers++; sonsSum += n; sonsBins[Math.Min(4, n)]++;
            }
            string Q(System.Collections.Generic.List<double> v) { if (v.Count == 0) return "-"; v.Sort(); return v[v.Count / 4].ToString("0.0") + "/" + v[v.Count / 2].ToString("0.0") + "/" + v[3 * v.Count / 4].ToString("0.0") + " (n " + v.Count + ")"; }
            Console.WriteLine("heirs: men married in the last 40 years " + married + ", their age then p25/50/75 " + Q(ageAtMarriage) + "; women " + Q(womenAge));
            Console.WriteLine("  their father then: dead already " + fatherDeadBefore + ", died 0-5 years after " + waitBins[1] + ", 5-10 " + waitBins[2] + ", 10-20 " + waitBins[3] + ", 20+ " + waitBins[4] + ", alive now " + fatherAlive + ", not known " + fatherUnknown);
            Console.WriteLine("  men of 20+ now " + men + ", fatherless " + orphan + "; their age when the father died p25/50/75 " + Q(sonAgeAtFatherDeath));
            Console.WriteLine("  fathers dead at 45+ in the last 60 years " + fathers + ": sons reaching 20 each " + (fathers > 0 ? (sonsSum / fathers).ToString("0.00") : "-") + "; with 0/1/2/3/4+ sons " + string.Join("/", sonsBins));
            // each village: households and its fields a household, against a household's need
            double hhAll = 0, haAll = 0, needAll = 0; int bound = 0, villages = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited || s.Population == 0) continue;
                villages++; int hc = w.HouseholdsOf(sid).Count; hhAll += hc; haAll += s.FieldHa; needAll += s.Population * w.Config.FieldHaPerPerson;
                if (s.ArableHa < s.Population * w.Config.FieldHaPerPerson) bound++;
            }
            Console.WriteLine("  villages " + villages + " (land bound " + bound + "), households " + hhAll + ", members a household " + (needAll / w.Config.FieldHaPerPerson / Math.Max(1, hhAll)).ToString("0.00") +
                              ", fields a household " + (haAll / Math.Max(1, hhAll)).ToString("0.00") + " ha against its need " + (needAll / Math.Max(1, hhAll)).ToString("0.00") + " ha; died out " + w.Metrics.DiedOut);
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
        /// <summary>
        /// (C11.1 measured first, `place --ways`) a people's ways as the world holds them: each living village's building tradition (D2-114: its roof form,
        /// its shade, its line - the first village it descends from, carried by founders with a change now and then), how alike neighbours are (pairs
        /// within a day's walk) against all pairs, how many lines live and how far each spreads, and whether the houses stand as their village's way
        /// (their recorded roof form against the tradition's).
        /// </summary>
        static void PeoplesWays(World w)
        {
            var ids = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) ids.Add(sid);
            int[] near = new int[4], all = new int[4];   // pairs: same line, same roof, same shade, same roof and shade
            int nearN = 0, allN = 0;
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    int ta = w.Settlements.Get(ids[i]).Tradition, tb = w.Settlements.Get(ids[j]).Tradition;
                    bool line = TC2.Core.Places.Architecture.Line(ta) == TC2.Core.Places.Architecture.Line(tb);
                    bool roof = TC2.Core.Places.Architecture.TraditionRoof(ta) == TC2.Core.Places.Architecture.TraditionRoof(tb);
                    bool shade = TC2.Core.Places.Architecture.TraditionPalette(ta) == TC2.Core.Places.Architecture.TraditionPalette(tb);
                    double d = Vec3d.ArcDistance(w.Settlements.Get(ids[i]).Anchor, w.Settlements.Get(ids[j]).Anchor, w.Planet.Radius);
                    var into = d <= 6000 ? near : null;
                    allN++; if (line) all[0]++; if (roof) all[1]++; if (shade) all[2]++; if (roof && shade) all[3]++;
                    if (into != null) { nearN++; if (line) near[0]++; if (roof) near[1]++; if (shade) near[2]++; if (roof && shade) near[3]++; }
                }
            string P(int k, int n) => n == 0 ? "-" : (100.0 * k / n).ToString("0") + " %";
            Console.WriteLine("ways: " + ids.Count + " villages; pairs within a day's walk " + nearN + " - the same line " + P(near[0], nearN) + ", roof form " + P(near[1], nearN) + ", shade " + P(near[2], nearN) +
                              ", both " + P(near[3], nearN) + "; all pairs " + allN + " - " + P(all[0], allN) + ", " + P(all[1], allN) + ", " + P(all[2], allN) + ", " + P(all[3], allN));
            var lines = new SortedDictionary<int, List<int>>();
            foreach (int sid in ids) { int l = TC2.Core.Places.Architecture.Line(w.Settlements.Get(sid).Tradition); if (!lines.TryGetValue(l, out var ll)) lines[l] = ll = new List<int>(); ll.Add(sid); }
            var order = new List<int>(lines.Keys);
            order.Sort((x, y) => lines[y].Count != lines[x].Count ? lines[y].Count.CompareTo(lines[x].Count) : x.CompareTo(y));
            foreach (int l in order)
            {
                var ll = lines[l];
                double far = 0;
                for (int i = 0; i < ll.Count; i++) for (int j = i + 1; j < ll.Count; j++) far = Math.Max(far, Vec3d.ArcDistance(w.Settlements.Get(ll[i]).Anchor, w.Settlements.Get(ll[j]).Anchor, w.Planet.Radius));
                var forms = new SortedDictionary<int, int>();
                foreach (int sid in ll) { int t = w.Settlements.Get(sid).Tradition; int k = TC2.Core.Places.Architecture.TraditionRoof(t) * 10 + TC2.Core.Places.Architecture.TraditionPalette(t); forms.TryGetValue(k, out int c); forms[k] = c + 1; }
                Console.WriteLine("  line of " + w.History.NameOf(EntityRef.Settlement(l)) + " (" + l + "): " + ll.Count + " villages, spread " + (far / 1000.0).ToString("0.0") + " km; ways (roof x10 + shade): " +
                                  string.Join(" ", new List<string>(System.Linq.Enumerable.Select(forms, kv => kv.Key + ":" + kv.Value)).ToArray()));
                var names = new List<string>();
                foreach (int sid in ll) if (names.Count < 14) names.Add(w.History.NameOf(EntityRef.Settlement(sid)) + (w.Settlements.Get(sid).Tongue != 0 ? "[" + TC2.Core.Worldgen.Names.Apart(w.Settlements.Get(sid).Tongue, w.Settlements.Get(ll[0]).Tongue) + "]" : ""));
                Console.WriteLine("    names (in [] how far each speaks from the first listed): " + string.Join(", ", names.ToArray()));
            }
            // the houses: do they stand as their village's way?
            int houses = 0, asWay = 0, shadeWay = 0;
            for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
            {
                ref var bb = ref w.Buildings.Get(b);
                if (!TC2.Core.Places.Architecture.Has(bb.Form) || bb.Settlement == 0 || !w.Settlements.Exists(bb.Settlement)) continue;
                int t = w.Settlements.Get(bb.Settlement).Tradition;
                houses++;
                if (TC2.Core.Places.Architecture.RoofOf(bb.Form) == TC2.Core.Places.Architecture.TraditionRoof(t)) asWay++;
                int pal = TC2.Core.Places.Architecture.PaletteOf(bb.Form), tp = TC2.Core.Places.Architecture.TraditionPalette(t);
                if (pal == tp || pal == (tp + 1) % 8 || pal == (tp + 2) % 8) shadeWay++;
            }
            Console.WriteLine("  buildings with a recorded form " + houses + ": the roof form their village's way " + P(asWay, houses) + ", the shade within its way's three " + P(shadeWay, houses));
        }

        /// <summary>
        /// (C7.2 measured first, `place --stonewalls`) each walled village: its ring (m, the dry stretches), what other villages' people cost it a year
        /// (the harms of three years, as `Conflict.Defence` weighs them), what its palisade's guarding is worth over a wall's life against the timber
        /// ring's labour and wood, whether it knows stone at hand (its outcrop and its distance, a quarry), its people and the years of grain in its
        /// store, and what a curtain of dry stone would cost it (loads a metre cut, carried, laid) against what it loses.
        /// </summary>
        static void StoneWalls(World w)
        {
            long now = w.Now;
            var rows = new List<(int sid, double perim, double lost, bool stone, double dist, bool quarry, int people, double years)>();
            int walled = 0, knowStone = 0, withQuarry = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || !TC2.Core.Society.Conflict.Walled(in s, out _)) continue;
                walled++;
                double perim = 2.0 * Math.PI * Math.Max(1.0, (double)s.WallR) * (s.WallDry > 0f ? (double)s.WallDry : 1.0);
                double lost = w.DeedBook.HarmSince(sid, now - 3 * SimTime.Year, out _) / 3.0;
                bool stone = TC2.Core.Places.Stone.Known(in s) >= 0;
                if (stone) knowStone++;
                bool quarry = s.Quarry != 0;
                if (quarry) withQuarry++;
                TC2.Core.Places.Trade.DaysOfFood(w, in s, out double daily);
                double years = daily > 0 ? s.Granary / daily / SimTime.DaysPerYear : 0;
                rows.Add((sid, perim, lost, stone, s.StoneDist, quarry, s.Population, years));
            }
            rows.Sort((x, y) => y.lost.CompareTo(x.lost));
            // a dry-stone curtain ~1.5 m high and 1 m thick: ~150 loads of 25 kg a metre, cut at the quarry's rate, carried two a trip, laid at ~8 loads an hour
            const double LoadsPerM = 150.0, LayPerHour = 8.0;
            Console.WriteLine("stonewalls: walled villages " + walled + ", knowing stone at hand " + knowStone + ", with a quarry " + withQuarry);
            Console.WriteLine("  village                 people  ring m  lost/yr FU  guarded over 15 y  timber ring FU  stone at m  stone ring h  (FU)   grain years");
            foreach (var r in rows)
            {
                double guarded = TC2.Core.Society.Conflict.WallGuards * r.lost * TC2.Core.Society.Conflict.WallLife;
                double timber = TC2.Core.Society.Conflict.LabourWorth * (TC2.Core.Places.Construction.PalisadeHoursPerM + TC2.Core.Places.Construction.PalisadeWoodPerM) * r.perim;
                double walkH = r.stone ? 2.0 * r.dist / 1.2 / 3600.0 / 2.0 : 0;   // (an hour's walk there and back for each two loads, at 1.2 m/s)
                double stoneH = r.perim * LoadsPerM * (1.0 / TC2.Core.Places.Stone.CutPerHour + walkH + 1.0 / LayPerHour);
                Console.WriteLine("  " + w.History.NameOf(EntityRef.Settlement(r.sid)).PadRight(22) + r.people.ToString().PadLeft(7) + r.perim.ToString("0").PadLeft(8) + r.lost.ToString("0").PadLeft(12) +
                                  guarded.ToString("0").PadLeft(19) + timber.ToString("0").PadLeft(16) + (r.stone ? r.dist.ToString("0") + (r.quarry ? " q" : "") : "-").PadLeft(12) +
                                  stoneH.ToString("0").PadLeft(14) + (" (" + (TC2.Core.Society.Conflict.LabourWorth * stoneH).ToString("0") + ")").PadLeft(9) + r.years.ToString("0.0").PadLeft(10));
            }
        }

        /// <summary>
        /// (settlement change at the ceiling, measured first: `place --ceiling`) why the map stops changing once the land fills: each living village's people,
        /// its arable land against what its people need (`HaPerHead`), what presses its households to leave (`Migration.Pressure`), the land none of its
        /// households holds and the households short of their want (C3.6b), the best place its people know and whether it is still free and good
        /// (`Migration.BestSite`), and the parties it sent out in the last fifty years; for the world, the villages with land to spare and the crowded,
        /// and the foundings of fifty years - whether land left empty in one village could draw the land-short of another (a pull no rule has yet).
        /// </summary>
        static void Ceiling(World w)
        {
            long now = w.Now;
            var M = w.Migration;
            var H = w.History;
            var parties = new Dictionary<int, int>();
            int founded50 = 0, parties50 = 0;
            for (int i = H.Count - 1; i >= 0; i--)
            {
                var e = H.At(i);
                if (e.Time < now - 50 * SimTime.Year) break;
                if (e.Kind == HistoryKind.PartySetOut) { parties.TryGetValue(e.Settlement, out int n); parties[e.Settlement] = n + 1; parties50++; }
                else if (e.Kind == HistoryKind.SettlementFounded) founded50++;
            }
            var rows = new List<(int sid, int pop, double ratio, double pressure, double unheld, int shortHh, int hhN, double loreQ, int live, double liveQ, int parties)>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var st = ref w.Settlements.Get(sid);
                if (!st.Inhabited) continue;
                double need = Math.Max(1.0, st.Population * TC2.Core.Places.SettlementSystem.HaPerHead(w, in st));
                double ratio = st.ArableHa / need;
                double pressure = M.Pressure(in st);
                double unheld = w.Holdings.Unheld(sid);
                var hhs = w.HouseholdsOf(sid);
                int shortHh = 0, hhN = 0;
                for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); if (hh.Dissolved != 0) continue; hhN++; if (hh.Land < w.Holdings.LandWant(hhs[i]) - 1e-9) shortHh++; }
                st.Lore.Best(out float lq);
                int live = M.BestSite(sid, out double liveQ);
                parties.TryGetValue(sid, out int np);
                rows.Add((sid, st.Population, ratio, pressure, unheld, shortHh, hhN, lq, live, liveQ, np));
            }
            rows.Sort((a, b) => a.ratio.CompareTo(b.ratio));
            Console.WriteLine("ceiling at " + SimTime.Format(now) + ": " + rows.Count + " villages; parties set out in the last fifty years " + parties50 + ", villages founded " + founded50);
            Console.WriteLine("  village                 people  land/need  pressure  unheld ha  short hh  best known (stored / free now)  parties 50 y");
            int roomy = 0, crowded = 0, knowFree = 0; double spare = 0; int shortCrowded = 0;
            foreach (var r in rows)
            {
                if (r.ratio >= 1.25) { roomy++; spare += r.unheld; }
                if (r.ratio < 0.9) { crowded++; shortCrowded += r.shortHh; }
                if (r.live >= 0) knowFree++;
                Console.WriteLine("  " + w.History.NameOf(EntityRef.Settlement(r.sid)).PadRight(22) + r.pop.ToString().PadLeft(7) + r.ratio.ToString("0.00").PadLeft(11) + r.pressure.ToString("0.00").PadLeft(10) +
                                  r.unheld.ToString("0").PadLeft(11) + (r.shortHh + "/" + r.hhN).PadLeft(10) + (r.loreQ.ToString("0.00") + " / " + (r.live >= 0 ? r.liveQ.ToString("0.00") : "none")).PadLeft(32) + r.parties.ToString().PadLeft(14));
            }
            Console.WriteLine("  land to spare (1.25 x need or more): " + roomy + " villages, " + spare.ToString("0") + " ha held by none; crowded (under 0.9): " + crowded + " villages, " + shortCrowded +
                              " households short of their want; villages knowing a place still free and good: " + knowFree);
        }

        /// <summary>
        /// (marginal land, measured first: `place --margins`) the land no village holds and why none is founded on it: every land cell by its distance to the
        /// nearest living village - within a village's fields' reach, between villages (inside the spacing no village is founded in), beyond the spacing but
        /// within a scout's sight (`Migration.ScoutMax` out and `Sites.LookRadius` round), beyond sight; their hectares and arable land (as it stands, and
        /// cleared of its woods), held by fields or free, under the plough's floor (0.08); for the cells a village could stand on (beyond the spacing) the
        /// site's worth (`Sites.Quality`) by bands and what holds each short of `Sites.MinQuality` (ice or lake, cold, dry or marsh, height, no water, little
        /// free arable land); and how many villages could be founded at each lower bar, packed `Sites.Spacing` apart, the arable land they would hold and the
        /// people it would feed.
        /// </summary>
        static void Margins(World w, int show)
        {
            var P = w.Planet; var g = P.Grid; var cfg = w.Config;
            int n = P.Cells;
            var vil = new List<(int sid, Vec3d at)>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { ref var s = ref w.Settlements.Get(sid); if (s.Inhabited) vil.Add((sid, s.Anchor)); }
            double sight = Migration.ScoutMax + Sites.LookRadius;
            string[] zn = { "within a village's fields' reach", "between villages (in the spacing)", "beyond the spacing, in sight", "beyond any scout's sight" };
            var cells = new int[4]; var ha = new double[4]; var arNow = new double[4]; var arOpen = new double[4]; var arHeld = new double[4]; var arFree = new double[4]; var arWaste = new double[4];
            var nearSid = new int[n]; var nearD = new double[n];
            var scratch = new List<int>();
            var cand = new List<(double q, int c)>();
            // why a cell beyond the spacing falls short of MinQuality: 0 ice or lake, 1 cold or hot, 2 dry or marsh, 3 high, 4 no water, 5 little free arable land
            string[] why = { "ice or lake", "too cold or hot", "too dry or marsh", "too high", "no water to drink or fish", "little free arable land within reach" };
            var whyN = new int[6]; var whyHa = new double[6];
            double[] bars = { 0.25, 0.2, 0.15, 0.1, 0.05, 0.02 };
            var bandN = new int[bars.Length + 1];
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) continue;
                Vec3d at = g.Center[c];
                double bd = double.MaxValue; int bs = 0;
                for (int i = 0; i < vil.Count; i++) { double d = Vec3d.ArcDistance(at, vil[i].at, P.Radius); if (d < bd) { bd = d; bs = vil[i].sid; } }
                nearSid[c] = bs; nearD[c] = bd;
                int z = bd < cfg.FieldReach ? 0 : bd < Sites.Spacing ? 1 : bd < sight ? 2 : 3;
                double area = P.AreaHa(c), tm = w.PlanetSys.MeanTemp(c);
                double a = P.Arability(c, tm), ao = P.ArabilityOpen(c, tm);
                cells[z]++; ha[z] += area; arNow[z] += area * a; arOpen[z] += area * ao;
                if (P.FieldOwner[c] != 0) arHeld[z] += area * a;
                else if (w.SettlementAtCell(c) == 0) { if (a >= 0.08) arFree[z] += area * a; else arWaste[z] += area * ao; }
                if (z < 2) continue;
                double q = Sites.Quality(w, c, scratch);
                int band = bars.Length;
                for (int b = 0; b < bars.Length; b++) if (q >= bars[b]) { band = b; break; }
                bandN[band]++;
                if (q >= bars[bars.Length - 1]) cand.Add((q, c));
                if (q >= Sites.MinQuality) continue;
                // what holds it short: the factor of its worth that is least
                int k;
                double hab = Sites.Habitability(P, c);
                double tmn = P.TMean[c], m = P.Moist[c];
                double tf = DMath.SmoothStep(4, 11, tmn) * (1.0 - DMath.SmoothStep(24, 30, tmn));
                double mf = DMath.SmoothStep(0.22, 0.4, m) * (1.0 - DMath.SmoothStep(0.85, 1.0, m));
                double ef = 1.0 - DMath.SmoothStep(500, 1100, P.Elev[c] - P.SeaLevel);
                if (P.Ice[c] > 0.5f || P.LakeDepth[c] > 0.3f) k = 0;
                else if (hab > 0 && q <= 0) k = 4;
                else
                {
                    double af = hab > 0 ? q / hab : 1.0;
                    k = 1; double lo = tf;
                    if (mf < lo) { k = 2; lo = mf; }
                    if (ef < lo) { k = 3; lo = ef; }
                    if (hab > 0 && af < lo) { k = 5; }
                }
                whyN[k]++; whyHa[k] += area;
            }
            int land = cells[0] + cells[1] + cells[2] + cells[3];
            Console.WriteLine("margins at " + SimTime.Format(w.Now) + ": " + vil.Count + " villages, " + land + " land cells (" + (ha[0] + ha[1] + ha[2] + ha[3]).ToString("0") + " ha); fields' reach " +
                              cfg.FieldReach.ToString("0") + " m, spacing " + Sites.Spacing.ToString("0") + " m, sight " + sight.ToString("0") + " m");
            Console.WriteLine("  zone                                  cells       ha   arable now  cleared  held by fields  free (>=0.08)  waste (<0.08, cleared)");
            for (int z = 0; z < 4; z++)
                Console.WriteLine("  " + zn[z].PadRight(36) + cells[z].ToString().PadLeft(7) + ha[z].ToString("0").PadLeft(9) + arNow[z].ToString("0").PadLeft(13) + arOpen[z].ToString("0").PadLeft(9) +
                                  arHeld[z].ToString("0").PadLeft(16) + arFree[z].ToString("0").PadLeft(15) + arWaste[z].ToString("0").PadLeft(24));
            var sb = new System.Text.StringBuilder("  sites beyond the spacing by worth:");
            for (int b = 0; b <= bars.Length; b++) sb.Append(b < bars.Length ? "  >=" + bars[b].ToString("0.00") + " " + bandN[b] : "  less " + bandN[b]);
            Console.WriteLine(sb.ToString());
            sb.Clear(); sb.Append("  short of " + Sites.MinQuality.ToString("0.00") + " (the least of its worth's factors):");
            for (int k = 0; k < 6; k++) sb.Append("  " + why[k] + " " + whyN[k] + " (" + whyHa[k].ToString("0") + " ha)");
            Console.WriteLine(sb.ToString());
            // villages that could be founded at each lower bar, best first, `Sites.Spacing` apart
            cand.Sort((x, y) => x.q != y.q ? y.q.CompareTo(x.q) : x.c.CompareTo(y.c));
            List<int> lastChosen = null;
            for (int b = 0; b < bars.Length; b++)
            {
                var chosen = new List<int>();
                foreach (var (q, c) in cand)
                {
                    if (q < bars[b]) break;
                    bool ok = true;
                    for (int i = 0; i < chosen.Count && ok; i++) if (Vec3d.ArcDistance(g.Center[c], g.Center[chosen[i]], P.Radius) < Sites.Spacing) ok = false;
                    if (ok) chosen.Add(c);
                }
                double ar = 0; int inSight = 0;
                foreach (int c in chosen)
                {
                    w.Hazards.CellsWithin(w, c, cfg.FieldReach, scratch);
                    for (int i = 0; i < scratch.Count; i++) { int x = scratch[i]; if (!P.IsLand(x) || P.FieldOwner[x] != 0) continue; double ax = P.Arability(x, w.PlanetSys.MeanTemp(x)); if (ax >= 0.08) ar += P.AreaHa(x) * ax; }
                    if (nearD[c] < sight) inSight++;
                }
                Console.WriteLine("  at a bar of " + bars[b].ToString("0.00") + ": " + chosen.Count + " villages could be founded (" + inSight + " in a scout's sight), holding " + ar.ToString("0") +
                                  " arable ha - bread for ~" + (ar / cfg.FieldHaPerPerson).ToString("0") + " people");
                lastChosen = chosen;
            }
            // (C3.6g) daughter villages on a village's own margins: a cell nearer to it than to any other, inside its spacing, its houses beyond every
            // village's fields (`FieldReach` and the claims' slack of 0.75 cell), worth MinQuality with that room kept; packed that far apart, land counted once
            double gap = cfg.FieldReach + 0.75 * P.CellSize;
            var mcand = new List<(double q, int c)>();
            var withSite = new HashSet<int>();
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c) || nearD[c] < gap || nearD[c] >= Sites.Spacing || P.FieldOwner[c] != 0 || w.SettlementAtCell(c) != 0) continue;
                double q = Sites.Quality(w, c, scratch, gap);
                if (q < Sites.MinQuality) continue;
                mcand.Add((q, c)); withSite.Add(nearSid[c]);
            }
            mcand.Sort((x, y) => x.q != y.q ? y.q.CompareTo(x.q) : x.c.CompareTo(y.c));
            var mchosen = new List<int>(); var counted = new HashSet<int>(); double mar = 0;
            foreach (var (q, c) in mcand)
            {
                bool ok = true;
                for (int i = 0; i < mchosen.Count && ok; i++) if (Vec3d.ArcDistance(g.Center[c], g.Center[mchosen[i]], P.Radius) < gap) ok = false;
                if (!ok) continue;
                mchosen.Add(c);
                w.Hazards.CellsWithin(w, c, cfg.FieldReach, scratch);
                for (int i = 0; i < scratch.Count; i++) { int x = scratch[i]; if (!P.IsLand(x) || P.FieldOwner[x] != 0 || !counted.Add(x)) continue; double ax = P.Arability(x, w.PlanetSys.MeanTemp(x)); if (ax >= 0.08) mar += P.AreaHa(x) * ax; }
            }
            Console.WriteLine("  on the villages' own margins (houses " + gap.ToString("0") + " m from any village): " + mcand.Count + " cells worth " + Sites.MinQuality.ToString("0.00") + " or more, by " + withSite.Count +
                              " of " + vil.Count + " villages; " + mchosen.Count + " daughter villages could be founded, holding " + mar.ToString("0") + " arable ha - bread for ~" + (mar / cfg.FieldHaPerPerson).ToString("0") + " people");
            if (lastChosen == null || show <= 0) return;
            Console.WriteLine("  the sites at the lowest bar (best first): cell  worth  habitable  warmth  wet  low  water  free arable ha  nearest village (m)  a way on foot");
            int shown = 0;
            foreach (int c in lastChosen)
            {
                if (shown++ >= show) break;
                double q = Sites.Quality(w, c, scratch), hab = Sites.Habitability(P, c);
                double tmn = P.TMean[c], m = P.Moist[c];
                double tf = DMath.SmoothStep(4, 11, tmn) * (1.0 - DMath.SmoothStep(24, 30, tmn));
                double mf = DMath.SmoothStep(0.22, 0.4, m) * (1.0 - DMath.SmoothStep(0.85, 1.0, m));
                double ef = 1.0 - DMath.SmoothStep(500, 1100, P.Elev[c] - P.SeaLevel);
                w.Hazards.CellsWithin(w, c, cfg.FieldReach, scratch);
                double ar = 0;
                for (int i = 0; i < scratch.Count; i++) { int x = scratch[i]; if (!P.IsLand(x) || P.FieldOwner[x] != 0) continue; double ax = P.Arability(x, w.PlanetSys.MeanTemp(x)); if (ax >= 0.08) ar += P.AreaHa(x) * ax; }
                bool fresh = P.FreshWaterNear(c);
                bool way = nearSid[c] != 0 && w.Routes.GetToCell(w, nearSid[c], c) != null;
                Console.WriteLine("    " + c.ToString().PadLeft(6) + q.ToString("0.000").PadLeft(7) + hab.ToString("0.00").PadLeft(11) + (tf.ToString("0.00") + " (" + tmn.ToString("0.0") + " C)").PadLeft(15) +
                                  mf.ToString("0.00").PadLeft(6) + (ef.ToString("0.00") + " (" + (P.Elev[c] - P.SeaLevel).ToString("0") + " m)").PadLeft(14) + (fresh ? "fresh" : "shore").PadLeft(7) +
                                  ar.ToString("0.0").PadLeft(16) + (w.History.NameOf(EntityRef.Settlement(nearSid[c])) + " " + nearD[c].ToString("0")).PadLeft(28) + (way ? "yes" : "no").PadLeft(14));
            }
        }

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
        /// <summary>`place --near`: inhabited villages closer to each other than a new village may be founded (`Sites.Spacing`), with their distance,
        /// cells and the length of the way between them - two on one cell have a way of no length (the catch-up loop of session 17). Reads only.</summary>
        static void Near(World w)
        {
            var P = w.Planet;
            int pairs = 0, sameCell = 0, nil = 0, villages = 0;
            for (int a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
            {
                ref var sa = ref w.Settlements.Get(a);
                if (!sa.Inhabited) continue;
                villages++;
                for (int b = w.Settlements.Next(a); b != 0; b = w.Settlements.Next(b))
                {
                    ref var sb = ref w.Settlements.Get(b);
                    if (!sb.Inhabited) continue;
                    double d = Vec3d.ArcDistance(sa.Anchor, sb.Anchor, P.Radius);
                    if (d >= TC2.Core.Places.Sites.Spacing) continue;
                    pairs++;
                    if (sa.Cell == sb.Cell) sameCell++;
                    var r = w.Routes.Get(w, a, b);
                    if (r != null && r.Length <= 0.5) nil++;
                    Console.WriteLine("  #" + a + " " + w.History.NameOf(new EntityRef(EntityKind.Settlement, a)) + " (" + sa.Population + ", founded y" + (sa.Founded / SimTime.Year) + ", cell " + sa.Cell + ") and #" + b + " " +
                                      w.History.NameOf(new EntityRef(EntityKind.Settlement, b)) + " (" + sb.Population + ", founded y" + (sb.Founded / SimTime.Year) + ", cell " + sb.Cell + "): " + d.ToString("0") + " m apart, way " +
                                      (r == null ? "none" : r.Length.ToString("0") + " m"));
                }
            }
            Console.WriteLine("near: " + villages + " villages; " + pairs + " pairs closer than " + TC2.Core.Places.Sites.Spacing.ToString("0") + " m, " + sameCell + " on one cell, " + nil + " with a way of no length");
        }

        /// <summary>`place --heads`: who became first among a village's people and why - after a death (the event's cause), or with the one before
        /// still living (a yearly weighing, or the headman gone to live in another household: C3.6e's retirements); contested splits by the same
        /// (a split whose vacancy was a living headman's move has no cause). Reads only.</summary>
        static void Heads(World w)
        {
            var H = w.History;
            var retiredAt = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<long>>();   // (village, its eldest) -> the times of their retirements
            int retired = 0;
            for (int i = 0; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Kind != HistoryKind.Retired) continue;
                retired++;
                if (!retiredAt.TryGetValue((e.Settlement, e.Other.Id), out var ts)) retiredAt[(e.Settlement, e.Other.Id)] = ts = new System.Collections.Generic.List<long>();
                ts.Add(e.Time);
            }
            int rose = 0, afterDeath = 0, living = 0, atRetire = 0, splits = 0, splitsLiving = 0;
            int[] how = new int[3], howRetire = new int[3];
            for (int i = 0; i < H.Count; i++)
            {
                var e = H.At(i);
                if (e.Kind == HistoryKind.HeadmanRose)
                {
                    rose++; if (e.ArgB >= 0 && e.ArgB < 3) how[e.ArgB]++;
                    if (e.Cause1 != 0) afterDeath++;
                    else if (!e.Other.IsNone)
                    {
                        living++;
                        // (the weighing runs on the village's next day - in DEEP its next step, up to twelve days on - after the retirement)
                        bool r = false;
                        if (retiredAt.TryGetValue((e.Settlement, e.Other.Id), out var ts)) foreach (long t in ts) if (t <= e.Time && e.Time - t <= 13 * SimTime.Day) r = true;
                        if (r) { atRetire++; if (e.ArgB >= 0 && e.ArgB < 3) howRetire[e.ArgB]++; }
                    }
                }
                else if (e.Kind == HistoryKind.VillageSplit) { splits++; if (e.Cause1 == 0) splitsLiving++; }
            }
            Console.WriteLine("heads: " + rose + " rose (first " + how[0] + ", within the household " + how[1] + ", another household " + how[2] + "); after a death " + afterDeath +
                              ", the one before living " + living + " (of them at a retirement of theirs " + atRetire + ": within " + howRetire[1] + ", another household " + howRetire[2] + ")" +
                              "; retirements recorded " + retired + "; splits " + splitsLiving + " of " + splits + " at a living headman's move");
        }

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

        /// <summary>
        /// (C3.7a survey, `26` §2h) The croft each lived home's layout leaves it: the free ground behind and beside it (cells of `cell` m, each free by
        /// `Footprints.YardFree` - no other building, street, lane or green - nearer this home than any other, and reached from its walls through free
        /// cells), out to 9 m past either end and 24 m behind its front. By the village's form (streets or none) and the home's plan; m2 quartiles.
        /// </summary>
        static void Crofts(World w, int cell)
        {
            double c2 = cell * 0.5;
            var byForm = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<double>>();
            void Add(string k, double v) { if (!byForm.TryGetValue(k, out var l)) byForm[k] = l = new System.Collections.Generic.List<double>(); l.Add(v); }
            var homes = new System.Collections.Generic.List<int>();
            double stripSum = 0, cellSum = 0, diffSum = 0; int pairs = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited || s.Population == 0) continue;
                var bl = w.BuildingsOf(sid);
                homes.Clear();
                for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (Structures.IsHome(b.Kind) && b.Household != 0 && Homes.RuinLeft(in b, w.Now) > 0) homes.Add(bl[i]); }
                for (int hi = 0; hi < homes.Count; hi++)
                {
                    int bid = homes[hi];
                    ref var b = ref w.Buildings.Get(bid);
                    int plan = Footprints.PlanOf(in b);
                    double hx = 0, hz = 0;
                    for (int q = 0; q < Footprints.Count(plan); q++) { Footprints.Rect(plan, q, out double rcx, out double rcz, out double rhx, out double rhz); hx = Math.Max(hx, Math.Abs(rcx) + rhx); hz = Math.Max(hz, Math.Abs(rcz) + rhz); }
                    double af = Footprints.FaceOf(in s, in b), fx = Math.Cos(af), fy = Math.Sin(af), ux = -fy, uy = fx;
                    int nx = (int)Math.Ceiling((hx + 9.0) / cell), nz0 = (int)Math.Ceiling((24.0 - hz) / cell), nz1 = (int)Math.Ceiling(hz / cell);
                    int W = 2 * nx + 1, H = nz0 + nz1 + 1;
                    var free = new bool[W, H]; var seen = new bool[W, H];
                    for (int ix = 0; ix < W; ix++)
                        for (int iz = 0; iz < H; iz++)
                        {
                            double cx = (ix - nx) * cell, cz = (iz - nz0) * cell;   // (cz < 0 behind its middle)
                            if (Math.Abs(cx) < hx + c2 && Math.Abs(cz) < hz + c2) continue;   // (the house itself)
                            if (cz > hz) continue;   // (not before its front: the street side is the street's)
                            if (!Footprints.YardFree(w, sid, bid, cx, cz, c2, c2)) continue;
                            double px = b.X + ux * cx + fx * cz, py = b.Y + uy * cx + fy * cz;
                            double own = (px - b.X) * (px - b.X) + (py - b.Y) * (py - b.Y);
                            bool nearer = true;
                            for (int hj = 0; hj < homes.Count && nearer; hj++)
                            {
                                if (hj == hi) continue;
                                ref var o = ref w.Buildings.Get(homes[hj]);
                                if ((px - o.X) * (px - o.X) + (py - o.Y) * (py - o.Y) < own) nearer = false;
                            }
                            free[ix, iz] = nearer;
                        }
                    // reached from its walls: a flood from the free cells touching the house
                    var stack = new System.Collections.Generic.Stack<(int, int)>();
                    for (int ix = 0; ix < W; ix++)
                        for (int iz = 0; iz < H; iz++)
                        {
                            if (!free[ix, iz]) continue;
                            double cx = (ix - nx) * cell, cz = (iz - nz0) * cell;
                            if (Math.Abs(cx) < hx + c2 + cell && Math.Abs(cz) < hz + c2 + cell) { seen[ix, iz] = true; stack.Push((ix, iz)); }
                        }
                    int n = 0;
                    while (stack.Count > 0)
                    {
                        var (ix, iz) = stack.Pop(); n++;
                        for (int d = 0; d < 4; d++)
                        {
                            int jx = ix + (d == 0 ? 1 : d == 1 ? -1 : 0), jz = iz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (jx < 0 || jz < 0 || jx >= W || jz >= H || seen[jx, jz] || !free[jx, jz]) continue;
                            seen[jx, jz] = true; stack.Push((jx, jz));
                        }
                    }
                    double m2 = n * cell * cell;
                    // the strips' estimate the simulation keeps (Gardens.Strips, C3.7a: 4 m strips behind to 16 m, beside to 8 m, short of half the way
                    // to the neighbours) beside the cells' measure
                    {
                        double back = 16.0, left = 8.0, right = 8.0, ux2 = -fy, uy2 = fx;
                        for (int hj = 0; hj < homes.Count; hj++)
                        {
                            if (hj == hi) continue;
                            ref var o2 = ref w.Buildings.Get(homes[hj]);
                            double dx = (double)o2.X - b.X, dy = (double)o2.Y - b.Y;
                            double along = -(dx * fx + dy * fy), across = dx * ux2 + dy * uy2;
                            if (along > 0 && Math.Abs(across) < hx + 6.0) back = Math.Min(back, along * 0.5 - hz);
                            if (Math.Abs(along) < hz + 4.0) { if (across > 0) right = Math.Min(right, across * 0.5 - hx); else left = Math.Min(left, -across * 0.5 - hx); }
                        }
                        double area = 0, wx = hx + 2.0, cw = wx * 2.0 / 3.0;
                        for (int k = -1; k <= 1; k++)
                            for (double d = 0.0; d + 2.0 <= back + 1e-9; d += 2.0) { if (!Footprints.YardFree(w, sid, bid, k * cw, -(hz + d + 1.0), cw * 0.5, 1.0)) break; area += 2.0 * cw; }
                        double zf = hz - 1.0, zb = -(hz + 4.0), zm = 0.5 * (zf + zb), hzs = 0.25 * (zf - zb);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double room = side > 0 ? right : left;
                            for (int row = 0; row < 2; row++)
                                for (double d = 0.0; d + 4.0 <= room + 1e-9; d += 4.0) { if (!Footprints.YardFree(w, sid, bid, side * (hx + d + 2.0), row == 0 ? zm + hzs : zm - hzs, 2.0, hzs)) break; area += 8.0 * hzs; }
                        }
                        area = Math.Min(320.0, area);
                        Add("strips", area); stripSum += area; cellSum += Math.Min(320.0, m2); pairs++; diffSum += Math.Abs(area - Math.Min(320.0, m2));
                    }
                    string form = s.StreetN > 0 ? "streets" : "no streets";
                    Add(form, m2);
                    Add("plan " + plan, m2);
                    Add("all", m2);
                }
            }
            Console.WriteLine("crofts at " + SimTime.FormatShort(w.Now) + " (cells of " + cell + " m; free ground behind and beside each lived home, nearer it than any other, reached from its walls):");
            Console.WriteLine("  the strips' estimate against the cells (each capped at 320 m2): mean " + (stripSum / Math.Max(1, pairs)).ToString("0") + " m2 against " + (cellSum / Math.Max(1, pairs)).ToString("0") + ", mean absolute difference " + (diffSum / Math.Max(1, pairs)).ToString("0") + " m2 over " + pairs + " homes");
            var keys = new System.Collections.Generic.List<string>(byForm.Keys); keys.Sort(string.CompareOrdinal);
            foreach (var k in keys)
            {
                var l = byForm[k]; l.Sort();
                double q(double f) => l[Math.Min(l.Count - 1, (int)(f * l.Count))];
                int none = 0; foreach (var v in l) if (v < 20) none++;
                Console.WriteLine("  " + k.PadRight(12) + " homes " + l.Count.ToString().PadLeft(5) + "  m2 q10/q25/median/q75/q90 " + q(0.1).ToString("0") + "/" + q(0.25).ToString("0") + "/" + q(0.5).ToString("0") + "/" + q(0.75).ToString("0") + "/" + q(0.9).ToString("0") + "  under 20 m2 " + none);
            }
        }
    }
}
