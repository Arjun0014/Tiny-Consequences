using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The lord's household (C10.11): a first household whose grain runs years beyond its needs keeps landless households - its own village's, the neediest
    /// first, and a lord's realm's, who come to the seat - feeding each a year of its need at the harvest; kept households do not weigh leaving; they are let go
    /// when the grain no longer keeps them. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class HallTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static World Pair(out int a, out int b)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= w.Config.MarriageReach && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        /// <summary>Premise: some households of a village hold no land (as a full land's landless do), with little food of their own.</summary>
        static List<int> MakeLandless(World w, int sid, int n)
        {
            var list = new List<int>();
            var hhs = w.HouseholdsOf(sid);
            int first = Leaders.First(in w.Settlements.Get(sid));
            for (int i = 0; i < hhs.Count && list.Count < n; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                if (hhs[i] == first || hh.Party != 0 || hh.Dissolved != 0 || w.Members(hhs[i]).Count == 0) continue;
                hh.Land = 0; hh.Grain = 2.0 * (list.Count + 1);
                list.Add(hhs[i]);
            }
            return list;
        }

        static World Settled(out int a, out int b)
        {
            var w = Pair(out a, out b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            for (int d = 0; d < 60 && (Leaders.First(in w.Settlements.Get(a)) == 0 || Leaders.First(in w.Settlements.Get(b)) == 0); d++) w.AdvanceTo(w.Now + SimTime.Day);
            Assume.That(Leaders.First(in w.Settlements.Get(a)) != 0, "a first household at a");
            return w;
        }

        [Test]
        public void AFirstHouseholdWithGrainToSpareKeepsItsVillagesLandlessTheNeediestFirst()
        {
            Hall.Off = false;
            var w = Settled(out int a, out int b);
            int first = Leaders.First(in w.Settlements.Get(a));
            var landless = MakeLandless(w, a, 3);
            Assume.That(landless.Count == 3, "three landless households");
            // premise: the first household's grain keeps two of them, not three (beyond its own next year and three more)
            double year(int h) => w.SettlementSys.HouseholdNeed(h) * SimTime.DaysPerYear;
            w.Households.Get(first).Grain = year(first) * 4.0 + 3.0 * (year(landless[0]) + year(landless[1])) + 1.0;
            Hall.Harvest(w, a, 0);
            Assert.AreEqual(first, w.Households.Get(landless[0]).Keeper, "the neediest kept");
            Assert.AreEqual(first, w.Households.Get(landless[1]).Keeper);
            Assert.AreEqual(0, w.Households.Get(landless[2]).Keeper, "the grain keeps two: the least needy is not taken in");
            Assert.Greater(w.Households.Get(landless[0]).Grain, year(landless[0]) * 0.99, "a year of its need from the first household's grain");
            int taken = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.TakenIn && e.Object.Id == first) { taken++; StringAssert.Contains("into their keep", HistoryText.Describe(w, in e)); } }
            Assert.AreEqual(2, taken, "told");
            // kept households do not weigh leaving: fed by their keeper
            Assert.AreEqual(2, Hall.Count(w, a));
            // the grain gone: at the next harvest they are let go
            w.Households.Get(first).Grain = 0;
            Hall.Harvest(w, a, 0);
            Assert.AreEqual(0, w.Households.Get(landless[0]).Keeper, "let go when the grain no longer keeps them");
            Assert.AreEqual(0, Hall.Count(w, a));
        }

        [Test]
        public void ALordsHouseholdKeepsItsRealmsLandlessWhoComeToTheSeat()
        {
            Hall.Off = false;
            var w = Settled(out int a, out int b);
            Assume.That(w.Settlements.Get(b).Inhabited, "a tributary");
            // premise: b pays a (a bond as the world makes them), the lord's first household rich in grain, its own village's people all holding land
            w.Settlements.Get(b).Lord = a;
            int first = Leaders.First(in w.Settlements.Get(a));
            var hhsA = w.HouseholdsOf(a);
            for (int i = 0; i < hhsA.Count; i++) { ref var hh = ref w.Households.Get(hhsA[i]); if (hh.Land < 0.05) hh.Land = 1.0; }
            var landless = MakeLandless(w, b, 2);
            Assume.That(landless.Count == 2 && w.Conflict.PolitySize(a) > 0, "a lord and landless households in its tributary");
            w.Households.Get(first).Grain += 50000; w.Settlements.Get(a).Granary += 50000;   // (its own grain in its village's store: C3.3)
            Hall.Harvest(w, a, 0);
            for (int k = 0; k < 2; k++)
            {
                Assert.AreEqual(first, w.Households.Get(landless[k]).Keeper, "kept by the lord's household");
                Assert.AreNotEqual(0, w.Households.Get(landless[k]).Party, "and on its way to the seat");
            }
            w.AdvanceTo(w.Now + SimTime.Days(3));
            for (int k = 0; k < 2; k++) Assert.AreEqual(a, w.Households.Get(landless[k]).Settlement, "living at the seat now");
            double g0 = w.Households.Get(landless[0]).Grain;
            Hall.Harvest(w, a, 0);
            ref var k0 = ref w.Households.Get(landless[0]);
            if (k0.Land >= 0.05) Assert.AreEqual(0, k0.Keeper, "given land at the seat (it had land to spare): let go, holding land of its own");
            else Assert.Greater(k0.Grain, g0, "fed at the seat's harvest");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ANewFirstHouseholdTakesOverItsPredecessorsDependants()
        {
            Hall.Off = false;
            var w = Settled(out int a, out _);
            int first = Leaders.First(in w.Settlements.Get(a));
            var landless = MakeLandless(w, a, 2);
            w.Households.Get(first).Grain += 50000; w.Settlements.Get(a).Granary += 50000;
            Hall.Harvest(w, a, 0);
            Assume.That(w.Households.Get(landless[0]).Keeper == first, "kept");
            // premise: another household of the village becomes its first (as a following grows), rich enough to keep them
            int next = 0;
            var hhs = w.HouseholdsOf(a);
            for (int i = 0; i < hhs.Count && next == 0; i++) if (hhs[i] != first && !landless.Contains(hhs[i]) && w.Members(hhs[i]).Count > 0) next = hhs[i];
            Assume.That(next != 0, "another household");
            w.Settlements.Get(a).HeadHh = next;
            w.Households.Get(next).Grain += 50000; w.Settlements.Get(a).Granary += 50000;
            Hall.Harvest(w, a, 0);
            Assert.AreEqual(next, w.Households.Get(landless[0]).Keeper, "the hall's people pass with the hall");
            Assert.AreEqual(next, w.Households.Get(landless[1]).Keeper);
        }

        [Test]
        public void NoKeepWithTheRuleOff()
        {
            Hall.Off = true;
            try
            {
                var w = Settled(out int a, out _);
                int first = Leaders.First(in w.Settlements.Get(a));
                var landless = MakeLandless(w, a, 2);
                w.Households.Get(first).Grain += 50000; w.Settlements.Get(a).Granary += 50000;
                Hall.Harvest(w, a, 0);
                foreach (int h in landless) Assert.AreEqual(0, w.Households.Get(h).Keeper);
            }
            finally { Hall.Off = false; }
        }

        [Test]
        public void ASaveWithKeptHouseholdsContinuesIdentically()
        {
            Hall.Off = false;
            var w = Settled(out int a, out _);
            int first = Leaders.First(in w.Settlements.Get(a));
            MakeLandless(w, a, 2);
            w.Households.Get(first).Grain += 50000; w.Settlements.Get(a).Granary += 50000;
            Hall.Harvest(w, a, 0);
            Assume.That(Hall.Count(w, a) > 0, "households kept");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Years(1);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }
    }
}
