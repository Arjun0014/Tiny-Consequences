using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>Shared world builders: premises must be states the world could reach (15 §3).</summary>
    public static class TestWorlds
    {
        public static WorldConfig Small(ulong seed = 7)
        {
            return new WorldConfig { Seed = seed, SurfaceN = 48, Settlements = 4, FoundersPerSettlement = 18 };
        }

        public static World Make(ulong seed = 7) => WorldGenerator.Create(Small(seed));

        /// <summary>
        /// A small world in which a village's people have seen copper (C5.1): its founders saw it, or - the premise - someone of it went to
        /// the nearest copper body within its reach and saw it there.
        /// </summary>
        public static World WithCopper(out int sid, ulong from = 481, ulong to = 600)
        {
            for (ulong seed = from; seed < to; seed++)
            {
                var w = Make(seed);
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).OreCell != 0) return w;
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    var near = new List<int>();
                    w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, 0.9 * TC2.Core.Places.Mining.Reach, near);
                    near.Sort();
                    for (int i = 0; i < near.Count; i++)
                        if (TC2.Core.Places.Mining.CopperAt(w, near[i])) { TC2.Core.Places.Mining.Saw(w, sid, 0, near[i]); if (w.Settlements.Get(sid).OreCell != 0) return w; }
                }
            }
            sid = 0;
            return null;
        }

        /// <summary>Every living person has exactly one live (non-stale) step event, and lists agree with state.</summary>
        public static string Audit(World w)
        {
            var live = new Dictionary<int, int>();
            for (int i = 0; i < w.Queue.RawCount; i++)
            {
                var e = w.Queue.RawAt(i);
                if (e.Time < w.Now) return "event in the past: " + e;
                if (e.Kind != EventKind.AgentStep || !w.People.Exists(e.Target)) continue;
                ref var p = ref w.People.Get(e.Target);
                if (p.Alive && p.ActVer == e.Version) live[e.Target] = live.TryGetValue(e.Target, out int n) ? n + 1 : 1;
            }
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                live.TryGetValue(id, out int n);
                int want = w.Resolution == Resolution.Coarse ? 0 : 1;
                if (n != want) return "person " + id + " has " + n + " live step events (" + w.Resolution + ")";
                if (!w.Residents(p.Home).Contains(id)) return "person " + id + " missing from residents of " + p.Home;
                if (!w.Members(p.Household).Contains(id)) return "person " + id + " missing from household " + p.Household;
                if (p.At != 0 && p.At != p.Home && !w.Visitors(p.At).Contains(id)) return "visitor " + id + " not listed at " + p.At;
                if (p.Step == StepKind.Travel && p.At != 0) return "traveller " + id + " is also at a place";
                if (w.Resolution == Resolution.Activity && p.T1 < w.Now && p.Step != StepKind.None) return "person " + id + " step ended in the past";
            }
            int pop = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) pop += w.Settlements.Get(s).Population;
            int alive = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) if (w.People.Get(id).Alive) alive++;
            if (pop != alive) return "population cache " + pop + " != living " + alive;
            // livestock (D2-048): every live animal in its household's list and only there, kept by a household that exists
            int listed = 0, beasts = 0;
            for (int id = w.Beasts.First(); id != 0; id = w.Beasts.Next(id))
            {
                ref var be = ref w.Beasts.Get(id);
                if (!be.Alive) continue;
                beasts++;
                if (!w.Households.Exists(be.Owner)) return "beast " + id + " of a missing household " + be.Owner;
                var hh = w.Households.Get(be.Owner);
                if (hh.Dissolved != 0) return "beast " + id + " of a dissolved household " + be.Owner;
                if (!w.Settlements.Get(hh.Settlement).Inhabited) return "beast " + id + " left in an abandoned village";
                if (!w.BeastsOf(be.Owner).Contains(id)) return "beast " + id + " missing from its household's list";
            }
            for (int h = w.Households.First(); h != 0; h = w.Households.Next(h))
            {
                var list = w.BeastsOf(h);
                for (int i = 0; i < list.Count; i++)
                {
                    if (!w.Beasts.Exists(list[i]) || !w.Beasts.Get(list[i]).Alive || w.Beasts.Get(list[i]).Owner != h) return "household " + h + " lists beast " + list[i] + " that is not its live animal";
                    if (i > 0 && list[i] <= list[i - 1]) return "household " + h + "'s animals out of id order";
                }
                listed += list.Count;
            }
            if (listed != beasts) return "households list " + listed + " animals, " + beasts + " live";
            return null;
        }
    }

    [TestFixture]
    public class InspectorTests
    {
        [Test]
        public void EveryActivityReadsAsASentence()
        {
            // what people take up over a few lived days: each reads as what they do and why, never a code
            var w = TestWorlds.Make(77);
            var seen = new System.Collections.Generic.HashSet<ActivityKind>();
            for (int h = 0; h < 24 * 6; h++)
            {
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (!p.Alive) continue;
                    string text = TC2.Core.History.HistoryText.Doing(w, in p);
                    Assert.IsFalse(string.IsNullOrEmpty(text));
                    Assert.IsFalse(text.Contains("(" + p.Why + ")"), p.Act + " has no sentence: " + text);
                    seen.Add(p.Act);
                }
            }
            System.Console.WriteLine("activities seen: " + string.Join(", ", seen));
            Assert.Greater(seen.Count, 8);
        }
    }

    [TestFixture]
    public class DeterminismTests
    {
        [Test]
        public void SameSeedSameWorld()
        {
            var a = TestWorlds.Make(11); var b = TestWorlds.Make(11); var c = TestWorlds.Make(12);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "generation");
            Assert.AreNotEqual(a.StateHash(), c.StateHash(), "different seeds");
            a.AdvanceTo(SimTime.Days(3)); b.AdvanceTo(SimTime.Days(3));
            Assert.AreEqual(a.StateHash(), b.StateHash(), "after 3 days");
        }

        [Test]
        public void SlicingDoesNotChangeTheWorld()
        {
            var a = TestWorlds.Make(5); var b = TestWorlds.Make(5);
            long end = SimTime.Days(4);
            a.AdvanceTo(end);
            var rs = new RandomSequence(99);
            while (b.Now < end) b.AdvanceTo(Math.Min(end, b.Now + 1 + rs.NextBelow(5000)));
            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void EventBudgetsDoNotChangeTheWorld()
        {
            var a = TestWorlds.Make(6); var b = TestWorlds.Make(6);
            long end = SimTime.Days(3);
            a.AdvanceTo(end);
            int calls = 0;
            while (true)
            {
                var budget = Budget.Events(37);
                calls++;
                if (b.AdvanceTo(end, ref budget)) break;
            }
            Assert.Greater(calls, 10);
            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void SaveLoadContinuesIdentically()
        {
            var a = TestWorlds.Make(8);
            var rs = new RandomSequence(3);
            foreach (double days in new[] { 0.3, 1.7, 4.2 })
            {
                a.AdvanceTo(SimTime.Days(days) + rs.NextBelow(3600));
                byte[] bytes = SaveFile.Save(a, 1_000_000, OfflineMode.Living);
                var b = SaveFile.Load(bytes, out var hdr);
                Assert.AreEqual(a.StateHash(), b.StateHash(), "loaded state at day " + days);
                Assert.AreEqual(a.Now, hdr.WorldTime);
                long t = a.Now + SimTime.Days(1.5);
                a.AdvanceTo(t); b.AdvanceTo(t);
                Assert.AreEqual(a.StateHash(), b.StateHash(), "continuation after day " + days);
                Assert.IsNull(TestWorlds.Audit(b), "audit of the loaded world");
            }
        }

        [Test]
        public void InvariantsHoldOverAMonth()
        {
            var w = TestWorlds.Make(9);
            for (int d = 1; d <= 30; d++)
            {
                w.AdvanceTo(SimTime.Days(d) + 1234);
                string err = TestWorlds.Audit(w);
                Assert.IsNull(err, "day " + d + ": " + err);
            }
        }
    }

    [TestFixture]
    public class CoarseTests
    {
        static long Midnight(long t) => SimTime.StartOfDay(t) + SimTime.Day;

        [Test]
        public void EnteringAndLeavingCoarseKeepsTheWorldConsistentAndDeterministic()
        {
            var a = TestWorlds.Make(31); var b = TestWorlds.Make(31);
            foreach (var w in new[] { a, b })
            {
                w.AdvanceTo(SimTime.Days(2) + 5000);
                w.RequestResolution(Resolution.Coarse, Midnight(w.Now));
                w.AdvanceTo(SimTime.Days(6));
                Assert.AreEqual(Resolution.Coarse, w.Resolution);
                Assert.IsNull(TestWorlds.Audit(w), "audit in COARSE");
                w.RequestResolution(Resolution.Activity, Midnight(w.Now));
                w.AdvanceTo(SimTime.Days(9));
                Assert.AreEqual(Resolution.Activity, w.Resolution);
                Assert.IsNull(TestWorlds.Audit(w), "audit back in ACTIVITY");
            }
            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void SaveLoadInCoarseContinuesIdentically()
        {
            var a = TestWorlds.Make(32);
            a.RequestResolution(Resolution.Coarse, SimTime.Day);
            a.AdvanceTo(SimTime.Days(20) + 777);
            var b = SaveFile.Load(SaveFile.Save(a, 5, OfflineMode.Ancient), out _);
            a.AdvanceTo(SimTime.Days(60)); b.AdvanceTo(SimTime.Days(60));
            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void PeopleEatWorkAndHarvestInCoarse()
        {
            var w = TestWorlds.Make(33);
            w.RequestResolution(Resolution.Coarse, SimTime.Day);
            w.AdvanceTo(SimTime.Years(2));
            int unfed = 0, alive = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                alive++;
                if (w.Now - p.LastMeal > SimTime.Day) unfed++;
            }
            Assert.Greater(alive, 40);
            Assert.AreEqual(0, unfed, "everyone ate in the last day");
            int harvests = 0;
            for (int i = 0; i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.Harvest && w.History.At(i).Magnitude > 50) harvests++;
            Assert.GreaterOrEqual(harvests, 4, "fields were sown, tended and brought in by COARSE labour");
        }
    }

    [TestFixture]
    public class HazardTests
    {
        /// <summary>
        /// Premise: a dry spell (the soil along a road dried out) and a traveller on that road; the god strikes the
        /// ground ahead of them. Mechanism under test: the fire is perceived from the road at the moment it comes
        /// into sight, the traveller re-decides with the fire as the cause, and the chain reaches YOU.
        /// </summary>
        [Test]
        public void ATravellerMeetsAFireAheadAndReDecides()
        {
            var w = TestWorlds.Make(21);
            w.AdvanceTo(SimTime.Days(1) + 9 * SimTime.Hour); // morning of day 2
            // two settlements and a route between them
            int a = w.Settlements.First(), b = 0; Route route = null;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s)) { route = w.Routes.Get(w, a, s); if (route != null && route.Length > 1200) { b = s; break; } }
            Assume.That(route != null && b != 0, "no long enough route in this world");
            // a grown-up of A who is at home and awake
            int traveller = 0;
            var res = w.Residents(a);
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (p.Alive && p.At == a && p.AgeYears(w.Now) > 18 && p.Act != ActivityKind.Sleep) { traveller = p.Id; break; }
            }
            Assume.That(traveller != 0);
            Assert.IsTrue(w.Agents.StartTrip(traveller, b));
            // the dry spell along the road
            var P = w.Planet;
            for (int k = 0; k < route.Cell.Length; k++)
            {
                int c = route.Cell[k];
                P.SoilWater[c] = 0.02f; P.RainToday[c] = 0;
                P.Grass[c] = Math.Max(P.Grass[c], 0.6f); P.Forest[c] = Math.Max(P.Forest[c], 0.5f);
                for (int j = 0; j < 8; j++) { int nb = P.Grid.Nbr8[c * 8 + j]; P.SoilWater[nb] = 0.02f; P.RainToday[nb] = 0; P.Grass[nb] = Math.Max(P.Grass[nb], 0.6f); }
            }
            // the strike: two thirds of the way along, ahead of the traveller
            ref var tp = ref w.People.Get(traveller);
            double sAhead = tp.S0 + (tp.S1 - tp.S0) * 0.66;
            int target = P.Grid.CellOf(route.PointAt(sAhead));
            w.Issue(new Command { Time = w.Now + 60, Kind = CommandKind.GodLightning, Cell = target });
            long histBefore = w.History.LastId;
            w.AdvanceTo(w.Now + 4 * SimTime.Hour);
            // the traveller noticed and re-decided, with the fire as the cause
            bool found = false;
            for (int i = w.History.FirstIndexAfter(histBefore); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.JourneyInterrupted || e.Subject.Id != traveller) continue;
                found = true;
                Assert.AreEqual(RootKind.You, e.RootKind, "the chain ends at YOU");
                var chain = new long[16];
                int n = w.History.CausalChain(e.Id, chain);
                var kinds = new List<HistoryKind>();
                for (int k = 0; k < n; k++) if (w.History.TryGet(chain[k], out var ce)) kinds.Add(ce.Kind);
                CollectionAssert.Contains(kinds, HistoryKind.FireStarted);
                CollectionAssert.Contains(kinds, HistoryKind.LightningStrike);
                CollectionAssert.Contains(kinds, HistoryKind.GodCommand);
                break;
            }
            Assert.IsTrue(found, "no JourneyInterrupted for the traveller; interrupts " + w.Metrics.Interrupts);
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void NobodyKnowsAFireTheyCannotSee()
        {
            // a strike far from every settlement and road interrupts nobody
            var w = TestWorlds.Make(22);
            w.AdvanceTo(SimTime.Hours(30));
            var P = w.Planet;
            int far = -1; double best = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                double nearest = double.MaxValue;
                for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) nearest = Math.Min(nearest, Vec3d.ArcDistance(P.Grid.Center[c], w.Settlements.Get(s).Anchor, P.Radius));
                if (nearest > best) { best = nearest; far = c; }
            }
            P.SoilWater[far] = 0.02f; P.Grass[far] = 0.8f;
            long before = w.Metrics.Interrupts;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodLightning, Cell = far });
            w.AdvanceTo(w.Now + 2 * SimTime.Hour);
            Assert.AreEqual(before, w.Metrics.Interrupts, "interrupts from an unseen fire");
        }
    }

    [TestFixture]
    public class HistoryTests
    {
        [Test]
        public void ImportanceFlowsUpTheChain()
        {
            var ids = new IdAllocator();
            var h = new HistoryLedger();
            long root = h.Record(ids, new HistoryEvent { Kind = HistoryKind.LightningStrike, Time = 0, RootKind = RootKind.Nature });
            long fire = h.Record(ids, new HistoryEvent { Kind = HistoryKind.FireStarted, Time = 10, Cause1 = root });
            float before = h.At(h.IndexOf(root)).Importance;
            long famine = h.Record(ids, new HistoryEvent { Kind = HistoryKind.Famine, Time = 30, Cause1 = fire, Magnitude = 50 });
            Assert.Greater(h.At(h.IndexOf(root)).Importance, before + 5, "a small strike became important through its consequences");
            Assert.AreEqual(root, h.At(h.IndexOf(famine)).Root);
            Assert.AreEqual(RootKind.Nature, h.At(h.IndexOf(famine)).RootKind);
        }

        [Test]
        public void CompactionDropsWhatNoLongerMattersAndKeepsChainsConnected()
        {
            var ids = new IdAllocator();
            var h = new HistoryLedger();
            long drought = h.Record(ids, new HistoryEvent { Kind = HistoryKind.Drought, Time = 0, RootKind = RootKind.Nature });
            long fire = h.Record(ids, new HistoryEvent { Kind = HistoryKind.FireStarted, Time = 10, Cause1 = drought });
            long burned = h.Record(ids, new HistoryEvent { Kind = HistoryKind.JourneyInterrupted, Time = 20, Cause1 = fire, Tier = Tier.Personal });
            long kept = h.Record(ids, new HistoryEvent { Kind = HistoryKind.JourneyInterrupted, Time = 30, Cause1 = burned, Tier = Tier.Personal, Flags = EventFlags.Pinned });
            int dropped = h.Compact(1000 * SimTime.Year, r => false);
            Assert.AreEqual(2, dropped, "the old local fire and the unimportant personal event");
            Assert.Less(h.IndexOf(fire), 0);
            Assert.Less(h.IndexOf(burned), 0);
            Assert.GreaterOrEqual(h.IndexOf(drought), 0, "historical events are kept forever");
            Assert.AreEqual(drought, h.At(h.IndexOf(kept)).Cause1, "the kept event now points to its nearest kept ancestor");
        }
    }
}
