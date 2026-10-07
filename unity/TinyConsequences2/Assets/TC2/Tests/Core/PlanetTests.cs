using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Planet mechanisms (15 §2): drainage, runoff routing, lakes that fill, spill and dry, rivers.</summary>
    [TestFixture]
    public class DrainageTests
    {
        [Test]
        public void EveryLandCellDrainsToTheSeaDownhillOfItsFilledLevel()
        {
            var w = TestWorlds.Make(11);
            var P = w.Planet; var d = w.Drainage;
            int land = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) { Assert.AreEqual(-1, d.Receiver[c], "sea cells have no receiver"); continue; }
                land++;
                Assert.GreaterOrEqual(d.Filled[c], P.Elev[c], "a filled level is never below the bed");
                int at = c, hops = 0;
                while (P.IsLand(at))
                {
                    int r = d.Receiver[at];
                    Assert.GreaterOrEqual(r, 0, "land cell " + at + " has no receiver");
                    Assert.LessOrEqual(d.Filled[r], d.Filled[at], "water never flows up: " + at + " -> " + r);
                    at = r;
                    Assert.Less(++hops, P.Cells, "a cycle in the drainage from " + c);
                }
            }
            Assert.AreEqual(land, d.StackCount, "the stack holds every land cell once");
        }

        [Test]
        public void RunoffIsConservedDownTheTree()
        {
            var w = TestWorlds.Make(12);
            var P = w.Planet; var d = w.Drainage;
            var runoff = new float[P.Cells];
            double total = 0;
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c)) { runoff[c] = 0.001f * (1 + c % 7); total += runoff[c] * P.GeoAreaHa(c) * 10000.0; }   // (the drainage keeps geo-areas: the same rivers at any size, D2-085)
            d.Accumulate(P, runoff);
            double outlets = 0;
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c) && !P.IsLand(d.Receiver[c])) outlets += d.Discharge[c];
            Assert.AreEqual(1.0, outlets / total, 1e-4, "what falls on the land leaves it at the river mouths");
        }

        [Test]
        public void WaterIsConservedThroughTheLakes()
        {
            // runoff = what reaches the sea + what the lakes gained + what they evaporated
            var w = TestWorlds.Make(16);
            var P = w.Planet; var d = w.Drainage;
            Assert.Greater(d.BasinCount, 0, "a noisy relief has depressions");
            var runoff = new float[P.Cells];
            for (int day = 0; day < 5; day++)
            {
                double fallen = 0, before = 0, after = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    before += P.LakeWater[c];
                    runoff[c] = P.IsLand(c) ? 0.0004f * ((c * 7 + day) % 11) : 0f;
                    if (P.IsLand(c)) fallen += runoff[c] * P.GeoAreaHa(c) * 10000.0;   // (geo-areas, D2-085)
                }
                double evaporated = d.Route(P, runoff);
                double toSea = 0;
                for (int c = 0; c < P.Cells; c++)
                {
                    after += P.LakeWater[c];
                    if (P.IsLand(c) && !P.IsLand(d.Receiver[c]) && d.BasinOf[c] < 0) toSea += d.Discharge[c];
                }
                Assert.AreEqual(1.0, (toSea + (after - before) + evaporated) / fallen, 1e-3, "day " + day + ": water is neither made nor lost");
            }
        }

        static int BiggestBasin(Drainage d)
        {
            int best = -1;
            for (int b = 0; b < d.BasinCount; b++) if (best < 0 || d.BasinCapacity[b] > d.BasinCapacity[best]) best = b;
            return best;
        }

        [Test]
        public void ABasinFillsThenSpillsWhatItCannotHold()
        {
            var w = TestWorlds.Make(17);
            var P = w.Planet; var d = w.Drainage;
            int b = BiggestBasin(d);
            Assert.GreaterOrEqual(b, 0);
            int deep = d.DeepestCell(b), exit = d.BasinExit[b];
            for (int c = 0; c < P.Cells; c++) { P.LakeWater[c] = 0; P.Temp[c] = -5f; } // a cold world: little evaporation
            d.SettleStored(P);
            var runoff = new float[P.Cells];
            for (int c = 0; c < P.Cells; c++) if (d.BasinOf[c] == b) runoff[c] = 0.05f; // a downpour on the basin only
            double prev = 0; bool spilled = false;
            for (int day = 0; day < 4000 && !spilled; day++)
            {
                d.Route(P, runoff);
                double v = P.LakeWater[deep];
                Assert.GreaterOrEqual(v, prev - 1e-3, "a basin fed more than it loses only fills");
                Assert.LessOrEqual(v, d.BasinCapacity[b] * 1.0001, "never holds more than its capacity");
                prev = v;
                if (d.Discharge[exit] > 0) spilled = true;
            }
            Assert.IsTrue(spilled, "a full basin spills into its exit");
            Assert.AreEqual(d.BasinCapacity[b], P.LakeWater[deep], d.BasinCapacity[b] * 1e-4, "it spills only when full");
            Assert.AreEqual(d.BasinSpill[b], d.BasinLevel[b], 0.05, "a full lake stands at its spill level");
            Assert.IsTrue(P.IsLake(deep), "a full basin is a lake");
        }

        [Test]
        public void ALakeWithoutInflowDriesByEvaporationAndNeverBelowEmpty()
        {
            var w = TestWorlds.Make(18);
            var P = w.Planet; var d = w.Drainage;
            int b = BiggestBasin(d);
            int deep = d.DeepestCell(b);
            for (int c = 0; c < P.Cells; c++) { P.LakeWater[c] = 0; P.Temp[c] = 25f; }
            P.LakeWater[deep] = (float)System.Math.Min(d.BasinCapacity[b] * 0.5, 60000.0);
            d.SettleStored(P);
            var dry = new float[P.Cells];
            double prev = P.LakeWater[deep];
            for (int day = 0; day < 20000 && P.LakeWater[deep] > 0; day++)
            {
                double wet = d.BasinWetHa[b];
                d.Route(P, dry);
                double v = P.LakeWater[deep];
                double expect = System.Math.Max(0, prev - Drainage.LakeEvaporation(25) * wet * 10000.0);
                Assert.AreEqual(expect, v, 1.0, "a day's loss is the evaporation from yesterday's wet area");
                Assert.GreaterOrEqual(v, 0.0, "never below empty");
                prev = v;
            }
            Assert.AreEqual(0.0, P.LakeWater[deep], 1e-6, "it dries out");
            Assert.IsFalse(P.IsLake(deep), "a dry basin is not a lake");
        }

        [Test]
        public void DrainageAreaGrowsDownstream()
        {
            var w = TestWorlds.Make(13);
            var P = w.Planet; var d = w.Drainage;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                int r = d.Receiver[c];
                if (P.IsLand(r)) Assert.Greater(d.AreaHa[r], d.AreaHa[c], "a receiver drains more than its tributary");
            }
        }

        [Test]
        public void ASoakedLandRaisesTheRiversDownstream()
        {
            var w = TestWorlds.Make(14);
            var P = w.Planet; var d = w.Drainage;
            int mouth = -1; float best = 0;
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c) && !P.IsLand(d.Receiver[c]) && d.BasinOf[c] < 0 && d.AreaHa[c] > best) { best = d.AreaHa[c]; mouth = c; }
            Assert.GreaterOrEqual(mouth, 0);
            float before = P.RiverFlow[mouth];
            for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) P.SoilWater[c] = 0.95f; // a soaked land sheds its excess
            var b = Budget.Unlimited;
            w.AdvanceTo(w.Now + 3 * SimTime.Day, ref b);
            Assert.Greater(P.RiverFlow[mouth], before, "a soaked basin raises the flow at its mouth");
        }

        [Test]
        public void StreamsEndOnlyInRiversLakesDepressionsOrTheSea()
        {
            var w = TestWorlds.Make(15);
            var P = w.Planet; var d = w.Drainage;
            int streams = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c) || !P.IsStream(c)) continue;
                streams++;
                int r = d.Receiver[c];
                if (!P.IsLand(r) || d.BasinOf[c] >= 0) continue;
                Assert.IsTrue(P.IsStream(r) || P.IsLake(r) || d.BasinOf[r] >= 0, "a stream does not vanish on open land: " + c + " -> " + r);
            }
            Assert.Greater(streams, 0, "a wet planet has streams");
        }

        [Test]
        public void LakesAndRiversSurviveSaveAndLoad()
        {
            var w = TestWorlds.Make(19);
            var b = Budget.Unlimited;
            w.AdvanceTo(w.Now + 6 * SimTime.Day, ref b);
            var bytes = TC2.Core.Persistence.SaveFile.Save(w, 0, TC2.Core.Persistence.OfflineMode.Paused, 0, 0, "");
            var w2 = TC2.Core.Persistence.SaveFile.Load(bytes, out _);
            for (int c = 0; c < w.Planet.Cells; c++)
                Assert.AreEqual(w.Planet.LakeDepth[c], w2.Planet.LakeDepth[c], "lake depth derived again on load at " + c);
            var b1 = Budget.Unlimited; var b2 = Budget.Unlimited;
            w.AdvanceTo(w.Now + 6 * SimTime.Day, ref b1);
            w2.AdvanceTo(w2.Now + 6 * SimTime.Day, ref b2);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a loaded world's waters go on the same");
        }
    }
}
