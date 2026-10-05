using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The household's own work and warmth (D2-056): meals are made ready before they are eaten, water is carried from the well,
    /// the hearth burns the village's wood, and a roof and a fire stand between the cold and the body. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class HouseholdWorkTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void ToLocalHour(World w, int sid, double h)
        {
            long sod = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long dt = SimTime.Hours(h) - sod;
            if (dt <= 0) dt += SimTime.Day;
            w.AdvanceTo(w.Now + dt);
        }

        [Test]
        public void HungryPeopleWithNothingCookedMakeTheMealFirst()
        {
            var w = TestWorlds.Make(421);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            ToLocalHour(w, sid, 9.0);
            // premise: nothing is cooked in any home of the village, the larders are full, everyone has gone six hours unfed
            var hhs = w.HouseholdsOf(sid);
            var hungry = new List<int>();
            for (int i = 0; i < hhs.Count; i++)
            {
                ref var hh = ref w.Households.Get(hhs[i]);
                hh.Ready = 0; hh.Larder = 10;
                var mem = w.Members(hhs[i]);
                for (int k = 0; k < mem.Count; k++)
                {
                    ref var p = ref w.People.Get(mem[k]);
                    if (!p.Alive || p.At != sid) continue;
                    p.LastMeal = w.Now - SimTime.Hours(6);
                    if (p.AgeYears(w.Now) >= 2) hungry.Add(mem[k]);   // (babies live on their mothers' milk)
                }
            }
            long start = w.Now;
            double prepH = w.Metrics.PrepHours, prepFu = w.Metrics.PrepFu;
            w.AdvanceTo(w.Now + SimTime.Hours(6));   // (a long step - a stint in the fields - ends before hunger is felt)
            int fed = 0;
            foreach (int pid in hungry) { var p = w.People.Get(pid); if (p.Alive && p.LastMeal > start) fed++; }
            Console.WriteLine("of " + hungry.Count + " hungry, fed within six hours " + fed + "; cooked " + (w.Metrics.PrepFu - prepFu).ToString("0.0") + " FU in " + (w.Metrics.PrepHours - prepH).ToString("0.0") + " h");
            Assert.Greater(w.Metrics.PrepFu - prepFu, 0, "someone cooked");
            Assert.GreaterOrEqual(fed, (int)(0.9 * hungry.Count), "the hungry were fed - by what was cooked");
            double rate = (w.Metrics.PrepFu - prepFu) / (w.Metrics.PrepHours - prepH);
            Assert.That(rate, Is.InRange(0.5 * Hearth.PrepRate, Hearth.PrepRate + 1e-9), "an hour at the quern and the hearth makes about a FU ready");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ChildrenAndGrownPeopleCarryWaterWhenThePotsAreLow()
        {
            var w = TestWorlds.Make(423);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            ToLocalHour(w, sid, 8.0);
            // premise: every pot in the world is empty
            for (int h = w.Households.First(); h != 0; h = w.Households.Next(h)) w.Households.Get(h).Water = 0f;
            var kids = new HashSet<int>(); var grown = new HashSet<int>();
            long end = w.Now + SimTime.Hours(4);
            while (w.Now < end)
            {
                w.AdvanceTo(w.Now + 120);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (!p.Alive || p.Act != ActivityKind.Water) continue;
                    if (p.AgeYears(w.Now) < 12) kids.Add(id); else grown.Add(id);
                }
            }
            int homes = 0, watered = 0;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) { homes++; if (w.Households.Get(hhs[i]).Water > 0) watered++; }
            Console.WriteLine("carried water: children " + kids.Count + ", grown " + grown.Count + "; homes of the first village with water after four hours " + watered + "/" + homes);
            Assert.Greater(kids.Count, 0, "children of six and more carry water");
            Assert.Greater(grown.Count, 0, "and grown people");
            Assert.GreaterOrEqual(watered, (int)(0.9 * homes), "the pots were filled");
        }

        [Test]
        public void ARoofAndAFireKeepTheColdOut()
        {
            // the law: a house with its fire is warm in a hard frost; a camp with none is not, and the cold takes most from infants
            double t = -12;
            Assert.GreaterOrEqual(Hearth.Indoors(t, Construction.House, Hearth.Wanted(t, Construction.House)), Hearth.ChillLine);
            Assert.Less(Hearth.Indoors(t, Construction.Camp, 0), Hearth.ChillLine);
            Assert.Greater(Hearth.Chill(Hearth.Indoors(t, Construction.Camp, 0), 0.5), Hearth.Chill(Hearth.Indoors(t, Construction.Camp, 0), 30));
            // the world: a village in a deep frost, a: in houses with wood to burn, b: in camps with none
            var a = TestWorlds.Make(425); var b = TestWorlds.Make(425);
            Coarse(a); Coarse(b);
            int sid = a.Settlements.First();
            long mark = b.History.LastId;
            double woodA = 0;
            for (int d = 0; d < 12; d++)
            {
                foreach (var w in new[] { a, b })
                {
                    // premise each day (the climate would warm the cell back): the village's ground in a deep, steady frost
                    int c = w.Settlements.Get(sid).Cell;
                    w.Planet.TMean[c] = -12f; w.Planet.TAmp[c] = 0f; w.PlanetSys.RefreshMeans(w.Planet);
                }
                if (a.Settlements.Get(sid).Woodstore < 400) a.Settlements.Get(sid).Woodstore = 400;
                woodA += a.Settlements.Get(sid).Woodstore;
                var bh = b.HouseholdsOf(sid);
                for (int i = 0; i < bh.Count; i++) { ref var hh = ref b.Households.Get(bh[i]); hh.Shelter = Construction.Camp; hh.Build = 0f; }
                b.Settlements.Get(sid).Woodstore = 0;
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(a.Now);
                woodA -= a.Settlements.Get(sid).Woodstore;
            }
            var ah = a.HouseholdsOf(sid);
            for (int i = 0; i < ah.Count; i++) Assert.GreaterOrEqual(a.Households.Get(ah[i]).Indoor, (float)Hearth.ChillLine, "a house with a fire is warm");
            var bhh = b.HouseholdsOf(sid);
            for (int i = 0; i < bhh.Count; i++) Assert.Less(b.Households.Get(bhh[i]).Indoor, (float)Hearth.ChillLine, "a camp without a fire is not");
            Assert.Greater(woodA, 0, "the hearths burnt the store's wood");
            bool told = false;
            for (int i = b.History.FirstIndexAfter(mark); i < b.History.Count; i++) if (b.History.At(i).Kind == HistoryKind.ColdHomes && b.History.At(i).Settlement == sid) told = true;
            Assert.IsTrue(told, "the cold homes are history");
            double ha = MeanHealth(a, sid, 0, 200), hb = MeanHealth(b, sid, 0, 200);
            double hbYoung = MeanHealth(b, sid, 0, 5), hbGrown = MeanHealth(b, sid, 16, 60);
            Console.WriteLine("health after twelve days of frost: houses with wood " + ha.ToString("0.00") + ", camps without " + hb.ToString("0.00") +
                              " (under five " + hbYoung.ToString("0.00") + ", grown " + hbGrown.ToString("0.00") + "); died of the cold " + a.Metrics.ColdDeaths + " / " + b.Metrics.ColdDeaths);
            Assert.AreEqual(0, a.Metrics.ColdDeaths, "none in warm homes");
            Assert.Greater(ha, hb, "the cold harms those it reaches");
            if (!double.IsNaN(hbYoung)) Assert.Less(hbYoung, hbGrown, "the small most");
            Assert.IsNull(TestWorlds.Audit(a)); Assert.IsNull(TestWorlds.Audit(b));
        }

        static double MeanHealth(World w, int sid, double lo, double hi)
        {
            var res = w.Residents(sid);
            double sum = 0; int n = 0;
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                double age = p.AgeYears(w.Now);
                if (!p.Alive || age < lo || age >= hi) continue;
                sum += p.Health; n++;
            }
            return n > 0 ? sum / n : double.NaN;
        }

        [Test]
        public void CoarseAndActivityKeepHouseAlike()
        {
            var a = TestWorlds.Make(427); var b = TestWorlds.Make(427);
            Coarse(b);
            a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(a.Now);
            double pa = a.Metrics.PrepHours, pb = b.Metrics.PrepHours, fa = a.Metrics.PrepFu, fb = b.Metrics.PrepFu;
            double wa = a.Metrics.WaterHours, wb = b.Metrics.WaterHours, woodA = 0, woodB = 0;
            double daysA = 0, daysB = 0;
            for (int d = 0; d < 6; d++)
            {
                daysA += a.LivingPeople(); daysB += b.LivingPeople();
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(a.Now);
            }
            double prepA = (a.Metrics.PrepHours - pa) / daysA, prepB = (b.Metrics.PrepHours - pb) / daysB;
            double fuA = (a.Metrics.PrepFu - fa) / daysA, fuB = (b.Metrics.PrepFu - fb) / daysB;
            double watA = (a.Metrics.WaterHours - wa) / daysA, watB = (b.Metrics.WaterHours - wb) / daysB;
            Console.WriteLine("water trips per person-day activity " + (a.Metrics.WaterTrips / daysA).ToString("0.000") + " coarse " + (b.Metrics.WaterTrips / daysB).ToString("0.000"));
            Console.WriteLine("per person-day: hours making food ready activity " + prepA.ToString("0.00") + " coarse " + prepB.ToString("0.00") +
                              "; FU made " + fuA.ToString("0.00") + " / " + fuB.ToString("0.00") + "; hours carrying water " + watA.ToString("0.000") + " / " + watB.ToString("0.000"));
            Assert.That(fuA, Is.InRange(0.5, 1.1), "about the day's food is made ready each day");
            Assert.That(fuB / fuA, Is.InRange(0.8, 1.25), "the same food made ready at both resolutions");
            Assert.That(prepB / prepA, Is.InRange(0.75, 1.33), "with the same hours");
            Assert.That(watB / watA, Is.InRange(0.6, 1.6), "and the same water carried");
        }
    }
}
