using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>How an absence is lived (12 §3, D2-023): the plan is logged commands, the arrival is in full activity.</summary>
    [TestFixture]
    public class CatchUpPlanTests
    {
        [Test]
        public void AShortAbsenceIsLivedInFull()
        {
            var w = TestWorlds.Make(21);
            Assert.IsFalse(OfflineProgress.PlanCatchUp(w, w.Now + 3 * SimTime.Year), "a few years of a small world stay in ACTIVITY");
            Assert.AreEqual(Resolution.Activity, w.Resolution);
        }

        [Test]
        public void ALongAbsenceIsLivedCoarseAndArrivesInFullActivity()
        {
            var w = TestWorlds.Make(22);
            int people = w.LivingPeople();
            const double threshold = 1500; // the rule at a test's scale (the game uses CoarsePersonYears)
            long target = w.Now + SimTime.Years(threshold / people * 1.2);
            Assert.IsTrue(OfflineProgress.PlanCatchUp(w, target, threshold), "many lives: COARSE");
            long start = SimTime.StartOfDay(w.Now) + SimTime.Day;
            var b = Budget.Unlimited;
            w.AdvanceTo(start + SimTime.Hour, ref b);
            Assert.AreEqual(Resolution.Coarse, w.Resolution, "COARSE from the next midnight");
            Assert.IsNull(TestWorlds.Audit(w));
            b = Budget.Unlimited;
            w.AdvanceTo(target - OfflineProgress.ActivityTail + SimTime.Hour, ref b);
            Assert.AreEqual(Resolution.Activity, w.Resolution, "full activity again before the player is back");
            b = Budget.Unlimited;
            w.AdvanceTo(target, ref b);
            Assert.IsNull(TestWorlds.Audit(w), "everyone has a live activity on arrival");
        }
    }
}
