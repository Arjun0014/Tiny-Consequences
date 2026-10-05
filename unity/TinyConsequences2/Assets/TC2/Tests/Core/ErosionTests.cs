using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Rivers cut and carry (05 §3b, D2-031).</summary>
    [TestFixture]
    public class ErosionTests
    {
        [Test]
        public void RiversCutTheirValleysAndTheRockTheyCarrySettlesOrReachesTheSea()
        {
            var w = TestWorlds.Make(71);
            var P = w.Planet;
            var d = w.Drainage;
            int n = P.Cells;
            var before = (float[])P.Elev.Clone();
            bool[] river = new bool[n], land = new bool[n];
            for (int c = 0; c < n; c++) { land[c] = P.IsLand(c); river[c] = land[c] && P.RiverFlow[c] >= 1500f && d.BasinOf[c] < 0; }
            double eroded = 0, settled = 0, deep = 0;
            for (int i = 0; i < 100; i++)
            {
                d.Rebuild(P);
                Erosion.Step(w, Tectonics.StepYears);
                eroded += Erosion.Eroded; settled += Erosion.Settled; deep += Erosion.ToDeep;
            }
            double dropRiver = 0, dropSlope = 0; int nr = 0, ns = 0;
            for (int c = 0; c < n; c++)
            {
                if (!land[c]) continue;
                double drop = before[c] - P.Elev[c];
                if (river[c]) { dropRiver += drop; nr++; }
                else if (P.RiverFlow[c] < Erosion.MinFlow) { dropSlope += drop; ns++; }
            }
            Assert.Greater(nr, 10, "the world has rivers");
            System.Console.WriteLine("mean drop in 1,000 years: river cells " + (dropRiver / nr).ToString("0.0") + " m, hillslopes " + (dropSlope / ns).ToString("0.0") + " m");
            Assert.Greater(dropRiver / nr, 2.0, "river cells cut down metres in a thousand years");
            // (D2-043) every slope also creeps and washes toward the way the water goes: the land wears as the plates raise it
            Assert.Greater(dropSlope / ns, 0.5, "hillslopes wear down toward their rivers");
            Assert.Greater(eroded, 0);
            Assert.AreEqual(eroded, settled + deep, eroded * 1e-6, "every cubic metre cut settles somewhere or reaches the deep");
            // the sea at river mouths shallows: deltas grow
            int shallowed = 0;
            for (int c = 0; c < n; c++) if (!land[c] && P.Elev[c] > before[c] + 1f) shallowed++;
            Assert.Greater(shallowed, 0, "the shallows at river mouths fill");
        }
    }
}
