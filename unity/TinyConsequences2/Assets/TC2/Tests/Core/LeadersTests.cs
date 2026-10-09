using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The village's first household and its headman (C10.2, D2-127): the household with the largest following - its close kin, those who owe it
    /// grain, those it fed in hard times - is the first; its eldest grown member is first among the people; an incumbent keeps the place while its
    /// following is near the largest; at a death the place passes within the household while it still holds its following; in hard times the first
    /// household opens its grain to the hungry and they follow it. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class LeadersTests
    {
        /// <summary>Premise: household `creditor` is owed grain by `n` other households of the village (the first n it is not).</summary>
        static void Owed(World w, int sid, int creditor, int n)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count && n > 0; i++) { if (hl[i] == creditor) continue; w.Holdings.Owe(hl[i], creditor, 10, w.Now); n--; }
        }

        static World Village(ulong seed, out int sid)
        {
            var w = TestWorlds.Make(seed);
            sid = w.Settlements.First();
            return w;
        }

        [Test]
        public void TheHouseholdMostFollowGivesTheVillageItsFirst()
        {
            var w = Village(941, out int sid);
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 6);
            int a = hl[hl.Count - 1];
            Owed(w, sid, a, 2);
            Leaders.Weigh(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            Assert.AreEqual(a, s.HeadHh, "the household two others owe");
            Assert.AreEqual(Leaders.Eldest(w, sid, a), s.Head, "its eldest grown member");
            Assert.AreEqual(2, Leaders.Following(w, sid, a, out int kin, out int owing, out int fed));
            Assert.AreEqual(2, owing);
            Assert.IsTrue(w.History.TryGet(s.HeadEvent, out var e) && e.Kind == HistoryKind.HeadmanRose && e.ArgB == 0, "recorded: the first");
            System.Console.WriteLine(HistoryText.Line(w, e));
            // a rival a little ahead does not unseat it; one well ahead does
            int b = hl[0];
            var w2 = w;
            Owed(w2, sid, b, 3);   // (b owed by three, a by two: 2 >= 0.5 x 3 - a stays)
            Leaders.Weigh(w2, sid);
            Assert.AreEqual(a, w2.Settlements.Get(sid).HeadHh, "an incumbent is not unseated by a hair");
            Owed(w2, sid, b, 5);   // (b owed by five: more than twice a's following)
            Leaders.Weigh(w2, sid);
            Assert.AreEqual(b, w2.Settlements.Get(sid).HeadHh, "outstood");
            Assert.IsTrue(w2.History.TryGet(w2.Settlements.Get(sid).HeadEvent, out var e2) && e2.ArgB == 2, "another household rose");
        }

        [Test]
        public void AtADeathThePlacePassesWithinTheHousehold()
        {
            var w = Village(942, out int sid);
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 4);
            // premise: a household of two grown members, owed by the others
            int a = 0;
            for (int i = 0; i < hl.Count && a == 0; i++)
            {
                var mem = w.Members(hl[i]); int grown = 0;
                for (int k = 0; k < mem.Count; k++) if (w.People.Get(mem[k]).AgeYears(w.Now) >= Leaders.FromAge) grown++;
                if (grown >= 2) a = hl[i];
            }
            Assume.That(a != 0, "a household with two grown members");
            Owed(w, sid, a, 3);
            Leaders.Weigh(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            int first = s.Head;
            Assert.AreEqual(a, s.HeadHh);
            ref var p = ref w.People.Get(first);
            w.Agents.Kill(ref p, 0, 1);
            Leaders.Day(w, sid);
            ref var s2 = ref w.Settlements.Get(sid);
            Assert.AreEqual(a, s2.HeadHh, "the household keeps the place");
            Assert.AreNotEqual(first, s2.Head);
            Assert.AreEqual(a, w.People.Get(s2.Head).Household, "the next of the household");
            Assert.IsTrue(w.History.TryGet(s2.HeadEvent, out var e) && e.ArgB == 1 && e.Other.Id == first, "within the household, after the dead");
            System.Console.WriteLine(HistoryText.Line(w, e));
        }

        [Test]
        public void InHardTimesTheFirstHouseholdFeedsTheHungryAndTheyFollowIt()
        {
            var w = Village(943, out int sid);
            var hl = w.HouseholdsOf(sid);
            Assume.That(hl.Count >= 3);
            int first = hl[hl.Count - 1], hungry = hl[0];
            Assume.That(!Holdings.KinOf(w, sid, hungry).Contains(first));
            Owed(w, sid, first, 2);
            Leaders.Weigh(w, sid);
            Assume.That(w.Settlements.Get(sid).HeadHh == first);
            for (int i = 0; i < hl.Count; i++) w.Households.Get(hl[i]).Grain = 0;
            double needF = w.SettlementSys.HouseholdNeed(first);
            ref var s = ref w.Settlements.Get(sid);
            s.Common = 0; s.Ration = 0.5;   // (hard times)
            int toHarvest = 20;
            w.Households.Get(first).Grain = needF * 0.5 * (toHarvest + Holdings.Reserve) + 40;   // (40 beyond its own needs at the village's ration)
            s.Granary = w.Households.Get(first).Grain;
            double owed0 = w.Holdings.Owes(hungry, out _);
            double got = w.Holdings.Provide(sid, hungry, 30, toHarvest);
            Assert.AreEqual(30, got, 1e-6, "fed by the first household");
            Assert.AreEqual(needF * 0.5 * (toHarvest + Holdings.Reserve) + 10, w.Households.Get(first).Grain, 1e-6);
            Assert.AreEqual(first, w.Households.Get(hungry).Patron, "and follows it");
            Assert.AreEqual(owed0, w.Holdings.Owes(hungry, out _), 1e-9, "a gift, not a loan");
            Assert.AreEqual(2, Leaders.Following(w, sid, first, out _, out _, out int fed), "a household that owes it and was fed by it follows it once");
            Assert.AreEqual(1, fed);
            // without the first household (`--nohead`) nobody's grain opens to it in hard times
            Leaders.Off = true;
            try
            {
                var w2 = Village(943, out _);
                var hl2 = w2.HouseholdsOf(sid);
                for (int i = 0; i < hl2.Count; i++) w2.Households.Get(hl2[i]).Grain = 0;
                ref var t = ref w2.Settlements.Get(sid); t.Common = 0; t.Ration = 0.5;
                w2.Households.Get(first).Grain = needF * 0.5 * (toHarvest + Holdings.Reserve) + 40;
                t.Granary = w2.Households.Get(first).Grain;
                Assert.AreEqual(0, w2.Holdings.Provide(sid, hungry, 30, toHarvest), 1e-9);
            }
            finally { Leaders.Off = false; }
        }

        [Test]
        public void TributeIsTheLordsFirstHouseholdsGrain()
        {
            foreach (bool off in new[] { false, true })
            {
                Leaders.TributeOff = off;
                try
                {
                    var w = Village(945, out int a);
                    int b = w.Settlements.Next(a);
                    Assume.That(b != 0, "two villages");
                    if (w.Conflict.FitAt(b, w.Now) < w.Conflict.FitAt(a, w.Now)) { int t = a; a = b; b = t; }   // (the stronger is the lord)
                    Leaders.Weigh(w, b);
                    int first = w.Settlements.Get(b).HeadHh;
                    Assume.That(first != 0);
                    // premise: a pays tribute to b, and b is the stronger (it keeps a as its tributary)
                    ref var sa = ref w.Settlements.Get(a); sa.Lord = b; sa.Granary = 5000;
                    Assume.That(w.Conflict.FitAt(b, w.Now) >= w.Conflict.FitAt(a, w.Now), "the lord the stronger");
                    double g0 = w.Households.Get(first).Grain, c0 = w.Settlements.Get(b).Common, s0 = w.Settlements.Get(b).Granary;
                    w.Conflict.Harvested(a, 1000);
                    double paid = w.Settlements.Get(b).Granary - s0;
                    Assert.AreEqual(Conflict.TributeShare * 1000, paid, 1e-6, "the tribute paid");
                    if (!off) Assert.AreEqual(g0 + paid, w.Households.Get(first).Grain, 1e-6, "the lord's own: its first household's grain");
                    else { w.Holdings.Reconcile(b); Assert.AreEqual(c0 + paid, w.Settlements.Get(b).Common, 1e-6, "--notribhead: the lord village's common grain"); }
                }
                finally { Leaders.TributeOff = false; }
            }
        }

        // ------------------------------------------------------------------ C10.3: a contested succession splits a village

        /// <summary>A big premise village (70 founders) with a good place known 2-4 km out, or null.</summary>
        static World Big(ulong seed, out int sid)
        {
            var w = TC2.Core.Worldgen.WorldGenerator.Create(new WorldConfig { Seed = seed, SurfaceN = 48, Settlements = 2, FoundersPerSettlement = 70 });
            sid = w.Settlements.First();
            var P = w.Planet; var s = w.Settlements.Get(sid); var scratch = new System.Collections.Generic.List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                double d = Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius);
                if (d < 2000 || d > 4000) continue;
                if (Sites.Quality(w, c, scratch) >= 0.4 && w.Routes.GetToCell(w, sid, c) != null) { w.Migration.Look(sid, c, 0); return w.Settlements.Get(sid).Lore.Count > 0 ? w : null; }
            }
            return null;
        }

        [Test]
        public void AContestedSuccessionSplitsTheVillage()
        {
            foreach (int rivalN in new[] { 6, 2 })
            {
                foreach (bool off in rivalN == 6 ? new[] { false, true } : new[] { false })
                {
                    Leaders.SplitOff = off;
                    try
                    {
                        World w = null; int sid = 0;
                        for (ulong seed = 951; seed < 970 && w == null; seed++) { w = Big(seed, out sid); if (w != null && w.HouseholdsOf(sid).Count < 16) w = null; }
                        Assume.That(w != null, "a big village knowing a good place");
                        var hl = w.HouseholdsOf(sid);
                        // premise: the first household is owed by eight, a rival by others not bound to the first
                        int first = 0;
                        for (int i = 0; i < hl.Count && first == 0; i++) if (Leaders.Eldest(w, sid, hl[i]) != 0 && w.Members(hl[i]).Count >= 2) first = hl[i];
                        int rival = 0;
                        for (int i = hl.Count - 1; i >= 0 && rival == 0; i--) if (hl[i] != first && Leaders.Eldest(w, sid, hl[i]) != 0) rival = hl[i];
                        Assume.That(first != 0 && rival != 0);
                        var others = new System.Collections.Generic.List<int>();
                        for (int i = 0; i < hl.Count; i++) if (hl[i] != first && hl[i] != rival) others.Add(hl[i]);
                        for (int i = 0; i < 8; i++) w.Holdings.Owe(others[i], first, 10, w.Now);
                        for (int i = 8; i < 8 + rivalN; i++) w.Holdings.Owe(others[i], rival, 10, w.Now);
                        Leaders.Weigh(w, sid);
                        Assume.That(w.Settlements.Get(sid).HeadHh == first, "the first household");
                        int parties0 = w.Parties.Count;
                        { int ppl = w.Members(rival).Count; for (int i = 8; i < 8 + rivalN; i++) ppl += w.Members(others[i]).Count; System.Console.WriteLine("rival following " + Leaders.Following(w, sid, rival, out _, out _, out _) + ", first's " + Leaders.Following(w, sid, first, out _, out _, out _) + ", faction people " + ppl + ", households " + hl.Count + ", lore " + w.Settlements.Get(sid).Lore.Count); }
                        // the first among them dies
                        ref var dead = ref w.People.Get(w.Settlements.Get(sid).Head);
                        w.Agents.Kill(ref dead, 0, 1);
                        Leaders.Day(w, sid);
                        bool split = w.Households.Get(rival).Party != 0;
                        if (rivalN == 6 && !off)
                        {
                            Assert.IsTrue(split, "the rival will not follow: it leads its people away");
                            int went = 0; for (int i = 8; i < 8 + rivalN; i++) if (w.Households.Get(others[i]).Party == w.Households.Get(rival).Party) went++;
                            Assert.GreaterOrEqual(went, rivalN - 1, "its following with it (a birth near keeps one back)");
                            for (int i = 0; i < 8; i++) Assert.AreEqual(0, w.Households.Get(others[i]).Party, "the first's following stays");
                            Assert.AreEqual(first, w.Settlements.Get(sid).HeadHh, "the first household keeps the place");
                            Assert.AreEqual(parties0 + 1, w.Parties.Count);
                            var evs = w.History.EventsOf(EntityRef.Household(rival));
                            bool told = false;
                            for (int i = 0; evs != null && i < evs.Count; i++) if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.VillageSplit) { told = true; System.Console.WriteLine(HistoryText.Line(w, e)); }
                            Assert.IsTrue(told, "the split recorded, its cause the death");
                            // they set out and found a village at the place
                            int villages0 = w.InhabitedSettlements();
                            w.AdvanceTo(w.Now + SimTime.Days(6));
                            Assert.Greater(w.InhabitedSettlements(), villages0, "a village of their own");
                        }
                        else Assert.IsFalse(split, rivalN == 6 ? "--nosplit" : "a rival followed by too few stays");
                    }
                    finally { Leaders.SplitOff = false; }
                }
            }
        }

        [Test]
        public void AFactionThatKnowsNoPlaceLooksForLandAndGoesWhenItIsFound()
        {
            World w = null; int sid = 0;
            for (ulong seed = 951; seed < 970 && w == null; seed++) { w = Big(seed, out sid); if (w != null && w.HouseholdsOf(sid).Count < 16) w = null; }
            Assume.That(w != null, "a big village knowing a good place");
            ref var s0 = ref w.Settlements.Get(sid);
            int site = s0.Lore.Best(out _);
            s0.Lore.Forget(site);   // (premise: the village knows no place)
            Assume.That(!w.Migration.KnowsSite(sid));
            var hl = w.HouseholdsOf(sid);
            int first = 0, rival = 0;
            for (int i = 0; i < hl.Count && first == 0; i++) if (Leaders.Eldest(w, sid, hl[i]) != 0 && w.Members(hl[i]).Count >= 2) first = hl[i];
            for (int i = hl.Count - 1; i >= 0 && rival == 0; i--) if (hl[i] != first && Leaders.Eldest(w, sid, hl[i]) != 0) rival = hl[i];
            var others = new System.Collections.Generic.List<int>();
            for (int i = 0; i < hl.Count; i++) if (hl[i] != first && hl[i] != rival) others.Add(hl[i]);
            for (int i = 0; i < 8; i++) w.Holdings.Owe(others[i], first, 10, w.Now);
            for (int i = 8; i < 14; i++) w.Holdings.Owe(others[i], rival, 10, w.Now);
            Leaders.Weigh(w, sid);
            Assume.That(w.Settlements.Get(sid).HeadHh == first);
            ref var dead = ref w.People.Get(w.Settlements.Get(sid).Head);
            w.Agents.Kill(ref dead, 0, 1);
            Leaders.Day(w, sid);
            Assert.AreEqual(0, w.Households.Get(rival).Party, "no place known: they stay for now");
            Assert.AreEqual(rival, w.Settlements.Get(sid).SplitHh, "and look for land");
            { ref var sf = ref w.Settlements.Get(sid); sf.Founded = w.Now - SimTime.Years(4); sf.ScoutAt = w.Now - SimTime.Days(7); }   // (premise: a village past its first years, nobody out looking this week)
            Assert.IsTrue(w.Migration.WantsScout(in w.Settlements.Get(sid)), "the village sends someone to look");
            // a scout finds the place: they go
            w.Migration.Look(sid, site, 0);
            Leaders.Day(w, sid);
            Assert.AreNotEqual(0, w.Households.Get(rival).Party, "land found: they go");
            Assert.AreEqual(0, w.Settlements.Get(sid).SplitHh);
            // and a faction that finds nothing in three years stays
            ref var s1 = ref w.Settlements.Get(sid); s1.SplitHh = others[20 % others.Count]; s1.SplitSince = w.Now - (Leaders.SplitYears + 1) * SimTime.Year;
            Leaders.Day(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).SplitHh, "tired of looking, they stay");
        }

        [Test]
        public void ALivedAndACoarseVillageFindTheirFirstAndSaveIt()
        {
            foreach (bool coarse in new[] { false, true })
            {
                var w = Village(944, out int sid);
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Days(coarse ? 60 : 4));
                Assert.AreNotEqual(0, w.Settlements.Get(sid).Head, "a village finds its first");
                var w2 = TC2.Core.Persistence.SaveFile.Load(TC2.Core.Persistence.SaveFile.Save(w, 0, TC2.Core.Persistence.OfflineMode.Paused, 0, 0, ""), out _);
                w.AdvanceTo(w.Now + SimTime.Days(10)); w2.AdvanceTo(w2.Now + SimTime.Days(10));
                Assert.AreEqual(w.StateHash(), w2.StateHash(), "saved and loaded, the same");
                System.Console.WriteLine((coarse ? "COARSE: " : "lived: ") + TC2.Core.History.Gazetteer.Overview(w, sid));
            }
        }
    }
}
