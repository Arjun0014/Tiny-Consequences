using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>Diagnostics: people's states after a run (who is hungry, what they are doing).</summary>
        public static int Probe(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            long span = SpanFrom(a, 2);
            w.AdvanceTo(w.Now + span);
            var byAct = new Dictionary<string, int>();
            int hungry = 0;
            var rows = new List<string>();
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                string key = p.Act + "/" + p.Step;
                byAct[key] = byAct.TryGetValue(key, out int v) ? v + 1 : 1;
                double sinceMeal = (w.Now - p.LastMeal) / 3600.0;
                if (sinceMeal > 12) hungry++;
                if (rows.Count < a.Int("rows", 25))
                    rows.Add("#" + id + " age " + p.AgeYears(w.Now).ToString("0.0", CultureInfo.InvariantCulture) + " " + p.Act + "/" + p.Step + " why " + p.Why +
                             " at " + p.At + " home " + p.Home + " T0 " + SimTime.FormatShort(p.T0) + " T1 " + SimTime.FormatShort(p.T1) +
                             " meal " + sinceMeal.ToString("0.0", CultureInfo.InvariantCulture) + "h ago health " + p.Health.ToString("0.00", CultureInfo.InvariantCulture) + " ver " + p.ActVer);
            }
            Console.WriteLine("at " + SimTime.Format(w.Now) + ": living " + w.LivingPeople() + ", unfed > 12 h: " + hungry);
            foreach (var kv in byAct.OrderBy(k => k.Key, StringComparer.Ordinal)) Console.WriteLine("  " + kv.Key + ": " + kv.Value);
            foreach (var r in rows) Console.WriteLine(r);
            // queue audit: live step events per person
            var live = new Dictionary<int, int>();
            for (int i = 0; i < w.Queue.RawCount; i++)
            {
                var e = w.Queue.RawAt(i);
                if (e.Kind != TC2.Core.Scheduling.EventKind.AgentStep) continue;
                if (!w.People.Exists(e.Target)) continue;
                ref var p = ref w.People.Get(e.Target);
                if (p.Alive && p.ActVer == e.Version) live[e.Target] = live.TryGetValue(e.Target, out int n) ? n + 1 : 1;
            }
            int none = 0, multi = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                live.TryGetValue(id, out int n);
                if (n == 0) none++; else if (n > 1) multi++;
            }
            Console.WriteLine("audit: living without a live step event " + none + ", with several " + multi);
            return 0;
        }
    }
}
