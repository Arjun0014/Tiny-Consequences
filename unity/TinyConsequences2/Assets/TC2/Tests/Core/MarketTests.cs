using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Markets and rings on dry ground (C1.6, D2-095): a village traders come to often lays out a market; a market draws traders from
    /// farther and they deal better there; the count of traders halves each year; a palisade's posts stand on dry ground only.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class MarketTests
    {
        [Test]
        public void AVillageTradersComeToOftenLaysOutAMarket()
        {
            var w = TestWorlds.Make(901);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.Population = Math.Max(s.Population, 120); s.TechKnown = 0; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0;
            s.TradeIn = (float)(Trade.MarketFrom - 1);
            Structures.Plan(w, sid);
            Assert.AreNotEqual(Structures.Market, w.Settlements.Get(sid).Project != 0 ? w.Buildings.Get(w.Settlements.Get(sid).Project).Kind : (byte)0, "too few traders");
            ref var s2 = ref w.Settlements.Get(sid); s2.Project = 0; s2.TradeIn = (float)(Trade.MarketFrom + 2);
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid);
            Assert.AreEqual(Structures.Market, w.Buildings.Get(bid).Kind, "a market");
            Assert.AreEqual(Trade.MarketFrom + 2, w.Buildings.Get(bid).Stock, 1e-4, "it remembers the traders that made it");
            // the count halves in a year
            ref var s3 = ref w.Settlements.Get(sid); s3.TradeIn = 16f;
            double kept = SettlementSystem.Keep(0.693147 / SimTime.DaysPerYear, SimTime.DaysPerYear);
            Assert.AreEqual(0.5, kept, 0.01, "halves in a year");
        }

        [Test]
        public void AMarketDrawsTradersFromFartherAndTheyDealBetterThere()
        {
            // two worlds alike; in one the far village has a market: the trader of the first goes there when the deal is to be had
            foreach (ulong seed in new ulong[] { 42, 43, 44, 45, 46 })
            {
                var w = WorldGenerator(seed);
                int a = w.Settlements.First(), far = 0; double fd = 0;
                for (int o = w.Settlements.Next(a); o != 0; o = w.Settlements.Next(o))
                {
                    double d = Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius);
                    if (d > 6000 && d <= Trade.MarketReach && d > fd) { fd = d; far = o; }
                }
                if (far == 0) continue;
                // premise: a has metal to spare and no fibre; far has fibre to spare and no metal; no other village deals
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) { ref var so = ref w.Settlements.Get(o); so.Metal = so.Population * 0.5; so.Fibre = so.Population * 2.0; }
                ref var sa = ref w.Settlements.Get(a); sa.Metal = sa.Population * 3.0; sa.Fibre = 0;
                ref var sf = ref w.Settlements.Get(far); sf.Metal = 0; sf.Fibre = sf.Population * 6.0;
                int before = w.Trade.BestPartner(a, out double g0);
                Assert.AreNotEqual(far, before, "beyond a day's walk: not without a market");
                Structures.Place(w, far, Structures.Market, out float mx, out float my);
                Structures.Standing(w, far, Structures.Market, mx, my);
                int after = w.Trade.BestPartner(a, out double g1);
                Console.WriteLine("seed " + seed + ": " + (fd / 1000).ToString("0.0") + " km to a market village: partner " + before + " -> " + after + ", gain " + g0.ToString("0.0") + " -> " + g1.ToString("0.0"));
                Assert.AreEqual(far, after, "a market draws them a long day's walk");
                return;
            }
            Assert.Inconclusive("no pair of villages 6-9 km apart");
        }

        static World WorldGenerator(ulong seed) => TC2.Core.Worldgen.WorldGenerator.Create(new WorldConfig { Seed = seed });

        [Test]
        public void APalisadeStandsOnDryGroundOnly()
        {
            // a coastal village: the ring round its homes crosses the shore; its posts and their cost are only the dry stretches
            foreach (ulong seed in new ulong[] { 42, 7, 43, 44, 45, 46, 47 })
            {
                var w = WorldGenerator(seed);
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    double r = w.Conflict.WallRadius(sid) + 120;   // (a ring wide enough to reach the shore of a coastal village)
                    double dry = Structures.DryShare(w, in s, r);
                    if (dry >= 0.95 || dry <= 0.2) continue;
                    s.Wall = 1; s.WallR = (float)r; s.WallDry = (float)dry; s.WorkBuild = 0;
                    Construction.WorkCost(Construction.Palisade, in s, out double hours, out double wood);
                    Assert.AreEqual(Construction.PalisadeHoursPerM * 2 * Math.PI * r * dry, hours, 1e-3 * hours, "the dry stretches only (radius and share kept as floats)");
                    Console.WriteLine("seed " + seed + " village " + sid + ": ring " + r.ToString("0") + " m, " + (dry * 100).ToString("0") + "% on dry ground, " + hours.ToString("0") + " h");
                    return;
                }
            }
            Assert.Inconclusive("no coastal village found");
        }
    }
}
