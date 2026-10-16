using System;
using System.Collections.Generic;
using System.IO;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// Tools (session 21): a loaded world lived on at its own resolution to the midnight before village `sid`'s own day `doy` of its year (its calendar:
        /// the southern seasons half a year on), then in lived days from that midnight; -1 lives from the next midnight as before. Returns that midnight.
        /// </summary>
        static long LiveToDay(World w, int sid, int doy)
        {
            long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
            if (doy >= 0 && sid != 0 && w.Settlements.Exists(sid))
            {
                long local = w.LocalOffsetOf(sid);
                int now0 = SettlementSystem.DayOfYear(in w.Settlements.Get(sid), w.Now);
                int days = (doy - now0 + SimTime.DaysPerYear) % SimTime.DaysPerYear;
                long midnight = SimTime.StartOfDay(w.Now + local) - local + days * SimTime.Day;
                while (midnight <= w.Now) midnight += SimTime.DaysPerYear * SimTime.Day;
                if (midnight - SimTime.Day > w.Now) w.AdvanceTo(midnight - SimTime.Day);   // (the save's own resolution, to the day before)
                next0 = midnight;
            }
            if (w.StepDays > 1) w.RequestStepDays(1, next0);
            if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
            return next0;
        }

        /// <summary>The first inhabited village, or `--sid` (tools).</summary>
        static int SidArg(World w, Args a)
        {
            int sid = a.Int("sid", 0);
            if (sid != 0 && w.Settlements.Exists(sid)) return sid;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (w.Settlements.Get(s).Inhabited) return s;
            return 0;
        }

        /// <summary>
        /// `wet --load SAVE [--days 2] [--doy D [--sid S]]` (D2-111's rule audited: nobody stands in the sea): the world lived from its next midnight (or the
        /// midnight that begins village S's own day D: the first village's by default), hour by hour; every living person not out in a boat whose place is
        /// under the drawn water (`Boats.Afloat`) - counted by what they are doing, with examples.
        /// </summary>
        public static int Wet(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            long next0 = LiveToDay(w, SidArg(w, a), a.Int("doy", -1));
            w.AdvanceTo(next0 + SimTime.Hour);
            int days = a.Int("days", 2);
            var P = w.Planet;
            // standing buildings whose ground is under the drawn water now (a lake over it, or the sea within the home's two metres)
            int homesWet = 0, worksWet = 0, homesLake = 0, worksLake = 0, homes = 0;
            var wetBy = new Dictionary<string, int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (Homes.Fallen(in b, w.Now) || Boats.IsBoat(b.Kind)) continue;
                    bool home = Structures.IsHome(b.Kind);
                    if (home) homes++;
                    var d = s.LocalToSphere(b.X, b.Y, P.Radius);
                    if (Shore.Dry(P, d)) continue;
                    Shore.At(P, d, out int c, out double lake);
                    bool byLake = lake > 0.3;
                    if (home) { homesWet++; if (byLake) homesLake++; } else { worksWet++; if (byLake) worksLake++; }
                    string k = w.History.NameOf(EntityRef.Settlement(sid)) + (home ? " homes" : " " + Structures.Name(b.Kind)) + (byLake ? " (lake " + lake.ToString("0.0") + " m)" : " (sea)");
                    wetBy.TryGetValue(k, out int n0); wetBy[k] = n0 + 1;
                }
            }
            Console.WriteLine("standing buildings not on dry ground now: homes " + homesWet + " of " + homes + " (" + homesLake + " under a lake), works " + worksWet + " (" + worksLake + " under a lake)");
            foreach (var kv in wetBy) Console.WriteLine("  " + kv.Key + " " + kv.Value);
            var byAct = new Dictionary<string, int>();
            var seen = new Dictionary<string, int>();
            var byVillage = new Dictionary<string, int>();
            long samples = 0, wet = 0;
            int shown = 0;
            for (int h = 0; h < days * 24; h++)
            {
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive) continue;
                    if (p.Act == ActivityKind.Sail || p.Act == ActivityKind.Voyage) continue;   // (out in a boat)
                    samples++;
                    var pos = w.Agents.PositionAt(p, w.Now);
                    if (!Boats.Afloat(P, pos)) continue;
                    wet++;
                    string k = p.Act + "/" + p.Why + "/" + p.Step;
                    byAct.TryGetValue(k, out int n); byAct[k] = n + 1;
                    string vk = (p.Home != 0 && w.Settlements.Exists(p.Home) ? w.History.NameOf(EntityRef.Settlement(p.Home)) : "-") + " " + p.Act;
                    byVillage.TryGetValue(vk, out int vn); byVillage[vk] = vn + 1;
                    seen.TryGetValue(k, out int sn); seen[k] = sn + 1;
                    if (sn < 3 && shown < 40)
                    {
                        shown++;
                        string at = p.Home != 0 && w.Settlements.Exists(p.Home) ? w.History.NameOf(EntityRef.Settlement(p.Home)) : "-";
                        double dry = 0;
                        if (p.Home != 0 && w.Settlements.Exists(p.Home))
                        {
                            ref var s = ref w.Settlements.Get(p.Home);
                            s.SphereToLocal(pos, P.Radius, out double x, out double y);
                            double x0 = x, y0 = y;
                            Boats.OutOfTheWater(w, in s, ref x, ref y);
                            dry = Math.Sqrt((x - x0) * (x - x0) + (y - y0) * (y - y0));
                        }
                        Console.WriteLine("  " + SimTime.Format(w.Now) + " #" + id + " of " + at + ": " + k + " cart " + Carts.Usable(w, p.Household) + ", " + dry.ToString("0") + " m from dry ground");
                    }
                }
            }
            Console.WriteLine("WET " + wet + " of " + samples + " person-hours under the drawn water (" + (100.0 * wet / Math.Max(1, samples)).ToString("0.000") + " %):");
            var keys = new List<string>(byAct.Keys);
            keys.Sort((x, y) => byAct[y].CompareTo(byAct[x]));
            foreach (var k in keys) Console.WriteLine("  " + k + " " + byAct[k]);
            var vkeys = new List<string>(byVillage.Keys);
            vkeys.Sort((x, y) => byVillage[y].CompareTo(byVillage[x]));
            Console.WriteLine("by village and act:");
            for (int i = 0; i < Math.Min(20, vkeys.Count); i++) Console.WriteLine("  " + vkeys[i] + " " + byVillage[vkeys[i]]);
            return 0;
        }

        /// <summary>
        /// `crowds --load SAVE --doy D [--hour 11] [--sid S] [--reach 2500] [--top 6]` (session 21: a crowd in a shot is measured, not guessed): the world
        /// lived to village S's own day D at its hour H; everyone within `reach` m of it at their simulated place (`PositionAt`; not those out in boats);
        /// the densest spots - most others within 60 m, each spot taking the people within 120 m of it out of the running - with what they do and why,
        /// whose households, and the ground (a field of whose village, or the waste); then each activity's densest spot, as the `act:` shot views find it.
        /// </summary>
        public static int Crowds(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            int sid = SidArg(w, a);
            if (sid == 0) { Console.WriteLine("no village"); return 1; }
            long midnight = LiveToDay(w, sid, a.Int("doy", -1));
            w.AdvanceTo(midnight + DMath.ToLong(a.Double("hour", 11.0) * SimTime.Hour));
            var P = w.Planet;
            var s = w.Settlements.Get(sid);
            double reach = a.Double("reach", 2500.0);
            var ids = new List<int>(); var xs = new List<double>(); var ys = new List<double>(); var cells = new List<int>();
            var byAct = new Dictionary<string, int>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || p.Act == ActivityKind.Sail || p.Act == ActivityKind.Voyage) continue;
                var pos = w.Agents.PositionAt(p, w.Now);
                if (Vec3d.ArcDistance(pos, s.Anchor, P.Radius) > reach) continue;
                s.SphereToLocal(pos, P.Radius, out double x, out double y);
                ids.Add(id); xs.Add(x); ys.Add(y); cells.Add(P.Grid.CellOf(pos));
                string k = p.Act.ToString(); byAct.TryGetValue(k, out int n0); byAct[k] = n0 + 1;
            }
            int n = ids.Count;
            Console.WriteLine("crowds round " + w.History.NameOf(EntityRef.Settlement(sid)) + " (" + s.Population + " people) at " + SimTime.Format(w.Now) + ", its day " +
                              SettlementSystem.DayOfYear(in s, w.Now) + " (" + s.Crop + "): " + n + " people within " + reach.ToString("0") + " m");
            var acts = new List<string>(byAct.Keys); acts.Sort((x, y) => byAct[y].CompareTo(byAct[x]));
            Console.WriteLine("  by what they do: " + string.Join(", ", acts.ConvertAll(k => k + " " + byAct[k])));
            var near = new int[n];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double dx = xs[i] - xs[j], dy = ys[i] - ys[j];
                    if (dx * dx + dy * dy <= 3600.0) { near[i]++; near[j]++; }
                }
            var taken = new bool[n];
            int top = a.Int("top", 6);
            for (int k = 0; k < top; k++)
            {
                int best = -1;
                for (int i = 0; i < n; i++) if (!taken[i] && (best < 0 || near[i] > near[best])) best = i;
                if (best < 0 || near[best] < 3) break;
                var mix = new Dictionary<string, int>(); var homes = new Dictionary<string, int>(); var hh = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    double dx = xs[i] - xs[best], dy = ys[i] - ys[best], d2 = dx * dx + dy * dy;
                    if (d2 <= 3600.0)
                    {
                        var p = w.People.Get(ids[i]);
                        string m = p.Act + "/" + p.Why; mix.TryGetValue(m, out int c0); mix[m] = c0 + 1;
                        string h = p.Home != 0 && w.Settlements.Exists(p.Home) ? w.History.NameOf(EntityRef.Settlement(p.Home)) : "-"; homes.TryGetValue(h, out int c1); homes[h] = c1 + 1;
                        hh.Add(p.Household);
                    }
                    if (d2 <= 14400.0) taken[i] = true;
                }
                int cell = cells[best], fo = P.FieldOwner[cell];
                double dist = Math.Sqrt(xs[best] * xs[best] + ys[best] * ys[best]), bearing = Math.Atan2(xs[best], ys[best]) * 180.0 / Math.PI;
                var ml = new List<string>(mix.Keys); ml.Sort((x, y) => mix[y].CompareTo(mix[x]));
                Console.WriteLine("  spot " + (k + 1) + ": " + (near[best] + 1) + " within 60 m, " + dist.ToString("0") + " m from the square at " + bearing.ToString("0") + " deg (x " + xs[best].ToString("0") + ", y " +
                                  ys[best].ToString("0") + "), cell " + cell + (fo != 0 ? " a field of " + w.History.NameOf(EntityRef.Settlement(fo)) : " not a field") + (Boats.Afloat(P, P.Grid.Center[cell]) ? " (the cell's middle under water)" : "") +
                                  "; " + hh.Count + " households; " + string.Join(", ", ml.ConvertAll(m => m + " " + mix[m])) + " | from " + string.Join(", ", new List<string>(homes.Keys).ConvertAll(h => h + " " + homes[h])));
            }
            // each activity's densest spot (the act: shot views' rule)
            var bestOf = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string k = w.People.Get(ids[i]).Act.ToString();
                int same = 0;
                for (int j = 0; j < n; j++)
                {
                    if (i == j || w.People.Get(ids[j]).Act.ToString() != k) continue;
                    double dx = xs[i] - xs[j], dy = ys[i] - ys[j];
                    if (dx * dx + dy * dy <= 3600.0) same++;
                }
                if (!bestOf.TryGetValue(k, out int b) || same > b) bestOf[k] = same;
            }
            Console.WriteLine("  each activity's busiest spot (others at the same work within 60 m): " + string.Join(", ", acts.ConvertAll(k => k + " " + (bestOf.TryGetValue(k, out int b2) ? b2 : 0))));
            return 0;
        }
    }
}
