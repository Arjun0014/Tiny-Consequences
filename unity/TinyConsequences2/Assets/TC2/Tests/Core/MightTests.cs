using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>A lordship's might (C10.5, D2-159): villages weigh each other by their people of fighting age - a day's wounds, sickness and hunger
    /// pass and do not count - and a lord by the men its call brings from its tributaries near the village weighed (C10.1's own call).</summary>
    [TestFixture]
    public class MightTests
    {
        /// <summary>Premise: a lord, its tributary and a third village within a day's walk of both (the tributary with a way to it): the lord alone
        /// is not the stronger by `TributeOdds`, with the men its tributary owes its call it is.</summary>
        static World Triple(out int lord, out int trib, out int other, ulong from = 901)
        {
            for (ulong seed = from; seed < from + 60; seed++)
            {
                var w = TestWorlds.Make(seed);
                var live = new List<int>();
                for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (w.Settlements.Get(s).Inhabited) live.Add(s);
                foreach (int l in live)
                    foreach (int t in live)
                        foreach (int v in live)
                        {
                            if (l == t || l == v || t == v) continue;
                            if (Far(w, l, v, Conflict.BreadReach) || Far(w, t, v, 6000) || w.Routes.Get(w, t, v) == null) continue;
                            if (w.Conflict.Hands(l) >= Conflict.TributeOdds * w.Conflict.Hands(v)) continue;   // (the lord alone is not the stronger)
                            w.Settlements.Get(t).Lord = l;
                            if (w.Conflict.Might(l, v) >= Conflict.TributeOdds * w.Conflict.Might(v, l)) { lord = l; trib = t; other = v; return w; }
                            w.Settlements.Get(t).Lord = 0;
                        }
            }
            lord = trib = other = 0;
            return null;
        }

        /// <summary>Premise: two villages within a day's walk, the first the stronger by its people of fighting age (`TributeOdds` with `odds`).</summary>
        static World Pair(out int strong, out int weak, double odds, ulong from = 931)
        {
            for (ulong seed = from; seed < from + 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (int b = w.Settlements.First(); b != 0; b = w.Settlements.Next(b))
                    {
                        if (a == b || !w.Settlements.Get(a).Inhabited || !w.Settlements.Get(b).Inhabited || Far(w, a, b, 6000) || w.Routes.Get(w, a, b) == null) continue;
                        if (w.Conflict.Hands(a) >= odds * w.Conflict.Hands(b) && w.Conflict.Hands(b) >= 4) { strong = a; weak = b; return w; }
                    }
            }
            strong = weak = 0;
            return null;
        }

        static bool Far(World w, int a, int b, double m) => Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) > m;

        /// <summary>The giver's relief to the receiver in a famine: `days` of the receiver's people's food (as `Trade.Given` records it).</summary>
        static void Gift(World w, int giver, int receiver, double days)
        {
            ref var r = ref w.Settlements.Get(receiver);
            Trade.DaysOfFood(w, in r, out double daily);
            double fu = days * daily;
            long told = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.ReliefSent, Time = w.Now, Cell = r.Cell, Settlement = receiver, Subject = EntityRef.Settlement(giver),
                Object = EntityRef.Settlement(receiver), Magnitude = (float)fu, RootKind = RootKind.People,
            });
            int deed = w.DeedBook.Record(DeedKind.Relief, 0, giver, 0, receiver, Trade.Grain, fu, fu, told);
            w.DeedBook.Learn(receiver, deed, KnownHow.Saw);
            w.DeedBook.Learn(giver, deed, KnownHow.Own);
        }

        /// <summary>A village's store at `days` of its food (nothing to spare below 60).</summary>
        static void Store(World w, int sid, double days)
        {
            ref var s = ref w.Settlements.Get(sid);
            Trade.DaysOfFood(w, in s, out double daily);
            s.Granary = days * daily;
            s.Sheaves = 0;
        }

        /// <summary>Every grown person of a village wounded and wasted - a bad season, not a lasting loss.</summary>
        static void BadSeason(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (q.Alive && q.AgeYears(w.Now) >= 16) { q.Hurt = 0.5f; q.Health = 0.5f; } }
        }

        [Test]
        public void ALordIsAsStrongAsTheMenItsCallBrings()
        {
            var w = Triple(out int l, out int t, out int v);
            Assume.That(w != null, "a lord, its tributary and a village near both");
            // its might against v: its own people of fighting age and the share of its tributary's people its call brings (never those with close kin in v)
            double expect = w.Conflict.Hands(l);
            var res = w.Residents(t);
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                double age = q.AgeYears(w.Now);
                if (q.Alive && age >= 16 && age < 50 && !w.Conflict.KinIn(in q, v)) expect += Conflict.CallOwed + Conflict.CallBold * q.Bold / 255.0;
            }
            Console.WriteLine("the lord's own " + w.Conflict.Hands(l) + ", with its call " + w.Conflict.Might(l, v).ToString("0.0") + "; the village " + w.Conflict.Hands(v));
            Assert.AreEqual(expect, w.Conflict.Might(l, v), 1e-9);
            Assert.Greater(w.Conflict.Might(l, v), (double)w.Conflict.Hands(l), "its tributary's men count");
            Assert.AreEqual((double)w.Conflict.Hands(l), w.Conflict.Might(l, t), 1e-9, "never the village weighed itself");
            // it fed v in its famines, and v cannot give it back: v bows to the lord whose call would bring its tributary's men
            Gift(w, l, v, 45.0);
            Store(w, v, 40.0);
            int fitL = w.Conflict.FitAt(l, w.Now), fitV = w.Conflict.FitAt(v, w.Now);
            w.Conflict.Harvested(v, w.Settlements.Get(v).Granary);
            Assert.AreEqual(l, w.Settlements.Get(v).Lord, "it bows to the lord and the men its call brings");
            // --oldmight: the lord's own village alone, as D2-075 weighed it
            Assume.That(fitL < Conflict.TributeOdds * fitV, "the lord's own village alone not the stronger by the old count");
            var w2 = Triple(out int l2, out int t2, out int v2);
            Gift(w2, l2, v2, 45.0);
            Store(w2, v2, 40.0);
            Conflict.MightOff = true;
            try { w2.Conflict.Harvested(v2, w2.Settlements.Get(v2).Granary); }
            finally { Conflict.MightOff = false; }
            Assert.AreEqual(0, w2.Settlements.Get(v2).Lord, "--oldmight: not to the lord's village alone");
        }

        [Test]
        public void ABondHoldsThroughTheLordsBadSeasonAndEndsWhenItsPeopleAreGone()
        {
            var w = Pair(out int lord, out int trib, 1.0);
            Assume.That(w != null, "two villages a walk apart, the first the stronger");
            ref var st = ref w.Settlements.Get(trib);
            st.Lord = lord; st.LordSince = w.Now;
            // the lord's grown people wounded and wasted: by the day's count the tributary is the stronger - but a season's wounds mend
            BadSeason(w, lord);
            Assume.That(w.Conflict.FitAt(lord, w.Now) < w.Conflict.FitAt(trib, w.Now), "the day's count would free it");
            w.Settlements.Get(trib).Granary += 500;
            w.Conflict.Harvested(trib, 500);
            Assert.AreEqual(lord, w.Settlements.Get(trib).Lord, "it pays: its lord's people are hurt, not gone");
            string page = Gazetteer.Neighbours(w, trib);
            Console.Write(page);
            StringAssert.Contains("could bring", page, "the page tells the weighing it paid by");
            // a sickness kills the lord's grown people one by one until the tributary is the stronger: it pays no more
            var res = new List<int>();
            var rl = w.Residents(lord);
            for (int i = 0; i < rl.Count; i++) res.Add(rl[i]);
            for (int i = 0; i < res.Count && w.Conflict.Might(lord, trib) >= w.Conflict.Might(trib, lord); i++)
            {
                ref var q = ref w.People.Get(res[i]);
                if (q.Alive && q.AgeYears(w.Now) >= 16 && q.AgeYears(w.Now) < 60) w.Agents.Kill(ref q, 0, 10);
            }
            long mark = w.History.LastId;
            w.Settlements.Get(trib).Granary += 500;
            w.Conflict.Harvested(trib, 500);
            Assert.AreEqual(0, w.Settlements.Get(trib).Lord, "no longer the stronger: it pays no more");
            HistoryEvent ended = default;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.TributeEnded) ended = e; }
            Assert.AreEqual(HistoryKind.TributeEnded, ended.Kind);
            Assert.AreEqual(0, ended.ArgA, "not gone: grown too weak");
            string line = HistoryText.Line(w, ended);
            Console.WriteLine(line);
            StringAssert.Contains("of fighting age", line, "history tells the weighing");
            // --oldmight: the same bad season frees the tributary (D2-075's count of the day)
            var w2 = Pair(out int lord2, out int trib2, 1.0);
            ref var st2 = ref w2.Settlements.Get(trib2);
            st2.Lord = lord2; st2.LordSince = w2.Now;
            BadSeason(w2, lord2);
            w2.Settlements.Get(trib2).Granary += 500;
            Conflict.MightOff = true;
            try { w2.Conflict.Harvested(trib2, 500); }
            finally { Conflict.MightOff = false; }
            Assert.AreEqual(0, w2.Settlements.Get(trib2).Lord, "--oldmight: the day's count frees it");
        }

        [Test]
        public void ABeatenVillageWeighsItsPeopleNotItsWoundedAbed()
        {
            // premise: b's band held the field at a this month, a knows no good land to go to and what b costs it outweighs a tribute; a and b alike in
            // people of fighting age - but half a's grown people lie wounded from the fight
            var w = Pair(out int a, out int b, 1.0);
            Assume.That(w != null, "two villages a walk apart");
            if (w.Conflict.Hands(b) >= Conflict.TributeOdds * w.Conflict.Hands(a)) { int x = a; a = b; b = x; }   // (b not the stronger by its people)
            Assume.That(w.Conflict.Hands(b) < Conflict.TributeOdds * w.Conflict.Hands(a));
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Day);
            Raided(w, a, b);
            var ra = w.Residents(a);
            int k = 0;
            for (int i = 0; i < ra.Count; i++) { ref var q = ref w.People.Get(ra[i]); if (q.Alive && q.AgeYears(w.Now) >= 16 && (k++ % 2) == 0) q.Hurt = 0.6f; }
            Assume.That(w.Conflict.FitAt(b, w.Now) >= Conflict.TributeOdds * w.Conflict.FitAt(a, w.Now), "the day's count would have it bow");
            Conflict.MightTally = new double[14];
            try { w.AdvanceTo(w.Now + SimTime.Day); }
            finally { Console.WriteLine("weighings: bowing " + Conflict.MightTally[0] + ", the old count would " + Conflict.MightTally[1] + ", might " + Conflict.MightTally[2]); Conflict.MightTally = null; }
            Assert.AreEqual(0, w.Settlements.Get(a).Lord, "its wounded mend: it does not bow to a neighbour no stronger than its people");
        }

        [Test]
        public void OldMightBowsOnTheWoundedCount()
        {
            // the same premise under `--oldmight`: D2-075 weighed the day's fit at home, half a's grown people abed - it bowed (and paid no more once
            // they mended: the flickering bonds of session 23)
            var w = Pair(out int a, out int b, 1.0);
            Assume.That(w != null, "two villages a walk apart");
            if (w.Conflict.Hands(b) >= Conflict.TributeOdds * w.Conflict.Hands(a)) { int x = a; a = b; b = x; }
            Conflict.MightOff = true;
            try
            {
                w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Day);
                Raided(w, a, b);
                var ra = w.Residents(a);
                int k = 0;
                for (int i = 0; i < ra.Count; i++) { ref var q = ref w.People.Get(ra[i]); if (q.Alive && q.AgeYears(w.Now) >= 16 && (k++ % 2) == 0) q.Hurt = 0.6f; }
                Assume.That(w.Conflict.FitAt(b, w.Now) >= Conflict.TributeOdds * w.Conflict.FitAt(a, w.Now), "the day's count would have it bow");
                w.AdvanceTo(w.Now + SimTime.Day);
                Assert.AreEqual(b, w.Settlements.Get(a).Lord, "--oldmight: it bows on the day's count, its wounded abed");
            }
            finally { Conflict.MightOff = false; }
        }

        /// <summary>Premise: b's band held the field at a (a raid a saw, a killing b's people did), a knows no good land to go to, its harvest
        /// such that what b costs it a year outweighs a tenth of it.</summary>
        static void Raided(World w, int a, int b)
        {
            int doer = 0;
            var rb = w.Residents(b);
            for (int i = 0; i < rb.Count; i++) { var p = w.People.Get(rb[i]); double age = p.AgeYears(w.Now); if (p.Alive && age >= 18 && age < 50) { doer = rb[i]; break; } }
            long ev = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Settlement = a, Subject = EntityRef.Settlement(b), Object = EntityRef.Settlement(a) });
            int kill = w.DeedBook.Record(DeedKind.Kill, doer, b, 0, a, 0, 1.0, Conflict.KillWorth, 0);
            { ref var kd = ref w.Deeds.Get(kill); kd.Flags |= DeedKind.Claimed; }
            w.DeedBook.Learn(a, kill, KnownHow.Saw);
            int raid = w.DeedBook.Record(DeedKind.Raid, doer, b, 0, a, Trade.Grain, 120, 150, ev);
            w.DeedBook.Learn(a, raid, KnownHow.Saw);
            ref var sa = ref w.Settlements.Get(a);
            sa.Lore = KnownSites.Empty;
            if (sa.ExpectedHarvest < 500) sa.ExpectedHarvest = 500;
        }
    }
}
