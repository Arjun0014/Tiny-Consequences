using System;
using System.Collections.Generic;
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
    /// <summary>Tribute carried (C10.6, D2-161): the lord's tenth is set aside at the tributary's harvest and its people carry it to the lord's store,
    /// load by load along the way between them - lived, walkers (and carts) on the road; a COARSE day, the same loads at once.</summary>
    [TestFixture]
    public class TributeCarriedTests
    {
        /// <summary>Premise: two villages a day's walk apart with a way between them, the second (no weaker by its people) the first's lord.</summary>
        static World Bound(out int trib, out int lord, ulong from = 961)
        {
            for (ulong seed = from; seed < from + 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (int b = w.Settlements.First(); b != 0; b = w.Settlements.Next(b))
                    {
                        if (a == b || !w.Settlements.Get(a).Inhabited || !w.Settlements.Get(b).Inhabited) continue;
                        if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) > 6000 || w.Routes.Get(w, a, b) == null) continue;
                        if (w.Conflict.Hands(b) < w.Conflict.Hands(a) || w.Conflict.FitAt(a, w.Now) < 4) continue;
                        ref var s = ref w.Settlements.Get(a);
                        s.Lord = b; s.LordSince = w.Now;
                        trib = a; lord = b;
                        return w;
                    }
            }
            trib = lord = 0;
            return null;
        }

        /// <summary>The tributary's harvest of `fu`: its tenth set aside.</summary>
        static void Harvest(World w, int trib, double fu)
        {
            w.Settlements.Get(trib).Granary += fu;
            w.Conflict.Harvested(trib, fu);
        }

        static long ToLocal(World w, int sid, double hour)
        {
            long lsec = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long d = SimTime.FloorMod(SimTime.Hours(hour) - lsec, SimTime.Day);
            return w.Now + (d == 0 ? SimTime.Day : d);
        }

        [Test]
        public void TheTenthIsSetAsideAtTheHarvestAndCarriedToTheLordsStore()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + 1);
            double lord0 = w.Settlements.Get(b).Granary, way0 = w.Ways.UseOf(w.Now, a, b);
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try { Coarse(w, a, b, lord0, way0); }
            finally { Conflict.CarryTally = null; }
        }

        void Coarse(World w, int a, int b, double lord0, double way0)
        {
            Harvest(w, a, 1000);
            Assert.AreEqual(100.0, w.Settlements.Get(a).TributeDue, 1e-6, "a tenth set aside for the lord");
            Assert.AreEqual(lord0, w.Settlements.Get(b).Granary, 1e-6, "not yet in the lord's store");
            long t0 = w.Now;
            while (w.Settlements.Get(a).TributeDue >= 1.0 && w.Now - t0 < SimTime.Days(30)) w.AdvanceTo(w.Now + SimTime.Day);
            double days = (w.Now - t0) / (double)SimTime.Day;
            var ct = Conflict.CarryTally;
            Console.WriteLine("carried in " + days.ToString("0") + " days: " + ct[0] + " loads, " + ct[1].ToString("0.0") + " FU; the way's wear " +
                              way0.ToString("0.0") + " -> " + w.Ways.UseOf(w.Now, a, b).ToString("0.0"));
            Assert.Less(w.Settlements.Get(a).TributeDue, 1.0, "carried");
            Assert.LessOrEqual(days, Conflict.CarryDays + 2, "within the days it means to");
            Assert.Greater(ct[1], 99.0 - 1e-6, "brought to the lord's store");
            double kept = Math.Exp(-0.693147180559945 * (w.Now - t0) / (Ways.HalfLifeYears * SimTime.Year));   // (the old wear fades as it is walked anew)
            Assert.Greater(w.Ways.UseOf(w.Now, a, b) - way0 * kept, 2.0 * ct[0] - 0.5, "the way between them walked there and back for each load");
        }

        [Test]
        public void LivedCarriersWalkTheLoadsToTheLordAndComeHome()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            w.AdvanceTo(ToLocal(w, a, 1.0));
            Harvest(w, a, 1000);
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            var seen = new HashSet<int>();
            int onTheWay = 0;
            long end = w.Now + SimTime.Days(Conflict.CarryDays + 4);
            while (w.Now < end && (w.Settlements.Get(a).TributeDue >= 1.0 || onTheWay > 0))
            {
                w.AdvanceTo(w.Now + SimTime.Minutes(30));
                onTheWay = 0;
                var res = w.Residents(a);
                for (int i = 0; i < res.Count; i++)
                {
                    var q = w.People.Get(res[i]);
                    if (!q.Alive || q.Why != WhyCode.Tribute) continue;
                    seen.Add(q.Id);
                    if (q.Cargo == Trade.Grain && q.CargoTo == Trade.ToLord) onTheWay++;
                }
            }
            double brought = Conflict.CarryTally[1], lived = Conflict.CarryTally[2];
            Conflict.CarryTally = null;
            Console.WriteLine("lived: " + seen.Count + " carriers, " + lived + " loads brought, " + brought.ToString("0.0") + " FU, still set aside " + w.Settlements.Get(a).TributeDue.ToString("0.0"));
            Assert.Greater(lived, 0.0, "brought by lived carriers");
            Assert.Greater(seen.Count, 0, "its people carried it");
            Assert.Less(w.Settlements.Get(a).TributeDue, 1.0, "all of it");
            Assert.Greater(brought, 99.0 - 1e-3, "into the lord's store");
            w.AdvanceTo(w.Now + SimTime.Day);
            var ra = w.Residents(a);
            for (int i = 0; i < ra.Count; i++) { var q = w.People.Get(ra[i]); Assert.IsFalse(q.Alive && q.CargoTo == Trade.ToLord && q.CargoQty > 0f, "nobody left holding a load"); }
        }

        [Test]
        public void ALoadSentHomeBeforeTheLordIsSetAsideAgainAndCarried()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            w.AdvanceTo(ToLocal(w, a, 1.0));
            Harvest(w, a, 1000);
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                // to the first carrier on the way
                int carrier = 0; float load = 0f;
                long end = w.Now + SimTime.Days(3);
                while (w.Now < end && carrier == 0)
                {
                    w.AdvanceTo(w.Now + SimTime.Minutes(10));
                    var res = w.Residents(a);
                    for (int i = 0; i < res.Count && carrier == 0; i++)
                    {
                        var q = w.People.Get(res[i]);
                        if (q.Alive && q.Why == WhyCode.Tribute && q.CargoTo == Trade.ToLord && q.CargoQty > 0f && q.At != b) { carrier = q.Id; load = q.CargoQty; }
                    }
                }
                Assume.That(carrier != 0, "a carrier on the way");
                double due = w.Settlements.Get(a).TributeDue, store = w.Settlements.Get(a).Granary;
                // everyone home at once (COARSE) while the load is on the way: it is set aside again with the rest, not put in the common store
                w.RequestResolution(Resolution.Coarse, w.Now + 1);
                w.AdvanceTo(w.Now + 2);
                Assert.AreEqual(0f, w.People.Get(carrier).CargoQty, "home with it");
                Assert.AreEqual(due + load, w.Settlements.Get(a).TributeDue, 1e-3, "the load set aside again");
                Assert.AreEqual(store, w.Settlements.Get(a).Granary, 1e-6, "not into the common store");
                long t0 = w.Now;
                while (w.Settlements.Get(a).TributeDue >= 1.0 && w.Now - t0 < SimTime.Days(30)) w.AdvanceTo(w.Now + SimTime.Day);
                Assert.Less(w.Settlements.Get(a).TributeDue, 1.0, "carried at last");
                Assert.Greater(Conflict.CarryTally[1], 99.0 - 1e-3, "the whole tenth brought to the lord");
            }
            finally { Conflict.CarryTally = null; }
        }

        [Test]
        public void ABondThatEndsGivesBackWhatIsStillDue()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            Harvest(w, a, 1000);
            Assume.That(w.Settlements.Get(a).TributeDue > 99.0);
            // a sickness kills the lord's grown people: at the next harvest the tributary is the stronger and pays no more
            var rb = new List<int>();
            var lb = w.Residents(b);
            for (int i = 0; i < lb.Count; i++) rb.Add(lb[i]);
            for (int i = 0; i < rb.Count && w.Conflict.Might(b, a) >= w.Conflict.Might(a, b); i++)
            {
                ref var q = ref w.People.Get(rb[i]);
                if (q.Alive && q.AgeYears(w.Now) >= 16 && q.AgeYears(w.Now) < 60) w.Agents.Kill(ref q, 0, 10);
            }
            double store = w.Settlements.Get(a).Granary, due = w.Settlements.Get(a).TributeDue;
            w.Conflict.Harvested(a, 0.0);
            Assert.AreEqual(0, w.Settlements.Get(a).Lord, "it pays no more");
            Assert.AreEqual(0.0, w.Settlements.Get(a).TributeDue, 1e-9, "nothing set aside for it");
            Assert.AreEqual(store + due, w.Settlements.Get(a).Granary, 1e-6, "what was set aside is the village's again");
        }

        [Test]
        public void NoWayOnFootToTheLordSetsNothingAside()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            // water all round the tributary's ground (a bond for bread needs only a day's walk as the crow flies: C10.4)
            var P = w.Planet;
            int c = w.Settlements.Get(a).Cell, cb = w.Settlements.Get(b).Cell;
            for (int q = 0; q < 8; q++) { int nb = P.Grid.Nbr8[c * 8 + q]; Assume.That(nb != cb, "the lord on another cell"); P.Elev[nb] = (float)(P.SeaLevel - 20.0); }
            w.Routes.Invalidate();
            Assume.That(w.Routes.Get(w, a, b) == null, "no way on foot between them");
            double store = w.Settlements.Get(a).Granary, lord0 = w.Settlements.Get(b).Granary;
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                Harvest(w, a, 1000);
                Assert.AreEqual(0.0, w.Settlements.Get(a).TributeDue, 1e-9, "nothing set aside: nobody could carry it there");
                Assert.AreEqual(store + 1000.0, w.Settlements.Get(a).Granary, 1e-6, "the whole harvest stays in its store");
                Assert.AreEqual(lord0, w.Settlements.Get(b).Granary, 1e-6, "nothing reaches the lord");
                Assert.AreEqual(1.0, Conflict.CarryTally[8], "a tenth not set aside for want of a way");
                Assert.AreEqual(100.0, Conflict.CarryTally[9], 1e-6, "of a tenth of the harvest");
                w.AdvanceTo(w.Now + SimTime.Days(3));
                Assert.AreEqual(0.0, w.Settlements.Get(a).TributeDue, 1e-9, "and none carried");
                Assert.AreEqual(1, w.Settlements.Get(a).Lord == b ? 1 : 0, "still bound");
                string page = Gazetteer.Neighbours(w, a);
                Console.WriteLine(page);
                StringAssert.Contains("No way on foot leads from it to", page, "the page tells why nothing goes");
            }
            finally { Conflict.CarryTally = null; }
        }

        [Test]
        public void ATenthNobodyCarriedInAYearIsTheVillagesAgainAtTheNextHarvest()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                Harvest(w, a, 1000);
                Assume.That(w.Settlements.Get(a).TributeDue > 99.0, "a tenth set aside");
                // the next harvest comes with the last tenth still set aside (nobody fit to carry it)
                double store = w.Settlements.Get(a).Granary;
                Harvest(w, a, 500);
                Assert.AreEqual(50.0, w.Settlements.Get(a).TributeDue, 1e-6, "only the new tenth set aside");
                Assert.AreEqual(store + 500.0 + 100.0 - 50.0, w.Settlements.Get(a).Granary, 1e-6, "the old tenth back in its store");
                Assert.AreEqual(100.0, Conflict.CarryTally[7], 1e-6, "told: a year uncarried");
            }
            finally { Conflict.CarryTally = null; }
        }

        [Test]
        public void OldTributeGoesAtTheHarvest()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            double lord0 = w.Settlements.Get(b).Granary;
            Conflict.CarryOff = true;
            try { Harvest(w, a, 1000); }
            finally { Conflict.CarryOff = false; }
            Assert.AreEqual(0.0, w.Settlements.Get(a).TributeDue, 1e-9, "--oldtribute: nothing set aside");
            Assert.AreEqual(lord0 + 100.0, w.Settlements.Get(b).Granary, 1e-6, "--oldtribute: in the lord's store at once");
        }

        [Test]
        public void ASaveTakenWhileTributeIsCarriedContinuesIdentically()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "a tributary and its lord a walk apart");
            w.AdvanceTo(ToLocal(w, a, 1.0));
            Harvest(w, a, 1000);
            // to the first carrier on the way
            bool out1 = false;
            long end = w.Now + SimTime.Days(3);
            while (w.Now < end && !out1)
            {
                w.AdvanceTo(w.Now + SimTime.Minutes(20));
                var res = w.Residents(a);
                for (int i = 0; i < res.Count && !out1; i++) { var q = w.People.Get(res[i]); out1 = q.Alive && q.Why == WhyCode.Tribute && q.CargoTo == Trade.ToLord && q.CargoQty > 0f; }
            }
            Assume.That(out1, "a carrier on the way");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Days(2);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }
    }
}
