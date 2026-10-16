using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The muck (C3.7c, D2-153): the herds' night dung piles in the yards; carted out after the harvest and through the sowing it reaches the
    /// fields and gives the soil back what the herds' dung gave before; uncarted it gives nothing and leaches; saves keep it. Mechanisms.
    /// </summary>
    [TestFixture]
    public class MuckTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void TheNightsDungPilesInTheYardsAndIsCartedOutBeforeThePloughing()
        {
            // (the muck alone: the herds kept off the fields, whose day dung would be spread there with no carting - C3.7d, StubbleTests)
            Stubble.Off = true;
            try { NightsDung(); }
            finally { Stubble.Off = false; }
        }

        static void NightsDung()
        {
            var w = TestWorlds.Make(4611);
            Coarse(w);
            int sid = w.Settlements.First();
            Assume.That(w.Stock.Grazers(sid) > 0, "a village with grazing herds");
            double m0 = w.Settlements.Get(sid).Muck, g0 = w.Settlements.Get(sid).Grazed;
            // the crop standing (growing, then ripe): no carting - the heaps grow by the night's share of the grass the herds ate
            for (int d = 0; d < 60 && w.Settlements.Get(sid).Crop != CropStage.Growing; d++) w.AdvanceTo(w.Now + SimTime.Day);
            var s0 = w.Settlements.Get(sid);
            System.Console.WriteLine("muck test: day " + SimTime.FloorDiv(w.Now, SimTime.Day) % SimTime.DaysPerYear + " crop " + s0.Crop + " muck " + s0.Muck.ToString("0") + " grazers " + w.Stock.Grazers(sid));
            double mA = s0.Muck, gA = s0.Grazed;
            w.AdvanceTo(w.Now + SimTime.Days(6));
            var s1 = w.Settlements.Get(sid);
            System.Console.WriteLine("muck test: 6 days on, crop " + s1.Crop + " muck " + s1.Muck.ToString("0"));
            Assume.That(s1.Crop == CropStage.Growing || s1.Crop == CropStage.Ripe, "the crop still standing (no carting till the harvest)");
            Assert.Greater(s1.Muck, mA, "the yards' heaps grow");
            double grass = s1.Grazed - gA;
            Assert.That(s1.Muck - mA, Is.InRange(0.7 * grass * Muck.PerDm * Muck.NightShare, 1.0 * grass * Muck.PerDm + 1e-6), "the night's share of what they ate, as muck (less a little leached)");
            Assert.AreEqual(0.0, s1.Spread, 1e-9, "none carted while the crop stands");
            // after the harvest: carted out and spread
            for (int d = 0; d < 60 && w.Settlements.Get(sid).Crop != CropStage.Harvested; d++) w.AdvanceTo(w.Now + SimTime.Day);
            double mH = w.Settlements.Get(sid).Muck, h0 = w.Metrics.MuckHours, k0 = w.Metrics.MuckKg;
            System.Console.WriteLine("muck test: at the harvest, crop " + w.Settlements.Get(sid).Crop + " muck " + mH.ToString("0"));
            Assume.That(mH > 100.0);
            w.AdvanceTo(w.Now + SimTime.Days(6));
            var s2 = w.Settlements.Get(sid);
            Assert.Greater(s2.Spread, 0.0, "carted out onto the fields");
            Assert.Greater(w.Metrics.MuckHours, h0, "with hours of work");
            double t = (w.Metrics.MuckKg - k0) / 1000.0, h = w.Metrics.MuckHours - h0;
            Assert.That(h / t, Is.InRange(Muck.OxHPerT * 0.99, Muck.BackHPerT * 2.0 + 1e-9), "at a cart's pace or a back's");
            System.Console.WriteLine("muck: " + (mH / 1000).ToString("0.0") + " t in the yards at the harvest; " + t.ToString("0.0") + " t carted in 6 days in " + h.ToString("0") + " h (" + (h / t).ToString("0.00") + " h a tonne)");
        }

        [Test]
        public void OnlyMuckSpreadGivesBackAndAllOfItAsTheDungDidBefore()
        {
            const double fresh = 130;
            var a = new Settlement { ArableHa = 10, FieldHa = 8, Tired = 0.5, Grazed = 20000 };
            var b = a; b.Spread = a.Grazed * Muck.PerDm * Muck.NightShare;   // (all the yards' muck carted out)
            var c = a; c.Spread = 0;                                          // (none carted)
            var d = a;
            SettlementSystem.SoilYear(ref b, 800, fresh);
            SettlementSystem.SoilYear(ref c, 800, fresh);
            Muck.Off = true;
            try { SettlementSystem.SoilYear(ref d, 800, fresh); }
            finally { Muck.Off = false; }
            Assert.AreEqual(d.Tired, b.Tired, 1e-12, "all of it carted: the land gets back what the dung gave before C3.7c");
            Assert.Greater(c.Tired, b.Tired, "none carted: less back");
            Assert.AreEqual(0.0, b.Spread, "the season's spreading counted once");
        }

        [Test]
        public void UncartedMuckLeachesAndSavesKeepIt()
        {
            var s = new Settlement { Muck = 10000 };
            Muck.Age(ref s, SimTime.DaysPerYear);
            Assert.AreEqual(10000 * DMath.Exp(-Muck.Leach), s.Muck, 10000 * 0.001, "a year waiting: about a quarter gone (leached and rotted down at Leach a year)");
            var w = TestWorlds.Make(4612);
            int sid = w.Settlements.First();
            ref var sv = ref w.Settlements.Get(sid); sv.Muck = 1234.5; sv.Spread = 678.9;
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(1234.5, w2.Settlements.Get(sid).Muck, 0.0); Assert.AreEqual(678.9, w2.Settlements.Get(sid).Spread, 0.0);
        }
    }
}
