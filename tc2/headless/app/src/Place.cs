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
            if (a.Has("households")) { ListHouseholds(w, a.Int("sid", 0) != 0 ? a.Int("sid", 0) : PickPlace(w, "big")); return 0; }
            int sid = a.Int("sid", 0);
            if (sid == 0) sid = PickPlace(w, a.Get("pick", "big"));
            if (sid == 0) { Console.WriteLine("no place to pick"); return 1; }
            Console.Write(Gazetteer.All(w, sid));
            return 0;
        }

        /// <summary>
        /// `place --load SAVE --list` (D2-114, picking villages to look at): each inhabited place - its people, age, wall, lord, tradition, and its
        /// standing houses by plan (small/cottage/long/two/hall), roof cover and walls, and its works.
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
                int[] plan = new int[5], cover = new int[5], walls = new int[5]; int huts = 0, works = 0, lords = 0;
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
                Console.WriteLine("#" + id + " " + w.History.NameOf(EntityRef.Settlement(id)) + ": " + s.Population + " people, " + (year - (int)(s.Founded / SimTime.Year)) + " y old, wall " + s.Wall +
                                  (s.Lord != 0 ? ", tributary of #" + s.Lord : "") + (lords > 0 ? ", lord of " + lords : "") + ", line " + Architecture.Line(s.Tradition) + " roof " + Architecture.RoofName(Architecture.TraditionRoof(s.Tradition)) +
                                  " shade " + Architecture.TraditionPalette(s.Tradition) + "; huts " + huts + ", plans " + J(plan) + ", covers " + J(cover) + ", walls " + J(walls) + "; works " + works + " (" + kinds + ")" + "; households " + hl.Count + " means <1/1-1.5/1.5-3/3+ " + J(mb) + ", with cattle " + cattle + ", of 8+ " + big);
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
            Console.WriteLine("#" + sid + " " + w.History.NameOf(EntityRef.Settlement(sid)) + " " + s.Population + " people, granary " + s.Granary.ToString("0") + " FU");
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
                                  ", cattle " + cattle + " small " + small + ", boat " + (hh.Boat != 0 ? 1 : 0) + ", formed " + ((w.Now - hh.Formed) / (double)SimTime.Year).ToString("0") + " y, from square " + System.Math.Sqrt(dx * dx + dy * dy).ToString("0") + " m, means " + Architecture.Means(w, hl[i]).ToString("0.00") + ": " + form);
            }
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
