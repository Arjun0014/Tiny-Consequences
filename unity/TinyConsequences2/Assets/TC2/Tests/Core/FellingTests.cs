using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The woods remember their felling (W-FELL, `27` §6): the share of a cell's cover a village fells stands as stumps - a record the view draws -
    /// until the woods grow back over them and they rot; it never exceeds the open share, the sea takes it with the land, saves keep it.
    /// </summary>
    [TestFixture]
    public class FellingTests
    {
        [Test]
        public void FellingLeavesStumpsWhereTheWoodsWere()
        {
            var w = TestWorlds.Make(4411);
            var P = w.Planet;
            int sid = w.Settlements.First();
            int c = Woods.Ensure(w, sid);
            Assume.That(c >= 0, "a woodlot within reach");
            double f0 = P.Forest[c], s0 = P.Felled[c];
            double per = Woods.KgPerHa / Woods.LoadKg * P.AreaHa(c);
            double loads = Woods.Fell(w, sid, 0.25 * f0 * per);
            Assert.Greater(loads, 0.0);
            Assert.AreEqual(f0 - loads / per, P.Forest[c], 1e-5, "the cover felled");
            Assert.AreEqual(s0 + loads / per, P.Felled[c], 1e-5, "its stumps: the share felled");
            Assert.LessOrEqual(P.Felled[c] + P.Forest[c], 1.0 + 1e-6, "never more stumps than open ground");
        }

        [Test]
        public void TheWoodsGrowBackOverTheirStumpsAndTheStumpsRot()
        {
            // the woods regrow where they were cut first
            Assert.AreEqual(0.20, PlanetSystems.Regrown(0.30, 0.10, 0.50, 0.0), 1e-9, "a tenth regrown: a tenth fewer stumps");
            // stumps rot over the years where nothing regrows
            double rot = PlanetSystems.Regrown(0.30, 0.0, 0.50, 1.0);
            Assert.AreEqual(0.30 * (1.0 - 1.0 / PlanetSystems.StumpYears), rot, 1e-9, "a year's rot");
            Assert.AreEqual(0.0, PlanetSystems.Regrown(0.30, 0.40, 0.90, 0.0), "grown back over them: none left");
            Assert.AreEqual(0.10, PlanetSystems.Regrown(0.30, 0.0, 0.90, 0.0), 1e-9, "never more than the open share");
            Assert.AreEqual(0.0, PlanetSystems.Regrown(0.0015, 0.0, 0.50, 0.0), "a few stumps less than a tree's worth are gone");
            // and in a living world: a felled woodlot left alone loses its stumps over the decades
            var w = TestWorlds.Make(4412);
            var P = w.Planet;
            int sid = w.Settlements.First();
            int c = Woods.Ensure(w, sid);
            Assume.That(c >= 0);
            double per = Woods.KgPerHa / Woods.LoadKg * P.AreaHa(c);
            Woods.Fell(w, sid, 0.3 * P.Forest[c] * per);
            float felled = P.Felled[c];
            Assume.That(felled > 0.05f);
            double f1 = P.Forest[c];
            for (int k = 0; k < 40; k++) w.PlanetSys.GrowVegetation(w, 0.5);   // (20 years of growth, nobody cutting there)
            Assert.Less(P.Felled[c], felled, "fewer stumps after twenty years");
            Assert.LessOrEqual(P.Felled[c] + P.Forest[c], 1.0 + 1e-6);
            System.Console.WriteLine("woodlot cell " + c + ": cover " + f1.ToString("0.00") + " -> " + P.Forest[c].ToString("0.00") + ", stumps " + felled.ToString("0.00") + " -> " + P.Felled[c].ToString("0.00") + " in 20 years");
        }

        [Test]
        public void StumpsKeepThroughASave()
        {
            var w = TestWorlds.Make(4413);
            int sid = w.Settlements.First();
            int c = Woods.Ensure(w, sid);
            Assume.That(c >= 0);
            Woods.Fell(w, sid, 200);
            Assume.That(w.Planet.Felled[c] > 0f);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.Planet.Felled[c], w2.Planet.Felled[c], 0f);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "the same world");
        }
    }
}
