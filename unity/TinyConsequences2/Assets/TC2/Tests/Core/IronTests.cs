using System;
using System.Collections.Generic;
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
    /// Iron (C5.2b, D2-102): bog ore lies where water stands on flat ground and forms again once dug out; the bloomery is found only at a
    /// smithy by a copper smith whose village has seen bog ore; iron is smelted from bog ore and charcoal at a smithy; iron tools quicken
    /// hands more than copper and replace it; iron arms strike harder; both resolutions dig bog ore. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class IronTests
    {
        /// <summary>A small world in which a village's people have seen bog ore (its founders, or someone who went to a bed within reach).</summary>
        static World WithBog(out int sid)
        {
            for (ulong seed = 601; seed < 700; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Settlements.Get(sid).FeCell != 0) return w;
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    var near = new List<int>();
                    w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, 0.9 * Mining.Reach, near);
                    near.Sort();
                    for (int i = 0; i < near.Count; i++)
                        if (Mining.BogAt(w, near[i])) { Mining.Saw(w, sid, 0, near[i]); if (w.Settlements.Get(sid).FeCell != 0) return w; }
                }
            }
            sid = 0;
            return null;
        }

        /// <summary>Premise: one grown person of the village smelts copper well and knows the bloomery.</summary>
        static int IronSmith(World w, int sid)
        {
            var res = w.Residents(sid);
            int smith = 0;
            for (int i = 0; i < res.Count && smith == 0; i++) { var p = w.People.Get(res[i]); double a = p.AgeYears(w.Now); if (a >= 20 && a < 50) smith = res[i]; }
            ref var q = ref w.People.Get(smith); q.KMetal = 0.9f; q.KIron = 0.8f; q.KPottery = 0.8f;
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown |= (byte)((1 << Tech.Metal) | (1 << Tech.Iron)); s.BestMetal = 0.9f; s.BestIron = 0.8f;
            return smith;
        }

        [Test]
        public void BogOreLiesWhereWaterStandsOnFlatGround()
        {
            var w = WithBog(out _);
            Assume.That(w != null, "a world with bog ore");
            var P = w.Planet;
            int beds = 0, flats = 0;
            for (int c = 0; c < P.Grid.CellCount; c++)
            {
                bool wet = P.IsLand(c) && P.Ice[c] < 0.5f && (P.LakeDepth[c] > 0.3f || P.RiverFlow[c] >= Mining.BrookFlow || P.Moist[c] >= Mining.FenMoist) && Shore.Dry(P, P.Grid.Center[c]);
                bool flat = true;
                for (int k = 0; k < 4 && wet; k++) { int nb = P.Grid.Nbr4[c * 4 + k]; if (P.IsLand(nb) && Math.Abs((double)P.Elev[nb] - P.Elev[c]) >= Mining.BogFlat) flat = false; }
                if (wet && flat) flats++;
                if (!Mining.BogAt(w, c)) continue;
                beds++;
                Assert.IsTrue(wet && flat, "water standing on flat ground");
                Assert.That(Mining.OreOf(w, c, Mining.Iron), Is.InRange(Mining.BogMin, Mining.BogMax));
            }
            Console.WriteLine(beds + " beds of bog ore among " + flats + " wet flats");
            Assert.Greater(beds, 0);
            if (flats >= 40) Assert.That(beds / (double)flats, Is.InRange(Mining.BogShare * 0.6, Mining.BogShare * 1.4));
        }

        [Test]
        public void BogOreIsKnownBySeeingAndFormsAgainOnceDugOut()
        {
            var w = WithBog(out int sid);
            Assume.That(w != null, "a village with bog ore within reach");
            ref var s = ref w.Settlements.Get(sid);
            int cell = s.FeCell - 1;
            s.FeCell = 0;
            long mark = w.History.LastId;
            Mining.Saw(w, sid, w.Residents(sid)[0], cell);
            Assert.AreEqual(cell + 1, w.Settlements.Get(sid).FeCell);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.OreFound && w.History.At(i).ArgA == Mining.Iron) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // dug out: forgotten, not known again while it is spent ...
            int pits = Structures.Standing(w, sid, Structures.BogPits, 0, 0);
            int di = w.Deposits.IndexAt(cell, Mining.Iron);
            w.Deposits.At(di).Ore = 20.0;
            Mining.Dig(w, sid, pits, 10.0, Mining.DigPerHour, false, double.MaxValue, Mining.Iron);
            Assert.AreEqual(0, w.Settlements.Get(sid).FeCell, "dug out and forgotten");
            Assert.AreEqual(0, w.Settlements.Get(sid).FeMine, "its pits left");
            Mining.Saw(w, sid, 0, cell);
            Assert.AreNotEqual(cell + 1, w.Settlements.Get(sid).FeCell, "not while it is spent");
            // ... and formed again forty years on (premise: dug out long ago)
            w.Deposits.At(di).GaveOut = w.Now - SimTime.Years(41);
            Mining.Saw(w, sid, 0, cell);
            Assert.AreEqual(cell + 1, w.Settlements.Get(sid).FeCell, "formed again");
            Assert.AreEqual(Mining.OreOf(w, cell, Mining.Iron), w.Deposits.Get(di).Ore, 1e-9);
        }

        [Test]
        public void TheBloomeryIsFoundOnlyByACopperSmithWhoHasSeenBogOre()
        {
            var w = WithBog(out int sid);
            Assume.That(w != null, "a village with bog ore within reach");
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) w.People.Get(res[i]).KIron = 0f;
            w.Settlements.Get(sid).BestIron = 0f;
            ref var a = ref w.People.Get(res[0]);
            a.KMetal = 0.2f;
            w.Learning.Practice(ref a, Tech.Iron, 1e12);
            Assert.Less(a.KIron, 0.1f, "no copper smith: no bloomery");
            ref var b = ref w.People.Get(res[1]);
            b.KMetal = 0.9f;
            int fe = w.Settlements.Get(sid).FeCell;
            w.Settlements.Get(sid).FeCell = 0;
            w.Learning.Practice(ref b, Tech.Iron, 1e12);
            Assert.Less(b.KIron, 0.1f, "no bog ore seen: no bloomery");
            w.Settlements.Get(sid).FeCell = fe;
            long mark = w.History.LastId;
            w.Learning.Practice(ref b, Tech.Iron, 1e12);
            Assert.AreEqual((float)Tech.FirstFound, b.KIron, 1e-6f, "a copper smith who has seen bog ore finds the bloomery");
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.TechniqueFound) Console.WriteLine(HistoryText.Line(w, w.History.At(i)));
        }

        [Test]
        public void IronIsSmeltedAtASmithyFromBogOreAndCharcoal()
        {
            var w = WithBog(out int sid);
            Assume.That(w != null, "a village with bog ore within reach");
            int smith = IronSmith(w, sid);
            Structures.Place(w, sid, Structures.Smithy, out float x, out float y);
            int smithy = Structures.Standing(w, sid, Structures.Smithy, x, y);
            ref var s = ref w.Settlements.Get(sid);
            s.FeOre = 800; s.Charcoal = 1500; s.Ore = 0; s.Iron = 0; s.Metal = 0; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0;
            Assert.IsTrue(Forge.Use(w, in w.Settlements.Get(sid)), "bog ore and charcoal, tools short of iron");
            double hand = Forge.Hand(in w.People.Get(smith));   // (practice quickens a smith's hand: C1.4b)
            double used = Forge.Work(w, ref w.People.Get(smith), sid, smithy, 10.0);
            var s2 = w.Settlements.Get(sid);
            double kg = Tech.IronPerHour(in w.People.Get(smith)) * Forge.Pace * hand * 10.0;
            Console.WriteLine("10 h at the bloomery: " + s2.Iron.ToString("0.00") + " kg of bar iron from " + (800 - s2.FeOre).ToString("0.0") + " kg of bog ore and " + (1500 - s2.Charcoal).ToString("0.0") + " kg of charcoal");
            Assert.AreEqual(10.0, used, 1e-9);
            Assert.AreEqual(kg, s2.Iron, 1e-9);
            Assert.AreEqual(800 - kg * Mining.FeOrePerKg, s2.FeOre, 1e-9, "eight kilos of ore a kilo");
            Assert.AreEqual(1500 - kg * Charcoal.PerKgIron, s2.Charcoal, 1e-9, "fifteen of charcoal");
            // no bog ore: no iron, however much charcoal
            ref var s3 = ref w.Settlements.Get(sid); s3.FeOre = 0; double iron0 = s3.Iron;
            Forge.Work(w, ref w.People.Get(smith), sid, smithy, 10.0);
            Assert.AreEqual(iron0, w.Settlements.Get(sid).Iron, 1e-12, "no ore, no iron");
        }

        [Test]
        public void IronToolsQuickenHandsMoreThanCopperAndReplaceIt()
        {
            var s = new Settlement { Population = 10 };
            Assert.AreEqual(1.0, Tech.ToolPace(in s), 1e-12);
            s.Metal = Tech.MetalPerHead * 10;
            Assert.AreEqual(1.25, Tech.ToolPace(in s), 1e-12, "copper");
            Assert.AreEqual(0.0, Tech.CopperWanted(in s), 1e-12);
            s.Metal = 0; s.Iron = Tech.IronPerHead * 10;
            Assert.AreEqual(1.4, Tech.ToolPace(in s), 1e-12, "iron");
            Assert.AreEqual(0.6, Tech.ToolWear(in s), 1e-12);
            Assert.AreEqual(0.0, Tech.CopperWanted(in s), 1e-12, "no copper wanted where iron serves");
            s.Iron = Tech.IronPerHead * 5;
            Assert.AreEqual(Tech.MetalPerHead * 10 * 0.5, Tech.CopperWanted(in s), 1e-12, "copper for those iron does not yet serve");
        }

        [Test]
        public void IronIsTradedToWhereItIsLackedAndItsSmithsMakeForTheNextBuyers()
        {
            var w = TestWorlds.Make(1402);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
            // premise: a has iron beyond its toolkits and little grain; b has no iron and grain to spare
            sa.Iron = Tech.IronPerHead * sa.Population * 1.5; sa.Granary = 5 * sa.Population; sa.Metal = 0;
            sb.Iron = 0; sb.Granary = 400 * sb.Population; sb.Metal = Tech.MetalPerHead * sb.Population;
            double gain = w.Trade.Plan(a, b, out byte give, out double qty, out byte take);
            Console.WriteLine("deal: " + give + " x" + qty.ToString("0.0") + " for " + take + ", gain " + gain.ToString("0.0"));
            Assert.Greater(gain, 0.0);
            Assert.AreEqual(Trade.Iron, give, "iron goes where it is lacked");
            double target0 = Forge.IronTarget(in w.Settlements.Get(a));
            int trader = w.Residents(a)[0];
            ref var p = ref w.People.Get(trader);
            w.Trade.Load(ref p, a, b);
            double carried = p.CargoQty;
            w.Trade.Exchange(ref p, b);
            Assert.AreEqual(carried, w.Settlements.Get(b).Iron, 1e-4, "into the buyers' store");
            Assert.AreEqual(carried, w.Settlements.Get(a).IronSold, 1e-4, "the sellers know what they sold");
            Assert.AreEqual(target0 + carried, Forge.IronTarget(in w.Settlements.Get(a)), 1e-4, "and their smiths make that much more");
        }

        [Test]
        public void IronArmsStrikeHarder()
        {
            var w = WithBog(out int sid);
            Assume.That(w != null, "a village with bog ore within reach");
            ref var s = ref w.Settlements.Get(sid);
            s.Arms = Forge.ArmsPerHead * s.Population; s.IronArms = 0; s.Metal = Tech.MetalPerHead * s.Population;
            int fighter = 0; var res = w.Residents(sid);
            for (int i = 0; i < res.Count && fighter == 0; i++) { var p = w.People.Get(res[i]); double a = p.AgeYears(w.Now); if (a >= 20 && a < 40) fighter = res[i]; }
            double copper = w.Conflict.Strength(in w.People.Get(fighter));
            s.IronArms = s.Arms;
            double iron = w.Conflict.Strength(in w.People.Get(fighter));
            Assert.AreEqual(copper * 1.15, iron, 1e-9, "iron arms for every fighter: 1.15 times");
        }

        static string Smiths(World w, int sid)
        {
            var res = w.Residents(sid); string o = "";
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.KMetal >= 0.5f) o += p.KMetal.ToString("0.0") + "/" + p.KIron.ToString("0.0") + " "; }
            return o;
        }

        [Test]
        public void AVillageDigsBogOreAtBothResolutions()
        {
            var made = new double[2]; var dug = new double[2];
            foreach (bool coarse in new[] { true, false })
            {
                var w = WithBog(out int sid);
                Assume.That(w != null, "a village with bog ore within reach");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Years(2));   // (a settled village: its homes raised, so its free hours are the slack's)
                IronSmith(w, sid);
                ref var s = ref w.Settlements.Get(sid);
                s.Iron = 0; s.FeOre = 0; s.Wall = 0; s.Granary = 30 * s.Population;
                if (s.FeCell == 0) { Assert.Inconclusive("the village has forgotten its bog ore"); }
                s.FeOreWant = (float)Mining.Want(w, sid, Mining.Iron);
                if (Mining.Mine(w, sid, Mining.Iron) == 0) { Structures.Place(w, sid, Structures.BogPits, out float x, out float y); Structures.Standing(w, sid, Structures.BogPits, x, y); }
                if (Charcoal.Site(w, sid) == 0 && Woods.Ensure(w, sid) >= 0) { Structures.Place(w, sid, Structures.CharHearth, out float hx, out float hy); Structures.Standing(w, sid, Structures.CharHearth, hx, hy); }
                if (Forge.Smithy(w, sid) == 0) { Structures.Place(w, sid, Structures.Smithy, out float sx, out float sy); Structures.Standing(w, sid, Structures.Smithy, sx, sy); }
                double kg0 = w.Metrics.BogOreDug, ch0 = w.Metrics.CharcoalKg, fe0 = w.Metrics.IronKg, sh0 = w.Metrics.SmithyHours;
                w.AdvanceTo(w.Now + SimTime.Years(1));   // (a year: a COARSE day gives the fields every hour in their season, a lived one interleaves)
                var s3 = w.Settlements.Get(sid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": bog ore " + (w.Metrics.BogOreDug - kg0).ToString("0") + " kg at " + s3.FeDist.ToString("0") + " m; store " + s3.FeOre.ToString("0") + " (wants " + s3.FeOreWant.ToString("0") + "), iron " + s3.Iron.ToString("0.0") + " kg, charcoal burnt " + (w.Metrics.CharcoalKg - ch0).ToString("0") + " kg, iron made " + (w.Metrics.IronKg - fe0).ToString("0.0") + " kg in " + (w.Metrics.SmithyHours - sh0).ToString("0") + " smithy hours; smiths (copper/iron) " + Smiths(w, sid));
                Assert.Greater(w.Metrics.BogOreDug - kg0, 20, (coarse ? "COARSE" : "lived") + ": bog ore dug and carried home");
                made[coarse ? 0 : 1] = w.Metrics.IronKg - fe0; dug[coarse ? 0 : 1] = w.Metrics.BogOreDug - kg0;
                Assert.IsNull(TestWorlds.Audit(w));
            }
            // a year: the resolutions dig and smelt alike (a COARSE smith's labour day at the forge: C5.2b)
            Assert.That(dug[0] / dug[1], Is.InRange(0.67, 1.5), "bog ore dug at par");
            Assert.That(made[0] / made[1], Is.InRange(0.67, 1.5), "iron made at par");
        }
    }
}
