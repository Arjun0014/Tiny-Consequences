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
        public void AFiresGroundIsToldInHectaresNotTheGridsCells()
        {
            // (session 30) "The fire near Feinhope burnt out after 17 hours (1 cells)" stood in the book (s30web1): a fire's ground is the land it burned, by
            // the world's own measure (a cell's geo area), and an hour is one hour
            var w = TestWorlds.Make(611);
            int sid = w.Settlements.First();
            int cell = w.Settlements.Get(sid).Cell;
            double cellHa = w.Planet.GeoAreaHa(cell);
            long one = w.Record(new HistoryEvent { Kind = HistoryKind.FireOut, Time = w.Now, Settlement = sid, Cell = cell, Subject = EntityRef.Settlement(sid), ArgA = 1, Magnitude = 1, RootKind = RootKind.Nature });
            long many = w.Record(new HistoryEvent { Kind = HistoryKind.Wildfire, Time = w.Now, Settlement = sid, Cell = cell, Subject = EntityRef.Settlement(sid), ArgA = 17, Magnitude = 30, RootKind = RootKind.Nature });
            Assert.IsTrue(w.History.TryGet(one, out var e1)); Assert.IsTrue(w.History.TryGet(many, out var e2));
            string a = HistoryText.Describe(w, in e1), b = HistoryText.Describe(w, in e2);
            StringAssert.DoesNotContain("cells", a); StringAssert.DoesNotContain("cells", b);
            StringAssert.Contains("after 1 hour,", a);
            StringAssert.Contains("about " + DMath.Max(1.0, DMath.RoundHalfUp(cellHa)).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ha", a);
            StringAssert.Contains(" ha burnt so far", b);
        }

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
            Assert.Greater(book.Parts[3].Chapters.Count, 0, "the places");
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
            long firstBow = Rec(w, HistoryKind.TributeAgreed, b, a);
            Years(w, 3);
            // (session 32) a ruler of A's on record in its realm's years
            int ruler = w.Residents(a)[0], rulerHh = w.People.Get(ruler).Household;
            long rose = w.Record(new HistoryEvent { Kind = HistoryKind.HeadmanRose, Time = w.Now, Settlement = a, Cell = -1, Subject = EntityRef.Person(ruler), Object = EntityRef.Household(rulerHh) });
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
            StringAssert.Contains("ended when it was no longer the stronger", ra.Blocks.Find(x => x.Kind == BookKind.Para).Text, "B's bond ended");
            // (session 32) it opens at a glance: its rise and fall and its line of rulers, each row on its record
            Assert.AreEqual(BookKind.Heading, ra.Blocks[0].Kind); Assert.AreEqual("Its rise and fall", ra.Blocks[0].Text);
            var bowRow = ra.Blocks.Find(x => x.Kind == BookKind.Row && x.Event == firstBow);
            StringAssert.Contains("its first tributary", bowRow.Text, "B's bow, its first tributary");
            Assert.IsTrue(ra.Blocks.Exists(x => x.Kind == BookKind.Heading && x.Text == "Its line of rulers"));
            var rulerRow = ra.Blocks.Find(x => x.Kind == BookKind.Row && x.Event == rose);
            Assert.AreEqual(ruler, rulerRow.Person, "the ruler's row opens their life");
            StringAssert.StartsWith("Year ", rulerRow.Text);
            foreach (var bl in ra.Blocks) if (bl.Kind == BookKind.Row) Assert.IsTrue(w.History.TryGet(bl.Event, out _), "every row stands on a record");
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
        public void ABondEndsWhenTheVillagePayingIsAbandoned()
        {
            // premise (by hand, as the ledger would hold it): villages 2 and 3 bow to village 1; village 3 is later abandoned - no harvest of its own
            // tells its bond's end, so the book must read its abandonment as the end: today village 1 takes tribute from one village, not two
            Conflict.DemandOff = true;
            try
            {
                var w = TestWorlds.Make(622);
                Coarse(w);
                Years(w, 2);
                Rec(w, HistoryKind.TributeAgreed, 2, 1);
                Rec(w, HistoryKind.TributeAgreed, 3, 1);
                Years(w, 3);
                w.Record(new HistoryEvent { Kind = HistoryKind.SettlementAbandoned, Time = w.Now, Settlement = 3, Cell = -1, Subject = EntityRef.Settlement(3) });
                Years(w, 2);
                var book = HistoryBook.Write(w);
                string pre = string.Join(" ", book.Preface.ToArray());
                Console.WriteLine(pre);
                bool other = false;   // (the world's own bonds in its first years, if any, would share the preface)
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).Lord != 0) other = true;
                if (!other)
                {
                    StringAssert.Contains("two villages paid it tribute", pre, "both paid at its height");
                    StringAssert.Contains("Today one lord takes tribute from one village.", pre, "the abandoned village pays no more");
                }
                var realm = book.Parts[1].Chapters.Find(x => x.Blocks.Exists(bl => bl.Text != null && bl.Text.Contains("when the village paying it was abandoned")));
                Assert.IsNotNull(realm, "the realm's chapter tells the bond that ended with its village");
            }
            finally { Conflict.DemandOff = false; }
        }

        [Test]
        public void ARealmsMapShowsItsVillagesAtItsHeight()
        {
            // premise (by hand, as the ledger holds it): villages 2 and 3 bow to village 1, then 3 is abandoned - the realm's chapter is drawn on a map
            // at its height with all three; at the end village 3 stands no longer
            Conflict.DemandOff = true;
            try
            {
                var w = TestWorlds.Make(625);
                Coarse(w);
                Years(w, 2);
                Rec(w, HistoryKind.TributeAgreed, 2, 1);
                Rec(w, HistoryKind.TributeAgreed, 3, 1);
                Years(w, 3);
                int height = (int)(w.Now / SimTime.Year);
                w.Record(new HistoryEvent { Kind = HistoryKind.SettlementAbandoned, Time = w.Now, Settlement = 3, Cell = -1, Subject = EntityRef.Settlement(3) });
                Years(w, 2);
                var writer = new HistoryBook.Writer(w);
                var realm = writer.Part(1).Chapters.Find(x => x.MapSeat == 1);
                Assert.IsNotNull(realm, "the realm's chapter has its map");
                var inside = new System.Collections.Generic.List<int>(); var outside = new System.Collections.Generic.List<int>();
                writer.RealmAt(1, realm.MapYear, inside, outside);
                CollectionAssert.Contains(inside, 1, "the seat"); CollectionAssert.Contains(inside, 2, "its tributary");
                if (realm.MapYear < height) CollectionAssert.Contains(inside, 3, "at its height village 3 paid it too");
                writer.RealmAt(1, (int)(w.Now / SimTime.Year), inside, outside);
                CollectionAssert.DoesNotContain(inside, 3); CollectionAssert.DoesNotContain(outside, 3, "an abandoned village stands no longer");
                CollectionAssert.Contains(inside, 2);
            }
            finally { Conflict.DemandOff = false; }
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
                StringAssert.Contains("one band was driven off", first.Blocks.Find(x => x.Kind == BookKind.Para).Text);
                StringAssert.Contains("bowed to " + an, second.Blocks.Find(x => x.Kind == BookKind.Para).Text, "the bow ends it");
                // (session 32) each war opens with its card: its sides, its raids, its outcome - on its first raid's record, its two places
                var card = second.Blocks[0];
                Console.WriteLine(card.Text);
                Assert.AreEqual(BookKind.Card, card.Kind);
                Assert.IsTrue(w.History.TryGet(card.Event, out var ce) && ce.Kind == HistoryKind.Raid, "on its first raid");
                Assert.IsTrue((card.Settlement == a && card.Other == b) || (card.Settlement == b && card.Other == a), "its two sides' places");
                StringAssert.Contains(" against ", card.Text); StringAssert.Contains("two raids", card.Text); StringAssert.Contains("Outcome: " + bn + " bowed to " + an, card.Text);
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
            var threads = HistoryBook.Write(w).Parts[8];
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
