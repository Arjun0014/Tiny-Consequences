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
        /// `wet --load SAVE [--days 2]` (D2-111's rule audited: nobody stands in the sea): the world lived from its next midnight, hour by hour; every
        /// living person not out in a boat whose place is under the drawn water (`Boats.Afloat`) - counted by what they are doing, with examples.
        /// </summary>
        public static int Wet(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
            if (w.StepDays > 1) w.RequestStepDays(1, next0);
            if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0);
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
    }
}
