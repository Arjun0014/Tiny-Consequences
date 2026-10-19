using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>Bread and dependence (C10.4, D2-157): a village that cannot give back the famine grain a stronger neighbour gave it bows to that
    /// neighbour; the tribute gives the grain back; a lord feeds its own.</summary>
    [TestFixture]
    public class BreadTests
    {
        /// <summary>Premise: two inhabited villages within a day's walk - the giver and the receiver, the giver the stronger by `TributeOdds` in its
        /// people of fighting age (or, `weaker`, not). (Before C10.5 the receiver's people were laid up with wounds: a day's wounds no longer count.)</summary>
        static World Pair(out int giver, out int receiver, bool weaker = false, ulong from = 701)
        {
            for (ulong seed = from; seed < from + 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (int b = w.Settlements.First(); b != 0; b = w.Settlements.Next(b))
                    {
                        if (a == b || !w.Settlements.Get(a).Inhabited || !w.Settlements.Get(b).Inhabited) continue;
                        if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) > Conflict.BreadReach) continue;
                        if (w.Routes.Get(w, a, b) == null) continue;   // (a way on foot between them: the tenth is carried along it, C10.6)
                        bool stronger = w.Conflict.Might(a, b) >= Conflict.TributeOdds * w.Conflict.Might(b, a);
                        if (stronger == weaker) continue;
                        giver = a; receiver = b;
                        return w;
                    }
            }
            giver = receiver = 0;
            return null;
        }

        /// <summary>The giver's relief to the receiver in a famine: `days` of the receiver's people's food, known as relief deeds are (`Trade.Given`).</summary>
        static int Gift(World w, int giver, int receiver, double days, out long told)
        {
            ref var r = ref w.Settlements.Get(receiver);
            Trade.DaysOfFood(w, in r, out double daily);
            double fu = days * daily;
            told = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.ReliefSent, Time = w.Now, Cell = r.Cell, Settlement = receiver, Subject = EntityRef.Settlement(giver),
                Object = EntityRef.Settlement(receiver), Magnitude = (float)fu, RootKind = RootKind.People,
            });
            int deed = w.DeedBook.Record(DeedKind.Relief, 0, giver, 0, receiver, Trade.Grain, fu, fu, told);
            w.DeedBook.Learn(receiver, deed, KnownHow.Saw);
            w.DeedBook.Learn(giver, deed, KnownHow.Own);
            return deed;
        }

        /// <summary>The receiver's store at `days` of its food after the harvest (nothing to spare below 60).</summary>
        static void Store(World w, int sid, double days)
        {
            ref var s = ref w.Settlements.Get(sid);
            Trade.DaysOfFood(w, in s, out double daily);
            s.Granary = days * daily;
            s.Sheaves = 0;
        }

        [Test]
        public void AVillageThatCannotGiveBackItsFamineGrainBowsToItsStrongerGiver()
        {
            var w = Pair(out int g, out int r);
            Assume.That(w != null, "two villages within a day's walk");
            Gift(w, g, r, 45.0, out long told);
            Store(w, r, 40.0);   // (no grain to spare: the new harvest leaves it short of two months)
            double owed0 = w.DeedBook.Owed(r, g);
            w.Conflict.Harvested(r, w.Settlements.Get(r).Granary);
            ref var s = ref w.Settlements.Get(r);
            Assert.AreEqual(g, s.Lord, "it bows to the village that fed it");
            Assert.IsTrue(w.History.TryGet(s.LordEvent, out var e));
            Assert.AreEqual(HistoryKind.TributeAgreed, e.Kind);
            Assert.AreEqual(1, e.ArgB, "for bread");
            Assert.AreEqual(told, e.Cause1, "its cause the gift");
            Console.WriteLine(HistoryText.Describe(w, in e));
            Assert.Greater(w.Settlements.Get(r).TributeDue, 0.0, "its tenth set aside for its lord (C10.6)");
            CarriedIn(w, r);   // (its people carry it to the lord's store: C10.6)
            double owed1 = w.DeedBook.Owed(r, g);
            Console.WriteLine("owed " + owed0.ToString("0") + " FU before its first tribute, " + owed1.ToString("0") + " after");
            Assert.Less(owed1, owed0, "the tribute gives the grain back");
            string page = Gazetteer.Neighbours(w, r);   // (the settlement page tells why it pays, and what it owes)
            Console.Write(page);
            StringAssert.Contains("its grain kept it through its famines", page);
            StringAssert.Contains("days' food for the grain it gave it in its famines", page);
        }

        [Test]
        public void AVillageThatCanGiveItBackFromItsHarvestDoesNotBow()
        {
            var w = Pair(out int g, out int r);
            Assume.That(w != null, "two villages within a day's walk");
            Gift(w, g, r, 45.0, out _);
            Store(w, r, 120.0);   // (60 days to spare: all but 15 of the 45 it owes - less than a month left)
            w.Conflict.Harvested(r, w.Settlements.Get(r).Granary);
            Assert.AreEqual(0, w.Settlements.Get(r).Lord, "it will give it back");
        }

        [Test]
        public void ItBowsToNoWeakerGiverNorForLittle()
        {
            var w = Pair(out int g, out int r, weaker: true);
            Assume.That(w != null, "two villages within a day's walk");
            Gift(w, g, r, 45.0, out _);
            Store(w, r, 40.0);
            w.Conflict.Harvested(r, w.Settlements.Get(r).Granary);
            Assert.AreEqual(0, w.Settlements.Get(r).Lord, "not to a giver no stronger than it");
            var w2 = Pair(out int g2, out int r2);
            Gift(w2, g2, r2, 20.0, out _);
            Store(w2, r2, 40.0);
            w2.Conflict.Harvested(r2, w2.Settlements.Get(r2).Granary);
            Assert.AreEqual(0, w2.Settlements.Get(r2).Lord, "not for less than a month of its food");
        }

        [Test]
        public void NoBreadSwitchesItOff()
        {
            var w = Pair(out int g, out int r);
            Assume.That(w != null, "two villages within a day's walk");
            Gift(w, g, r, 45.0, out _);
            Store(w, r, 40.0);
            Conflict.BreadOff = true;
            try { w.Conflict.Harvested(r, w.Settlements.Get(r).Granary); }
            finally { Conflict.BreadOff = false; }
            Assert.AreEqual(0, w.Settlements.Get(r).Lord, "--nobread: lordship by conquest alone");
        }

        [Test]
        public void ALordFeedsItsOwnWhateverItOwes()
        {
            // Premise: the receiver bowed for bread; a famine again, the lord with grain to spare - and owed more than its open hand allows (D2-113:
            // a giver owed `CreditDays` of a famine's need gives no more)
            var w = Pair(out int g, out int r);
            Assume.That(w != null, "two villages within a day's walk");
            Gift(w, g, r, 45.0, out _);
            Store(w, r, 40.0);
            w.Conflict.Harvested(r, w.Settlements.Get(r).Granary);
            Assume.That(w.Settlements.Get(r).Lord == g, "bowed");
            Gift(w, g, r, 2.0 * Trade.CreditDays, out _);   // (owed beyond what a giver's open hand allows)
            Famine(w, r);
            Store(w, g, 400.0);   // (the lord has grain to spare)
            double before = w.Settlements.Get(r).Granary;
            w.Trade.Day(g);
            double fed = w.Settlements.Get(r).Granary - before;
            Console.WriteLine("the lord's relief to its tributary owing it " + w.DeedBook.Owed(r, g).ToString("0") + " FU: " + fed.ToString("0.0") + " FU");
            Assert.Greater(fed, 0.0, "a lord feeds its own");
            // the same without bread: owed that much, nothing given
            var w2 = Pair(out int g2, out int r2);
            Gift(w2, g2, r2, 45.0, out _);
            Store(w2, r2, 40.0);
            w2.Conflict.Harvested(r2, w2.Settlements.Get(r2).Granary);
            Gift(w2, g2, r2, 2.0 * Trade.CreditDays, out _);
            Famine(w2, r2);
            Store(w2, g2, 400.0);
            Conflict.BreadOff = true;
            try
            {
                double b2 = w2.Settlements.Get(r2).Granary;
                w2.Trade.Day(g2);
                Assert.AreEqual(0.0, w2.Settlements.Get(r2).Granary - b2, 1e-9, "--nobread: what it owes closes the giver's hand (D2-113)");
            }
            finally { Conflict.BreadOff = false; }
        }

        /// <summary>Premise (C10.6): the tributary's people have carried its lord's tenth, set aside at the harvest, into the lord's store.</summary>
        internal static void CarriedIn(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            if (s.Lord == 0 || s.TributeDue <= 0) return;
            double due = s.TributeDue;
            s.TributeDue = 0;
            w.Conflict.Brought(sid, s.Lord, due);
        }

        /// <summary>A famine at the village now, its store a few days' food.</summary>
        static void Famine(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), RootKind = RootKind.Nature });
            s.FamineAt = w.Now;
            Trade.DaysOfFood(w, in s, out double daily);
            s.Granary = 3.0 * daily; s.Sheaves = 0;
        }
    }
}
