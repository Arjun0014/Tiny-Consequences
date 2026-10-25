using System.Collections.Generic;
using NUnit.Framework;
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
    /// The burning-in (C8.0): a band that holds the field fires the homes of the raided village's people who killed its own - killings it knows by name,
    /// not made good - and the vengeance answers the killing; the household goes out to an empty home or a camp with what did not burn. Never for wounds
    /// alone, never unseen, never with the rule off. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class BurnTests
    {
        static World Pair(out int a, out int b)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        /// <summary>A grown person of a village whose household has a home standing.</summary>
        static int Housed(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]); double age = p.AgeYears(w.Now);
                if (!p.Alive || age < 18 || age >= 50 || !w.Households.Exists(p.Household)) continue;
                int bid = w.Households.Get(p.Household).Building;
                if (bid != 0 && w.Buildings.Exists(bid) && !Homes.Fallen(in w.Buildings.Get(bid), w.Now)) return res[i];
            }
            return 0;
        }

        static List<int> Band(World w, int sid, int n)
        {
            var raiders = new List<int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count && raiders.Count < n; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.AgeYears(w.Now) >= 18 && q.AgeYears(w.Now) < 45 && q.Health > 0.8) raiders.Add(res[i]); }
            return raiders;
        }

        // premise: none of the raided village's people is fit to come out (abed - health under what a fight wants): the band holds the field
        static void NoneFit(World w, int sid) { var res = w.Residents(sid); for (int i = 0; i < res.Count; i++) w.People.Get(res[i]).Health = 0.5f; }

        // premise: a person of `at` killed a man of `of` (the killing seen: `of` knows the killer by name)
        static int Killing(World w, int killer, int at, int of, byte kind = DeedKind.Kill)
        {
            int d = w.DeedBook.Record(kind, killer, at, 0, of, 0, 1.0, kind == DeedKind.Kill ? Conflict.KillWorth : Conflict.WoundWorth, 0);
            w.DeedBook.Learn(of, d, KnownHow.Saw);
            return d;
        }

        static int Burnings(World w, long mark)
        {
            int n = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.BuildingBurned) n++;
            return n;
        }

        [Test]
        public void ABandThatHoldsTheFieldBurnsTheKillersHomeAndTheKillingIsAnswered()
        {
            Conflict.BurnOff = false;
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int killer = Housed(w, a);
            Assume.That(killer != 0, "a housed grown person in the raided village");
            int hid = w.People.Get(killer).Household, home = w.Households.Get(hid).Building;
            int kill = Killing(w, killer, a, b);
            double before = w.DeedBook.Grievance(b, a);
            { ref var hh = ref w.Households.Get(hid); hh.Larder = 40; }
            var raiders = Band(w, b, 6);
            Assume.That(raiders.Count == 6, "a band of six");
            NoneFit(w, a);
            long mark = w.History.LastId;
            w.Conflict.FallNow(b, a, raiders, seen: true);
            Assert.AreEqual(1, Burnings(w, mark), "the killer's home burned");
            var bd = w.Buildings.Get(home);
            Assert.IsTrue(Homes.Fallen(in bd, w.Now), "a ruin at once");
            Assert.AreEqual(0, bd.Household, "nobody lives in it");
            var h = w.Households.Get(hid);
            Assert.AreNotEqual(home, h.Building, "the household lives elsewhere now: an empty home or a camp");
            Assert.AreEqual(0.0, h.Larder, 1e-9, "its larder burned with it");
            Assert.IsTrue(DeedBook.Settled(in w.Deeds.Get(kill)), "the killing is answered");
            Assert.Less(w.DeedBook.Grievance(b, a), before, "and leaves the avengers' sums");
            // the history: the burning part of the raid, told with the killer
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.BuildingBurned) continue;
                Assert.AreEqual(EntityRef.Household(hid), e.Subject);
                Assert.AreEqual(b, e.Object.Id, "by the band's village");
                Assert.AreEqual(killer, e.Other.Id, "for the killer's deed");
                Assert.AreEqual(HistoryKind.Raid, w.History.TryGet(e.Cause1, out var r) ? r.Kind : HistoryKind.None, "part of the raid");
                StringAssert.Contains("burned the home of", HistoryText.Describe(w, in e));
            }
            // a world saved after the burning lives on as if never saved
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Living), out _);
            long end = w.Now + SimTime.Days(3);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash());
        }

        [Test]
        public void NoBurningUnseenForWoundsAloneOrWithTheRuleOff()
        {
            foreach (int k in new[] { 0, 1, 2 })
            {
                Conflict.BurnOff = k == 2;
                try
                {
                    var w = Pair(out int a, out int b);
                    w.AdvanceTo(w.Now + SimTime.Hours(30));
                    int killer = Housed(w, a);
                    Killing(w, killer, a, b, k == 1 ? DeedKind.Wound : DeedKind.Kill);
                    var raiders = Band(w, b, 6);
                    NoneFit(w, a);
                    long mark = w.History.LastId;
                    w.Conflict.FallNow(b, a, raiders, seen: k != 0);
                    Assert.AreEqual(0, Burnings(w, mark), k == 0 ? "an unseen band carries off grain and burns nothing" : k == 1 ? "wounds alone burn no home" : "the rule off");
                }
                finally { Conflict.BurnOff = false; }
            }
        }
    }
}
