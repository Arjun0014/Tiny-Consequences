using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The potter's kiln, the village's jars and its hearths' pots (C3.2, D2-121): clay lies where the ground's shape lays it, wet or dry this season;
    /// a kiln is raised at the clay where the store's grain lacks jars for a month of a potter's work; its makers are the village's best potters and
    /// fire ware from the clay and the village's wood only; jars keep the grain they seal and break; a hearth that cannot fire its own pots cooks in
    /// kiln pots, on less wood; ware goes to villages without clay, and what is sold is made again. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class PotteryTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Premise: the village fires pots (its grown people competent, its best a master).</summary>
        static void Potters(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.AgeYears(w.Now) >= 16) p.KPottery = 0.8f; }
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown |= (byte)Pottery.Bit; s.BestPottery = 0.8f;
        }

        static int StandingKiln(World w, int sid)
        {
            Structures.Place(w, sid, Structures.Kiln, out float x, out float y);
            return Structures.Standing(w, sid, Structures.Kiln, x, y);
        }

        /// <summary>A small world with a village that has (or has not) clay within reach.</summary>
        static World WithClay(bool clay, out int sid, ulong from = 620, ulong to = 700)
        {
            for (ulong seed = from; seed < to; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) if (w.Learning.Clay(w.Settlements.Get(sid).Cell) == clay) return w;
            }
            sid = 0;
            return null;
        }

        [Test]
        public void ClayLiesWhereRiversAndLakesLaidItWetOrDryThisSeason()
        {
            int withClay = 0, without = 0;
            for (ulong seed = 620; seed < 626; seed++)
            {
                var w = TestWorlds.Make(seed);
                var P = w.Planet; var d = w.Drainage;
                var near = new List<int>();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                {
                    int cell = w.Settlements.Get(sid).Cell;
                    // by the rule: a reach draining 200 ha or more, or a depression, within reach - the nearest of them
                    w.Hazards.CellsWithin(w, cell, Learning.ClayReach, near);
                    int nearest = -1; double nd = double.MaxValue;
                    foreach (int c in near)
                    {
                        if (!P.IsLand(c)) continue;
                        if (!((d.BasinOf[c] < 0 && d.AreaHa[c] >= Learning.ClayAreaHa) || P.InBasin[c] != 0)) continue;
                        double dist = Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[c], P.Radius);
                        if (dist < nd || (dist == nd && c < nearest)) { nd = dist; nearest = c; }
                    }
                    Assert.AreEqual(nearest, w.Learning.ClayCell(cell), "the nearest clay ground");
                    Assert.AreEqual(nearest >= 0, w.Learning.Clay(cell));
                    if (nearest >= 0) withClay++; else without++;
                }
                // a dry season: no stream runs and the lakes are low - the clay is where it was (before C3.2 it went with the water)
                var before = new List<bool>();
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) before.Add(w.Learning.Clay(w.Settlements.Get(sid).Cell));
                for (int c = 0; c < P.Cells; c++) { P.RiverFlow[c] = 0f; P.LakeDepth[c] = 0f; }
                int k = 0;
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) Assert.AreEqual(before[k++], w.Learning.Clay(w.Settlements.Get(sid).Cell), "clay is not taken by a dry spell");
            }
            Console.WriteLine("villages with clay within reach " + withClay + ", without " + without);
            Assert.Greater(withClay, 0); Assert.Greater(without, 0, "not everywhere");
        }

        [Test]
        public void AKilnIsRaisedAtTheClayWhereTheStoresGrainLacksJars()
        {
            var w = WithClay(true, out int sid);
            Assume.That(w, Is.Not.Null, "a village with clay");
            Potters(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Project = 0; s.Wall = 0; s.Jars = 0; s.Pots = 0;
            // a small store: its jars would not keep a potter a month
            s.Granary = 0.5 * Pottery.KilnFrom * Pottery.JarHolds;
            Assert.IsFalse(Pottery.KilnWanted(w, in w.Settlements.Get(sid)), "too little grain lacks jars");
            // a store whose grain lacks a month's jars (within its granaries)
            ref var s1 = ref w.Settlements.Get(sid);
            s1.Granary = Math.Min(Structures.StoreCapacity(in s1), 1.2 * Pottery.KilnFrom * Pottery.JarHolds);
            Assume.That(Pottery.JarsWanted(in w.Settlements.Get(sid)), Is.GreaterThanOrEqualTo(Pottery.KilnFrom), "its granaries hold enough");
            Assert.IsTrue(Pottery.KilnWanted(w, in w.Settlements.Get(sid)));
            Structures.Plan(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreNotEqual(0, s2.Project, "a work begun");
            var b = w.Buildings.Get(s2.Project);
            Assert.AreEqual(Structures.Kiln, b.Kind, "a kiln");
            Assert.AreEqual(s2.PotEvent != 0 ? s2.PotEvent : s2.FoundedEvent, b.Cause, "enabled by their pottery");
            // at the clay: within reach of the clay ground's middle (or at the village's edge where the clay is in it)
            var P = w.Planet;
            int clay = w.Learning.ClayCell(s2.Cell);
            s2.SphereToLocal(P.Grid.Center[clay], P.Radius, out double cx, out double cy);
            double fromClay = Math.Sqrt(((double)b.X - cx) * ((double)b.X - cx) + ((double)b.Y - cy) * ((double)b.Y - cy));
            double clayFromSquare = Math.Sqrt((cx - s2.SquareX) * (cx - s2.SquareX) + (cy - s2.SquareY) * (cy - s2.SquareY));
            Console.WriteLine("kiln " + fromClay.ToString("0") + " m from the clay's middle, which lies " + clayFromSquare.ToString("0") + " m from the square; " + HistoryText.WorkWhy(Structures.Kiln, 0));
            Assert.That(fromClay < 125.0 || clayFromSquare < Structures.HomesReach(w, sid) + 30.0, "laid out at the clay");
            Assert.IsTrue(Shore.Dry(P, s2.LocalToSphere(b.X, b.Y, P.Radius)), "on dry ground");
            // none without the craft, while walls rise, or without clay
            s2.Project = 0;
            ref var s3 = ref w.Settlements.Get(sid);
            s3.Wall = 1;
            Assert.IsFalse(Pottery.KilnWanted(w, in w.Settlements.Get(sid)), "never while walls rise");
            s3.Wall = 0; s3.TechKnown = (byte)(s3.TechKnown & ~Pottery.Bit);
            Assert.IsFalse(Pottery.KilnWanted(w, in w.Settlements.Get(sid)), "nobody can fire pots");
            var w2 = WithClay(false, out int dry);
            Assume.That(w2, Is.Not.Null, "a village without clay");
            Potters(w2, dry);
            ref var t = ref w2.Settlements.Get(dry);
            t.Project = 0; t.Wall = 0; t.Granary = Math.Min(Structures.StoreCapacity(in t), 1.2 * Pottery.KilnFrom * Pottery.JarHolds);
            Assume.That(Pottery.JarsWanted(in w2.Settlements.Get(dry)), Is.GreaterThanOrEqualTo(Pottery.KilnFrom));
            Assert.IsFalse(Pottery.KilnWanted(w2, in w2.Settlements.Get(dry)), "no clay within reach: no kiln");
            Assert.IsFalse(Pottery.Site(w2, dry, out _, out _));
        }

        [Test]
        public void TheVillagesBestPottersWorkAtTheKiln()
        {
            var w = WithClay(true, out int sid);
            Assume.That(w, Is.Not.Null);
            Potters(w, sid);
            var able = new List<int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); double a = p.AgeYears(w.Now); if (a >= 16 && a < 60) able.Add(res[i]); }
            Assume.That(able.Count, Is.GreaterThan(Pottery.Hands + 1), "more able potters than hands");
            for (int i = 0; i < able.Count; i++) { ref var p = ref w.People.Get(able[i]); p.KPottery = (float)(0.55 + 0.4 * ((i * 7) % able.Count) / able.Count); }
            ref var tie = ref w.People.Get(able[able.Count - 1]); tie.KPottery = w.People.Get(able[0]).KPottery;   // (a tie: the lower id first)
            StandingKiln(w, sid);
            var s = w.Settlements.Get(sid);
            var keys = new List<(double k, int id)>();
            foreach (int id in able) keys.Add((Pottery.Key(in w.People.Get(id)), id));
            keys.Sort((x, y) => x.k != y.k ? y.k.CompareTo(x.k) : x.id.CompareTo(y.id));
            for (int i = 0; i < keys.Count; i++)
                Assert.AreEqual(i < Pottery.Hands, Pottery.Maker(in s, in w.People.Get(keys[i].id), w.Now), "the " + (i + 1) + "th best potter");
            // practice keeps a place: the next best, long at a kiln, passes the weakest hand
            ref var next = ref w.People.Get(keys[Pottery.Hands].id);
            next.SkPot = 0.9f;
            Pottery.Bar(w, sid);
            Assert.IsTrue(Pottery.Maker(in w.Settlements.Get(sid), in w.People.Get(keys[Pottery.Hands].id), w.Now), "a practised hand takes a place");
            Assert.IsFalse(Pottery.Maker(in w.Settlements.Get(sid), in w.People.Get(keys[Pottery.Hands - 1].id), w.Now), "and the weakest gives it up");
            // the role read from practice, not from knowing how (before C3.2 everyone who could fire a pot was "a potter")
            Assert.IsTrue(Biography.Work(w, keys[Pottery.Hands].id).Contains("a potter at the kiln"));
            Assert.IsFalse(Biography.Work(w, keys[keys.Count - 1].id).Contains("a potter"), "an unpractised hand is not known as a potter");
        }

        [Test]
        public void AtTheKilnWareIsFiredFromTheClayAndTheVillagesWood()
        {
            var w = WithClay(true, out int sid);
            Assume.That(w, Is.Not.Null);
            Potters(w, sid);
            int kiln = StandingKiln(w, sid);
            int maker = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count && maker == 0; i++) if (Pottery.Maker(in w.Settlements.Get(sid), in w.People.Get(res[i]), w.Now)) maker = res[i];
            Assert.AreNotEqual(0, maker, "a maker");
            ref var s = ref w.Settlements.Get(sid);
            s.Woodstore = 500; s.Pots = 0; s.Jars = 0; s.PotsSold = 400f;   // premise: wood in store, and buyers lately (the target well above a stint's making)
            ref var p = ref w.People.Get(maker);
            p.SkPot = 0.5f;
            double rate = Pottery.Rate(in p, w.Now);
            Assert.AreEqual(Pottery.KgPerHour * (0.5 + 0.5 * p.Health) * (1.0 + 0.6 * p.KPottery) * (0.8 + 0.4 * 0.5), rate, 1e-9, "the hand's skill and practice");
            double used = Pottery.Work(w, ref p, sid, kiln, 3.0);
            double made = w.Settlements.Get(sid).Pots;
            Assert.AreEqual(3.0, used, 1e-9);
            Assert.AreEqual(rate * 3.0, made, 1e-9);
            Assert.AreEqual(500 - made * Pottery.WoodPerKg, w.Settlements.Get(sid).Woodstore, 1e-9, "fired on the village's wood");
            Assert.Greater(w.People.Get(maker).SkPot, 0.5f, "practice");
            Assert.AreEqual(3.0, w.Buildings.Get(kiln).Use, 1e-4); Assert.AreEqual(made, w.Buildings.Get(kiln).Stock, 1e-4);
            // COARSE: the walk out to the clay and home comes off a stint
            ref var p1 = ref w.People.Get(maker);
            double before = w.Settlements.Get(sid).Pots, rate1 = Pottery.Rate(in p1, w.Now);
            Pottery.Work(w, ref p1, sid, kiln, 3.0, 1, true);
            double coarseMade = w.Settlements.Get(sid).Pots - before;
            double dist = Pottery.Dist(w, in w.Settlements.Get(sid), kiln);
            Assert.AreEqual(rate1 * 3.0 * Stone.AfterWalk(dist, w.Config.WalkSpeed), coarseMade, 1e-9, "less the walk (" + dist.ToString("0") + " m)");
            // no wood: no firing
            ref var s2 = ref w.Settlements.Get(sid); s2.Woodstore = 0;
            Assert.IsFalse(Pottery.Use(in w.Settlements.Get(sid)), "nothing to fire it with");
            ref var p2 = ref w.People.Get(maker);
            Assert.AreEqual(0.0, Pottery.Work(w, ref p2, sid, kiln, 3.0), "no ware without wood");
            // the store at its target: no more
            ref var s3 = ref w.Settlements.Get(sid); s3.Woodstore = 500; s3.PotsSold = 0; s3.HearthShort = 0; s3.Granary = 0;
            s3.Pots = Pottery.Target(in w.Settlements.Get(sid));
            Assert.IsFalse(Pottery.Use(in w.Settlements.Get(sid)), "the store holds what it keeps");
            ref var p3 = ref w.People.Get(maker);
            Assert.AreEqual(0.0, Pottery.Work(w, ref p3, sid, kiln, 3.0));
        }

        [Test]
        public void JarsKeepTheGrainTheySealAndBreak()
        {
            var s = new Settlement { Granary = 8000, Granaries = 1, Population = 100 };
            Assert.AreEqual(1.0, Pottery.RotFactor(in s), 1e-12, "no jars: the open store's rot");
            s.Jars = 8000 / Pottery.JarHolds;
            Assert.AreEqual(1.0 - Pottery.Seal, Pottery.RotFactor(in s), 1e-12, "all sealed: D2-063's 0.65");
            s.Jars = 4000 / Pottery.JarHolds;
            Assert.AreEqual(1.0 - 0.5 * Pottery.Seal, Pottery.RotFactor(in s), 1e-12, "half sealed, in proportion");
            s.Jars = 20000 / Pottery.JarHolds;
            Assert.AreEqual(1.0 - Pottery.Seal, Pottery.RotFactor(in s), 1e-12, "empty jars keep nothing more");
            s.Granary = 30000; s.Jars = Structures.GranaryHolds / Pottery.JarHolds;
            Assert.AreEqual(1.0 - Pottery.Seal, Pottery.RotFactor(in s), 1e-12, "grain beyond the granaries lies in heaps, never in jars");
            Assert.AreEqual(0.0, Pottery.JarsWanted(in s), 1e-9);
            // in a world: ware in store stood as jars as the grain lacks them; jars break
            var w = TestWorlds.Make(631);
            int sid = w.Settlements.First();
            ref var v = ref w.Settlements.Get(sid);
            v.Granary = 4000; v.Jars = 0; v.Pots = 3000;
            Pottery.Day(w, sid, 0.0, 1);
            var v1 = w.Settlements.Get(sid);
            Assert.AreEqual(Math.Min(4000, Structures.StoreCapacity(in v1)) / Pottery.JarHolds, v1.Jars, 1e-6, "stood as jars as far as the grain lacks them");
            Assert.AreEqual(3000 - v1.Jars, v1.Pots, 1e-6, "the rest stays in store");
            ref var v2 = ref w.Settlements.Get(sid); double j0 = v2.Jars; v2.Pots = 0;
            Pottery.Day(w, sid, 0.0, SimTime.DaysPerYear);
            Assert.AreEqual(j0 * SettlementSystem.Keep(Pottery.JarBreak / SimTime.DaysPerYear, SimTime.DaysPerYear), w.Settlements.Get(sid).Jars, 1e-6, "a twentieth break in a year");
            // a store with jars loses less to rot than the same store without
            var a = new Settlement { Granary = 8000, Granaries = 1, Jars = 2000 }; var o = new Settlement { Granary = 8000, Granaries = 1 };
            Structures.Rot(ref a, 0.12 / SimTime.DaysPerYear * Pottery.RotFactor(in a), 48); Structures.Rot(ref o, 0.12 / SimTime.DaysPerYear * Pottery.RotFactor(in o), 48);
            Console.WriteLine("a year's rot: sealed " + (8000 - a.Granary).ToString("0") + " FU, open " + (8000 - o.Granary).ToString("0") + " FU");
            Assert.Less(8000 - a.Granary, 0.7 * (8000 - o.Granary));
            // the tools' switch: pots and jars from knowing how (D2-063, before C3.2)
            Pottery.Off = true;
            try
            {
                var k = new Settlement { Granary = 8000, Granaries = 1, BestPottery = 0.8f };
                Assert.AreEqual(Tech.PotteryKeeps(0.8), Pottery.RotFactor(in k), 1e-12);
                Assert.AreEqual(0.0, w.Trade.Value(sid, Trade.Pots), "no ware traded");
            }
            finally { Pottery.Off = false; }
        }

        [Test]
        public void AHearthThatCannotFireItsOwnCooksInKilnPotsOnLessWood()
        {
            var w = WithClay(false, out int sid);
            Assume.That(w, Is.Not.Null, "a village without clay");
            Potters(w, sid);   // (they know how - but there is no clay to fire)
            int hid = w.HouseholdsOf(sid)[0];
            int members = w.Members(hid).Count;
            Assert.IsFalse(Pottery.FiresOwn(w, sid, Pottery.Best(w, hid)), "no clay: no pots of its own");
            Assert.AreEqual(1.0, Pottery.CookFactor(w, hid), 1e-12, "and none from a kiln: the full wood");
            ref var s = ref w.Settlements.Get(sid);
            s.Pots = 1000;
            ref var hh = ref w.Households.Get(hid);
            double lacked = Pottery.Draw(w, ref s, ref hh, hid, members);
            Assert.AreEqual(0.0, lacked, 1e-9);
            Assert.AreEqual(members * Pottery.PerHead, w.Households.Get(hid).Pots, 1e-4, "kiln pots for its members");
            Assert.AreEqual(1000 - members * Pottery.PerHead, w.Settlements.Get(sid).Pots, 1e-4);
            Assert.AreEqual(0.8, Pottery.CookFactor(w, hid), 1e-6, "cooking in fired pots");
            ref var h2 = ref w.Households.Get(hid);
            Pottery.Wear(ref h2, members, 100);
            Assert.AreEqual(members * Pottery.PerHead * (1.0 - 100.0 / Pottery.PotDays), w.Households.Get(hid).Pots, 1e-4, "broken with use");
            Assert.Greater(Pottery.CookFactor(w, hid), 0.8, "fewer pots, more wood");
            // a store with none: it lacks them (its potters, or its traders, know)
            ref var s2 = ref w.Settlements.Get(sid); s2.Pots = 0;
            ref var h3 = ref w.Households.Get(hid);
            Assert.Greater(Pottery.Draw(w, ref s2, ref h3, hid, members), 0.0, "short");
            // where there is clay, a household with a potter fires its own and takes none
            var wc = WithClay(true, out int sc);
            Assume.That(wc, Is.Not.Null);
            Potters(wc, sc);
            int hc = wc.HouseholdsOf(sc)[0];
            Assume.That(Pottery.Best(wc, hc), Is.GreaterThanOrEqualTo(Tech.Competent));
            ref var sc1 = ref wc.Settlements.Get(sc); sc1.Pots = 1000;
            ref var hc1 = ref wc.Households.Get(hc);
            Assert.AreEqual(0.0, Pottery.Draw(wc, ref sc1, ref hc1, hc, wc.Members(hc).Count), 1e-9);
            Assert.AreEqual(1000.0, wc.Settlements.Get(sc).Pots, 1e-9, "it takes none");
            Assert.AreEqual(Tech.PotteryFuel(Pottery.Best(wc, hc)), Pottery.CookFactor(wc, hc), 1e-12, "its own pots, by its best potter (D2-063)");
            // in a running world: hearths in kiln pots burn less of the village's wood than the same hearths without
            var a = TestWorlds.Make(w.Seed); var b = TestWorlds.Make(w.Seed);
            foreach (var x in new[] { a, b })
            {
                Potters(x, sid);
                ref var sx = ref x.Settlements.Get(sid); sx.Woodstore = 5000; sx.Pots = x == a ? 400.0 * sx.Population : 0.0;
            }
            double w0a = a.Settlements.Get(sid).Woodstore, w0b = b.Settlements.Get(sid).Woodstore, f0a = a.Metrics.WoodFelled, f0b = b.Metrics.WoodFelled;
            a.AdvanceTo(a.Now + SimTime.Days(4)); b.AdvanceTo(b.Now + SimTime.Days(4));
            double burntA = w0a - a.Settlements.Get(sid).Woodstore + (a.Metrics.WoodFelled - f0a), burntB = w0b - b.Settlements.Get(sid).Woodstore + (b.Metrics.WoodFelled - f0b);
            Console.WriteLine("wood burnt in four days: hearths in kiln pots " + burntA.ToString("0.0") + ", without " + burntB.ToString("0.0") + " (pots taken " + a.Metrics.PotsDrawn.ToString("0") + " kg)");
            Assert.Greater(a.Metrics.PotsDrawn, 0.0);
            Assert.Less(burntA, burntB, "less wood");
            Assert.IsNull(TestWorlds.Audit(a));
        }

        [Test]
        public void PottersWorkAtTheKilnAtBothResolutions()
        {
            var perDay = new double[2];
            foreach (bool coarse in new[] { true, false })
            {
                var w = WithClay(true, out int sid);
                Assume.That(w, Is.Not.Null);
                if (coarse) Coarse(w);
                Potters(w, sid);
                int kiln = StandingKiln(w, sid);
                ref var s = ref w.Settlements.Get(sid);
                s.Pots = 0; s.Jars = 0; s.PotsSold = 300f; s.Granary = 30 * s.Population; s.Woodstore = 800;   // premise: buyers lately (work for days), food and wood
                bool saw = false; int makerDays = 0;
                for (int h = 0; h < 6 * 24; h++)
                {
                    { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); if (sk.PotsSold < 300f) sk.PotsSold = 300f; if (sk.Woodstore < 400) sk.Woodstore = 800; }   // (the fields need no hands now; buyers keep coming)
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    if (h % 24 == 12) { var res0 = w.Residents(sid); for (int i = 0; i < res0.Count; i++) if (Pottery.Maker(in w.Settlements.Get(sid), in w.People.Get(res0[i]), w.Now)) makerDays++; }
                    if (!coarse && !saw) { var res = w.Residents(sid); for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Act == ActivityKind.Pot) { saw = true; Console.WriteLine(HistoryText.Doing(w, in w.People.Get(res[i]))); } }
                }
                perDay[coarse ? 0 : 1] = w.Metrics.KilnHours / Math.Max(1, makerDays);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": kiln hours " + w.Metrics.KilnHours.ToString("0") + " (" + perDay[coarse ? 0 : 1].ToString("0.0") + " a maker-day), ware fired " + w.Metrics.PotsMade.ToString("0") + " kg, stood as jars " + w.Metrics.JarsSet.ToString("0") + " kg, kiln " + Pottery.Dist(w, in w.Settlements.Get(sid), kiln).ToString("0") + " m out");
                Assert.Greater(w.Metrics.KilnHours, 10, "the potters worked at the kiln");
                Assert.Greater(w.Metrics.PotsMade, 10, "ware fired");
                if (!coarse) Assert.IsTrue(saw, "seen at the kiln");
                Assert.IsNull(TestWorlds.Audit(w));
                // a save in the middle of it continues as the world does
                var bytes = SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, "");
                var w2 = SaveFile.Load(bytes, out _);
                w.AdvanceTo(w.Now + SimTime.Days(2)); w2.AdvanceTo(w2.Now + SimTime.Days(2));
                Assert.AreEqual(w.StateHash(), w2.StateHash(), "saved and loaded, the same world");
            }
            Assert.That(perDay[0] / Math.Max(1e-9, perDay[1]), Is.InRange(0.5, 2.0), "a COARSE potter's day at the kiln near a lived one's");
        }

        [Test]
        public void WareGoesToVillagesWithoutClayAndWhatIsSoldIsMadeAgain()
        {
            var w = TestWorlds.Make(632);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            Assume.That(b != 0, "two villages");
            ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
            // premise: a has ware to spare, its grain sealed, and is short of grain; b has grain to spare in granaries that hold no jars
            sa.Granary = 5; sa.Jars = 100; sa.Pots = 5.0 * sa.Population + 200;
            sb.Granary = Math.Min(Structures.StoreCapacity(in sb), 200.0 * sb.Population); sb.Jars = 0; sb.Pots = 0;
            Assume.That(Pottery.Sealed(in w.Settlements.Get(b)), Is.LessThan(0.01));
            Assert.Greater(w.Trade.Value(b, Trade.Pots), 4.0 * w.Trade.Value(a, Trade.Pots), "ware is worth much where grain lies unsealed");
            Assert.Greater(w.Trade.Spare(a, Trade.Pots), 0.0);
            double gain = w.Trade.Plan(a, b, out byte give, out double qty, out byte take);
            Assert.Greater(gain, 0.0, "a deal worth the walk");
            Assert.AreEqual(Trade.Pots, give, "a's trader takes out pots and jars");
            Assert.AreEqual(Trade.Grain, take, "and brings back grain");
            double target0 = Pottery.Target(in w.Settlements.Get(a));
            int trader = w.Residents(a)[0];
            ref var p = ref w.People.Get(trader);
            w.Trade.Load(ref p, a, b);
            w.Trade.Exchange(ref p, b);
            w.Trade.Unload(ref p, a);
            Console.WriteLine("traded: " + qty.ToString("0") + " kg of ware for grain; a sold " + w.Settlements.Get(a).PotsSold.ToString("0") + ", b holds " + w.Settlements.Get(b).Pots.ToString("0") + " kg; a's target " + target0.ToString("0") + " -> " + Pottery.Target(in w.Settlements.Get(a)).ToString("0"));
            Assert.Greater(w.Settlements.Get(b).Pots, 1.0, "b has ware in its store");
            Assert.Greater(w.Settlements.Get(a).PotsSold, 1f, "a's sale is known");
            Assert.Greater(Pottery.Target(in w.Settlements.Get(a)), target0, "and its potters make that much more");
            Assert.Greater(w.Metrics.PotsTraded, 1.0);
            // b's ware stands in its granaries as jars the next day
            Pottery.Day(w, b, 0.0, 1);
            Assert.Greater(w.Settlements.Get(b).Jars, 1.0, "stood as jars");
        }
    }
}
