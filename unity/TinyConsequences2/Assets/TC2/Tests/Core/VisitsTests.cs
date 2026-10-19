using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Health;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// People across water (C9.5c, D2-166): a COARSE day's visits between neighbours - the sickness and the news they carry, the ways they wear - go only to
    /// villages a way on foot joins, as a lived visitor walks one; across water villages meet as their boats' crews do. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class VisitsTests
    {
        /// <summary>A's COARSE days for `days` days, the visit tally on: what its visits did.</summary>
        static double[] CoarseDays(World w, int days)
        {
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            Disease.VisitTally = new double[Disease.VisitTallyN];
            try { w.AdvanceTo(w.Now + SimTime.Days(days)); return Disease.VisitTally; }
            finally { Disease.VisitTally = null; }
        }

        [Test]
        public void ACoarseDaysVisitsGoOnlyWhereAWayOnFootLeads()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            double d = Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius);
            Assert.IsFalse(Disease.Visited(w, a, b, d), "no way on foot: no visitors walk there");
            Assert.IsFalse(Disease.Visited(w, b, a, d));
            // a neighbour a way on foot does join, if the world has one: visited
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == a || o == b || !w.Settlements.Get(o).Inhabited || w.Routes.Get(w, a, o) == null) continue;
                double d2 = Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius);
                if (d2 <= 6000) { Assert.IsTrue(Disease.Visited(w, a, o, d2), "a way on foot: visited"); break; }
            }
            double use0 = w.Ways.UseOf(w.Now, a, b);
            var t = CoarseDays(w, 30);
            Console.WriteLine("thirty COARSE days: visits along a way " + t[0].ToString("0.0") + ", across water " + t[1].ToString("0.0") + "; news met " + t[4] + " and " + t[5]);
            Assert.AreEqual(0.0, t[1], "no visit across the water");
            Assert.AreEqual(0.0, t[3], "nobody caught a sickness on one");
            Assert.AreEqual(0.0, t[5], "no news met across it on a day's visits");
            Assert.LessOrEqual(w.Ways.UseOf(w.Now, a, b), use0 + 1e-9, "no way worn across the water");
            // --oldvisits: the day's visits crossed it as if a road ran there
            var w2 = AcrossTests.Across(out int a2, out int b2);
            Disease.OldVisits = true;
            try
            {
                var t2 = CoarseDays(w2, 30);
                Console.WriteLine("--oldvisits: visits along a way " + t2[0].ToString("0.0") + ", across water " + t2[1].ToString("0.0") + "; news met " + t2[4] + " and " + t2[5]);
                Assert.Greater(t2[1], 0.0, "--oldvisits: visits across the water");
                Assert.Greater(w2.Ways.UseOf(w2.Now, a2, b2), 0.0, "--oldvisits: a way worn across it");
                // the visits along a way keep their odds: a lived visitor picks among all neighbours and does not go where no way leads - those visits
                // are lost, never spread over the rest (a first version gave 17.5 along a way against the old rule's 9.6)
                Assert.That(t[0], Is.InRange(0.7 * t2[0], 1.3 * t2[0]), "visits along a way as before");
            }
            finally { Disease.OldVisits = false; }
        }
    }
}
