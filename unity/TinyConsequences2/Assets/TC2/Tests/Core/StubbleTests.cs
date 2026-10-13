using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The herds on the stubble and the fallow (C3.7d, D2-155): after the harvest the village's reaped fields are its herds' until the sowing ploughs
    /// them in, the herds' day ground is a field, and what they eat there drops its day's dung on the fields; the rested arable is grazed all year,
    /// its cells' grass falling by the fallow's part of them; `--nostubble` keeps the herds off the fields; saves keep the stubble. Mechanisms.
    /// </summary>
    [TestFixture]
    public class StubbleTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void ToStage(World w, int sid, CropStage stage)
        {
            for (int d = 0; d < 60 && w.Settlements.Get(sid).Crop != stage; d++) w.AdvanceTo(w.Now + SimTime.Day);
        }

        [Test]
        public void TheHerdsGrazeTheStubbleAndDungTheFieldsUntilTheSowing()
        {
            var w = TestWorlds.Make(4611);
            Coarse(w);
            int sid = w.Settlements.First();
            Assume.That(w.Stock.Grazers(sid) > 0, "a village with grazing herds");
            Stubble.Tally = new double[3];
            try
            {
                ToStage(w, sid, CropStage.Harvested);
                var s0 = w.Settlements.Get(sid);
                Assume.That(s0.Crop == CropStage.Harvested);
                Assert.That(s0.Stubble, Is.InRange(0.8 * s0.FieldHa * Stubble.PerHa, s0.FieldHa * Stubble.PerHa + 1e-6), "the reaped fields' stubble is the herds'");
                long today = SimTime.FloorDiv(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
                int ground = w.Stock.GroundOf(sid, today);
                Assert.GreaterOrEqual(ground, 0);
                Assert.AreEqual(sid, w.Planet.FieldOwner[ground], "the herds' ground of the day is one of the village's own fields");
                double eat0 = Stubble.Tally[0], fal0 = Stubble.Tally[1], dung0 = Stubble.Tally[2];
                w.AdvanceTo(w.Now + SimTime.Days(3));
                var s1 = w.Settlements.Get(sid);
                double eaten = Stubble.Tally[0] - eat0, onFallow = Stubble.Tally[1] - fal0, dung = Stubble.Tally[2] - dung0;
                Assert.Greater(eaten, 0.0, "they graze the stubble");
                Assert.Less(s1.Stubble, s0.Stubble, "and eat it down");
                Assert.AreEqual((eaten + onFallow) * Muck.PerDm * (1.0 - Muck.NightShare), dung, 1e-6 + 1e-9 * dung, "their day's dung dropped on the fields");
                System.Console.WriteLine("stubble: " + (s0.Stubble / 1000).ToString("0.0") + " t on " + s0.FieldHa.ToString("0.0") + " ha at the harvest; " + (eaten / 1000).ToString("0.00") + " t eaten in 3 days, " + (dung / 1000).ToString("0.00") + " t of dung on the fields");
                ToStage(w, sid, CropStage.Sown);
                Assert.AreEqual(0.0, w.Settlements.Get(sid).Stubble, 0.0, "the sowing ploughs the stubble in");
            }
            finally { Stubble.Tally = null; }
        }

        [Test]
        public void TheFallowIsGrazedByItsPartOfTheFieldCells()
        {
            var w = TestWorlds.Make(4611);
            Coarse(w);
            int sid = w.Settlements.First();
            Assume.That(w.Stock.Grazers(sid) > 0);
            ToStage(w, sid, CropStage.Growing);
            // premise: the village rests a third of its arable this year (a young village's land is fresh: it rests none)
            ref var s = ref w.Settlements.Get(sid);
            s.ArableHa = s.FieldHa * 1.5;
            var fc = w.FieldCellsOf(sid);
            double g0 = 0, a = 0;
            for (int i = 0; i < fc.Count; i++) { g0 += w.Planet.Grass[fc[i]] * w.Planet.AreaHa(fc[i]); a += w.Planet.AreaHa(fc[i]); }
            Stubble.Tally = new double[3];
            try
            {
                w.AdvanceTo(w.Now + SimTime.Day);
                Assert.Greater(Stubble.Tally[1], 0.0, "the herds graze the fallow");
                Assert.AreEqual(0.0, Stubble.Tally[0], 0.0, "no stubble while the crop grows");
                double g1 = 0;
                for (int i = 0; i < fc.Count; i++) g1 += w.Planet.Grass[fc[i]] * w.Planet.AreaHa(fc[i]);
                // the kg the fallow lost is what was eaten off it (a day's regrowth aside): the field cells' grass falls by the fallow's part only
                double lostKg = (g0 - g1) * Ecology.GrassMax;
                System.Console.WriteLine("fallow: " + (Stubble.Tally[1]).ToString("0") + " kg eaten in a day; the field cells' grass " + (g0 / a).ToString("0.000") + " -> " + (g1 / a).ToString("0.000") + " (" + lostKg.ToString("0") + " kg)");
                Assert.Less(lostKg, 2.0 * Stubble.Tally[1] + 1.0, "never more grass gone than eaten (the whole cells were not grazed at the fallow's rate)");
            }
            finally { Stubble.Tally = null; }
        }

        [Test]
        public void NoStubbleKeepsTheHerdsOffTheFieldsAndSavesKeepIt()
        {
            var w = TestWorlds.Make(4611);
            Coarse(w);
            int sid = w.Settlements.First();
            Stubble.Off = true;
            try
            {
                ToStage(w, sid, CropStage.Harvested);
                Assert.AreEqual(0.0, w.Settlements.Get(sid).Stubble, 0.0, "--nostubble: no stubble kept for the herds");
                long today = SimTime.FloorDiv(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
                int ground = w.Stock.GroundOf(sid, today);
                if (ground >= 0) Assert.AreEqual(0, w.Planet.FieldOwner[ground], "their ground off the fields");
            }
            finally { Stubble.Off = false; }
            ref var sv = ref w.Settlements.Get(sid); sv.Stubble = 4321.5;
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(4321.5, w2.Settlements.Get(sid).Stubble, 0.0, "saves keep the stubble");
        }
    }
}
