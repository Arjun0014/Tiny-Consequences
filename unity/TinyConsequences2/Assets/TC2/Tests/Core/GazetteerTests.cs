using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
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
            // every line of its history is one of its records
            var evs = w.History.EventsOf(EntityRef.Settlement(sid));
            string life = Gazetteer.Life(w, sid, 100000);
            Assert.AreEqual(evs.Count, life.Split('\n').Length - 1, "nothing but its records");
        }

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
