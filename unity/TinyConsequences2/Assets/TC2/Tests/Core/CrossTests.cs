using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>A boat to cross (C9.5b): a dugout carries a load across a short water to a village within a day's walk; a village with no boat whose
    /// neighbour across the water is worth reaching makes one for that - history tells why.</summary>
    [TestFixture]
    public class CrossTests
    {
        /// <summary>A dugout made at once for a household of the village with two who can paddle (premise).</summary>
        static int Dugout(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                if (w.Households.Get(hl[i]).Boat != 0 || !w.Trade.Crewed(hl[i], sid)) continue;
                w.Settlements.Get(sid).Woodstore += Boats.Wood;
                Boats.Begin(w, sid, hl[i], Structures.Boat);
                Boats.Work(w, sid, hl[i], 2000.0, 1.0, 0);
                if (Boats.Of(w, hl[i]) != 0) return Boats.Of(w, hl[i]);
            }
            return 0;
        }

        /// <summary>A boat of the village's households being made (0 none).</summary>
        static int Making(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { int m = Boats.Making(w, hl[i]); if (m != 0) return m; }
            return 0;
        }

        static bool NoBoats(World w, int sid)
        {
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) if (Boats.IsBoat(w.Buildings.Get(bl[i]).Kind)) return false;
            return true;
        }

        /// <summary>Premise on `AcrossTests.Across`: a deal across the water - a's store full of grain and no hides, b's holding five days of its need
        /// (it wants grain, and does not starve) with hides to spare.</summary>
        static void Deal(World w, int a, int b)
        {
            ref var sa = ref w.Settlements.Get(a); sa.Granary = 100000; sa.Hides = 0;
            ref var sb = ref w.Settlements.Get(b);
            Trade.DaysOfFood(w, in sb, out double daily);
            sb.Granary = 5.0 * daily; sb.Sheaves = 0; sb.Hides = 5000;
        }

        [Test]
        public void ADugoutCrossesOnlyAShortWater()
        {
            Assert.IsTrue(Boats.Carries(Structures.Boat, 5000), "a dugout across a short water");
            Assert.IsFalse(Boats.Carries(Structures.Boat, 7000), "beyond a day's walk a dugout carries nothing");
            Assert.IsTrue(Boats.Carries(Structures.PlankBoat, 15000), "a plank boat as far as the sea's ways go");
            Boats.CrossOff = true;
            try { Assert.IsFalse(Boats.Carries(Structures.Boat, 5000), "--oldcross: no load in a dugout"); }
            finally { Boats.CrossOff = false; }
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            AcrossTests.Coarse(w);
            Assume.That(Dugout(w, a) != 0 && w.Conflict.PlankBoats(a) == 0, "a dugout of a's, no plank boat");
            Assert.AreEqual(Boats.DugoutLoadKg, w.Trade.FreeLoadKg(a, 5000), 1e-9, "its load across a short water");
            Assert.AreEqual(0.0, w.Trade.FreeLoadKg(a, 7000), 1e-9, "none beyond a day's walk");
            Assert.AreEqual(1, w.Conflict.CrossBoats(a, 5000), "a boat that can carry the tenth across a short water");
        }

        [Test]
        public void ADugoutCarriesReliefAcrossAShortWater()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            AcrossTests.Coarse(w);
            w.Planet.Storms.Clear();   // (premise: a calm sea)
            AcrossTests.Famine(w, b);
            w.Settlements.Get(a).Granary = 100000;
            Assume.That(Dugout(w, a) != 0 && w.Conflict.PlankBoats(a) == 0, "a dugout of a's, no plank boat");
            double g0 = w.Settlements.Get(b).Granary;
            Boats.CrossOff = true;
            try { w.Trade.Day(a); }
            finally { Boats.CrossOff = false; }
            Assert.AreEqual(g0, w.Settlements.Get(b).Granary, 1e-6, "--oldcross: no grain in a dugout");
            long mark = w.History.LastId;
            w.Trade.Day(a);
            double sent = w.Settlements.Get(b).Granary - g0;
            Console.WriteLine("relief in a dugout across the water: " + sent.ToString("0") + " FU");
            Assert.Greater(sent, 0.0, "a dugout's load across the water");
            Assert.LessOrEqual(sent, Boats.DugoutLoadKg / Trade.KgOf(Trade.Grain) + 1e-6, "no more than a dugout carries");
            bool bySea = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.ReliefSent && w.History.At(i).ArgA == 1) bySea = true;
            Assert.IsTrue(bySea, "told as by sea");
        }

        [Test]
        public void AVillageWithNoBoatMakesOneToReachItsNeighbourAcrossTheWater()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            AcrossTests.Coarse(w);
            Deal(w, a, b);
            ref var sa = ref w.Settlements.Get(a);
            sa.Fish.Expect = 0f;   // (premise: its shore promises nothing a fisher's boat would add - no boat for the fish)
            sa.Woodstore += 100;
            Assume.That(NoBoats(w, a), "no boat of a's");
            Assume.That(Woods.Lot(in sa) >= 0 && w.Planet.Forest[Woods.Lot(in sa)] >= Boats.BigTrees, "woods with trunks fit to hollow");
            Assume.That(Boats.Across(w, a, out byte why, out double worth) == b && why == Boats.ForTrade, "b across the water, worth reaching");
            Console.WriteLine("the deal across the water for a dugout's load: " + worth.ToString("0.0") + " (worth a crossing " + Trade.WorthACrossing + ")");
            Boats.CrossOff = true;
            try { Boats.Plan(w, a, 1); }
            finally { Boats.CrossOff = false; }
            Assert.AreEqual(0, Making(w, a), "--oldcross: no boat to cross");
            long mark = w.History.LastId;
            Boats.Plan(w, a, 1);
            int mk = Making(w, a);
            Assert.AreNotEqual(0, mk, "a boat laid down to cross");
            var made = w.Buildings.Get(mk);
            Assert.AreEqual(Structures.Boat, made.Kind, "a dugout (the village builds no boats of planks)");
            Assert.IsTrue(w.History.TryGet(made.Cause, out var laid) && laid.Kind == HistoryKind.BoatToCross && laid.Object.Id == b, "caused by the crossing it is made for");
            string told = HistoryText.Describe(w, in laid);
            Console.WriteLine(told);
            StringAssert.Contains("across the water", told);
            // its household's people hollow it, told so
            int hid = made.Household, worker = 0;
            var mem = w.Members(hid);
            for (int i = 0; i < mem.Count && worker == 0; i++) { var q = w.People.Get(mem[i]); if (q.Alive && q.AgeYears(w.Now) >= 16) worker = mem[i]; }
            Assume.That(worker != 0);
            { ref var q = ref w.People.Get(worker); q.Why = WhyCode.Boat; q.Act = ActivityKind.Build; }
            string doing = HistoryText.Doing(w, in w.People.Get(worker));
            Console.WriteLine("the worker: " + doing);
            StringAssert.Contains("no way on foot leads to " + w.History.NameOf(EntityRef.Settlement(b)) + " across the water", doing);
            // made: history tells it as made to cross, caused by its laying down
            Boats.Work(w, a, hid, 2000.0, 1.0, worker);
            Assert.AreNotEqual(0, Boats.Of(w, hid), "made");
            bool toldMade = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.BoatBuilt) continue;
                Assert.AreEqual(made.Cause, e.Cause1, "the crossing the cause");
                Console.WriteLine(HistoryText.Describe(w, in e));
                StringAssert.Contains("cross the water to", HistoryText.Describe(w, in e));
                toldMade = true;
            }
            Assert.IsTrue(toldMade, "its making told");
            // a village with a boat lays down no other to cross
            Boats.Plan(w, a, 1);
            Assert.AreEqual(0, Making(w, a), "one boat is enough to cross");
        }

        [Test]
        public void ADugoutTradesAcrossTheWater()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            AcrossTests.Coarse(w);
            Deal(w, a, b);
            Assume.That(Dugout(w, a) != 0 && w.Conflict.PlankBoats(a) == 0, "a dugout of a's, no plank boat");
            Assume.That(w.Trade.BestSeaPartner(a, out double gain, out _, Boats.DugoutLoadKg, true) == b, "the deal across the water worth a crossing");
            Boats.CrossTally = new double[Boats.CrossTallyN];
            try
            {
                long t0 = w.Now;
                while (Boats.CrossTally[4] < 1 && w.Now - t0 < SimTime.Days(240)) { w.Planet.Storms.Clear(); w.AdvanceTo(w.Now + SimTime.Day); Deal(w, a, b); }
                Console.WriteLine("trade crossings in a dugout: " + Boats.CrossTally[4] + " in " + ((w.Now - t0) / (double)SimTime.Day).ToString("0") + " days, " + Boats.CrossTally[5].ToString("0") + " kg (gain " + gain.ToString("0.0") + ")");
                Assert.GreaterOrEqual(Boats.CrossTally[4], 1.0, "a dugout's crossing to trade");
                Assert.LessOrEqual(Boats.CrossTally[5], Boats.CrossTally[4] * 2.0 * Boats.DugoutLoadKg + 1e-6, "a dugout's load each way");
            }
            finally { Boats.CrossTally = null; }
        }

        [Test]
        public void ALivedCrewPaddlesReliefAcrossInADugout()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(Dugout(w, a) != 0 && w.Conflict.PlankBoats(a) == 0, "a dugout of a's, no plank boat");
            w.AdvanceTo(AcrossTests.ToLocal(w, a, 1.0));
            w.Planet.Storms.Clear();
            Boats.CrossTally = new double[Boats.CrossTallyN];
            try
            {
                AcrossTests.Famine(w, b);
                w.Settlements.Get(a).Granary = 100000;
                double g0 = w.Settlements.Get(b).Granary;
                long end = w.Now + SimTime.Days(4);
                string paddling = null;
                while (w.Now < end && w.Settlements.Get(b).Granary - g0 < 1.0)
                {
                    w.Planet.Storms.Clear();
                    w.AdvanceTo(w.Now + SimTime.Minutes(30));
                    var res = w.Residents(a);
                    for (int i = 0; i < res.Count && paddling == null; i++)
                    {
                        var q = w.People.Get(res[i]);
                        if (q.Alive && q.Voyage != 0 && q.Step == StepKind.Travel && q.CargoTo == Trade.AsGift) paddling = HistoryText.Doing(w, in q);
                    }
                }
                double got = w.Settlements.Get(b).Granary - g0;
                Console.WriteLine("lived: " + got.ToString("0") + " FU across the water in a dugout; crossings " + Boats.CrossTally[3] + "; " + paddling);
                Assert.Greater(Boats.CrossTally[3], 0.0, "a lived crossing in a dugout");
                Assert.Greater(got, 0.0, "the grain landed");
                Assert.LessOrEqual(got, Boats.DugoutLoadKg / Trade.KgOf(Trade.Grain) + 1e-3, "a dugout's load");
            }
            finally { Boats.CrossTally = null; }
        }

        [Test]
        public void ASaveTakenWhileADugoutIsCrossingContinuesIdentically()
        {
            var w = AcrossTests.Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(Dugout(w, a) != 0, "a dugout of a's");
            w.AdvanceTo(AcrossTests.ToLocal(w, a, 1.0));
            AcrossTests.Famine(w, b);
            w.Settlements.Get(a).Granary = 100000;
            bool out1 = false;
            long end = w.Now + SimTime.Days(3);
            while (w.Now < end && !out1)
            {
                w.Planet.Storms.Clear();
                w.AdvanceTo(w.Now + SimTime.Minutes(20));
                var res = w.Residents(a);
                for (int i = 0; i < res.Count && !out1; i++) { var q = w.People.Get(res[i]); out1 = q.Alive && q.Voyage != 0 && q.Step == StepKind.Travel; }
            }
            Assume.That(out1, "a dugout out on the water");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Days(2);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }
    }
}
