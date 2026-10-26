using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>(session 27, the view's clouds) The day's air read for the clouds is the weather's own draw: rain fell only where that draw lay under the
    /// chance, and reading it changes nothing in the world.</summary>
    [TestFixture]
    public class DayAirTests
    {
        [Test]
        public void TheDaysAirAgreesWithTheRainThatFell()
        {
            var w = TestWorlds.Make(27);
            var P = w.Planet;
            w.AdvanceTo(w.Now + 9 * SimTime.Day + 6 * SimTime.Hour);
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            var blocks = new double[w.PlanetSys.WeatherBlocks];
            var draw = new float[P.Cells]; var chance = new float[P.Cells];
            ulong before = w.StateHash();
            w.PlanetSys.DayAir(w, day, blocks, draw, chance);
            Assert.AreEqual(before, w.StateHash(), "reading the day's air writes nothing");
            int wet = 0, dryChecked = 0, rainedWhereTheDrawForbade = 0, sureButDry = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (NearAStorm(P, c)) continue;   // (storms bring their own rain)
                // the weather rains where 0.7 x the smooth draw + 0.3 x a cell's own draw lies under the chance
                double floor = 0.7 * draw[c], ceil = 0.7 * draw[c] + 0.3;
                if (P.RainToday[c] > 0f) { wet++; if (floor >= chance[c]) rainedWhereTheDrawForbade++; }
                else { dryChecked++; if (ceil < chance[c]) sureButDry++; }
            }
            Assert.Greater(wet, 50, "some rain fell on day " + day);
            Assert.AreEqual(0, rainedWhereTheDrawForbade, "rain only where the day's draw allowed it");
            Assert.AreEqual(0, sureButDry, "rain wherever the day's draw made it sure");
            // the chance is the climate's: wetter where the air is moister
            double moistC = 0, dryC = 0; int nm = 0, nd = 0;
            for (int c = 0; c < P.Cells; c++) { if (P.Moist[c] > 0.7f) { moistC += chance[c]; nm++; } else if (P.Moist[c] < 0.2f) { dryC += chance[c]; nd++; } }
            if (nm > 0 && nd > 0) Assert.Greater(moistC / nm, dryC / nd, "moist lands are likelier to rain");
        }

        [Test]
        public void TheDaysAirIsTheSameAfterASaveAndLoad()
        {
            var w = TestWorlds.Make(28);
            w.AdvanceTo(w.Now + 3 * SimTime.Day);
            long day = SimTime.FloorDiv(w.Now, SimTime.Day);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            var b1 = new double[w.PlanetSys.WeatherBlocks]; var b2 = new double[w2.PlanetSys.WeatherBlocks];
            var d1 = new float[w.Planet.Cells]; var c1 = new float[w.Planet.Cells];
            var d2 = new float[w2.Planet.Cells]; var c2 = new float[w2.Planet.Cells];
            w.PlanetSys.DayAir(w, day, b1, d1, c1);
            w2.PlanetSys.DayAir(w2, day, b2, d2, c2);
            CollectionAssert.AreEqual(d1, d2, "the same draw");
            CollectionAssert.AreEqual(c1, c2, "the same chance");
        }

        static bool NearAStorm(PlanetState P, int c)
        {
            for (int k = 0; k < P.Storms.Count; k++)
                if (Vec3d.ArcDistance(P.Grid.Center[c], P.Storms[k].At, P.Radius) < P.Storms[k].Radius * 3.0) return true;
            return false;
        }
    }
}
