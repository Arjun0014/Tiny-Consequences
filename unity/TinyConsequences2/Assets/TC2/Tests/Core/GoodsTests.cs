using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Goods (D2-058): a household's clothes, tools and vessels wear with use and are made and mended by its own hands; worn
    /// clothes let the cold in, worn tools slow the work. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class GoodsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void SetGear(World w, int sid, double perHead)
        {
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Gear = (float)(perHead * w.Members(hhs[i]).Count);
        }

        static double GearPerHead(World w, int sid)
        {
            var hhs = w.HouseholdsOf(sid);
            double g = 0; int n = 0;
            for (int i = 0; i < hhs.Count; i++) { g += w.Households.Get(hhs[i]).Gear; n += w.Members(hhs[i]).Count; }
            return n > 0 ? g / n : 0;
        }

        [Test]
        public void WornGoodsAreMadeAgainByTheHouseholdsOwnHands()
        {
            var w = TestWorlds.Make(431);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            SetGear(w, sid, 0.3);   // premise: the village's clothes and tools are worn thin
            double h0 = w.Metrics.CraftHours, m0 = w.Metrics.GearMade;
            w.AdvanceTo(w.Now + SimTime.Days(4));
            double after = GearPerHead(w, sid), hours = w.Metrics.CraftHours - h0, made = w.Metrics.GearMade - m0;
            Console.WriteLine("goods a head 0.30 -> " + after.ToString("0.00") + " in four days; making " + hours.ToString("0") + " h for " + made.ToString("0.00") + " (" + (hours / Math.Max(1e-9, made)).ToString("0") + " h each)");
            Assert.Greater(after, 0.3 + 4 * 0.0, "they made more than the days wore");
            Assert.That(hours / made, Is.InRange(0.999 * Goods.MakeHours, 2.0 * Goods.MakeHours), "about the law's hours for each person's goods (more by children and the frail)");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void WornClothesLetTheColdIn()
        {
            // the law: the chill line rises as the clothes go
            Assert.Greater(Goods.ChillLine(0.0), Goods.ChillLine(1.0));
            double t = -12, indoors = Hearth.Indoors(t, Construction.House, Hearth.Wanted(t, Construction.House));
            Assert.AreEqual(0.0, Hearth.Chill(indoors, 30, 1.0), "a warm house and good clothes: no harm");
            Assert.Greater(Hearth.Chill(indoors, 30, 0.0), 0.0, "the same house in rags: the cold gets in");
            // the world: a village in a deep frost in houses with wood, a: clothed, b: in rags (kept so)
            var a = TestWorlds.Make(433); var b = TestWorlds.Make(433);
            Coarse(a); Coarse(b);
            int sid = a.Settlements.First();
            for (int d = 0; d < 10; d++)
            {
                foreach (var w in new[] { a, b })
                {
                    int c = w.Settlements.Get(sid).Cell;
                    w.Planet.TMean[c] = -12f; w.Planet.TAmp[c] = 0f; w.PlanetSys.RefreshMeans(w.Planet);
                    if (w.Settlements.Get(sid).Woodstore < 400) w.Settlements.Get(sid).Woodstore = 400;
                }
                SetGear(a, sid, 1.2); SetGear(b, sid, 0.0);
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(a.Now);
            }
            double ha = Mean(a, sid), hb = Mean(b, sid);
            Console.WriteLine("health after ten days of frost in warm houses: clothed " + ha.ToString("0.00") + ", in rags " + hb.ToString("0.00"));
            Assert.Greater(ha, hb, "rags let the cold in where clothes keep it out");
        }

        static double Mean(World w, int sid)
        {
            var res = w.Residents(sid); double s = 0; int n = 0;
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive) { s += p.Health; n++; } }
            return n > 0 ? s / n : 0;
        }

        [Test]
        public void WornToolsSlowTheWorkOfHands()
        {
            // the same village sowing for two days, a: well equipped, b: with worn-out tools (kept so); the field work a lived
            // hour brings is measured against the hours people spent in the fields
            var a = TestWorlds.Make(435); var b = TestWorlds.Make(435);
            int sid = a.Settlements.First();
            double rateA = FieldRate(a, sid, 1.2), rateB = FieldRate(b, sid, 0.0);
            Console.WriteLine("field work an hour: well equipped " + rateA.ToString("0.000") + ", worn tools " + rateB.ToString("0.000") + " (" + (rateB / rateA).ToString("0.00") + ")");
            Assert.That(rateB / rateA, Is.InRange(Goods.WorkFactor(0.0) - 0.08, Goods.WorkFactor(0.0) + 0.08), "worn tools work at ~0.7 of good ones");
        }

        static double FieldRate(World w, int sid, double gear)
        {
            double hours = 0, work = 0;
            long end = w.Now + SimTime.Days(2);
            while (w.Now < end)
            {
                SetGear(w, sid, gear);
                double before = w.Settlements.Get(sid).CropWork;
                w.AdvanceTo(w.Now + 120);
                double gain = w.Settlements.Get(sid).CropWork - before;
                if (gain > 0) work += gain;
                var res = w.Residents(sid);
                for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.Act == ActivityKind.Fields && w.Now >= p.TA) hours += 120.0 / SimTime.Hour * (0.6 + p.SkFarm) * (0.5 + 0.5 * p.Health); }
            }
            return work / Math.Max(1e-9, hours);
        }

        [Test]
        public void CoarseAndActivityMakeAlike()
        {
            // from goods worn to 0.6 a head in every village, both resolutions make and mend toward what households keep
            var a = TestWorlds.Make(437); var b = TestWorlds.Make(437);
            Coarse(b);
            a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(a.Now);
            for (int sid = a.Settlements.First(); sid != 0; sid = a.Settlements.Next(sid)) { SetGear(a, sid, 0.6); SetGear(b, sid, 0.6); }
            double ha = a.Metrics.CraftHours, hb = b.Metrics.CraftHours, days = 0;
            var byAge = new double[4]; var pop = new double[4];
            for (int d = 0; d < 12; d++)
            {
                days += a.LivingPeople();
                long end = a.Now + SimTime.Day;
                while (a.Now < end)
                {
                    a.AdvanceTo(a.Now + 300);
                    for (int id = a.People.First(); id != 0; id = a.People.Next(id))
                    {
                        var q = a.People.Get(id); if (!q.Alive) continue;
                        double ag = q.AgeYears(a.Now); int g = ag < 8 ? 0 : ag < 12 ? 1 : ag < 60 ? 2 : 3;
                        pop[g] += 300.0 / SimTime.Day; if (q.Act == ActivityKind.Craft) byAge[g] += 300.0 / SimTime.Hour;
                    }
                }
                b.AdvanceTo(a.Now);
            }
            Console.WriteLine("lived making h per person-day by age: under 8 " + (byAge[0] / pop[0]).ToString("0.00") + " 8-11 " + (byAge[1] / pop[1]).ToString("0.00") + " 12-59 " + (byAge[2] / pop[2]).ToString("0.00") + " 60+ " + (byAge[3] / pop[3]).ToString("0.00"));
            double pa = (a.Metrics.CraftHours - ha) / days, pb = (b.Metrics.CraftHours - hb) / days;
            double ga = 0, gb = 0; int n = 0;
            for (int sid = a.Settlements.First(); sid != 0; sid = a.Settlements.Next(sid)) { ga += GearPerHead(a, sid); gb += GearPerHead(b, sid); n++; }
            ga /= n; gb /= n;
            Console.WriteLine("making per person-day from worn goods: activity " + pa.ToString("0.00") + " h, coarse " + pb.ToString("0.00") + " h; goods a head after twelve days " + ga.ToString("0.00") + " / " + gb.ToString("0.00"));
            Assert.Greater(ga, 0.6); Assert.Greater(gb, 0.6);
            Assert.That(pb / pa, Is.InRange(0.75, 1.33), "the same making at both resolutions");
            Assert.That((gb - 0.6) / (ga - 0.6), Is.InRange(0.75, 1.33), "and the same goods made");
        }
    }
}
