using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The settlement page (C12.2, D2-106): a place read from state and the ledger - its size word from its people, its works as they stand,
    /// its ore and who works it, its history as its records; reading every page changes nothing. Mechanisms.
    /// </summary>
    [TestFixture]
    public class GazetteerTests
    {
        [Test]
        public void APlacePageIsReadFromStateAndTheLedger()
        {
            var w = TestWorlds.WithCopper(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            w.AdvanceTo(w.Now + SimTime.Days(10));
            ref var s = ref w.Settlements.Get(sid);
            string page = Gazetteer.All(w, sid);
            Console.WriteLine(page);
            StringAssert.StartsWith("== " + w.History.NameOf(EntityRef.Settlement(sid)) + ", " + Gazetteer.Word(in s) + " of " + s.Population + " people", page);
            Assert.AreEqual("a hamlet", Gazetteer.Word(new Settlement { Population = 20 }));
            Assert.AreEqual("a town", Gazetteer.Word(new Settlement { Population = 300 }));
            StringAssert.Contains("well (", Gazetteer.Works(w, sid), "its well, standing");
            StringAssert.Contains("Copper: a body known", Gazetteer.Materials(w, sid));
            // a mine raised there: worked by its people
            Structures.Place(w, sid, Structures.Mine, out float x, out float y);
            Structures.Standing(w, sid, Structures.Mine, x, y);
            StringAssert.Contains("worked by its people", Gazetteer.Materials(w, sid));
            // every line of its history is one of its records - its own and its political events (its headmen, its splits: C10.2, C10.3)
            var evs = w.History.EventsOf(EntityRef.Settlement(sid));
            var pe = w.History.PlaceEventsOf(sid);
            Assume.That(pe != null && pe.Count > 0, "a village with a headman");
            string life = Gazetteer.Life(w, sid, 100000);
            Assert.AreEqual(evs.Count + pe.Count, life.Split('\n').Length - 1, "nothing but its records");
            StringAssert.Contains("became first among the people of", life, "its headman in its history");
        }

        /// <summary>
        /// (session 27, context 4: the paper pages of s27book6) the page says each thing once - the crafts gathered by their best hand ("masters among
        /// them" once, not after every craft), no bracket inside a bracket, the homes counted once - and reading it changes nothing.
        /// </summary>
        [Test]
        public void APagesWordsSayEachThingOnce()
        {
            var w = TestWorlds.Make(1501);
            w.AdvanceTo(w.Now + SimTime.Years(1));
            int sid = 0;
            for (int o = w.Settlements.First(); o != 0 && sid == 0; o = w.Settlements.Next(o)) if (w.Settlements.Get(o).Inhabited) sid = o;
            Assume.That(sid != 0, "a living village");
            ref var s = ref w.Settlements.Get(sid);
            // premise: three crafts known with masters, none other practised
            s.TechKnown = (byte)((1 << Tech.Nets) | (1 << Tech.Loom) | (1 << Tech.Quern));
            s.BestNets = 0.95f; s.BestLoom = 0.92f; s.BestQuern = 0.97f; s.BestPottery = 0f; s.BestArd = 0f; s.BestMetal = 0f; s.BestIron = 0f; s.BestBoat = 0f;
            string crafts = Gazetteer.Crafts(w, sid);
            Console.WriteLine(crafts);
            Assert.AreEqual(1, Times(crafts, "masters among them"), "the masters said once: " + crafts);
            StringAssert.Contains(" and ", crafts, "the crafts as a list");
            // then a skilled loom and pottery tried but not yet known: three groups, each said once
            s.BestLoom = 0.7f; s.BestPottery = 0.3f;
            crafts = Gazetteer.Crafts(w, sid);
            Console.WriteLine(crafts);
            Assert.AreEqual(1, Times(crafts, "(masters among them)"), crafts);
            Assert.AreEqual(1, Times(crafts, "(skilled hands among them)"), crafts);
            Assert.AreEqual(1, Times(crafts, "(none of them yet competent)"), crafts);
            // every place's overview: no bracket inside a bracket, its houses counted once
            ulong h = w.StateHash();
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                string ov = Gazetteer.Overview(w, o);
                int depth = 0, most = 0;
                foreach (char ch in ov) { if (ch == '(') most = Math.Max(most, ++depth); else if (ch == ')') depth--; }
                Assert.LessOrEqual(most, 1, "a bracket inside a bracket: " + ov);
                StringAssert.DoesNotMatch(@"\d+ houses \(\d+ on stone footings\)", ov, "the houses counted twice");
            }
            Assert.AreEqual(h, w.StateHash(), "the page reads only");
        }

        static int Times(string s, string what) { int n = 0; for (int i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + what.Length, StringComparison.Ordinal)) n++; return n; }

        [Test]
        public void ReadingEveryPlacesPageChangesNothing()
        {
            var w = TestWorlds.Make(1501);
            w.AdvanceTo(w.Now + SimTime.Years(1));
            ulong h = w.StateHash();
            long hist = w.History.LastId;
            int n = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) n += Gazetteer.All(w, sid).Length;
            Assert.Greater(n, 0);
            Assert.AreEqual(h, w.StateHash(), "the page reads only");
            Assert.AreEqual(hist, w.History.LastId);
        }
    }
}
