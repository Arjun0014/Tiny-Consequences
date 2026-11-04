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

        // ------------------------------------------------------------------ (session 32 review) what a shock must not do

        [Test]
        public void AGaleAtNightLetsThoseAtHomeSleep()
        {
            // a gale blowing over a village through the night: those at home are in already - they sleep, not stand up to keep in until the wind drops
            var w = TestWorlds.Make(617);
            int sid = w.Settlements.First();
            long local = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Day + SimTime.FloorMod(20 * SimTime.Hour - local, SimTime.Day));   // (the next evening, 8 pm there)
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodStorm, Cell = w.Settlements.Get(sid).Cell, Arg = 50 });
            int awake = 0, asleep = 0, nightsSeen = 0;
            for (int k = 0; k < 48; k++)
            {
                w.AdvanceTo(w.Now + SimTime.Minutes(15));
                long t = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
                if (t < 1 * SimTime.Hour || t > 4 * SimTime.Hour) continue;
                nightsSeen++;
                awake += Count(w, sid, p => p.At == p.Home && p.Why == WhyCode.Shock && p.Act == ActivityKind.Idle && p.AgeYears(w.Now) >= 14);
                asleep += Count(w, sid, p => p.At == p.Home && p.Act == ActivityKind.Sleep);
            }
            Console.WriteLine("1-4 am, a quarter-hour at a time: keeping in awake " + awake + ", asleep " + asleep + " (" + nightsSeen + " looks)");
            Assume.That(nightsSeen > 0);
            Assert.AreEqual(0, awake, "at night those at home sleep through the gale");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void TheSickAbedStayAbedWhenAStoneFalls()
        {
            // a sickness in the village; a stone falls beyond the blast: the sick abed at home do not get up and run to the crowd on the green
            var w = TestWorlds.Make(613);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodPlague, Cell = w.Settlements.Get(sid).Cell });
            int abed = 0;
            for (int d = 0; d < 40 && abed < 2; d++) { w.AdvanceTo(w.Now + SimTime.Day); abed = Count(w, sid, p => p.At == p.Home && p.SickPhase == TC2.Core.Health.Disease.Infectious); }
            Assume.That(abed >= 1, "someone abed with the sickness");
            w.RequestResolution(Resolution.Activity, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hours(10));
            abed = Count(w, sid, p => p.At == p.Home && p.SickPhase == TC2.Core.Health.Disease.Infectious);
            Assume.That(abed >= 1, "still abed in the lived days");
            int c = w.Settlements.Get(sid).Cell; c = w.Planet.Grid.Nbr4[c * 4]; c = w.Planet.Grid.Nbr4[c * 4];
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = c });
            w.AdvanceTo(w.Now + SimTime.Minutes(3));
            int sickRunning = Count(w, sid, p => p.SickPhase == TC2.Core.Health.Disease.Infectious && p.Why == WhyCode.Shock);
            Console.WriteLine("abed " + abed + "; sick taken by the shock " + sickRunning + ", startled in all " + Count(w, sid, p => p.Why == WhyCode.Shock));
            Assert.AreEqual(0, sickRunning, "the sick abed stay abed");
        }

        [Test]
        public void AQuakeRehomesNoHouseholdInAHomeItBringsDown()
        {
            // a great quake under villages with an empty home: a household whose home falls may take the empty one - never one the same quake brings down
            // (it would lose its home twice in the same moment: two takings told, or a taking told and the household camping after all)
            // premise: in each village the last home of the list made empty (its household dies out), so a home that falls before it in the list finds it
            int cases = 0, bad = 0;
            foreach (ulong seed in new ulong[] { 611, 615, 617 })
            {
                var w = TestWorlds.Make(seed);
                w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Days(3));
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    if (!w.Settlements.Get(sid).Inhabited) continue;
                    var bl = w.BuildingsOf(sid);
                    int last = 0;
                    for (int i = bl.Count - 1; i >= 0 && last == 0; i--) { var b = w.Buildings.Get(bl[i]); if (b.Household != 0 && TC2.Core.Places.Structures.IsHome(b.Kind) && b.Done != 0) last = b.Household; }
                    if (last == 0) continue;
                    var mem = new System.Collections.Generic.List<int>();
                    for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var q = w.People.Get(id); if (q.Alive && q.Household == last) mem.Add(id); }
                    foreach (int id in mem) { ref var q = ref w.People.Get(id); w.Agents.Kill(ref q, 0, 1); }
                    long mark = w.History.LastId;
                    w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodQuake, Cell = w.Settlements.Get(sid).Cell, Arg = 8 });
                    w.AdvanceTo(w.Now + SimTime.Minutes(30));
                    var taken = new System.Collections.Generic.Dictionary<int, int>();
                    var drowned = new System.Collections.Generic.HashSet<int>();   // (the quake's wave may take the home they took: another blow, not this one)
                    long cast = 0, at = 0;
                    for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                    {
                        var e = w.History.At(i);
                        if (e.Kind == HistoryKind.GodCommand) { cast = e.Id; at = e.Time; }
                        if (e.Kind == HistoryKind.HomesDrowned) drowned.Add(e.Settlement);
                        if (e.Kind == HistoryKind.HomeTaken && e.Root == cast && cast != 0 && e.Time == at) { cases++; taken.TryGetValue(e.Subject.Id, out int n); taken[e.Subject.Id] = n + 1; }
                    }
                    foreach (var kv in taken)
                    {
                        bool gone = !w.Households.Exists(kv.Key), homeless = !gone && w.Households.Get(kv.Key).Building == 0 && !drowned.Contains(w.Households.Get(kv.Key).Settlement);
                        if (kv.Value > 1 || homeless) { bad++; Console.WriteLine("seed " + seed + ": household " + kv.Key + " took a home " + kv.Value + " time(s) in one quake" + (homeless ? " and camps after all" : "")); }
                    }
                    w.AdvanceTo(w.Now + SimTime.Days(10));
                }
            }
            Console.WriteLine(cases + " homes taken in quakes, " + bad + " households that lost one they took");
            Assert.Greater(cases, 0, "homes were taken in the quakes");
            Assert.AreEqual(0, bad, "no household loses a home it took in the same quake");
        }

        [Test]
        public void ARoofLightningBurnsIsWhatLeavesItsHouseholdToFindAnother()
        {
            // a bolt among the homes burns a roof: the home its household then takes (or the lot it clears) follows from the burning, not from the bolt alone
            var w = TestWorlds.Make(615);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(4));
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            var P = w.Planet;
            int cell = -1;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count && cell < 0; i++)
            {
                var b = w.Buildings.Get(bl[i]);
                if (b.Household == 0 || !TC2.Core.Places.Structures.IsHome(b.Kind)) continue;
                var at = s.LocalToSphere(b.X, b.Y, P.Radius);
                int c = P.Grid.CellOf(at);
                if (Vec3d.ArcDistance(at, P.Grid.Center[c], P.Radius) < 30.0) cell = c;
            }
            Assume.That(cell >= 0, "a home near its cell's heart");
            // (premise: an empty home in the village to take - the last home's household dies out)
            int last = 0;
            double far = 31.0;   // (the home farthest from where the bolt falls: never the one near its heart it burns)
            for (int i = 0; i < bl.Count; i++)
            {
                var b = w.Buildings.Get(bl[i]);
                if (b.Household == 0 || !TC2.Core.Places.Structures.IsHome(b.Kind) || b.Done == 0) continue;
                double d = Vec3d.ArcDistance(w.Settlements.Get(sid).LocalToSphere(b.X, b.Y, P.Radius), P.Grid.Center[cell], P.Radius);
                if (d > far) { far = d; last = b.Household; }
            }
            Assume.That(last != 0, "a household to die out");
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var q = ref w.People.Get(id); if (q.Alive && q.Household == last) w.Agents.Kill(ref q, 0, 1); }
            long burn = 0; int told = 0;
            for (int k = 0; k < 6 && burn == 0; k++)
            {
                long mark = w.History.LastId;
                w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodLightning, Cell = cell });
                w.AdvanceTo(w.Now + SimTime.Hours(2));
                for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count && burn == 0; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.BuildingBurned || e.ArgA != 2) continue;
                    burn = e.Id;
                    for (int j = w.History.FirstIndexAfter(mark); j < w.History.Count; j++)
                    {
                        var f = w.History.At(j);
                        if ((f.Kind == HistoryKind.HomeTaken || f.Kind == HistoryKind.HomeCleared) && f.Time == e.Time && (f.Subject == e.Subject || f.Other == e.Subject))
                        { told++; Console.WriteLine(HistoryText.Describe(w, in f) + " <- " + f.Cause1 + " (the burning " + burn + ")"); Assert.AreEqual(burn, f.Cause1, "it follows from the burning"); }
                    }
                }
                w.AdvanceTo(w.Now + SimTime.Days(1));
            }
            Assume.That(burn != 0, "a roof burned");
            Assert.Greater(told, 0, "its household took the empty home");
        }

        [Test]
        public void ASaveTakenWhileAVillageRunsFromAStoneContinuesIdentically()
        {
            // a stone falls, a quake and a gale follow: saved while the startles are still queued and while the village runs, the loaded world lives on alike
            var w = TestWorlds.Make(615);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(4));
            int sid = w.Settlements.First();
            int c = w.Settlements.Get(sid).Cell; c = w.Planet.Grid.Nbr4[c * 4]; c = w.Planet.Grid.Nbr4[c * 4];
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = c });
            w.Issue(new Command { Time = w.Now + SimTime.Minutes(20), Kind = CommandKind.GodStorm, Cell = w.Settlements.Get(sid).Cell, Arg = 50 });
            foreach (long at in new[] { 10L, 5 * SimTime.Minute, 40 * SimTime.Minute })
            {
                // (10 s: the startles queued; then while they run and keep together; then in the gale)
                w.AdvanceTo(w.Now + at);
                var w2 = TC2.Core.Persistence.SaveFile.Load(TC2.Core.Persistence.SaveFile.Save(w, 0, TC2.Core.Persistence.OfflineMode.Paused, 0, 0, ""), out _);
                Assert.AreEqual(w.StateHash(), w2.StateHash(), "the loaded world is the saved one");
                long until = w.Now + 90 * SimTime.Minute;
                w.AdvanceTo(until); w2.AdvanceTo(until);
                Assert.AreEqual(w.StateHash(), w2.StateHash(), "and both live on alike");
            }
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
