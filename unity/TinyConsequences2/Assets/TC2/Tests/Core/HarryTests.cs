using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The harrying (C8.1a): a band broken on a village's standing palisade burns and tramples the crop standing outside the walls, a share by its
    /// numbers, the loss caused by the raid; behind no wall, with no crop standing, or with the rule off, nothing. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class HarryTests
    {
        static World Pair(out int a, out int b)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        // premise: the crop growing in the raided village's fields, the band two of the raiders' people (a small band, broken on the walls)
        static World Growing(out int a, out int b, bool wall)
        {
            var w = Pair(out a, out b);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            for (int d = 0; d < 60 && w.Settlements.Get(a).Crop != CropStage.Growing; d++) w.AdvanceTo(w.Now + SimTime.Day);
            ref var sa = ref w.Settlements.Get(a);
            if (wall) { sa.Wall = 2; sa.WallCond = 1f; sa.WallR = 40f; sa.Works |= Construction.Palisade; }
            return w;
        }

        static List<int> Band(World w, int sid, int n)
        {
            var raiders = new List<int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count && raiders.Count < n; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.AgeYears(w.Now) >= 18 && q.AgeYears(w.Now) < 45 && q.Health > 0.8) raiders.Add(res[i]); }
            return raiders;
        }

        static int Harried(World w, long mark, out long ev)
        {
            int n = 0; ev = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.FieldsHarried) { n++; ev = e.Id; } }
            return n;
        }

        [Test]
        public void ABandBrokenOnTheWallsHarriesTheCropOutsideThem()
        {
            Conflict.HarryOff = false;
            var w = Growing(out int a, out int b, wall: true);
            Assume.That(b != 0 && w.Settlements.Get(a).Crop == CropStage.Growing, "two villages, a crop growing");
            var raiders = Band(w, b, 2);
            double before = w.Settlements.Get(a).CropBurnt;
            long mark = w.History.LastId;
            w.Conflict.FallNow(b, a, raiders, seen: true);
            long raid = 0; int outcome = -1;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.Raid) { raid = e.Id; outcome = e.ArgB / 100; } }
            Assume.That(outcome == 2, "two against a walled village are driven off");
            Assert.AreEqual(1, Harried(w, mark, out long hev), "it harried the fields outside the walls");
            Assert.AreEqual(before + Conflict.HarryPerRaider * raiders.Count, w.Settlements.Get(a).CropBurnt, 1e-6, "a share by its numbers");
            Assert.IsTrue(w.History.TryGet(hev, out var he) && he.Cause1 == raid, "caused by the raid");
            StringAssert.StartsWith("Driven off from its walls, the band from", HistoryText.Describe(w, in he));
        }

        [Test]
        public void NoHarryingBehindNoWallOrWithTheRuleOff()
        {
            foreach (int k in new[] { 0, 1 })
            {
                Conflict.HarryOff = k == 1;
                try
                {
                    var w = Growing(out int a, out int b, wall: k == 1);
                    var raiders = Band(w, b, 2);
                    long mark = w.History.LastId;
                    w.Conflict.FallNow(b, a, raiders, seen: true);
                    Assert.AreEqual(0, Harried(w, mark, out _), k == 0 ? "an open village's crop is not harried for a band driven off" : "the rule off");
                }
                finally { Conflict.HarryOff = false; }
            }
        }
    }
}
