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
    /// Stone from the ground (C1.3, D2-092): rock is known only where people saw it; a village that wants stone opens a quarry at the
    /// rock it knows; quarrying cuts and carries loads home (the walk costs); a house raised with stone in store stands on footings and
    /// lasts twice as long. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class StoneTests
    {
        /// <summary>A world and a village with rock showing within reach (and the nearest such cell), or false.</summary>
        static bool WithRock(out World w, out int sid, out int rock)
        {
            foreach (ulong seed in new ulong[] { 701, 702, 703, 704, 705, 706, 707, 708 })
            {
                w = TestWorlds.Make(seed);
                var P = w.Planet;
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    var cells = new System.Collections.Generic.List<int>();
                    w.Hazards.CellsWithin(w, s.Cell, Stone.Reach, cells);
                    rock = -1; double bd = double.MaxValue;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (!Stone.Outcrop(P, cells[i])) continue;
                        double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[cells[i]], P.Radius);
                        if (d < bd) { bd = d; rock = cells[i]; }
                    }
                    if (rock >= 0 && bd > 150 && bd <= Stone.Reach) return true;
                }
            }
            w = null; sid = 0; rock = -1;
            return false;
        }

        static void Know(World w, int sid, int rock)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.StoneCell = 0; s.StoneDist = 0;
            Stone.Saw(w, sid, w.Residents(sid)[0], rock);
        }

        [Test]
        public void RockIsKnownWhereItsPeopleSawItAndTheFirstSightIsHistory()
        {
            Assume.That(WithRock(out var w, out int sid, out int rock), "a village with rock within reach");
            ref var s0 = ref w.Settlements.Get(sid);
            s0.StoneCell = 0; s0.StoneDist = 0; s0.StoneEvent = 0;   // (premise: nobody has been there yet)
            Assert.AreEqual(-1, Stone.Known(in w.Settlements.Get(sid)), "nothing known before anyone goes");
            // someone goes to a cell far from any rock: nothing learnt
            int pid = w.Residents(sid)[0];
            long mark = w.History.LastId;
            Stone.Saw(w, sid, pid, rock);
            var s = w.Settlements.Get(sid);
            Assert.AreEqual(rock, Stone.Known(in s), "the rock they saw");
            Assert.Greater(s.StoneDist, 0f);
            Assert.AreNotEqual(0L, s.StoneEvent);
            Assert.IsTrue(w.History.TryGet(s.StoneEvent, out var e));
            Assert.AreEqual(HistoryKind.StoneFound, e.Kind);
            Assert.AreEqual(pid, e.Other.Id, "who saw it");
            Console.WriteLine(HistoryText.Line(w, e));
        }

        [Test]
        public void AVillageThatWantsStoneOpensAQuarryAtTheRockItKnows()
        {
            Assume.That(WithRock(out var w, out int sid, out int rock), "a village with rock within reach");
            Know(w, sid, rock);
            // premise: a household lives in a hut (it will raise a house: footings wanted)
            ref var hh = ref w.Households.Get(w.HouseholdsOf(sid)[0]); hh.Shelter = Construction.Hut;
            ref var s = ref w.Settlements.Get(sid);
            s.StoneWant = (float)Stone.Want(w, sid);
            Assert.Greater(s.StoneWant, 0f, "footings wanted");
            s.Project = 0;
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid, "a quarry begun");
            var q = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.Quarry, q.Kind);
            Assert.AreEqual(w.Settlements.Get(sid).StoneEvent, q.Cause, "because they found the rock");
            double d = Math.Sqrt((double)q.X * q.X + (double)q.Y * q.Y);
            Assert.AreEqual(w.Settlements.Get(sid).StoneDist, d, 60.0, "at the rock, not by the green");
            ref var s2 = ref w.Settlements.Get(sid); s2.Woodstore = 50;
            for (int k = 0; k < 20 && w.Settlements.Get(sid).Project != 0; k++) Construction.Work(w, sid, 0, 5, 1.0, w.Residents(sid)[0]);
            Assert.AreEqual(bid, w.Settlements.Get(sid).Quarry, "opened");
            Assert.AreEqual(bid, Stone.Quarry(w, sid));
        }

        [Test]
        public void QuarryingCutsStoneAndEveryLoadIsCarriedHome()
        {
            double walk = 1.25, r0 = 3.0;
            Assert.Less(Stone.Rate(r0, 800, walk), Stone.Rate(r0, 300, walk), "farther rock, fewer loads an hour");
            Assert.AreEqual(1.0 / (1.0 / r0 + 2.0 * 600 / walk / 3600.0), Stone.Rate(r0, 600, walk), 1e-9, "a load's cutting and its carry home both ways");
            Assume.That(WithRock(out var w, out int sid, out int rock), "a village with rock within reach");
            Know(w, sid, rock);
            int qid = Structures.Begin(w, sid, Structures.Quarry, 0, CauseRel.CausedBy);
            ref var sq = ref w.Settlements.Get(sid); sq.Woodstore = 50;
            while (w.Settlements.Get(sid).Project != 0) Construction.Work(w, sid, 0, 5, 1.0, 0);
            double s0 = w.Settlements.Get(sid).Stone;
            double got = Stone.Cut(w, sid, qid, 4.0, r0, false);
            Assert.AreEqual(4.0 * Stone.Rate(r0, w.Settlements.Get(sid).StoneDist, w.Config.WalkSpeed), got, 1e-9);
            Assert.AreEqual(s0 + got, w.Settlements.Get(sid).Stone, 1e-9, "into the store");
            Assert.AreEqual(got, w.Buildings.Get(qid).Stock, 1e-4, "the face grows by what was cut");
            Console.WriteLine("rock " + w.Settlements.Get(sid).StoneDist.ToString("0") + " m off: " + got.ToString("0.0") + " loads home in 4 hours");
        }

        [Test]
        public void AHouseRaisedWithStoneInStoreStandsOnFootingsAndLastsTwiceAsLong()
        {
            var w = TestWorlds.Make(709);
            int sid = w.Settlements.First();
            int hid = w.HouseholdsOf(sid)[0];
            // premise: the household lives in a hut it is making a house of; the store has stone; wood to build with
            ref var hh = ref w.Households.Get(hid); hh.Shelter = Construction.Hut; hh.Build = 0f;
            if (hh.Building != 0) w.Buildings.Get(hh.Building).Kind = Construction.Hut;
            ref var s = ref w.Settlements.Get(sid); s.Stone = 100; s.Woodstore = 500; s.Project = 0; s.Wall = 0;
            for (int k = 0; k < 200 && w.Households.Get(hid).Shelter < Construction.House; k++) Construction.Work(w, sid, hid, 5, 1.0, 0);
            Assert.AreEqual(Construction.House, w.Households.Get(hid).Shelter);
            var b = w.Buildings.Get(w.Households.Get(hid).Building);
            Assert.AreEqual(1, b.Mat, "on stone footings");
            Assert.AreEqual(Stone.FootingLoads, b.Stone, 1e-4);
            Assert.AreEqual(100 - Stone.FootingLoads, w.Settlements.Get(sid).Stone, 1e-9, "out of the store");
            // left empty for 70 years: a house on posts falls in at 60, one on footings at 120
            var left = b; left.Left = w.Now - SimTime.Years(70); left.Household = 0;
            var onPosts = left; onPosts.Mat = 0;
            Assert.IsTrue(Homes.Fallen(in onPosts, w.Now), "on posts: fallen");
            Assert.IsFalse(Homes.Fallen(in left, w.Now), "on footings: still standing");
            Assert.AreEqual(2.0 * Homes.FallYears(in onPosts), Homes.FallYears(in left), 1e-9);
            // with no stone, a house on posts
            ref var s2 = ref w.Settlements.Get(sid); s2.Stone = 10;
            int hid2 = w.HouseholdsOf(sid)[1];
            ref var h2 = ref w.Households.Get(hid2); h2.Shelter = Construction.Hut; h2.Build = 0f;
            for (int k = 0; k < 200 && w.Households.Get(hid2).Shelter < Construction.House; k++) Construction.Work(w, sid, hid2, 5, 1.0, 0);
            Assert.AreEqual(0, w.Buildings.Get(w.Households.Get(hid2).Building).Mat, "no stone: on posts");
        }

        [Test]
        public void AVillageCutsStoneAtItsQuarryAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                Assume.That(WithRock(out var w, out int sid, out int rock), "a village with rock within reach");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                Know(w, sid, rock);
                ref var s = ref w.Settlements.Get(sid);
                Structures.Place(w, sid, Structures.Quarry, out float qx, out float qy);
                Structures.Standing(w, sid, Structures.Quarry, qx, qy);
                // premise: households in huts (footings wanted); food enough to stay off the wild
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); if (i % 2 == 0) { h.Shelter = Construction.Hut; if (h.Building != 0) w.Buildings.Get(h.Building).Kind = Construction.Hut; } }
                ref var s2 = ref w.Settlements.Get(sid); s2.Granary = 30 * s2.Population; s2.Stone = 0;
                w.AdvanceTo(w.Now + SimTime.Days(6));
                var s3 = w.Settlements.Get(sid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": stone cut " + w.Metrics.StoneCut.ToString("0") + " loads in " + w.Metrics.QuarryHours.ToString("0") + " h at " + s3.StoneDist.ToString("0") + " m; store " + s3.Stone.ToString("0") + " (wants " + s3.StoneWant.ToString("0") + "), footings " + w.Metrics.Footings);
                Assert.Greater(w.Metrics.StoneCut, 5, (coarse ? "COARSE" : "lived") + ": stone was cut and carried home");
                Assert.Greater(w.Metrics.QuarryHours, 0);
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
