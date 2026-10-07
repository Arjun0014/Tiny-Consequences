using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Voyages (C9.3, D2-113): ways over the sea between villages' landings; a plank boat's load in a deal; grain sent by sea to a famine
    /// beyond a day's walk, a boatload a day for each plank boat, in a calm sea. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class VoyageTests
    {
        /// <summary>A world with two villages that keep landings, 6 km to SeaRoutes.Most apart, with a way over the sea between them.</summary>
        static World Pair(out int a, out int b, ulong from = 701)
        {
            for (ulong seed = from; seed < from + 60; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int s1 = w.Settlements.First(); s1 != 0; s1 = w.Settlements.Next(s1))
                {
                    if (!Boats.EnsureLanding(w, s1)) continue;
                    for (int s2 = w.Settlements.Next(s1); s2 != 0; s2 = w.Settlements.Next(s2))
                    {
                        double d = Vec3d.ArcDistance(w.Settlements.Get(s1).Anchor, w.Settlements.Get(s2).Anchor, w.Planet.Radius);
                        if (d <= 6000 || d > SeaRoutes.Most || !Boats.EnsureLanding(w, s2)) continue;
                        if (w.SeaRoutes.Get(w, s1, s2) == null) continue;
                        a = s1; b = s2;
                        return w;
                    }
                }
            }
            a = b = 0;
            return null;
        }

        [Test]
        public void TheSeaHasWaysBetweenLandingsThatKeepToTheWater()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages with landings, beyond a day's walk, the sea between them");
            var P = w.Planet;
            var r = w.SeaRoutes.Get(w, a, b);
            Assert.AreSame(r, w.SeaRoutes.Get(w, b, a), "one way for the pair (cached)");
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            double straight = Vec3d.ArcDistance(sa.LocalToSphere(sa.LandX, sa.LandY, P.Radius), sb.LocalToSphere(sb.LandX, sb.LandY, P.Radius), P.Radius);
            Console.WriteLine("the way by sea " + (r.Length / 1000).ToString("0.0") + " km between landings " + (straight / 1000).ToString("0.0") + " km apart, " + r.Pts.Length + " points");
            Assert.GreaterOrEqual(r.Length, straight - 1.0, "no shorter than the straight line");
            Assert.LessOrEqual(r.Length, SeaRoutes.Most);
            for (int i = 1; i < r.Pts.Length - 1; i++) Assert.IsFalse(P.IsLand(P.Grid.CellOf(r.Pts[i])), "point " + i + " over the sea");
            P.CoastVersion++;   // (the coast moved: ways are found again)
            Assert.AreNotSame(r, w.SeaRoutes.Get(w, a, b));
        }

        [Test]
        public void ABoatsLoadIsTwentyWalkersLoads()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages");
            // premise: one has iron to spare and wants hides, the other the reverse
            ref var sa = ref w.Settlements.Get(a); sa.Iron = 4000 + TC2.Core.Knowledge.Tech.IronPerHead * sa.Population; sa.Hides = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Iron = 0; sb.Hides = 2000 + sb.Population;
            double walk = w.Trade.Plan(a, b, out byte g1, out double q1, out _);
            double boat = w.Trade.Plan(a, b, out byte g2, out double q2, out _, Trade.BoatLoadKg);
            Console.WriteLine("a walker's load: " + q1.ToString("0") + " of good " + g1 + ", gain " + walk.ToString("0") + "; a boat's: " + q2.ToString("0") + " of good " + g2 + ", gain " + boat.ToString("0"));
            Assert.Greater(walk, 0.0); Assert.AreEqual(g1, g2);
            Assert.AreEqual(Trade.BoatLoadKg / Trade.LoadKg, q2 / q1, 0.01, "twenty walkers' loads where both spare enough");
            Assert.Greater(boat, Trade.WorthAVoyage);
        }

        [Test]
        public void GrainGoesBySeaToAFamineBeyondADaysWalkInACalmSea()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages");
            var P = w.Planet;
            P.Storms.Clear();   // (premise: a calm sea)
            // premise: a has grain to spare; b is in famine with an empty store
            ref var sa = ref w.Settlements.Get(a); sa.Granary = 100000;
            ref var sb = ref w.Settlements.Get(b); sb.Granary = 0; sb.FamineAt = w.Now; sb.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = sb.Cell, Settlement = b, Subject = EntityRef.Settlement(b), RootKind = RootKind.Nature });
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (o != a) { ref var so = ref w.Settlements.Get(o); if (o != b) so.FamineEvent = 0; }
            // without a plank boat, a famine beyond a day's walk gets nothing from a
            double g0 = w.Settlements.Get(b).Granary;
            w.Trade.Day(a);
            Assert.AreEqual(g0, w.Settlements.Get(b).Granary, 1e-6, "no boat: no relief beyond a day's walk");
            // a plank boat of a's
            var hl = w.HouseholdsOf(a);
            int hid = hl[0];
            ref var sa2 = ref w.Settlements.Get(a);
            sa2.TechKnown |= (byte)Boats.Bit; sa2.Woodstore += Boats.PlankWood;
            int bid = Boats.Begin(w, a, hid, Structures.PlankBoat);
            Boats.Work(w, a, hid, 2000.0, 1.0, 0);   // (a learner's pace: no builder named)
            Assume.That(Boats.Of(w, hid) == bid, "a's plank boat made");
            long mark = w.History.LastId; double sea0 = w.Metrics.ReliefBySeaFu;
            w.Trade.Day(a);
            double sent = w.Settlements.Get(b).Granary - g0;
            Console.WriteLine("by sea: " + sent.ToString("0") + " FU (a boatload is " + (Trade.BoatLoadKg / 0.75).ToString("0") + ")");
            Assert.Greater(sent, 0.0, "a boatload of grain along the coast");
            Assert.LessOrEqual(sent, Trade.BoatLoadKg / 0.75 + 1e-6, "no more than its boat carries");
            Assert.AreEqual(sent, w.Metrics.ReliefBySeaFu - sea0, 1e-6);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.ReliefSent) { told = true; Assert.AreEqual(1, w.History.At(i).ArgA, "told as by sea"); Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // a storm over the way: they wait for a calm sea
            var r = w.SeaRoutes.Get(w, a, b);
            P.Storms.Add(new Storm { Id = w.Ids.Next(EntityKind.Process), At = r.PointAt(0.5 * r.Length), Heading = Vec3d.UnitX, Speed = 0f, Wind = 30f, Radius = 3000f, Peak = 30f, Born = w.Now, Kind = 1 });
            double g1 = w.Settlements.Get(b).Granary;
            w.Trade.Day(a);
            Assert.AreEqual(g1, w.Settlements.Get(b).Granary, 1e-6, "no boat puts out in a storm");
        }
    }
}
