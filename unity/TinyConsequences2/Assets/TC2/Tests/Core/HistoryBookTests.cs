using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The History Book as a book (session 25, HistoryBook): written after the fact from the ledger and the annals in eight parts; every
    /// line stands on its record and says what the record says; the same world writes the same book; realms are rebuilt year by year
    /// from their bonds (tiers counted under the highest lord) and agree with the tribute the world holds now; raids between two villages
    /// are one war until a bow between them or twelve quiet years; the threads of cause run root first through what they passed.
    /// Historians read; they never write the world.
    /// </summary>
    [TestFixture]
    public class HistoryBookTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static long Rec(World w, HistoryKind k, int subj, int obj, int argA = 0, int argB = 0, long cause = 0, int at = 0) =>
            w.Record(new HistoryEvent { Kind = k, Time = w.Now, Settlement = at != 0 ? at : subj, Cell = -1, Subject = EntityRef.Settlement(subj), Object = EntityRef.Settlement(obj), ArgA = argA, ArgB = argB, Cause1 = cause });

        static void Years(World w, double y) => w.AdvanceTo(w.Now + SimTime.Years(y));

        [Test]
        public void TheBookIsWrittenFromTheLedgerAndEveryLineStandsOnItsRecord()
        {
            var w = TestWorlds.Make(607);
            Coarse(w);
            Years(w, 90);
            ulong h0 = w.StateHash();
            var book = HistoryBook.Write(w);
            Assert.AreEqual(HistoryBook.PartTitles.Length, book.Parts.Count, "eight parts");
            for (int i = 0; i < book.Parts.Count; i++) Assert.AreEqual(HistoryBook.PartTitles[i], book.Parts[i].Title, "the contents name the parts as written");
            Assert.Greater(book.Preface.Count, 0);
            Assert.Greater(book.Parts[0].Chapters.Count, 0, "the ages");
            Assert.Greater(book.Parts[4].Chapters.Count, 0, "the places");
            int lines = 0;
            foreach (var p in book.Parts)
                foreach (var ch in p.Chapters)
                {
                    Assert.IsNotEmpty(ch.Title);
                    foreach (var b in ch.Blocks)
                    {
                        if (b.Kind != BookKind.Line) { Assert.IsNotEmpty(b.Text); continue; }
                        lines++;
                        Assert.IsTrue(w.History.TryGet(b.Event, out var e), "every line is an event the ledger holds");
                        Assert.AreEqual(HistoryText.Dated(w, e), b.Text, "and says what the record says, under its date");
                    }
                }
            Assert.Greater(lines, 20);
            string md = HistoryBook.Markdown(book);
            Console.WriteLine(md.Length > 3000 ? md.Substring(0, 3000) : md);
            Assert.AreEqual(md, HistoryBook.Markdown(HistoryBook.Write(w)), "the same world writes the same book");
            Assert.AreEqual(h0, w.StateHash(), "historians read; they never write the world");
        }

        [Test]
        public void RealmsAreRebuiltFromTheirBondsTierByTier()
        {
            // premise: the ledger of four villages' bonds over thirty years - B bows to A, C to B (A's realm two tiers deep), D to A; then B
            // stops paying A, taking C with it (A keeps D; B becomes a realm of its own)
            var w = TestWorlds.Make(611);
            Coarse(w);
            Years(w, 2);
            int a = 1, b = 2, c = 3, d = 4;
            foreach (int sid in new[] { a, b, c, d }) Assert.IsTrue(w.Settlements.Exists(sid), "the premise's villages");
            Rec(w, HistoryKind.TributeAgreed, b, a);
            Years(w, 3);
            Rec(w, HistoryKind.TributeAgreed, c, b);
            Years(w, 5);
            Rec(w, HistoryKind.TributeAgreed, d, a, argB: 1);
            Years(w, 10);
            Rec(w, HistoryKind.TributeEnded, b, a);
            Years(w, 2);
            var book = HistoryBook.Write(w);
            var realms = book.Parts[1];
            foreach (var ch in realms.Chapters) Console.WriteLine(ch.Title + " - " + ch.Sub + "\n  " + (ch.Blocks.Count > 0 ? ch.Blocks[0].Text : ""));
            string an = w.History.NameOf(EntityRef.Settlement(a)), bn = w.History.NameOf(EntityRef.Settlement(b));
            var ra = realms.Chapters.Find(x => x.Title == "The Realm of " + an);
            Assert.IsNotNull(ra, "A's realm is told");
            StringAssert.Contains("at its height four villages", ra.Sub, "at its height B, B's own tributary C and D paid A: four villages with A");
            StringAssert.Contains("ended when it was no longer the stronger", ra.Blocks[0].Text, "B's bond ended");
            var lesser = realms.Chapters.Find(x => x.Title == "Lesser Lordships");
            Assert.IsNotNull(lesser, "B, paying nobody after it stopped, is a lordship of one village paying it");
            Assert.IsTrue(lesser.Blocks.Exists(x => x.Text.StartsWith(bn + " - ")), "B's own lordship");
            // the Timeline draws the same realm: four villages with its seat at its height, year by year from its bonds
            var tl = HistoryBook.Timeline(w);
            Assert.AreEqual(tl.To - tl.From + 1, tl.People.Length, "a year a column");
            Assert.AreEqual(1, tl.Realms.Count, "only realms of two villages paying or more are drawn");
            Assert.AreEqual(an, tl.Realms[0].Name);
            Assert.AreEqual(4, tl.Realms[0].Peak);
            Assert.AreEqual(4, tl.Realms[0].Villages[tl.Realms[0].PeakYear - tl.From], "its height in its year's column");
            Assert.AreEqual(2, tl.Realms[0].Villages[tl.To - tl.From], "and now A with D only");
            Assert.IsTrue(tl.Marks.Exists(m => m.Kind == 0 && m.Year == tl.Realms[0].PeakYear), "its height a moment of the timeline");
        }

        [Test]
        public void TheRealmsTodayAreTheTributeTheWorldHolds()
        {
            var w = TestWorlds.Make(613);
            Coarse(w);
            Years(w, 120);
            var book = HistoryBook.Write(w);
            int paying = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                if (s.Lord == 0 || !s.Inhabited) continue;
                paying++;
            }
            // the preface's count of villages paying today is the world's own
            string pre = string.Join(" ", book.Preface.ToArray());
            Console.WriteLine(pre + "\n(paying now in the world: " + paying + ")");
            if (paying == 0) StringAssert.DoesNotContain("lords take tribute from", pre);
            else StringAssert.Contains("tribute from " + (paying == 1 ? "one village" : paying < 13 ? new[] { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" }[paying] + " villages" : paying + " villages"), pre);
        }

        [Test]
        public void RaidsAreOneWarUntilABowOrTwelveQuietYears()
        {
            // premise: raids and a bow between villages 1 and 2 written by hand - the world's own askings (C10.8) are off, or a refusal
            // between the same two villages in those years would be a war event of its own and the premise's bow not the war's end
            Conflict.DemandOff = true;
            try
            {
                var w = TestWorlds.Make(617);
                Coarse(w);
                Years(w, 2);
                int a = 1, b = 2;
                Rec(w, HistoryKind.Raid, a, b, argA: 10, argB: 103, at: b);
                Years(w, 3);
                Rec(w, HistoryKind.Raid, a, b, argA: 12, argB: 204, at: b);   // (driven off)
                Years(w, 20);   // twelve quiet years and more: a new war
                Rec(w, HistoryKind.Raid, b, a, argA: 9, argB: 105, at: a);
                Years(w, 1);
                Rec(w, HistoryKind.Raid, a, b, argA: 14, argB: 106, at: b);
                Years(w, 1);
                Rec(w, HistoryKind.TributeAgreed, b, a);   // and the bow that ends it
                Years(w, 1);
                var wars = HistoryBook.Write(w).Parts[2];
                foreach (var ch in wars.Chapters) Console.WriteLine(ch.Title + " - " + ch.Sub + "\n  " + (ch.Blocks.Count > 0 ? ch.Blocks[0].Text : ""));
                string an = w.History.NameOf(EntityRef.Settlement(a)), bn = w.History.NameOf(EntityRef.Settlement(b));
                var first = wars.Chapters.Find(x => x.Title == "The Raids of " + an + " on " + bn);
                var second = wars.Chapters.Find(x => x.Title == "The War of " + an + " and " + bn);
                Assert.IsNotNull(first, "the first two raids, one way: raids");
                Assert.IsNotNull(second, "after the quiet years, raids both ways: a war");
                StringAssert.Contains("one band was driven off", first.Blocks[0].Text);
                StringAssert.Contains("bowed to " + an, second.Blocks[0].Text, "the bow ends it");
            }
            finally { Conflict.DemandOff = false; }
        }

        [Test]
        public void AThreadOfCauseRunsFromItsRootThroughWhatItPassed()
        {
            // premise: lightning burns a village's fields; its harvest fails, it rations and starves; households set out and found a village;
            // a raid follows years later - a chain through nature, hunger, founding and war
            var w = TestWorlds.Make(619);
            Coarse(w);
            Years(w, 2);
            int a = 1, b = 2;
            long e1 = w.Record(new HistoryEvent { Kind = HistoryKind.LightningStrike, Time = w.Now, Settlement = a, Cell = -1 });
            long e2 = Rec(w, HistoryKind.FieldsBurned, a, a, cause: e1);
            Years(w, 0.5);
            long e3 = w.Record(new HistoryEvent { Kind = HistoryKind.HarvestFailed, Time = w.Now, Settlement = a, Cell = -1, Subject = EntityRef.Settlement(a), Magnitude = 1f, Cause1 = e2 });
            Years(w, 1);
            long e4 = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Settlement = a, Cell = -1, Subject = EntityRef.Settlement(a), Cause1 = e3 });
            Years(w, 1);
            long e5 = w.Record(new HistoryEvent { Kind = HistoryKind.PartySetOut, Time = w.Now, Settlement = a, Cell = -1, Subject = EntityRef.Settlement(a), ArgA = 3, ArgB = 12, Cause1 = e4 });
            Years(w, 12);
            long e6 = Rec(w, HistoryKind.Raid, a, b, argA: 10, argB: 103, cause: e5, at: b);
            Years(w, 1);
            var threads = HistoryBook.Write(w).Parts[6];
            foreach (var ch in threads.Chapters) Console.WriteLine(ch.Title + " - " + ch.Sub);
            BookChapter mine = null;
            foreach (var ch in threads.Chapters) foreach (var bl in ch.Blocks) if (bl.Event == e6) mine = ch;
            Assert.IsNotNull(mine, "the chain is a thread");
            var ids = new System.Collections.Generic.List<long>();
            foreach (var bl in mine.Blocks) if (bl.Kind == BookKind.Line) ids.Add(bl.Event);
            Assert.AreEqual(e1, ids[0], "root first");
            Assert.AreEqual(e6, ids[ids.Count - 1], "its last effect last");
            Assert.IsTrue(ids.Contains(e4) && ids.Contains(e5), "each kind of thing it passed is shown at its first link");
            StringAssert.Contains("nature", mine.Blocks[0].Text);
        }
    }
}
