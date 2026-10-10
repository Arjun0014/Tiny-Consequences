using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Household formation (C3.5, D2-129): a couple marries when what they bring of their households' grain keeps the two of them to the next
    /// harvest and a few days beyond - otherwise the match waits; a bride marries where the grain is. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class HouseholdFormationTests
    {
        /// <summary>Premise: every woman of an age to marry looks for a match every day.</summary>
        static World Eager(ulong seed)
        {
            var cfg = TestWorlds.Small(seed);
            cfg.MarriageRatePerDay = 1.0;
            return WorldGenerator.Create(cfg);
        }

        /// <summary>Premise: a village's households hold this many days of their own needs as their own grain (the rest of its store is common).</summary>
        static void Grain(World w, int sid, double days)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { ref var hh = ref w.Households.Get(hl[i]); hh.Grain = w.SettlementSys.HouseholdNeed(hl[i]) * days; }
        }

        /// <summary>Premise (a state the world reaches): a wife of a village is widowed - her husband dies; 0 if none is of an age to marry again.</summary>
        static int Widow(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                if (!p.Alive || !p.IsFemale || p.Partner == 0 || p.AgeYears(w.Now) < 18 || p.AgeYears(w.Now) > 34) continue;
                ref var h = ref w.People.Get(p.Partner);
                w.Agents.Kill(ref h, 0, 1);
                return p.Id;
            }
            return 0;
        }

        /// <summary>Premise: a husband of a village is widowed - his wife dies; 0 if none is of an age to marry again.</summary>
        static int Widower(World w, int sid, int not)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                if (!p.Alive || p.IsFemale || p.Partner == 0 || p.Partner == not || p.AgeYears(w.Now) < 18 || p.AgeYears(w.Now) > 46) continue;
                ref var h = ref w.People.Get(p.Partner);
                w.Agents.Kill(ref h, 0, 1);
                return p.Id;
            }
            return 0;
        }

        [Test]
        public void ACoupleMarriesOnlyWhenTheyCanKeepAHousehold()
        {
            foreach (bool off in new[] { false, true })
            {
                SettlementSystem.MeansOff = off;
                try
                {
                    var w = Eager(1611);
                    int sid = w.Settlements.First();
                    int woman = Widow(w, sid), man = Widower(w, sid, 0);
                    Assume.That(woman != 0 && man != 0, "a widow and a widower");
                    for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) Grain(w, s, 0);
                    w.AdvanceTo(w.Now + SimTime.Days(2));
                    if (!off) Assert.AreEqual(0, w.People.Get(woman).Partner, "no grain of their own (the store's grain is the village's): the match waits");
                    else { Assert.AreNotEqual(0, w.People.Get(woman).Partner, "--nomeans: she marries all the same"); continue; }
                    // their households' grain to keep them a year: they marry
                    for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) Grain(w, s, 2 * SimTime.DaysPerYear);
                    w.AdvanceTo(w.Now + SimTime.Days(2));
                    Assert.AreNotEqual(0, w.People.Get(woman).Partner, "with the means of a household she marries");
                    bool told = false;
                    var evs = w.History.EventsOf(EntityRef.Person(woman));
                    for (int i = 0; evs != null && i < evs.Count; i++)
                        if (w.History.TryGet(evs[i], out var e) && e.Kind == TC2.Core.History.HistoryKind.Marriage && e.ArgA > 0)
                        {
                            string line = TC2.Core.History.HistoryText.Line(w, e);
                            StringAssert.Contains("bread of their own", line);
                            System.Console.WriteLine(line);
                            told = true;
                        }
                    Assert.IsTrue(told, "the marriage's record tells what they brought");
                }
                finally { SettlementSystem.MeansOff = false; }
            }
        }

        [Test]
        public void WhatACoupleBringsIsTheirMouthsShareOfTheirHouseholdsGrain()
        {
            var w = TestWorlds.Make(1612);
            int sid = w.Settlements.First();
            int woman = Widow(w, sid), man = Widower(w, sid, woman);
            Assume.That(woman != 0 && man != 0 && w.People.Get(woman).Household != w.People.Get(man).Household, "a widow and a widower of two households");
            int hw = w.People.Get(woman).Household, hm = w.People.Get(man).Household;
            { ref var a = ref w.Households.Get(hw); a.Grain = 300; a.Larder = 10; }
            { ref var b = ref w.Households.Get(hm); b.Grain = 90; b.Larder = 0; }
            double nw = w.Agents.DailyNeed(in w.People.Get(woman), w.Now), nm = w.Agents.DailyNeed(in w.People.Get(man), w.Now);
            double bring = 310 * DMath.Min(1.0, nw / w.SettlementSys.HouseholdNeed(hw)) + 90 * DMath.Min(1.0, nm / w.SettlementSys.HouseholdNeed(hm));
            Assert.AreEqual(bring / (nw + nm), w.SettlementSys.CoupleDays(sid, woman, man), 1e-9, "each one's mouth's share of their household's grain and larder");
            Assert.AreEqual(bring / (nw + nm) / SimTime.DaysPerYear, w.SettlementSys.CoupleMeans(sid, woman, man), 1e-12);
            // a household of another village keeps its grain in its own store: a couple setting up elsewhere brings none of it
            int other = w.Settlements.Next(sid);
            Assume.That(other != 0);
            Assert.AreEqual(0.0, w.SettlementSys.CoupleDays(other, woman, man), 1e-9, "neither household lives there: nothing to bring");
            // the harvest's distance on the village's own calendar
            int th = w.SettlementSys.ToHarvest(sid);
            Assert.That(th >= 1 && th <= SimTime.DaysPerYear);
        }

        [Test]
        public void ABrideMarriesWhereTheGrainIs()
        {
            for (ulong seed = 1613; seed < 1640; seed++)
            {
                var w = Eager(seed);
                // premise: a widow in one village whose households have nothing of their own; a widower in each village within a day's walk,
                // where every household has four years of its needs (his mouth's share must keep them both)
                int poor = w.Settlements.First();
                int bride = Widow(w, poor), grooms = 0;
                if (bride == 0) continue;
                for (int s = w.Settlements.Next(poor); s != 0; s = w.Settlements.Next(s))
                {
                    if (Vec3d.ArcDistance(w.Settlements.Get(poor).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) > w.Config.MarriageReach) continue;
                    if (Widower(w, s, 0) != 0) grooms++;
                }
                if (grooms == 0) continue;
                for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) Grain(w, s, s == poor ? 0 : 4 * SimTime.DaysPerYear);
                w.AdvanceTo(w.Now + SimTime.Days(8));
                var p = w.People.Get(bride);
                if (p.Partner == 0) continue;   // (no way open to a neighbour these days)
                Assert.AreNotEqual(poor, p.Home, "she married where a household could be kept - not at home, where nobody has grain of their own");
                Assert.AreNotEqual(poor, w.People.Get(p.Partner).Home);
                return;
            }
            Assert.Inconclusive("no widow married into a neighbouring village in these worlds");
        }
        // ---------------------------------------------------------------- a living: the household's niche (C3.5b)
        [Test]
        public void ALivingIsTheLandAndHandsACoupleWouldHaveAtTheLastHarvest()
        {
            var w = TestWorlds.Make(1641);
            int sid = w.Settlements.First();
            int woman = Widow(w, sid), man = Widower(w, sid, woman);
            Assume.That(woman != 0 && man != 0 && w.People.Get(woman).Household != w.People.Get(man).Household, "a widow and a widower of two households");
            int hw = w.People.Get(woman).Household, hm = w.People.Get(man).Household;
            { ref var s = ref w.Settlements.Get(sid); s.LastHarvest = 1000; s.ArableHa = 20; }
            { ref var a = ref w.Households.Get(hw); a.Land = 0; }
            { ref var b = ref w.Households.Get(hm); b.Land = 0; }
            double none = w.SettlementSys.CoupleLiving(sid, woman, man);
            // the labour's half by their two hands over the village's, against the two of them's year
            double all = 0; var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Party == 0) all += w.Holdings.Hands(hl[i]);
            double hands = w.Holdings.HandOf(woman) + w.Holdings.HandOf(man);
            double need = (w.Agents.DailyNeed(in w.People.Get(woman), w.Now) + w.Agents.DailyNeed(in w.People.Get(man), w.Now)) * SimTime.DaysPerYear;
            Assert.AreEqual(1000 * (1.0 - Holdings.LandShare) * hands / all / need, none, 1e-9, "landless: their hands' share of the harvest");
            // his family holds land: his portion comes with him
            { ref var b = ref w.Households.Get(hm); b.Land = 10; }
            double portion = w.Holdings.PortionFor(hm);
            Assert.Greater(portion, 0.0);
            double landed = w.SettlementSys.CoupleLiving(sid, woman, man);
            Assert.AreEqual(none + 1000 * Holdings.LandShare * portion / 20.0 / need, landed, 1e-9, "and the land's half by the hectares they would hold");
            // a village yet to bring in a harvest gives nothing to judge by
            { ref var s = ref w.Settlements.Get(sid); s.LastHarvest = 0; }
            Assert.AreEqual(1.0, w.SettlementSys.CoupleLiving(sid, woman, man));
        }

        [Test]
        public void WithTheNicheACoupleWithoutALivingWaitsUntilItsGrainCarriesTheShortfall()
        {
            double custom = SettlementSystem.NicheYears;
            bool was = SettlementSystem.NicheOn;
            foreach (bool niche in new[] { true, false })
            {
                SettlementSystem.NicheOn = niche;
                try
                {
                    var w = Eager(1611);
                    int sid = w.Settlements.First();
                    int woman = Widow(w, sid), man = Widower(w, sid, 0);
                    Assume.That(woman != 0 && man != 0, "a widow and a widower");
                    // premise: nobody holds land, a poor last harvest (their hands keep them a fraction of a year), a year and a fifth of grain each
                    for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s))
                    {
                        var hl = w.HouseholdsOf(s);
                        for (int i = 0; i < hl.Count; i++) { ref var hh = ref w.Households.Get(hl[i]); hh.Land = 0; }
                        ref var st = ref w.Settlements.Get(s); st.LastHarvest = 0.2 * st.Population * SimTime.DaysPerYear;
                        Grain(w, s, 1.2 * SimTime.DaysPerYear);
                    }
                    double living = w.SettlementSys.CoupleLiving(w.People.Get(man).Home, woman, man);
                    Assume.That(living < 0.5, "premise: no living");
                    w.AdvanceTo(w.Now + SimTime.Days(2));
                    if (!niche) { Assert.AreNotEqual(0, w.People.Get(woman).Partner, "C3.5 alone: a year's grain and they marry"); continue; }
                    Assert.AreEqual(0, w.People.Get(woman).Partner, "no living, and their grain would not carry the shortfall: the match waits");
                    // where the custom asks grain to carry the shortfall for one year (premise: the granaries hold about two years, so more
                    // grain than that cannot be given - the surplus rots), the year and a fifth they bring carries it: they marry
                    SettlementSystem.NicheYears = 1.0;
                    w.AdvanceTo(w.Now + SimTime.Days(4));
                    Assert.AreNotEqual(0, w.People.Get(woman).Partner, "with grain to carry them until they find their feet, they marry");
                }
                finally { SettlementSystem.NicheOn = was; SettlementSystem.NicheYears = custom; }
            }
        }
    }
}
