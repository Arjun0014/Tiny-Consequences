using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The ground buildings cover (C13.3, D2-118): every home is its plan's rectangles turned to the way it faces, every work a disc; lots, plans and
    /// works are laid out only where their ground meets nothing else's, no street or lane, not the green or the market place; a town's core
    /// frontage is divided into narrow plots that take only narrow houses; the market place widens with the traders who come; homes left in the
    /// open are taken down. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class FootprintsTests
    {
        [Test]
        public void RectanglesMeetOnlyWhereTheyOverlap()
        {
            Footprints.Rect(Architecture.Cottage, 0, out double cx, out double cz, out double hx, out double hz);
            // two cottages side by side along their fronts (both facing +y): apart by more than their widths, they do not meet
            Assert.IsFalse(Footprints.Boxes(0, 0, Math.PI / 2, cx, cz, hx, hz, 2 * hx + 0.01, 0, Math.PI / 2, cx, cz, hx, hz, 0.0));
            Assert.IsTrue(Footprints.Boxes(0, 0, Math.PI / 2, cx, cz, hx, hz, 2 * hx - 0.01, 0, Math.PI / 2, cx, cz, hx, hz, 0.0));
            // the gap between them counts
            Assert.IsTrue(Footprints.Boxes(0, 0, Math.PI / 2, cx, cz, hx, hz, 2 * hx + 0.2, 0, Math.PI / 2, cx, cz, hx, hz, Footprints.Gap));
            // one turned 45 degrees beside the other: its corner reaches out by (hx + hz) / sqrt 2
            double corner = (hx + hz) * Math.Sqrt(0.5);
            Assert.IsTrue(Footprints.Boxes(0, 0, Math.PI / 2, cx, cz, hx, hz, hx + corner - 0.05, 0, Math.PI / 4, cx, cz, hx, hz, 0.0));
            Assert.IsFalse(Footprints.Boxes(0, 0, Math.PI / 2, cx, cz, hx, hz, hx + corner + 0.05, 0, Math.PI / 4, cx, cz, hx, hz, 0.0));
            // a disc
            Assert.IsTrue(Footprints.BoxDisc(0, 0, 0, cx, cz, hx, hz, 0, hx + 1.9, 2.0, 0.0));
            Assert.IsFalse(Footprints.BoxDisc(0, 0, 0, cx, cz, hx, hz, 0, hx + 2.1, 2.0, 0.0));
            // every plan keeps the ground of a one-room cottage within the ground a lot keeps; a narrow house keeps within half a lot
            Footprints.Rect(Footprints.HutPlan, 0, out _, out _, out double lx, out double lz);
            Footprints.Rect(Architecture.Small, 0, out _, out _, out double sx, out double sz);
            Assert.LessOrEqual(Math.Max(sx, sz), Math.Min(lx, lz), "a one-room cottage stands on a lot's ground turned any way");
            Assert.IsTrue(Footprints.Narrow(Architecture.Row) && Footprints.Narrow(Architecture.Small));
            Assert.IsFalse(Footprints.Narrow(Architecture.Cottage) || Footprints.Narrow(Architecture.Two) || Footprints.Narrow(Architecture.Long) || Footprints.Narrow(Architecture.Hall));
        }

        /// <summary>A household with no people that raises a house where FreeLot puts it; its means set by its goods.</summary>
        static int Settle(World w, int sid, float gear)
        {
            int hid = w.Ids.Next(EntityKind.Household);
            Homes.FreeLot(w, sid, hid, out float x, out float y);
            w.Households.Add(hid, new Household { Id = hid, Settlement = sid, HomeX = x, HomeY = y, Shelter = Construction.House, Gear = gear, Formed = w.Now });
            w.HouseholdsOf(sid).InsertSorted(hid);
            Homes.Raised(w, hid);
            return hid;
        }

        /// <summary>Lays `n` streets (premise) at bearings whose streets and lanes run clear of what stands, at least a radian apart.</summary>
        static void Streets2(World w, int sid, int n = 2)
        {
            for (double b = 0.0; b < 2 * Math.PI && w.Settlements.Get(sid).StreetN < n; b += 0.05)
            {
                ref var s = ref w.Settlements.Get(sid);
                bool apart = true;
                for (int k = 0; k < s.StreetN; k++) if (Streets.Apart(b, s.StreetAt(k)) < 1.0) apart = false;
                if (!apart || Streets.StreetCuts(w, sid, b) > 0 || Streets.LaneCuts(w, sid, b, 1) > 0 || Streets.LaneCuts(w, sid, b, -1) > 0) continue;
                s.SetStreet(s.StreetN, b); s.StreetN++;
            }
            Assume.That(w.Settlements.Get(sid).StreetN == n, "room for the streets");
        }

        [Test]
        public void ATownsHousesNeverStandInEachOtherAndItsCoreRowsAreNarrow()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Population = 420;                      // premise: a town (its core frontage divided)
            Streets2(w, sid);                        // premise: two streets laid where they run clear of its first houses
            int core = 0, rows = 0, half = 0;
            for (int k = 0; k < 110; k++)
            {
                int hid = Settle(w, sid, k % 3 == 0 ? 0f : 4f + (k % 5));   // (some poor, most of some means)
                ref var hh = ref w.Households.Get(hid);
                ref var b = ref w.Buildings.Get(hh.Building);
                ref var s2 = ref w.Settlements.Get(sid);
                int plan = Architecture.PlanOf(b.Form);
                if (Streets.CorePlot(in s2, b.X, b.Y))
                {
                    core++;
                    Assert.IsTrue(Footprints.Narrow(plan), "a divided plot takes only a narrow house (" + Architecture.PlanName(plan) + ")");
                    if (plan == Architecture.Row) rows++;
                    // a plot at a half step: between the 13 m lots
                    Streets.Facing(in s2, b.X, b.Y, out double fx, out double fy, out int line);
                    double along = Math.Sqrt((fx - s2.SquareX) * (fx - s2.SquareX) + (fy - s2.SquareY) * (fy - s2.SquareY));
                    double j = (along - Streets.Start) / Streets.Spacing;
                    if (Math.Abs(j - Math.Round(j)) > 0.25) half++;
                }
                Assert.AreEqual(1, b.Faced, "laid out with its bearing");
            }
            ref var s3 = ref w.Settlements.Get(sid);
            Footprints.Audit(w, sid, out int hh2, out int hw, out int ww, out int open);
            Console.WriteLine("110 houses: town core reach " + s3.CoreR.ToString("0") + " m, " + core + " on divided plots (" + rows + " narrow town houses, " + half + " on half steps); overlapping home/home " + hh2 + ", home/work " + hw + "; in the open " + open);
            Assert.Greater(s3.CoreR, 0f, "a town's core is divided");
            Assert.AreEqual(0, hh2, "no house stands in another");
            Assert.AreEqual(0, hw, "nor in a work");
            Assert.AreEqual(0, open, "nor in a street, the green or a ring");
            Assert.Greater(rows, 8, "rows of narrow town houses along the core's streets");
            Assert.Greater(half, 4, "plots divided at the half step");
        }

        [Test]
        public void AVillageBelowATownKeepsWholeLots()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Population = 120;
            Streets2(w, sid, 1);
            for (int k = 0; k < 30; k++) Settle(w, sid, 5f);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreEqual(0f, s2.CoreR, "no divided core");
            int rows = 0; var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) { var b = w.Buildings.Get(bl[i]); if (Structures.IsHome(b.Kind) && Architecture.PlanOf(b.Form) == Architecture.Row) rows++; }
            Assert.AreEqual(0, rows, "no narrow town houses in a village");
        }

        [Test]
        public void TheMarketPlaceWidensWithItsTradersAndNothingNewStandsInIt()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            Assert.AreEqual(0f, w.Settlements.Get(sid).MarketR, "no market, no market place");
            Structures.MarketDealt(w, sid);
            Assert.AreEqual(0f, w.Settlements.Get(sid).MarketR, "traders at a green with no market widen nothing");
            ref var s0 = ref w.Settlements.Get(sid);
            // a market raised (premise: laid out and built as the village's work would be)
            int mid = Structures.Begin(w, sid, Structures.Market, 0, CauseRel.None);
            var mk = w.Buildings.Get(mid);
            double md = Math.Sqrt((mk.X - s0.SquareX) * (mk.X - s0.SquareX) + (mk.Y - s0.SquareY) * (mk.Y - s0.SquareY));
            Console.WriteLine("the market's cross laid out " + md.ToString("0.0") + " m from the square");
            Assert.Less(md, Footprints.GreenR, "a market is held on the green");
            Structures.Raised(w, sid, mid, 1e6, 0, 0);
            Assert.AreEqual((float)Structures.MarketFirst, w.Settlements.Get(sid).MarketR, 1e-3, "its market place: the green and its stalls");
            // an empty house standing where the place will widen: taken down when it does; a lived-in one stays
            float before = w.Settlements.Get(sid).MarketR;
            int n = 0;
            for (; n < 2000 && w.Settlements.Get(sid).MarketR < Structures.MarketMost; n++) Structures.MarketDealt(w, sid);
            float after = w.Settlements.Get(sid).MarketR;
            Console.WriteLine("market place " + before.ToString("0.0") + " -> " + after.ToString("0.0") + " m after " + n + " traders");
            Assert.Greater(after, before, "widened by the traders who came");
            Assert.LessOrEqual(after, Structures.MarketMost + 1e-3, "up to its most");
            Assert.Greater(n, 100, "a trader widens it a little");
            // nothing new in it: lots, houses and works keep out
            for (int k = 0; k < 25; k++)
            {
                int hid = Settle(w, sid, 3f);
                var b = w.Buildings.Get(w.Households.Get(hid).Building);
                Assert.AreEqual(0, Homes.InOpen(w, w.Households.Get(hid).Building), "no new house in the market place");
            }
            ref var s1 = ref w.Settlements.Get(sid);
            Assert.IsFalse(Footprints.WorkFits(w, sid, s1.SquareX + 12.0, s1.SquareY + 12.0, Structures.Granary, 0.0, false), "no work in the market place");
        }

        [Test]
        public void AHomeLeftInTheMarketPlaceIsTakenDownAndItsTimberKept()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            // a housed household (worldgen), its house then reached by a market place laid wide (premise)
            int hid = 0, bid = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { var h = w.Households.Get(hl[i]); if (h.Building != 0 && h.Shelter >= Construction.House) { hid = hl[i]; bid = h.Building; break; } }
            Assume.That(bid != 0);
            ref var s = ref w.Settlements.Get(sid);
            var b0 = w.Buildings.Get(bid);
            double d = Math.Sqrt((b0.X - s.SquareX) * (b0.X - s.SquareX) + (b0.Y - s.SquareY) * (b0.Y - s.SquareY));
            s.MarketR = (float)(d + 1.0);
            Assert.AreEqual(2, Homes.InOpen(w, bid), "it stands in the market place");
            // never renewed there (out of it, the same old house of a household of means would be)
            ref var hh = ref w.Households.Get(hid);
            hh.Gear = 50f;
            ref var bb = ref w.Buildings.Get(bid);
            bb.Built = w.Now - SimTime.Years(90);
            w.Settlements.Get(sid).Woodstore = 1e5;
            float place = w.Settlements.Get(sid).MarketR;
            w.Settlements.Get(sid).MarketR = 0f;
            Homes.WeighRebuilding(w, sid);
            Assume.That(w.Households.Get(hid).Rebuild != 0, "out of the market place it would be renewed");
            w.Households.Get(hid).Rebuild = 0;
            w.Settlements.Get(sid).MarketR = place;
            Homes.WeighRebuilding(w, sid);
            Assert.AreEqual(0, w.Households.Get(hid).Rebuild, "a house in the market place is not renewed there");
            double wood0 = w.Settlements.Get(sid).Woodstore;
            int mark = w.History.Count;
            Homes.Leave(w, hid);
            var b1 = w.Buildings.Get(bid);
            Assert.AreEqual(0.0, Homes.RuinLeft(in b1, w.Now), 1e-9, "taken down: gone at once");
            Assert.Greater(w.Settlements.Get(sid).Woodstore, wood0, "half its timber back in the woodstore");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.HomeCleared) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told, "history tells it");
            // a house left elsewhere stands empty, as before
            int other = 0;
            for (int i = 0; i < hl.Count; i++) { var h = w.Households.Get(hl[i]); if (hl[i] != hid && h.Building != 0 && Homes.InOpen(w, h.Building) == 0) { other = hl[i]; break; } }
            Assume.That(other != 0);
            int ob = w.Households.Get(other).Building;
            Homes.Leave(w, other);
            var o = w.Buildings.Get(ob);
            Assert.AreEqual(1.0, Homes.RuinLeft(in o, w.Now), 1e-9, "an empty house out of the open stands");
        }

        [Test]
        public void NoHouseStandsOnARiverAndTheRiverEndsAtTheWater()
        {
            // a village with a river line within 120 m of its square (the first found over a few small worlds)
            World w = null; int sid = 0; TC2.Core.Planet.RiverLines.Piece near = default;
            for (ulong seed = 7; seed < 40 && sid == 0; seed++)
            {
                var t = TestWorlds.Make(seed);
                TC2.Core.Planet.RiverLines.Ensure(t.Planet, t.Drainage, t.Seed);
                for (int q = t.Settlements.First(); q != 0 && sid == 0; q = t.Settlements.Next(q))
                {
                    ref var sq = ref t.Settlements.Get(q);
                    foreach (var pc in TC2.Core.Planet.RiverLines.All)
                    {
                        sq.SphereToLocal(pc.A, t.Planet.Radius, out double px, out double py);
                        if (Math.Sqrt((px - sq.SquareX) * (px - sq.SquareX) + (py - sq.SquareY) * (py - sq.SquareY)) < 120.0) { w = t; sid = q; near = pc; break; }
                    }
                }
            }
            Assume.That(sid != 0, "a village by a river");
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            s.SphereToLocal(near.A, P.Radius, out double rx, out double ry);
            Assert.IsTrue(Footprints.Wet(w, in s, rx, ry, 0.0, Architecture.Cottage), "a house on the river's line is in the water");
            Assert.IsFalse(Footprints.HomeFits(w, sid, rx, ry, 0.0, Architecture.Cottage, 0, 0), "and never laid out there");
            // every lot a village's new households take keeps off it
            for (int k = 0; k < 60; k++)
            {
                int hid = Settle(w, sid, 3f);
                ref var s2 = ref w.Settlements.Get(sid);
                var b = w.Buildings.Get(w.Households.Get(hid).Building);
                Assert.IsFalse(Footprints.Wet(w, in s2, b.X, b.Y, Footprints.FaceOf(in s2, in b), Footprints.PlanOf(in b)), "no house on the river");
            }
            // every reach ends on dry ground or at the drawn waterline - never out over the water
            int ends = 0;
            var all = TC2.Core.Planet.RiverLines.All;
            for (int i = 0; i < all.Count; i++)
            {
                bool last = i == all.Count - 1 || all[i + 1].Cell != all[i].Cell;
                if (!last || TC2.Core.Planet.Shore.Dry(P, all[i].B)) continue;
                ends++;
                // its mouth: the last dry point is within a few metres
                Vec3d probe = Vec3d.Slerp(all[i].B, all[i].A, Math.Min(1.0, 6.0 / Math.Max(1e-6, Vec3d.ArcDistance(all[i].A, all[i].B, P.Radius))));
                Assert.IsTrue(TC2.Core.Planet.Shore.Dry(P, probe) || Vec3d.ArcDistance(all[i].A, all[i].B, P.Radius) < 6.0, "it ends a couple of metres into the water");
            }
            Console.WriteLine(all.Count + " river pieces; " + ends + " reaches end at the water's edge");
        }

        [Test]
        public void AStreetIsLaidClearOfTheHousesStandingAndALaneThroughThemStaysShut()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First(), o = 0;
            for (int q = w.Settlements.First(); q != 0; q = w.Settlements.Next(q)) if (q != sid && w.Routes.Get(w, sid, q) != null) { o = q; break; }
            Assume.That(o != 0, "a neighbour reached over land");
            // the bearing the way leaves by
            var r = w.Routes.Get(w, sid, o);
            ref var s = ref w.Settlements.Get(sid);
            s.SphereToLocal(r.PointAt(r.A == sid ? 80.0 : r.Length - 80.0), w.Planet.Radius, out double wx, out double wy);
            double a = Math.Atan2(wy - s.SquareY, wx - s.SquareX);
            // houses standing across that bearing 40-60 m out, and across its left lane's line (premise)
            int hid0 = w.HouseholdsOf(sid)[0];
            int Put(double x, double y)
            {
                int id = w.Ids.Next(EntityKind.Building);
                w.Buildings.Add(id, new Building { Id = id, Settlement = sid, X = (float)x, Y = (float)y, Kind = Construction.House, Household = hid0, Style = id, Built = w.Now, Cond = 1f, Done = 1,
                                                   Form = Architecture.Pack(Architecture.Long, 0, 0, 0, false, 0), Face = (float)(a + Math.PI / 2), Faced = 1 });
                w.BuildingsOf(sid).InsertSorted(id);
                return id;
            }
            ref var s1 = ref w.Settlements.Get(sid);
            double ux = Math.Cos(a), uy = Math.Sin(a);
            Put(s1.SquareX + ux * 45, s1.SquareY + uy * 45);
            int laneHouse = Put(s1.SquareX + ux * 70 - uy * Streets.LaneOff, s1.SquareY + uy * 70 + ux * Streets.LaneOff);
            w.Ways.Walk(w, sid, o, 60.0);
            Streets.Refresh(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assume.That(s2.StreetN == 1, "a street laid");
            double b = s2.StreetAt(0);
            Console.WriteLine("the way leaves at " + a.ToString("0.000") + ", the street laid at " + b.ToString("0.000") + ", lanes shut " + s2.LanesShut);
            Assert.LessOrEqual(Streets.Apart(a, b), Streets.Bend + 1e-9, "within its bend of the way");
            // the bearing whose street runs through fewest of what stands (none where there is a gap; never more than MostCuts)
            int fewest = int.MaxValue;
            for (int k = 0; k <= 2 * Streets.BendSteps; k++) fewest = Math.Min(fewest, Streets.StreetCuts(w, sid, a + ((k & 1) == 0 ? 1 : -1) * ((k + 1) >> 1) * (Streets.Bend / Streets.BendSteps)));
            Console.WriteLine("its street runs through " + Streets.StreetCuts(w, sid, b) + " building(s); the fewest of any bearing " + fewest);
            Assert.AreEqual(fewest, Streets.StreetCuts(w, sid, b), "laid where it runs through fewest");
            Assert.LessOrEqual(fewest, Streets.MostCuts);
            Assert.Greater(Streets.StreetCuts(w, sid, a), Streets.StreetCuts(w, sid, b), "not along the way's own bearing, through the house across it");
            // a lane through a standing house stays shut: no lots on it, nothing faces it - until the house is gone
            int shutLanes = 0;
            for (int side = 0; side < 2; side++) if (Streets.Shut(in s2, 1 + side)) { shutLanes++; Assert.Greater(Streets.LaneCuts(w, sid, b, side == 0 ? 1 : -1), 0); }
            if (shutLanes > 0)
            {
                ref var lb = ref w.Buildings.Get(laneHouse);
                lb.Household = 0; lb.Left = w.Now - SimTime.Years(2000);   // (gone into the land)
                Streets.OpenLanes(w, sid);
                Assert.AreEqual(0, w.Settlements.Get(sid).LanesShut & 3, "opened once nothing stands in it");
            }
        }
    }
}
