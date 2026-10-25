using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Ruins reused (C13.4): a household seeking a lot may clear a fallen home's ruin and build on it - the ruin gone at once, the stone of its footings
    /// carried to the village's store, told; with `--oldruins` the ruin keeps its lot until it has gone into the land, as before. A living village rebuilds
    /// on its old plots. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class RuinsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void AHouseholdSeekingALotClearsAFallenHomesRuinAndItsStoneComesBack()
        {
            var w = TestWorlds.Make(623);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            int seeker = hl[0], last = hl.Count > 1 ? hl[1] : hl[0];
            try
            {
                // premise: the lot the village would give now is where a house fell in ten years ago, on stone footings (nobody's home, its ruin standing)
                Homes.OldRuins = true;
                Homes.FreeLot(w, sid, seeker, out float rx, out float ry);
                int id = w.Ids.Next(EntityKind.Building);
                long fell = w.Now - SimTime.Years(Homes.HouseFallYears * 2 + 10);   // (left that long ago: on footings a house stands twice as long)
                w.Buildings.Add(id, new Building { Id = id, Settlement = sid, X = rx, Y = ry, Kind = Construction.House, Household = 0, Last = last, Style = last, Built = fell - SimTime.Years(40), Left = fell, Cond = 1f, Done = 1, Mat = 1, Stone = (float)Stone.FootingLoads });
                w.BuildingsOf(sid).InsertSorted(id);
                Assert.IsTrue(Homes.ClearableRuin(in w.Buildings.Get(id), w.Now), "premise: a fallen house's ruin");
                // the old rule: the ruin keeps its lot - the household is given another
                Homes.FreeLot(w, sid, seeker, out float ox, out float oy);
                Assert.IsFalse(ox == rx && oy == ry, "the ruin kept its lot");
                Assert.Greater(Homes.RuinLeft(in w.Buildings.Get(id), w.Now), 0.0, "and stands");
                // ruins reused: the household takes the old plot, the ruin cleared, its footing stone in the store
                Homes.OldRuins = false;
                double stone0 = w.Settlements.Get(sid).Stone;
                Homes.FreeLot(w, sid, seeker, out float nx, out float ny);
                Assert.AreEqual(rx, nx, 1e-4f); Assert.AreEqual(ry, ny, 1e-4f);
                Assert.AreEqual(0.0, Homes.RuinLeft(in w.Buildings.Get(id), w.Now), 1e-12, "the ruin cleared");
                Assert.AreEqual(stone0 + Stone.FootingLoads * Homes.RuinStoneBack, w.Settlements.Get(sid).Stone, 1e-6, "its footing stone carried to the store");
                bool told = false;
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != HistoryKind.HomeCleared || e.ArgA != 5) continue;
                    told = true;
                    Assert.AreEqual(EntityRef.Household(last), e.Subject); Assert.AreEqual(EntityRef.Household(seeker), e.Other);
                    StringAssert.StartsWith("The ruins of the house of", HistoryText.Describe(w, in e));
                    StringAssert.Contains("the stone of its footings carried to the store", HistoryText.Describe(w, in e));
                }
                Assert.IsTrue(told, "told");
            }
            finally { Homes.OldRuins = false; }
        }

        [Test]
        public void LivingVillagesRebuildOnTheirOldPlots()
        {
            // twin worlds, ruins reused and not: over 90 years the villages that live on clear ruins for new homes and keep fewer standing
            int Ruins(World w)
            {
                int n = 0;
                for (int b = w.Buildings.First(); b != 0; b = w.Buildings.Next(b))
                {
                    ref var bb = ref w.Buildings.Get(b);
                    if (w.Settlements.Exists(bb.Settlement) && w.Settlements.Get(bb.Settlement).Inhabited && Homes.ClearableRuin(in bb, w.Now)) n++;
                }
                return n;
            }
            World Run(bool old)
            {
                Homes.OldRuins = old;
                try { var w = TestWorlds.Make(624); Coarse(w); w.AdvanceTo(w.Now + SimTime.Years(90)); return w; }
                finally { Homes.OldRuins = false; }
            }
            var w0 = Run(true); var w1 = Run(false);
            int cleared = 0;
            for (int i = 0; i < w1.History.Count; i++) { var e = w1.History.At(i); if (e.Kind == HistoryKind.HomeCleared && e.ArgA == 5) cleared++; }
            int r0 = Ruins(w0), r1 = Ruins(w1);
            Console.WriteLine("ruins standing in living villages at year 90: kept " + r0 + ", reused " + r1 + "; cleared for new homes " + cleared);
            Assert.Greater(cleared, 0, "ruins cleared for new homes");
            Assert.Less(r1, r0, "fewer ruins stand in living villages");
        }
    }
}
