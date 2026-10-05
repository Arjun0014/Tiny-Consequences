using NUnit.Framework;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>The sea shapes its coasts (05 §3b, D2-034).</summary>
    [TestFixture]
    public class CoastTests
    {
        static double Volume(PlanetState P) { double v = 0; for (int c = 0; c < P.Cells; c++) v += P.Elev[c] * P.AreaHa(c); return v; }

        [Test]
        public void WavesWearALowIsletAwayAndDriftFillsANarrowInlet()
        {
            var w = TestWorlds.Make(91);
            var P = w.Planet;
            var g = P.Grid;
            int islet = -1, inlet = -1;
            for (int c = 0; c < P.Cells && (islet < 0 || inlet < 0); c++)
            {
                bool allSea = true, allLand = true;
                for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (P.IsLand(nb)) allSea = false; else allLand = false; }
                if (islet < 0 && allSea && !P.IsLand(c) && P.Elev[c] < P.SeaLevel - 50) islet = c;
                if (inlet < 0 && allLand && P.IsLand(c) && P.Elev[c] > P.SeaLevel + 30 && P.LakeDepth[c] <= 0f) inlet = c;
            }
            Assert.GreaterOrEqual(islet, 0); Assert.GreaterOrEqual(inlet, 0);
            P.Elev[islet] = (float)(P.SeaLevel + 12);
            P.Elev[inlet] = (float)(P.SeaLevel - 12);
            double before = Volume(P);
            for (int i = 0; i < 12; i++) Coast.Step(w, Tectonics.StepYears);
            Assert.IsFalse(P.IsLand(islet), "the islet is gone in 120 years");
            Assert.IsTrue(P.IsLand(inlet), "the inlet is filled in 120 years");
            Assert.AreEqual(before, Volume(P), System.Math.Abs(before) * 1e-6 + 1.0, "no rock made or lost");
        }
    }
}
