using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The household's garden (C3.7a, `26` §2h): its croft is the free ground the layout leaves behind and beside its home; it is dug and weeded
    /// while the crops grow and what it bears is gathered into the larder from the ripening, by lived days and COARSE days alike. Mechanisms.
    /// </summary>
    [TestFixture]
    public class GardenTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static void ToLocalHour(World w, int sid, double h)
        {
            long sod = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long dt = SimTime.Hours(h) - sod;
            if (dt <= 0) dt += SimTime.Day;
            w.AdvanceTo(w.Now + dt);
        }

        /// <summary>Premise: every household of the village has raised its house where it lives (its first years lived), and its croft measured.</summary>
        static void Housed(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Building != 0) continue;
                hh.Shelter = Construction.House;
                Homes.Raised(w, hl[i]);
            }
            Gardens.Year(w, sid);
        }

        [Test]
        public void ACroftIsTheFreeGroundBehindAndBesideAHome()
        {
            var w = TestWorlds.Make(431);
            int sid = w.Settlements.First();
            Housed(w, sid);
            var hl = w.HouseholdsOf(sid);
            int homes = 0, crofts = 0; double sum = 0;
            for (int i = 0; i < hl.Count; i++)
            {
                var hh = w.Households.Get(hl[i]);
                if (hh.Building == 0) continue;
                homes++;
                double m2 = Gardens.Measure(w, sid, hh.Building, out double gx, out double gy);
                Assert.That(m2, Is.InRange(0.0, Gardens.MaxM2), "within a croft's size");
                Assert.AreEqual(m2, hh.Garden, 1e-3, "the croft measured when the year turned");
                if (m2 > 0)
                {
                    crofts++; sum += m2;
                    var b = w.Buildings.Get(hh.Building);
                    double d = Math.Sqrt((gx - b.X) * (gx - b.X) + (gy - b.Y) * (gy - b.Y));
                    Assert.That(d, Is.InRange(2.0, 22.0), "its gardener works behind or beside the house, not in it");
                }
            }
            Console.WriteLine("homes " + homes + ", with a croft " + crofts + ", mean " + (crofts > 0 ? sum / crofts : 0).ToString("0") + " m2");
            Assert.Greater(homes, 0);
            Assert.GreaterOrEqual(crofts, homes / 2, "most homes of a young village have ground behind them");
            Gardens.Off = true;
            try { Assert.AreEqual(0.0, Gardens.Measure(w, sid, w.Households.Get(hl[0]).Building, out _, out _), "--nogarden: none"); }
            finally { Gardens.Off = false; }
        }

        [Test]
        public void AGardenIsDugAndWeededWhileTheCropsGrowThenWhatItBearsIsGathered()
        {
            const float m2 = 200f;
            var hh = new Household { Garden = m2, Building = 1, Larder = 3.0 };
            Assert.Greater(Gardens.Behindhand(in hh, 5, 0.9), 0.0, "the digging is due in the sowing");
            Assert.AreEqual(0.0, Gardens.Work(ref hh, 10, 0.9, 10.0), "nothing gathered while it is dug");
            Gardens.Work(ref hh, 25, 0.9, 1000.0);
            Assert.AreEqual((Gardens.DigH + Gardens.TendH) * m2, hh.GardenWork, 1e-2, "dug and weeded as far as it wants, no further");
            Assert.LessOrEqual(Gardens.Behindhand(in hh, 29, 0.9), 1e-6, "nothing more wanted of it before the ripening");
            double bears = Gardens.Bears(in hh, 0.9);
            Assert.AreEqual(Gardens.FuPerM2 * m2 * 0.9, bears, 1e-4, "a garden kept well bears its area's worth in the season's growth");
            double fu = Gardens.Work(ref hh, 35, 0.9, Gardens.GatherH * m2 * 0.5);
            Assert.AreEqual(0.5 * bears, fu, 1e-4, "half the gathering, half of what it bears");
            Gardens.Work(ref hh, 45, 0.9, 1000.0);
            Assert.AreEqual(bears, hh.GardenFu, 1e-3, "all it bears, no more");
            Assert.AreEqual(3.0 + hh.GardenFu, hh.Larder, 1e-3, "into the larder");
            Assert.LessOrEqual(Gardens.Behindhand(in hh, 47, 0.9), 1e-4, "gathered: nothing more wanted");
            var half = new Household { Garden = m2, Building = 1 };
            Gardens.Work(ref half, 20, 0.9, 0.5 * (Gardens.DigH + Gardens.TendH) * m2);
            Assert.AreEqual(0.5 * bears, Gardens.Bears(in half, 0.9), 1e-3, "a garden half kept bears half");
            var none = new Household { Garden = m2, Building = 0 };
            Assert.AreEqual(0.0, Gardens.Behindhand(in none, 5, 0.9), "no home, no garden to work");
        }

        [Test]
        public void InTheSowingPeopleDigTheirGardensBehindTheirHouses()
        {
            var w = TestWorlds.Make(433);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Day);
            Housed(w, sid);
            ToLocalHour(w, sid, 7.5);
            // premise: the village's fields are sown (they come first - in the sowing's first days every free hand is in them)
            { ref var sv = ref w.Settlements.Get(sid); sv.CropWork = sv.CropWorkNeed; }
            double h0 = w.Metrics.GardenHours;
            int seen = 0, posed = 0;
            long end = w.Now + SimTime.Hours(10);
            while (w.Now < end)
            {
                w.AdvanceTo(w.Now + 600);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (!p.Alive || p.Act != ActivityKind.Garden || w.Now < p.TA) continue;
                    seen++;
                    if (Presence.Of(w, in p, w.Now, out _, out _, out byte pose, out _) && pose == Presence.Hoe) posed++;
                }
            }
            double worked = w.Metrics.GardenHours - h0;
            int dug = 0; var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).GardenWork > 0f) dug++;
            Console.WriteLine("garden hours " + worked.ToString("0.0") + ", households that dug " + dug + " of " + hl.Count + ", seen at it " + seen + " (hoeing " + posed + ")");
            Assert.Greater(worked, 0.0, "hours went into the gardens");
            Assert.Greater(dug, 0, "households dug their crofts");
            Assert.Greater(seen, 0, "seen in their gardens");
            Assert.AreEqual(seen, posed, "drawn at the spade and the hoe");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void CoarseDaysWorkTheGardensAsLivedDaysDo()
        {
            var w = TestWorlds.Make(435);
            int sid = w.Settlements.First();
            Housed(w, sid);
            Coarse(w);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hour);
            double h0 = w.Metrics.GardenHours;
            // premise: the village's fields are sown (they take the hours first), each day of the three
            for (int d = 0; d < 3; d++) { { ref var sv = ref w.Settlements.Get(sid); sv.CropWork = sv.CropWorkNeed; } w.AdvanceTo(w.Now + SimTime.Day); }
            double worked = w.Metrics.GardenHours - h0;
            var hl = w.HouseholdsOf(sid);
            double due = 0, done = 0;
            int doy = SimTime.DayOfYear(w.Now + TC2.Core.Planet.PlanetState.LocalOffset(w.Settlements.Get(sid).Anchor));
            for (int i = 0; i < hl.Count; i++) { var hh = w.Households.Get(hl[i]); due += Gardens.Behindhand(in hh, doy, 1.0) + hh.GardenWork; done += hh.GardenWork; }
            Console.WriteLine("COARSE garden hours " + worked.ToString("0.0") + "; done " + done.ToString("0.0") + " of " + due.ToString("0.0") + " due by day " + doy);
            Assert.Greater(worked, 0.0, "COARSE days give the gardens hours");
            Assert.GreaterOrEqual(done, 0.8 * due, "and keep up with what the season wants");
            Gardens.Off = true;
            try
            {
                double h1 = w.Metrics.GardenHours;
                w.AdvanceTo(w.Now + 2 * SimTime.Day);
                Assert.AreEqual(h1, w.Metrics.GardenHours, 1e-9, "--nogarden: no garden work");
            }
            finally { Gardens.Off = false; }
        }
    }
}
