using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Places by their ore (C5.3, D2-103): a village whose smiths want ore it knows none of has its scouts value land near ore they see
    /// (half as much again); the founders of a new village look over the land round their site and know the ore there from the first day.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class OreSitesTests
    {
        /// <summary>A world, a village and a place its scout could stand where bog ore and a good site within reach of it are both in sight.</summary>
        static World Scene(out int sid, out int at, ulong seed0)
        {
            var near = new List<int>(); var sight = new List<int>(); var scratch = new List<int>();
            for (ulong seed = seed0; seed < seed0 + 60; seed++)
            {
                var w = TestWorlds.Make(seed);
                var P = w.Planet;
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, Migration.ScoutMax, near);
                    near.Sort();
                    for (int i = 0; i < near.Count; i++)
                    {
                        at = near[i];
                        if (Vec3d.ArcDistance(P.Grid.Center[at], w.Settlements.Get(sid).Anchor, P.Radius) < Migration.ScoutMin) continue;
                        w.Hazards.CellsWithin(w, at, Sites.LookRadius, sight);
                        int bog = -1;
                        for (int j = 0; j < sight.Count && bog < 0; j++) if (Mining.BogAt(w, sight[j])) bog = sight[j];
                        if (bog < 0) continue;
                        for (int j = 0; j < sight.Count; j++)
                            if (Sites.Quality(w, sight[j], scratch) >= Sites.MinQuality && Vec3d.ArcDistance(P.Grid.Center[sight[j]], P.Grid.Center[bog], P.Radius) <= Mining.SitePull) return w;
                    }
                }
            }
            sid = 0; at = -1;
            return null;
        }

        static void Wants(World w, int sid, bool iron)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.FeCell = 0; s.FeOre = 0; s.Iron = 0; s.Lore = KnownSites.Empty;
            s.BestIron = iron ? 0.8f : 0f;
        }

        [Test]
        public void ScoutsOfAVillageThatLacksOreValueLandByIt()
        {
            var w = Scene(out int sid, out int at, 701);
            Assume.That(w != null, "a scout's look over bog ore and good land");
            ulong seed = w.Seed;
            // without want of iron: the land as it is
            Wants(w, sid, false);
            w.Migration.Look(sid, at, 0);
            int c0 = w.Settlements.Get(sid).Lore.Best(out float q0);
            // the same world, its village's smiths wanting iron it knows no bog ore for
            var w2 = TestWorlds.Make(seed);
            Wants(w2, sid, true);
            Assert.IsTrue(Mining.Lacks(in w2.Settlements.Get(sid), Mining.Iron), "it lacks bog ore");
            long mark = w2.History.LastId;
            w2.Migration.Look(sid, at, 0);
            int c1 = w2.Settlements.Get(sid).Lore.Best(out float q1);
            Console.WriteLine("best place without want " + c0 + " (" + q0.ToString("0.00") + "), wanting iron " + c1 + " (" + q1.ToString("0.00") + ")");
            Assert.GreaterOrEqual(q1, q0, "worth at least as much");
            Assert.Greater(q1, q0 * 1.0001f, "land by the ore they want is worth more");
            bool told = false;
            for (int i = w2.History.FirstIndexAfter(mark); i < w2.History.Count; i++)
                if (w2.History.At(i).Kind == HistoryKind.Scouted && w2.History.At(i).ArgA == Mining.Iron) { told = true; Console.WriteLine(HistoryText.Line(w2, w2.History.At(i))); }
            Assert.IsTrue(told, "told as land by the ore");
        }

        [Test]
        public void FoundersOfANewVillageKnowTheOreRoundTheirSite()
        {
            var w = Scene(out int sid, out int at, 701);
            Assume.That(w != null, "a place with bog ore near");
            var sight = new List<int>();
            w.Hazards.CellsWithin(w, at, Sites.LookRadius, sight);
            int bog = -1;
            for (int j = 0; j < sight.Count && bog < 0; j++) if (Mining.BogAt(w, sight[j])) bog = sight[j];
            int site = -1; var scratch = new List<int>();
            for (int j = 0; j < sight.Count && site < 0; j++)
                if (Sites.Quality(w, sight[j], scratch) >= Sites.MinQuality && Vec3d.ArcDistance(w.Planet.Grid.Center[sight[j]], w.Planet.Grid.Center[bog], w.Planet.Radius) <= Mining.SitePull) site = sight[j];
            long mark = w.History.LastId;
            int nid = w.Migration.FoundAt(site, 0, sid);
            Assert.AreNotEqual(0, w.Settlements.Get(nid).FeCell, "bog ore by their site, known from the first day");
            Assert.LessOrEqual(w.Settlements.Get(nid).FeDist, Mining.FoundersLook + 1.0);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.OreFound && w.History.At(i).Settlement == nid) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
        }
    }
}
