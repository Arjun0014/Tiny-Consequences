using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Working the land harder (C3.6, D2-131): where the arable within a village's reach is short of what its people want to sow, it plans more
    /// ploughing and hoeing a hectare at sowing - enough to bring its harvest up to what the wanted hectares would give, as far as falling returns
    /// allow - and only the extra work done adds to the harvest. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class TilthTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Premise: villages on cramped ground - their fields lie within a short reach of their houses, short of their people's want
        /// (the state a grown village reaches on its land).</summary>
        static World Cramped(ulong seed, double reach)
        {
            var cfg = TestWorlds.Small(seed);
            cfg.FieldReach = reach;
            return WorldGenerator.Create(cfg);
        }

        /// <summary>Lives the world day by day until the village's crop reaches the stage (at most a year and a half).</summary>
        static void Until(World w, int sid, CropStage stage)
        {
            for (int d = 0; d < 72; d++)
            {
                if (w.Settlements.Get(sid).Crop == stage) return;
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            Assert.Fail("the crop never reached " + stage);
        }

        /// <summary>Lives the world through the village's next sowing (the plan is made on its first day).</summary>
        static void ToSowing(World w, int sid)
        {
            if (w.Settlements.Get(sid).Crop == CropStage.Sown) Until(w, sid, CropStage.Growing);
            Until(w, sid, CropStage.Sown);
        }

        static int CrampedVillage(World w)
        {
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (s.Inhabited && s.Population > 0 && s.ArableHa > 2 && s.ArableHa < 0.8 * s.Population * w.Config.FieldHaPerPerson) return sid;
            }
            return 0;
        }

        [Test]
        public void TheExtraWorkFollowsTheShortfallAtFallingReturns()
        {
            Assert.AreEqual(0.0, SettlementSystem.TilthFor(40, 40), "fields enough: no extra work");
            Assert.AreEqual(0.0, SettlementSystem.TilthFor(40, 30), "more fields than wanted: none");
            double last = 0;
            for (double f = 39; f >= 20; f -= 1)
            {
                double t = SettlementSystem.TilthFor(f, 40);
                Assert.Greater(t, 0.0);
                Assert.GreaterOrEqual(t, last, "the shorter the land, the more extra work a hectare");
                Assert.LessOrEqual(t, SettlementSystem.TilthMax, "never more than the most a village plans");
                if (t < SettlementSystem.TilthMax)
                    Assert.AreEqual(40.0 / f, 1.0 + SettlementSystem.GainOf(t), 1e-9, "enough to bring the harvest of its hectares up to the wanted ones'");
                last = t;
            }
            Assert.AreEqual(SettlementSystem.TilthMax, SettlementSystem.TilthFor(10, 40), "far short: the most it plans");
            // returns fall with every hour: each tenth more of the season's hours adds less than the one before, and the harvest an hour falls
            double prevStep = double.MaxValue, prevPerHour = 1.0;
            for (double e = 0.1; e <= SettlementSystem.TilthMax + 1e-9; e += 0.1)
            {
                double step = SettlementSystem.GainOf(e) - SettlementSystem.GainOf(e - 0.1);
                Assert.Less(step, prevStep, "falling returns");
                double perHour = (1.0 + SettlementSystem.GainOf(e)) / (1.0 + e);
                Assert.Less(perHour, prevPerHour, "the harvest an hour falls as the hours a hectare rise (Boserup)");
                prevStep = step; prevPerHour = perHour;
            }
            Assert.AreEqual(0.0, SettlementSystem.GainOf(0), "no extra work, no gain");
        }

        [Test]
        public void AVillageShortOfLandWorksItHarderAndReapsMore()
        {
            double[] reaped = new double[2];
            float gainOn = 0f;
            foreach (bool off in new[] { false, true })
            {
                SettlementSystem.TilthOff = off;
                try
                {
                    var w = Cramped(3131, 150);
                    Coarse(w);
                    int sid = CrampedVillage(w);
                    Assume.That(sid != 0, "a village whose arable within reach is short of its want");
                    ToSowing(w, sid);
                    var s = w.Settlements.Get(sid);
                    double want = s.Population * w.Config.FieldHaPerPerson * (1.0 + 0.5 * s.Memory);
                    if (off)
                    {
                        Assert.AreEqual(0.0, s.Tilth, "--notilth: worked as before");
                        Assert.AreEqual(s.FieldHa * SettlementSystem.SowHa, s.CropWorkNeed, 1e-9);
                        Assert.AreEqual(0L, s.TilthEvent);
                    }
                    else
                    {
                        Assert.Less(s.FieldHa, want, "premise: its fields are short of its people's want");
                        Assert.Greater(s.Tilth, 0.0, "it plans more work a hectare");
                        Assert.AreEqual(SettlementSystem.TilthFor(s.FieldHa, want), s.Tilth, 1e-12);
                        Assert.AreEqual(s.FieldHa * (SettlementSystem.SowHa + 0.5 * s.Tilth * SettlementSystem.SeasonHa), s.CropWorkNeed, 1e-9, "the sowing's work: a second ploughing and the harrowing");
                        Assert.IsTrue(w.History.TryGet(s.TilthEvent, out var e) && e.Kind == HistoryKind.FieldsWorkedHarder, "recorded");
                        Assert.That(e.ArgB, Is.InRange(s.Population - 3, s.Population + 3), "its people at the sowing");
                        Assert.AreEqual((int)DMath.RoundHalfUp(s.ArableHa), e.ArgA, "its arable within reach");
                        StringAssert.Contains("plough and hoe each hectare harder", Gazetteer.Food(w, sid), "the settlement page tells it");
                        StringAssert.Contains("began to work their fields harder", HistoryText.Describe(w, e), "and its history");
                        Until(w, sid, CropStage.Growing);
                        s = w.Settlements.Get(sid);
                        Assert.AreEqual(1.0, s.Sown, 1e-9, "the ordinary sowing done first");
                        Assert.Greater(s.TilthDone, 0.0, "extra ploughing done");
                        Assert.AreEqual(s.FieldHa * (SettlementSystem.TendHa + 0.5 * s.Tilth * SettlementSystem.SeasonHa), s.CropWorkNeed, 1e-9, "and more hoeing wanted");
                    }
                    Until(w, sid, CropStage.Harvested);
                    s = w.Settlements.Get(sid);
                    reaped[off ? 1 : 0] = s.LastHarvest;
                    if (off) { Assert.AreEqual(0f, s.TilthGain); continue; }
                    double done = DMath.Min(s.Tilth, s.TilthDone / (s.FieldHa * SettlementSystem.SeasonHa));
                    Assert.Greater(done, 0.5 * s.Tilth, "most of the extra work was done (idle hands)");
                    Assert.AreEqual((float)SettlementSystem.GainOf(done), s.TilthGain, "the harvest gained what the work done gives");
                    gainOn = s.TilthGain;
                }
                finally { SettlementSystem.TilthOff = false; }
            }
            Assert.Greater(gainOn, 0.05f);
            double ratio = reaped[0] / reaped[1];
            Assert.AreEqual(1.0 + gainOn, ratio, 0.06, "the same world reaps by about that much more (" + reaped[0] + " against " + reaped[1] + ")");
        }

        [Test]
        public void AVillageWithLandEnoughWorksAsBefore()
        {
            var w = TestWorlds.Make(3132);
            Coarse(w);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s0 = w.Settlements.Get(sid);
                if (!s0.Inhabited || s0.Population == 0 || s0.ArableHa < s0.Population * w.Config.FieldHaPerPerson) continue;
                ToSowing(w, sid);
                var s = w.Settlements.Get(sid);
                Assume.That(s.ArableHa >= s.Population * w.Config.FieldHaPerPerson * (1.0 + 0.5 * s.Memory), "still land enough at its sowing");
                Assert.AreEqual(0.0, s.Tilth);
                Assert.AreEqual(s.FieldHa * SettlementSystem.SowHa, s.CropWorkNeed, 1e-9);
                Assert.AreEqual(0L, s.TilthEvent);
                return;
            }
            Assert.Inconclusive("no village with land enough");
        }

        [Test]
        public void ASaveInTheHoeingContinuesTheSame()
        {
            var w = Cramped(3131, 150);
            Coarse(w);
            int sid = CrampedVillage(w);
            Assume.That(sid != 0);
            ToSowing(w, sid);
            Until(w, sid, CropStage.Growing);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assume.That(w.Settlements.Get(sid).Tilth > 0);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "loaded, the same");
            w.AdvanceTo(w.Now + SimTime.Days(30)); w2.AdvanceTo(w2.Now + SimTime.Days(30));
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and lives on the same through the harvest");
            Assert.AreEqual(w.Settlements.Get(sid).TilthGain, w2.Settlements.Get(sid).TilthGain);
        }

        // ---------------------------------------------------------------- the soil: the fields' heart, rest and the herds' dung (C3.6f, D2-139)
        /// <summary>Years of a village's fields with a share of its arable sown and its herds' grazing a hectare a day: what a sown hectare bears at the end.</summary>
        static double Years(int years, double arable, double sown, double grazedKgHaDay, double tired = 0)
        {
            var s = new Settlement { ArableHa = arable, FieldHa = sown * arable, Tired = tired };
            const double fresh = 130;
            for (int y = 0; y < years; y++)
            {
                s.Grazed = grazedKgHaDay * SimTime.DaysPerYear * arable;
                s.Spread = s.Grazed * Muck.PerDm * Muck.NightShare;   // (its yards' muck all carted out: C3.7c)
                double yield = s.FieldHa * fresh * 0.9 * SettlementSystem.Heart(in s);   // (an ordinary season: nine tenths of the crop grown)
                SettlementSystem.SoilYear(ref s, yield, fresh);
            }
            return SettlementSystem.Heart(in s);
        }

        [Test]
        public void CroppedEveryYearTheFieldsTireToAFloorAndRestMendsThem()
        {
            double worn = Years(80, 10, 1.0, 0);
            double five = Years(5, 10, 1.0, 0);
            Assert.Less(five, 0.9, "five years cropped: the crops a hectare fall");
            Assert.Greater(five, worn, "and go on falling");
            Assert.Less(worn, 0.55, "cropped every year, unmanured, a sown hectare settles near half a fresh field's crop");
            Assert.Greater(worn, SettlementSystem.SoilFloor + 0.05, "above what worn land bears: rain and the soil give back");
            double half = Years(80, 10, 0.5, 0, tired: 0.8);
            Assert.Greater(half, 0.85, "half of it resting each year, the land mends");
            // what the arable gives over the years: a share at rest bears more than cropping every hectare every year
            double fifth = Years(80, 10, 0.8, 0);
            Assert.Greater(0.8 * fifth, 1.0 * worn * 1.15, "resting a fifth, the arable gives more than cropped every year");
            Console.WriteLine("a sown hectare bears: cropped every year " + worn.ToString("0.000") + ", a fifth resting " + fifth.ToString("0.000") + ", half resting " + half.ToString("0.000"));
        }

        [Test]
        public void TheHerdsDungCarriesTheWastesHeartOntoTheFields()
        {
            double none = Years(80, 10, 1.0, 0);
            double some = Years(80, 10, 1.0, 2.0);
            double much = Years(80, 10, 1.0, 6.0);
            Assert.Greater(some, none, "herds grazing the waste: their dung mends the fields");
            Assert.Greater(much, some, "the more they graze, the more it gives back");
            SettlementSystem.DungOff = true;
            try { Assert.AreEqual(none, Years(80, 10, 1.0, 6.0), 1e-12, "--nodung: nothing back"); }
            finally { SettlementSystem.DungOff = false; }
        }

        [Test]
        public void ATiredVillageRestsAShareAndNewLandComesInFresh()
        {
            var w = TestWorlds.Make(5131);
            int sid = w.Settlements.First();
            Coarse(w);
            Until(w, sid, CropStage.Harvested);
            // premise: its fields tired, and its people have seen it in their crops
            { ref var s = ref w.Settlements.Get(sid); s.Tired = 0.8; s.TiredSeen = 0.8f; }
            double arable0 = w.Settlements.Get(sid).ArableHa;
            long mark = w.History.LastId;
            Until(w, sid, CropStage.Sown);
            var v = w.Settlements.Get(sid);
            Assert.AreEqual(0.32, v.Rest, 1e-6, "it rests a share by the tiredness it has seen");
            Assert.LessOrEqual(v.FieldHa, Math.Max(0.5, v.ArableHa * (1.0 - v.Rest)) + 1e-9, "and sows the rest");
            if (v.ArableHa > arable0) Assert.AreEqual(0.8 * arable0 / v.ArableHa, v.Tired, 1e-9, "land newly broken comes in fresh");
            else Assert.AreEqual(0.8, v.Tired, 1e-9);
            int rested = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.FieldsRested) continue;
                rested++;
                StringAssert.Contains("rest a share of their fields", HistoryText.Line(w, e));
                Console.WriteLine(HistoryText.Line(w, e));
            }
            Assert.AreEqual(1, rested, "recorded once");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void WithTheSoilOffEveryHectareBearsAFreshCrop()
        {
            var s = new Settlement { Tired = 0.9 };
            Assert.Less(SettlementSystem.Heart(in s), 0.5);
            SettlementSystem.SoilOff = true;
            try { Assert.AreEqual(1.0, SettlementSystem.Heart(in s)); }
            finally { SettlementSystem.SoilOff = false; }
        }

        [Test]
        public void ASaveKeepsTheFieldsHeart()
        {
            var w = TestWorlds.Make(5132);
            int sid = w.Settlements.First();
            { ref var s = ref w.Settlements.Get(sid); s.Tired = 0.37; s.TiredSeen = 0.31f; s.Rest = 0.12f; s.Grazed = 4321.5; }
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Living), out _);
            var a = w.Settlements.Get(sid); var b = w2.Settlements.Get(sid);
            Assert.AreEqual(a.Tired, b.Tired); Assert.AreEqual(a.TiredSeen, b.TiredSeen); Assert.AreEqual(a.Rest, b.Rest); Assert.AreEqual(a.Grazed, b.Grazed);
            Assert.AreEqual(w.StateHash(), w2.StateHash());
        }
    }
}
