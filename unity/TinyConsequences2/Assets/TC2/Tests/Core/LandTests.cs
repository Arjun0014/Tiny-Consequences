using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Household land (C3.6b, D2-133): each household holds hectares of its village's fields - founders by their heads, a marrying child a portion of
    /// what the family's land can spare, the last to leave a household the rest, an heir what a household that died out held; those who set out
    /// leave theirs to the village, and at sowing land lost is lost from every holding by the same share while unheld land goes to the least landed
    /// first. Holdings move nothing else (yet). Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class LandTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static double Held(World w, int sid)
        {
            double h = 0; var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) h += w.Households.Get(hl[i]).Land;
            return h;
        }

        [Test]
        public void FoundersHoldTheFieldsByTheirHeads()
        {
            var w = TestWorlds.Make(5101);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                Assert.AreEqual(s.ArableHa, Held(w, sid), 1e-6, "all its arable is held");
                Assert.AreEqual(0.0, w.Holdings.Unheld(sid), 1e-6);
                var hl = w.HouseholdsOf(sid);
                int heads = 0; for (int i = 0; i < hl.Count; i++) heads += w.Members(hl[i]).Count;
                for (int i = 0; i < hl.Count; i++)
                    Assert.AreEqual(s.ArableHa * w.Members(hl[i]).Count / heads, w.Households.Get(hl[i]).Land, 1e-6, "by its heads");
            }
        }

        [Test]
        public void AMarryingChildTakesAPortionOnlyOfWhatTheLandCanSpare()
        {
            var w = TestWorlds.Make(5102);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            int from = 0;
            for (int i = 0; i < hl.Count && from == 0; i++) if (w.Members(hl[i]).Count >= 3) from = hl[i];
            Assume.That(from != 0, "a household of three or more");
            ref var a = ref w.Households.Get(from);
            double want = w.Holdings.LandWant(from);
            var mem = w.Members(from);
            int child = mem[mem.Count - 1];
            // premise: the family holds twice what its people would sow; a new household for the child
            a.Land = 2 * want;
            int to = w.Ids.Next(EntityKind.Household);
            w.Households.Add(to, new Household { Id = to, Settlement = sid, Formed = w.Now });
            w.HouseholdsOf(sid).InsertSorted(to);
            mem.Remove(child);
            double left = w.Holdings.LandWant(from), before = w.Households.Get(from).Land;
            w.Holdings.Portion(from, to);
            double head = w.Config.FieldHaPerPerson * (1.0 + 0.5 * w.Settlements.Get(sid).Memory);
            Assert.AreEqual(System.Math.Min(before - left, head), w.Households.Get(to).Land, 1e-9, "a portion: the child's want, as far as the land spares it");
            Assert.AreEqual(before, w.Households.Get(from).Land + w.Households.Get(to).Land, 1e-9, "land moves, it is not made");
            // a family whose land is no more than its want gives nothing
            ref var a2 = ref w.Households.Get(from); a2.Land = w.Holdings.LandWant(from);
            int to2 = w.Ids.Next(EntityKind.Household);
            w.Households.Add(to2, new Household { Id = to2, Settlement = sid, Formed = w.Now });
            w.HouseholdsOf(sid).InsertSorted(to2);
            w.Holdings.Portion(from, to2);
            Assert.AreEqual(0.0, w.Households.Get(to2).Land, 1e-12, "where the family's land is short, no portion");
        }

        [Test]
        public void TheLastOutTheHeirAndThoseWhoGoEachTakeOrLeaveTheLand()
        {
            var w = TestWorlds.Make(5103);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int a = hl[0], b = hl[1], c = hl[2];
            double la = w.Households.Get(a).Land, lb = w.Households.Get(b).Land, lc = w.Households.Get(c).Land;
            Assume.That(la > 0 && lb > 0 && lc > 0);
            // the last to leave a household for a new one in the village takes its land
            w.Holdings.Close(a, b);
            Assert.AreEqual(0.0, w.Households.Get(a).Land);
            Assert.AreEqual(la + lb, w.Households.Get(b).Land, 1e-9, "the last out takes it");
            // a household that died out: its heir takes its land
            w.Holdings.Inherit(b, c, sid);
            Assert.AreEqual(la + lb + lc, w.Households.Get(c).Land, 1e-9, "the heir takes it");
            // no heir: the village's, unheld
            double before = w.Holdings.Unheld(sid);
            w.Holdings.Inherit(c, 0, sid);
            Assert.AreEqual(before + la + lb + lc, w.Holdings.Unheld(sid), 1e-6, "with no heir it is the village's");
            // those who set out leave theirs to the village
            var hl2 = w.HouseholdsOf(sid);
            int d = 0; for (int i = 0; i < hl2.Count; i++) if (w.Households.Get(hl2[i]).Land > 0) { d = hl2[i]; break; }
            Assume.That(d != 0);
            double ld = w.Households.Get(d).Land, u0 = w.Holdings.Unheld(sid);
            w.Holdings.SetOut(sid, new List<int> { d }, 0);
            Assert.AreEqual(0.0, w.Households.Get(d).Land);
            Assert.AreEqual(u0 + ld, w.Holdings.Unheld(sid), 1e-6, "left to the village");
        }

        [Test]
        public void AtSowingUnheldLandGoesToTheLeastLandedFirst()
        {
            var w = TestWorlds.Make(5104);
            Coarse(w);
            int sid = w.Settlements.First();
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            // premise: one household's land left unheld (it went), another landless, the rest as they were
            int gone = hl[0], poor = hl[1];
            ref var g = ref w.Households.Get(gone); double free = g.Land; g.Land = 0;
            ref var p = ref w.Households.Get(poor); free += p.Land; p.Land = 0;
            w.Holdings.LandYear(sid);
            double wantPoor = w.Holdings.LandWant(poor), wantGone = w.Holdings.LandWant(gone);
            Assert.Greater(w.Households.Get(gone).Land, 0.0, "the landless are given land first");
            Assert.Greater(w.Households.Get(poor).Land, 0.0);
            Assert.LessOrEqual(w.Households.Get(poor).Land, wantPoor + 1e-9, "up to what they would sow");
            Assert.AreEqual(System.Math.Min(free, wantGone + wantPoor), w.Households.Get(gone).Land + w.Households.Get(poor).Land, 1e-6, "as far as the unheld land goes");
            Assert.AreEqual(w.Settlements.Get(sid).ArableHa, Held(w, sid) + w.Holdings.Unheld(sid), 1e-6);
            // land lost: every holding loses the same share
            ref var s = ref w.Settlements.Get(sid);
            double held = Held(w, sid), l1 = w.Households.Get(hl[2]).Land;
            s.ArableHa = 0.5 * held;
            w.Holdings.LandYear(sid);
            Assert.AreEqual(0.5 * l1, w.Households.Get(hl[2]).Land, 1e-9, "half the land lost, half of each holding");
        }

        [Test]
        public void HoldingsMoveNothingElse()
        {
            int people0 = 0, people1 = 0; double grain0 = 0, grain1 = 0; long births0 = 0, births1 = 0;
            foreach (bool off in new[] { true, false })
            {
                Holdings.LandOff = off;
                try
                {
                    var w = TestWorlds.Make(5105);
                    Coarse(w);
                    w.AdvanceTo(w.Now + SimTime.Years(3));
                    int n = w.LivingPeople(); double g = 0;
                    for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid)) g += w.Settlements.Get(sid).Granary;
                    if (off) { people0 = n; grain0 = g; births0 = w.Metrics.Births; } else { people1 = n; grain1 = g; births1 = w.Metrics.Births; }
                }
                finally { Holdings.LandOff = false; }
            }
            Assert.AreEqual(people0, people1, "the same people");
            Assert.AreEqual(births0, births1);
            Assert.AreEqual(grain0, grain1, 1e-9, "the same grain");
        }

        [Test]
        public void ASaveKeepsTheHoldings()
        {
            var w = TestWorlds.Make(5106);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(2) + SimTime.Days(5));
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash());
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid)) Assert.AreEqual(w.Households.Get(hid).Land, w2.Households.Get(hid).Land);
            w.AdvanceTo(w.Now + SimTime.Years(1)); w2.AdvanceTo(w2.Now + SimTime.Years(1));
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and lives on the same");
        }
    }
}
