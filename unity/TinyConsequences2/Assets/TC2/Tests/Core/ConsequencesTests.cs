using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// (session 32: what came of it) A cast's tree of consequences: the cast at its root, each record under the record that caused it, the records of one kind
    /// and place told together, at once / in the days and seasons after / years on; every card's records rooted in the cast; read only.
    /// </summary>
    [TestFixture]
    public class ConsequencesTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Every record of every card runs back to the cast by its first causes, and each stands under a card holding its cause.</summary>
        static void Traces(World w, ConsequenceTree t)
        {
            var rooted = new HashSet<long>();
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Root == t.Cast && e.Id != t.Cast && e.RootKind == RootKind.You) rooted.Add(e.Id); }
            Assert.AreEqual(rooted.Count, t.Records, "every record rooted in the cast is read");
            Assert.AreEqual(t.Records + 1, t.Root.Total, "and the cast carries them all");
            foreach (var n in t.Nodes)
            {
                Assert.IsNotEmpty(n.Title, "a card says what it is");
                Assert.IsNotEmpty(n.When, "and when");
                Assert.Greater(n.Members.Count, 0);
                Assert.LessOrEqual(n.Depth, Consequences.MaxDepth);
                if (n.Index == 0) continue;
                var p = t.Nodes[n.Parent];
                Assert.IsTrue(p.Children.Contains(n.Index));
                if (!n.Detail && !n.More) Assert.LessOrEqual(p.Children.Count, Consequences.MaxBranches + 2, "a few branches a card, the rest folded");
                foreach (long id in n.Members)
                {
                    Assert.IsTrue(w.History.TryGet(id, out var e), "a real record");
                    Assert.AreEqual(t.Cast, e.Root, "rooted in the cast: " + HistoryText.Describe(w, in e));
                    Assert.AreEqual(RootKind.You, e.RootKind);
                    long cur = e.Id; int steps = 0;
                    while (cur != t.Cast && steps++ < 200 && w.History.TryGet(cur, out var up)) cur = up.Cause1;
                    Assert.AreEqual(t.Cast, cur, "its first causes run back to the cast");
                    if (n.Detail || n.More) continue;
                    if (p.Index == 0) Assert.IsTrue(e.Cause1 == t.Cast || e.Cause2 == t.Cast || (!rooted.Contains(e.Cause1) && !rooted.Contains(e.Cause2)), "under the cast: the cast its cause");
                    else Assert.IsTrue(p.Members.Contains(e.Cause1) || p.Members.Contains(e.Cause2), "under the card that holds its cause");
                }
            }
        }

        static void Print(ConsequenceTree t, int i)
        {
            var n = t.Nodes[i];
            Console.WriteLine(new string(' ', 2 * n.Depth) + n.Title + "  [" + n.When + ", " + n.Tier + (n.Below > 0 ? ", " + n.Below + " below" : "") + "]");
            foreach (int c in n.Children) Print(t, c);
        }

        [Test]
        public void AMeteorsTreeBeginsAtTheCastAndAtOnceHoldsTheStrikeItsDeadAndWhatFell()
        {
            var w = TestWorlds.Make(615);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Days(4));
            long cast = 0, strike = 0; int flattened = 0, killed = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) cast = e.Id;
                if (e.Kind == HistoryKind.MeteorStruck) { strike = e.Id; flattened = e.ArgA; killed = e.ArgB; }
            }
            Assert.AreNotEqual(0L, cast); Assert.AreNotEqual(0L, strike);
            Assert.AreEqual(cast, Consequences.LatestCast(w));
            ulong before = w.StateHash();
            var t = Consequences.Build(w, cast);
            var again = Consequences.Build(w, cast);
            Assert.AreEqual(before, w.StateHash(), "building the tree writes nothing");
            Assert.IsNotNull(t);
            Assert.IsNull(Consequences.Build(w, strike), "a tree grows from a cast only");
            Print(t, 0);
            // the cast at its root
            Assert.AreEqual(cast, t.Root.Event); Assert.AreEqual(HistoryKind.GodCommand, t.Root.Kind); Assert.AreEqual(ConsequenceTier.Cast, t.Root.Tier);
            StringAssert.StartsWith("Threw down a stone", t.Root.Title);
            // the strike under it, at once
            var hit = t.Nodes.Find(n => n.Kind == HistoryKind.MeteorStruck && !n.Detail);
            Assert.IsNotNull(hit, "the strike is a card");
            Assert.AreEqual(0, hit.Parent, "under the cast");
            Assert.AreEqual(ConsequenceTier.Immediate, hit.Tier);
            Assert.AreEqual("the same hour", hit.When);
            Assert.IsTrue(hit.Members.Contains(strike));
            // what fell: its record's count
            Assert.Greater(flattened, 0);
            var fell = t.Nodes.Find(n => n.Detail);
            Assert.IsNotNull(fell, "the buildings it flattened");
            Assert.AreEqual(hit.Index, fell.Parent); Assert.AreEqual(flattened, fell.Count); Assert.AreEqual(ConsequenceTier.Immediate, fell.Tier);
            StringAssert.Contains("flattened", fell.Title);
            // its dead, told together by their place, every one
            int dead = 0;
            foreach (var n in t.Nodes)
            {
                if (n.Kind != HistoryKind.PersonDied || n.Sub != 14) continue;
                dead += n.Count;
                Assert.AreEqual(hit.Index, n.Parent, "the stone's dead under the stone");
                Assert.AreEqual(ConsequenceTier.Immediate, n.Tier);
                StringAssert.Contains(n.Count == 1 ? "killed by the stone" : n.Count + " people killed by the stone", n.Title);
            }
            Assert.AreEqual(killed, dead, "every one it killed");
            Traces(w, t);
            // the same world, the same tree
            Assert.AreEqual(t.Nodes.Count, again.Nodes.Count);
            for (int i = 0; i < t.Nodes.Count; i++) { Assert.AreEqual(t.Nodes[i].Title, again.Nodes[i].Title); Assert.AreEqual(t.Nodes[i].Event, again.Nodes[i].Event); Assert.AreEqual(t.Nodes[i].Parent, again.Nodes[i].Parent); }
        }

        [Test]
        public void TheBooksFirstPageAndYourActionsTellTheCastAndWhatCameOfItByWhen()
        {
            // (session 32) the book's first page: a line for each part, your newest cast with what came of it; its chapter in Your Actions tells it by when
            var w = TestWorlds.Make(615);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Days(3));
            long cast = Consequences.LatestCast(w);
            Assert.AreNotEqual(0L, cast);
            ulong before = w.StateHash();
            var bw = new HistoryBook.Writer(w);
            Assert.AreEqual(HistoryBook.PartTitles.Length, bw.Book.Contents.Count, "a line for every part");
            StringAssert.Contains("1 cast", bw.Book.Contents[HistoryBook.YourActionsPart].Replace("one cast", "1 cast"));
            var hand = HistoryBook.YourHand(w, 4);
            Assert.AreEqual(1, hand.Count); Assert.AreEqual(cast, hand[0].Id); Assert.AreEqual(0, hand[0].Chapter);
            HistoryBook.CameOf(w, cast, out int n);
            Assert.AreEqual(n, hand[0].Count, "what came of it, as your hand counts it");
            StringAssert.StartsWith("Threw down a stone", hand[0].Title);
            var ch = bw.Part(HistoryBook.YourActionsPart).Chapters[0];
            Assert.AreEqual(cast, ch.Blocks[0].Event);
            bool heading = false; string atOnce = null;
            foreach (var b in ch.Blocks)
            {
                if (b.Kind == BookKind.Heading && b.Text == "What came of it, by when") heading = true;
                if (b.Kind == BookKind.Note && b.Text.StartsWith("At once: ", StringComparison.Ordinal)) atOnce = b.Text;
            }
            Console.WriteLine(atOnce);
            Assert.IsTrue(heading, "told by when");
            Assert.IsNotNull(atOnce);
            StringAssert.Contains("people killed by the stone", atOnce);
            Assert.AreEqual(before, w.StateHash(), "the book writes nothing");
        }

        [Test]
        public void ItsCardsAreGroupedToldWhenTheyCameAndWhatTheyCannotShowIsFolded()
        {
            // a cast's records laid in the ledger by hand: a stone, its dead in two places, a hunger days on and what it led to years on, seven things more
            // the cast did at once and a thread longer than the tree is deep
            var w = TestWorlds.Make(611);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            int cell = w.Settlements.Get(a).Cell;
            long now = w.Now;
            long Rec(HistoryKind k, long cause, long time, int at, EntityRef subj = default, EntityRef obj = default, int argA = 0, int argB = 0, double mag = 0) =>
                w.Record(new HistoryEvent { Kind = k, Time = time, Cell = cell, Settlement = at, Subject = subj, Object = obj, Cause1 = cause, ArgA = argA, ArgB = argB, Magnitude = (float)mag, RootKind = RootKind.You });
            long cast = Rec(HistoryKind.GodCommand, 0, now, a, argA: (int)CommandKind.GodMeteor);
            long strike = Rec(HistoryKind.MeteorStruck, cast, now, a, argA: 3, argB: 9);
            for (int k = 0; k < 7; k++) Rec(HistoryKind.PersonDied, strike, now, a, EntityRef.Person(90000 + k), argA: 14, mag: 30 + k);
            for (int k = 0; k < 2; k++) Rec(HistoryKind.PersonDied, strike, now, b, EntityRef.Person(90100 + k), argA: 14, mag: 40 + k);
            var others = new[] { HistoryKind.FireStarted, HistoryKind.Quake, HistoryKind.Landslides, HistoryKind.Drought, HistoryKind.RainBrought, HistoryKind.WarmAir, HistoryKind.ColdAir };
            foreach (var k in others) Rec(k, cast, now + SimTime.Hours(1), a, EntityRef.Settlement(a), argA: 1, mag: 6);
            long link = strike;
            for (int k = 0; k < 14; k++) link = Rec(HistoryKind.WildFood, link, now + SimTime.Hours(2 + k), a, EntityRef.Settlement(a));
            long hunger = Rec(HistoryKind.Hunger, strike, now + SimTime.Days(5), a, EntityRef.Settlement(a));
            for (int k = 0; k < 3; k++) Rec(HistoryKind.HouseholdMoved, hunger, now + SimTime.Years(2) + k * SimTime.Day, a, EntityRef.Household(90200 + k), EntityRef.Settlement(b));
            Rec(HistoryKind.TributeAgreed, hunger, now + SimTime.Years(12), a, EntityRef.Settlement(a), EntityRef.Settlement(b), mag: 0.1);
            Rec(HistoryKind.Hunger, 0, now + SimTime.Years(12), b, EntityRef.Settlement(b));   // (not the cast's: never in its tree)

            var t = Consequences.Build(w, cast);
            Print(t, 0);
            Traces(w, t);
            Assert.AreEqual(1 + 7 + 2 + others.Length + 14 + 1 + 3 + 1, t.Records);
            // under the cast: the five biggest branches, the rest one card
            Assert.AreEqual(Consequences.MaxBranches + 1, t.Root.Children.Count);
            var more = t.Nodes[t.Root.Children[t.Root.Children.Count - 1]];
            Assert.IsTrue(more.More); Assert.AreEqual(others.Length + 1 - Consequences.MaxBranches, more.Folded.Count); Assert.AreEqual(more.Folded.Count, more.Count);
            StringAssert.StartsWith("+3 more branches", more.Title);
            var hit = t.Nodes[t.Root.Children[0]];
            Assert.AreEqual(HistoryKind.MeteorStruck, hit.Kind, "the first, the stone: the same hour, the most under it");
            // the stone's: what fell, its dead by place, its hunger
            var fell = t.Nodes[hit.Children[0]];
            Assert.IsTrue(fell.Detail); Assert.AreEqual(3, fell.Count); Assert.AreEqual("3 buildings flattened", fell.Title);
            var deadA = t.Nodes.Find(n => n.Kind == HistoryKind.PersonDied && n.Settlement == a);
            var deadB = t.Nodes.Find(n => n.Kind == HistoryKind.PersonDied && n.Settlement == b);
            Assert.AreEqual(7, deadA.Count); Assert.AreEqual(2, deadB.Count); Assert.AreEqual(7, deadA.Members.Count);
            StringAssert.StartsWith("7 people killed by the stone at ", deadA.Title);
            Assert.AreEqual(ConsequenceTier.Immediate, deadA.Tier);
            var hu = t.Nodes.Find(n => n.Kind == HistoryKind.Hunger);
            Assert.AreEqual(hit.Index, hu.Parent); Assert.AreEqual(ConsequenceTier.Secondary, hu.Tier); Assert.AreEqual("5 days on", hu.When);
            StringAssert.StartsWith("Hunger in ", hu.Title);
            // years on: the households that left, the bow
            var left = t.Nodes.Find(n => n.Kind == HistoryKind.HouseholdMoved);
            Assert.AreEqual(hu.Index, left.Parent); Assert.AreEqual(3, left.Count); Assert.AreEqual(ConsequenceTier.LongTerm, left.Tier);
            StringAssert.StartsWith("2 years on, over 2 days", left.When);
            StringAssert.StartsWith("3 households left ", left.Title);
            var bow = t.Nodes.Find(n => n.Kind == HistoryKind.TributeAgreed);
            Assert.AreEqual(hu.Index, bow.Parent); Assert.AreEqual(ConsequenceTier.LongTerm, bow.Tier); Assert.AreEqual("12 years on", bow.When);
            StringAssert.Contains(" bowed to ", bow.Title);
            Assert.IsNull(t.Nodes.Find(n => n.Kind == HistoryKind.Hunger && n.Settlement == b), "nothing that is not the cast's");
            // the long thread: as deep as the tree goes, the rest counted below it
            Assert.AreEqual(Consequences.MaxDepth, t.MaxDepth);
            var deepest = t.Nodes.Find(n => n.Depth == Consequences.MaxDepth);
            Assert.AreEqual(HistoryKind.WildFood, deepest.Kind); Assert.AreEqual(14 - (Consequences.MaxDepth - 1), deepest.Below);
            // later cards never come before their parents'
            foreach (var n in t.Nodes) if (n.Index > 0 && !n.More) Assert.GreaterOrEqual((int)n.Tier, (int)t.Nodes[n.Parent].Tier);
            Assert.AreEqual(0, t.CastsAfter);
        }
    }
}
