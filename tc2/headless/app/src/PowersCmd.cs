using System;
using System.Collections.Generic;
using System.IO;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    /// <summary>
    /// (session 32: every power checked) `powers --load FILE --power NAME [--off M] [--years Y]`: casts one of the fifteen powers as the bar does (its Arg)
    /// at the most peopled village of a saved world (or M metres east of its square), lives it day by day for 16 days at ACTIVITY, then COARSE to Y years,
    /// and prints what the cast changed - the villages within 1.5 km (people, crop, growth, tiredness, soil water, sick, buildings standing), the beasts
    /// round it, the vents and deposits - and the records rooted in the cast (by Cause1's line) and linked to it by either cause, by kind. An observer.
    /// </summary>
    public static class PowersCmd
    {
        static string Timeline;
        static readonly string[] Names ={ "lightning", "storm", "rain", "drought", "warmth", "cold", "quake", "eruption", "meteor", "plague", "blight", "bounty", "cull", "bring", "ore" };

        public static int Run(Args a)
        {
            string p = a.Get("power", "meteor");
            if (p == "all") { foreach (var n in Names) { One(a, n); Console.WriteLine(); } return 0; }
            return One(a, p);
        }

        static int One(Args a, string p)
        {
            CommandKind k; long arg = 0;
            switch (p)
            {
                case "lightning": k = CommandKind.GodLightning; break;
                case "storm": k = CommandKind.GodStorm; arg = 50; break;
                case "rain": k = CommandKind.GodRain; break;
                case "drought": k = CommandKind.GodDrought; break;
                case "warmth": k = CommandKind.GodWarm; break;
                case "cold": k = CommandKind.GodCold; break;
                case "quake": k = CommandKind.GodQuake; arg = 65; break;
                case "eruption": k = CommandKind.GodEruption; arg = 3; break;
                case "meteor": k = CommandKind.GodMeteor; break;
                case "plague": k = CommandKind.GodPlague; break;
                case "blight": k = CommandKind.GodBlight; break;
                case "bounty": k = CommandKind.GodBounty; break;
                case "cull": k = CommandKind.GodCull; break;
                case "bring": k = CommandKind.GodBring; break;
                case "ore": k = CommandKind.GodOre; break;
                default: Console.WriteLine("unknown power " + p); return 2;
            }
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day + a.Long("day", 0) * SimTime.Day;
            if (w.StepDays > 1) w.RequestStepDays(1, next0);
            if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
            w.AdvanceTo(next0 + SimTime.Hours(9));
            int sid = 0, best = -1;
            for (int v = w.Settlements.First(); v != 0; v = w.Settlements.Next(v)) { ref var s = ref w.Settlements.Get(v); if (s.Inhabited && s.Population > best) { best = s.Population; sid = v; } }
            ref var vs = ref w.Settlements.Get(sid);
            double off = a.Double("off", 0);
            int cell = vs.Cell;
            if (off > 0) cell = w.Planet.Grid.CellOf((vs.Anchor + vs.East * (off / w.Planet.Radius)).Normalized);
            Vec3d at = w.Planet.Grid.Center[cell];
            Console.WriteLine("== " + p + " (" + k + ", arg " + arg + ") at " + w.History.NameOf(EntityRef.Settlement(sid)) + " (" + best + " people) " + (off > 0 ? off + " m east, " : "") + "cell " + cell
                              + " (" + Math.Round(Vec3d.ArcDistance(at, vs.Anchor, w.Planet.Radius)) + " m from its square; cell size " + Math.Round(w.Planet.CellSize) + " m), " + SimTime.Format(w.Now));
            {
                // the nearest lived-in home to the cell's heart (a bolt's roof)
                double hd = double.MaxValue;
                for (int v = w.Settlements.First(); v != 0; v = w.Settlements.Next(v))
                {
                    ref var s = ref w.Settlements.Get(v);
                    if (!s.Inhabited || Vec3d.ArcDistance(s.Anchor, at, w.Planet.Radius) > 900.0) continue;
                    var hl = w.HouseholdsOf(v);
                    for (int i = 0; i < hl.Count; i++)
                    {
                        ref var hh = ref w.Households.Get(hl[i]);
                        if (hh.Building == 0 || !w.Buildings.Exists(hh.Building)) continue;
                        ref var b = ref w.Buildings.Get(hh.Building);
                        if (TC2.Core.Places.Homes.Fallen(in b, w.Now)) continue;
                        double d = Vec3d.ArcDistance(at, s.LocalToSphere(b.X, b.Y, w.Planet.Radius), w.Planet.Radius);
                        if (d < hd) hd = d;
                    }
                }
                Console.WriteLine("   the nearest home lived in is " + Math.Round(hd) + " m from the cell's heart");
            }
            var near = new List<int>();
            for (int v = w.Settlements.First(); v != 0; v = w.Settlements.Next(v)) { ref var s = ref w.Settlements.Get(v); if (s.Inhabited && Vec3d.ArcDistance(at, s.Anchor, w.Planet.Radius) <= 1500) near.Add(v); }
            near.Sort((x, y) => Vec3d.ArcDistance(at, w.Settlements.Get(x).Anchor, w.Planet.Radius).CompareTo(Vec3d.ArcDistance(at, w.Settlements.Get(y).Anchor, w.Planet.Radius)));
            State(w, near, at, "before");
            long mark = w.History.LastId;
            try
            {
                w.Issue(new Command { Time = w.Now, Kind = k, Cell = cell, Arg = arg });
                w.AdvanceTo(w.Now + SimTime.Minutes(30));
            }
            catch (Exception e) { Console.WriteLine("THROWN at the cast: " + e); return 1; }
            long cast = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.RefAt(i).Kind == HistoryKind.GodCommand) { cast = w.History.RefAt(i).Id; break; }
            if (cast == 0) { Console.WriteLine("no cast recorded"); return 1; }
            long t0 = w.Now;
            try
            {
                foreach (int d in new[] { 0, 1, 5, 16 })
                {
                    if (d > 0) w.AdvanceTo(t0 + d * SimTime.Day);
                    Console.WriteLine("-- +" + d + " days, " + SimTime.Format(w.Now));
                    State(w, near, at, null); Ledger(w, cast);
                }
                double years = a.Double("years", 3);
                if (years > 0)
                {
                    long n1 = SimTime.StartOfDay(w.Now) + SimTime.Day;
                    w.RequestResolution(Resolution.Coarse, n1);
                    int steps = (int)Math.Ceiling(years * 4);
                    for (int st = 1; st <= steps; st++)
                    {
                        w.AdvanceTo(t0 + (long)(st * SimTime.Year / 4));
                        if (st % 4 != 0 && st != steps) continue;
                        Console.WriteLine("-- +" + (st / 4.0) + " years, " + SimTime.Format(w.Now));
                        State(w, near, at, null); Ledger(w, cast);
                    }
                }
                if (a.Has("timeline")) Timeline = "," + a.Get("timeline") + ",";
                if (a.Has("place")) Place(w, cast, sid);
            }
            catch (Exception e) { Console.WriteLine("THROWN after the cast: " + e); return 1; }
            return 0;
        }

        static void State(World w, List<int> near, Vec3d at, string tag)
        {
            var P = w.Planet;
            int sick = 0, hurt = 0, alive = 0;
            for (int j = 0; j < near.Count; j++)
            {
                var res = w.Residents(near[j]);
                for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (!q.Alive) continue; alive++; if (q.SickPhase > 0) sick++; if (q.Hurt > 0.05f) hurt++; }
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("   near ").Append(near.Count).Append(" villages: ").Append(alive).Append(" people, sick ").Append(sick).Append(", hurt ").Append(hurt).Append(" | ");
            for (int j = 0; j < Math.Min(4, near.Count); j++)
            {
                ref var s = ref w.Settlements.Get(near[j]);
                int stand = 0; var bl = w.BuildingsOf(near[j]);
                for (int i = 0; i < bl.Count; i++) { ref var b = ref w.Buildings.Get(bl[i]); if (!TC2.Core.Places.Homes.Fallen(in b, w.Now)) stand++; }
                double soil = s.FieldCell >= 0 && P.SoilWater != null ? P.SoilWater[s.FieldCell] : -1;
                sb.Append(w.History.NameOf(EntityRef.Settlement(near[j]))).Append(" ").Append(Math.Round(Vec3d.ArcDistance(at, s.Anchor, P.Radius))).Append("m: pop ").Append(s.Population)
                  .Append(" crop ").Append(s.Crop).Append(" g").Append(s.CropGrowth.ToString("0.00")).Append(" burnt").Append(s.CropBurnt.ToString("0.00")).Append(" cc").Append(s.CropCause != 0 ? "y" : "n")
                  .Append(" tired ").Append(s.Tired.ToString("0.00")).Append(" soil ").Append(soil.ToString("0.00")).Append(" gran ").Append(Math.Round(s.Granary)).Append(" bldg ").Append(stand)
                  .Append(s.Epidemic != 0 ? " EPI" : "").Append("; ");
            }
            Console.WriteLine(sb.ToString());
            // the beasts within 900 m of the place, hunters and game
            if (P.Abund != null && P.Abund.Length > 0)
            {
                int n = P.Cells; double hunt = 0, game = 0;
                for (int q = 0; q < n; q++)
                {
                    if (Vec3d.ArcDistance(at, P.Grid.Center[q], P.Radius) > World.GodReach) continue;
                    for (int s = 0; s < P.Species.Count; s++) { double v = P.Abund[s * n + q]; if (v <= 0) continue; if (P.Species[s].Prey != 0) hunt += v; else game += v; }
                }
                int vents = 0; for (int i = 0; i < P.Vents.Count; i++) if (Vec3d.ArcDistance(at, P.Vents[i].At, P.Radius) <= 1500) vents++;
                Console.WriteLine("   beasts within 900 m: hunters " + Math.Round(hunt, 1) + ", game " + Math.Round(game, 1) + "; vents within 1.5 km " + vents + "; fires " + w.Fire.Active.Count + "; storms " + P.Storms.Count + "; air shift here " + w.AirShift(P.Grid.CellOf(at)) + (tag != null ? " (" + tag + ")" : ""));
            }
        }

        static void Ledger(World w, long cast)
        {
            var h = w.History;
            int ci = h.IndexOf(cast);
            if (ci < 0) { Console.WriteLine("   the cast is gone from the ledger"); return; }
            var mine = new HashSet<long> { cast };
            var byKind = new SortedDictionary<string, int>(); var second = new SortedDictionary<string, int>();
            int rooted = 0, clos = 0;
            for (int i = ci + 1; i < h.Count; i++)
            {
                ref var e = ref h.RefAt(i);
                bool r = e.RootKind == RootKind.You && e.Root == cast;
                bool c = mine.Contains(e.Cause1) || (e.Cause2 != 0 && mine.Contains(e.Cause2));
                if (r) { rooted++; Bump(byKind, e.Kind.ToString()); }
                if (r || c) { mine.Add(e.Id); clos++; if (!r) Bump(second, e.Kind.ToString()); }
            }
            Console.WriteLine("   rooted " + rooted + ": " + Line(byKind));
            if (second.Count > 0) Console.WriteLine("   linked by a second cause only " + (clos - rooted) + ": " + Line(second));
            try { var t = Consequences.Build(w, cast); Console.WriteLine("   tree " + t.Records + " records, " + t.Nodes.Count + " cards, records by tier " + string.Join("/", t.TierRecords)); }
            catch (Exception e) { Console.WriteLine("   TREE THROWN: " + e.Message); }
        }

        /// <summary>The records at the village since the cast, by kind: those rooted in the cast and the rest (what a cast's tree may be missing).</summary>
        static void Place(World w, long cast, int sid)
        {
            var h = w.History; int ci = h.IndexOf(cast);
            var mine = new SortedDictionary<string, int>(); var other = new SortedDictionary<string, int>();
            for (int i = ci + 1; i < h.Count; i++)
            {
                ref var e = ref h.RefAt(i);
                if (e.Settlement != sid) continue;
                if (e.RootKind == RootKind.You && e.Root == cast) Bump(mine, e.Kind.ToString()); else Bump(other, e.Kind.ToString() + "/" + e.RootKind);
            }
            if (Timeline != null)
            {
                int deaths = 0; long lastDay = -1;
                for (int i = ci + 1; i < h.Count; i++)
                {
                    ref var e = ref h.RefAt(i);
                    if (e.Settlement != sid || Timeline.IndexOf("," + e.Kind + ",") < 0) continue;
                    if (e.Kind == HistoryKind.PersonDied) { long day = e.Time / SimTime.Day; if (day == lastDay) { deaths++; continue; } if (deaths > 0) Console.WriteLine("      (+" + deaths + " more deaths that day)"); deaths = 0; lastDay = day; }
                    string c1 = "-"; if (e.Cause1 != 0 && h.TryGet(e.Cause1, out var ce)) c1 = ce.Kind + "#" + ce.Id; else if (e.Cause1 != 0) c1 = "gone#" + e.Cause1;
                    Console.WriteLine("    " + SimTime.Format(e.Time) + " " + e.Kind + "#" + e.Id + " cause " + c1 + " root " + e.RootKind + (e.Root == cast ? " (the cast)" : "") + " argA " + e.ArgA);
                }
                if (deaths > 0) Console.WriteLine("      (+" + deaths + " more deaths that day)");
            }
            Console.WriteLine("   at the village, rooted in the cast: " + Line(mine));
            Console.WriteLine("   at the village, not: " + Line(other));
        }

        static void Bump(SortedDictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }
        static string Line(SortedDictionary<string, int> d) { var sb = new System.Text.StringBuilder(); foreach (var kv in d) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", "); return sb.ToString().TrimEnd(',', ' '); }
    }
}
