using System;
using System.IO;
using TC2.Core.Agents;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// The person page in text (C12.1): `person --load SAVE [--pid N | --pick oldest|smith|fighter|elder|child] [--village ID]` - the same
        /// page the HUD shows, from the world's state and its ledger.
        /// </summary>
        public static int PersonPage(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            int pid = a.Int("pid", 0);
            if (pid == 0) pid = Pick(w, a.Get("pick", "oldest"), a.Int("village", 0));
            if (pid == 0) { Console.WriteLine("nobody to pick"); return 1; }
            Console.Write(Biography.All(w, pid));
            return 0;
        }

        /// <summary>A living person of a village (0: any) by a kind: the oldest, a smith, the best fighter, an elder, a child.</summary>
        public static int Pick(World w, string kind, int village)
        {
            int best = 0; double bv = double.MinValue;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive || (village != 0 && p.Home != village)) continue;
                double age = p.AgeYears(w.Now), v;
                switch (kind)
                {
                    case "smith": v = p.SkSmith * 100 + p.KMetal; break;   // (the most practised smith: C1.4b)
                    case "fighter": v = p.SkFight * 100 + p.Fights; break;
                    case "weaver": v = p.SkCraft * 100 + p.KLoom; break;   // (the most practised maker at a weaver's shed: C3.1)
                    case "elder": v = age > 50 ? 1000 - age : -1; break;
                    case "child": v = age < 12 ? age : -1; break;
                    default: v = age; break;
                }
                if (v > bv) { bv = v; best = id; }
            }
            return best;
        }
    }
}
