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
        /// <summary>
        /// The History Book as a book (session 25, `HistoryBook`): `book --load SAVE [--md FILE] [--noids]` - the whole book of a saved
        /// world as Markdown (to FILE, or the console), its parts and chapters counted, every line checked to stand on a record the
        /// ledger holds, and the time it took to write. Reads only.
        /// </summary>
        public static int WholeBook(Args a)
        {
            var w = TC2.Core.Persistence.SaveFile.Load(System.IO.File.ReadAllBytes(a.Get("load")), out _);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var bw = new HistoryBook.Writer(w);
            var times = new System.Text.StringBuilder("first reading " + F(bw.LastMs, "0") + " ms");
            for (int i = 0; i < HistoryBook.PartTitles.Length; i++) { bw.Book.Parts.Add(bw.Part(i)); times.Append(", " + HistoryBook.Roman(i + 1) + " " + F(bw.LastMs, "0")); }
            var book = bw.Book;
            double ms = watch.Elapsed.TotalMilliseconds;
            Console.WriteLine("timing: " + times);
            int chapters = 0, lines = 0, bad = 0;
            foreach (var p in book.Parts)
                foreach (var ch in p.Chapters)
                {
                    chapters++;
                    foreach (var b in ch.Blocks) if (b.Kind == BookKind.Line) { lines++; if (!w.History.TryGet(b.Event, out _)) bad++; }
                }
            string md = HistoryBook.Markdown(book, !a.Has("noids"));
            if (a.Has("md")) System.IO.File.WriteAllText(a.Get("md"), md); else Console.WriteLine(md);
            Console.WriteLine("the book of year " + book.Year + ": " + book.Parts.Count + " parts, " + chapters + " chapters, " + lines + " lines (" + bad + " without their record), " + md.Length + " characters; written in " + F(ms, "0") + " ms from " + w.History.Count + " events and " + w.Annals.Count + " years of annals");
            return bad == 0 ? 0 : 1;
        }

        public static int ChronicleBook(Args a)
        {
            var w = WorldGenerator.Create(ConfigFrom(a));
            if (a.Has("coarse") || a.Has("deep")) w.RequestResolution(Resolution.Coarse, w.Now + 1);
            if (a.Has("deep")) w.RequestStepDays(a.Int("deep", 12), w.Now + 1);   // DEEP (F8)
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
