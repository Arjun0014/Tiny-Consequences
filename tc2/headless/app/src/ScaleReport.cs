using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TC2.Core.Diagnostics;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    /// <summary>
    /// The planet's size seen from the inside (D2-085, tools only): how far apart the villages lie, how many a day's walk reaches,
    /// how many separate communities the walks make, how far kin and contacts reach, the polities, and what a person-day costs.
    /// `run --scale`: a short line at each report, the whole report at the end.
    /// </summary>
    public static class ScaleReport
    {
        const double DayWalk = 6000.0;   // m: the reach of visits, trade, marriage, relief, taking and raids (Trade, Conflict, World)
        static double _villageYears, _personDays, _lastT = -1;

        static string F(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);

        public static void Begin(World w) { ScaleTally.Sums = new double[ScaleTally.Kinds * ScaleTally.Stride]; _lastT = w.Now; _villageYears = 0; _personDays = 0; }

        /// <summary>Accumulates village-years and person-days lived since the last call; returns the short line.</summary>
        public static string Line(World w)
        {
            double dt = (w.Now - _lastT) / (double)SimTime.Day; _lastT = w.Now;
            _villageYears += w.InhabitedSettlements() * dt / SimTime.DaysPerYear;
            _personDays += w.LivingPeople() * dt;
            var s = Snapshot(w);
            return "      scale: villages " + s.Villages + "  nearest " + F(s.NearestKm, "0.0") + " km  within a day's walk " + F(s.WithinWalk, "0.0") +
                   "  communities " + s.Communities + " (largest " + F(100 * s.LargestShare, "0") + " % of people)  reachable people " + F(s.Reachable, "0");
        }

        sealed class Snap
        {
            public int Villages, People, Communities, LandParts, InhabitedParts, MaxPerPart, Polities, Tributaries, LargestPolity;
            public double HomeMean, HomeMax; public int Ruins, Homes;
            public double LandKm2, NearestKm, WithinWalk, Within3, Reachable, LargestShare, Hops, KinKm, KinAway, KinN, WoodsNear, WoodsAll, LargestPartKm2;
        }

        static Snap Snapshot(World w)
        {
            var P = w.Planet;
            var g = P.Grid;
            var sn = new Snap();
            var ids = new List<int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Inhabited) ids.Add(sid);
            int n = ids.Count;
            sn.Villages = n; sn.People = w.LivingPeople();
            double land = 0;
            for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) land += P.AreaHa(c);
            sn.LandKm2 = land / 100.0;
            // land parts (Nbr4 over land) and the villages on each
            var part = new int[P.Cells];
            int parts = 0; double largest = 0;
            var q = new Queue<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || part[c] != 0) continue;
                parts++; double a = 0;
                part[c] = parts; q.Enqueue(c);
                while (q.Count > 0)
                {
                    int x = q.Dequeue(); a += P.AreaHa(x);
                    for (int k = 0; k < 4; k++) { int nb = g.Nbr4[x * 4 + k]; if (P.IsLand(nb) && part[nb] == 0) { part[nb] = parts; q.Enqueue(nb); } }
                }
                if (a > largest) largest = a;
            }
            sn.LandParts = parts; sn.LargestPartKm2 = largest / 100.0;
            var perPart = new Dictionary<int, int>();
            foreach (int sid in ids) { int pc = part[w.Settlements.Get(sid).Cell]; perPart.TryGetValue(pc, out int k0); perPart[pc] = k0 + 1; }
            sn.InhabitedParts = perPart.Count;
            foreach (var kv in perPart) sn.MaxPerPart = Math.Max(sn.MaxPerPart, kv.Value);
            if (n == 0) return sn;
            // distances between villages
            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    d[i, j] = d[j, i] = Vec3d.ArcDistance(w.Settlements.Get(ids[i]).Anchor, w.Settlements.Get(ids[j]).Anchor, P.Radius);
            double nearSum = 0; int nearN = 0; double walkSum = 0, w3 = 0, reachSum = 0;
            for (int i = 0; i < n; i++)
            {
                double best = double.MaxValue; int within = 0, within3 = 0; double reach = w.Settlements.Get(ids[i]).Population;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (d[i, j] < best) best = d[i, j];
                    if (d[i, j] <= DayWalk) { within++; reach += w.Settlements.Get(ids[j]).Population; }
                    if (d[i, j] <= 3000) within3++;
                }
                if (best < double.MaxValue) { nearSum += best; nearN++; }
                walkSum += within; w3 += within3;
                reachSum += reach * w.Settlements.Get(ids[i]).Population;   // (weighted by the village's people: what a person reaches)
            }
            sn.NearestKm = nearN > 0 ? nearSum / nearN / 1000.0 : 0;
            sn.WithinWalk = walkSum / n; sn.Within3 = w3 / n;
            int pop = 0; foreach (int sid in ids) pop += w.Settlements.Get(sid).Population;
            sn.Reachable = pop > 0 ? reachSum / pop : 0;
            // communities: villages joined by walks of a day or less; hops between villages within one
            var comm = new int[n]; int cn = 0; int bestPop = 0; double hopSum = 0; int hopN = 0;
            for (int i = 0; i < n; i++)
            {
                if (comm[i] != 0) continue;
                cn++; int cp = 0; var members = new List<int>();
                comm[i] = cn; var qq = new Queue<int>(); qq.Enqueue(i);
                while (qq.Count > 0) { int x = qq.Dequeue(); members.Add(x); cp += w.Settlements.Get(ids[x]).Population; for (int j = 0; j < n; j++) if (comm[j] == 0 && d[x, j] <= DayWalk) { comm[j] = cn; qq.Enqueue(j); } }
                if (cp > bestPop) bestPop = cp;
                foreach (int src in members)
                {
                    var hop = new Dictionary<int, int> { [src] = 0 }; var bq = new Queue<int>(); bq.Enqueue(src);
                    while (bq.Count > 0) { int x = bq.Dequeue(); foreach (int y in members) if (!hop.ContainsKey(y) && d[x, y] <= DayWalk) { hop[y] = hop[x] + 1; bq.Enqueue(y); } }
                    foreach (var kv in hop) if (kv.Key != src) { hopSum += kv.Value; hopN++; }
                }
            }
            sn.Communities = cn; sn.LargestShare = pop > 0 ? bestPop / (double)pop : 0; sn.Hops = hopN > 0 ? hopSum / hopN : 0;
            // kin: grown people and their living mothers - how far apart their homes are
            double kinSum = 0; int kinN = 0, away = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || p.AgeYears(w.Now) < 16 || p.Mother == 0 || !w.People.Exists(p.Mother)) continue;
                ref var m = ref w.People.Get(p.Mother);
                if (!m.Alive || !w.Settlements.Exists(p.Home) || !w.Settlements.Exists(m.Home)) continue;
                kinN++;
                if (p.Home != m.Home) { away++; kinSum += Vec3d.ArcDistance(w.Settlements.Get(p.Home).Anchor, w.Settlements.Get(m.Home).Anchor, P.Radius); }
            }
            sn.KinN = kinN; sn.KinAway = kinN > 0 ? away / (double)kinN : 0; sn.KinKm = away > 0 ? kinSum / away / 1000.0 : 0;
            // polities: lordships by their root lord
            var size = new Dictionary<int, int>();
            foreach (int sid in ids)
            {
                int r = sid, guard = 0;
                while (w.Settlements.Get(r).Lord != 0 && w.Settlements.Exists(w.Settlements.Get(r).Lord) && guard++ < 64) r = w.Settlements.Get(r).Lord;
                if (r != sid) sn.Tributaries++;
                size.TryGetValue(r, out int k1); size[r] = k1 + 1;
            }
            foreach (var kv in size) if (kv.Value > 1) { sn.Polities++; sn.LargestPolity = Math.Max(sn.LargestPolity, kv.Value); }
            // the woods round the villages (1.5 km) against all the land's
            var cells = new List<int>(); double fw = 0, fa = 0;
            foreach (int sid in ids)
            {
                w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, 1500, cells);
                foreach (int c in cells) if (P.IsLand(c)) { fw += P.Forest[c] * P.AreaHa(c); fa += P.AreaHa(c); }
            }
            sn.WoodsNear = fa > 0 ? fw / fa : 0;
            double fl = 0, la = 0; for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) { fl += P.Forest[c] * P.AreaHa(c); la += P.AreaHa(c); }
            sn.WoodsAll = la > 0 ? fl / la : 0;
            // the villages' shape: how far their lived-in homes lie from the square (mean, and the farthest), and their ruins
            double sumMean = 0, sumMax = 0; int vs = 0, ruins = 0, homes = 0;
            foreach (int sid in ids)
            {
                ref var st = ref w.Settlements.Get(sid);
                var bl = w.BuildingsOf(sid);
                double sd = 0, mx = 0; int k = 0;
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (b.Household == 0) { ruins++; continue; }
                    double dx = (double)b.X - st.SquareX, dy = (double)b.Y - st.SquareY, dd = Math.Sqrt(dx * dx + dy * dy);
                    sd += dd; k++; if (dd > mx) mx = dd;
                }
                homes += k;
                if (k > 0) { sumMean += sd / k; sumMax += mx; vs++; }
            }
            sn.HomeMean = vs > 0 ? sumMean / vs : 0; sn.HomeMax = vs > 0 ? sumMax / vs : 0; sn.Ruins = ruins; sn.Homes = homes;
            return sn;
        }

        public static void Final(World w, double ms, double years)
        {
            var s = Snapshot(w);
            var sb = new StringBuilder();
            sb.AppendLine("SCALE radius " + F(w.Planet.Radius / 1000.0, "0.0") + " km  cell " + F(w.Planet.CellSize, "0") + " m  land " + F(s.LandKm2, "0") + " km2 in " + s.LandParts + " parts (largest " + F(s.LargestPartKm2, "0") + " km2)");
            sb.AppendLine("SCALE people " + s.People + "  villages " + s.Villages + "  per km2 of land " + F(s.People / Math.Max(1e-9, s.LandKm2), "0.0") + "  people a village " + F(s.People / (double)Math.Max(1, s.Villages), "0") +
                          "  inhabited land parts " + s.InhabitedParts + " (most villages on one " + s.MaxPerPart + ")");
            sb.AppendLine("SCALE nearest village " + F(s.NearestKm, "0.00") + " km  within a day's walk (6 km) " + F(s.WithinWalk, "0.0") + "  within 3 km " + F(s.Within3, "0.0") +
                          "  communities " + s.Communities + " (largest " + F(100 * s.LargestShare, "0") + " % of people; hops between its villages " + F(s.Hops, "0.0") + ")  people a person can reach in a day " + F(s.Reachable, "0"));
            sb.AppendLine("SCALE villages' shape: homes lived in " + s.Homes + ", mean " + F(s.HomeMean, "0") + " m from the square, the farthest " + F(s.HomeMax, "0") + " m (village means); ruins and empty homes " + s.Ruins);
            sb.AppendLine("SCALE kin: grown people with a living mother " + F(s.KinN, "0") + ", " + F(100 * s.KinAway, "0") + " % in another village, " + F(s.KinKm, "0.00") + " km away" +
                          "  polities " + s.Polities + " (tributaries " + s.Tributaries + ", largest " + s.LargestPolity + " villages)  woods round villages " + F(s.WoodsNear, "0.00") + " / all land " + F(s.WoodsAll, "0.00"));
            var t = ScaleTally.Sums;
            if (t != null)
            {
                for (int k = 0; k < ScaleTally.Kinds; k++)
                {
                    int o = k * ScaleTally.Stride;
                    double cnt = t[o]; if (cnt <= 0) continue;
                    double other = cnt - t[o + 1];
                    double mean = other > 0 ? t[o + 2] / other : 0;
                    double sd = other > 1 ? Math.Sqrt(Math.Max(0, t[o + 3] / other - mean * mean)) : 0;
                    sb.AppendLine("SCALE contact " + ScaleTally.Names[k].PadRight(9) + F(cnt, "0").PadLeft(8) + "  per village-year " + F(cnt / Math.Max(1e-9, _villageYears), "0.000").PadLeft(7) +
                                  "  same village " + F(100 * t[o + 1] / cnt, "0").PadLeft(3) + " %  between villages " + F(mean, "0.00") + " km (sd " + F(sd, "0.00") + ", max " + F(t[o + 4], "0.0") +
                                  ", beyond 3 km " + F(other > 0 ? 100 * t[o + 5] / other : 0, "0") + " %)");
                }
            }
            // the wild kinds alive and how many of each (a small world holds fewer of the great beasts)
            var P = w.Planet; var ws = new StringBuilder("SCALE wild:");
            for (int k = 0; k < P.Species.Count; k++)
            {
                var sp = P.Species[k]; if (sp.Gone != 0) { ws.Append(" " + TC2.Core.Planet.Ecology.KindName(sp.Kind) + " gone,"); continue; }
                double tot = 0; for (int c = 0; c < P.Cells; c++) tot += P.Abund[k * P.Cells + c];
                ws.Append(" " + TC2.Core.Planet.Ecology.KindName(sp.Kind) + " " + F(tot, "0") + ",");
            }
            sb.AppendLine(ws.ToString());
            sb.AppendLine("SCALE cost " + F(ms / Math.Max(1e-9, years), "0") + " ms a world year, " + F(1000.0 * ms / Math.Max(1.0, _personDays), "0.00") + " us a person-day lived (" + F(_personDays / SimTime.DaysPerYear, "0") + " person-years, " + F(_villageYears, "0") + " village-years)");
            Console.Write(sb.ToString());
        }
    }
}
