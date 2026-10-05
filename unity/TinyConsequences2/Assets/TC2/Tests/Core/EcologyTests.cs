using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Wildlife as abundance fields (06 §2, §5, D2-035, D2-039): mechanisms, not outcomes.</summary>
    [TestFixture]
    public class EcologyTests
    {
        static int Kind(PlanetState P, byte kind) { for (int s = 0; s < P.Species.Count; s++) if (P.Species[s].Kind == kind) return s; return -1; }
        static double MeanCond(PlanetState P, int s) { double t = 0, nn = 0; for (int c = 0; c < P.Cells; c++) if (P.Abund[s * P.Cells + c] > 0.05f) { t += P.Cond[s * P.Cells + c]; nn++; } return nn > 0 ? t / nn : 0; }
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
            Assert.AreEqual(13, P.Species.Count, "eight wild species on land, five in the sea");
            for (int s = 0; s < P.Species.Count; s++)
            {
                Assert.Greater(Ecology.Total(P, s), 10.0 * Ecology.Density, Ecology.KindName(P.Species[s].Kind) + " live somewhere");
                bool sea = P.Species[s].Habitat == 1;
                for (int c = 0; c < P.Cells; c++)
                    if (P.IsLand(c) == sea) Assert.AreEqual(0f, P.Abund[s * P.Cells + c], sea ? "no fish on land" : "no land animals at sea");
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
            // (kinds that died out have left the fields: look each up again by its kind)
            Assert.Greater(Ecology.Total(b.Planet, Kind(b.Planet, 0)), Ecology.Total(a.Planet, Kind(a.Planet, 0)) * 1.1, "without wolves the deer come back faster");
            int wa = Kind(a.Planet, 5), wc = Kind(c2.Planet, 5);
            Assert.GreaterOrEqual(wa, 0);
            Assert.Less(wc < 0 ? 0.0 : Ecology.Total(c2.Planet, wc), Ecology.Total(a.Planet, wa) * 0.3, "without their prey the wolves starve");
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

        [Test]
        public void ThePlanktonIsRichestWhereRiversAndShallowsFeedItAndDarkInThePolarNight()
        {
            var w = TestWorlds.Make(104);
            var P = w.Planet;
            var mid = Ecology.SeaFoodNow(w, 0.375);   // early summer in the north, winter in the south
            double mouths = 0, nMouths = 0, open = 0, nOpen = 0, polarNight = 0, nPolar = 0;
            var d = w.Drainage;
            var mouth = new bool[P.Cells];
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                int r = d.Receiver[c];
                if (r >= 0 && !P.IsLand(r) && P.RiverFlow[c] > Ecology.RiverRich * 0.05) mouth[r] = true;
            }
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c)) continue;
                double lat = P.Lat[c] * 180 / System.Math.PI;
                if (lat < -70) { polarNight += mid[c]; nPolar++; }
                if (System.Math.Abs(lat) > 50) continue;   // compare like with like: the mid latitudes
                if (mouth[c]) { mouths += mid[c]; nMouths++; }
                else if (P.SeaLevel - P.Elev[c] > 800) { open += mid[c]; nOpen++; }
            }
            Assume.That(nMouths > 0 && nOpen > 0);
            Assert.Greater(mouths / nMouths, 1.5 * open / nOpen, "a river's mouth feeds the sea far more than the deep ocean");
            if (nPolar > 0) Assert.Less(polarNight / nPolar, 0.1 * open / nOpen + 1e-6, "no plankton grows in the polar night");
        }

        [Test]
        public void ColdFishLiveInColdSeasWarmFishInWarmSeasAndSealsByTheShore()
        {
            var w = TestWorlds.Make(105);
            var P = w.Planet;
            int herring = Kind(P, 8), sardine = Kind(P, 9), seal = Kind(P, 12);
            double hT = 0, hN = 0, sT = 0, sN = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c)) continue;
                double a = P.Abund[herring * P.Cells + c], b = P.Abund[sardine * P.Cells + c];
                hT += a * P.TMean[c]; hN += a; sT += b * P.TMean[c]; sN += b;
                if (P.Abund[seal * P.Cells + c] > 0.01f)
                {
                    bool shore = false;   // a seal is never more than a few cells from land
                    for (int q = 0; q < 4 && !shore; q++)
                    {
                        int nb = P.Grid.Nbr4[c * 4 + q];
                        if (P.IsLand(nb)) shore = true;
                        for (int q2 = 0; q2 < 4 && !shore; q2++) { int nb2 = P.Grid.Nbr4[nb * 4 + q2]; if (P.IsLand(nb2)) shore = true;
                            for (int q3 = 0; q3 < 4 && !shore; q3++) { int nb3 = P.Grid.Nbr4[nb2 * 4 + q3]; if (P.IsLand(nb3)) shore = true;
                                for (int q4 = 0; q4 < 4 && !shore; q4++) if (P.IsLand(P.Grid.Nbr4[nb3 * 4 + q4])) shore = true; } }
                    }
                    Assert.IsTrue(shore, "seals keep to the coasts");
                }
            }
            Assert.Greater(hN, 1000); Assert.Greater(sN, 1000);
            Assert.Greater(sT / sN, hT / hN + 8, "sardines swim in seas far warmer than the herring's");
        }

        [Test]
        public void WithoutTheirHuntersTheShoalsGrowUntilTheyGoHungry()
        {
            var a = TestWorlds.Make(106); var b = TestWorlds.Make(106);
            int cod = Kind(b.Planet, 10), seal = Kind(b.Planet, 12), herring = Kind(a.Planet, 8);
            for (int c = 0; c < b.Planet.Cells; c++) { b.Planet.Abund[cod * b.Planet.Cells + c] = 0f; b.Planet.Abund[seal * b.Planet.Cells + c] = 0f; }
            // the shoals swing with the plankton every few years: compare the years after the cod have come back from the spin-up
            double na = 0, nb = 0, ca = 0, cb = 0;
            for (int y = 0; y < 10; y++)
            {
                Run(a, 1); Run(b, 1);
                if (y < 5) continue;
                na += Ecology.Total(a.Planet, herring); nb += Ecology.Total(b.Planet, herring);
                ca += MeanCond(a.Planet, herring); cb += MeanCond(b.Planet, herring);
            }
            Assert.Greater(nb, na * 1.08, "without their hunters the shoals grow");
            Assert.Less(cb, ca, "until the plankton cannot feed them all");
        }
    }
}
