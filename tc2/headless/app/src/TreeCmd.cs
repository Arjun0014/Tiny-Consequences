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
    /// (session 32: what came of it) `tree --load FILE --power meteor|drought|plague|blight|quake|storm|cull|cold [--years Y] [--coarse]`: casts a power at
    /// the most peopled village of a saved world, lives Y years and prints, each year, the cast's tree (Consequences.Build) - how many records, cards, tiers
    /// and how far on the last one came - and what the ledger links to the cast by either cause (the forward closure over Cause1 and Cause2) against what
    /// its root (Cause1's line alone) holds. An observer: the tree and the closure read the ledger only.
    /// </summary>
    public static class TreeCmd
    {
        public static int Run(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            Console.WriteLine("loaded at " + SimTime.Format(w.Now) + ", " + w.InhabitedSettlements() + " villages, " + w.LivingPeople() + " people, ledger " + w.History.Count);
            long next0 = SimTime.StartOfDay(w.Now) + SimTime.Day;
            if (a.Has("coarse")) w.RequestResolution(Resolution.Coarse, next0);
            else { if (w.StepDays > 1) w.RequestStepDays(1, next0); if (w.Resolution != Resolution.Activity) w.RequestResolution(Resolution.Activity, next0); }
            w.AdvanceTo(next0 + SimTime.Hours(9));
            int sid = 0, best = -1;
            for (int v = w.Settlements.First(); v != 0; v = w.Settlements.Next(v)) { ref var s = ref w.Settlements.Get(v); if (s.Inhabited && s.Population > best) { best = s.Population; sid = v; } }
            string p = a.Get("power", "meteor");
            CommandKind k = p == "drought" ? CommandKind.GodDrought : p == "plague" ? CommandKind.GodPlague : p == "blight" ? CommandKind.GodBlight : p == "quake" ? CommandKind.GodQuake
                          : p == "storm" ? CommandKind.GodStorm : p == "cull" ? CommandKind.GodCull : p == "cold" ? CommandKind.GodCold : p == "eruption" ? CommandKind.GodEruption : CommandKind.GodMeteor;
            long mark = w.History.LastId;
            Console.WriteLine("cast " + k + " at " + w.History.NameOf(EntityRef.Settlement(sid)) + " (" + best + " people)");
            w.Issue(new Command { Time = w.Now, Kind = k, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Hours(2));
            long cast = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.RefAt(i).Kind == HistoryKind.GodCommand) { cast = w.History.RefAt(i).Id; break; }
            if (cast == 0) { Console.WriteLine("no cast recorded"); return 1; }
            double years = a.Double("years", 3);
            long t0 = w.Now;
            int steps = (int)Math.Ceiling(years * 4);
            for (int st = 1; st <= steps; st++)
            {
                w.AdvanceTo(t0 + (long)(st * SimTime.Year / 4));
                if (st % 4 != 0 && st != steps && st > 1) continue;
                Report(w, cast, a.Has("v") && st == steps);
            }
            return 0;
        }

        static void Report(World w, long cast, bool print)
        {
            var h = w.History;
            int ci = h.IndexOf(cast);
            if (ci < 0) { Console.WriteLine(SimTime.Format(w.Now) + ": the cast is gone from the ledger"); return; }
            long ct = h.RefAt(ci).Time;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var t = Consequences.Build(w, cast);
            long ms = sw.ElapsedMilliseconds;
            int[] tiers = new int[Consequences.Tiers]; long lastCard = 0;
            foreach (var n in t.Nodes) { tiers[(int)n.Tier]++; if (n.Index > 0 && n.First - ct > lastCard) lastCard = n.First - ct; }
            // the forward closure: a record is the cast's when either cause is
            var mine = new HashSet<long> { cast };
            int rooted = 0, closure = 0, viaSecond = 0;
            var lateRoot = new SortedDictionary<string, int>(); var lateClos = new SortedDictionary<string, int>();
            long lastRoot = 0, lastClos = 0;
            for (int i = ci + 1; i < h.Count; i++)
            {
                ref var e = ref h.RefAt(i);
                bool r = e.RootKind == RootKind.You && e.Root == cast;
                bool c = mine.Contains(e.Cause1) || (e.Cause2 != 0 && mine.Contains(e.Cause2));
                if (r) { rooted++; if (e.Time - ct > SimTime.Year) Bump(lateRoot, e.Kind.ToString()); if (e.Time - ct > lastRoot) lastRoot = e.Time - ct; }
                if (c || r)
                {
                    mine.Add(e.Id); closure++;
                    if (!r) { viaSecond++; if (e.Time - ct > SimTime.Year) Bump(lateClos, e.Kind.ToString()); }
                    if (e.Time - ct > lastClos) lastClos = e.Time - ct;
                }
            }
            Console.WriteLine(SimTime.Format(w.Now) + ": tree " + t.Records + " records" + (t.Capped ? " (capped)" : "") + ", " + t.Nodes.Count + " cards (cast/at once/days/that year/years/decades " + string.Join("/", tiers) + "; records " + string.Join("/", t.TierRecords) + "), depth " + t.MaxDepth
                              + ", last card " + Consequences.Offset(lastCard) + ", last rooted record " + Consequences.Offset(lastRoot) + "; closure by either cause " + closure + " (" + viaSecond + " not rooted, last " + Consequences.Offset(lastClos) + "); built in " + ms + " ms");
            if (lateRoot.Count > 0) Console.WriteLine("  rooted, a year on and later: " + Line(lateRoot));
            if (lateClos.Count > 0) Console.WriteLine("  linked by a second cause only, a year on and later: " + Line(lateClos));
            if (print) Print(t, 0);
        }

        static void Bump(SortedDictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }
        static string Line(SortedDictionary<string, int> d) { var sb = new System.Text.StringBuilder(); foreach (var kv in d) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", "); return sb.ToString().TrimEnd(',', ' '); }

        static void Print(ConsequenceTree t, int i)
        {
            var n = t.Nodes[i];
            Console.WriteLine("  " + new string(' ', 2 * n.Depth) + n.Title + "  [" + n.When + ", " + n.Tier + ", " + n.Total + (n.Below > 0 ? ", " + n.Below + " below" : "") + "]");
            foreach (int c in n.Children) Print(t, c);
        }
    }
}
