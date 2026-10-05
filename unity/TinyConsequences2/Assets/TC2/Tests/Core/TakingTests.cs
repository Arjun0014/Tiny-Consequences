using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
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

        // ------------------------------------------------------------------ answers, news and taking back (D2-072)
        /// <summary>Premise: a man of a struck a man of b (not taking) - a wound b knows by sight.</summary>
        static int Wounding(World w, int a, int b, out int struck)
        {
            int striker = Grown(w, a);
            struck = Grown(w, b);
            ref var q = ref w.People.Get(struck);
            int wound = w.DeedBook.Record(DeedKind.Wound, striker, a, q.Household, b, 0, 0.6, Conflict.WoundWorth * 0.6, 0);
            w.DeedBook.Learn(b, wound, KnownHow.Saw);
            w.DeedBook.Learn(a, wound, KnownHow.Own);
            return wound;
        }

        static int Female(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.SexKind == Sex.Female && p.AgeYears(w.Now) >= 30) return res[i]; }
            return 0;
        }

        [Test]
        public void AmendsAreMadeWhereTiesAndMeansAllowAndRefusedWhereNot()
        {
            // made: a has grain to spare, b once fed it, and a woman of a is the mother of a man of b
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int wound = Wounding(w, a, b, out int struck);
            Full(w, a, 120);
            int help = w.DeedBook.Record(DeedKind.Relief, 0, b, 0, a, Trade.Grain, 200, 200, 0);
            w.DeedBook.Learn(a, help, KnownHow.Saw);
            int mother = Female(w, a);
            Assume.That(mother != 0);
            ref var son = ref w.People.Get(Grown(w, b, struck));
            son.Mother = mother;
            Assert.IsTrue(w.Conflict.KinAcross(a, b), "kin across the line");
            double g0 = w.DeedBook.Grievance(b, a), store = w.Settlements.Get(b).Granary;
            ref var claimant = ref w.People.Get(struck);
            long mark = w.History.LastId;
            w.Conflict.Answer(ref claimant, a, wound);
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.AmendsMade) Console.WriteLine(HistoryText.Line(w, e)); }
            var d = w.Deeds.Get(wound);
            Assert.AreNotEqual(0, d.Answer);
            Assert.AreEqual(DeedKind.Amends, w.Deeds.Get(d.Answer).Kind, "amends made");
            Assert.Greater(w.Settlements.Get(b).Granary, store, "in grain to the wronged village's store");
            Console.WriteLine("b's grievance against a: " + g0.ToString("0") + " -> " + w.DeedBook.Grievance(b, a).ToString("0"));
            Assert.AreEqual(0.0, w.DeedBook.Grievance(b, a), 1e-9, "a wrong made good weighs no more");
            Assert.Greater(w.DeedBook.Gratitude(b, a), 0.0);
            // refused: a has nothing to spare
            var w2 = Pair(out a, out b);
            w2.AdvanceTo(w2.Now + SimTime.Hours(30));
            int wound2 = Wounding(w2, a, b, out int struck2);
            w2.Settlements.Get(a).Granary = 0;
            double h0 = w2.DeedBook.Grievance(b, a);
            ref var claimant2 = ref w2.People.Get(struck2);
            mark = w2.History.LastId;
            w2.Conflict.Answer(ref claimant2, a, wound2);
            for (int i = w2.History.FirstIndexAfter(mark); i < w2.History.Count; i++) { var e = w2.History.At(i); if (e.Kind == HistoryKind.ClaimRefused) Console.WriteLine(HistoryText.Line(w2, e)); }
            Assert.AreEqual(DeedKind.Refusal, w2.Deeds.Get(w2.Deeds.Get(wound2).Answer).Kind, "refused");
            Assert.Greater(w2.DeedBook.Grievance(b, a), h0, "a refusal is a new wrong");
        }

        [Test]
        public void AClaimantWalksOverInTheMorningAndTheAnswerIsHistory()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int wound = Wounding(w, a, b, out int struck);
            // the village's day sends someone of the wronged household (lived: an errand for the morning)
            long off = w.LocalOffsetOf(b);
            w.AdvanceTo(SimTime.StartOfDay(w.Now + off) + SimTime.Day + SimTime.Hours(6) - off);   // (the first morning after the village's day)
            Assert.AreNotEqual(0, w.Deeds.Get(wound).Flags & DeedKind.Claimed, "amends asked");
            int claimant = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) if (w.People.Get(id).Errand == wound) claimant = id;
            Assume.That(claimant != 0, "someone has the errand");
            string walking = "";
            long end = w.Now + SimTime.Hours(16);
            while (w.Now < end && w.Deeds.Get(wound).Answer == 0)
            {
                w.AdvanceTo(w.Now + 600);
                var q = w.People.Get(claimant);
                if (walking.Length == 0 && q.Act == ActivityKind.TripOut && q.Why == WhyCode.Claim) walking = HistoryText.Doing(w, in q);
            }
            Console.WriteLine(walking);
            Assert.IsTrue(walking.Length > 0, "they walked over");
            Assert.AreNotEqual(0, w.Deeds.Get(wound).Answer, "and had their answer");
            var ans = w.Deeds.Get(w.Deeds.Get(wound).Answer);
            Assert.IsTrue(w.History.TryGet(ans.Event, out var told), "the answer is history");
            Console.WriteLine(HistoryText.Line(w, told));
            Assert.AreEqual(claimant, told.Other.Id, "history names who asked");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void NewsTellsWhoBeatsTakersAndTheirTakersKeepAway()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int c = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (s != a && s != b) { c = s; break; }
            // premise: c's people beat a taker from a (a knows it by sight)
            int beat = w.DeedBook.Record(DeedKind.Wound, Grown(w, c), c, w.People.Get(Grown(w, a)).Household, a, 0, 0.8, Conflict.Deserved * Conflict.WoundWorth * 0.8, 0);
            { ref var bd = ref w.Deeds.Get(beat); bd.Flags |= DeedKind.InTheAct; }
            w.DeedBook.Learn(a, beat, KnownHow.Saw);
            Assert.Greater(w.DeedBook.Answers(a, c), 0.0, "a knows c answers takers");
            Assert.AreEqual(0.0, w.DeedBook.Answers(b, c), "b does not yet");
            w.DeedBook.Meet(a, b);   // (a visit between a and b)
            Assert.Greater(w.DeedBook.Answers(b, c), 0.0, "the news reached b");
            Assert.IsTrue(w.DeedBook.Knows(b, beat, out byte how) && how == KnownHow.Told);
            // b retells nothing it only heard
            int d = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (s != a && s != b && s != c) { d = s; break; }
            w.DeedBook.Meet(b, d);
            Assert.IsFalse(w.DeedBook.Knows(d, beat, out _), "hearsay is not passed on");
            // a taker of b weighs c's store less freely (c's people are known to beat takers)
            Console.WriteLine("what b knows of c's answers: " + w.DeedBook.Answers(b, c).ToString("0.00") + " (keeps takers away " + (100 * Math.Min(1.0, w.DeedBook.Answers(b, c) / Conflict.AnswersFull)).ToString("0") + "%)");
        }

        [Test]
        public void AFedVillageHoldingAHeavyWrongTakesBack()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            // premise: b's people killed a man of a; a knows it; a is fed, b's store holds grain
            int kill = w.DeedBook.Record(DeedKind.Kill, Grown(w, b), b, w.People.Get(Grown(w, a)).Household, a, 0, 1.0, Conflict.KillWorth, 0);
            w.DeedBook.Learn(a, kill, KnownHow.Saw);
            { ref var kd = ref w.Deeds.Get(kill); kd.Flags |= DeedKind.Claimed; kd.Answer = 0; }
            long n0 = w.Metrics.TakenBack;
            for (int day = 0; day < 40; day++) { Full(w, b); w.AdvanceTo(w.Now + SimTime.Day); }
            Console.WriteLine("a's foe " + w.Settlements.Get(a).Foe + " (b " + b + "), its weight " + w.Settlements.Get(a).FoeWorth.ToString("0") + "; loads taken back " + (w.Metrics.TakenBack - n0));
            Assert.AreEqual(b, w.Settlements.Get(a).Foe, "a holds b most to account");
            Assert.Greater(w.Metrics.TakenBack - n0, 0, "and its fed and bold took back from b's store");
        }

        [Test]
        public void ASaveWithDeedsGrudgesAndNewsContinuesIdentically()
        {
            // (the sums of grievance and gratitude are state; the news a village has told another so far is not, and re-telling it
            // after a load must change nothing)
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int wound = Wounding(w, a, b, out _);
            int help = w.DeedBook.Record(DeedKind.Relief, 0, a, 0, b, Trade.Grain, 120, 120, 0);
            w.DeedBook.Learn(b, help, KnownHow.Saw);
            for (int k = 0; k < 2; k++) { int d = w.DeedBook.Record(DeedKind.Take, Grown(w, a), a, 0, b, Trade.Grain, Conflict.LoadFu, Conflict.LoadFu, 0); w.DeedBook.Learn(b, d, KnownHow.Found); }
            w.AdvanceTo(w.Now + SimTime.Days(1) + SimTime.Hours(7));   // (a claim under way, a watch kept, news told on visits)
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            var w2 = SaveFile.Load(bytes, out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "loaded as saved");
            long end = w.Now + SimTime.Days(12);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Console.WriteLine("b's grievance against a " + w.DeedBook.Grievance(b, a).ToString("0.000") + " / " + w2.DeedBook.Grievance(b, a).ToString("0.000") + "; answered " + (w.Deeds.Get(wound).Answer != 0));
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a world saved with its deeds and grudges continues as if never saved");
            Assert.IsNull(TestWorlds.Audit(w2));
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
