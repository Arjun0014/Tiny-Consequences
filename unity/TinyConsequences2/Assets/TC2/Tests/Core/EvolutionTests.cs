using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Adaptation and new species (06 §2, D2-037).</summary>
    [TestFixture]
    public class EvolutionTests
    {
        static int Kind(PlanetState P, byte kind) { for (int s = 0; s < P.Species.Count; s++) if (P.Species[s].Kind == kind) return s; return -1; }

        [Test]
        public void ACoatFollowsWhatThePlaceAsksSlowly()
        {
            var w = TestWorlds.Make(111);
            var P = w.Planet;
            int hare = Kind(P, 1);
            var sp = P.Species[hare];
            int c = -1;
            for (int i = 0; i < P.Cells; i++) if (P.IsLand(i) && P.Abund[hare * P.Cells + i] > 0.5f) { c = i; break; }
            Assert.GreaterOrEqual(c, 0);
            int idx = hare * P.Cells + c;
            double ideal = Evolution.Ideal(P, sp, c);
            P.Coat[idx] = (float)(ideal + 8.0);   // a coat far from what this place asks
            double keep = DMath.Exp(-Evolution.Pull * (sp.Fecundity + 0.5) * 0.25);
            double cost = 0;
            for (int i = 0; i < 400; i++) cost += Evolution.Select(P, sp, idx, c, 0.25, keep);   // a hundred years
            double off = System.Math.Abs(P.Coat[idx] - ideal);
            Assert.Less(off, 8.0 * 0.6, "a century takes the coat a good way toward the place");
            Assert.Greater(off, 8.0 * 0.05, "but not all the way: adaptation is slow");
            Assert.Greater(cost, 0, "a coat far from the place costs lives while it lasts");
        }

        /// <summary>Two groups of hares at opposite ends of the land, nothing between them; the second with a warmer coat.</summary>
        static (World w, int hare, System.Collections.Generic.List<int> a, System.Collections.Generic.List<int> b) TwoGroups(ulong seed, float coatB)
        {
            var w = TestWorlds.Make(seed);
            var P = w.Planet;
            var g = P.Grid;
            int hare = Kind(P, 1), n = P.Cells;
            for (int c = 0; c < n; c++) P.Abund[hare * n + c] = 0f;
            int a0 = -1, b0 = -1;
            double farthest = 0;
            for (int c = 0; c < n; c++)
            {
                if (!P.IsLand(c)) continue;
                if (a0 < 0) { a0 = c; continue; }
                double ang = Vec3d.Angle(g.Center[c], g.Center[a0]);
                if (ang > farthest) { farthest = ang; b0 = c; }
            }
            Assert.Greater(farthest, 0.5, "land far from the first group");
            System.Collections.Generic.List<int> Group(int c0, float coat)
            {
                var list = new System.Collections.Generic.List<int> { c0 };
                var seen = new System.Collections.Generic.HashSet<int> { c0 };
                for (int h = 0; h < list.Count && list.Count < 40; h++)
                    for (int q = 0; q < 4 && list.Count < 40; q++) { int nb = g.Nbr4[list[h] * 4 + q]; if (seen.Add(nb)) list.Add(nb); }
                foreach (int c in list) { P.Abund[hare * n + c] = 3f; P.Coat[hare * n + c] = coat; P.Drift[hare * n + c] = 0f; }
                return list;
            }
            var ga = Group(a0, 0f);
            var gb = Group(b0, coatB);
            return (w, hare, ga, gb);
        }

        [Test]
        public void ACutOffPopulationBecomesASpeciesOnlyWhenItsLineageHasLongBeenApart()
        {
            var (w, hare, a, b) = TwoGroups(112, 7f);
            var P = w.Planet;
            int n = P.Cells, before = P.Species.Count;
            Assert.AreEqual(0, Evolution.Speciate(w), "a different coat alone does not make a new kind at once");
            // as if they had been apart for a millennium and more: their lineages have wandered apart
            foreach (int c in b) P.Drift[hare * n + c] = P.Drift[hare * n + a[0]] + 0.8f;   // 0.8 x (1 + 0.35) > 1
            int wolf = Kind(P, 5);
            for (int c = 0; c < n; c++) P.Abund[wolf * n + c] = 0f;   // and the wolves are all gone
            long last = w.History.LastId;
            Assert.AreEqual(1, Evolution.Speciate(w), "apart long enough, and made different by their climates: a kind of their own");
            Assert.AreEqual(before + 1, P.Species.Count);
            var child = P.Species[before];
            Assert.AreEqual(P.Species[hare].Id, child.Parent, "it descends from the hares");
            Assert.AreEqual(P.Species[hare].ColdTol + 7f, child.ColdTol, 0.01f, "its coat is its own now");
            foreach (int c in b) Assert.AreEqual(0f, P.Abund[hare * n + c], "its animals are no longer counted as hares");
            bool speciation = false, extinction = false;
            for (int i = w.History.FirstIndexAfter(last); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Speciation) { speciation = true; Assert.AreEqual(Tier.Epochal, e.Tier); }
                if (e.Kind == HistoryKind.Extinction) extinction = true;
            }
            Assert.IsTrue(speciation, "the history tells of the new kind");
            Assert.IsTrue(extinction, "and of the wolves that are no more");
            Assert.AreNotEqual(0, P.Species[wolf].Gone);
        }

        [Test]
        public void IsolatedLineagesWanderApartWhileARangeWandersTogether()
        {
            var (w, hare, a, b) = TwoGroups(113, 0f);
            var P = w.Planet;
            int n = P.Cells;
            Evolution.Speciate(w);
            float da = P.Drift[hare * n + a[0]], db = P.Drift[hare * n + b[0]];
            foreach (int c in a) Assert.AreEqual(da, P.Drift[hare * n + c], "one range, one lineage");
            foreach (int c in b) Assert.AreEqual(db, P.Drift[hare * n + c], "one range, one lineage");
            Assert.AreNotEqual(da, db, "two ranges cut off from each other wander apart");
            double step = Evolution.DriftStep(P.Species[hare]);
            Assert.LessOrEqual(System.Math.Abs(da), 1.74 * step);
            Assert.LessOrEqual(System.Math.Abs(db), 1.74 * step);
        }
    }
}
