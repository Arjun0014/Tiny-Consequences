using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Plate mechanisms (05 §3b, D2-025).</summary>
    [TestFixture]
    public class TectonicsTests
    {
        static int Continental(PlanetState P) { int n = 0; for (int c = 0; c < P.Cells; c++) if (P.Crust[c] == 1) n++; return n; }

        [Test]
        public void ARigidlyTurningCrustKeepsItsContinents()
        {
            // one motion for every plate: no boundary is active, so only resampling could make or lose crust
            var w = TestWorlds.Make(41);
            var P = w.Planet;
            var pole = new Vec3d(0.3, 0.8, 0.5).Normalized;
            w.Tect.Drift = false;
            for (int i = 0; i < P.Plates.Length; i++) P.Plates[i] = new PlateInfo { Pole = pole, Omega = 1.2 / P.Radius, Continental = P.Plates[i].Continental };
            int before = Continental(P);
            double sum0 = 0; for (int c = 0; c < P.Cells; c++) sum0 += P.Elev[c];
            long stepS = DMath.ToLong(Tectonics.StepYears * SimTime.Year);
            for (int i = 0; i < 200; i++) w.Tect.Step(P, Tectonics.StepYears, w.Seed, i * stepS); // the game's cadence: cells change hands every 16 steps
            int after = Continental(P);
            var T = w.Tect;
            Assert.AreEqual(1.0, after / (double)before, 0.03, "continental area kept by rigid motion (2,000 years, ~2.4 km): coll " + T.Collided + " subd " + T.Subducted + " ridges " + T.Ridges + " fills " + T.Fills + " lostFill " + T.ContLostToFill + " gainFill " + T.ContGainedByFill + " dropC " + T.DropC + " dropO " + T.DropO + " accr " + T.Accreted + " before " + before + " after " + after);
        }

        [Test]
        public void PlatesCarryTheirCrustAtTheirSpeed()
        {
            var w = TestWorlds.Make(42);
            var P = w.Planet;
            // follow a sample from the middle of the fastest plate (far from where it could be overridden)
            int fast = 0;
            for (int i = 1; i < P.Plates.Length; i++) if (System.Math.Abs(P.Plates[i].Omega) > System.Math.Abs(P.Plates[fast].Omega)) fast = i;
            Vec3d mid = Vec3d.Zero;
            for (int c = 0; c < P.Cells; c++) if (P.Plate[c] == fast) mid = mid + P.Grid.Center[c];
            mid = mid.Normalized;
            int peak = -1; double bestDot = -2;
            for (int c = 0; c < P.Cells; c++) if (P.Plate[c] == fast && Vec3d.Dot(P.Grid.Center[c], mid) > bestDot) { bestDot = Vec3d.Dot(P.Grid.Center[c], mid); peak = c; }
            Vec3d start = w.Tect.SamplePos(P, peak);
            w.Tect.Drift = false;
            var pl = P.Plates[fast];
            double years = 1000;
            long stepS = DMath.ToLong(Tectonics.StepYears * SimTime.Year);
            for (int i = 0; i < 100; i++) w.Tect.Step(P, Tectonics.StepYears, w.Seed, i * stepS);
            double a = pl.Omega * years;
            Vec3d expect = Tectonics.Rotate(start, pl.Pole, DMath.Cos(a), DMath.Sin(a));
            int expectCell = P.Grid.CellOf(expect);
            Assert.AreEqual(fast, P.Plate[expectCell], "the plate is where its motion carried it");
            double moved = Vec3d.ArcDistance(start, expect, P.Radius);
            Assert.That(moved, Is.InRange(300.0, 3000.0), "~0.5-1.5 m a year");
        }

        [Test]
        public void NewSeaFloorFormsOnlyWherePlatesMeet()
        {
            var w = TestWorlds.Make(43);
            var P = w.Planet;
            long stepS = DMath.ToLong(Tectonics.StepYears * SimTime.Year);
            for (int i = 0; i <= 48; i++) w.Tect.Step(P, Tectonics.StepYears, w.Seed, i * stepS); // ends on a hand-over
            int young = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.Crust[c] != 0 || P.CrustAge[c] > Tectonics.StepYears * 0.5) continue;
                young++;
                bool boundary = false;
                for (int q = 0; q < 8; q++) if (P.Plate[P.Grid.Nbr8[c * 8 + q]] != P.Plate[c]) { boundary = true; break; }
                for (int q = 0; q < 8 && !boundary; q++)
                {
                    int nb = P.Grid.Nbr8[c * 8 + q];
                    if (P.Crust[nb] == 0 && P.CrustAge[nb] < 200) boundary = true; // the ridge's own young floor
                }
                Assert.IsTrue(boundary, "new crust at " + c + " is at a ridge");
            }
            Assert.Greater(young, 0, "plates that part make new sea floor");
        }

        [Test]
        public void VillagesRideTheirPlates()
        {
            var w = TestWorlds.Make(44);
            int sid = w.Settlements.First();
            var s0 = w.Settlements.Get(sid);
            var pl = w.Planet.Plates[w.Planet.Plate[s0.Cell]];
            w.TectonicStep();
            var s1 = w.Settlements.Get(sid);
            double a = pl.Omega * Tectonics.StepYears;
            Vec3d expect = Tectonics.Rotate(s0.Anchor, pl.Pole, DMath.Cos(a), DMath.Sin(a));
            Assert.Less(Vec3d.ArcDistance(expect, s1.Anchor, w.Planet.Radius), 1e-6, "the village moved with its ground");
            Assert.AreEqual(sid, w.SettlementAtCell(s1.Cell), "and is found where it now stands");
            Assert.IsNull(TestWorlds.Audit(w));
        }
    }
}
