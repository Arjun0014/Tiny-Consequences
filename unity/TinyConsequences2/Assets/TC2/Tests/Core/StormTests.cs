using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Storms (05 §3, D2-032): fed by warm seas, starved by land, their harm caused by them, their rain in rivers.</summary>
    [TestFixture]
    public class StormTests
    {
        static Storm Make(World w, Vec3d at, float wind, byte kind = 0)
        {
            Storms.Frame(at, out Vec3d east, out _);
            return new Storm { Id = w.Ids.Next(EntityKind.Process), At = at, Heading = east * -1.0, Speed = 0.05f, Wind = wind, Peak = wind, Radius = 1800f, Kind = kind, Born = w.Now };
        }

        [Test]
        public void WarmSeasFeedACycloneAndLandStarvesIt()
        {
            var w = TestWorlds.Make(81);
            var P = w.Planet;
            int sea = -1, land = -1;
            for (int c = 0; c < P.Cells && (sea < 0 || land < 0); c++)
            {
                double lat = System.Math.Abs(P.Grid.Center[c].Latitude) * 180 / System.Math.PI;
                if (lat > 20) continue;
                if (!P.IsLand(c) && sea < 0) sea = c;
                if (P.IsLand(c) && land < 0) land = c;
            }
            Assert.GreaterOrEqual(sea, 0); Assert.GreaterOrEqual(land, 0);
            // a sea as warm as Earth's tropics: the storm grows toward what it allows
            P.TMean[sea] = 29f; P.TAmp[sea] = 0f;
            var s = Make(w, P.Grid.Center[sea], 20f);
            s.Speed = 0f;
            for (int i = 0; i < 16; i++) Storms.Move(P, ref s, Storms.StepSeconds, w.Now);
            Assert.Greater(s.Wind, 40f, "two days over a 29 C sea make a great storm");
            var l = Make(w, P.Grid.Center[land], 50f);
            l.Speed = 0f;
            for (int i = 0; i < 8; i++) Storms.Move(P, ref l, Storms.StepSeconds, w.Now);
            Assert.Less(l.Wind, 25f, "a day over land and it is a gale at most");
        }

        [Test]
        public void AGreatStormsHarmIsCausedByItAndItsRainFallsOnTheNextDay()
        {
            var w = TestWorlds.Make(82);
            var P = w.Planet;
            int sid = w.Settlements.First();
            ref var st = ref w.Settlements.Get(sid);
            st.Crop = CropStage.Growing;
            var anchor = st.Anchor;
            // the eyewall over the village's fields
            Storms.Frame(anchor, out Vec3d east, out _);
            Vec3d at = (anchor * P.Radius + east * 300.0).Normalized;
            P.Storms.Add(Make(w, at, 58f));
            for (int c = 0; c < P.Cells; c++) P.StormRain[c] = 0f;
            long before = w.History.LastId;
            w.StormStep();
            long storm = 0; bool flattened = false;
            for (int i = w.History.FirstIndexAfter(before); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Storm) { storm = e.Id; continue; }
                Assert.AreNotEqual(0, storm, "the storm is recorded first");
                Assert.AreEqual(storm, e.Root, e.Kind + " traces to the storm");
                if (e.Kind == HistoryKind.CropsFlattened && e.Settlement == sid) flattened = true;
            }
            Assert.IsTrue(flattened, "the wind flattened the crop");
            Assert.Greater(w.Settlements.Get(sid).CropBurnt, 0.02, "a share lost in the first three hours");
            int wet = P.Grid.CellOf(anchor);
            Assert.Greater(P.StormRain[wet], 0.2f, "rain waits for the next day");
            // the next weather day takes it
            var b = Budget.Unlimited;
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hour, ref b);
            Assert.Greater(P.RainToday[wet], 0.2f, "it rained");
        }

        [Test]
        public void SaveLoadWithStormsInFlightContinuesIdentically()
        {
            var a = TestWorlds.Make(83);
            a.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(a.Now) + SimTime.Day);
            var P = a.Planet;
            P.Storms.Add(Make(a, P.Grid.Center[100], 30f));
            P.Storms.Add(Make(a, P.Grid.Center[9000], 26f, 1));
            a.Queue.Push(a.Now + Storms.StepSeconds, Phase.Process, EventKind.StormStep, 0);
            var b0 = Budget.Unlimited;
            a.AdvanceTo(a.Now + SimTime.Day, ref b0);
            Assert.Greater(P.Storms.Count, 0, "storms still blowing at the save");
            var b = SaveFile.Load(SaveFile.Save(a, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "loaded");
            var b1 = Budget.Unlimited; var b2 = Budget.Unlimited;
            a.AdvanceTo(a.Now + 6 * SimTime.Day, ref b1);
            b.AdvanceTo(b.Now + 6 * SimTime.Day, ref b2);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "the same world through the storms' lives");
        }
    }
}
