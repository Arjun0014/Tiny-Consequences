using System;
using System.Globalization;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// The History Book of a world lived N years (D2-077; F7's done-when): its ages found after the fact, each chapter's numbers
        /// from the annals and its most important events, every line with its record - and a check that every line stands on one
        /// (an event the ledger holds, whose causes can be walked; or the annals' years). With --why, each chapter's lines' causes.
        /// Usage: chronicle [--seed N] [--years Y] [--coarse] [--lines L] [--why] [--places]
        /// </summary>
        public static int ChronicleBook(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            if (a.Has("coarse")) w.RequestResolution(Resolution.Coarse, w.Now + 1);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            w.AdvanceTo(w.Now + SimTime.Years(a.Double("years", 100)));
            Console.WriteLine("a world of seed " + w.Seed + " lived " + F(a.Double("years", 100), "0") + " years in " + F(watch.Elapsed.TotalSeconds, "0") + " s: " + w.LivingPeople() + " people in " + w.InhabitedSettlements() + " places; annals of " + w.Annals.Count + " years, " + w.History.Count + " events kept");
            watch.Restart();
            var eras = Chronicle.Eras(w);
            int lines = a.Int("lines", 10), bad = 0, total = 0, unwalkable = 0;
            foreach (var era in eras)
            {
                Console.WriteLine();
                Console.WriteLine("== " + era.Name + " (years " + era.From + " to " + era.To + (era.Present ? ", to this day" : "") + ")");
                foreach (var l in Chronicle.Chapter(w, era, lines))
                {
                    total++;
                    if (l.Event != 0)
                    {
                        if (!w.History.TryGet(l.Event, out _)) { bad++; Console.WriteLine("  [missing " + l.Event + "] " + l.Text); continue; }
                        Console.WriteLine("  [" + l.Event + "] " + l.Text);
                        string why = HistoryText.Why(w, l.Event);
                        if (why.Length == 0) unwalkable++;
                        if (a.Has("why")) foreach (var row in why.Split('\n')) if (row.Length > 0 && row.Contains("<-")) Console.WriteLine("        " + row.Trim());
                    }
                    else
                    {
                        bool ok = l.AnnalFrom >= 0 && l.AnnalTo <= w.Annals.Count && l.AnnalTo > l.AnnalFrom;
                        if (!ok) bad++;
                        Console.WriteLine("  [annals " + (ok ? w.Annals[l.AnnalFrom].Year + "-" + w.Annals[l.AnnalTo - 1].Year : "missing") + "] " + l.Text);
                    }
                }
            }
            if (a.Has("places"))
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    Console.WriteLine();
                    Console.WriteLine("== " + w.History.NameOf(EntityRef.Settlement(sid)) + (w.Settlements.Get(sid).Inhabited ? "" : " (abandoned)"));
                    foreach (var l in Chronicle.Place(w, sid, lines)) { total++; if (!w.History.TryGet(l.Event, out _)) bad++; Console.WriteLine("  [" + l.Event + "] " + l.Text); }
                }
            Console.WriteLine();
            Console.WriteLine("the book: " + eras.Count + " ages, " + total + " lines, " + bad + " without their record, " + unwalkable + " whose causes cannot be walked; written in " + F(watch.Elapsed.TotalMilliseconds, "0") + " ms");
            return bad == 0 ? 0 : 1;
        }
    }
}
