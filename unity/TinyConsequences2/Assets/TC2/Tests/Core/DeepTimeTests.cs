using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// DEEP (04 §4.3b, F8): in COARSE, calm villages live several days at a step through the same laws, never across a day of their
    /// calendar; it is deterministic, saves exactly, keeps every list, and lives the world as COARSE does (distributions, not
    /// identities).
    /// </summary>
    [TestFixture]
    public class DeepTimeTests
    {
        static World Lived(ulong seed, int stepDays)
        {
            var w = TestWorlds.Make(seed);
            long midnight = SimTime.StartOfDay(w.Now) + SimTime.Day;
            w.RequestResolution(Resolution.Coarse, midnight);
            if (stepDays > 1) w.RequestStepDays(stepDays, midnight);
            return w;
        }

        [Test]
        public void AStepNeverCrossesTheVillageCalendar()
        {
            Assert.AreEqual(12, SettlementSystem.DaysToCalendar(0, 12), "sowing, to the crop growing");
            Assert.AreEqual(6, SettlementSystem.DaysToCalendar(24, 12), "to the crop ripe (30)");
            Assert.AreEqual(9, SettlementSystem.DaysToCalendar(30, 12), "to the harvest (39)");
            Assert.AreEqual(2, SettlementSystem.DaysToCalendar(39, 12), "to the day households weigh leaving (41)");
            Assert.AreEqual(7, SettlementSystem.DaysToCalendar(41, 12), "to the next year's sowing");
            Assert.AreEqual(1, SettlementSystem.DaysToCalendar(5, 1), "a day at a time");
        }

        [Test]
        public void ADaysChanceOverAStep()
        {
            Assert.AreEqual(0.1, SettlementSystem.Over(0.1, 1), "a day's step is the day's chance, exactly");
            Assert.AreEqual(1.0 - 0.9 * 0.9 * 0.9, SettlementSystem.Over(0.1, 3), 1e-15);
            Assert.AreEqual(0.9 * 0.9, SettlementSystem.Keep(0.1, 2), 1e-15);
        }

        [Test]
        public void AStepKeepsItsDays()
        {
            // a gate that opens within a step opens on its day: the days of the step before a moment has come
            long now = 100 * SimTime.Day;
            Assert.AreEqual(0, SettlementSystem.DaysUntil(now - 5, now), "come already");
            Assert.AreEqual(0, SettlementSystem.DaysUntil(now, now), "comes now");
            Assert.AreEqual(1, SettlementSystem.DaysUntil(now + 1, now), "comes within the first day: from the second");
            Assert.AreEqual(1, SettlementSystem.DaysUntil(now + SimTime.Day, now));
            Assert.AreEqual(2, SettlementSystem.DaysUntil(now + SimTime.Day + 1, now));
            // a day's cap stays a day's: a share of a ground a day, over a step as day after day
            Assert.AreEqual(0.3, WildFood.Most(0.3, 1), "a day's step is the day's cap, exactly");
            Assert.AreEqual(1.0 - 0.7 * 0.7 * 0.7, WildFood.Most(0.3, 3), 1e-15);
            // the store a DEEP step leads is seen as days lived would hold it: the lead spent evenly through the step
            var s = new Settlement { Granary = 100.0, Ahead = 24.0, AheadFrom = now, AheadTo = now + 12 * SimTime.Day };
            Assert.AreEqual(124.0, s.StoreSeen(now), 1e-12, "at the step's start: the step's draws not yet made");
            Assert.AreEqual(112.0, s.StoreSeen(now + 6 * SimTime.Day), 1e-12, "half way");
            Assert.AreEqual(100.0, s.StoreSeen(now + 12 * SimTime.Day), "at its end: the granary");
            var plain = new Settlement { Granary = 37.25 };
            Assert.AreEqual(37.25, plain.StoreSeen(now), "outside DEEP the store seen is the granary, exactly");
        }

        [Test]
        public void EachWorkerOfAStepChoosesOnTheGroundTheStepLeft()
        {
            // days lived choose their work each day on that day's woods, so a village's own take turns its people to other work; a
            // DEEP step chooses once a person, each on the ground as the step's earlier workers left it (Retake, 04 §4.3b rule 2)
            var w = TestWorlds.Make(31);
            int sid = w.Settlements.First();
            w.Wild.Survey(sid);
            w.Wild.MidStep(sid, 1.0);
            ref var s = ref w.Settlements.Get(sid);
            Assert.GreaterOrEqual(s.Gath.A, 0, "the village has woods to gather in");
            float before = s.Gath.Expect;
            Assert.Greater(before, 0f);
            for (int k = 0; k < 6; k++) w.Wild.Gather(s.Gath.A, 60.0, 1.0, (ulong)(k + 1), 12);   // (six workers' twelve-day takes)
            Assert.AreEqual(before, w.Settlements.Get(sid).Gath.Expect, "the take alone changes nothing a worker reads");
            w.Wild.Retake(sid, WildFood.Gathering);
            float after = w.Settlements.Get(sid).Gath.Expect;
            System.Console.WriteLine("gathering FU an hour: " + before.ToString("0.000") + " -> " + after.ToString("0.000"));
            Assert.Less(after, before, "the next worker sees the woods the step's earlier workers left");
        }

        [Test]
        public void CalmVillagesLiveSeveralDaysAtAStep()
        {
            var w = Lived(31, 12);
            w.AdvanceTo(w.Now + SimTime.Years(3));
            Assert.IsNull(TestWorlds.Audit(w));
            Assert.Greater(w.Metrics.VillageSteps, 0);
            double perStep = w.Metrics.VillageDays / (double)w.Metrics.VillageSteps;
            System.Console.WriteLine("village-days a step " + perStep.ToString("0.00") + " (" + w.Metrics.VillageSteps + " steps)");
            Assert.Greater(perStep, 3.0, "calm villages step several days");
        }

        [Test]
        public void DeepIsDeterministicAndSavesExactly()
        {
            var a = Lived(32, 12);
            var b = Lived(32, 12);
            long t1 = a.Now + SimTime.Years(2) + 777;
            a.AdvanceTo(t1); b.AdvanceTo(t1);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "two DEEP runs");
            byte[] bytes = SaveFile.Save(a, 1_000_000, OfflineMode.Ancient);
            var c = SaveFile.Load(bytes, out _);
            Assert.AreEqual(a.StateHash(), c.StateHash(), "loaded");
            long t2 = t1 + SimTime.Years(1) + 4321;
            a.AdvanceTo(t2); c.AdvanceTo(t2);
            Assert.AreEqual(a.StateHash(), c.StateHash(), "continued after a load");
            Assert.IsNull(TestWorlds.Audit(c));
        }

        [Test]
        public void AnAncientAbsenceIsLivedDeepAndArrivesInFullActivity()
        {
            var w = TestWorlds.Make(35);
            int people = w.LivingPeople();
            const double threshold = 1500;   // the rule at a test's scale (the game uses CoarsePersonYears)
            long target = w.Now + SimTime.Years(threshold / people * 1.2 + 1.0);
            Assert.IsTrue(OfflineProgress.PlanCatchUp(w, target, OfflineMode.Ancient, threshold), "many lives: COARSE");
            long start = SimTime.StartOfDay(w.Now) + SimTime.Day;
            w.AdvanceTo(start + SimTime.Hour);
            Assert.AreEqual(Resolution.Coarse, w.Resolution);
            Assert.AreEqual(OfflineProgress.DeepStepDays, w.StepDays, "Ancient lives it DEEP");
            w.AdvanceTo(target - OfflineProgress.ActivityTail + SimTime.Hour);
            Assert.AreEqual(Resolution.Activity, w.Resolution, "full activity again before the player is back");
            Assert.LessOrEqual(w.StepDays, 1, "DEEP over before that");
            // every village lives day by day again: its next day is at most a day away
            for (int i = 0; i < w.Queue.RawCount; i++)
            {
                var e = w.Queue.RawAt(i);
                if (e.Kind == TC2.Core.Scheduling.EventKind.SettlementDaily && w.Settlements.Exists(e.Target) && w.Settlements.Get(e.Target).Inhabited)
                    Assert.LessOrEqual(e.Time - w.Now, SimTime.Day, "village " + e.Target + "'s next day");
            }
            w.AdvanceTo(target);
            Assert.IsNull(TestWorlds.Audit(w), "everyone has a live activity on arrival");
        }

        [Test]
        public void DeepLivesTheWorldAsCoarseDoes()
        {
            // the same small worlds lived 60 years at COARSE and at DEEP: the same people and laws, a longer step - how many live, are
            // born, die and of what, what the fields and the herds give, in the same ranges
            var pc = new int[Livestock.Kinds]; var pd = new int[Livestock.Kinds];
            foreach (ulong seed in new ulong[] { 33, 34 })
            {
                var c = Lived(seed, 1);
                var d = Lived(seed, 12);
                long end = c.Now + SimTime.Years(60);
                // the herds read at five moments over the last eight years, not one (C9.1: a moment's count on four villages swung with a
                // few herds' culls past the band when boats moved the trajectories - the mix is what is kept)
                var kc = new int[Livestock.Kinds]; var kd = new int[Livestock.Kinds];
                for (int m = 4; m >= 0; m--)
                {
                    long at = end - m * SimTime.Years(2);
                    c.AdvanceTo(at); d.AdvanceTo(at);
                    for (int b = c.Beasts.First(); b != 0; b = c.Beasts.Next(b)) if (c.Beasts.Get(b).Alive) kc[c.Beasts.Get(b).Kind]++;
                    for (int b = d.Beasts.First(); b != 0; b = d.Beasts.Next(b)) if (d.Beasts.Get(b).Alive) kd[d.Beasts.Get(b).Kind]++;
                }
                for (int k = 0; k < Livestock.Kinds; k++) { kc[k] = (kc[k] + 2) / 5; kd[k] = (kd[k] + 2) / 5; }
                Assert.IsNull(TestWorlds.Audit(d));
                var mc = c.Metrics; var md = d.Metrics;
                System.Console.WriteLine("seed " + seed + " COARSE vs DEEP: people " + c.LivingPeople() + " / " + d.LivingPeople() + ", places " + c.InhabitedSettlements() + " / " + d.InhabitedSettlements()
                    + ", births " + mc.Births + " / " + md.Births + ", deaths " + mc.Deaths + " / " + md.Deaths + " (hunger " + mc.HungerDeaths + " / " + md.HungerDeaths + ", cold " + mc.ColdDeaths + " / " + md.ColdDeaths
                    + ", sickness " + mc.DiseaseDeaths + " / " + md.DiseaseDeaths + "), marriages " + mc.Marriages + " / " + md.Marriages + ", beasts born " + mc.BeastBirths + " / " + md.BeastBirths
                    + ", wild FU " + mc.WildFu.ToString("0") + " / " + md.WildFu.ToString("0") + ", village-days a step " + (md.VillageDays / (double)System.Math.Max(1, md.VillageSteps)).ToString("0.0"));
                Assert.AreEqual(c.LivingPeople(), d.LivingPeople(), 0.35 * c.LivingPeople() + 10, "people");
                Assert.AreEqual(mc.Births, md.Births, 0.30 * mc.Births + 10, "births");
                Assert.AreEqual(mc.Deaths, md.Deaths, 0.30 * mc.Deaths + 10, "deaths");
                Assert.AreEqual(mc.Marriages, md.Marriages, 0.30 * mc.Marriages + 10, "marriages");
                for (int k = 0; k < Livestock.Kinds; k++) { pc[k] += kc[k]; pd[k] += kd[k]; System.Console.WriteLine("  " + Livestock.Laws[k].Name + " " + kc[k] + " / " + kd[k]); }
            }
            // the herds keep their mix (a step that read its first day for all its days bred cattle three times as often, and culled
            // pigs to a fourteenth): a beast count on a four-village world swings with a few herds' culls and with the villages its
            // trajectory founds, so the test catches a factor of 2.5 in the two worlds' herds together (D2-083: the six-seed 400-year
            // comparison is the calibration - goats 1.11, pigs 0.73, n.s.; D2-135: twelve small worlds, 60 years, DEEP against COARSE:
            // cattle x0.96, goats x0.92, pigs x1.23, single worlds x0.3-x3.7 either way, with households leaving by their own land or
            // by their village's crowding alike - one world at a time the guard read chance)
            for (int k = 0; k < Livestock.Kinds; k++)
            {
                double ratio = (pd[k] + 15.0) / (pc[k] + 15.0);
                System.Console.WriteLine("  both worlds' " + Livestock.Laws[k].Name + " " + pc[k] + " / " + pd[k] + " (x" + ratio.ToString("0.00") + ")");
                Assert.That(ratio, Is.InRange(1.0 / 2.5, 2.5), Livestock.Laws[k].Name);
            }
        }
    }
}
