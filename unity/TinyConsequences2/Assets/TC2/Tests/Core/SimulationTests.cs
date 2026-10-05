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
                if (n != 1) return "person " + id + " has " + n + " live step events";
                if (!w.Residents(p.Home).Contains(id)) return "person " + id + " missing from residents of " + p.Home;
                if (!w.Members(p.Household).Contains(id)) return "person " + id + " missing from household " + p.Household;
                if (p.At != 0 && p.At != p.Home && !w.Visitors(p.At).Contains(id)) return "visitor " + id + " not listed at " + p.At;
                if (p.Step == StepKind.Travel && p.At != 0) return "traveller " + id + " is also at a place";
                if (p.T1 < w.Now && p.Step != StepKind.None) return "person " + id + " step ended in the past";
            }
            int pop = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) pop += w.Settlements.Get(s).Population;
            int alive = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) if (w.People.Get(id).Alive) alive++;
            if (pop != alive) return "population cache " + pop + " != living " + alive;
            return null;
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
