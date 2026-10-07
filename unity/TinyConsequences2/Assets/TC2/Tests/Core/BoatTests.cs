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
        static int Made(World w, int sid, int hid, int worker)
        {
            int bid = Boats.Begin(w, sid, hid);
            w.Settlements.Get(sid).Woodstore += Boats.Wood;
            Boats.Work(w, sid, hid, Boats.Hours + 1.0, 1.0, worker);
            return bid;
        }

        [Test]
        public void AHouseholdThatFishesMakesADugoutFromItsWoods()
        {
            var w = Coast(out int sid);
            Assume.That(w != null, "a fishing village with promising open sea");
            int hid = Fishers(w, sid, out int fisher);
            Assume.That(hid != 0 && Woods.Lot(in w.Settlements.Get(sid)) >= 0, "a household that fishes; woods within reach");
            Assert.IsTrue(Boats.Wants(w, in w.Settlements.Get(sid), hid), "it would make a boat");
            Boats.Plan(w, sid);
            int bid = w.Households.Get(hid).Boat;
            Assume.That(bid != 0, "this household was the first of the village to want one");
            Assert.AreEqual(0, w.Buildings.Get(bid).Done, "laid down, not yet made");
            Assert.AreEqual(Structures.Boat, w.Buildings.Get(bid).Kind);
            Assert.IsFalse(Boats.Wants(w, in w.Settlements.Get(sid), hid), "one at a time");
            double wood0 = w.Settlements.Get(sid).Woodstore;
            long mark = w.History.LastId;
            double used = Boats.Work(w, sid, hid, 200.0, 1.0, fisher);
            Assert.AreEqual(Boats.Hours, used, 0.02, "its hours");
            Assert.AreEqual(Boats.Wood, wood0 - w.Settlements.Get(sid).Woodstore, 0.02, "the trunk from the store's timber");
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
            int bid = Boats.Begin(w, sid, hid);
            w.Settlements.Get(sid).Woodstore = 0;   // (no timber came)
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
