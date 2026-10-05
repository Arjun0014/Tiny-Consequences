using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Wildlife as abundance fields (06 §2, D2-035): mechanisms, not outcomes.</summary>
    [TestFixture]
    public class EcologyTests
    {
        static int Kind(PlanetState P, byte kind) { for (int s = 0; s < P.Species.Count; s++) if (P.Species[s].Kind == kind) return s; return -1; }
        static double Grass(PlanetState P) { double g = 0; for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) g += P.Grass[c] * P.AreaHa(c); return g; }

        /// <summary>The whole world (vegetation grows back between the animals' steps), coarse, for some years.</summary>
        static void Run(World w, double years)
        {
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            var b = Budget.Unlimited;
            w.AdvanceTo(w.Now + SimTime.Years(years), ref b);
        }

        [Test]
        public void TheWorldBeginsWithItsAnimalsSpreadToWhatTheLandHolds()
        {
            var w = TestWorlds.Make(101);
            var P = w.Planet;
            Assert.AreEqual(8, P.Species.Count, "eight wild species");
            for (int s = 0; s < P.Species.Count; s++)
            {
                Assert.Greater(Ecology.Total(P, s), 10.0, Ecology.KindName(P.Species[s].Kind) + " live somewhere");
                for (int c = 0; c < P.Cells; c++)
                    if (!P.IsLand(c)) Assert.AreEqual(0f, P.Abund[s * P.Cells + c], "no animals at sea");
            }
            // none where it is far too cold for them
            int aur = Kind(P, 2);
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c) && P.TMean[c] < P.Species[aur].ColdTol - 8) Assert.Less(P.Abund[aur * P.Cells + c], 0.05f, "no aurochs in the deep cold");
        }

        [Test]
        public void WolvesSlowTheDeersRecoveryAndDeerFeedTheWolves()
        {
            // after a crash (nine in ten deer gone) deer are far below what the land feeds: there the hunt matters
            var a = TestWorlds.Make(102); var b = TestWorlds.Make(102); var c2 = TestWorlds.Make(102);
            int wolf = Kind(a.Planet, 5), deer = Kind(a.Planet, 0);
            foreach (var w in new[] { a, b, c2 })
                for (int c = 0; c < w.Planet.Cells; c++) w.Planet.Abund[deer * w.Planet.Cells + c] *= 0.1f;
            // b: no wolves; c2: none of the wolves' prey (deer, aurochs, boar, ibex)
            for (int c = 0; c < b.Planet.Cells; c++) b.Planet.Abund[wolf * b.Planet.Cells + c] = 0f;
            foreach (byte prey in new byte[] { 0, 2, 3, 4 })
            {
                int s = Kind(c2.Planet, prey);
                for (int c = 0; c < c2.Planet.Cells; c++) c2.Planet.Abund[s * c2.Planet.Cells + c] = 0f;
            }
            Run(a, 6); Run(b, 6); Run(c2, 6);
            Assert.Greater(Ecology.Total(b.Planet, deer), Ecology.Total(a.Planet, deer) * 1.1, "without wolves the deer come back faster");
            Assert.Less(Ecology.Total(c2.Planet, wolf), Ecology.Total(a.Planet, wolf) * 0.3, "without their prey the wolves starve");
        }

        [Test]
        public void GrazersEatTheGrass()
        {
            var a = TestWorlds.Make(103); var b = TestWorlds.Make(103);
            var P = b.Planet;
            for (int i = 0; i < P.Abund.Length; i++) P.Abund[i] = 0f;   // b: no animals at all
            double g0 = Grass(a.Planet);
            Run(a, 3); Run(b, 3);
            Assert.Less(Grass(a.Planet), Grass(b.Planet), "grazed land holds less grass");
            Assert.Greater(g0, 0);
        }
    }
}
