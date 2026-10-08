using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The person page (C12.1, D2-099; 07 §9): a person shown from the world's state and its ledger - who bore them and where, their kin as
    /// the people store knows them, their life as the records they are part of, roles read from what they have practised; reading the page
    /// changes nothing. Mechanisms.
    /// </summary>
    [TestFixture]
    public class BiographyTests
    {
        static int ChildBornHere(World w)
        {
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (p.Alive && p.Mother != 0 && Biography.BornAt(w, id) != 0) return id;
            }
            return 0;
        }

        [Test]
        public void APageIsReadFromStateAndTheLedger()
        {
            var w = TestWorlds.Make(1201);
            w.AdvanceTo(w.Now + SimTime.Years(3));
            int pid = ChildBornHere(w);
            Assert.AreNotEqual(0, pid, "someone was born in three years");
            ref var p = ref w.People.Get(pid);
            string page = Biography.All(w, pid);
            Console.WriteLine(page);
            string name = w.History.NameOf(EntityRef.Person(pid));
            StringAssert.StartsWith("== " + name + ", ", page);
            StringAssert.Contains("Born in year " + (p.Born / SimTime.Year) + " in " + w.History.NameOf(EntityRef.Settlement(Biography.BornAt(w, pid))), page);
            StringAssert.Contains("Mother: " + w.History.NameOf(EntityRef.Person(p.Mother)), page);
            // the mother's page counts this child among hers
            int kids = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var q = ref w.People.Get(id); if (q.Mother == p.Mother || q.Father == p.Mother) kids++; }
            StringAssert.Contains("Children (" + kids + "): ", Biography.Family(w, p.Mother));
            // every line of their life is one of their records, in order
            var evs = w.History.EventsOf(EntityRef.Person(pid));
            string life = Biography.Life(w, pid, 1000);
            int at = 0;
            for (int i = 0; i < evs.Count; i++)
            {
                Assert.IsTrue(w.History.TryGet(evs[i], out var e));
                int k = life.IndexOf(HistoryText.Line(w, e), at, StringComparison.Ordinal);
                Assert.GreaterOrEqual(k, at, "record " + i + " in its place");
                at = k;
            }
            Assert.AreEqual(evs.Count, life.Split('\n').Length - 1, "nothing but their records");
        }

        [Test]
        public void RolesAreReadFromPracticeNeverAssigned()
        {
            var w = TestWorlds.Make(1202);
            int pid = w.People.First();
            ref var p = ref w.People.Get(pid);
            p.SkFight = 0f; p.Fights = 0; p.KPottery = 0.8f; p.SkPot = 0f; p.SkFish = 0f;   // (knowing how to fire pots is not being a potter: C3.2)
            string before = Biography.Work(w, pid);
            StringAssert.DoesNotContain("fighter", before);
            StringAssert.DoesNotContain("a potter", before);
            p.SkFight = 0.5f; p.Fights = 2; p.SkPot = 0.4f; p.SkFish = 0.7f;   // (hours at a kiln make a potter)
            string after = Biography.Work(w, pid);
            StringAssert.Contains("a fighter who has fought", after);
            StringAssert.Contains("a potter", after);
            StringAssert.Contains("a fisher", after);
            StringAssert.Contains("2 fights lived through", Biography.Deeds(w, pid));
        }

        [Test]
        public void SmithsAndBoatwrightsAreReadFromTheirWorkNotTheirKnowledge()
        {
            // (C1.4b, D2-125: a year-300 world read 5,730 of its people as "a smith" and "a boatwright" - all who could work copper or knew the
            // planks' craft - where 175 had worked at a smithy and 10 had built a plank boat)
            var w = TestWorlds.Make(1204);
            int pid = 0;
            for (int id = w.People.First(); id != 0 && pid == 0; id = w.People.Next(id)) { var q = w.People.Get(id); if (q.Alive && q.AgeYears(w.Now) >= 20 && q.Household != 0) pid = id; }
            Assume.That(pid != 0);
            ref var p = ref w.People.Get(pid);
            p.KMetal = 0.9f; p.KIron = 0.8f; p.KBoat = 0.9f; p.SkSmith = 0f;   // (they know the crafts)
            string before = Biography.Work(w, pid);
            StringAssert.DoesNotContain("an ironsmith", before);
            StringAssert.DoesNotContain("a smith", before);
            StringAssert.DoesNotContain("a boatwright", before);
            p.SkSmith = 0.4f;   // (hours at the smithy or the open hearth)
            w.Record(new HistoryEvent { Kind = HistoryKind.BoatBuilt, Time = w.Now, Settlement = p.Home, Subject = EntityRef.Household(p.Household), Other = EntityRef.Person(pid), ArgA = Structures.PlankBoat, RootKind = RootKind.People });
            string after = Biography.Work(w, pid);
            Console.WriteLine(after);
            StringAssert.Contains("an ironsmith", after);
            StringAssert.Contains("a boatwright", after);
            StringAssert.Contains("(1 plank boat)", after);
            Assert.AreEqual(1, Biography.PlankBoats(w, pid));
            // a dugout they hollowed is no plank boat
            w.Record(new HistoryEvent { Kind = HistoryKind.BoatBuilt, Time = w.Now, Settlement = p.Home, Subject = EntityRef.Household(p.Household), Other = EntityRef.Person(pid), ArgA = Structures.Boat, RootKind = RootKind.People });
            Assert.AreEqual(1, Biography.PlankBoats(w, pid));
        }

        [Test]
        public void ReadingEveryPageChangesNothing()
        {
            var w = TestWorlds.Make(1203);
            w.AdvanceTo(w.Now + SimTime.Years(2));
            ulong h = w.StateHash();
            long hist = w.History.LastId;
            int n = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { n += Biography.All(w, id).Length; }
            Assert.Greater(n, 0);
            Assert.AreEqual(h, w.StateHash(), "the page reads only");
            Assert.AreEqual(hist, w.History.LastId);
        }
    }
}
