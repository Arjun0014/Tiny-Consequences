using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>The ways' woods (D2-160): a route between two villages is worked out from the relief, the land and the woods as they stood at the
    /// last turn of the plates, so the route the running world cached early is the route a loaded world - or a view asking first - builds late.</summary>
    [TestFixture]
    public class RouteGroundTests
    {
        /// <summary>Premise: a world with two villages a route apart, its route, and the woods along it and beside it changed afterwards (felled
        /// and grown) without a turn of the plates.</summary>
        static World Changed(out int a, out int b, out Route early)
        {
            for (ulong seed = 7; seed < 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (b = w.Settlements.Next(a); b != 0; b = w.Settlements.Next(b))
                    {
                        early = w.Routes.Get(w, a, b);
                        if (early == null || early.Pts.Length < 6) continue;
                        // the woods change: the cells the route crosses grow thick, every other land cell is cleared - a way through open land now
                        // costs less than the old one
                        var P = w.Planet;
                        var on = new System.Collections.Generic.HashSet<int>(early.Cell);
                        for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) P.Forest[c] = on.Contains(c) ? 1f : 0f;
                        return w;
                    }
            }
            a = b = 0; early = null;
            return null;
        }

        static bool Same(Route x, Route y)
        {
            if (x == null || y == null || x.Pts.Length != y.Pts.Length || x.Length != y.Length) return false;
            for (int i = 0; i < x.Pts.Length; i++) if (x.Pts[i].X != y.Pts[i].X || x.Pts[i].Y != y.Pts[i].Y || x.Pts[i].Z != y.Pts[i].Z) return false;
            return true;
        }

        [Test]
        public void ARouteBuiltLateIsTheRouteBuiltEarly()
        {
            var w = Changed(out int a, out int b, out var early);
            Assume.That(w != null, "two villages a route apart");
            w.Routes.Invalidate();   // (as a load clears it)
            var late = w.Routes.Get(w, a, b);
            Console.WriteLine("early " + early.Length.ToString("0") + " m over " + early.Pts.Length + " points; late " + late.Length.ToString("0") + " m over " + late.Pts.Length);
            Assert.IsTrue(Same(early, late), "the same route: the woods it was worked out from have not changed");
            // `--oldways`: worked out from the woods as they are now, the late route goes another way
            RouteCache.LiveForest = true;
            try
            {
                w.Routes.Invalidate();
                var old = w.Routes.Get(w, a, b);
                Console.WriteLine("--oldways: " + old.Length.ToString("0") + " m over " + old.Pts.Length + " points");
                Assert.IsFalse(Same(early, old), "--oldways: another route from the woods as they are now");
            }
            finally { RouteCache.LiveForest = false; w.Routes.Invalidate(); }
        }

        [Test]
        public void ALoadedWorldBuildsTheRoutesTheRunningWorldHad()
        {
            var w = Changed(out int a, out int b, out var early);
            Assume.That(w != null, "two villages a route apart");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);   // (a load: its routes rebuilt on demand)
            Assert.IsTrue(Same(early, copy.Routes.Get(copy, a, b)), "the loaded world's route is the running world's");
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "and the same state (the ways' woods saved)");
        }

        [Test]
        public void TheWaysWoodsAreTakenAtTheTurnOfThePlates()
        {
            var w = TestWorlds.Make(11);
            var P = w.Planet;
            int land = -1;
            for (int c = 0; c < P.Cells && land < 0; c++) if (P.IsLand(c)) land = c;
            Assume.That(land >= 0);
            Assert.AreEqual(P.Forest[land], P.WayForest[land], "at the world's making: its woods");
            P.Forest[land] = P.Forest[land] > 0.5f ? 0.1f : 0.9f;
            Assert.AreNotEqual(P.Forest[land], P.WayForest[land], "the woods change; the ways' woods wait for the turn");
            w.TectonicStep();
            Assert.AreEqual(P.Forest[land], P.WayForest[land], "taken again at the turn of the plates");
        }
    }
}
