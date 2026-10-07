using System;
using System.IO;
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
            int sid = a.Int("sid", 0);
            if (sid == 0) sid = PickPlace(w, a.Get("pick", "big"));
            if (sid == 0) { Console.WriteLine("no place to pick"); return 1; }
            Console.Write(Gazetteer.All(w, sid));
            return 0;
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
