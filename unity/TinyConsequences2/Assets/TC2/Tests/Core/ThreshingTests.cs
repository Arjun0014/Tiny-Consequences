using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The threshing floor (C3.7b, `26` §2i): the reaped grain comes into the village's store in the sheaf and only grain threshed out of it is
    /// eaten; its people thresh it at the floor a little ahead of their need through the year - lived days and COARSE days alike, a hungry hand
    /// before it forages; with no able hand the sheaves stand while the threshed grain runs out. Mechanisms.
    /// </summary>
    [TestFixture]
    public class ThreshingTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void Until(World w, int sid, CropStage stage)
        {
            for (int d = 0; d < 72; d++)
            {
                if (w.Settlements.Get(sid).Crop == stage) return;
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            Assert.Fail("the crop never reached " + stage);
        }

        static void ToLocalHour(World w, int sid, double h)
        {
            long sod = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long dt = SimTime.Hours(h) - sod;
            if (dt <= 0) dt += SimTime.Day;
            w.AdvanceTo(w.Now + dt);
        }

        static double Larders(World w, int sid)
        {
            double l = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) l += w.Households.Get(hl[i]).Larder;
            return l;
        }

        [Test]
        public void TheHarvestStandsInTheSheafAndIsThreshedThroughTheYear()
        {
            var w = TestWorlds.Make(4311);
            Coarse(w);
            int sid = w.Settlements.First();
            if (w.Settlements.Get(sid).Crop == CropStage.Harvested) Until(w, sid, CropStage.Sown);
            Until(w, sid, CropStage.Harvested);
            var s = w.Settlements.Get(sid);
            Assume.That(s.LastHarvest > 100.0, "a harvest worth threshing");
            Assert.Greater(s.Sheaves, 0.5 * s.LastHarvest, "most of the harvest stands in the sheaf on its day (" + s.Sheaves.ToString("0") + " of " + s.LastHarvest.ToString("0") + ")");
            Assert.LessOrEqual(s.Sheaves, s.Granary + 1e-9, "the sheaves are a part of the store");
            // premise: last year's grain eaten down to two days of the village's need (a young world's store holds seventy days of it, threshed -
            // eaten first, the sheaves would wait weeks)
            {
                ref var sp = ref w.Settlements.Get(sid);
                sp.Granary = sp.Sheaves + 2.0 * Threshing.DayNeed(w, in sp);
                s = sp;
            }
            double sh0 = s.Sheaves, fu0 = w.Metrics.ThreshFu, h0 = w.Metrics.ThreshHours;
            double low = double.MaxValue;
            for (int d = 0; d < 16; d++)
            {
                w.AdvanceTo(w.Now + SimTime.Day);
                var sd = w.Settlements.Get(sid);
                Assert.LessOrEqual(sd.Sheaves, sd.Granary + 1e-6, "never more sheaves than the store");
                if (sd.Sheaves > 1.0) low = Math.Min(low, Threshing.Threshed(in sd) / Math.Max(1.0, Threshing.DayNeed(w, in sd)));
            }
            var s2 = w.Settlements.Get(sid);
            double fu = w.Metrics.ThreshFu - fu0, hours = w.Metrics.ThreshHours - h0;
            Console.WriteLine("harvest " + s.LastHarvest.ToString("0") + " FU: in the sheaf " + sh0.ToString("0") + " -> " + s2.Sheaves.ToString("0") + " in 16 days; threshed " + fu.ToString("0") +
                              " FU in " + hours.ToString("0.0") + " h; the least threshed grain at a day's end " + low.ToString("0.0") + " days of its need");
            Assert.Less(s2.Sheaves, sh0, "threshed as the days go");
            Assert.Greater(fu, 0.0);
            Assert.That(hours / fu, Is.InRange(1.0 / Threshing.PerHour - 1e-9, 1.0 / (0.3 * Threshing.PerHour)), "at the flail's pace: 0.04 h a FU for a strong hand");
            double need = 16 * Threshing.DayNeed(w, in s2);
            Assert.That(fu, Is.InRange(0.3 * need, need + 2.0 * Threshing.AheadSlack * Threshing.DayNeed(w, in s2)), "about what its people ate, and its days ahead");
            Assert.GreaterOrEqual(low, 1.0, "its people keep threshed grain ahead of their need");
        }

        [Test]
        public void OnlyThreshedGrainIsEatenAndWithNoAbleHandTheSheavesStand()
        {
            var w = TestWorlds.Make(4312);
            Coarse(w);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            // premise: the whole store in the sheaf, the larders and the hearths empty, and nobody able to stand at the floor (abed)
            ref var s = ref w.Settlements.Get(sid);
            Assume.That(s.Granary > 50.0);
            s.Sheaves = s.Granary;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Larder = 0; h.Ready = 0; }
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); p.Health = 0.25f; }
            double g0 = w.Settlements.Get(sid).Granary, f0 = w.Metrics.ThreshFu;
            w.AdvanceTo(w.Now + SimTime.Day);
            var s1 = w.Settlements.Get(sid);
            Assert.AreEqual(f0, w.Metrics.ThreshFu, 1e-9, "no hand able to thresh: nothing threshed");
            Assert.LessOrEqual(Threshing.Threshed(in s1), 1e-6, "the store all in the sheaf still");
            Assert.LessOrEqual(g0 - s1.Granary, 0.02 * g0, "nothing of it eaten or drawn into the larders (only the store's rot)");
            // the hands well again: they thresh, and the larders fill from the grain threshed
            res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.Alive) p.Health = Math.Max(0.6f, p.Health); }
            w.AdvanceTo(w.Now + SimTime.Days(2));
            var s2 = w.Settlements.Get(sid);
            Assert.Greater(w.Metrics.ThreshFu, f0 + 1.0, "threshed once there are hands");
            Assert.Less(s2.Sheaves, s2.Granary, "grain out of the sheaf in the store");
            Assert.Greater(Larders(w, sid), 0.0, "and in the larders");
        }

        [Test]
        public void LivedDaysThreshAtTheFloorAheadOfTheirNeed()
        {
            var w = TestWorlds.Make(4313);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            ToLocalHour(w, sid, 6.5);
            // premise: most of the store in the sheaf - a day of the village's need threshed
            ref var s = ref w.Settlements.Get(sid);
            double day = Threshing.DayNeed(w, in s);
            Assume.That(s.Granary > 20.0 * day);
            s.Sheaves = s.Granary - day;
            double f0 = w.Metrics.ThreshFu;
            Threshing.Floor(w, sid, out double fx, out double fy);
            int seen = 0, atFloor = 0, flail = 0;
            long end = w.Now + SimTime.Hours(12);
            while (w.Now < end)
            {
                w.AdvanceTo(w.Now + 900);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (!p.Alive || p.Act != ActivityKind.Thresh || w.Now < p.TA || p.At != sid) continue;
                    seen++;
                    double dx = p.ToX - fx, dy = p.ToY - fy;
                    if (dx * dx + dy * dy <= (Threshing.Ring + 0.01) * (Threshing.Ring + 0.01)) atFloor++;
                    if (Presence.Of(w, in p, w.Now, out _, out _, out byte pose, out _) && pose == Presence.Flail) flail++;
                }
            }
            var s1 = w.Settlements.Get(sid);
            double fu = w.Metrics.ThreshFu - f0;
            Console.WriteLine("a lived day: " + seen + " threshers seen (" + atFloor + " at the floor, " + flail + " swinging the flail); threshed " + fu.ToString("0") + " FU against " + day.ToString("0") + " a day's need; threshed in the store " + (Threshing.Threshed(in s1) / day).ToString("0.0") + " days");
            Assert.Greater(seen, 0, "people go to the floor");
            Assert.AreEqual(seen, atFloor, "at the floor, in its ring");
            Assert.Greater(flail, 0, "the flail drawn");
            Assert.Greater(fu, day, "more than the day's need threshed: ahead of it");
            Assert.GreaterOrEqual(Threshing.Threshed(in s1), 2.0 * day, "the store's threshed grain brought up ahead of its need");
            Assert.LessOrEqual(Threshing.Threshed(in s1), (Threshing.AheadDays(s1.Crop) + 6.0) * day, "and not much beyond it (the stints under way are counted)");
        }

        [Test]
        public void AHungryHandThreshesBeforeItForages()
        {
            var w = TestWorlds.Make(4314);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            ToLocalHour(w, sid, 7.0);
            // premise: nothing threshed in the store, nothing in the larders or cooked, everyone hungry since yesterday's evening
            ref var s = ref w.Settlements.Get(sid);
            Assume.That(s.Granary > 50.0);
            s.Sheaves = s.Granary;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) { ref var h = ref w.Households.Get(hl[i]); h.Larder = 0; h.Ready = 0; }
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); p.LastMeal = w.Now - SimTime.Hours(12); }
            int hungryThresh = 0, wildNoFood = 0;
            long end = w.Now + SimTime.Hours(6);
            while (w.Now < end)
            {
                w.AdvanceTo(w.Now + 600);
                for (int i = 0; i < res.Count; i++)
                {
                    var p = w.People.Get(res[i]);
                    if (!p.Alive) continue;
                    if (p.Act == ActivityKind.Thresh && p.Why == WhyCode.Hungry) hungryThresh++;
                    if ((p.Act == ActivityKind.Gather || p.Act == ActivityKind.Hunt || p.Act == ActivityKind.Fish) && p.Why == WhyCode.NoFood) wildNoFood++;
                }
            }
            int fed = 0, alive = 0;
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (!p.Alive) continue; alive++; if (w.Now - p.LastMeal < SimTime.Hours(6)) fed++; }
            Console.WriteLine("hungry at the floor " + hungryThresh + " person-looks, out to the wild for want of food " + wildNoFood + "; fed within the morning " + fed + " of " + alive);
            Assert.Greater(hungryThresh, 0, "a hungry hand goes to the floor");
            Assert.AreEqual(0, wildNoFood, "nobody goes out to the wild for want of food beside the sheaves");
            Assert.Greater(fed, alive / 2, "the household eats from the grain threshed the same morning");
        }

        [Test]
        public void SheavesKeepThroughASaveAndTheNoThreshControlHasNone()
        {
            var w = TestWorlds.Make(4315);
            Coarse(w);
            int sid = w.Settlements.First();
            Until(w, sid, CropStage.Ripe);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assume.That(w.Settlements.Get(sid).Sheaves > 1.0);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.Settlements.Get(sid).Sheaves, w2.Settlements.Get(sid).Sheaves, 0.0, "the sheaves saved exactly");
            Assert.AreEqual(w.Settlements.Get(sid).ThreshOut, w2.Settlements.Get(sid).ThreshOut, 0.0);
            Assert.AreEqual(w.Metrics.ThreshFu, w2.Metrics.ThreshFu, 0.0);
            Threshing.Off = true;
            try
            {
                var w3 = TestWorlds.Make(4315);
                Coarse(w3);
                Until(w3, sid, CropStage.Ripe);
                Until(w3, sid, CropStage.Harvested);
                Assert.AreEqual(0.0, w3.Settlements.Get(sid).Sheaves, "--nothresh: the grain comes in threshed");
                Assert.AreEqual(0.0, w3.Metrics.ThreshFu, "and nobody threshes");
            }
            finally { Threshing.Off = false; }
        }
    }
}
