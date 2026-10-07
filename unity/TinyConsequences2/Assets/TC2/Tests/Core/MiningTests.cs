using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Planet;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Ore from the ground (C5.1, D2-100): copper lies where the geology lays it (crags near a volcanic arc), in finite bodies; a village
    /// knows it only where its people saw it; a village that can smelt and is short of metal opens a mine at the body and digs, every load
    /// carried home; smelting takes ore - none in store, no metal; a body counted down by what is dug gives out, its mine is left and the
    /// village looks again. Both resolutions. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class MiningTests
    {
        /// <summary>Premise: the village's people can smelt (one works copper well) and its tools have no metal.</summary>
        static void Smiths(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.AgeYears(w.Now) >= 20) { p.KMetal = 0.8f; p.KPottery = 0.8f; break; } }
            s.BestMetal = 0.8f; s.Metal = 0; s.Wall = 0; s.Project = 0;
            s.OreWant = (float)Mining.Want(w, sid);
        }

        [Test]
        public void CopperLiesOnlyWhereTheGroundLaysItInFiniteBodies()
        {
            var w = TestWorlds.Make(1301);
            var P = w.Planet;
            int crags = 0, copper = 0;
            for (int c = 0; c < P.Grid.CellCount; c++)
            {
                if (!Mining.CopperAt(w, c)) continue;
                copper++;
                Assert.IsTrue(Stone.Outcrop(P, c), "on rock showing");
                double near = double.MaxValue;
                for (int i = 0; i < P.Vents.Count; i++) near = Math.Min(near, Vec3d.ArcDistance(P.Vents[i].At, P.Grid.Center[c], PlanetState.GeoRadius));
                Assert.LessOrEqual(near, Mining.ArcReach, "near a volcanic arc");
                double kg = Mining.OreOf(w, c);
                Assert.That(kg, Is.InRange(Mining.OreMin, Mining.OreMax));
                Assert.AreEqual(kg, Mining.OreOf(TestWorlds.Make(1301), c), "the same body whoever finds it");
            }
            for (int c = 0; c < P.Grid.CellCount; c++)
            {
                if (!Stone.Outcrop(P, c) || !Shore.Dry(P, P.Grid.Center[c])) continue;
                for (int i = 0; i < P.Vents.Count; i++) if (Vec3d.ArcDistance(P.Vents[i].At, P.Grid.Center[c], PlanetState.GeoRadius) <= Mining.ArcReach) { crags++; break; }
            }
            Console.WriteLine(copper + " copper bodies among " + crags + " crags near the arcs (" + P.Vents.Count + " volcanoes)");
            if (crags >= 40) Assert.That(copper / (double)crags, Is.InRange(Mining.CopperShare * 0.5, Mining.CopperShare * 1.6), "some of the crags near an arc");
        }

        [Test]
        public void AVillageKnowsCopperOnlyWhereItsPeopleSawIt()
        {
            var w = TestWorlds.WithCopper(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            ref var s = ref w.Settlements.Get(sid);
            int cell = s.OreCell - 1;
            int di = w.Deposits.IndexAt(cell, Mining.Copper);
            Assert.GreaterOrEqual(di, 0, "the body is kept once seen");
            var d = w.Deposits.Get(di);
            Assert.AreEqual(Mining.OreOf(w, cell), d.Ore + d.Taken, 1e-6, "as much as the ground laid");
            // forget, and see it again: the first sight is history, with who saw it
            s.OreCell = 0; s.OreDist = 0;
            long mark = w.History.LastId;
            int pid = w.Residents(sid)[0];
            Mining.Saw(w, sid, pid, cell);
            Assert.AreEqual(cell + 1, w.Settlements.Get(sid).OreCell);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.OreFound) continue;
                told = true; Assert.AreEqual(pid, e.Other.Id, "who saw it"); Console.WriteLine(HistoryText.Line(w, e));
            }
            Assert.IsTrue(told);
            Assert.AreEqual(di, w.Deposits.IndexAt(cell, Mining.Copper), "one body, however often it is seen");
            // another village seeing the same body shares it
            int other = w.Settlements.Next(sid) != 0 ? w.Settlements.Next(sid) : w.Settlements.First();
            int n0 = w.Deposits.Count;
            ref var o = ref w.Settlements.Get(other); o.OreCell = 0;
            Mining.Saw(w, other, 0, cell);
            Assert.AreEqual(n0, w.Deposits.Count, "no second body");
            if (w.Settlements.Get(other).OreCell != 0) Assert.AreEqual(cell + 1, w.Settlements.Get(other).OreCell);
        }

        [Test]
        public void AVillageShortOfMetalOpensAMineAtTheOreAndCarriesItHome()
        {
            var w = TestWorlds.WithCopper(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            Smiths(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            Assert.Greater(s.OreWant, 0f, "ore wanted for tools short of metal");
            // nobody who can smelt: none wanted
            s.BestMetal = 0.1f;
            Assert.AreEqual(0.0, Mining.Want(w, sid), "no smelter, no ore wanted");
            s.BestMetal = 0.8f;
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid, "a work begun");
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.Mine, b.Kind);
            Assert.AreEqual(w.Settlements.Get(sid).OreEvent, b.Cause, "because they saw the copper");
            s.SphereToLocal(w.Planet.Grid.Center[s.OreCell - 1], w.Planet.Radius, out double mx, out double my);
            Assert.AreEqual(mx, b.X, 0.01); Assert.AreEqual(my, b.Y, 0.01);
            Structures.Raised(w, sid, bid, 1000, 10, 0);
            Assert.AreEqual(bid, Mining.Mine(w, sid), "the mine stands");
            // dig: what comes home is what an hour digs less the walk with every load; the body loses exactly that
            int di = w.Deposits.IndexAt(s.OreCell - 1, Mining.Copper);
            double left0 = w.Deposits.Get(di).Ore;
            var p = w.People.Get(w.Residents(sid)[0]);
            double r0 = Mining.DigRate(in p, 1.0), rate = Mining.HourRate(w, sid, r0, false);
            if (s.OreDist > 1f) Assert.Less(rate, r0, "the walk with every load"); else Assert.AreEqual(r0, rate, 1e-9, "at the face: no walk");
            double kg = Mining.Dig(w, sid, bid, 3.0, r0, false);
            Assert.AreEqual(Math.Min(3.0 * rate, left0), kg, 1e-9);
            Assert.AreEqual(left0 - kg, w.Deposits.Get(di).Ore, 1e-6, "taken from the body");
            Assert.AreEqual(kg, w.Settlements.Get(sid).Ore, 1e-9, "into the village's store");
            Console.WriteLine("the mine " + w.Settlements.Get(sid).OreDist.ToString("0") + " m out: " + rate.ToString("0.0") + " kg an hour home of " + r0.ToString("0.0") + " dug; the body holds " + left0.ToString("0") + " kg");
        }

        [Test]
        public void NoOreInStoreNoCopper()
        {
            var w = TestWorlds.WithCopper(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            Smiths(w, sid);
            Structures.Place(w, sid, Structures.Smithy, out float x, out float y);
            int smithy = Structures.Standing(w, sid, Structures.Smithy, x, y);
            ref var s = ref w.Settlements.Get(sid);
            s.Ore = 0; s.Charcoal = Charcoal.StoreMost; s.FoeWorth = 0; s.WatchEvent = 0;
            int smith = 0; var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).KMetal >= 0.8f) smith = res[i];
            Assert.IsFalse(Forge.Use(w, in w.Settlements.Get(sid)), "no ore dug: nothing to smelt");
            Assert.AreEqual(0.0, Forge.Work(w, ref w.People.Get(smith), sid, smithy, 4.0), "no hours spent");
            Assert.AreEqual(0.0, w.Settlements.Get(sid).Metal, "and no metal from nothing");
            s.Ore = 100;
            double used = Forge.Work(w, ref w.People.Get(smith), sid, smithy, 4.0);
            var s2 = w.Settlements.Get(sid);
            Assert.Greater(used, 0.0);
            Assert.AreEqual(100.0 - s2.Metal * Mining.OrePerKg, s2.Ore, 1e-9, "five kilos of ore a kilo of copper");
        }

        [Test]
        public void ABodyGivesOutItsMineIsLeftAndTheVillageLooksAgain()
        {
            var w = TestWorlds.WithCopper(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            Smiths(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            int cell = s.OreCell - 1;
            Structures.Place(w, sid, Structures.Mine, out float x, out float y);
            int mine = Structures.Standing(w, sid, Structures.Mine, x, y);
            int di = w.Deposits.IndexAt(cell, Mining.Copper);
            w.Deposits.At(di).Ore = 30.0;   // premise: the last of it
            long mark = w.History.LastId;
            double kg = Mining.Dig(w, sid, mine, 20.0, Mining.DigPerHour, false);
            Assert.AreEqual(30.0, kg, 1e-9, "no more than it held");
            var d = w.Deposits.Get(di);
            Assert.AreEqual(0.0, d.Ore); Assert.AreNotEqual(0L, d.GaveOut);
            Assert.AreEqual(0, w.Settlements.Get(sid).OreCell, "forgotten");
            Assert.AreEqual(0, w.Settlements.Get(sid).Mine, "the mine left");
            Assert.AreNotEqual(0L, w.Buildings.Get(mine).Left);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.OreGaveOut) continue;
                told = true; Assert.AreEqual(w.Settlements.Get(sid).OreEvent, e.Cause1, "the copper they found, dug out"); Console.WriteLine(HistoryText.Line(w, e));
            }
            Assert.IsTrue(told);
            Mining.Saw(w, sid, 0, cell);
            Assert.AreNotEqual(cell + 1, w.Settlements.Get(sid).OreCell, "a spent body is not known again");
        }

        [Test]
        public void AVillageDigsOreAtItsMineAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.WithCopper(out int sid);
                Assume.That(w != null, "a village with copper within reach");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Days(30));   // (past the first month's roofs: building takes every free hour in a new world)
                Smiths(w, sid);
                double dug0 = w.Metrics.OreDug;
                if (Mining.Mine(w, sid) == 0) { Structures.Place(w, sid, Structures.Mine, out float x, out float y); Structures.Standing(w, sid, Structures.Mine, x, y); }
                ref var s = ref w.Settlements.Get(sid); s.Granary = 30 * s.Population; s.Ore = 0;
                w.AdvanceTo(w.Now + SimTime.Days(6));
                var s3 = w.Settlements.Get(sid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": ore dug " + (w.Metrics.OreDug - dug0).ToString("0") + " kg in " + w.Metrics.MineHours.ToString("0.0") + " h at " + s3.OreDist.ToString("0") + " m; store " + s3.Ore.ToString("0") + " (wants " + s3.OreWant.ToString("0") + "), metal " + s3.Metal.ToString("0.0") + " kg");
                Assert.Greater(w.Metrics.OreDug - dug0, 20, (coarse ? "COARSE" : "lived") + ": ore was dug and carried home");
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
