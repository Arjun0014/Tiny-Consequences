using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The weaver's shed and the village's wares (C3.1, D2-119): raised only where a village's wear of goods would keep several makers at
    /// its looms; its makers are the village's best weavers and make wares faster there, from real fibre only; households take wares before
    /// making their own, and wares last longer than homespun; wares go to the villages that lack them, and what is sold is made again.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class WeavingTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Premise: the village weaves on the loom (its grown people competent, its best a master).</summary>
        static void Weavers(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.AgeYears(w.Now) >= 16) p.KLoom = 0.8f; }
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown |= (byte)Weaving.Bit; s.BestLoom = 0.8f;
        }

        static int StandingShed(World w, int sid)
        {
            Structures.Place(w, sid, Structures.Workshop, out float x, out float y);
            return Structures.Standing(w, sid, Structures.Workshop, x, y);
        }

        [Test]
        public void AWeaversShedIsRaisedOnlyWhereThePeoplesWearKeepsItsLoomsBusy()
        {
            var w = TestWorlds.Make(611);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            Weavers(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.Wall = 0;
            Assert.IsFalse(Weaving.ShedWanted(w, in w.Settlements.Get(sid)), "a village of " + s.Population + ": its wear keeps no makers busy");
            double from = Weaving.MakersFrom * Weaving.MakerDay(in s) / (Goods.WearPerDay * Tech.ToolWear(in s));
            Console.WriteLine("a shed from " + from.ToString("0") + " people (its best weaver " + s.BestLoom.ToString("0.00") + ", tools wear " + Tech.ToolWear(in s).ToString("0.00") + ")");
            Assert.That(from, Is.InRange(100.0, 300.0), "larger places only");
            int pop0 = s.Population;
            s.Population = (int)from + 5;   // premise: the village has grown (the planner reads its people)
            Assert.IsTrue(Weaving.ShedWanted(w, in w.Settlements.Get(sid)));
            Structures.Plan(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreNotEqual(0, s2.Project, "a work begun");
            var b = w.Buildings.Get(s2.Project);
            Assert.AreEqual(Structures.Workshop, b.Kind, "a weaver's shed");
            Assert.AreEqual(s2.LoomEvent != 0 ? s2.LoomEvent : s2.FoundedEvent, b.Cause, "enabled by their weaving");
            double r = Math.Sqrt(((double)b.X - s2.SquareX) * ((double)b.X - s2.SquareX) + ((double)b.Y - s2.SquareY) * ((double)b.Y - s2.SquareY));
            Assert.Greater(r, Structures.HomesReach(w, sid), "at the edge of the homes, on the road");
            Console.WriteLine(HistoryText.WorkWhy(Structures.Workshop, 0));
            s2.Population = pop0;
            // no loom: no shed, however large
            var w2 = TestWorlds.Make(611);
            int sid2 = w2.Settlements.First();
            ref var t = ref w2.Settlements.Get(sid2);
            t.TechKnown = (byte)(t.TechKnown & ~Weaving.Bit); t.Population = 1000; t.Project = 0;
            Assert.IsFalse(Weaving.ShedWanted(w2, in w2.Settlements.Get(sid2)), "nobody weaves on the loom: no shed");
            // walls rising first
            ref var s3 = ref w.Settlements.Get(sid); s3.Population = (int)from + 5; s3.Workshop = 0; s3.Wall = 1;
            Assert.IsFalse(Weaving.ShedWanted(w, in w.Settlements.Get(sid)), "never while walls rise");
            s3.Population = pop0; s3.Wall = 0;
        }

        [Test]
        public void TheVillagesBestWeaversHaveTheLooms()
        {
            var w = TestWorlds.Make(612);
            int sid = w.Settlements.First();
            Weavers(w, sid);
            // premise: grown weavers of varied hands - more of them than the shed has looms
            var able = new List<int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); double a = p.AgeYears(w.Now); if (a >= 16 && a < 60) able.Add(res[i]); }
            Assume.That(able.Count, Is.GreaterThan(Weaving.Looms + 1), "more able weavers than looms");
            for (int i = 0; i < able.Count; i++) { ref var p = ref w.People.Get(able[i]); p.KLoom = (float)(0.55 + 0.4 * ((i * 7) % able.Count) / able.Count); }
            ref var tie = ref w.People.Get(able[able.Count - 1]); tie.KLoom = w.People.Get(able[0]).KLoom;   // (a tie: the lower id first)
            StandingShed(w, sid);
            var s = w.Settlements.Get(sid);
            var keys = new List<(double k, int id)>();
            foreach (int id in able) keys.Add((Weaving.Key(in w.People.Get(id)), id));
            keys.Sort((x, y) => x.k != y.k ? y.k.CompareTo(x.k) : x.id.CompareTo(y.id));
            int makers = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                bool m = Weaving.Maker(in s, in w.People.Get(keys[i].id), w.Now);
                Assert.AreEqual(i < Weaving.Looms, m, "the " + (i + 1) + "th best weaver " + (i < Weaving.Looms ? "has" : "has no") + " loom");
                if (m) makers++;
            }
            Assert.AreEqual(Weaving.Looms, makers);
            // practice keeps a loom: the ninth best works at a shed elsewhere for a long while and passes the eighth
            ref var ninth = ref w.People.Get(keys[Weaving.Looms].id);
            ninth.SkCraft = 0.9f;
            Weaving.Bar(w, sid);
            Assert.IsTrue(Weaving.Maker(in w.Settlements.Get(sid), in w.People.Get(keys[Weaving.Looms].id), w.Now), "a practised hand takes a loom");
            Assert.IsFalse(Weaving.Maker(in w.Settlements.Get(sid), in w.People.Get(keys[Weaving.Looms - 1].id), w.Now), "and the weakest gives it up");
        }

        [Test]
        public void AtTheShedWaresAreMadeFasterFromRealFibreOnly()
        {
            var w = TestWorlds.Make(613);
            int sid = w.Settlements.First();
            Weavers(w, sid);
            int shed = StandingShed(w, sid);
            int maker = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count && maker == 0; i++) if (Weaving.Maker(in w.Settlements.Get(sid), in w.People.Get(res[i]), w.Now)) maker = res[i];
            Assert.AreNotEqual(0, maker, "a maker");
            ref var s = ref w.Settlements.Get(sid);
            s.Fibre = 500; s.Wares = 0; s.WaresSold = 100;   // premise: fibre in store, and buyers lately (the target well above a stint's making)
            ref var p = ref w.People.Get(maker);
            p.SkCraft = 0.5f;
            double rate = Goods.MakePerHour(p.AgeYears(w.Now), p.Health) * Tech.LoomFactor(in p);   // (at home)
            double used = Weaving.Work(w, ref p, sid, shed, 3.0);
            double made = w.Settlements.Get(sid).Wares;
            Assert.AreEqual(3.0, used, 1e-9);
            Assert.AreEqual(rate * Weaving.Pace * (0.8 + 0.4 * 0.5) * 3.0, made, 1e-9, "the shed's pace and the hand's practice");
            Assert.Greater(made, rate * 3.0 * 1.4, "half as fast again as at home");
            Assert.AreEqual(500 - made * Goods.MaterialPerGood, w.Settlements.Get(sid).Fibre, 1e-9, "2.5 kg of fibre a ware");
            Assert.Greater(w.People.Get(maker).SkCraft, 0.5f, "practice");
            Assert.AreEqual(3.0, w.Buildings.Get(shed).Use, 1e-4); Assert.AreEqual(made, w.Buildings.Get(shed).Stock, 1e-4);
            // no fibre: no cloth from bark and grass
            ref var s2 = ref w.Settlements.Get(sid); s2.Fibre = 0;
            Assert.IsFalse(Weaving.Use(in w.Settlements.Get(sid)), "nothing to weave");
            ref var p2 = ref w.People.Get(maker);
            Assert.AreEqual(0.0, Weaving.Work(w, ref p2, sid, shed, 3.0), "no wares without fibre");
            // the store at its target: no more
            ref var s3 = ref w.Settlements.Get(sid); s3.Fibre = 500; s3.WaresSold = 0; s3.Wares = Weaving.Target(in w.Settlements.Get(sid));
            Assert.IsFalse(Weaving.Use(in w.Settlements.Get(sid)), "the store holds what it keeps");
            ref var p3 = ref w.People.Get(maker);
            Assert.AreEqual(0.0, Weaving.Work(w, ref p3, sid, shed, 3.0));
        }

        [Test]
        public void WaresLastLongerThanHomespun()
        {
            var a = new Household { Gear = 4.8f, Wares = 0f };
            var b = new Household { Gear = 4.8f, Wares = 4.8f };
            var c = new Household { Gear = 4.8f, Wares = 2.4f };
            Goods.Wear(ref a, 4, 30, 1.0); Goods.Wear(ref b, 4, 30, 1.0); Goods.Wear(ref c, 4, 30, 1.0);
            double worn = 4 * Goods.WearPerDay * 30;
            Assert.AreEqual(4.8 - worn, a.Gear, 1e-5, "homespun at the law's rate");
            Assert.AreEqual(4.8 - worn * Weaving.WaresWear, b.Gear, 1e-5, "wares at a share of it");
            Assert.AreEqual(b.Gear, b.Wares, 1e-6);
            Assert.AreEqual(4.8 - worn * (0.5 + 0.5 * Weaving.WaresWear), c.Gear, 1e-5, "the mix they hold, worn in proportion");
            Assert.AreEqual(2.4 - 0.5 * worn * Weaving.WaresWear, c.Wares, 1e-5);
            var d = new Household { Gear = 0.01f, Wares = 0.01f };
            Goods.Wear(ref d, 4, 30, 1.0);
            Assert.AreEqual(0f, d.Gear); Assert.AreEqual(0f, d.Wares, "worn through");
        }

        [Test]
        public void HouseholdsTakeWaresBeforeMakingTheirOwn()
        {
            // two copies of a world whose goods are worn thin: a, wares in every village's store; b, none - a's households take them and make less
            var a = TestWorlds.Make(614); var b = TestWorlds.Make(614);
            int sid = a.Settlements.First();
            foreach (var w in new[] { a, b })
                for (int q = w.Settlements.First(); q != 0; q = w.Settlements.Next(q))
                {
                    var hhs = w.HouseholdsOf(q);
                    for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); hh.Gear = (float)(0.5 * w.Members(hhs[i]).Count); hh.Wares = 0f; }
                    if (w == a) { ref var sq = ref w.Settlements.Get(q); sq.Wares = 1.0 * sq.Population; }
                }
            double c0a = a.Metrics.CraftHours, c0b = b.Metrics.CraftHours;
            a.AdvanceTo(a.Now + SimTime.Days(3)); b.AdvanceTo(b.Now + SimTime.Days(3));
            var s2 = a.Settlements.Get(sid);
            double held = 0; var hl = a.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) held += a.Households.Get(hl[i]).Wares;
            Console.WriteLine("wares taken " + s2.WaresOut.ToString("0.0") + " (held " + held.ToString("0.0") + ", " + s2.Population + " people); making at home: with wares " + (a.Metrics.CraftHours - c0a).ToString("0") + " h, without " + (b.Metrics.CraftHours - c0b).ToString("0") + " h");
            Assert.Greater(s2.WaresOut, 0.5 * s2.Population, "they took wares from the store");
            Assert.AreEqual(s2.WaresHeld, held, 1e-3, "the village knows what its households hold");
            Assert.Less(a.Metrics.CraftHours - c0a, 0.5 * (b.Metrics.CraftHours - c0b), "hours freed: the household's hands make far less");
            Assert.IsNull(TestWorlds.Audit(a));
            // a household's wares go with its goods: to its heir, or a share with someone who leaves to form their own
            var hhs2 = a.HouseholdsOf(sid);
            for (int i = 0; i < hhs2.Count; i++) { var hh = a.Households.Get(hhs2[i]); Assert.LessOrEqual(hh.Wares, hh.Gear + 1e-4f, "never more wares than goods"); }
        }

        [Test]
        public void MakersWorkAtTheShedAtBothResolutions()
        {
            var perDay = new double[2];
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(615);
                int sid = w.Settlements.First();
                if (coarse) Coarse(w);
                Weavers(w, sid);
                int shed = StandingShed(w, sid);
                ref var s = ref w.Settlements.Get(sid);
                s.Fibre = 2000; s.Wares = 0; s.WaresSold = 60; s.Granary = 30 * s.Population; s.Woodstore = 600;   // premise: buyers lately (work for days), fibre, food and wood
                bool saw = false; int makerDays = 0;
                for (int h = 0; h < 6 * 24; h++)
                {
                    { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); sk.Fibre = 2000; if (sk.WaresSold < 60f) sk.WaresSold = 60f; }   // (the fields need no hands now; buyers keep coming)
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    if (h % 24 == 12) { var res0 = w.Residents(sid); for (int i = 0; i < res0.Count; i++) if (Weaving.Maker(in w.Settlements.Get(sid), in w.People.Get(res0[i]), w.Now)) makerDays++; }
                    if (!coarse && !saw) { var res = w.Residents(sid); for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Act == ActivityKind.Weave) { saw = true; Console.WriteLine(HistoryText.Doing(w, in w.People.Get(res[i]))); } }
                }
                var s2 = w.Settlements.Get(sid);
                perDay[coarse ? 0 : 1] = w.Metrics.ShedHours / Math.Max(1, makerDays);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": shed hours " + w.Metrics.ShedHours.ToString("0") + " (" + perDay[coarse ? 0 : 1].ToString("0.0") + " a maker-day), wares made " + w.Metrics.WaresMade.ToString("0.0") + ", taken " + w.Metrics.WaresDrawn.ToString("0.0") + ", in store " + s2.Wares.ToString("0.0"));
                Assert.Greater(w.Metrics.ShedHours, 10, "the makers worked at the shed");
                Assert.Greater(w.Metrics.WaresMade, 0.5, "wares made");
                if (!coarse) Assert.IsTrue(saw, "seen at the shed");
                Assert.IsNull(TestWorlds.Audit(w));
                // a save in the middle of it continues as the world does
                var bytes = SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, "");
                var w2 = SaveFile.Load(bytes, out _);
                w.AdvanceTo(w.Now + SimTime.Days(2)); w2.AdvanceTo(w2.Now + SimTime.Days(2));
                Assert.AreEqual(w.StateHash(), w2.StateHash(), "saved and loaded, the same world");
            }
            Assert.That(perDay[0] / Math.Max(1e-9, perDay[1]), Is.InRange(0.5, 2.0), "a COARSE maker's day at the shed near a lived one's");
        }

        [Test]
        public void ATownWhoseLoomsCannotKeepUpRaisesAnotherShed()
        {
            // (C3.1b) a village with a shed whose store its households drain while it holds fibre: its looms, not its flax, hold it back
            var w = TestWorlds.Make(617);
            int sid = w.Settlements.First();
            Weavers(w, sid);
            int first = StandingShed(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            Assert.AreEqual(1, s.Sheds); Assert.AreEqual(first, s.Workshop);
            s.Project = 0; s.Wall = 0;
            // fibre short: the flax holds it back - no day counts
            s.Wares = 0; s.WaresSold = 500f; s.Fibre = 0;
            for (int d = 0; d < 48; d++) Weaving.Tally(w, sid, 1);
            Assert.AreEqual(0, w.Settlements.Get(sid).ShedShort, "no flax: more looms would not help");
            // a store at what it keeps back from trade (traders drained it): not short - sales say nothing of the looms
            ref var s0 = ref w.Settlements.Get(sid); s0.Fibre = 5000; s0.Wares = Weaving.Kept * s0.Population;
            for (int d = 0; d < 48; d++) Weaving.Tally(w, sid, 1);
            Assert.AreEqual(0, w.Settlements.Get(sid).ShedShort, "a store drained by sales is not short");
            // drained by its own households, fibre for every loom: each short day counts; most of a year short wants another shed
            ref var s1 = ref w.Settlements.Get(sid); s1.Fibre = 5000; s1.Wares = 0;
            for (int d = 0; d < Weaving.ShortDays - 1; d++) Weaving.Tally(w, sid, 1);
            Weaving.Year(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).ShedMore, "short on fewer than the days it takes");
            Assert.AreEqual(0, w.Settlements.Get(sid).ShedShort, "the count starts again each year");
            for (int d = 0; d < 36; d++) Weaving.Tally(w, sid, 1);
            Weaving.Year(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).ShedMore, "short, but a village of " + w.Settlements.Get(sid).Population + " in wares would wear less than its looms make");
            ref var sg = ref w.Settlements.Get(sid);
            int pop0 = sg.Population;
            sg.Population = (int)(Weaving.LoomsOf(in sg) * Weaving.MakerDay(in sg) / (Goods.WearPerDay * Tech.ToolWear(in sg) * Weaving.WaresWear)) + 10;   // premise: a town grown past its looms
            Assert.IsTrue(Weaving.NeedsMore(in w.Settlements.Get(sid)));
            Console.WriteLine("a second shed from " + (sg.Population - 10) + " people");
            for (int d = 0; d < 36; d++) Weaving.Tally(w, sid, 1);
            Weaving.Year(w, sid);
            Assert.AreEqual(1, w.Settlements.Get(sid).ShedMore, "short most of the year, and outgrown its looms");
            Structures.Plan(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreNotEqual(0, s2.Project, "a work begun");
            var nb = w.Buildings.Get(s2.Project);
            Assert.AreEqual(Structures.Workshop, nb.Kind, "another weaver's shed");
            Assert.AreEqual(w.Buildings.Get(first).Event, nb.Cause, "because the first could not keep up");
            Assert.AreEqual(0, s2.ShedMore);
            int second = s2.Project;
            Structures.Raised(w, sid, second, Weaving.ShedHours, Weaving.ShedWood, 0);
            var s3 = w.Settlements.Get(sid);
            Assert.AreEqual(2, s3.Sheds); Assert.AreEqual(first, s3.Workshop, "the first stays the first");
            Assert.AreEqual(2 * Weaving.Looms, Weaving.LoomsOf(in s3), "eight more looms");
            // makers go to the shed nearest their home
            var res = w.Residents(sid);
            int toFirst = 0, toSecond = 0;
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                if (!Weaving.Maker(in s3, in p, w.Now)) continue;
                int at = Weaving.ShedFor(w, in s3, in p);
                var hh = w.Households.Get(p.Household);
                var b1 = w.Buildings.Get(first); var b2 = w.Buildings.Get(second);
                double d1 = (b1.X - hh.HomeX) * (b1.X - hh.HomeX) + (b1.Y - hh.HomeY) * (b1.Y - hh.HomeY), d2 = (b2.X - hh.HomeX) * (b2.X - hh.HomeX) + (b2.Y - hh.HomeY) * (b2.Y - hh.HomeY);
                Assert.AreEqual(d1 <= d2 ? first : second, at, "the nearer shed");
                if (at == first) toFirst++; else toSecond++;
            }
            Console.WriteLine("makers to the first shed " + toFirst + ", to the second " + toSecond);
            // the first lost (the sea took it): the other carries on as the village's shed
            ref var bf = ref w.Buildings.Get(first); bf.Left = w.Now - SimTime.Years(200);
            Structures.Lost(w, first);
            var s4 = w.Settlements.Get(sid);
            Assert.AreEqual(1, s4.Sheds); Assert.AreEqual(second, s4.Workshop, "the second is the village's shed now");
            Assert.AreEqual(second, Weaving.Shed(w, sid));
            ref var se = ref w.Settlements.Get(sid); se.Population = pop0;
        }

        [Test]
        public void WaresGoWhereTheyAreLackedAndWhatIsSoldIsMadeAgain()
        {
            var w = TestWorlds.Make(616);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            Assume.That(b != 0, "two villages");
            ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
            // premise: a has wares to spare and is short of grain; b has grain to spare and its people wear no wares
            sa.Wares = 1.0 * sa.Population + 20; sa.WaresHeld = (float)(Goods.Target * sa.Population); sa.Granary = 5;
            sb.Wares = 0; sb.WaresHeld = 0f; sb.Granary = 200.0 * sb.Population;
            Assert.Greater(w.Trade.Value(b, Trade.Wares), 10.0 * w.Trade.Value(a, Trade.Wares), "wares are worth much where nobody wears them");
            Assert.Greater(w.Trade.Spare(a, Trade.Wares), 0.0);
            double gain = w.Trade.Plan(a, b, out byte give, out double qty, out byte take);
            Assert.Greater(gain, 0.0, "a deal worth the walk");
            Assert.AreEqual(Trade.Wares, give, "a's trader takes out wares");
            Assert.AreEqual(Trade.Grain, take, "and brings back grain");
            double target0 = Weaving.Target(in w.Settlements.Get(a));
            var res = w.Residents(a);
            int trader = res[0];
            ref var p = ref w.People.Get(trader);
            w.Trade.Load(ref p, a, b);
            w.Trade.Exchange(ref p, b);
            w.Trade.Unload(ref p, a);
            Console.WriteLine("traded: " + qty.ToString("0.0") + " wares for grain; a sold " + w.Settlements.Get(a).WaresSold.ToString("0.0") + ", b holds " + w.Settlements.Get(b).Wares.ToString("0.0") + "; a's target " + target0.ToString("0.0") + " -> " + Weaving.Target(in w.Settlements.Get(a)).ToString("0.0"));
            Assert.Greater(w.Settlements.Get(b).Wares, 0.5, "b has wares in its store");
            Assert.Greater(w.Settlements.Get(a).WaresSold, 0.5f, "a's sale is known");
            Assert.Greater(Weaving.Target(in w.Settlements.Get(a)), target0, "and its makers make that much more");
            Assert.Greater(w.Metrics.WaresTraded, 0.5);
        }
    }
}
