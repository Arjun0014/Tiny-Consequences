using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// (session 32) A shock felt where people are - a stone from the sky, the ground shaking, a bolt at the door - takes them from their day at once: the
    /// unhurt run clear and keep together on open ground until it has passed, the badly hurt are abed, and then they go back to their day; a strong quake
    /// brings down buildings and its records say so, every one rooted in the cast.
    /// </summary>
    [TestFixture]
    public class ShockTests
    {
        static int Count(World w, int sid, Func<Person, bool> f)
        {
            int n = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive && p.At == sid && f(p)) n++; }
            return n;
        }

        [Test]
        public void AStoneFromTheSkyMakesTheVillageRunAndKeepTogetherUntilItPasses()
        {
            var w = TestWorlds.Make(615);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(4));   // (lived days: mid-morning, people about)
            int sid = w.Settlements.First();
            int present = Count(w, sid, p => p.Act != ActivityKind.Infant);
            Assume.That(present > 5, "people at home in the village");
            // (two cells off its heart, ~300 m: beyond the blast, well within what is felt - a stone on the square kills all at home in a hamlet)
            int c = w.Settlements.Get(sid).Cell; c = w.Planet.Grid.Nbr4[c * 4]; c = w.Planet.Grid.Nbr4[c * 4];
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = c });
            w.AdvanceTo(w.Now + SimTime.Minutes(3));
            int shaken = Count(w, sid, p => p.Why == WhyCode.Shock), abed = Count(w, sid, p => p.Act == ActivityKind.Ill);
            int running = Count(w, sid, p => p.Why == WhyCode.Shock && p.Act == ActivityKind.Flee);
            Console.WriteLine("present " + present + ", startled " + shaken + " (running " + running + "), abed " + abed);
            Assert.Greater(shaken, present / 2, "most of the village is taken from its day by the stone's fall");
            Assert.Greater(running, 0, "and runs clear of it");
            var text = HistoryText.Doing(w, w.People.Get(FirstShaken(w, sid)));
            StringAssert.Contains("a stone fell from the sky", text, "the inspector says why");
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.Greater(Count(w, sid, p => p.Why == WhyCode.Shock && p.Act == ActivityKind.Social), 0, "an hour on they keep together on open ground");
            w.AdvanceTo(w.Now + SimTime.Hours(3));
            Assert.AreEqual(0, Count(w, sid, p => p.Why == WhyCode.Shock), "once it has passed they go back to their day");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        static int FirstShaken(World w, int sid)
        {
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive && p.At == sid && p.Why == WhyCode.Shock) return id; }
            return 0;
        }

        [Test]
        public void AGreatQuakeUnderAVillageBringsDownBuildingsAndItsPeopleRunOutOfDoors()
        {
            var w = TestWorlds.Make(616);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(4));
            int sid = w.Settlements.First();
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodQuake, Cell = w.Settlements.Get(sid).Cell, Arg = 7 });
            w.AdvanceTo(w.Now + SimTime.Minutes(3));
            long cast = 0; int homes = 0, works = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) cast = e.Id;
                if (e.Kind == HistoryKind.Quake) Console.WriteLine("quake M " + e.Magnitude + " cells " + e.ArgA + "; buildings in the village " + w.BuildingsOf(sid).Count);
                if (e.Kind == HistoryKind.HomesShaken) { homes += e.ArgA; works += e.ArgB; Assert.AreEqual(cast, e.Root, "rooted in the cast"); StringAssert.StartsWith("The quake brought down", HistoryText.Describe(w, in e)); }
            }
            Console.WriteLine("homes fallen " + homes + ", works " + works + ", startled " + Count(w, sid, p => p.Why == WhyCode.Shock));
            Assert.Greater(homes + works, 0, "buildings fell under it");
            Assert.Greater(Count(w, sid, p => p.Why == WhyCode.Shock), 0, "its people ran out of doors");
            w.AdvanceTo(w.Now + SimTime.Hours(3));
            Assert.AreEqual(0, Count(w, sid, p => p.Why == WhyCode.Shock), "and went back to their day");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void AGaleOverAVillageSendsItsPeopleIn()
        {
            var w = TestWorlds.Make(617);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(4));
            int sid = w.Settlements.First();
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodStorm, Cell = w.Settlements.Get(sid).Cell, Arg = 50 });
            int most = 0;
            for (int k = 0; k < 40; k++) { w.AdvanceTo(w.Now + SimTime.Minutes(15)); most = Math.Max(most, Count(w, sid, p => p.Why == WhyCode.Shock)); }
            Console.WriteLine("keeping in at most " + most + " of " + Count(w, sid, p => true));
            Assert.Greater(most, 0, "a gale over the village sends its people in");
            var any = 0; for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive && p.At == sid && p.Why == WhyCode.Shock && p.Act == ActivityKind.Idle) { StringAssert.Contains("a gale is blowing", HistoryText.Doing(w, in p)); any++; break; } }
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void DeepTimeFeelsNoShock()
        {
            var w = TestWorlds.Make(615);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.AreEqual(0, Count(w, sid, p => p.Why == WhyCode.Shock), "nobody is out and about in deep time");
        }
    }
}
