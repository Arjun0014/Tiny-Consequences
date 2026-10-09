using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The household's grain and what households owe (C3.3, D2-126): the village's harvest shared among its households by their mouths, its own
    /// produce a household's own, the larder filled from its own grain, then the village's common grain, then its kin's spare (a gift), then a loan
    /// (a debt, repaid from the next harvest's surplus); losses nobody drew borne by all by their shares; the accounting moves no grain. Mechanisms,
    /// not outcomes.
    /// </summary>
    [TestFixture]
    public class HoldingsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static double Held(World w, int sid)
        {
            double h = w.Settlements.Get(sid).Common;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) h += w.Households.Get(hl[i]).Grain;
            return h;
        }

        [Test]
        public void AWorldsFirstStoreIsItsHouseholdsByTheirMouths()
        {
            var w = TestWorlds.Make(911);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                Assert.AreEqual(w.Settlements.Get(sid).Granary, Held(w, sid), 1e-6, "every grain of the store is someone's");
                var hl = w.HouseholdsOf(sid);
                double per = -1;
                for (int i = 0; i < hl.Count; i++)
                {
                    double need = w.SettlementSys.HouseholdNeed(hl[i]);
                    if (need <= 0) continue;
                    double r = w.Households.Get(hl[i]).Grain / need;
                    if (per < 0) per = r; else Assert.AreEqual(per, r, 1e-6, "by their mouths");
                }
            }
        }

        [Test]
        public void TheHarvestIsSharedByMouthsAndAHouseholdsCatchIsItsOwn()
        {
            var w = TestWorlds.Make(912);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 2);
            var before = new double[hl.Count];
            for (int i = 0; i < hl.Count; i++) before[i] = w.Households.Get(hl[i]).Grain;
            double yield = 1000;
            s.Granary += yield;
            w.Holdings.Harvest(sid, yield);
            double total = 0;
            for (int i = 0; i < hl.Count; i++) total += w.SettlementSys.HouseholdNeed(hl[i]);
            for (int i = 0; i < hl.Count; i++)
                Assert.AreEqual(before[i] + yield * w.SettlementSys.HouseholdNeed(hl[i]) / total, w.Households.Get(hl[i]).Grain, 1e-6, "its share by its mouths");
            // a household's catch beyond its larder goes into the store as its own grain
            int hid = hl[0];
            var p = w.People.Get(w.Members(hid)[0]);
            double g0 = w.Households.Get(hid).Grain, others0 = Held(w, sid) - g0;
            ref var hh = ref w.Households.Get(hid); hh.Larder = 1e6;   // (premise: its larder full)
            w.Wild.Deposit(p.Id, 30);
            Assert.AreEqual(g0 + 30, w.Households.Get(hid).Grain, 1e-6, "its catch, its own");
            Assert.AreEqual(others0, Held(w, sid) - w.Households.Get(hid).Grain, 1e-6, "nobody else's");
            Assert.AreEqual(w.Settlements.Get(sid).Granary, Held(w, sid), 1e-6);
        }

        [Test]
        public void ALarderTakesItsOwnThenTheCommonThenKinThenALoan()
        {
            var w = TestWorlds.Make(913);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int a = hl[0];
            // premise: a has 5 FU of its own, the village 7 of common grain, nobody else has more than they need but one household
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; }
            ref var s = ref w.Settlements.Get(sid);
            w.Households.Get(a).Grain = 5;
            s.Common = 7;
            var kin = Holdings.KinOf(w, sid, a);
            int lender = 0;
            for (int i = 0; i < hl.Count; i++) if (hl[i] != a && !kin.Contains(hl[i])) { lender = hl[i]; break; }
            Assume.That(lender != 0, "a household that is not a's kin");
            double lend = 10000;
            w.Households.Get(lender).Grain = lend;
            s.Granary = Held(w, sid);
            double fu = 5 + 7 + 20;
            s.Granary -= fu;
            w.Holdings.Draw(sid, a, fu, 20);
            Assert.AreEqual(0, w.Households.Get(a).Grain, 1e-9, "its own first");
            Assert.AreEqual(0, w.Settlements.Get(sid).Common, 1e-9, "then the village's common grain");
            Assert.AreEqual(lend - 20, w.Households.Get(lender).Grain, 1e-6, "then a loan from the one with grain to spare");
            Assert.AreEqual(20, w.Holdings.Owes(a, out int to), 1e-6, "the debt recorded");
            Assert.AreEqual(1, to);
            Assert.AreEqual(20, w.Holdings.Owed(lender, out int by), 1e-6);
            Assert.AreEqual(w.Settlements.Get(sid).Granary, Held(w, sid), 1e-6, "the account holds");
            // kin give first, and their gift is not reckoned
            if (kin.Count > 0)
            {
                int k = kin[0];
                w.Households.Get(k).Grain = 5000;
                ref var s2 = ref w.Settlements.Get(sid); s2.Granary += 5000;
                s2.Granary -= 15;
                w.Holdings.Draw(sid, a, 15, 20);
                Assert.AreEqual(5000 - 15, w.Households.Get(k).Grain, 1e-6, "its kin gave");
                Assert.AreEqual(20, w.Holdings.Owes(a, out _), 1e-6, "a gift: nothing more owed");
            }
            // a loan never takes what the lender needs itself until the harvest
            var w2 = TestWorlds.Make(913);
            var hl2 = w2.HouseholdsOf(sid);
            for (int i = 0; i < hl2.Count; i++) { ref var h = ref w2.Households.Get(hl2[i]); h.Grain = 0; }
            int l2 = lender;
            double needL = w2.SettlementSys.HouseholdNeed(l2);
            w2.Households.Get(l2).Grain = needL * (20 + Holdings.Reserve) + 3;   // (3 FU beyond its own)
            ref var s3 = ref w2.Settlements.Get(sid); s3.Common = 0; s3.Granary = Held(w2, sid) - 10;
            w2.Holdings.Draw(sid, a, 10, 20);
            Assert.AreEqual(needL * (20 + Holdings.Reserve), w2.Households.Get(l2).Grain, 1e-6, "lent only what it could spare");
            Assert.AreEqual(3, w2.Holdings.Owes(a, out _), 1e-6);
        }

        [Test]
        public void ADebtIsRepaidFromTheNextHarvestsSurplusOldestFirst()
        {
            var w = TestWorlds.Make(914);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int a = hl[0], b = hl[1], c = hl[2];
            // premise: a owes c 40 run up lately and b 30 run up a year before
            w.Holdings.Owe(a, c, 40, w.Now);
            w.Holdings.Owe(a, b, 30, w.Now - SimTime.Year);
            Assert.AreEqual(70, w.Holdings.Owes(a, out int to), 1e-6);
            Assert.AreEqual(2, to);
            // the harvest leaves a 35 FU beyond a year's needs: the oldest debt (to b) first
            double needA = w.SettlementSys.HouseholdNeed(a);
            double ownYear = needA * (SimTime.DaysPerYear + Holdings.Reserve);
            double total = 0;
            for (int i = 0; i < hl.Count; i++) total += w.SettlementSys.HouseholdNeed(hl[i]);
            double yield = (ownYear + 35 - w.Households.Get(a).Grain) * total / needA;
            double b0 = w.Households.Get(b).Grain, c0 = w.Households.Get(c).Grain;
            ref var s3 = ref w.Settlements.Get(sid); s3.Granary += yield;
            w.Holdings.Harvest(sid, yield);
            double shareB = yield * w.SettlementSys.HouseholdNeed(b) / total, shareC = yield * w.SettlementSys.HouseholdNeed(c) / total;
            Assert.AreEqual(b0 + shareB + 30, w.Households.Get(b).Grain, 1e-4, "b, lent first, is repaid in full");
            Assert.AreEqual(c0 + shareC + 5, w.Households.Get(c).Grain, 1e-4, "c gets what is left");
            Assert.AreEqual(35, w.Holdings.Owes(a, out to), 1e-4);
            Assert.AreEqual(1, to);
            Assert.AreEqual(ownYear, w.Households.Get(a).Grain, 1e-4, "a keeps a year's needs");
        }

        /// <summary>A household of the village that is not close kin to <paramref name="of"/> (and not it), else 0.</summary>
        static int NotKin(World w, int sid, int of, int skip = 0)
        {
            var kin = Holdings.KinOf(w, sid, of);
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (hl[i] != of && hl[i] != skip && !kin.Contains(hl[i])) return hl[i];
            return 0;
        }

        [Test]
        public void LossesNobodyDrewFallOnAllByTheirSharesAndGrainFromNoHouseholdIsTheVillages()
        {
            var w = TestWorlds.Make(915);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            double g0 = w.Settlements.Get(sid).Granary;
            Assume.That(g0 > 100);
            var before = new double[hl.Count];
            for (int i = 0; i < hl.Count; i++) before[i] = w.Households.Get(hl[i]).Grain;
            double c0 = w.Settlements.Get(sid).Common;
            ref var s = ref w.Settlements.Get(sid);
            s.Granary *= 0.75;   // (a theft, rot, a sale: by no household's hand)
            w.Holdings.Reconcile(sid);
            for (int i = 0; i < hl.Count; i++) Assert.AreEqual(before[i] * 0.75, w.Households.Get(hl[i]).Grain, 1e-6, "each bin bears its share");
            Assert.AreEqual(c0 * 0.75, w.Settlements.Get(sid).Common, 1e-6);
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Granary += 80;   // (relief, a sale's price, tribute: from no household)
            w.Holdings.Reconcile(sid);
            Assert.AreEqual(c0 * 0.75 + 80, w.Settlements.Get(sid).Common, 1e-6, "the village's common grain");
        }

        [Test]
        public void AnHeirTakesTheGrainTheDebtsAndTheClaims()
        {
            var w = TestWorlds.Make(916);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int a = hl[0], b = NotKin(w, sid, a), c = NotKin(w, sid, a, b);
            Assume.That(b != 0 && c != 0);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; }
            ref var s = ref w.Settlements.Get(sid); s.Common = 0;
            w.Households.Get(b).Grain = 1e4; s.Granary = Held(w, sid) - 25;
            w.Holdings.Draw(sid, a, 25, 20);   // a owes b 25
            w.Households.Get(a).Grain = 60;
            ref var s1 = ref w.Settlements.Get(sid); s1.Granary += 60;
            w.Holdings.Inherit(a, c, sid);
            Assert.AreEqual(60, w.Households.Get(c).Grain, 1e-9, "the heir's grain");
            Assert.AreEqual(25, w.Holdings.Owes(c, out _), 1e-9, "and the debt");
            Assert.AreEqual(0, w.Holdings.Owes(a, out _), 1e-9);
            // the creditor dies: its claim is its heir's; a debt to oneself is no more
            w.Holdings.Inherit(b, c, sid);
            Assert.AreEqual(0, w.Holdings.Owes(c, out _), 1e-9, "owed to itself: gone");
            // no heir: the grain is the village's and the debts lapse
            var w2 = TestWorlds.Make(916);
            var hl2 = w2.HouseholdsOf(sid);
            for (int i = 0; i < hl2.Count; i++) { ref var h = ref w2.Households.Get(hl2[i]); h.Grain = 0; }
            ref var t = ref w2.Settlements.Get(sid); t.Common = 0;
            w2.Households.Get(b).Grain = 1e4; t.Granary = Held(w2, sid) - 25;
            w2.Holdings.Draw(sid, a, 25, 20);
            w2.Households.Get(a).Grain = 9;
            w2.Holdings.Inherit(a, 0, sid);
            Assert.AreEqual(9, w2.Settlements.Get(sid).Common, 1e-9);
            Assert.AreEqual(0, w2.Holdings.Owed(b, out _), 1e-9, "lapsed");
        }

        /// <summary>The accounting moves no grain: with the household's means read as before, a world lives as it did without it (lived and COARSE).</summary>
        [Test]
        public void TheAccountingMovesNothingInTheWorld()
        {
            foreach (bool coarse in new[] { false, true })
            {
                var runs = new List<string>();
                foreach (bool off in new[] { true, false })
                {
                    Holdings.Off = off; Holdings.MeansOff = true;
                    try
                    {
                        var w = TestWorlds.Make(917);
                        if (coarse) Coarse(w);
                        // premise: each village's grain all in one household's bin - the others live on the common grain, their kin and loans
                        for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                        {
                            var hl = w.HouseholdsOf(sid);
                            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; }
                            ref var s0 = ref w.Settlements.Get(sid); s0.Common = 0;
                            if (hl.Count > 0 && !off) w.Households.Get(hl[hl.Count - 1]).Grain = s0.Granary;
                        }
                        w.AdvanceTo(w.Now + SimTime.Days(coarse ? 120 : 20));
                        var sb = new System.Text.StringBuilder();
                        for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) { var s = w.Settlements.Get(sid); sb.Append(s.Population).Append(':').Append(s.Granary.ToString("R")).Append(':').Append(s.Woodstore.ToString("R")).Append(' '); }
                        sb.Append(w.LivingPeople()).Append(' ').Append(w.Metrics.Births).Append(' ').Append(w.Metrics.Deaths).Append(' ').Append(w.History.Count);
                        runs.Add(sb.ToString());
                        if (!off) { Console.WriteLine((coarse ? "COARSE" : "lived") + ": fed from common " + w.Metrics.CommonFedFu.ToString("0") + ", kin " + w.Metrics.KinFu.ToString("0") + ", lent " + w.Metrics.LoanFu.ToString("0") + ", repaid " + w.Metrics.RepaidFu.ToString("0")); Assert.Greater(w.Metrics.LoanFu, 0, "loans made"); }
                    }
                    finally { Holdings.Off = false; Holdings.MeansOff = false; }
                }
                Assert.AreEqual(runs[0], runs[1], (coarse ? "COARSE" : "lived") + ": the same world with and without the households' account");
            }
        }

        [Test]
        public void SavedWithDebtsTheWorldContinuesTheSame()
        {
            var w = TestWorlds.Make(918);
            Coarse(w);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 2);
            int lender = NotKin(w, sid, hl[0]);
            Assume.That(lender != 0);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; }
            w.Households.Get(lender).Grain = 5000;
            ref var s = ref w.Settlements.Get(sid); s.Common = 0; s.Granary = Held(w, sid) - 40;
            w.Holdings.Draw(sid, hl[0], 40, 20);
            Assume.That(w.Holdings.DebtCount > 0);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "loaded, the same");
            w.AdvanceTo(w.Now + SimTime.Days(60)); w2.AdvanceTo(w2.Now + SimTime.Days(60));
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and lives on the same");
            Assert.AreEqual(w.Holdings.DebtCount, w2.Holdings.DebtCount);
        }

        // ------------------------------------------------------------------ makers paid in kind (C3.3b)

        [Test]
        public void AHouseholdTakingWaresPaysItsMakersOrOwesThem()
        {
            var w = TestWorlds.Make(921);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int maker1 = hl[0], maker2 = hl[1], taker = hl[2];
            ref var s = ref w.Settlements.Get(sid);
            s.BestLoom = 0.5f;
            double price = Holdings.Price(w, in s, Holdings.Wares);
            Assert.AreEqual(w.Config.FoodPerAdultDay / Weaving.MakerDay(in s), price, 1e-9, "the food of the maker-days a ware took");
            Assert.That(price, Is.InRange(5.0, 40.0), "a ware: some days of a maker's work");
            // premise: the two makers' wares in the store, 3 and 1; the taker has grain
            s.Wares = 4;
            w.Holdings.Made(maker1, Holdings.Wares, 3);
            w.Holdings.Made(maker2, Holdings.Wares, 1);
            w.Households.Get(taker).Grain = 1000;
            double g1 = w.Households.Get(maker1).Grain, g2 = w.Households.Get(maker2).Grain;
            ref var hh = ref w.Households.Get(taker);
            hh.Gear = 0;
            int members = w.Members(taker).Count;
            ref var s1 = ref w.Settlements.Get(sid);
            Weaving.Draw(w, ref s1, ref w.Households.Get(taker), members);
            double took = 4 - w.Settlements.Get(sid).Wares;
            Assume.That(took > 0.5, "the taker took wares");
            Assert.AreEqual(1000 - took * price, w.Households.Get(taker).Grain, 1e-6, "paid their worth from its grain");
            Assert.AreEqual(g1 + took * price * 0.75, w.Households.Get(maker1).Grain, 1e-6, "each maker by its share of what is theirs in store");
            Assert.AreEqual(g2 + took * price * 0.25, w.Households.Get(maker2).Grain, 1e-6);
            Assert.AreEqual(3 - took * 0.75, w.Households.Get(maker1).WaresDue, 1e-9, "what is still theirs");
            // a household short of grain takes only what it can pay for (no debts to the makers): with none, none - it makes its own
            var w2 = TestWorlds.Make(921);
            ref var t = ref w2.Settlements.Get(sid); t.BestLoom = 0.5f; t.Wares = 2;
            w2.Holdings.Made(maker1, Holdings.Wares, 2);
            w2.Households.Get(taker).Grain = 0; w2.Households.Get(taker).Gear = 0;
            ref var t1 = ref w2.Settlements.Get(sid);
            Weaving.Draw(w2, ref t1, ref w2.Households.Get(taker), w2.Members(taker).Count);
            Assert.AreEqual(2, w2.Settlements.Get(sid).Wares, 1e-9, "no grain: no wares taken");
            Assert.AreEqual(0, w2.Holdings.Owes(taker, out _), 1e-9, "and nothing owed");
            double p2 = Holdings.Price(w2, in w2.Settlements.Get(sid), Holdings.Wares);
            w2.Households.Get(taker).Grain = 0.5 * p2;   // (grain for half a ware)
            ref var t2 = ref w2.Settlements.Get(sid);
            Weaving.Draw(w2, ref t2, ref w2.Households.Get(taker), w2.Members(taker).Count);
            Assert.AreEqual(1.5, w2.Settlements.Get(sid).Wares, 1e-6, "half a ware: what its grain pays for");
            Assert.AreEqual(0, w2.Households.Get(taker).Grain, 1e-6);
            // wares nobody here made (bought in) are the village's: their worth is its common grain
            var w3 = TestWorlds.Make(921);
            ref var u = ref w3.Settlements.Get(sid); u.BestLoom = 0.5f; u.Wares = 2; double c0 = u.Common;
            w3.Households.Get(taker).Grain = 1000; w3.Households.Get(taker).Gear = 0;
            ref var u1 = ref w3.Settlements.Get(sid);
            Weaving.Draw(w3, ref u1, ref w3.Households.Get(taker), w3.Members(taker).Count);
            double took3 = 2 - w3.Settlements.Get(sid).Wares;
            Assert.AreEqual(c0 + took3 * Holdings.Price(w3, in w3.Settlements.Get(sid), Holdings.Wares), w3.Settlements.Get(sid).Common, 1e-6);
        }

        [Test]
        public void TheVillagePaysForItsJarsAndItsSmithsWorkFromItsCommonGrainThenALevy()
        {
            var w = TestWorlds.Make(922);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int smith = hl[0];
            // premise: no common grain; every household 100 FU
            for (int i = 0; i < hl.Count; i++) w.Households.Get(hl[i]).Grain = 100;
            ref var s = ref w.Settlements.Get(sid); s.Common = 10; s.Granary = Held(w, sid);
            double hours = 200, fu = hours * Holdings.HourWage(w);
            Assume.That(fu > 10);
            w.Holdings.Commission(sid, smith, hours);
            Assert.AreEqual(100 + fu, w.Households.Get(smith).Grain, 1e-6, "a day's food for a day's work at the forge");
            Assert.AreEqual(0, w.Settlements.Get(sid).Common, 1e-9, "the common grain first");
            double levy = fu - 10;
            for (int i = 1; i < hl.Count; i++) Assert.AreEqual(100 - levy / (hl.Count - 1), w.Households.Get(hl[i]).Grain, 1e-6, "then a levy by their shares - the smith's own excepted");
            Assert.AreEqual(w.Settlements.Get(sid).Granary, Held(w, sid), 1e-6, "no grain made or lost");
            // jars: ware stood in the granaries is paid to its potters by the village
            var w2 = TestWorlds.Make(922);
            int potter = hl[1];
            for (int i = 0; i < hl.Count; i++) w2.Households.Get(hl[i]).Grain = 100;
            ref var t = ref w2.Settlements.Get(sid); t.Common = 1000; t.Granary = Held(w2, sid); t.Pots = 50; t.Jars = 0; t.Granaries = 1;   // (premise: a granary standing)
            w2.Holdings.Made(potter, Holdings.Pots, 50);
            Pottery.Day(w2, sid, 0, 1);
            double set = 50 - w2.Settlements.Get(sid).Pots;
            Assume.That(set > 1, "jars stood");
            double worth = set * Holdings.Price(w2, in w2.Settlements.Get(sid), Holdings.Pots);
            Assert.AreEqual(100 + worth, w2.Households.Get(potter).Grain, 1e-6, "the potter paid for the jars");
            Assert.AreEqual(1000 - worth, w2.Settlements.Get(sid).Common, 1e-6, "from the common grain");
        }

        // ------------------------------------------------------------------ hard times (C3.3c)

        [Test]
        public void InHardTimesSharingContractsToTheHouseholdAndItsKin()
        {
            foreach (bool hard in new[] { false, true })
            {
                var w = TestWorlds.Make(931);
                int sid = w.Settlements.First();
                var hl = w.HouseholdsOf(sid);
                Assume.That(hl.Count >= 3);
                // premise: a household's member is the child of one of another household's (a married child and their parents' household)
                int a = hl[0], k = hl[1];
                {
                    int child = w.Members(a)[0], parent = w.Members(k)[0];
                    ref var c = ref w.People.Get(child);
                    if (w.People.Get(parent).IsFemale) c.Mother = parent; else c.Father = parent;
                }
                Assume.That(Holdings.KinOf(w, sid, a).Contains(k), "kin households");
                int l = NotKin(w, sid, a, k);
                Assume.That(l != 0 && !Holdings.KinOf(w, sid, a).Contains(l));
                for (int i = 0; i < hl.Count; i++) w.Households.Get(hl[i]).Grain = 0;
                ref var s = ref w.Settlements.Get(sid);
                s.Common = 0; s.Ration = hard ? 0.5 : 1.0;
                int toHarvest = 20;
                double needK = w.SettlementSys.HouseholdNeed(k), needL = w.SettlementSys.HouseholdNeed(l);
                // premise: the kin household holds its full needs to the harvest and 10 more at full meals; the other its full needs exactly
                w.Households.Get(k).Grain = needK * (toHarvest + Holdings.Reserve) + 10;
                w.Households.Get(l).Grain = needL * (toHarvest + Holdings.Reserve);
                s.Granary = Held(w, sid);
                Assert.AreEqual(hard, Holdings.Hard(in w.Settlements.Get(sid)));
                double asked = 10 + needK * 0.5 * (toHarvest + Holdings.Reserve) + 25;
                double got = w.Holdings.Provide(sid, a, asked, toHarvest);
                if (!hard)
                {
                    Assert.AreEqual(asked, got, 1e-9, "ordinary times: the larder gets all it asked; what nobody could spare falls on all by their shares");
                    Assert.AreEqual(needK * (toHarvest + Holdings.Reserve), w.Households.Get(k).Grain, 1e-6, "kin gave only their spare");
                }
                else
                {
                    double kinGives = w.Households.Get(k).Grain;   // (what is left of theirs)
                    Assert.AreEqual(needK * 0.5 * (toHarvest + Holdings.Reserve), kinGives, 1e-6, "kin share the shortage down to the village's ration");
                    Assert.AreEqual(needL * (toHarvest + Holdings.Reserve), w.Households.Get(l).Grain, 1e-9, "the others keep their own needs");
                    Assert.AreEqual(asked - 25, got, 1e-6, "no more than kin and true spare could give");
                    Assert.AreEqual(25, w.Metrics.UnsharedFu, 1e-6);
                    // a meal straight from the store: its own grain and the common grain only
                    Assert.AreEqual(w.Households.Get(a).Grain + w.Settlements.Get(sid).Common, w.Holdings.Available(sid, a), 1e-9);
                }
            }
        }

        [Test]
        public void UnpaidTheMakersEarnNothing()
        {
            Holdings.PayOff = true;
            try
            {
                var w = TestWorlds.Make(921);
                int sid = w.Settlements.First();
                var hl = w.HouseholdsOf(sid);
                ref var s = ref w.Settlements.Get(sid); s.Wares = 2;
                w.Holdings.Made(hl[0], Holdings.Wares, 2);
                w.Households.Get(hl[2]).Grain = 1000; w.Households.Get(hl[2]).Gear = 0;
                Weaving.Draw(w, ref w.Settlements.Get(sid), ref w.Households.Get(hl[2]), w.Members(hl[2]).Count);
                Assert.AreEqual(1000, w.Households.Get(hl[2]).Grain, 1e-9);
                Assert.AreEqual(0, w.Households.Get(hl[0]).WaresDue, 1e-9);
            }
            finally { Holdings.PayOff = false; }
        }
    }
}
