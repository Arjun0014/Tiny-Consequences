using System;
using System.Collections.Generic;
using System.IO;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Headless
{
    public static partial class Commands
    {
        sealed class DugoutBooking
        {
            public int Boat, Hh, From, To; public byte Kind; public long At, Last; public string State = "";
        }

        sealed class VoyageSeen
        {
            public int Boat, From, To, Crew, Skipper;
            public bool Dugout;
            public long First, Last, MidFrom, MidTo;
            public string Load = "nothing aboard";
        }

        /// <summary>
        /// `voyages --load SAVE [--days 96] [--dugouts]` (C9.5b shots): the world lived from its next midnight, as the game loads it, looked at every five
        /// minutes; each lived voyage as its crew puts out and comes home - its boat (a dugout or planks), its village and where it goes, its crew and its
        /// load, and when it is a third to two-thirds over the water on its way out (what the `voyage` shot frames) - in world time and in the setting-out
        /// village's own day and hour: `unity.mjs shots --load SAVE --village FROM --days D --hour H` (half an hour before) frames it.
        /// </summary>
        public static int Voyages(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            long save0 = w.Now;
            long next0 = LiveToDay(w, 0, -1);
            int days = a.Int("days", 96);
            bool dugouts = a.Has("dugouts");
            long end = next0 + days * SimTime.Day, step = 5 * SimTime.Minute;
            Console.WriteLine("voyages: " + SimTime.Format(save0) + " loaded; lived from " + SimTime.Format(next0) + " for " + days + " days, looked at every 5 minutes" + (dugouts ? " (dugouts only)" : ""));
            var open = new Dictionary<int, VoyageSeen>();
            var done = new List<VoyageSeen>();
            var aboard = new HashSet<int>();
            // (each booking of a dugout followed to its fate: sailed - its boat out on a voyage when the booking is gone - or not, and what its
            // household was doing the last time it was seen booked)
            var dbs = new Dictionary<long, DugoutBooking>();
            var dbDone = new List<DugoutBooking>();
            var dbSeen = new HashSet<long>();
            int all = 0, dug = 0;
            for (long t = next0 + step; t <= end; t += step)
            {
                w.AdvanceTo(t);
                aboard.Clear();
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    ref var p = ref w.People.Get(id);
                    if (!p.Alive || p.Voyage == 0) continue;
                    int bid = p.Voyage;
                    aboard.Add(bid);
                    if (!open.TryGetValue(bid, out var v))
                    {
                        bool dugout = w.Buildings.Exists(bid) && !Boats.IsPlank(w.Buildings.Get(bid).Kind);
                        v = new VoyageSeen { Boat = bid, From = p.Home, To = p.Dest, Dugout = dugout, First = t };
                        open[bid] = v;
                    }
                    v.Last = t;
                    if (p.CargoQty > 0f && v.Skipper == 0) { v.Skipper = id; v.Load = (p.CargoQty * Trade.KgOf(p.Cargo)).ToString("0") + " kg of " + GoodName(p.Cargo) + " (" + CargoToName(p.CargoTo) + ")"; }
                    if (p.Act == ActivityKind.Voyage && p.Step == StepKind.Travel && p.At != p.Dest)
                    {
                        double f = p.T1 > p.T0 ? (t - p.T0) / (double)(p.T1 - p.T0) : 0;
                        if (f > 0.33 && f < 0.67 && v.MidTo == 0) { if (v.MidFrom == 0) v.MidFrom = t; }
                        else if (v.MidFrom != 0 && v.MidTo == 0 && f >= 0.67) v.MidTo = t;
                    }
                }
                dbSeen.Clear();
                for (int i = 0; i < w.Trade.Bookings; i++)
                {
                    var bk = w.Trade.BookingAt(i);
                    if (!w.Buildings.Exists(bk.Boat) || Boats.IsPlank(w.Buildings.Get(bk.Boat).Kind)) continue;
                    long key = bk.At * 1000003L + bk.Boat;
                    dbSeen.Add(key);
                    if (!dbs.TryGetValue(key, out var db)) { db = new DugoutBooking { Boat = bk.Boat, Hh = bk.Hh, From = bk.From, To = bk.To, Kind = bk.Kind, At = bk.At }; dbs[key] = db; }
                    db.Last = t;
                    var acts = new SortedDictionary<string, int>();
                    var mem = w.Members(bk.Hh);
                    for (int m = 0; m < mem.Count; m++)
                    {
                        ref var q = ref w.People.Get(mem[m]);
                        if (!q.Alive) continue;
                        string k = q.Act.ToString() + (Trade.CanSail(in q, bk.From, t) ? "+" : "") + (q.AgeYears(t) < 16 ? "(child)" : "");
                        acts.TryGetValue(k, out int c); acts[k] = c + 1;
                    }
                    var parts = new List<string>(); foreach (var kv in acts) parts.Add(kv.Key + " x" + kv.Value);
                    long localSec = SimTime.FloorMod(t + w.LocalOffsetOf(bk.From), SimTime.Day);
                    db.State = (localSec / (double)SimTime.Hour).ToString("0.0") + " h: " + string.Join(", ", parts);
                }
                var gone = new List<long>();
                foreach (var kv in dbs) if (!dbSeen.Contains(kv.Key)) gone.Add(kv.Key);
                gone.Sort();
                foreach (long key in gone) { var db = dbs[key]; if (aboard.Contains(db.Boat)) db.State = "SAILED"; dbDone.Add(db); dbs.Remove(key); }
                foreach (var kv in open) kv.Value.Crew = 0;
                for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.Voyage != 0 && open.TryGetValue(p.Voyage, out var v)) v.Crew++; }
                var ended = new List<int>();
                foreach (var kv in open) if (!aboard.Contains(kv.Key)) ended.Add(kv.Key);
                ended.Sort();
                foreach (int bid in ended) { done.Add(open[bid]); open.Remove(bid); }
            }
            foreach (var kv in open) done.Add(kv.Value);
            done.Sort((x, y) => x.First.CompareTo(y.First));
            foreach (var v in done)
            {
                all++; if (v.Dugout) dug++;
                if (dugouts && !v.Dugout) continue;
                long local = w.Settlements.Exists(v.From) ? w.LocalOffsetOf(v.From) : 0;
                long at = v.MidFrom != 0 ? v.MidFrom : v.First;
                long day0 = SimTime.StartOfDay(save0 + local), dayAt = SimTime.StartOfDay(at + local);
                double hour = (at + local - dayAt) / (double)SimTime.Hour;
                Console.WriteLine("  " + (v.Dugout ? "dugout" : "planks") + " " + v.Boat + " of " + Name(w, v.From) + " (" + v.From + ") to " + Name(w, v.To) + " (" + v.To + "): out " + SimTime.Format(v.First) +
                                  ", home " + SimTime.Format(v.Last) + "; " + v.Load + "; mid-way out " + (v.MidFrom != 0 ? SimTime.Format(v.MidFrom) + " - " + (v.MidTo != 0 ? SimTime.Format(v.MidTo) : "?") : "never seen") +
                                  " = its village's day " + ((dayAt - day0) / SimTime.Day) + " from the save's, " + hour.ToString("0.00") + " h");
            }
            Console.WriteLine("voyages lived: " + all + " (in dugouts " + dug + ")");
            foreach (var kv in dbs) dbDone.Add(kv.Value);
            int sailed = 0;
            var why = new SortedDictionary<string, int>();
            foreach (var db in dbDone)
            {
                if (db.State == "SAILED") { sailed++; continue; }
                string k = db.State.Contains(": ") ? db.State.Substring(db.State.IndexOf(": ") + 2) : db.State;
                why.TryGetValue(k, out int c); why[k] = c + 1;
            }
            Console.WriteLine("dugout bookings: " + dbDone.Count + ", sailed " + sailed + "; not sailed, by what the household was doing when last seen booked (+ could sail):");
            foreach (var kv in why) Console.WriteLine("  " + kv.Value + "  " + kv.Key);
            int shown = 0;
            foreach (var db in dbDone)
            {
                if (db.State == "SAILED" || shown++ >= 12) continue;
                Console.WriteLine("  e.g. dugout " + db.Boat + " of household " + db.Hh + " at " + Name(w, db.From) + " to " + Name(w, db.To) + " (kind " + db.Kind + "): booked " + SimTime.Format(db.At) + ", last seen " + SimTime.Format(db.Last) + " - " + db.State);
            }
            return 0;
        }

        static string Name(World w, int sid) => sid != 0 && w.Settlements.Exists(sid) ? w.History.NameOf(EntityRef.Settlement(sid)) : "?";

        static string GoodName(byte g) => g == Trade.Grain ? "grain" : g == Trade.Metal ? "copper" : g == Trade.Fibre ? "fibre" : g == Trade.Hides ? "hides" : g == Trade.Iron ? "iron" :
                                          g == Trade.Wares ? "wares" : g == Trade.Pots ? "pots and jars" : "goods";

        static string CargoToName(byte to) =>
            to == Trade.ToLord ? "the lord's tenth" : to == Trade.ToTenth ? "for the tenth" : to == Trade.TenthAboard ? "the tenth aboard" : to == Trade.AsGift ? "relief" :
            to == Trade.AsReturn ? "grain given back" : to == Trade.ToFetch ? "fetched" : to == Trade.ToStore ? "to its store" : "trade";
    }
}
