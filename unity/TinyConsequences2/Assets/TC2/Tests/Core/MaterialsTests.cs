using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Materials from the planet (D2-063): goods draw fibre and hides from the village's store and are improvised without them;
    /// the hunt and the slaughter give hides, the harvest fibre; pottery is found only where clay is near and the ard only with an
    /// ox; pots keep grain and save wood, the ard speeds the fields. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class MaterialsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void GoodsDrawTheirMaterialsAndAreImprovisedWithout()
        {
            var s = new Settlement { Fibre = 10, Hides = 2 };
            Assert.AreEqual(2.0, Goods.Materials(ref s, 2.0), 1e-12, "with enough in store, all made");
            Assert.AreEqual(5.0, s.Fibre, 1e-12, "fibre first: 2.5 a person's goods");
            Assert.AreEqual(2.0, s.Hides, 1e-12);
            Assert.AreEqual(3.0, Goods.Materials(ref s, 3.0), 1e-12, "fibre short: hides make up the rest");
            Assert.AreEqual(0.0, s.Fibre, 1e-12);
            Assert.AreEqual(0.75, s.Hides, 1e-12, "a hide counts as two");
            var empty = new Settlement();
            Assert.AreEqual(Goods.Improvised, Goods.Materials(ref empty, 1.0), 1e-12, "nothing in store: improvised at its pace");
            var half = new Settlement { Fibre = 1.25 };
            Assert.AreEqual(0.5 + 0.5 * Goods.Improvised, Goods.Materials(ref half, 1.0), 1e-12, "half from the store, half improvised");
        }

        [Test]
        public void TheHuntAndTheSlaughterGiveHidesAndTheHarvestFibre()
        {
            var w = TestWorlds.Make(471);
            Coarse(w);
            int sid = w.Settlements.First();
            double hides0 = 0, fibre0 = 0, eaten0 = w.Metrics.BeastsEaten, game0 = w.Metrics.GameKg;
            bool first = true;
            double harvests = 0;
            for (int d = 0; d < SimTime.DaysPerYear + 2; d++)
            {
                // premise each day: everyone's goods plentiful, so nobody makes any - the store only fills
                for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                {
                    var hhs = w.HouseholdsOf(s2);
                    for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Gear = (float)(2.0 * w.Members(hhs[i]).Count);
                }
                if (first) { hides0 = w.Settlements.Get(sid).Hides; fibre0 = w.Settlements.Get(sid).Fibre; first = false; }
                double before = w.Settlements.Get(sid).LastHarvest;
                long hb = w.Settlements.Get(sid).Crop == CropStage.Harvested ? 1 : 0;
                w.AdvanceTo(w.Now + SimTime.Day);
                if (hb == 0 && w.Settlements.Get(sid).Crop == CropStage.Harvested) harvests += w.Settlements.Get(sid).FieldHa;
            }
            var st = w.Settlements.Get(sid);
            double eaten = w.Metrics.BeastsEaten - eaten0, game = w.Metrics.GameKg - game0;
            Console.WriteLine("a year with nothing made: hides " + hides0.ToString("0.0") + " -> " + st.Hides.ToString("0.0") + " (animals slaughtered in the world " + eaten +
                              ", game " + game.ToString("0") + " kg), fibre " + fibre0.ToString("0.0") + " -> " + st.Fibre.ToString("0.0") + " (harvested " + harvests.ToString("0.0") + " ha)");
            Assert.Greater(harvests, 0, "(a harvest came in)");
            Assert.Greater(st.Fibre, fibre0, "the harvest's flax");
            Assert.LessOrEqual(st.Fibre - fibre0, harvests * SettlementSystem.FibrePerHa + 1e-6, "no more than its fields give");
            Assert.Greater(st.Hides, hides0, "the hides of the hunt and the slaughter");
        }

        [Test]
        public void PotteryIsFoundOnlyNearClayAndTheArdOnlyWithAnOx()
        {
            for (ulong seed = 473; seed < 490; seed++)
            {
                var w = TestWorlds.Make(seed);
                int wet = 0, dry = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    if (w.Learning.Clay(w.Settlements.Get(sid).Cell)) { if (wet == 0) wet = sid; } else if (dry == 0) dry = sid;
                }
                if (wet == 0 || dry == 0) continue;
                // premise: nobody anywhere knows pots or the ard
                for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                {
                    var res = w.Residents(s2);
                    for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); q.KPottery = 0f; q.KArd = 0f; }
                    ref var s = ref w.Settlements.Get(s2); s.BestPottery = 0f; s.BestArd = 0f;
                }
                // a cook of each village grinds for ever (so many hours that any chance is a certainty)
                ref var dryCook = ref w.People.Get(w.Residents(dry)[0]);
                w.Learning.Practice(ref dryCook, Tech.Pottery, 1e12);
                Assert.Less(dryCook.KPottery, 0.1f, "no clay near: no pots, however long they cook");
                long mark = w.History.LastId;
                ref var wetCook = ref w.People.Get(w.Residents(wet)[0]);
                w.Learning.Practice(ref wetCook, Tech.Pottery, 1e12);
                Assert.AreEqual((float)Tech.FirstFound, wetCook.KPottery, 1e-6f, "clay near: a cook finds the pots");
                bool told = false;
                for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                    if (w.History.At(i).Kind == HistoryKind.TechniqueFound) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
                Assert.IsTrue(told, "and history tells it");
                // the ard: a field worker of a household with a grown ox, and of one without
                int withOx = 0, without = 0;
                for (int h = w.Households.First(); h != 0; h = w.Households.Next(h))
                {
                    if (w.Members(h).Count == 0) continue;
                    if (w.Stock.HasOx(h)) { if (withOx == 0) withOx = h; } else if (without == 0) without = h;
                }
                Assume.That(withOx != 0 && without != 0, "a household with an ox and one without");
                ref var a = ref w.People.Get(w.Members(without)[0]);
                w.Learning.Practice(ref a, Tech.Ard, 1e12);
                Assert.Less(a.KArd, 0.1f, "no ox: no ard");
                ref var b = ref w.People.Get(w.Members(withOx)[0]);
                if (w.Settlements.Get(b.Home).BestArd < 0.1f) { w.Learning.Practice(ref b, Tech.Ard, 1e12); Assert.AreEqual((float)Tech.FirstFound, b.KArd, 1e-6f, "an ox at home: the ard is found"); }
                return;
            }
            Assert.Inconclusive("no world among the seeds tried with villages both near clay and far from it");
        }

        [Test]
        public void PotsKeepTheGrainAndSaveWoodTheArdSpeedsTheFields()
        {
            Assert.AreEqual(1.0, Tech.PotteryKeeps(0.0), 1e-12);
            Assert.AreEqual(0.65, Tech.PotteryKeeps(0.6), 1e-12, "a potter's village loses ~a third less of its grain");
            Assert.AreEqual(0.8, Tech.PotteryFuel(1.0), 1e-12, "and burns a fifth less wood cooking");
            var p = new Person { KArd = 1f };
            Assert.AreEqual(1.8, Tech.ArdFactor(in p, true), 1e-6, "a ploughman with an ox");
            Assert.AreEqual(1.0, Tech.ArdFactor(in p, false), 1e-12, "the same without one");
        }
    }
}
