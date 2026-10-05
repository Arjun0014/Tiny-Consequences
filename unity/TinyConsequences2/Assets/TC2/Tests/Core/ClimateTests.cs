using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Climate mechanisms (05 §3b, D2-024): forcing, feedbacks, ice and sea level, moving coasts.</summary>
    [TestFixture]
    public class ClimateTests
    {
        static void Seasons(World w, int n)
        {
            var P = w.Planet;
            long t = w.Now;
            for (int i = 0; i < n; i++)
            {
                w.PlanetSys.Climate.Step(P, w.Seed, t, Climate.StepYears);
                w.PlanetSys.RefreshMeans(P);
                w.AfterClimate();
                t += SimTime.Year / 4;
            }
        }

        [Test]
        public void AGeneratedWorldIsTemperateWithWarmTropicsAndColdPoles()
        {
            var w = TestWorlds.Make(31);
            var P = w.Planet;
            Assert.That(P.TGlobal, Is.InRange(8.0, 20.0), "global mean");
            double eq = Climate.ZonalAt(P.ZonalT, 0), pole = 0.5 * (Climate.ZonalAt(P.ZonalT, 1) + Climate.ZonalAt(P.ZonalT, -1));
            Assert.That(eq - pole, Is.InRange(30.0, 55.0), "equator to pole");
        }

        [Test]
        public void HigherObliquityWarmsThePolesAndCoolsTheTropics()
        {
            double lo = 18 * DMath.Deg2Rad, hi = 28 * DMath.Deg2Rad;
            Assert.Greater(Climate.InsolationShape(1, hi), Climate.InsolationShape(1, lo), "poles get more sun");
            Assert.Less(Climate.InsolationShape(0, hi), Climate.InsolationShape(0, lo), "the equator gets less");
        }

        [Test]
        public void DoublingCO2WarmsTheWorldWithinTheSensitivityBand()
        {
            var a = TestWorlds.Make(32); var b = TestWorlds.Make(32);
            b.Planet.CO2 = 560;
            Seasons(a, 400); Seasons(b, 400);
            double dT = b.Planet.TGlobal - a.Planet.TGlobal;
            Assert.That(dT, Is.InRange(1.5, 6.0), "warming for doubled CO2 (with ice and snow feedbacks)");
            Assert.LessOrEqual(b.Planet.IceVolume, a.Planet.IceVolume, "a warmer world holds no more ice");
        }

        [Test]
        public void AVolcanicVeilCoolsForAFewYearsAndFades()
        {
            var a = TestWorlds.Make(33); var b = TestWorlds.Make(33);
            b.Planet.Aerosol = 0.4;
            Seasons(a, 6); Seasons(b, 6);
            Assert.Less(b.Planet.TGlobal, a.Planet.TGlobal - 0.2, "cooler within a year and a half");
            Seasons(a, 60); Seasons(b, 60);
            Assert.Less(b.Planet.Aerosol, 0.01, "the veil fades");
            Assert.AreEqual(a.Planet.TGlobal, b.Planet.TGlobal, 0.3, "and the climate returns");
        }

        [Test]
        public void AColderWorldGrowsIceAndTheSeaFallsByTheWaterItLocksUp()
        {
            var w = TestWorlds.Make(34);
            var P = w.Planet;
            double ice0 = P.IceVolume;
            P.GlobalTempOffset = -6;
            Seasons(w, 400);
            Assert.Greater(P.IceVolume, ice0 * 1.2, "ice sheets grow");
            double expect = P.SeaLevelRef - 0.9 * P.IceVolume / P.OceanAreaRef + 0.5 * (P.TGlobal - P.TGlobalRef);
            Assert.AreEqual(expect, P.SeaLevelNow, 1e-9, "sea level = reference - ice as water over the ocean + expansion");
            Assert.Less(P.SeaLevelNow, P.SeaLevel0 - 1, "the sea fell");
        }

        [Test]
        public void ACoastThatMovesRebuildsTheDrainageAndEntersTheHistory()
        {
            var w = TestWorlds.Make(35);
            var P = w.Planet;
            int ver = w.Drainage.Version;
            double coast0 = P.SeaLevel;
            int histBefore = w.History.Count;
            P.GlobalTempOffset = -8;
            Seasons(w, 600);
            Assert.Less(P.SeaLevel, coast0 - 1, "the coastline followed the falling sea");
            Assert.Greater(w.Drainage.Version, ver, "the drainage was rebuilt");
            Assert.AreEqual(P.SeaLevel, w.Drainage.BuiltSeaLevel, 1e-9, "for the new coast");
            bool coastEvent = false, iceAge = false;
            for (int i = histBefore; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.CoastChanged) coastEvent = true;
                if (e.Kind == HistoryKind.IceAge) iceAge = true;
            }
            Assert.IsTrue(coastEvent, "the history tells of the new shore");
            Assert.IsTrue(iceAge, "and of the age of ice");
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                Assert.GreaterOrEqual(w.Drainage.Receiver[c], 0, "new land drains: " + c);
                if (P.FieldOwner[c] != 0) Assert.IsTrue(P.IsLand(c));
            }
        }

        [Test]
        public void SaveAndLoadAcrossClimateStepsContinueIdentically()
        {
            var w = TestWorlds.Make(36);
            var b = Budget.Unlimited;
            w.AdvanceTo(w.Now + 10 * SimTime.Day, ref b);
            var bytes = SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, "");
            var w2 = SaveFile.Load(bytes, out _);
            var b1 = Budget.Unlimited; var b2 = Budget.Unlimited;
            w.AdvanceTo(w.Now + 30 * SimTime.Day, ref b1);
            w2.AdvanceTo(w2.Now + 30 * SimTime.Day, ref b2);
            Assert.AreEqual(w.Planet.TGlobal, w2.Planet.TGlobal, "the same climate");
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "the same world after two seasons of climate");
        }
    }
}
