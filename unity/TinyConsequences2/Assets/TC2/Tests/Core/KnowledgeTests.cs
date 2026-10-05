using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Techniques carried by people (D2-062; F4's done-when): a technique spreads by contact - it travels with whoever moves and
    /// takes root where they go - and a complex one is lost in a small isolated group while a large one keeps it (Henrich 2004).
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class KnowledgeTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void TheBestOfManyCopiesKeepsAComplexTechniqueTheBestOfAFewLosesIt()
        {
            // the law alone: each generation copies its best, each copy short by the loss and scattered by a keyed draw
            var law = Tech.Laws[Tech.Quern];
            double few = Generations(law, 6, 6), many = Generations(law, 300, 6);
            Console.WriteLine("the rotary quern after six generations from 0.8: six learners " + few.ToString("0.00") + ", three hundred " + many.ToString("0.00"));
            Assert.Less(few, Tech.Forgotten, "the best of a few copies falls short each generation");
            Assert.GreaterOrEqual(many, 0.75, "the best of many keeps up with the master");
            var nets = Tech.Laws[Tech.Nets];
            Assert.Greater(Generations(nets, 6, 6), Tech.Competent, "a simple technique holds even among a few");
        }

        static double Generations(Tech.Law law, int learners, int gens)
        {
            double m = 0.8;
            for (int g = 0; g < gens; g++)
            {
                double best = 0;
                for (int i = 0; i < learners; i++) best = Math.Max(best, DMath.Clamp01(m - law.Alpha + law.Beta * Tech.Scatter(99, g * 100000 + i, Tech.Quern)));
                m = best;
            }
            return m;
        }

        [Test]
        public void ATechniqueTravelsWithABrideAndTakesRootWhereSheGoes()
        {
            for (ulong seed = 451; seed < 460; seed++)
            {
                var w = TestWorlds.Make(seed);
                int from = 0, to = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    bool knows = (w.Settlements.Get(sid).TechKnown & (1 << Tech.Loom)) != 0;
                    if (knows && from == 0) from = sid; else if (!knows && to == 0) to = sid;
                }
                if (from == 0 || to == 0) continue;
                // premise: a weaver of one village marries a man of a village that does not weave on the loom
                int bride = 0, groom = 0;
                var a = w.Residents(from); var b = w.Residents(to);
                for (int i = 0; i < a.Count; i++) { var p = w.People.Get(a[i]); if (p.IsFemale && p.Partner == 0 && p.AgeYears(w.Now) >= 16 && p.KLoom >= Tech.Competent) { bride = a[i]; break; } }
                for (int i = 0; i < b.Count; i++) { var p = w.People.Get(b[i]); if (!p.IsFemale && p.Partner == 0 && p.AgeYears(w.Now) >= 16) { groom = b[i]; break; } }
                if (bride == 0 || groom == 0) continue;
                Coarse(w);
                long mark = w.History.LastId;
                w.SettlementSys.Wed(to, bride, groom);
                Assert.AreEqual(to, w.People.Get(bride).Home, "she lives in her husband's village");
                w.AdvanceTo(w.Now + SimTime.Years(12));
                // its own people weave now: those who were born there or lived there before she came
                int weavers = 0;
                var res = w.Residents(to);
                for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Id != bride && p.KLoom >= Tech.Competent) weavers++; }
                bool told = false;
                for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind == HistoryKind.TechniqueKnown && e.Settlement == to && e.ArgA == Tech.Loom) { told = true; Console.WriteLine(HistoryText.Line(w, e)); }
                }
                Console.WriteLine("seed " + seed + ": the loom came to a village with a bride; twelve years on " + weavers + " of its people weave on it");
                Assert.IsTrue(told, "the village came to know it");
                Assert.Greater(weavers, 0, "its own people learned it from her (and from each other)");
                Assert.IsNull(TestWorlds.Audit(w));
                return;
            }
            Assert.Inconclusive("no pair of villages with and without the loom in the seeds tried");
        }

        [Test]
        public void AComplexTechniqueIsLostInASmallIsolatedVillageAndKeptInALargeOne()
        {
            double small = OneMaster(14, 150, out bool lostSmall, out int popSmall);
            double large = OneMaster(150, 60, out bool lostLarge, out int popLarge);
            Console.WriteLine("one master of the rotary quern (0.6): an isolated village of " + popSmall + " after 150 years keeps " + small.ToString("0.00") +
                              (lostSmall ? " (lost)" : "") + "; a village of " + popLarge + " after 60 years " + large.ToString("0.00") + (lostLarge ? " (lost)" : ""));
            Assert.Less(small, Tech.Forgotten, "the small village's copies fall short generation after generation");
            Assert.IsTrue(lostSmall, "and its loss is history");
            Assert.GreaterOrEqual(large, Tech.Competent, "the large village keeps it");
        }

        /// <summary>A world of one village whose only master of the quern is one grown person at 0.6 (the premise); its best after the years.</summary>
        static double OneMaster(int founders, double years, out bool lost, out int pop)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 461, SurfaceN = 48, Settlements = 1, FoundersPerSettlement = founders });
            int sid = w.Settlements.First();
            var res = w.Residents(sid);
            int master = 0; double youngest = 999;
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                p.KQuern = 0f;
                double age = p.AgeYears(w.Now);
                if (age >= 18 && age < youngest) { youngest = age; master = res[i]; }   // (the youngest grown one: the master lives long)
            }
            w.People.Get(master).KQuern = 0.6f;
            Coarse(w);
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Years(years));
            lost = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.TechniqueLost && e.Settlement == sid && e.ArgA == Tech.Quern) lost = true;
            }
            pop = w.Settlements.Get(sid).Population;
            return w.Settlements.Get(sid).BestQuern;
        }
    }
}
