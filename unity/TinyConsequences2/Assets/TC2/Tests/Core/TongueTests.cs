using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// A people's tongue (C11.1): each first people speaks its own; a village's founders bring theirs from the village they came from, changed in at most
    /// one thing; places are named in their founders' tongue, families in their first people's, the born in the tongue of the village they are born in.
    /// `--oldnames` names as before. Mechanisms.
    /// </summary>
    [TestFixture]
    public class TongueTests
    {
        [Test]
        public void EachFirstPeopleSpeaksItsOwnTongueAndNamesItsPlaceInIt()
        {
            var w = TestWorlds.Make(631);
            var tongues = new List<long>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                ref var s = ref w.Settlements.Get(sid);
                Assert.AreNotEqual(0L, s.Tongue, "every first people speaks");
                string name = w.History.NameOf(EntityRef.Settlement(sid)).ToLowerInvariant();
                var ends = Names.EndsOf(s.Tongue);
                Assert.Greater(ends.Length, 0);
                Assert.IsTrue(Array.Exists(ends, e => name.EndsWith(e)), name + " ends as its people name their places (" + string.Join(", ", ends) + ")");
                Console.WriteLine(name + ": " + string.Join(", ", ends) + " - " + w.History.NameOf(EntityRef.Settlement(sid)));
                tongues.Add(s.Tongue);
            }
            Assert.Greater(tongues.Count, 1, "the premise: more than one first people");
            int apart = 0;
            for (int i = 0; i < tongues.Count; i++) for (int j = i + 1; j < tongues.Count; j++) if (Names.Apart(tongues[i], tongues[j]) >= 4) apart++;
            Assert.Greater(apart, 0, "first peoples speak apart");
        }

        [Test]
        public void FoundersBringTheirTongueChangedInAtMostOneThing()
        {
            var w = TestWorlds.Make(633);
            int origin = w.Settlements.First();
            long t = w.Settlements.Get(origin).Tongue;
            int same = 0, n = 200;
            for (int k = 0; k < n; k++)
            {
                long d = Migration.TongueAt(w, 100000 + k, origin);
                int apart = Names.Apart(t, d);
                Assert.LessOrEqual(apart, 1, "one sound, ending or habit at a time");
                if (apart == 0) same++;
                Assert.Greater(Names.EndsOf(d).Length, 0, "a tongue keeps an ending for its places");
            }
            Console.WriteLine("unchanged " + same + " of " + n);
            Assert.That(same, Is.InRange(n * 3 / 10, n * 7 / 10), "about half the foundings speak as they came");
            // a place founded from it is named with one of the endings its founders' tongue gives
            long d0 = Migration.TongueAt(w, 100500, origin);
            string place = Names.Place(w.Seed, 100500, d0).ToLowerInvariant();
            Assert.IsTrue(Array.Exists(Names.EndsOf(d0), e => place.EndsWith(e)), place);
        }

        [Test]
        public void TheBookTellsThePeoplesOfTheWorld()
        {
            var w = TestWorlds.Make(607);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Years(90));
            var lines = new Dictionary<int, int>();
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { int l = Architecture.Line(w.Settlements.Get(sid).Tradition); lines.TryGetValue(l, out int n); lines[l] = n + 1; }
            int peoples = 0; foreach (var kv in lines) if (kv.Value >= 2) peoples++;
            Assume.That(peoples, Is.GreaterThan(0), "the premise: a village founded from another in ninety years");
            var book = TC2.Core.History.HistoryBook.Write(w);
            var ch = book.Parts[4].Chapters.Find(x => x.Title == "The Peoples");
            Assert.IsNotNull(ch, "the places begin with the peoples");
            int heads = 0, records = 0;
            foreach (var b in ch.Blocks)
            {
                if (b.Kind == TC2.Core.History.BookKind.Heading) heads++;
                if (b.Kind == TC2.Core.History.BookKind.Line) { records++; Assert.IsTrue(w.History.TryGet(b.Event, out var e) && e.Kind == TC2.Core.History.HistoryKind.SettlementFounded, "its lines are foundings on record"); }
                Console.WriteLine(b.Text);
            }
            Assert.AreEqual(peoples, heads, "each people of more than one village");
            Assert.Greater(records, 0);
        }

        [Test]
        public void UnderTheOldRulesNamesAreAsBefore()
        {
            Migration.TonguesOff = true;
            try
            {
                var w = TestWorlds.Make(631);
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    Assert.AreEqual(0L, w.Settlements.Get(sid).Tongue);
                    Assert.AreEqual(Names.Place(w.Seed, sid), w.History.NameOf(EntityRef.Settlement(sid)), "--oldnames: the one speech of before");
                }
            }
            finally { Migration.TonguesOff = false; }
        }
    }
}
