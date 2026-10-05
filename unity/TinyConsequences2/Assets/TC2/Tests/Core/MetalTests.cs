using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Metal (D2-064): copper is found only by a potter where its ore shows (a technique that needs a technique and the ground),
    /// a smith brings metal into the village's store from ore and charcoal, and metal tools quicken hands and outlast the goods
    /// they replace. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class MetalTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static World WorldWithOre(out int ore, out int none)
        {
            for (ulong seed = 481; seed < 520; seed++)
            {
                var w = TestWorlds.Make(seed);
                ore = 0; none = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    if (w.Learning.Ore(w.Settlements.Get(sid).Cell)) { if (ore == 0) ore = sid; } else if (none == 0) none = sid;
                }
                if (ore != 0) return w;
            }
            ore = 0; none = 0;
            return null;
        }

        static void Unknown(World w)
        {
            for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
            {
                var res = w.Residents(s2);
                for (int i = 0; i < res.Count; i++) w.People.Get(res[i]).KMetal = 0f;
                w.Settlements.Get(s2).BestMetal = 0f;
            }
        }

        [Test]
        public void MetalToolsQuickenHandsAndOutlastTheGoods()
        {
            Assert.AreEqual(0.5, Tech.MetalShare(5.0, 20), 1e-12, "half a toolkit a head of five kg among twenty");
            Assert.AreEqual(1.25, Tech.MetalPace(1.0), 1e-12);
            Assert.AreEqual(0.75, Tech.MetalWear(1.0), 1e-12);
            Assert.AreEqual(1.0, Tech.MetalPace(0.0), 1e-12, "no metal, no change");
        }

        [Test]
        public void CopperIsFoundOnlyByAPotterWhereItsOreShows()
        {
            var w = WorldWithOre(out int ore, out int none);
            Assume.That(w != null, "a world with copper ore near a village");
            Unknown(w);
            // a maker of the ore village who fires no pots: never, however long they work
            ref var a = ref w.People.Get(w.Residents(ore)[0]);
            a.KPottery = 0f;
            w.Learning.Practice(ref a, Tech.Metal, 1e12);
            Assert.Less(a.KMetal, 0.1f, "no furnace, no crucible: no copper");
            // a potter far from any ore: never
            if (none != 0)
            {
                ref var b = ref w.People.Get(w.Residents(none)[0]);
                b.KPottery = 0.8f;
                w.Learning.Practice(ref b, Tech.Metal, 1e12);
                Assert.Less(b.KMetal, 0.1f, "no ore: no copper");
            }
            // a potter where the ore shows: found
            ref var c = ref w.People.Get(w.Residents(ore)[1]);
            c.KPottery = 0.8f;
            long mark = w.History.LastId;
            w.Learning.Practice(ref c, Tech.Metal, 1e12);
            Assert.AreEqual((float)Tech.FirstFound, c.KMetal, 1e-6f, "a potter by the ore finds how to smelt it");
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.TechniqueFound) Console.WriteLine(HistoryText.Line(w, w.History.At(i)));
        }

        [Test]
        public void ASmithFillsTheVillagesStoreWithMetalFromOreAndCharcoal()
        {
            var w = WorldWithOre(out int ore, out _);
            Assume.That(w != null, "a world with copper ore near a village");
            Coarse(w);
            // premise: the ore village's grown people all smelt and work copper well; nobody's goods are worn (so making is
            // their slack's work only as the days wear them)
            var res = w.Residents(ore);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.AgeYears(w.Now) >= 16) { p.KMetal = 0.9f; p.KPottery = 0.8f; } }
            ref var s0 = ref w.Settlements.Get(ore);
            s0.Metal = 0; s0.Woodstore = 500;
            double kg0 = w.Metrics.MetalKg;
            double pace0 = Tech.MetalPace(Tech.MetalShare(0, s0.Population));
            w.AdvanceTo(w.Now + SimTime.Years(1));
            var s = w.Settlements.Get(ore);
            double share = Tech.MetalShare(s.Metal, s.Population);
            Console.WriteLine("a year of smiths in a village of " + s.Population + ": " + s.Metal.ToString("0.0") + " kg of metal tools (" + (100 * share).ToString("0") + " % of a toolkit a head), smelted in the world " + (w.Metrics.MetalKg - kg0).ToString("0.0") + " kg; the pace of hands " + pace0.ToString("0.00") + " -> " + Tech.MetalPace(share).ToString("0.00"));
            Assert.Greater(s.Metal, 0.0, "metal in the store");
            Assert.Greater(Tech.MetalPace(share), pace0, "and quicker hands for it");
            Assert.IsNull(TestWorlds.Audit(w));
        }
    }
}
