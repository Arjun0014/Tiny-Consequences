using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The lord's peace (C10.10): two villages of one realm raise no bands against each other, and a claim for blood between them is judged by their
    /// common lord - the nearest of the doers' lords who is the claimants' village or one of its lords - whose judgment binds the doers where it is no
    /// weaker than they: they make amends, willing or not. Between realms nothing changes. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class PeaceTests
    {
        /// <summary>Premise: three villages each within a day's walk of the others by a way on foot, no close kin across the lines between `a` and `b`;
        /// `lord` the strongest of them as a lord weighs it (with the call of the other once it pays).</summary>
        static World Three(out int lord, out int a, out int b)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            var ids = new List<int>();
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (w.Settlements.Get(s).Inhabited) ids.Add(s);
            bool Near(int x, int y) => Vec3d.ArcDistance(w.Settlements.Get(x).Anchor, w.Settlements.Get(y).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, x, y) != null;
            foreach (int l in ids) foreach (int x in ids) foreach (int y in ids)
                    {
                        if (l == x || x == y || l == y || !Near(l, x) || !Near(l, y) || !Near(x, y)) continue;
                        if (w.Conflict.KinAcross(x, y)) continue;
                        if (w.Conflict.Might(l, x) < w.Conflict.Might(x, l) || w.Conflict.Might(l, y) < w.Conflict.Might(y, l)) continue;
                        lord = l; a = x; b = y;
                        return w;
                    }
            lord = a = b = 0;
            return null;
        }

        /// <summary>A bond for peace, set and told as a bow tells it (its record the bond stands on).</summary>
        static void Bond(World w, int payer, int lord)
        {
            ref var s = ref w.Settlements.Get(payer);
            s.Lord = lord; s.LordSince = w.Now;
            s.LordEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.TributeAgreed, Time = w.Now, Cell = s.Cell, Settlement = payer, Subject = EntityRef.Settlement(payer), Object = EntityRef.Settlement(lord),
                Magnitude = (float)Conflict.TributeShare, ArgA = 1, ArgB = 2, RootKind = RootKind.People,
            });
        }

        /// <summary>Premise: `x` holds a heavy wrong against `y` (a raid of y's on its store it saw), read as its day reads its foe.</summary>
        static void Wronged(World w, int x, int y)
        {
            long ev = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Cell = w.Settlements.Get(x).Cell, Settlement = x, Subject = EntityRef.Settlement(y), Object = EntityRef.Settlement(x), RootKind = RootKind.People });
            int deed = w.DeedBook.Record(DeedKind.Raid, 0, y, 0, x, Trade.Grain, 0.0, Conflict.RaidFull, ev);
            w.DeedBook.Learn(x, deed, KnownHow.Saw);
            w.DeedBook.Learn(y, deed, KnownHow.Own);
            ref var s = ref w.Settlements.Get(x);
            s.Foe = y; s.FoeWorth = (float)Conflict.RaidFull; s.FoeEvent = ev;
        }

        static int Grown(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.AgeYears(w.Now) >= 18 && p.AgeYears(w.Now) < 50) return res[i]; }
            return 0;
        }

        /// <summary>Premise: a man of `doers` struck a man of `wronged` - a wound the wronged village knows by sight; the wronged man.</summary>
        static int Wounding(World w, int doers, int wronged, out int struck)
        {
            struck = Grown(w, wronged);
            int wound = w.DeedBook.Record(DeedKind.Wound, Grown(w, doers), doers, w.People.Get(struck).Household, wronged, 0, 0.6, Conflict.WoundWorth * 0.6, 0);
            w.DeedBook.Learn(wronged, wound, KnownHow.Saw);
            w.DeedBook.Learn(doers, wound, KnownHow.Own);
            return wound;
        }

        [Test]
        public void TheJudgeIsTheNearestOfTheDoersLordsOverTheClaimantsToo()
        {
            var w = Three(out int lord, out int a, out int b);
            Assume.That(w != null, "three villages near each other");
            var C = w.Conflict;
            Assert.AreEqual(0, C.Judge(a, b), "two free villages: no common lord");
            Assert.IsFalse(C.OnePeace(a, b));
            Bond(w, a, lord);
            Assert.AreEqual(lord, C.Judge(a, lord), "a tributary's wrong against its own lord: the lord judges it");
            Assert.AreEqual(0, C.Judge(lord, a), "nobody judges the lord itself");
            Assert.AreEqual(0, C.Judge(a, b), "a free village has no lord in common with a tributary");
            Bond(w, b, lord);
            Assert.AreEqual(lord, C.Judge(a, b), "two tributaries of one lord: their lord");
            Assert.IsTrue(C.OnePeace(a, b) && C.OnePeace(a, lord) && C.OnePeace(lord, b), "one realm");
            // b pays a, a pays the lord: a claim of a against b is judged by a (b's own lord), one of the lord's against b by the lord (a is not over it)
            ref var sb = ref w.Settlements.Get(b); sb.Lord = 0;
            Bond(w, b, a);
            Assert.AreEqual(a, C.Judge(b, a), "the tributary's tributary against its lord: that lord");
            Assert.AreEqual(lord, C.Judge(b, lord), "against its lord's lord: the nearest of its lords over the claimants, the highest");
            Conflict.PeaceOff = true;
            try { Assert.AreEqual(0, C.Judge(b, a)); Assert.IsFalse(C.OnePeace(a, b)); }
            finally { Conflict.PeaceOff = false; }
        }

        [Test]
        public void NoVillageRaisesABandAgainstAVillageOfItsOwnRealm()
        {
            Conflict.RaidTally = new double[Conflict.RaidTallyN];
            Conflict.PeaceTally = new double[Conflict.PeaceTallyN];
            try
            {
                // one realm: a holds a heavy wrong against b, both paying one lord - no band is weighed
                var w = Three(out int lord, out int a, out int b);
                Assume.That(w != null, "three villages near each other");
                Bond(w, a, lord); Bond(w, b, lord);
                Wronged(w, a, b);
                Assert.AreEqual(0, w.Conflict.RaiseBandNow(a), "the lord's peace: no band against a village of its own realm");
                Assert.AreEqual(0.0, Conflict.RaidTally[0], "nor weighed");
                Assert.AreEqual(1.0, Conflict.PeaceTally[3], "a band kept home by the peace");
                // two realms: b pays nobody - the band is weighed as before
                var w2 = Three(out lord, out a, out b);
                Bond(w2, a, lord);
                Wronged(w2, a, b);
                w2.Conflict.RaiseBandNow(a);
                Assert.AreEqual(1.0, Conflict.RaidTally[0], "across realms the wrong is weighed for a band");
                // the old rules: one realm, the band weighed
                Conflict.PeaceOff = true;
                var w3 = Three(out lord, out a, out b);
                Bond(w3, a, lord); Bond(w3, b, lord);
                Wronged(w3, a, b);
                w3.Conflict.RaiseBandNow(a);
                Assert.AreEqual(2.0, Conflict.RaidTally[0], "--oldpeace: weighed within the realm too");
            }
            finally { Conflict.PeaceOff = false; Conflict.RaidTally = null; Conflict.PeaceTally = null; }
        }

        [Test]
        public void AClaimWithinARealmIsJudgedByTheirLordAndTheDoersMakeAmendsUnwilling()
        {
            // a struck a man of b, and a holds a heavy wrong against b (unwilling: a free village would refuse); a has grain to spare
            HistoryEvent Answered(World w, int a, int b, out double paid)
            {
                int wound = Wounding(w, a, b, out int struck);
                Wronged(w, a, b);
                w.Settlements.Get(a).Granary += 20000;
                double before = w.Settlements.Get(b).Granary;
                long mark = w.History.LastId;
                ref var claimant = ref w.People.Get(struck);
                w.Conflict.Answer(ref claimant, a, wound);
                paid = w.Settlements.Get(b).Granary - before;
                for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind == HistoryKind.AmendsMade || e.Kind == HistoryKind.ClaimRefused) { Console.WriteLine(HistoryText.Line(w, e)); return e; }
                }
                return default;
            }
            var w1 = Three(out int lord, out int a1, out int b1);
            Assume.That(w1 != null, "three villages near each other");
            Bond(w1, a1, lord); Bond(w1, b1, lord);
            var e1 = Answered(w1, a1, b1, out double paid1);
            Assert.AreEqual(HistoryKind.AmendsMade, e1.Kind, "judged: amends made");
            Assert.AreEqual(lord, e1.ArgB, "at the judgment of their lord");
            Assert.Greater(paid1, 0.0, "in grain to the wronged village's store");
            StringAssert.Contains("at the judgment of " + w1.History.NameOf(EntityRef.Settlement(lord)), HistoryText.Line(w1, e1));
            // the book tells the realm's peace, its line the judgment's record
            var realms = HistoryBook.Write(w1).Parts[1];
            var rc = realms.Chapters.Find(x => x.Title == "The Realm of " + w1.History.NameOf(EntityRef.Settlement(lord)));
            Assert.IsNotNull(rc, "the lord's realm is told");
            Assert.IsTrue(rc.Blocks.Exists(x => x.Kind != BookKind.Line && x.Text == "Its peace"), "with its peace");
            Assert.IsTrue(rc.Blocks.Exists(x => x.Kind == BookKind.Line && x.Event == e1.Id), "and the judgment's own line");
            // the same claim between two realms (b pays nobody): refused
            var w2 = Three(out lord, out int a2, out int b2);
            Bond(w2, a2, lord);
            var e2 = Answered(w2, a2, b2, out double paid2);
            Assert.AreEqual(HistoryKind.ClaimRefused, e2.Kind, "no common lord: an unwilling village refuses");
            Assert.AreEqual(0.0, paid2);
            // the old rules, one realm: refused
            Conflict.PeaceOff = true;
            try
            {
                var w3 = Three(out lord, out int a3, out int b3);
                Bond(w3, a3, lord); Bond(w3, b3, lord);
                var e3 = Answered(w3, a3, b3, out _);
                Assert.AreEqual(HistoryKind.ClaimRefused, e3.Kind, "--oldpeace: refused within the realm too");
            }
            finally { Conflict.PeaceOff = false; }
        }
    }
}
