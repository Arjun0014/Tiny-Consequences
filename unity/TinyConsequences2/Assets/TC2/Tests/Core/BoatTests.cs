using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Boats (C9.1, D2-110): a household that lives by the shore's fish makes a dugout from a trunk of its woods when the open sea promises
    /// more than the shore; from it up to two of them fish the sea's grounds - the offshore shoals the shore never reaches - at both
    /// resolutions; nobody rows out in a strong wind, and a trip under a rising one may overturn the boat; a dugout splits with age.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class BoatTests
    {
        /// <summary>A small world with a village that fishes from its shore and has open sea within a boat's reach (and, if asked, sea
        /// that promises Worth times the shore).</summary>
        static World Coast(out int sid, bool promising = true, ulong from = 701)
        {
            for (ulong seed = from; seed < from + 200; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.Fish.A < 0 || s.Sea.A < 0) continue;
                    if (promising && Boats.SeaExpect(w, in s) < Boats.Worth * s.Fish.Expect) continue;
                    return w;
                }
            }
            sid = 0;
            return null;
        }

        /// <summary>Premise: a housed household of the village whose grown members fish well; its woods stand with trunks to hollow; timber in store.</summary>
        static int Fishers(World w, int sid, out int fisher)
        {
            fisher = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                int hid = hl[i];
                var mem = w.Members(hid);
                for (int j = 0; j < mem.Count; j++)
                {
                    ref var q = ref w.People.Get(mem[j]);
                    double a = q.AgeYears(w.Now);
                    if (!q.Alive || a < 18 || a >= 50) continue;
                    q.SkFish = 0.9f;
                    if (fisher == 0) fisher = mem[j];
                }
                if (fisher == 0) continue;
                ref var hh = ref w.Households.Get(hid);
                if (hh.Shelter == Construction.Camp) hh.Shelter = Construction.Hut;
                ref var s = ref w.Settlements.Get(sid);
                s.Woodstore += 100.0;
                int lot = Woods.Ensure(w, sid);
                if (lot >= 0 && w.Planet.Forest[lot] < Boats.BigTrees) w.Planet.Forest[lot] = 0.6f;
                return hid;
            }
            return 0;
        }

        /// <summary>Premise: the household's boat laid down and its hours put in.</summary>
        static int Made(World w, int sid, int hid, int worker, byte kind = Structures.Boat)
        {
            w.Settlements.Get(sid).Woodstore += Boats.WoodOf(kind);
            int bid = Boats.Begin(w, sid, hid, kind);
            if (Boats.IsPlank(kind) && w.People.Exists(worker)) w.People.Get(worker).KBoat = 0.8f;   // (premise: a builder who knows the craft)
            Boats.Work(w, sid, hid, Boats.HoursOf(kind) + 1.0, 1.0, worker);
            return bid;
        }

        /// <summary>Premise: the village builds boats of planks - its people know the craft (the best at 0.8, the rest at 0.6).</summary>
        static void Knows(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown |= (byte)Boats.Bit; s.BestBoat = 0.8f;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) w.People.Get(res[i]).KBoat = i == 0 ? 0.8f : 0.6f;
        }

        [Test]
        public void AHouseholdThatFishesMakesADugoutFromItsWoods()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0 && Woods.Lot(in w.Settlements.Get(sid)) >= 0, "a household that fishes; woods within reach");
            Assert.IsTrue(Boats.Wants(w, in w.Settlements.Get(sid), hid), "it would make a boat");
            double wood0 = w.Settlements.Get(sid).Woodstore;
            Boats.Plan(w, sid);
            int bid = w.Households.Get(hid).Boat;
            Assume.That(bid != 0, "this household was the first of the village to want one");
            Assert.AreEqual(0, w.Buildings.Get(bid).Done, "laid down, not yet made");
            Assert.AreEqual(Structures.Boat, w.Buildings.Get(bid).Kind);
            Assert.IsFalse(Boats.Wants(w, in w.Settlements.Get(sid), hid), "one at a time");
            Assert.AreEqual(Boats.Wood, wood0 - w.Settlements.Get(sid).Woodstore, 0.02, "the trunk taken from the store's timber as it is laid down");
            long mark = w.History.LastId;
            double used = Boats.Work(w, sid, hid, 200.0, 1.0, fisher);
            Assert.AreEqual(Boats.Hours, used, 0.02, "its hours");
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(1, b.Done);
            Assert.AreEqual(fisher, b.Leader);
            Assert.AreEqual(bid, Boats.Of(w, hid), "it can go out");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.BoatBuilt) { told = true; Assert.AreEqual(hid, w.History.At(i).Subject.Id); Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            Assert.IsFalse(Boats.Wants(w, in w.Settlements.Get(sid), hid), "it has one");
        }

        [Test]
        public void NoTrunkToHollowOrNobodyWhoFishesNoBoat()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out _);
            int lot = Woods.Lot(in w.Settlements.Get(sid));
            Assume.That(hid != 0 && lot >= 0, "a household that fishes; woods within reach");
            Assert.IsTrue(Boats.Wants(w, in w.Settlements.Get(sid), hid));
            w.Planet.Forest[lot] = 0.15f;   // (cut over: no trunk fit to hollow)
            Assert.IsFalse(Boats.Wants(w, in w.Settlements.Get(sid), hid), "the woods have no trunk left to hollow");
            w.Planet.Forest[lot] = 0.6f;
            var mem = w.Members(hid);
            for (int j = 0; j < mem.Count; j++) w.People.Get(mem[j]).SkFish = 0.05f;
            Assert.IsFalse(Boats.Wants(w, in w.Settlements.Get(sid), hid), "nobody of it lives by the fish");
        }

        [Test]
        public void FromABoatTheOpenSeasShoals()
        {
            // the same ground fished from a boat and from the shore, in two copies of one world: the boat reaches the offshore kinds too
            var w1 = Coast(out int sid); var w2 = Coast(out int sid2);
            Assume.That(w1 != null && sid == sid2, "a fishing village");
            int ground = w1.Settlements.Get(sid).Sea.A;
            Assert.GreaterOrEqual(w1.Wild.ExpectSea(ground), w1.Wild.ExpectFish(ground), "an hour there is worth more from a boat");
            double k1 = w1.Metrics.FishKg, k2 = w2.Metrics.FishKg;
            double boat = w1.Wild.Fish(ground, 4.0, 0.5, 12345UL, 1, true), shore = w2.Wild.Fish(ground, 4.0, 0.5, 12345UL, 1, false);
            Console.WriteLine("boat " + boat.ToString("0.00") + " FU, shore " + shore.ToString("0.00") + " FU at the same ground");
            Assert.GreaterOrEqual(boat, shore);
            Assert.AreEqual(w1.Metrics.FishKg - k1, w1.Metrics.BoatFishKg, 1e-9, "counted as fish from boats");
            Assert.AreEqual(0.0, w2.Metrics.BoatFishKg);
        }

        [Test]
        public void NobodyRowsOutInAStrongWind()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Made(w, sid, hid, fisher);
            ref var p = ref w.People.Get(fisher);
            int kind = w.Agents.WildChoice(in p, in w.Settlements.Get(sid), p.AgeYears(w.Now), w.Now, out _, out int boat);
            Assert.AreEqual(WildFood.Fishing, kind);
            Assert.AreEqual(bid, boat, "a fair day: out in the boat");
            // a storm whose winds blow over the sea's grounds
            var P = w.Planet; int ground = w.Settlements.Get(sid).Sea.A;
            int c0 = -1;
            for (int c = 0; c < P.Cells && c0 < 0; c++) { double d = Vec3d.ArcDistance(P.Grid.Center[c], P.Grid.Center[ground], PlanetState.GeoRadius); if (d > 300 && d < 600) c0 = c; }
            P.Storms.Add(new Storm { Id = w.Ids.Next(EntityKind.Process), At = P.Grid.Center[c0], Heading = Vec3d.UnitX, Speed = 0f, Wind = 30f, Radius = 2000f, Peak = 30f, Born = w.Now, Kind = 1 });
            Assert.Greater(Boats.SeaWind(w, in w.Settlements.Get(sid)), Boats.Max);
            w.Agents.WildChoice(in w.People.Get(fisher), in w.Settlements.Get(sid), p.AgeYears(w.Now), w.Now, out _, out boat);
            Assert.AreEqual(0, boat, "nobody rows out in that");
            // the danger of a trip grows with the wind it begins in
            Assert.AreEqual(0.0, Boats.Risk(Boats.Calm));
            Assert.Less(Boats.Risk(8.0), Boats.Risk(11.0));
            Assert.AreEqual(Boats.CaughtOut, Boats.Risk(Boats.Max), 1e-12);
            Assert.AreEqual(0, Boats.Trip(w, sid, hid, bid, fisher, 3.0, out _), "a calm sea is safe");
        }

        [Test]
        public void ABoatNotMadeInAYearIsGivenUp()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Boats.Begin(w, sid, hid);   // (no hands came to it)
            w.Settlements.Get(sid).Woodstore = 0;   // (and no timber in store for another)
            Boats.Plan(w, sid);
            Assert.AreEqual(bid, Boats.Making(w, hid), "still being made within its year");
            w.Buildings.Get(bid).Built = w.Now - SimTime.Year - SimTime.Day;   // (premise: laid down a year and a day ago)
            long given0 = w.Metrics.BoatsGivenUp;
            Boats.Plan(w, sid);
            Assert.AreEqual(0, w.Households.Get(hid).Boat, "given up");
            Assert.AreEqual(given0 + 1, w.Metrics.BoatsGivenUp);
            Assert.AreNotEqual(0L, w.Buildings.Get(bid).Left, "the half-hollowed trunk left");
            Assert.AreEqual(0, w.Buildings.Get(bid).Done);
        }

        [Test]
        public void ADugoutSplitsWithAge()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Made(w, sid, hid, fisher);
            Assert.AreEqual(bid, Boats.Of(w, hid));
            w.Buildings.Get(bid).Built = w.Now - (long)((Boats.Years + 1) * SimTime.Year);   // (premise: made thirteen years ago)
            long worn0 = w.Metrics.BoatsWorn;
            Assert.AreEqual(0, Boats.Of(w, hid), "split with age");
            Assert.AreEqual(worn0 + 1, w.Metrics.BoatsWorn);
            Assert.AreEqual(0, w.Households.Get(hid).Boat);
            Assert.AreNotEqual(0, w.Buildings.Get(bid).Left, "a hulk on the beach");
            Assert.IsTrue(Boats.Wants(w, in w.Settlements.Get(sid), hid), "they would make another");
        }

        [Test]
        public void AnOverturnedBoatIsLostWithItsPeopleInTheWater()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Made(w, sid, hid, fisher);
            long built = w.Buildings.Get(bid).Event;
            long mark = w.History.LastId, deaths0 = w.Metrics.Deaths;
            bool drowned = Boats.Overturned(w, sid, hid, bid, fisher, 11.0, out long ev);
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(0f, b.Cond, "lost");
            Assert.AreNotEqual(0, b.Left);
            Assert.AreEqual(0, w.Households.Get(hid).Boat);
            Assert.AreEqual(0, Boats.Of(w, hid));
            Assert.IsTrue(w.History.TryGet(ev, out var lost));
            Assert.AreEqual(HistoryKind.BoatLost, lost.Kind);
            Assert.AreEqual(built, lost.Cause1, "the boat's own story");
            Console.WriteLine(HistoryText.Line(w, lost));
            Assert.AreEqual(!drowned, w.People.Get(fisher).Alive);
            Assert.AreEqual(drowned ? deaths0 + 1 : deaths0, w.Metrics.Deaths);
            if (drowned) { Assert.IsTrue(w.History.TryGet(w.People.Get(fisher).DeathEvent, out var died)); Assert.AreEqual(12, died.ArgA, "drowned at sea"); }
        }

        [Test]
        public void BoatsLieOnDrySandByTheWater()
        {
            // the ground fished may lie cells out from the shore (ten seeds showed boats laid in the square, and in the sea when the square
            // was): the landing is the nearest dry sand to the shore nearest that ground; a boat left away from the water follows the coast
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            var P = w.Planet; var g = P.Grid;
            ref var s = ref w.Settlements.Get(sid);
            // premise: the village fishes a ground of open water with no land among its neighbours (the farthest such of the sea's reach)
            int far = -1; double fd = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c)) continue;
                bool open = true;
                for (int j = 0; j < 8 && open; j++) if (P.IsLand(g.Nbr8[c * 8 + j])) open = false;
                if (!open) continue;
                double d = Vec3d.ArcDistance(g.Center[c], s.Anchor, P.Radius);
                if (d < WildFood.SeaReach && d > fd) { fd = d; far = c; }
            }
            Assume.That(far >= 0, "open water within a boat's reach");
            s.Fish.A = far;
            Assert.IsTrue(Boats.Landing(w, sid, out float x, out float y), "a landing found");
            Assert.IsTrue(Boats.AtWater(w, in w.Settlements.Get(sid), x, y), "dry, by the water");
            Console.WriteLine("ground " + (fd / 1000.0).ToString("0.0") + " km out: landing at " + x.ToString("0") + "," + y.ToString("0"));
            // a boat lying on dry land away from the water (where the sea drew back) is drawn down to the shore when the coast moves
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Made(w, sid, hid, fisher);
            ref var s2 = ref w.Settlements.Get(sid);
            float ix = s2.SquareX, iy = s2.SquareY;
            for (int k = 0; k < 40 && Boats.AtWater(w, in s2, ix, iy); k++) { ix += 10f; }   // (a spot inland of the beach)
            Assume.That(!Boats.AtWater(w, in s2, ix, iy) && Shore.Dry(P, s2.LocalToSphere(ix, iy, P.Radius)), "dry ground away from the water");
            w.Buildings.Get(bid).X = ix; w.Buildings.Get(bid).Y = iy;
            Boats.ShoreMoved(w);
            var b = w.Buildings.Get(bid);
            Assert.IsTrue(Boats.AtWater(w, in w.Settlements.Get(sid), b.X, b.Y), "drawn down to the water's edge");
        }

        [Test]
        public void AVillageKeepsOneLandingItsBoatsInARow()
        {
            // C9.2: the landing is the beach nearest the village's square, chosen with its first boat and kept; its boats lie along it
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            var P = w.Planet; var g = P.Grid;
            Assert.AreEqual(0L, w.Settlements.Get(sid).LandAt, "no landing before a boat");
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3, "three households");
            int b1 = Boats.Begin(w, sid, hl[0]);
            ref var s = ref w.Settlements.Get(sid);
            Assert.AreNotEqual(0L, s.LandAt, "chosen with the first boat");
            Assert.IsTrue(Boats.AtWater(w, in s, s.LandX, s.LandY), "the landing is by the water, out of it");
            long at = s.LandAt; float lx = s.LandX, ly = s.LandY;
            // the beach nearest the square: no sea within reach lies much nearer the square than the landing does
            Vec3d sq = s.LocalToSphere(s.SquareX, s.SquareY, P.Radius);
            double nearest = double.MaxValue;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c)) continue;
                double d = Vec3d.ArcDistance(g.Center[c], sq, P.Radius);
                if (d < nearest) nearest = d;
            }
            double ldx = lx - s.SquareX, ldy = ly - s.SquareY, land = Math.Sqrt(ldx * ldx + ldy * ldy);
            Console.WriteLine("landing " + land.ToString("0") + " m from the square; the nearest sea cell's centre " + nearest.ToString("0") + " m");
            Assert.Less(land, nearest + 1.0 * P.Grid.MeanSpacing * P.Radius, "the beach nearest the square");
            w.AdvanceTo(w.Now + 3 * SimTime.Day);
            // (a dozen boats: the row runs along the beach, every one by the water)
            var bl = new List<int> { b1 };
            for (int i = 1; i < hl.Count && bl.Count < 12; i++) bl.Add(Boats.Begin(w, sid, hl[i]));
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreEqual(at, s2.LandAt, "kept");
            int[] bs = bl.ToArray();
            for (int i = 0; i < bs.Length; i++)
            {
                var b = w.Buildings.Get(bs[i]);
                Assert.IsTrue(Boats.AtWater(w, in s2, b.X, b.Y), "boat " + i + " on the sand by the water");
                double dx = b.X - lx, dy = b.Y - ly;
                Assert.Less(Math.Sqrt(dx * dx + dy * dy), 60.0, "boat " + i + " at the landing");
                for (int j = 0; j < i; j++)
                {
                    var o = w.Buildings.Get(bs[j]);
                    double ex = b.X - o.X, ey = b.Y - o.Y;
                    Assert.GreaterOrEqual(Math.Sqrt(ex * ex + ey * ey), 2.4, "boats " + j + " and " + i + " side by side, not on each other");
                }
            }
        }

        [Test]
        public void ABoatGoesOutWithItsCrew()
        {
            // C9.2 (lived): the one who takes the household's boat out is joined by those of it at leisure - one trip, the same ground, back
            // together; a boat out at sea is not taken out again, and it never carries more than its seats
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            int bid = Made(w, sid, hid, fisher);
            w.Settlements.Get(sid).Ration = 0.6;   // (premise: rations are short - the sea is worth going out to)
            long out0 = w.Metrics.BoatOutings, seats0 = w.Metrics.CrewSeats;
            int most = 0, hoursOut = 0;
            for (int h = 0; h < 8 * 24; h++)
            {
                w.AdvanceTo(w.Now + SimTime.Hour);
                var mem = w.Members(hid);
                int n = 0; long t1 = 0; float tx = 0, ty = 0;
                for (int j = 0; j < mem.Count; j++)
                {
                    var q = w.People.Get(mem[j]);
                    if (!q.Alive || q.Act != ActivityKind.Sail || q.WhyArg != bid) continue;
                    if (n == 0) { t1 = q.T1; tx = q.ToX; ty = q.ToY; }
                    else
                    {
                        Assert.AreEqual(t1, q.T1, "back together");
                        Assert.AreEqual(tx, q.ToX, 1e-3f, "to the same ground");
                        Assert.AreEqual(ty, q.ToY, 1e-3f);
                    }
                    n++;
                }
                Assert.LessOrEqual(n, Boats.Seats, "no more than its seats");
                if (n > 0) hoursOut++;
                if (n > most) most = n;
            }
            long outings = w.Metrics.BoatOutings - out0, crew = w.Metrics.CrewSeats - seats0;
            Console.WriteLine("lived: " + outings + " outings, " + crew + " crew seats beside the ones who took it out; most aboard at once " + most + "; hours with the boat out " + hoursOut);
            Assert.Greater(outings, 0, "the boat went out");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ShoreFishersStandOnTheShoreNeverInTheSea()
        {
            // a village's fishing grounds may lie cells out from its shore: those who fish from the shore stand at the water's edge (shots of
            // a year-300 world showed fifteen of them standing up to 100 m out in the sea, at the grounds' centres)
            int villages = 0, spots = 0, afar = 0;
            for (ulong seed = 701; seed < 709; seed++)
            {
                var w = TestWorlds.Make(seed);
                var P = w.Planet;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    ref var s = ref w.Settlements.Get(sid);
                    if (!s.Inhabited || s.Fish.A < 0) continue;
                    villages++;
                    int[] grounds = { s.Fish.A, s.Fish.B, s.Fish.C };
                    var rs = new RandomSequence(seed * 131 + (ulong)sid);
                    foreach (int ground in grounds)
                    {
                        if (ground < 0) continue;
                        bool open = true;
                        for (int j = 0; j < 8 && open; j++) if (P.IsLand(P.Grid.Nbr8[ground * 8 + j])) open = false;
                        if (open) afar++;
                        for (int k = 0; k < 20; k++)
                        {
                            w.Wild.GroundPoint(sid, ground, true, ref rs, out double x, out double y);
                            Assert.IsFalse(Boats.Afloat(P, w.Settlements.Get(sid).LocalToSphere(x, y, P.Radius)), "seed " + seed + " village " + sid + " ground " + ground + ": standing in the sea");
                            spots++;
                        }
                    }
                }
            }
            Console.WriteLine(villages + " fishing villages, " + spots + " spots on the shore; grounds with no land round them " + afar);
            Assert.Greater(spots, 0);
        }

        [Test]
        public void WhereTheSeaTookTheSquarePeopleGatherOnDryGround()
        {
            // 0-5 villages a world have their square under the sea by year 300: their people at leisure, children at play and visitors gathered
            // there, standing out in the sea (shots of a drowned town). Premise: the square moved onto the sea by the village
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village");
            var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            double wx = double.NaN, wy = 0;
            for (double r = 20.0; r <= 600.0 && double.IsNaN(wx); r += 10.0)
                for (int k = 0; k < 36 && double.IsNaN(wx); k++)
                {
                    double qx = s.SquareX + r * Math.Cos(k * Math.PI / 18.0), qy = s.SquareY + r * Math.Sin(k * Math.PI / 18.0);
                    if (Boats.AtSea(P, s.LocalToSphere(qx, qy, P.Radius)) && Boats.AtSea(P, s.LocalToSphere(qx + 12, qy + 12, P.Radius)) && Boats.AtSea(P, s.LocalToSphere(qx - 12, qy - 12, P.Radius))) { wx = qx; wy = qy; }
                }
            Assume.That(!double.IsNaN(wx), "the sea near the village");
            s.SquareX = (float)wx; s.SquareY = (float)wy;
            int seen = 0;
            for (int h = 0; h < 3 * 24; h++)
            {
                w.AdvanceTo(w.Now + SimTime.Hour);
                var res = w.Residents(sid);
                ref var s2 = ref w.Settlements.Get(sid);
                for (int i = 0; i < res.Count; i++)
                {
                    var q = w.People.Get(res[i]);
                    if (!q.Alive || q.At != sid || q.Step != StepKind.Local) continue;
                    if (q.Act != ActivityKind.Social && q.Act != ActivityKind.Play && q.Act != ActivityKind.Visit && q.Act != ActivityKind.Fish) continue;
                    seen++;
                    Assert.IsFalse(Boats.Afloat(P, s2.LocalToSphere(q.ToX, q.ToY, P.Radius)), q.Act + " in the sea, hour " + h);
                }
            }
            Console.WriteLine(seen + " person-hours at leisure, play, visits or fishing from the shore - none in the sea");
            Assert.Greater(seen, 0);
        }

        [Test]
        public void BoatsGoOutAlikeAtBothResolutions() => GoOutAlike(Structures.Boat);

        [Test]
        public void PlankBoatsGoOutAlikeAtBothResolutions() => GoOutAlike(Structures.PlankBoat);

        static void GoOutAlike(byte kind)
        {
            // pooled over worlds (C9.2): every household of the coast's villages that lives by the fish given a dugout, the same days lived
            // hour by hour and as COARSE days - the boats' trips, their crews and their catch a grown person-day at par
            double tripsA = 0, tripsB = 0, kgA = 0, kgB = 0, crewA = 0, crewB = 0, outA = 0, outB = 0, adA = 0, adB = 0, fishA = 0, fishB = 0;
            for (ulong seed = 701; seed < 706; seed++)
            {
                foreach (bool coarse in new[] { false, true })
                {
                    var w = TestWorlds.Make(seed);
                    if (coarse) { w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day); w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hours(1)); }
                    else w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hours(1));
                    int boats = 0;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    {
                        ref var s = ref w.Settlements.Get(sid);
                        if (!s.Inhabited || s.Fish.A < 0 || s.Sea.A < 0) continue;
                        var hl = w.HouseholdsOf(sid);
                        for (int i = 0; i < hl.Count; i++)
                        {
                            var mem = w.Members(hl[i]);
                            int fisher = 0;
                            for (int j = 0; j < mem.Count && fisher == 0; j++) { var q = w.People.Get(mem[j]); double a = q.AgeYears(w.Now); if (q.Alive && a >= 16 && a < 60 && q.SkFish >= Boats.FisherSkill) fisher = mem[j]; }
                            if (fisher == 0 || w.Households.Get(hl[i]).Boat != 0) continue;
                            Made(w, sid, hl[i], fisher, kind);
                            boats++;
                        }
                    }
                    long t0 = w.Metrics.BoatTrips, c0 = w.Metrics.CrewSeats, o0 = w.Metrics.BoatOutings; double k0 = w.Metrics.BoatFishKg, f0 = w.Metrics.FishKg;
                    double ad = 0;
                    for (int d = 0; d < 24; d++)
                    {
                        w.AdvanceTo(w.Now + SimTime.Day);
                        for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) ad += Adults(w, sid);
                    }
                    double trips = w.Metrics.BoatTrips - t0, kg = w.Metrics.BoatFishKg - k0, crew = w.Metrics.CrewSeats - c0, outs = w.Metrics.BoatOutings - o0;
                    Console.WriteLine("seed " + seed + (coarse ? " COARSE" : " lived ") + ": " + boats + " " + Boats.NameOf(kind) + "s; outings " + outs + ", trips " + trips + " (crew seats " + crew + "), " + kg.ToString("0") + " kg, " + ad.ToString("0") + " grown person-days");
                    double fish = w.Metrics.FishKg - f0;
                    if (coarse) { tripsB += trips; kgB += kg; crewB += crew; outB += outs; adB += ad; fishB += fish; } else { tripsA += trips; kgA += kg; crewA += crew; outA += outs; adA += ad; fishA += fish; }
                }
            }
            Console.WriteLine("lived: outings " + (outA / adA * 1000).ToString("0.0") + ", trips " + (tripsA / adA * 1000).ToString("0.0") + ", crew seats " + (crewA / adA * 1000).ToString("0.0") + ", kg " + (kgA / adA).ToString("0.000") + " a grown person-day (x1000 for counts)");
            Console.WriteLine("COARSE: outings " + (outB / adB * 1000).ToString("0.0") + ", trips " + (tripsB / adB * 1000).ToString("0.0") + ", crew seats " + (crewB / adB * 1000).ToString("0.0") + ", kg " + (kgB / adB).ToString("0.000"));
            Console.WriteLine("all fish: lived " + (fishA / adA).ToString("0.000") + ", COARSE " + (fishB / adB).ToString("0.000") + " kg a grown person-day");
            Assert.Greater(tripsA, 0); Assert.Greater(tripsB, 0);
            Assert.Greater(crewA, 0, "lived: boats went out with crews");
            // (a COARSE trip is the day's hours in the wild, often an hour or two; a lived one 3.5-5 h: trips are not compared, the catch is.
            // C9.1's rule - a second of the household taking the boat out hours after the first - gave lived days half the COARSE catch, x1.92)
            Assert.That((kgB / adB) / (kgA / adA), Is.InRange(0.6, 1.6), "the same fish from boats at both resolutions");
            Assert.That((fishB / adB) / (fishA / adA), Is.InRange(0.6, 1.6), "and the same fish in all");
        }

        static int Adults(World w, int sid)
        {
            int n = 0; var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.AgeYears(w.Now) >= 16) n++; }
            return n;
        }

        [Test]
        public void AWoodworkerHollowingADugoutFindsHowToBuildWithPlanks()
        {
            // C9.2b: boats of planks are found by those who practise their making - a woodworker at a dugout - where nobody knows them
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown = (byte)(s.TechKnown & ~Boats.Bit); s.BestBoat = 0f;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) w.People.Get(res[i]).KBoat = 0f;
            int other = 0;
            for (int i = 0; i < res.Count && other == 0; i++) if (res[i] != fisher && w.People.Get(res[i]).AgeYears(w.Now) >= 16) other = res[i];
            Assume.That(other != 0, "another grown person");
            w.People.Get(other).SkWood = 0.1f;   // (no woodworker)
            w.Learning.Practice(ref w.People.Get(other), TC2.Core.Knowledge.Tech.Boat, 1e6);
            Assert.Less(w.People.Get(other).KBoat, 0.1f, "no woodworker's hand: nothing found");
            w.People.Get(fisher).SkWood = 0.8f;
            long mark = w.History.LastId;
            w.Learning.Practice(ref w.People.Get(fisher), TC2.Core.Knowledge.Tech.Boat, 1e6);   // (hours enough that the keyed chance passes one)
            Assert.AreEqual((float)TC2.Core.Knowledge.Tech.FirstFound, w.People.Get(fisher).KBoat, 1e-6f, "a woodworker at a dugout finds it");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.TechniqueFound && w.History.At(i).ArgA == TC2.Core.Knowledge.Tech.Boat) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // a dugout's making is that practice (both resolutions call it through Boats.Work)
            w.Settlements.Get(sid).Woodstore += Boats.Wood;
            int bid = Boats.Begin(w, sid, hid);
            double used = Boats.Work(w, sid, hid, 5.0, 1.0, fisher);
            Assert.Greater(used, 0.0);
            Assert.AreEqual(Structures.Boat, w.Buildings.Get(bid).Kind, "without a shed and the craft known, a dugout");
        }

        [Test]
        public void AVillageThatBuildsWithPlanksRaisesABoatShedAndBuildsThemThere()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            Knows(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.ProjectKind = 0; s.Wall = 0;
            Assume.That((s.Works & Construction.Granary) != 0, "water and a store first");
            Assert.AreEqual(Structures.Boat, Boats.WantsKind(w, in w.Settlements.Get(sid), hid), "no shed yet: a dugout would do");
            Assert.IsTrue(Boats.ShedWanted(w, sid), "a boat wanted, and the craft known: a shed to build it in");
            Structures.Plan(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreEqual(Structures.BoatShed, s2.ProjectKind, "the boat shed begun");
            int shed = s2.Project;
            var sh = w.Buildings.Get(shed);
            double dx = sh.X - s2.LandX, dy = sh.Y - s2.LandY;
            Console.WriteLine("the shed " + Math.Sqrt(dx * dx + dy * dy).ToString("0") + " m from the landing");
            Assert.Less(Math.Sqrt(dx * dx + dy * dy), 75.0, "at the head of the landing");
            Assert.IsTrue(Shore.Dry(w.Planet, s2.LocalToSphere(sh.X, sh.Y, w.Planet.Radius)), "on dry ground");
            Structures.Raised(w, sid, shed, Boats.ShedHours, Boats.ShedWood, fisher);
            Assert.AreEqual(shed, w.Settlements.Get(sid).Shed, "standing");
            // now the household builds a plank boat there - one on the stocks at a time
            Assert.AreEqual(Structures.PlankBoat, Boats.WantsKind(w, in w.Settlements.Get(sid), hid));
            Boats.Plan(w, sid);
            int bid = Boats.Making(w, hid);
            Assume.That(bid != 0, "this household was the first to want a boat");
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.PlankBoat, b.Kind);
            Assert.AreEqual(sh.X, b.X, 1e-3f, "on the stocks in the shed"); Assert.AreEqual(sh.Y, b.Y, 1e-3f);
            Assert.AreEqual(bid, Boats.OnTheStocks(w, sid));
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (hl[i] != hid) Assert.AreEqual(0, Boats.WantsKind(w, in w.Settlements.Get(sid), hl[i]), "the others wait for the stocks");
            // a learner works at a fifth of a master's pace
            int learner = 0;
            var mem = w.Members(hid);
            for (int j = 0; j < mem.Count && learner == 0; j++) if (mem[j] != fisher && w.People.Get(mem[j]).AgeYears(w.Now) >= 14) learner = mem[j];
            w.Settlements.Get(sid).Woodstore += 100;
            if (learner != 0)
            {
                w.People.Get(learner).KBoat = 0.02f;
                double h0 = w.Buildings.Get(bid).Hours;
                Boats.Work(w, sid, hid, 10.0, 1.0, learner);
                Assert.AreEqual(10.0 * Boats.Learner, w.Buildings.Get(bid).Hours - h0, 1e-3, "a learner's pace");
            }
            long mark = w.History.LastId;
            Boats.Work(w, sid, hid, 1000.0, 1.0, fisher);
            var done = w.Buildings.Get(bid);
            Assert.AreEqual(1, done.Done, "built");
            Assert.IsTrue(Boats.AtWater(w, in w.Settlements.Get(sid), done.X, done.Y), "off the stocks and down to the landing");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.BoatBuilt) { told = true; Assert.AreEqual(Structures.PlankBoat, w.History.At(i).ArgA); Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            Assert.AreEqual(bid, Boats.Of(w, hid));
        }

        [Test]
        public void APlankBoatSeatsFourRidesOutHarderWeatherLastsLongerAndReachesMoreOfTheShoals()
        {
            Assert.AreEqual(4, Boats.SeatsOf(Structures.PlankBoat)); Assert.AreEqual(2, Boats.SeatsOf(Structures.Boat));
            Assert.Greater(Boats.MaxOf(Structures.PlankBoat), Boats.MaxOf(Structures.Boat), "out in winds that keep dugouts ashore");
            Assert.Less(Boats.Risk(Boats.Max, Structures.PlankBoat), Boats.Risk(Boats.Max, Structures.Boat), "safer in the same wind");
            Assert.AreEqual(0.0, Boats.Risk(Boats.Calm, Structures.PlankBoat));
            var w1 = Coast(out int sid); var w2 = Coast(out int sid2);
            Assume.That(w1 != null && sid == sid2, "a fishing village");
            int hid = Fishers(w1, sid, out int fisher);
            Assume.That(hid != 0, "a household that fishes");
            Knows(w1, sid);
            int bid = Made(w1, sid, hid, fisher, Structures.PlankBoat);
            Assert.AreEqual(bid, Boats.Of(w1, hid));
            w1.Buildings.Get(bid).Built = w1.Now - (long)(20 * SimTime.Year);   // (premise: built twenty years ago)
            Assert.AreEqual(bid, Boats.Of(w1, hid), "a plank boat still sound at twenty years (a dugout splits at twelve)");
            int ground = w1.Settlements.Get(sid).Sea.A;
            Assert.GreaterOrEqual(w1.Wild.ExpectSea(ground, true), w1.Wild.ExpectSea(ground), "an hour from a plank boat is worth at least a dugout's");
            double plank = w1.Wild.Fish(ground, 4.0, 0.5, 777UL, 1, true, true), dug = w2.Wild.Fish(ground, 4.0, 0.5, 777UL, 1, true, false);
            Console.WriteLine("plank boat " + plank.ToString("0.00") + " FU, dugout " + dug.ToString("0.00") + " FU at the same ground");
            Assert.GreaterOrEqual(plank, dug);
        }

        [Test]
        public void BothResolutionsFishFromTheBoat()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = Coast(out int sid);
                Assume.That(w != null, "a fishing village with promising open sea");
                if (coarse) { w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day); w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hours(1)); }
                int hid = Fishers(w, sid, out int fisher);
                Assume.That(hid != 0, "a household that fishes");
                Made(w, sid, hid, fisher);
                ref var s = ref w.Settlements.Get(sid);
                s.Ration = 0.6;   // (premise: rations are short - the shore and the sea must make up the rest)
                long trips0 = w.Metrics.BoatTrips; double kg0 = w.Metrics.BoatFishKg;
                {
                    ref var pf = ref w.People.Get(fisher);
                    int k0 = w.Agents.WildChoice(in pf, in w.Settlements.Get(sid), pf.AgeYears(w.Now), w.Now, out double e0, out int b0);
                    Console.WriteLine((coarse ? "COARSE" : "lived") + " at the start: the fisher's wild choice " + k0 + " by boat " + b0 + " expecting " + e0.ToString("0.00") + "; sea " + Boats.SeaExpect(w, in w.Settlements.Get(sid)).ToString("0.00") + ", shore " + w.Settlements.Get(sid).Fish.Expect.ToString("0.00"));
                }
                var acts = new Dictionary<ActivityKind, int>();
                for (int h = 0; h < 6 * 24; h++)
                {
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    var mem = w.Members(hid);
                    for (int j = 0; j < mem.Count; j++) { var q = w.People.Get(mem[j]); if (q.Alive && q.AgeYears(w.Now) >= 16) acts[q.Act] = acts.TryGetValue(q.Act, out int n) ? n + 1 : 1; }
                }
                var line = new System.Text.StringBuilder();
                foreach (var kv in acts) line.Append(kv.Key).Append(' ').Append(kv.Value).Append("  ");
                Console.WriteLine("  the household's grown members, hour by hour: " + line);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": " + (w.Metrics.BoatTrips - trips0) + " trips out in boats, " + (w.Metrics.BoatFishKg - kg0).ToString("0.0") + " kg of fish from them");
                Assert.Greater(w.Metrics.BoatTrips - trips0, 0, (coarse ? "COARSE" : "lived") + ": out in the boat");
                Assert.Greater(w.Metrics.BoatFishKg - kg0, 0.0);
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
