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
    /// <summary>
    /// The asking (C10.8, D2-168): at a free village's harvest its nearest neighbour within a day's walk, joined by a way on foot, outside its lordship,
    /// whose first among the people keeps sworn men and which is the stronger by the odds of a bow, asks it a tenth. Its people bow where they could
    /// neither stand against them nor go elsewhere; else they refuse, and the refusal is a wrong the asker holds.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class DemandTests
    {
        /// <summary>Premise: two inhabited villages within a day's walk joined by a way on foot, the asker the stronger by the odds of a bow (`Might` with its
        /// call), `men` fit men of the asker's village sworn to the one first among its people (as its harvest would swear them; nobody else in the young
        /// world keeps any).</summary>
        static World Pair(out int asker, out int asked, int men = 3, ulong from = 901)
        {
            for (ulong seed = from; seed < from + 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (int b = w.Settlements.First(); b != 0; b = w.Settlements.Next(b))
                    {
                        if (a == b || !w.Settlements.Get(a).Inhabited || !w.Settlements.Get(b).Inhabited) continue;
                        if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) > Conflict.BreadReach) continue;
                        if (w.Routes.Get(w, a, b) == null) continue;
                        if (w.Conflict.Might(a, b) < Conflict.TributeOdds * w.Conflict.Might(b, a)) continue;
                        if (!Swear(w, a, men)) continue;
                        asker = a; asked = b;
                        return w;
                    }
            }
            asker = asked = 0;
            return null;
        }

        /// <summary>`men` fit men of 18-40 of a village sworn to the one first among its people - only if it has that many (nothing changed otherwise).</summary>
        static bool Swear(World w, int sid, int men)
        {
            Leaders.Weigh(w, sid);
            int head = w.Settlements.Get(sid).Head;
            if (head == 0) return false;
            var res = w.Residents(sid);
            var can = new List<int>();
            for (int i = 0; i < res.Count; i++)
            {
                var q = w.People.Get(res[i]);
                double age = q.AgeYears(w.Now);
                if (!q.Alive || q.Id == head || q.SexKind != Sex.Male || q.Household == 0 || age < Retinue.FromAge || age >= Retinue.ToAge || !Conflict.Fit(in q, w.Now)) continue;
                can.Add(res[i]);
            }
            if (can.Count < men) return false;
            for (int i = 0; i < men; i++) w.People.Get(can[i]).Sworn = head;
            return Retinue.Count(w, sid) == men;
        }

        static List<HistoryEvent> Since(World w, long mark, HistoryKind kind)
        {
            var l = new List<HistoryEvent>();
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == kind) l.Add(w.History.At(i));
            return l;
        }

        [Test]
        public void AVillageWhosePeopleCannotGoElsewhereBowsWhenItsStrongerNeighbourKeepingSwornMenAsksItsTenth()
        {
            var w = Pair(out int asker, out int asked);
            Assume.That(w != null, "two villages a way on foot apart, the asker the stronger, with three sworn men");
            w.Settlements.Get(asked).Lore = KnownSites.Empty;   // (no good land known: they cannot go elsewhere)
            long mark = w.History.LastId;
            double harvest = w.Settlements.Get(asked).Granary;
            w.Conflict.Harvested(asked, harvest);
            ref var s = ref w.Settlements.Get(asked);
            Assert.AreEqual(asker, s.Lord, "it bows to the village that asked");
            var asked1 = Since(w, mark, HistoryKind.TributeDemanded);
            Assert.AreEqual(1, asked1.Count, "asked once");
            var e = asked1[0];
            Assert.AreEqual(asker, e.Subject.Id); Assert.AreEqual(asked, e.Object.Id); Assert.AreEqual(1, e.ArgA, "it bowed");
            Assert.AreEqual(Conflict.TributeShare * Math.Max(harvest, w.Settlements.Get(asked).LastHarvest), e.Magnitude, 1.0, "a tenth of its harvest");
            Assert.AreNotEqual(0L, e.Cause1, "the men the asker keeps made the asking (the swearing of one of them)");
            Assert.IsTrue(w.History.TryGet(s.LordEvent, out var bond));
            Assert.AreEqual(HistoryKind.TributeAgreed, bond.Kind);
            Assert.AreEqual(2, bond.ArgB, "on its asking");
            Assert.AreEqual(e.Id, bond.Cause1, "the bond's cause is the asking");
            Console.WriteLine(HistoryText.Line(w, e));
            Console.WriteLine(HistoryText.Line(w, bond));
            StringAssert.Contains("asked " + w.History.NameOf(EntityRef.Settlement(asked)) + " a tenth", HistoryText.Line(w, e));
            StringAssert.Contains("when it asked", HistoryText.Line(w, bond));
            StringAssert.Contains("when it asked a tenth, its people could neither stand against its men nor go elsewhere", Gazetteer.All(w, asked));
        }

        [Test]
        public void UnderTheOldRulesNobodyAsks()
        {
            Conflict.DemandOff = true;
            try
            {
                var w = Pair(out int asker, out int asked);
                Assume.That(w != null, "the premise");
                w.Settlements.Get(asked).Lore = KnownSites.Empty;
                long mark = w.History.LastId;
                w.Conflict.Harvested(asked, w.Settlements.Get(asked).Granary);
                Assert.AreEqual(0, w.Settlements.Get(asked).Lord, "--olddemand: it pays nobody");
                Assert.AreEqual(0, Since(w, mark, HistoryKind.TributeDemanded).Count, "--olddemand: nobody asks");
            }
            finally { Conflict.DemandOff = false; }
        }

        [Test]
        public void AVillageThatKnowsGoodLandElsewhereRefusesAndTheAskerHoldsTheRefusalAgainstIt()
        {
            var w = Pair(out int asker, out int asked);
            Assume.That(w != null, "the premise");
            ref var sa = ref w.Settlements.Get(asked);
            sa.Lore = KnownSites.Empty;
            sa.Lore.Learn(sa.Cell == 0 ? 1 : 0, 0.9, w.Now);   // (good land known: they could go)
            long mark = w.History.LastId;
            double before = w.DeedBook.Grievance(asker, asked);
            Assume.That(Conflict.TributeShare * w.Settlements.Get(asked).Granary >= Conflict.RaidFrom, "a tenth heavy enough to raise a band");
            w.Conflict.Harvested(asked, w.Settlements.Get(asked).Granary);
            Assert.AreEqual(0, w.Settlements.Get(asked).Lord, "it pays nobody");
            var asked1 = Since(w, mark, HistoryKind.TributeDemanded);
            Assert.AreEqual(1, asked1.Count, "asked once");
            Assert.AreEqual(0, asked1[0].ArgA, "it refused");
            Console.WriteLine(HistoryText.Line(w, asked1[0]));
            StringAssert.Contains("would not pay", HistoryText.Line(w, asked1[0]));
            Assert.Greater(w.DeedBook.Grievance(asker, asked), before, "the refusal is a wrong the asker holds");
            // the asker's day reads its foe: the refusal is a wrong heavy enough for its bands (D2-073), whatever the day then brings
            w.AdvanceTo(w.Now + SimTime.Day);
            Assert.AreEqual(asked, w.Settlements.Get(asker).Foe, "the asker's foe is the village that refused");
            Assert.GreaterOrEqual(w.Settlements.Get(asker).FoeWorth, (float)Conflict.RaidFrom, "heavy enough to raise a band");
        }

        [Test]
        public void NoAskingWithoutSwornMenOrByTheWeakerOrOfAVillageThatPaysALord()
        {
            // too few sworn men: no band of its own to come for the tenth
            var w = Pair(out int asker, out int asked, Conflict.DemandMen - 1);
            Assume.That(w != null, "the premise with too few men");
            w.Settlements.Get(asked).Lore = KnownSites.Empty;
            long mark = w.History.LastId;
            w.Conflict.Harvested(asked, w.Settlements.Get(asked).Granary);
            Assert.AreEqual(0, Since(w, mark, HistoryKind.TributeDemanded).Count, "too few sworn men: nobody asks");
            // the weaker does not ask the stronger
            var w2 = Pair(out int a2, out int b2);
            Assume.That(w2 != null, "the premise");
            Assume.That(Swear(w2, b2, Conflict.DemandMen) || Retinue.Count(w2, b2) >= Conflict.DemandMen, "the weaker keeping men too");
            w2.Settlements.Get(a2).Lore = KnownSites.Empty;
            long mark2 = w2.History.LastId;
            w2.Conflict.Harvested(a2, w2.Settlements.Get(a2).Granary);
            Assert.AreEqual(0, w2.Settlements.Get(a2).Lord, "the stronger is not made to pay by the weaker");
            foreach (var e in Since(w2, mark2, HistoryKind.TributeDemanded)) Assert.AreNotEqual(b2, e.Subject.Id, "the weaker never asks the stronger");
            // a village that pays a lord already is not asked
            var w3 = Pair(out int a3, out int b3);
            Assume.That(w3 != null, "the premise");
            int other = 0;
            for (int o = w3.Settlements.First(); o != 0 && other == 0; o = w3.Settlements.Next(o)) if (o != a3 && o != b3 && w3.Settlements.Get(o).Inhabited) other = o;
            Assume.That(other != 0, "a third village");
            w3.Settlements.Get(b3).Lord = other; w3.Settlements.Get(b3).LordSince = w3.Now;
            w3.Settlements.Get(b3).Lore = KnownSites.Empty;
            long mark3 = w3.History.LastId;
            w3.Conflict.Harvested(b3, w3.Settlements.Get(b3).Granary);
            Assert.AreEqual(0, Since(w3, mark3, HistoryKind.TributeDemanded).Count, "a tributary is not asked");
            // the asker's own lord is not asked by it
            var w4 = Pair(out int a4, out int b4);
            Assume.That(w4 != null, "the premise");
            w4.Settlements.Get(a4).Lord = b4; w4.Settlements.Get(a4).LordSince = w4.Now;
            w4.Settlements.Get(b4).Lore = KnownSites.Empty;
            long mark4 = w4.History.LastId;
            w4.Conflict.Harvested(b4, w4.Settlements.Get(b4).Granary);
            foreach (var e in Since(w4, mark4, HistoryKind.TributeDemanded)) Assert.AreNotEqual(a4, e.Subject.Id, "a tributary never asks its own lord");
        }
    }
}
