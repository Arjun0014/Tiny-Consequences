using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Timber from the woods (C1.2, D2-091): a load of wood is felled somewhere - its mass comes off a woodlot's forest share; the woodlot
    /// moves on as it is cut down and the walk costs hours; fields keep the woods off what they farm and released fields grow back.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class WoodsTests
    {
        [Test]
        public void FellingTakesItsMassFromTheWoodlotsForest()
        {
            var w = TestWorlds.Make(601);
            int sid = w.Settlements.First();
            int c = Woods.Ensure(w, sid);
            Assert.GreaterOrEqual(c, 0, "a village has woods within reach");
            var P = w.Planet;
            P.Forest[c] = 0.8f;   // premise: a good wood
            double f0 = P.Forest[c], loads = 40;
            double had = Woods.Fell(w, sid, loads);
            Assert.AreEqual(loads, had, 1e-9);
            double expect = f0 - loads * Woods.LoadKg / (Woods.KgPerHa * P.AreaHa(c));
            Assert.AreEqual(expect, P.Forest[c], 1e-5, "the loads' mass came off its forest share");
            Assert.AreEqual(loads, w.Metrics.WoodFelled, 1e-9);
            // what a lived woodcutter brings home comes off it too
            double store0 = w.Settlements.Get(sid).Woodstore, f1 = P.Forest[c];
            double got = Woods.Cut(w, sid, 3.0, 1.2, false);
            Assert.Greater(got, 0);
            Assert.AreEqual(store0 + got, w.Settlements.Get(sid).Woodstore, 1e-9, "into the store");
            Assert.AreEqual(f1 - got * Woods.LoadKg / (Woods.KgPerHa * P.AreaHa(c)), P.Forest[c], 1e-5, "and out of the woods");
        }

        [Test]
        public void FartherWoodsCostMoreHoursALoad()
        {
            double walk = 1.25, r0 = 1.2;
            Assert.AreEqual(r0, Woods.Rate(r0, 100, walk), 1e-12, "the near woods as always");
            Assert.AreEqual(r0, Woods.Rate(r0, Woods.Near, walk), 1e-12);
            double r600 = Woods.Rate(r0, 600, walk), r1500 = Woods.Rate(r0, 1500, walk);
            Assert.Less(r600, r0); Assert.Less(r1500, r600);
            // a load from 1.5 km: an extra walk of 2 x 1,290 m at 1.25 m/s (~0.57 h) on ~0.83 h of felling
            Assert.AreEqual(1.0 / (1.0 / r0 + 2.0 * (1500 - Woods.Near) / walk / 3600.0), r1500, 1e-9);
            Assert.Less(Woods.AfterWalk(1500, walk), 1.0, "a COARSE stint pays the walk out and home");
            Console.WriteLine("loads an hour: near " + r0 + ", 600 m " + r600.ToString("0.00") + ", 1.5 km " + r1500.ToString("0.00") + " (COARSE x" + Woods.AfterWalk(1500, walk).ToString("0.00") + ")");
        }

        [Test]
        public void TheWoodlotMovesOnAsTheWoodsAreCutAndHistoryTellsIt()
        {
            var w = TestWorlds.Make(602);
            int sid = w.Settlements.First();
            var P = w.Planet;
            // premise: the woods within 650 m are cut down to scrub
            ref var s = ref w.Settlements.Get(sid);
            var near = new System.Collections.Generic.List<int>();
            w.Hazards.CellsWithin(w, s.Cell, 650, near);
            for (int i = 0; i < near.Count; i++) if (Vec3d.ArcDistance(s.Anchor, P.Grid.Center[near[i]], P.Radius) <= 650) P.Forest[near[i]] = 0.03f;
            long mark = w.History.LastId;
            int c = Woods.Choose(w, sid);
            Assert.GreaterOrEqual(c, 0, "woods farther off");
            var s2 = w.Settlements.Get(sid);
            Assert.Greater(s2.WoodDist, 600f, "beyond the cut woods");
            Assert.GreaterOrEqual(s2.WoodFar, (byte)1);
            double wx = s2.WoodX, wy = s2.WoodY;
            Assert.Greater(Math.Sqrt(wx * wx + wy * wy), 500, "its woodcutters walk out there");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.WoodsReceded && w.History.At(i).Settlement == sid) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told, "the woods' retreat is history");
            // cutting a woodlot down moves the cutters to the next
            P.Forest[c] = (float)(Woods.Worth + 0.001);
            double had = Woods.Fell(w, sid, 30);
            Assert.Greater(had, 0);
            Assert.AreNotEqual(c, Woods.Lot(in w.Settlements.Get(sid)), "the next woodlot");
            Assert.GreaterOrEqual(P.Forest[c], (float)Woods.Floor, "felling leaves stumps and scrub");
        }

        [Test]
        public void FieldsKeepTheWoodsOffWhatTheyFarmAndReleasedFieldsGrowBack()
        {
            var w = TestWorlds.Make(603);
            var P = w.Planet;
            int sid = w.Settlements.First();
            var fields = w.FieldCellsOf(sid);
            Assert.Greater(fields.Count, 0, "the village farms");
            int c = -1; double best = 0;
            for (int i = 0; i < fields.Count; i++) { double f = Woods.Farmed(w, fields[i], w.PlanetSys.MeanTemp(fields[i])); if (f > best) { best = f; c = fields[i]; } }
            Assert.GreaterOrEqual(c, 0);
            Assert.Greater(best, 0.05, "a field cell is partly farmed");
            P.Forest[c] = 0.95f;   // premise: woods stand over it
            w.PlanetSys.GrowVegetation(w, 0.1);
            double open = Woods.Open(w, c, w.PlanetSys.MeanTemp(c));
            Assert.LessOrEqual((double)P.Forest[c], open + 1e-6, "the farmed share is kept clear");
            // the fields released (the village gone): the woods come back by their own law
            double f0 = P.Forest[c];
            P.FieldOwner[c] = 0;
            for (int k = 0; k < 40; k++) w.PlanetSys.GrowVegetation(w, 1.0);
            Console.WriteLine("field cell forest: farmed " + f0.ToString("0.00") + " -> 40 years after " + P.Forest[c].ToString("0.00"));
            Assert.Greater((double)P.Forest[c], f0, "released, the woods grow back");
        }

        [Test]
        public void AVillageBurnsAndBuildsFromItsOwnWoodsAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(604);
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                int sid = w.Settlements.First();
                double felled0 = w.Metrics.WoodFelled;
                ref var s = ref w.Settlements.Get(sid);
                s.Woodstore = 0;   // premise: the store is empty - the hearths and the work want wood
                w.AdvanceTo(w.Now + SimTime.Days(6));
                double felled = w.Metrics.WoodFelled - felled0;
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": felled " + felled.ToString("0") + " loads in six days, store " + w.Settlements.Get(sid).Woodstore.ToString("0") + ", woodlot at " + w.Settlements.Get(sid).WoodDist.ToString("0") + " m");
                Assert.Greater(felled, 10, (coarse ? "COARSE" : "lived") + ": its wood was felled from the woods");
                Assert.Greater(w.Settlements.Get(sid).Woodstore, 0);
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
