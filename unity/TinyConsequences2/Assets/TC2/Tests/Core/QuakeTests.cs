using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Earthquakes (05 §3b, D2-030): faults that load and cascade, consequences with causes, waves, continuity.</summary>
    [TestFixture]
    public class QuakeTests
    {
        [Test]
        public void FaultsLoadWithThePlatesAndRuptureInCascadesOfEverySize()
        {
            var w = TestWorlds.Make(61);
            var P = w.Planet;
            var slip = w.Tect.Slip;
            for (int c = 0; c < P.Cells; c++)
                if (slip[c] <= 0f) Assert.AreEqual(0f, P.Stress[c], "stress only on faults (cell " + c + ")");
            // load the faults for 6,000 years and let every patch past the threshold rupture
            var rup = new List<int>();
            int one = 0, ten = 0, forty = 0, quakes = 0;
            for (int step = 0; step < 600; step++)
            {
                for (int c = 0; c < P.Cells; c++) if (slip[c] > 0f) P.Stress[c] += slip[c] * 10f;
                w.Now += DMath.ToLong(10 * SimTime.Year);
                for (int c = 0; c < P.Cells; c++)
                {
                    if (P.Stress[c] < Quakes.Threshold) continue;
                    Quakes.Cascade(w, c, rup);
                    if (rup.Count == 0) continue;
                    quakes++;
                    if (rup.Count == 1) one++;
                    if (rup.Count >= 10) ten++;
                    if (rup.Count >= 40) forty++;
                }
            }
            Assert.Greater(quakes, 1000, "faults rupture again and again");
            Assert.Greater(one, quakes / 4, "most quakes stay one patch");
            Assert.Greater(ten, 0, "some run along the fault");
            Assert.Less(ten, one / 3, "big ones are rarer");
            Assert.LessOrEqual(forty, ten, "and the biggest rarest");
            double m1 = Quakes.Magnitude(1), m10 = Quakes.Magnitude(10);
            Assert.That(m1, Is.InRange(5.3, 5.7));
            Assert.That(m10, Is.InRange(6.7, 7.1));
        }

        static void LoadLineThrough(PlanetState P, int start, int length)
        {
            // a fault line of patches all at the threshold, running east through the start cell
            var g = P.Grid;
            int c = start;
            for (int i = 0; i < length; i++)
            {
                P.Stress[c] = (float)(Quakes.Threshold * 1.01);
                c = g.Nbr4[c * 4];
            }
        }

        [Test]
        public void AGreatQuakesConsequencesAllTraceToIt()
        {
            var w = TestWorlds.Make(62);
            var P = w.Planet;
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Granary = 5000;
            System.Array.Clear(P.Stress, 0, P.Stress.Length);
            LoadLineThrough(P, s.Cell, 40);
            int alive = w.LivingPeople();
            long before = w.History.LastId;
            w.Quake(s.Cell);
            long quake = 0;
            int deaths = 0, spoiled = 0;
            for (int i = w.History.FirstIndexAfter(before); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Quake) { quake = e.Id; Assert.Greater(e.Magnitude, 7.5f, "a great quake"); Assert.AreEqual(Tier.Historical, e.Tier); continue; }
                Assert.AreNotEqual(0, quake, "the quake is recorded first");
                Assert.AreEqual(quake, e.Root, e.Kind + " traces to the quake");
                if (e.Kind == HistoryKind.PersonDied) { deaths++; Assert.AreEqual(5, e.ArgA); }
                if (e.Kind == HistoryKind.StoresSpoiled) spoiled++;
            }
            Assert.Greater(deaths, 0, "houses fell on people");
            Assert.Less(w.LivingPeople(), alive);
            Assert.Greater(spoiled, 0, "stores spilled");
            Assert.Less(w.Settlements.Get(sid).Granary, 5000);
        }

        [Test]
        public void AGreatQuakeUnderTheSeaSendsAWaveOverLowCoasts()
        {
            var w = TestWorlds.Make(63);
            var P = w.Planet;
            var g = P.Grid;
            // the sea cell nearest a village's fields, and a fault through it
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            int sea = -1; double bd = double.MaxValue;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c)) continue;
                double d = Vec3d.ArcDistance(g.Center[c], s.Anchor, P.Radius);
                if (d < bd) { bd = d; sea = c; }
            }
            Assert.Less(bd, 3000, "the first village is near the sea");
            // the coast there is low: the wave can reach it
            for (int c = 0; c < P.Cells; c++)
                if (P.IsLand(c) && Vec3d.ArcDistance(g.Center[c], g.Center[sea], P.Radius) < 1200 && P.Elev[c] - P.SeaLevel > 8) P.Elev[c] = (float)(P.SeaLevel + 8);
            System.Array.Clear(P.Stress, 0, P.Stress.Length);
            // a patch of 60 sea cells around it, all at the threshold (connected: a BFS over the sea)
            var patch = new List<int> { sea };
            var seen = new HashSet<int> { sea };
            for (int head = 0; head < patch.Count && patch.Count < 60; head++)
                for (int q = 0; q < 4 && patch.Count < 60; q++)
                {
                    int nb = g.Nbr4[patch[head] * 4 + q];
                    if (P.IsLand(nb) || !seen.Add(nb)) continue;
                    patch.Add(nb);
                }
            foreach (int c in patch) P.Stress[c] = (float)(Quakes.Threshold * 1.01);
            long before = w.History.LastId;
            w.Quake(sea);
            long quake = 0, wave = 0;
            for (int i = w.History.FirstIndexAfter(before); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Quake) quake = e.Id;
                if (e.Kind == HistoryKind.Tsunami) { wave = e.Id; Assert.AreEqual(quake, e.Cause1, "the quake sent the wave"); Assert.Greater(e.ArgA, 0); }
            }
            Assert.AreNotEqual(0, wave, "a great wave struck the low coast");
            Assert.Greater(w.Metrics.Tsunamis, 0);
        }

        [Test]
        public void SaveLoadThroughQuakesContinuesIdentically()
        {
            var a = TestWorlds.Make(64);
            a.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(a.Now) + SimTime.Day);
            var P = a.Planet;
            for (int c = 0; c < P.Cells; c++) if (P.Stress[c] > 0f) P.Stress[c] = (float)(Quakes.Threshold * (0.97 + 0.03 * (c % 7) / 6.0));
            var b0 = Budget.Unlimited;
            a.AdvanceTo(a.Now + 12 * SimTime.Year, ref b0); // the first tectonic steps load them past the threshold
            long quakes = 0; foreach (long q in a.Metrics.Quakes) quakes += q;
            Assert.Greater(quakes, 0, "quakes before the save");
            var b = SaveFile.Load(SaveFile.Save(a, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "loaded");
            var b1 = Budget.Unlimited; var b2 = Budget.Unlimited;
            a.AdvanceTo(a.Now + 25 * SimTime.Year, ref b1);
            b.AdvanceTo(b.Now + 25 * SimTime.Year, ref b2);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "the same world through quakes and tectonic steps");
        }
    }
}
