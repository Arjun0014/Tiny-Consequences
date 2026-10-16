using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Ways on foot round the water (D2-156): lived walks bend round a lake or a bay across their way; crofts, the well's spot and the woods
    /// keep out of the water.</summary>
    [TestFixture]
    public class AfootTests
    {
        /// <summary>Premise: a village and a cell's middle on dry ground 200 m-1 km from it, not under its fields, whose straight way from the village's
        /// square and from its middle crosses the water, with a bend round it from both (bx, by: the cell's middle in the village's frame).</summary>
        static World AcrossTheWater(out int sid, out double bx, out double by, ulong from = 701)
        {
            var near = new List<int>();
            for (ulong seed = from; seed < from + 120; seed++)
            {
                var w = TestWorlds.Make(seed);
                var P = w.Planet;
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (!s.Inhabited || Boats.Afloat(P, s.LocalToSphere(s.SquareX, s.SquareY, P.Radius)) || Boats.Afloat(P, s.LocalToSphere(0, 0, P.Radius))) continue;
                    w.Hazards.CellsWithin(w, s.Cell, 1000.0, near);
                    near.Sort();
                    foreach (int c in near)
                    {
                        double r = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                        if (r < 200.0 || r > 1000.0 || !P.IsLand(c) || P.FieldOwner[c] != 0 || P.Ice[c] > 0.5f || !TC2.Core.Planet.Shore.Dry(P, P.Grid.Center[c])) continue;
                        s.SphereToLocal(P.Grid.Center[c], P.Radius, out double qx, out double qy);
                        if (Afoot.Dry(w, in s, s.SquareX, s.SquareY, qx, qy) || Afoot.Dry(w, in s, 0, 0, qx, qy)) continue;
                        if (!Afoot.Bend(w, in s, s.SquareX, s.SquareY, qx, qy, out _, out _) || !Afoot.Bend(w, in s, 0, 0, qx, qy, out _, out _)) continue;
                        bx = qx; by = qy;
                        return w;
                    }
                }
            }
            sid = 0; bx = by = 0;
            return null;
        }

        /// <summary>Under the drawn water anywhere along the straight way from a to b, looked at every `step` m.</summary>
        static bool WetAlong(World w, in Settlement s, double ax, double ay, double bx, double by, double step)
        {
            var P = w.Planet;
            double dx = bx - ax, dy = by - ay, L = Math.Sqrt(dx * dx + dy * dy);
            for (double t = step; t < L; t += step)
                if (Boats.Afloat(P, s.LocalToSphere(ax + dx * t / L, ay + dy * t / L, P.Radius))) return true;
            return Boats.Afloat(P, s.LocalToSphere(bx, by, P.Radius));
        }

        [Test]
        public void ABendKeepsBothLegsOutOfTheWaterAndTheWayIsLonger()
        {
            var w = AcrossTheWater(out int sid, out double bx, out double by);
            Assume.That(w != null, "a village with dry ground across the water");
            ref var s = ref w.Settlements.Get(sid);
            double ax = s.SquareX, ay = s.SquareY;
            Assert.IsTrue(WetAlong(w, in s, ax, ay, bx, by, 5.0), "the premise: the straight way crosses the water");
            Assert.IsTrue(Afoot.Bend(w, in s, ax, ay, bx, by, out double vx, out double vy));
            Assert.IsFalse(WetAlong(w, in s, ax, ay, vx, vy, 5.0), "to the bend dry (looked at every 5 m)");
            Assert.IsFalse(WetAlong(w, in s, vx, vy, bx, by, 5.0), "from the bend dry (looked at every 5 m)");
            double straight = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            double way = Afoot.Way(w, in s, ax, ay, bx, by);
            Console.WriteLine("village " + sid + ": " + straight.ToString("0") + " m straight across the water, " + way.ToString("0") + " m round it by (" + vx.ToString("0") + ", " + vy.ToString("0") + ")");
            Assert.Greater(way, straight);
            double av = Math.Sqrt((vx - ax) * (vx - ax) + (vy - ay) * (vy - ay));
            Assert.AreEqual(av, Afoot.Way(w, in s, ax, ay, vx, vy), 1e-6 * av, "a dry way is its straight length");
        }

        /// <summary>The premise's woods: the cell at the spot across the water stands dense and is the village's woodlot; nothing in its store.</summary>
        static void WoodsAcross(World w, int sid, double bx, double by)
        {
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            int c = P.Grid.CellOf(s.LocalToSphere(bx, by, P.Radius));
            P.Forest[c] = 1.0f;
            s.WoodLot = c + 1; s.WoodX = (float)bx; s.WoodY = (float)by;
            s.WoodDist = (float)Math.Sqrt(bx * bx + by * by);
            s.Woodstore = 0.0;
        }

        /// <summary>Lived hours with woodcutters going out to the woods across the water: looks every five minutes at everyone walking (and at the
        /// woodcutters at their work), counting the looks under the drawn water.</summary>
        static void LiveWithWoods(World w, int sid, int hours, out int walking, out int wet, out int woodLooks, out int woodWet)
        {
            var P = w.Planet;
            walking = wet = woodLooks = woodWet = 0;
            for (int m = 0; m < hours * 12; m++)
            {
                w.AdvanceTo(w.Now + 5 * SimTime.Minute);
                ref var s = ref w.Settlements.Get(sid);
                if (s.Woodstore > 2.0) s.Woodstore = 0.0;   // (their store kept low: they go on fetching)
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++)
                {
                    var q = w.People.Get(res[i]);
                    if (!q.Alive || q.At != sid || q.Step != StepKind.Local || q.Act == ActivityKind.Sail) continue;
                    bool moving = w.Now < q.TA;
                    if (!moving && q.Act != ActivityKind.Wood) continue;
                    bool inWater = Boats.Afloat(P, w.Agents.PositionAt(q, w.Now));
                    if (moving) { walking++; if (inWater) wet++; }
                    if (q.Act == ActivityKind.Wood) { woodLooks++; if (inWater) woodWet++; }
                }
            }
        }

        [Test]
        public void WoodcuttersWalkRoundTheWaterToWoodsAcrossIt()
        {
            // (D2-156, the `wet` audit: woodcutters walked across a bay to their woodlot and home over it again - a town's 325 person-hours in the water in
            // two lived days.) Premise: the village's woodlot across the water from its square
            var w = AcrossTheWater(out int sid, out double bx, out double by);
            Assume.That(w != null, "a village with dry ground across the water");
            WoodsAcross(w, sid, bx, by);
            long bent0 = w.Metrics.BentWalks;
            LiveWithWoods(w, sid, 48, out int walking, out int wet, out int woodLooks, out int woodWet);
            Console.WriteLine("village " + sid + ": " + walking + " looks at people walking, " + wet + " in the water; " + woodLooks + " at woodcutters, " + woodWet +
                              " in the water; walks bent " + (w.Metrics.BentWalks - bent0) + ", with no dry way " + w.Metrics.WetWalks);
            Assert.Greater(woodLooks, 0, "woodcutters went out");
            Assert.Greater(w.Metrics.BentWalks - bent0, 0, "walks bent round the water");
            Assert.AreEqual(0, woodWet, "no woodcutter in the water");
            Assert.LessOrEqual(wet, w.Metrics.WetWalks * 4, "walkers in the water only on walks no bend kept dry");
            // the same premise under the old rule: the woodcutters cross the water
            var w2 = AcrossTheWater(out int sid2, out double bx2, out double by2);
            Afoot.Off = true;
            try
            {
                WoodsAcross(w2, sid2, bx2, by2);
                LiveWithWoods(w2, sid2, 48, out int walking2, out int wet2, out int woodLooks2, out int woodWet2);
                Console.WriteLine("--wetways: " + walking2 + " looks at people walking, " + wet2 + " in the water; " + woodLooks2 + " at woodcutters, " + woodWet2 + " in the water");
                Assert.Greater(woodWet2, 0, "the old rule walked them through the water");
            }
            finally { Afoot.Off = false; }
        }

        [Test]
        public void CroftsAreNotDugIntoTheWater()
        {
            // (D2-156: crofts behind houses by a lake's edge ran down into the lake - the `wet` audit's gardeners in the water.) Over whole worlds: no
            // croft's bed under the drawn water, the gardener's spot on dry ground - where the old rule laid beds into it
            int homes = 0, wetOld = 0, wetNew = 0, spotsWet = 0;
            var strips = new List<(double cx, double cz, double hx, double hz)>();
            for (ulong seed = 701; seed < 741; seed++)
            {
                var w = TestWorlds.Make(seed);
                var P = w.Planet;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    var hl = w.HouseholdsOf(sid);
                    for (int i = 0; i < hl.Count; i++)
                    {
                        var hh = w.Households.Get(hl[i]);
                        if (hh.Building == 0 || !w.Buildings.Exists(hh.Building) || !Structures.IsHome(w.Buildings.Get(hh.Building).Kind)) continue;
                        var b = w.Buildings.Get(hh.Building);
                        double af = Footprints.FaceOf(in s, in b);
                        homes++;
                        Afoot.Off = true;
                        try { strips.Clear(); Gardens.Strips(w, sid, hh.Building, strips); }
                        finally { Afoot.Off = false; }
                        foreach (var r in strips) if (Afoot.WetBox(w, in s, b.X, b.Y, af, r.cx, r.cz, r.hx, r.hz)) { wetOld++; break; }
                        strips.Clear(); Gardens.Strips(w, sid, hh.Building, strips);
                        foreach (var r in strips) if (Afoot.WetBox(w, in s, b.X, b.Y, af, r.cx, r.cz, r.hx, r.hz)) { wetNew++; break; }
                        if (Gardens.Measure(w, sid, hh.Building, out double gx, out double gy) > 0 && Boats.Afloat(P, s.LocalToSphere(gx, gy, P.Radius))) spotsWet++;
                    }
                }
            }
            Console.WriteLine(homes + " homes: crofts with a bed in the water " + wetOld + " under the old rule, " + wetNew + " now; gardeners' spots in the water " + spotsWet);
            Assert.Greater(homes, 0);
            Assert.AreEqual(0, wetNew, "no bed in the water");
            Assert.AreEqual(0, spotsWet, "no gardener's spot in the water");
        }

        [Test]
        public void TheWoodsAreChosenByTheWayTheirCuttersWalk()
        {
            // (D2-156) Premise: the only woods within reach stand across the water from the village: under the old rule fetched at the straight line's
            // length; now at the length of the way round the water (or none, where that way is beyond the woods' reach)
            var w = AcrossTheWater(out int sid, out double bx, out double by);
            Assume.That(w != null, "a village with dry ground across the water");
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            int c = P.Grid.CellOf(s.LocalToSphere(bx, by, P.Radius));
            var near = new List<int>();
            w.Hazards.CellsWithin(w, s.Cell, Woods.Reach, near);
            foreach (int k in near) P.Forest[k] = 0.0f;
            P.Forest[c] = 1.0f;
            double straight = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
            double way = Afoot.Way(w, in s, 0, 0, bx, by);
            Afoot.Off = true;
            int old;
            try { old = Woods.Choose(w, sid); }
            finally { Afoot.Off = false; }
            float oldDist = w.Settlements.Get(sid).WoodDist;
            int lot = Woods.Choose(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Console.WriteLine("village " + sid + ": the woods " + c + " " + straight.ToString("0") + " m off straight across the water, " + way.ToString("0") + " m round it; the old rule's woodlot " +
                              old + " at " + oldDist.ToString("0") + " m, now " + lot + " at " + s2.WoodDist.ToString("0") + " m");
            Assert.AreEqual(c, old, "the old rule: the woods across the water");
            Assert.AreEqual(straight, oldDist, 0.5, "at the straight line's length");
            Assert.Greater(way, straight, "the way round the water is longer");
            if (way <= Woods.Reach)
            {
                Assert.AreEqual(c, lot, "the same woods");
                Assert.AreEqual(way, s2.WoodDist, 0.5, "at the length of the way round the water");
            }
            else Assert.AreEqual(-1, lot, "none: the way round is beyond the woods' reach");
        }

        [Test]
        public void SaveAndLoadContinueABentWalkExactly()
        {
            // (D2-156: the bend is part of a person's state) Premise: woodcutters on their way round the water when the world is saved
            var w = AcrossTheWater(out int sid, out double bx, out double by);
            Assume.That(w != null, "a village with dry ground across the water");
            WoodsAcross(w, sid, bx, by);
            int inBend = 0;
            for (int m = 0; m < 48 * 12 && inBend == 0; m++)
            {
                w.AdvanceTo(w.Now + 5 * SimTime.Minute);
                ref var s = ref w.Settlements.Get(sid);
                if (s.Woodstore > 2.0) s.Woodstore = 0.0;
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.Step == StepKind.Local && q.TBend > w.Now && q.TBend < q.TA) inBend++; }
            }
            Assume.That(inBend > 0, "someone walking round the water");
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "the loaded world is the saved one");
            long until = w.Now + 6 * SimTime.Hour;
            w.AdvanceTo(until); w2.AdvanceTo(until);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and both live on alike");
        }
    }
}
