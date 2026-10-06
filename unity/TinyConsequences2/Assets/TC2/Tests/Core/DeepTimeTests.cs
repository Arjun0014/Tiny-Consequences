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
            foreach (ulong seed in new ulong[] { 33, 34 })
            {
                var c = Lived(seed, 1);
                var d = Lived(seed, 12);
                long end = c.Now + SimTime.Years(60);
                c.AdvanceTo(end); d.AdvanceTo(end);
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
            }
        }
    }
}
