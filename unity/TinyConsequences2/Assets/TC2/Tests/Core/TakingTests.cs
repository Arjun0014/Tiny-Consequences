using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Deeds and taking by need (D2-069; F6 slice 1): the hungry take grain from a neighbour's full store in the night, seen or not
    /// by the homes round it; what villages know of deeds makes grievance and gratitude, read by trade, relief and the next taker's
    /// restraint; a COARSE night takes at the lived rate. The watch, blows and injury (D2-071, slice 2): a seen taker may be caught
    /// and struck, wounds heal or kill as deeds, a village that remembers losses keeps a watch. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class TakingTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Two villages of a world (seed 42's unless given) a short walk apart (with a road).</summary>
        static World Pair(out int a, out int b, ulong seed = 42)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = seed });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        static int Grown(World w, int sid, int skip = 0)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                double age = p.AgeYears(w.Now);
                if (res[i] != skip && p.Alive && p.At == sid && age >= 18 && age < 50) return res[i];
            }
            return 0;
        }

        /// <summary>Premise: a village's store full - two months of its need and loads beyond.</summary>
        static void Full(World w, int sid, double days = 60)
        {
            ref var s = ref w.Settlements.Get(sid);
            Trade.DaysOfFood(w, in s, out double daily);
            s.Granary = days * daily + 4 * Conflict.LoadFu;
        }

        static int LastDeed(World w) { int d = 0; for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id)) d = id; return d; }

        [Test]
        public void UnseenATakerCarriesALoadFromAFullStoreToTheirLarder()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            Full(w, b);
            int t = Grown(w, a);
            ref var p = ref w.People.Get(t);
            double before = w.Settlements.Get(b).Granary;
            long mark = w.History.LastId;
            w.Conflict.TakeWith(ref p, b, 1.0);   // (unseen: nobody round the store woke)
            Assert.AreEqual(Conflict.LoadFu, before - w.Settlements.Get(b).Granary, 1e-6, "a load out of the store");
            Assert.AreEqual(Trade.Grain, p.Cargo);
            Assert.AreEqual(Trade.ToLarder, p.CargoTo, "for the household, not the village");
            int d = LastDeed(w);
            Assert.AreEqual(DeedKind.Take, w.Deeds.Get(d).Kind);
            Assert.IsTrue(w.DeedBook.Knows(a, d, out byte how) && how == KnownHow.Own, "their own village knows it");
            Assert.AreEqual(Conflict.LoadFu >= Conflict.FoundShare * before, w.DeedBook.Knows(b, d, out _), "the loss is found only when the store is visibly lower");
            Assert.AreEqual(0.0, w.DeedBook.Grievance(b, a), "an unseen taker leaves no grudge against their village");
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.Taken) { Console.WriteLine(HistoryText.Line(w, e)); Assert.AreEqual(t, e.Subject.Id); } }
            // home with it: into the household's larder
            double larder = w.Households.Get(p.Household).Larder;
            w.Trade.Unload(ref p, a);
            Assert.AreEqual(larder + Conflict.LoadFu, w.Households.Get(p.Household).Larder, 1e-3);
            Assert.AreEqual(0f, p.CargoQty);
        }

        [Test]
        public void SeenATakerRunsAndTheWrongedVillageNeitherTradesNorSendsReliefToTheirs()
        {
            int a, b;
            var w = Pair(out a, out b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            // (a deal each wants: a spares metal and lacks fibre, b the other way round)
            ref var sa = ref w.Settlements.Get(a); sa.Metal = sa.Population * 2.0; sa.Fibre = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Metal = 0; sb.Fibre = sb.Population * 6.0;
            Full(w, b);
            Assume.That(w.Trade.Plan(a, b, out _, out _, out _) > 0, "they would deal");
            int t = Grown(w, a);
            ref var p = ref w.People.Get(t);
            double before = w.Settlements.Get(b).Granary;
            long mark = w.History.LastId;
            w.Conflict.TakeWith(ref p, b, -1.0);   // (seen: a sleeper by the store woke)
            Assert.AreEqual(before, w.Settlements.Get(b).Granary, "they left the grain");
            Assert.AreEqual(0f, p.CargoQty);
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.TakingSeen) Console.WriteLine(HistoryText.Line(w, e)); }
            double g0 = w.DeedBook.Grievance(b, a);
            Console.WriteLine("b's grievance against a " + g0.ToString("0.0") + " (grudge at " + Trade.Grudge + ")");
            Assert.Greater(w.DeedBook.Grievance(b, a), Trade.Grudge, "b knows who it was");
            if (w.Metrics.Blows == 0) Assert.AreEqual(0.0, w.DeedBook.Grievance(a, b), "a holds nothing against b unless its man was struck there (D2-071)");
            Assert.AreEqual(0.0, w.Trade.Plan(a, b, out _, out _, out _), "no deals with those who wronged them");
            Assert.AreEqual(0.0, w.Trade.Plan(b, a, out _, out _, out _));
            // a famine at a, grain to spare at b: no relief from b - where, without the wrong, it sends some
            Assert.AreEqual(0, ReliefFromBToA(w, a, b), "no grain for those who took from them");
            var w2 = Pair(out a, out b);
            w2.AdvanceTo(w2.Now + SimTime.Hours(30));
            Assert.AreNotEqual(0, ReliefFromBToA(w2, a, b), "(the same famine with no wrong known: b sends grain)");
            // the grudge fades: the fright and the intent halve in two years (blood, if any was shed, in ten)
            w.AdvanceTo(w.Now + SimTime.Years(2));
            Console.WriteLine("two years on: b's grievance " + w.DeedBook.Grievance(b, a).ToString("0.0"));
            Assert.Less(w.DeedBook.Grievance(b, a), 0.75 * g0, "it weighs less as it ages");
            if (w.Metrics.Blows == 0) Assert.Less(w.DeedBook.Grievance(b, a), Trade.Grudge, "and with no blood shed, b deals again");
        }

        static int ReliefFromBToA(World w, int a, int b)
        {
            ref var s = ref w.Settlements.Get(a);
            s.Granary = 0;
            s.FamineAt = w.Now;
            s.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = s.Cell, Settlement = a, Subject = EntityRef.Settlement(a) });
            long at = w.Now;
            w.Settlements.Get(b).Granary = 50000;
            w.AdvanceTo(w.Now + SimTime.Days(2));
            return w.DeedBook.HelpSince(b, a, at);
        }

        [Test]
        public void KinAndHelpHoldATakerBackAndAWrongLetsThemGo()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int t = Grown(w, a);
            var p = w.People.Get(t);
            Assume.That(!w.Conflict.KinIn(in p, b), "no kin of theirs there yet");
            double r0 = w.Conflict.Restraint(in p, b);
            Assert.AreEqual(0.0, r0, 1e-9, "a stranger's store");
            // a child of theirs lives there (premise: a family tie)
            int kid = Grown(w, b);
            ref var k = ref w.People.Get(kid);
            int mother = k.Mother, father = k.Father;
            if (p.SexKind == Sex.Female) k.Mother = t; else k.Father = t;
            double rKin = w.Conflict.Restraint(in p, b);
            k.Mother = mother; k.Father = father;
            // help b gave a (grain in a famine, known to a)
            int help = w.DeedBook.Record(DeedKind.Relief, 0, b, 0, a, Trade.Grain, 200, 200, 0);
            w.DeedBook.Learn(a, help, KnownHow.Saw);
            double rHelp = w.Conflict.Restraint(in p, b);
            // and a wrong b's people did a (seen at a's store)
            int wrong = w.DeedBook.Record(DeedKind.TriedToTake, Grown(w, b), b, 0, a, Trade.Grain, 0, 30, 0);
            w.DeedBook.Learn(a, wrong, KnownHow.Saw);
            double rWrong = w.Conflict.Restraint(in p, b);
            Console.WriteLine("restraint: a stranger's store " + r0.ToString("0.00") + ", kin there " + rKin.ToString("0.00") + ", help received " + rHelp.ToString("0.00") + ", then a wrong " + rWrong.ToString("0.00"));
            Assert.GreaterOrEqual(rKin, 0.85, "kin hold them back");
            Assert.Greater(rHelp, r0, "help received holds them back");
            Assert.Less(rWrong, rHelp, "a wrong lets them go: taking back is not stealing to them");
        }

        [Test]
        public void SleepersRoundTheStoreSeeATaker()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Construction.GranarySpot(in w.Settlements.Get(b), out double gx, out double gy);
            var hhs = w.HouseholdsOf(b);
            Assume.That(hhs.Count > 0);
            for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); hh.HomeX = (float)(gx + 300); hh.HomeY = (float)gy; }
            double far = w.Conflict.SeenChance(b);
            for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); hh.HomeX = (float)(gx + 5 + 0.5 * i); hh.HomeY = (float)gy; }
            double near = w.Conflict.SeenChance(b);
            Console.WriteLine(hhs.Count + " homes: far from the store seen " + far.ToString("0.00") + ", round it " + near.ToString("0.00"));
            Assert.AreEqual(0.0, far, "no home near: nobody wakes");
            Assert.AreEqual(Math.Min(Conflict.SeenAtMost, 1.0 - Math.Exp(-Conflict.WakePerHome * hhs.Count)), near, 1e-12, "each home round it may wake");
        }

        [Test]
        public void ALivedTakerGoesInTheDarkAndComesHome()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(SimTime.StartOfDay(w.Now + SimTime.Days(2)) + SimTime.Hours(21.5) - w.LocalOffsetOf(a));   // (a local evening)
            Full(w, b);
            int t = Grown(w, a);
            long n0 = w.Metrics.Takings + w.Metrics.TakingsSeen;
            Assert.IsTrue(w.Agents.SendTaker(t, b), "they set out");
            Console.WriteLine(HistoryText.Doing(w, w.People.Get(t)));
            bool sawHome = false;
            long end = w.Now + SimTime.Hours(8);
            while (w.Now < end)
            {
                w.AdvanceTo(w.Now + 600);
                var q = w.People.Get(t);
                if (!sawHome && q.Act == ActivityKind.TripHome) { sawHome = true; Console.WriteLine(HistoryText.Doing(w, in q)); }
            }
            var p = w.People.Get(t);
            Assert.AreEqual(n0 + 1, w.Metrics.Takings + w.Metrics.TakingsSeen, "they reached the store: taken or seen");
            Assert.IsTrue(sawHome, "and walked home in the dark");
            Assert.AreEqual(a, p.At, "home again");
            Assert.AreEqual(0f, p.CargoQty, "whatever they carried is in the larder");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        /// <summary>
        /// A month of a village kept starving beside a full store (its stores emptied each day, its game, fish and woods gone; its
        /// grown people bold and their bodies' reserves low - a premise that gives enough nights to count): its takings, the
        /// nights its hungry people weighed going, and its hungry grown person-nights.
        /// </summary>
        static long HungryMonth(ulong seed, bool coarse, out long weighed, out double hungryNights)
        {
            var w = Pair(out int a, out int b, seed);
            if (coarse) Coarse(w);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hours(12));
            var cells = new List<int>();
            w.Hazards.CellsWithin(w, w.Settlements.Get(a).Cell, WildFood.HuntReach + 600, cells);
            cells = new List<int>(cells);
            var woods = new List<int>();
            w.Hazards.CellsWithin(w, w.Settlements.Get(a).Cell, WildFood.GatherReach + 600, woods);
            woods = new List<int>(woods);
            var P = w.Planet;
            long n0 = w.Metrics.Takings + w.Metrics.TakingsSeen, wg0 = w.Metrics.TakeWeighed;
            hungryNights = 0;
            for (int d = 0; d < 30; d++)
            {
                foreach (int c in cells) for (int q = 0; q < P.Species.Count; q++) { var sp = P.Species[q]; if (WildFood.IsGame(sp) || WildFood.IsFish(sp)) P.Abund[q * P.Cells + c] = 0f; }
                foreach (int c in woods) if (P.IsLand(c)) P.Forest[c] = 0f;
                w.Settlements.Get(a).Granary = 0;
                var hhs = w.HouseholdsOf(a);
                for (int i = 0; i < hhs.Count; i++) { ref var hh = ref w.Households.Get(hhs[i]); hh.Larder = 0; hh.Ready = 0; }
                Full(w, b);
                var res = w.Residents(a);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    if (!p.Alive || p.AgeYears(w.Now) < 16) continue;
                    p.Bold = 255;
                    p.Reserve = (float)DMath.Min(p.Reserve, 0.3 * AgentSystem.ReserveCap(in p, w.Seed, w.Now));
                    if (Conflict.CanTake(in p, w.Now)) hungryNights++;
                }
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            weighed = w.Metrics.TakeWeighed - wg0;
            return w.Metrics.Takings + w.Metrics.TakingsSeen - n0;
        }

        [Test]
        public void TheCoarseNightTakesAtTheLivedRate()
        {
            // the same starving month lived and at COARSE, in four worlds: a lived night weighs going at bedtime with the body of
            // the night before, a COARSE night once with the day's account - the same choice, so the month's takings agree
            long lived = 0, coarse = 0, lw = 0, cw = 0;
            foreach (ulong seed in new ulong[] { 42, 43, 44, 45 })
            {
                long l = HungryMonth(seed, false, out long lwi, out double ln), c = HungryMonth(seed, true, out long cwi, out double cn);
                Console.WriteLine("seed " + seed + ", a starving month: lived " + l + " takings in " + lwi + " nights weighed (" + ln + " hungry grown person-nights), COARSE " + c + " in " + cwi + " (" + cn + ")");
                lived += l; coarse += c; lw += lwi; cw += cwi;
            }
            Console.WriteLine("in all: lived " + lived + " (" + lw + " weighed), COARSE " + coarse + " (" + cw + ")");
            Assert.Greater(lived, 0, "the hungry went to the full store");
            Assert.That((double)coarse / Math.Max(1, lived), Is.InRange(0.5, 2.0), "refit the lived bedtime or the COARSE night");
        }

        // ------------------------------------------------------------------ the watch, blows and injury (D2-071)
        [Test]
        public void ASeenTakerMayBeCaughtAndStruckAndTheWoundIsADeedBothVillagesKnow()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            Full(w, b);
            long mark = w.History.LastId;
            int attempts = 0;
            for (int night = 0; night < 24 && w.Metrics.Blows == 0; night++)
            {
                var res = w.Residents(a);
                for (int i = 0; i < res.Count; i++)
                {
                    ref var p = ref w.People.Get(res[i]);
                    double age = p.AgeYears(w.Now);
                    if (!p.Alive || age < 18 || age >= 50 || p.Hurt > 0f) continue;
                    w.Conflict.TakeWith(ref p, b, -1.0);   // (seen: a sleeper by the store woke)
                    attempts++;
                }
                w.AdvanceTo(w.Now + SimTime.Hours(1));
            }
            Console.WriteLine(attempts + " takers seen: " + w.Metrics.Blows + " blows");
            Assert.Greater(w.Metrics.Blows, 0, "some were caught, or turned on those who came out");
            HistoryEvent struck = default;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.Struck) { struck = e; Console.WriteLine(HistoryText.Line(w, e)); } }
            Assert.AreEqual(HistoryKind.Struck, struck.Kind);
            var hit = w.People.Get(struck.Object.Id);
            var by = w.People.Get(struck.Subject.Id);
            Assert.Greater(hit.Hurt, 0f, "the one struck is hurt");
            Assert.AreNotEqual(0, hit.HurtDeed);
            var d = w.Deeds.Get(hit.HurtDeed);
            Assert.AreEqual(DeedKind.Wound, d.Kind);
            Assert.AreEqual(by.Id, d.By, "the striker's deed");
            Assert.IsTrue(w.DeedBook.Knows(hit.Home, d.Id, out byte how) && how == KnownHow.Saw, "the struck one's village knows who did it");
            Assert.IsTrue(w.DeedBook.Knows(by.Home, d.Id, out byte how2) && how2 == KnownHow.Own);
            Assert.Greater(w.DeedBook.Grievance(hit.Home, by.Home), 0.0, "and holds it against the striker's village");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void WoundsHealWhenFedAndDeepOnesMayKill()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int striker = Grown(w, a);
            // premise: ten of b's people struck by a man of a - five lightly, five deeply
            var light = new List<int>(); var deep = new List<int>();
            var res = w.Residents(b);
            for (int i = 0; i < res.Count && light.Count + deep.Count < 10; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                double age = p.AgeYears(w.Now);
                if (!p.Alive || age < 16 || age >= 60) continue;
                double hurt = light.Count < 5 ? 0.35 : 0.95;
                int deed = w.DeedBook.Record(DeedKind.Wound, striker, a, p.Household, b, 0, hurt, Conflict.WoundWorth * hurt, 0);
                w.DeedBook.Learn(b, deed, KnownHow.Saw);
                p.Hurt = (float)hurt; p.HurtDeed = deed;
                (hurt < 0.5 ? light : deep).Add(res[i]);
            }
            Assume.That(deep.Count == 5, "ten grown people");
            // the deeply hurt keep to their beds (lived: at noon)
            w.AdvanceTo(SimTime.StartOfDay(w.Now + SimTime.Day) + SimTime.Hours(12) - w.LocalOffsetOf(b));
            int abed = 0;
            foreach (int id in deep) { var q = w.People.Get(id); if (q.Alive && q.Act == ActivityKind.Ill && q.Why == WhyCode.Hurt) abed++; }
            Console.WriteLine(HistoryText.Doing(w, w.People.Get(deep[0])));
            Assert.Greater(abed, 0, "the badly hurt are abed");
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(40));
            int dead = 0;
            foreach (int id in deep) if (!w.People.Get(id).Alive) dead++;
            foreach (int id in light) { var q = w.People.Get(id); Assert.IsTrue(q.Alive, "light wounds do not kill"); Assert.AreEqual(0f, q.Hurt, "and heal"); }
            int kills = 0;
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id)) { var k = w.Deeds.Get(id); if (k.Kind == DeedKind.Kill && k.By == striker && k.ToS == b) kills++; }
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.PersonDied && e.ArgA == 11) Console.WriteLine(HistoryText.Line(w, e)); }
            Console.WriteLine("deeply hurt: " + dead + " of 5 died; killings known " + kills + "; b's grievance against a " + w.DeedBook.Grievance(b, a).ToString("0"));
            Assert.Greater(dead, 0, "deep wounds may kill");
            Assert.AreEqual(dead, kills, "every death of wounds a killing, the striker's deed");
            Assert.Greater(w.DeedBook.Grievance(b, a), dead * 0.5 * Conflict.KillWorth, "blood weighs long");
        }

        [Test]
        public void AVillageThatRemembersLossesKeepsAWatchThatSeesTakers()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            double before = w.Conflict.SeenAt(b);
            Assert.AreEqual(0, w.Settlements.Get(b).Watcher, "no watch without losses");
            // premise: b has twice found grain gone from its store this year
            for (int k = 0; k < 2; k++)
            {
                int d = w.DeedBook.Record(DeedKind.Take, Grown(w, a), a, 0, b, Trade.Grain, Conflict.LoadFu, Conflict.LoadFu, 0);
                w.DeedBook.Learn(b, d, KnownHow.Found);
            }
            long mark = w.History.LastId;
            w.Conflict.Watch(b);   // (the village's choice tonight)
            w.Conflict.Watch(b);
            int watcher = w.Settlements.Get(b).Watcher;
            Assert.AreNotEqual(0, watcher, "losses worth more than a watcher's sleep: a watch");
            int told = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.WatchSet) { told++; Console.WriteLine(HistoryText.Line(w, e)); } }
            Assert.AreEqual(1, told, "history tells when a watch begins, not each night");
            Console.WriteLine("seen at b's store: " + before.ToString("0.00") + " -> " + w.Conflict.SeenAt(b).ToString("0.00"));
            Assert.GreaterOrEqual(w.Conflict.SeenAt(b), Conflict.WatchSeen, "a watcher sees takers");
            // lived: tonight's watcher goes to the store at their bedtime and stands there until two in the morning
            w.AdvanceTo(SimTime.StartOfDay(w.Now + SimTime.Days(2)) + SimTime.Hours(20) - w.LocalOffsetOf(b));
            bool stood = false;
            long end = w.Now + SimTime.Hours(7);
            while (w.Now < end && !stood)
            {
                w.AdvanceTo(w.Now + 900);
                int tonight = w.Settlements.Get(b).Watcher;
                if (tonight == 0) continue;
                var q = w.People.Get(tonight);
                if (q.Act == ActivityKind.Watch) { stood = true; Console.WriteLine(HistoryText.Doing(w, in q)); }
            }
            Assert.IsTrue(stood, "the watcher stood at the store in the night");
            // a year on with no more losses: the watch is let go
            w.AdvanceTo(w.Now + SimTime.Years(1.1));
            Assert.AreEqual(0, w.Settlements.Get(b).Watcher, "the losses forgotten enough, the watch ends");
        }

        [Test]
        public void NobodyTakesWhenNobodyIsHungry()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            long hungryDays = 0;
            for (int d = 0; d < 30; d++)
            {
                for (int id = w.People.First(); id != 0; id = w.People.Next(id)) if (Conflict.CanTake(w.People.Get(id), w.Now)) hungryDays++;
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            long takings = w.Metrics.Takings + w.Metrics.TakingsSeen;
            Console.WriteLine("a month untouched: " + takings + " takings, " + hungryDays + " hungry grown person-days");
            if (hungryDays == 0) Assert.AreEqual(0, takings, "without hunger nobody goes to a neighbour's store");
        }
    }
}
