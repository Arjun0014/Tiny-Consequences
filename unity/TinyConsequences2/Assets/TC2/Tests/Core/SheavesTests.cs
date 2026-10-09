using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The first sheaves (the hungry gap, D2-132): the grain of a ripe crop comes into the village's store - and its households' bins by their
    /// mouths - as it is reaped, a day's reaping at the next day's start; the rest and the year's debts on the harvest's day; sheaves already in
    /// the store are not burnt with the stubble. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class SheavesTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void Until(World w, int sid, CropStage stage)
        {
            for (int d = 0; d < 72; d++)
            {
                if (w.Settlements.Get(sid).Crop == stage) return;
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            Assert.Fail("the crop never reached " + stage);
        }

        /// <summary>Lives the world to the first morning of a ripe stage of the village's crop (not the one the world began in).</summary>
        static void ToRipe(World w, int sid)
        {
            if (w.Settlements.Get(sid).Crop == CropStage.Ripe) Until(w, sid, CropStage.Harvested);
            Until(w, sid, CropStage.Ripe);
        }

        static double Bins(World w, int sid)
        {
            double h = w.Settlements.Get(sid).Common;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) h += w.Households.Get(hl[i]).Grain;
            return h;
        }

        [Test]
        public void TheSheavesComeInAsTheyAreReaped()
        {
            var w = TestWorlds.Make(4211);
            Coarse(w);
            int sid = w.Settlements.First();
            ToRipe(w, sid);
            var s = w.Settlements.Get(sid);
            Assert.Greater(s.CropYield, 0.0, "the grown crop's yield is known as it ripens");
            Assert.AreEqual(s.FieldHa * w.Config.FieldYieldPerHa * DMath.Clamp01(s.CropGrowth) * s.Sown * (1.0 + s.TilthGain), s.CropYield, 1e-9);
            Assert.AreEqual(0.0, s.Reaped);
            double lastReaped = 0, bins0 = Bins(w, sid);
            bool cameIn = false;
            for (int d = 0; d < 8 && w.Settlements.Get(sid).Crop == CropStage.Ripe; d++)
            {
                w.AdvanceTo(w.Now + SimTime.Day);
                s = w.Settlements.Get(sid);
                if (s.Crop != CropStage.Ripe) break;
                double share = DMath.Clamp01(s.CropWork / System.Math.Max(1.0, s.CropWorkNeed));
                Assert.GreaterOrEqual(s.Reaped, lastReaped, "sheaves only come in");
                Assert.LessOrEqual(s.Reaped, s.CropYield * (1.0 - s.CropBurnt) * share + 1e-6, "no more than the reaped share of what the fields bear");
                if (s.Reaped > 0) cameIn = true;
                lastReaped = s.Reaped;
            }
            Assert.IsTrue(cameIn, "grain came into the store before the harvest's day");
            Until(w, sid, CropStage.Harvested);
            s = w.Settlements.Get(sid);
            Assert.AreEqual(System.Math.Max(lastReaped, s.CropYield * (1.0 - s.CropBurnt) * s.Harvested), s.LastHarvest, 1e-6, "the harvest is all of it, the sheaves with the rest");
            Assert.AreEqual(s.LastHarvest, s.Reaped, 1e-6);
            Assert.IsTrue(w.History.TryGet(w.Settlements.Get(sid).HarvestEvent, out var e) && e.Kind == HistoryKind.Harvest, "the harvest is recorded on its day");
            Assert.AreEqual((float)s.LastHarvest, e.Magnitude, 1e-3f);
            Assert.Greater(Bins(w, sid), bins0, "the households' bins took their shares");
        }

        [Test]
        public void ShareOfASheafSettlesNoDebtTheHarvestDoes()
        {
            var w = TestWorlds.Make(916);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 2);
            int a = hl[0], b = 0;
            var kin = Holdings.KinOf(w, sid, a);
            for (int i = 0; i < hl.Count; i++) if (hl[i] != a && !kin.Contains(hl[i])) { b = hl[i]; break; }
            Assume.That(b != 0);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; }
            ref var s = ref w.Settlements.Get(sid); s.Common = 0;
            w.Households.Get(b).Grain = 1e4; s.Granary = Bins(w, sid) - 25;
            w.Holdings.Draw(sid, a, 25, 20);   // a owes b 25
            Assume.That(w.Holdings.Owes(a, out _) > 24.9);
            // a sheaf's share puts a far beyond a year's needs: it pays nothing yet
            double fu = 1e5;
            ref var s1 = ref w.Settlements.Get(sid); s1.Granary += fu;
            w.Holdings.Harvest(sid, fu, false);
            Assert.AreEqual(25, w.Holdings.Owes(a, out _), 1e-9, "the sheaves before the harvest's day settle no debt");
            ref var s2 = ref w.Settlements.Get(sid); s2.Granary += 1;
            w.Holdings.Harvest(sid, 1);
            Assert.AreEqual(0, w.Holdings.Owes(a, out _), 1e-9, "the harvest's day settles it");
        }

        [Test]
        public void AHungryVillageEatsFromItsFirstSheaves()
        {
            double[] store = new double[2];
            float[] fed = new float[2];
            foreach (bool off in new[] { false, true })
            {
                SettlementSystem.SheavesOff = off;
                try
                {
                    var w = TestWorlds.Make(4212);
                    Coarse(w);
                    int sid = w.Settlements.First();
                    ToRipe(w, sid);
                    // premise: its store and larders are empty as the crop ripens (the hungry gap)
                    ref var s = ref w.Settlements.Get(sid);
                    s.Granary = 0; s.Common = 0;
                    var hl = w.HouseholdsOf(sid);
                    for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Grain = 0; h.Larder = 0; }
                    w.AdvanceTo(w.Now + SimTime.Days(4));
                    var s4 = w.Settlements.Get(sid);
                    Assume.That(s4.Crop == CropStage.Ripe, "still the reaping");
                    store[off ? 1 : 0] = s4.Granary;
                    float n = 0; int k = 0;
                    var res = w.Residents(sid);
                    for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive) { n += p.Nutrition; k++; } }
                    fed[off ? 1 : 0] = k > 0 ? n / k : 0f;
                }
                finally { SettlementSystem.SheavesOff = false; }
            }
            Assert.Greater(store[0], 1.0, "with the sheaves its store holds grain before the harvest's day");
            Assert.Less(store[1], store[0], "--nosheaves: the store waits for the harvest's day");
            Assert.Greater(fed[0], fed[1], "and its people eat better from their first sheaves (" + fed[0] + " against " + fed[1] + ")");
        }

        [Test]
        public void SheavesInTheStoreAreNotBurntWithTheStubble()
        {
            var w = TestWorlds.Make(4211);
            Coarse(w);
            int sid = w.Settlements.First();
            ToRipe(w, sid);
            for (int d = 0; d < 6; d++) w.AdvanceTo(w.Now + SimTime.Day);
            var s = w.Settlements.Get(sid);
            Assume.That(s.Crop == CropStage.Ripe && s.Reaped > 0.5 * s.CropYield, "most of it reaped and in");
            double inStore = s.Reaped;
            // a fire through the fields after the reaping
            ref var sf = ref w.Settlements.Get(sid); sf.CropBurnt = 0.9;
            Until(w, sid, CropStage.Harvested);
            var s2 = w.Settlements.Get(sid);
            Assert.GreaterOrEqual(s2.LastHarvest, inStore - 1e-9, "the sheaves in the store were not burnt");
            Assert.AreEqual(System.Math.Max(inStore, s2.CropYield * 0.1 * s2.Harvested), s2.LastHarvest, 1e-6, "only the standing crop burnt");
        }

        [Test]
        public void ASaveInTheReapingContinuesTheSame()
        {
            var w = TestWorlds.Make(4211);
            Coarse(w);
            int sid = w.Settlements.First();
            ToRipe(w, sid);
            w.AdvanceTo(w.Now + SimTime.Days(2) + SimTime.Hours(7));
            Assume.That(w.Settlements.Get(sid).Reaped > 0);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "loaded, the same");
            w.AdvanceTo(w.Now + SimTime.Days(12)); w2.AdvanceTo(w2.Now + SimTime.Days(12));
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and lives on the same through the harvest");
        }
    }
}
