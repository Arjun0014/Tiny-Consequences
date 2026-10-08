using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Health;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Trade by want and relief (D2-067; F5's done-when with D2-066): what one village spares and another lacks is carried
    /// between them and both gain; grain goes to a neighbour in famine; an epidemic jumps along a trade route and is traceable;
    /// the COARSE day trades at the lived rate. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class TradeTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Two villages of seed 42's world a short walk apart (with a road).</summary>
        static World Pair(out int a, out int b, ulong seed = 42)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = seed });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        /// <summary>Premise: A has metal to spare and no fibre; B fibre to spare and no metal.</summary>
        static void Stocks(World w, int a, int b)
        {
            ref var sa = ref w.Settlements.Get(a); sa.Metal = sa.Population * 2.0; sa.Fibre = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Metal = 0; sb.Fibre = sb.Population * 6.0;
        }

        [Test]
        public void ADealFollowsWantAndBothSidesGain()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Stocks(w, a, b);
            double gain = w.Trade.Plan(a, b, out byte give, out double qty, out byte take);
            Console.WriteLine("A to B: give " + give + " x " + qty.ToString("0.0") + ", take " + take + ", gain " + gain.ToString("0.0"));
            Assert.AreEqual(Trade.Metal, give, "A takes out the metal it spares");
            Assert.AreEqual(Trade.Fibre, take, "and brings back the fibre it lacks");
            Assert.GreaterOrEqual(gain, Trade.WorthAWalk);
            Assert.Greater(w.Trade.Value(b, Trade.Metal), w.Trade.Value(a, Trade.Metal), "metal is worth more where it is lacking");
            Assert.AreEqual(Trade.Fibre, w.Trade.Plan(b, a, out byte give2, out _, out byte take2) > 0 ? give2 : Trade.None, "and the other way round");
            Assert.AreEqual(Trade.Metal, take2);
        }

        [Test]
        public void ATraderCarriesTheLoadAndBringsBackWhatTheVillageWants()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            Stocks(w, a, b);
            int trader = 0;
            var res = w.Residents(a);
            for (int i = 0; i < res.Count && trader == 0; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.At == a && p.AgeYears(w.Now) >= 18 && p.AgeYears(w.Now) < 50) trader = res[i]; }
            double metalA = w.Settlements.Get(a).Metal, metalB = w.Settlements.Get(b).Metal, fibreA = w.Settlements.Get(a).Fibre;
            Assert.IsTrue(w.Agents.SendTrader(trader, b), "a trader sets out with a load");
            var t = w.People.Get(trader);
            Console.WriteLine(HistoryText.Doing(w, in t));
            Assert.AreEqual(Trade.Metal, t.Cargo);
            w.AdvanceTo(w.Now + SimTime.Hours(20));
            var t2 = w.People.Get(trader);
            Console.WriteLine("after: " + HistoryText.Doing(w, in t2) + "; metal A " + metalA.ToString("0.0") + " -> " + w.Settlements.Get(a).Metal.ToString("0.0") +
                              ", B " + metalB.ToString("0.0") + " -> " + w.Settlements.Get(b).Metal.ToString("0.0") + "; fibre A " + fibreA.ToString("0.0") + " -> " + w.Settlements.Get(a).Fibre.ToString("0.0"));
            Assert.Greater(w.Settlements.Get(b).Metal, metalB, "the metal reached the village that lacked it");
            Assert.Greater(w.Settlements.Get(a).Fibre, fibreA, "and the fibre came home");
            Assert.AreEqual(1, w.Metrics.TradeTrips);
            Assert.IsNull(TestWorlds.Audit(w));
        }

        static double WorldMetal(World w)
        {
            double m = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) m += w.Settlements.Get(s).Metal;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.Cargo == Trade.Metal) m += p.CargoQty; }
            return m;
        }

        [Test]
        public void ALoadOnTheRoadIsNotLostWhenTheWorldGoesCoarse()
        {
            // (a world without ore: no smith adds metal; only its slow wear takes any)
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            Stocks(w, a, b);
            int trader = 0;
            var res = w.Residents(a);
            for (int i = 0; i < res.Count && trader == 0; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.At == a && p.AgeYears(w.Now) >= 18 && p.AgeYears(w.Now) < 50) trader = res[i]; }
            Assert.IsTrue(w.Agents.SendTrader(trader, b));
            w.AdvanceTo(w.Now + SimTime.Minutes(20));
            Assume.That(w.People.Get(trader).Act == ActivityKind.TripOut, "on the road with the load");
            double load = w.People.Get(trader).CargoQty, before = WorldMetal(w);
            // premise: the world goes COARSE now (the player speeds time up): everyone is home at once
            w.RequestResolution(Resolution.Coarse, w.Now + 1);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            Console.WriteLine("load " + load.ToString("0.0") + " kg; metal in the world " + before.ToString("0.0") + " -> " + WorldMetal(w).ToString("0.0"));
            Assert.AreEqual(0f, w.People.Get(trader).CargoQty, "the trader is home and the load in the store");
            Assert.AreEqual(before, WorldMetal(w), 0.5, "nothing vanished with the change of resolution");
        }

        [Test]
        public void ReliefReachesAFamineFromANeighbourWithGrainToSpare()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            // premise: B is starving this season, A has grain to spare
            ref var sb = ref w.Settlements.Get(b);
            sb.Granary = 0;
            sb.FamineAt = w.Now;
            sb.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = sb.Cell, Settlement = b, Subject = EntityRef.Settlement(b) });
            w.Settlements.Get(a).Granary = 50000;
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(4));
            int told = 0;
            var givers = new System.Collections.Generic.HashSet<int>();
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.ReliefSent || e.Settlement != b) continue;
                told++; Console.WriteLine(HistoryText.Line(w, e));
                Assert.IsTrue(givers.Add(e.Subject.Id), "each giver told once for the famine, not every day of it (D2-113: each gift is owed back on its own)");
            }
            Console.WriteLine("relief " + w.Metrics.ReliefFu.ToString("0") + " FU");
            Assert.Greater(w.Metrics.ReliefFu, 0.0, "grain went to the famine");
            Assert.Greater(told, 0, "told");
        }

        /// <summary>
        /// Relief is owed back (D2-113, social storage): a giver gives the less the more of its gifts stand unreturned (famine after famine of
        /// the same village, less each time); the village gives back what it was given when it has grain to spare (told, caused by the gift),
        /// and its giver's hands open again.
        /// </summary>
        [Test]
        public void ReliefIsOwedBackAndGivenBackWhenTheHarvestAllows()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) w.Settlements.Get(o).FamineEvent = 0;
            w.Settlements.Get(a).Granary = 200000;
            double need = w.Settlements.Get(b).Population * w.Config.FoodPerAdultDay * 0.8;
            // famine after famine at b: twelve days each, its store eaten as it comes
            double Famine()
            {
                ref var sb = ref w.Settlements.Get(b);
                sb.FamineAt = w.Now;
                sb.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = sb.Cell, Settlement = b, Subject = EntityRef.Settlement(b), RootKind = RootKind.Nature });
                double got = 0;
                for (int d = 0; d < 12; d++)
                {
                    w.Settlements.Get(b).Granary = 0;
                    w.Trade.Day(a);
                    got += w.Settlements.Get(b).Granary;
                }
                w.Settlements.Get(b).Granary = 0;
                return got;
            }
            long mark = w.History.LastId;
            double g1 = Famine(), g2 = Famine(), g3 = Famine();
            double owed = w.DeedBook.Owed(b, a);
            Console.WriteLine("given in three famines: " + (g1 / need).ToString("0.0") + ", " + (g2 / need).ToString("0.0") + ", " + (g3 / need).ToString("0.0") + " days of their need; owed " + (owed / need).ToString("0.0") + " days");
            Assert.Greater(g1, 0.0, "the first famine is relieved");
            Assert.Less(g2, g1, "less the second time: the first gift stands unreturned");
            Assert.Less(g3, g2);
            Assert.LessOrEqual(owed, Trade.CreditDays * need + 3.0 * need, "no more than two months of their need (and a day's carrying) stands open");
            long sent = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.ReliefSent && e.Settlement == b) sent = e.Id; }
            Assert.AreNotEqual(0L, sent, "the gift is told");
            // b's harvest is good: it gives back, the oldest gift first, and a's hands open again
            double a0 = w.Settlements.Get(a).Granary, back = 0;
            w.Settlements.Get(b).Granary = 100000;
            for (int d = 0; d < 40 && w.DeedBook.Owed(b, a) >= 1.0; d++) { double before = w.Settlements.Get(b).Granary; w.Trade.Day(b); back += before - w.Settlements.Get(b).Granary; }
            Console.WriteLine("given back " + (back / need).ToString("0.0") + " days of their need; a's store +" + (w.Settlements.Get(a).Granary - a0).ToString("0") + "; owed now " + w.DeedBook.Owed(b, a).ToString("0.0"));
            Assert.AreEqual(owed, back, 1.0, "what was owed came back");
            Assert.AreEqual(back, w.Settlements.Get(a).Granary - a0, 1e-6, "into the giver's store");
            Assert.AreEqual(back, w.Metrics.ReliefReturnedFu, 1e-6);
            int told = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.ReliefReturned) continue;
                told++; Console.WriteLine(HistoryText.Line(w, e));
                Assert.AreEqual(sent, e.Cause1, "caused by the gift");
                Assert.AreEqual(b, e.Subject.Id); Assert.AreEqual(a, e.Object.Id);
            }
            Assert.AreEqual(1, told, "told once, at the first grain back");
            w.Settlements.Get(b).Granary = 0;
            double g4 = Famine();
            Console.WriteLine("the next famine: " + (g4 / need).ToString("0.0") + " days of their need");
            Assert.Greater(g4, g3, "given back, they are given again");
        }

        [Test]
        public void AnEpidemicJumpsAlongATradeRouteAndIsTraceable()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            // premise: B, the trading partner, is down with a contagious sickness
            int path = w.Disease.Outbreak(b, beta: 4.0, latent: 4, infectious: 10, virulence: 0.0, immunity: Disease.ForLife);
            var resB = w.Residents(b);
            for (int i = 0; i < resB.Count; i++) { ref var q = ref w.People.Get(resB[i]); if (q.Alive && q.Sick == 0) w.Disease.Infect(ref q, path, w.Settlements.Get(b).Epidemic); }
            w.AdvanceTo(SimTime.StartOfDay(w.Now + SimTime.Days(5)) + SimTime.Day + SimTime.Hours(1));
            long epiB = w.Settlements.Get(b).Epidemic;
            // and A's traders go to B with what A spares
            Stocks(w, a, b);
            var resA = w.Residents(a);
            int sent = 0;
            for (int i = 0; i < resA.Count && sent < 5; i++) { var p = w.People.Get(resA[i]); if (p.Alive && p.At == a && p.Sick == 0 && p.AgeYears(w.Now) >= 18 && p.AgeYears(w.Now) < 55 && w.Agents.SendTrader(resA[i], b)) sent++; }
            Assume.That(sent > 0, "traders set out");
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(14));
            long arrived = 0; int carrier = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.EpidemicArrived && e.Settlement == a) { arrived = e.Id; carrier = e.Other.Id; break; }
            }
            Assert.AreNotEqual(0L, arrived, "a trader carried it home");
            Assert.AreEqual(a, w.People.Get(carrier).Home);
            long c = arrived; bool leads = false;
            for (int i = 0; i < 64 && c != 0; i++) { if (c == epiB) { leads = true; break; } if (!w.History.TryGet(c, out var ev)) break; c = ev.Cause1; }
            Assert.IsTrue(leads, "its cause is B's epidemic: traceable along the trade");
            Console.WriteLine(HistoryText.Why(w, arrived));
        }

        [Test]
        public void TheCoarseTradeRateIsTheLivedOne()
        {
            // pooled over four worlds (C1.2: one world's ~3 trips moved with any change of its trajectory)
            double adultDays = 0;
            var seen = new System.Collections.Generic.HashSet<(int, long)>();
            int worlds = 0;
            foreach (ulong seed in new ulong[] { 42, 43, 44, 45 })
            {
            var w = Pair(out int a, out int b, seed);
            if (b == 0) continue;
            worlds++;
            w.AdvanceTo(w.Now + SimTime.Day);
            for (int d = 0; d < 30; d++)
            {
                Stocks(w, a, b);   // (the deal kept to be had)
                var res = w.Residents(a);
                for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); double age = p.AgeYears(w.Now); if (p.Alive && p.Sick == 0 && age >= 18 && age < 56 && p.Health > 0.7) adultDays++; }
                long end = w.Now + SimTime.Day;
                while (w.Now < end)
                {
                    w.AdvanceTo(w.Now + 600);
                    for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.Act == ActivityKind.TripOut && p.Why == WhyCode.Trade) seen.Add((res[i], p.T0)); }
                }
            }
            }
            Assume.That(worlds > 0, "two villages a walk apart");
            // A's own trading trips (a trader seen setting out), per healthy grown person of A a day
            double rate = seen.Count / adultDays;
            Console.WriteLine("lived trades per healthy grown person a day with a deal to be had " + rate.ToString("0.0000") + " (" + seen.Count + " trips in " + adultDays + " grown person-days, " + worlds + " worlds; COARSE uses " + Trade.TripsPerAdultDay + ")");
            Assert.That(rate / Trade.TripsPerAdultDay, Is.InRange(0.4, 2.5), "refit AgentSystem.TradeChance or Trade.TripsPerAdultDay");
        }
    }
}
