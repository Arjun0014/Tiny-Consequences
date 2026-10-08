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
            if (a.Has("overlaps")) { Overlaps(w, a.Int("sid", 0)); return 0; }
            if (a.Has("layout")) { Layout(w, a.Int("sid", 0) != 0 ? a.Int("sid", 0) : PickPlace(w, "big")); return 0; }
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
                                  (TC2.Core.Places.Weaving.Shed(w, id) != 0 ? ", WEAVER'S SHED" : "") + ", wares " + s.Wares.ToString("0") + " (worn " + (100.0 * s.WaresHeld / Math.Max(1.0, TC2.Core.Places.Goods.Target * s.Population)).ToString("0") + "%, sold " + s.WaresSold.ToString("0") + ")" +
                                  (s.Lord != 0 ? ", tributary of #" + s.Lord : "") + (lords > 0 ? ", lord of " + lords : "") + ", traders " + s.TradeIn.ToString("0.0") + (s.Market != 0 ? " market" : "") + ", line " + Architecture.Line(s.Tradition) + " roof " + Architecture.RoofName(Architecture.TraditionRoof(s.Tradition)) +
                                  " shade " + Architecture.TraditionPalette(s.Tradition) + "; huts " + huts + ", plans " + J(plan) + ", covers " + J(cover) + ", walls " + J(walls) + "; works " + works + " (" + kinds + ")" + "; households " + hl.Count + " means <1/1-1.5/1.5-3/3+ " + J(mb) + ", with cattle " + cattle + ", of 8+ " + big);
            }
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
