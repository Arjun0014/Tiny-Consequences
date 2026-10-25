using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Prospecting (C5.3c): a village whose smiths lack ore looks for it twice a year - the land within its reach first, where the nearest body of what it
    /// lacks becomes known and its finding is told; finding none, someone goes farther as a scout does. With `--oldprospect` nobody looks. Mechanisms,
    /// not outcomes.
    /// </summary>
    [TestFixture]
    public class ProspectTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Premise: a village of smiths with no metal and no ore known, founded years ago; a land cell at 1.2-1.8 km from it (or -1).</summary>
        static int Smiths(World w, int sid, double from, double to)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.BestMetal = 0.5f; s.Metal = 0; s.OreCell = 0; s.OreDist = 0; s.Founded = w.Now - SimTime.Years(5); s.ProspectAt = 0;
            var P = w.Planet;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || P.Ice[c] > 0.5f || !TC2.Core.Planet.Shore.Dry(P, P.Grid.Center[c])) continue;
                double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                if (d >= from && d <= to) return c;
            }
            return -1;
        }

        [Test]
        public void ASmithsVillageLackingOreFindsTheBodyWithinItsReach()
        {
            var w = TestWorlds.Make(626);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            int cell = Smiths(w, sid, 1200, 1800);
            Assert.GreaterOrEqual(cell, 0, "premise: land a walk away");
            Mining.Reveal(w, cell, 0);   // premise: a body of copper there, unseen by the village
            Assert.IsTrue(Mining.Lacks(in w.Settlements.Get(sid), Mining.Copper), "premise: its smiths lack copper ore");
            try
            {
                Mining.ProspectOff = true;
                Mining.Prospect(w, sid, 1);
                Assert.AreEqual(0, w.Settlements.Get(sid).OreCell, "nobody looks with the rule off");
            }
            finally { Mining.ProspectOff = false; }
            Mining.Prospect(w, sid, 1);
            var s = w.Settlements.Get(sid);
            Assert.AreNotEqual(0, s.OreCell, "its people looked over their land and found ore");
            Assert.LessOrEqual(s.OreDist, Mining.Reach + 1e-6, "within the reach it digs");
            Assert.IsFalse(Mining.Lacks(in s, Mining.Copper), "no longer lacking");
            bool told = false;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.OreFound && e.Settlement == sid) told = true; }
            Assert.IsTrue(told, "its finding told");
            // twice a year, not every day
            long at = w.Settlements.Get(sid).ProspectAt;
            w.Settlements.Get(sid).OreCell = 0;
            Mining.Prospect(w, sid, 1);
            Assert.AreEqual(0, w.Settlements.Get(sid).OreCell, "nobody goes again before the half-year is out");
            Assert.AreEqual(at, w.Settlements.Get(sid).ProspectAt);
        }

        [Test]
        public void FindingNoneWithinReachSomeoneGoesFarther()
        {
            var w = TestWorlds.Make(627);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            // a village whose reach shows no ore of the ground's own rule
            int sid = 0;
            for (int v = w.Settlements.First(); v != 0 && sid == 0; v = w.Settlements.Next(v))
            {
                var near = new System.Collections.Generic.List<int>();
                w.Hazards.CellsWithin(w, w.Settlements.Get(v).Cell, Mining.Reach, near);
                bool any = false;
                foreach (int c in near) if (Mining.CopperAt(w, c)) { any = true; break; }
                if (!any) sid = v;
            }
            if (sid == 0) Assert.Inconclusive("premise: every village of this world has copper within reach");
            Smiths(w, sid, 0, 0);
            long scout0 = w.Settlements.Get(sid).ScoutAt;
            Mining.ProspectTally = new double[3];
            try
            {
                Mining.Prospect(w, sid, 1);
                Assert.AreEqual(1.0, Mining.ProspectTally[0], "a season gone looking");
                Assert.AreEqual(0.0, Mining.ProspectTally[1], "nothing within reach");
                Assert.AreEqual(0, w.Settlements.Get(sid).OreCell);
                Assert.AreEqual(1.0, Mining.ProspectTally[2], "someone went farther");
                Assert.AreEqual(w.Now, w.Settlements.Get(sid).ScoutAt, "and looked round as a scout does");
                Assert.AreNotEqual(scout0, w.Settlements.Get(sid).ScoutAt);
            }
            finally { Mining.ProspectTally = null; }
        }
    }
}
