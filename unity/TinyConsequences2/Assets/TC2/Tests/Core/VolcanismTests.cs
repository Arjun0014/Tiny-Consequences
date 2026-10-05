using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>Volcanoes and the carbon cycle (05 §3b, D2-028): mechanisms, consequences with causes, continuity.</summary>
    [TestFixture]
    public class VolcanismTests
    {
        static long StepS => DMath.ToLong(Tectonics.StepYears * SimTime.Year);

        [Test]
        public void APlumeUnderAMovingPlateLeavesAChainOfVolcanoes()
        {
            var w = TestWorlds.Make(51);
            var P = w.Planet;
            w.Tect.Drift = false;
            // the plume under the middle of the biggest plate, which is made to move briskly
            int big = 0; var count = new int[P.Plates.Length];
            for (int c = 0; c < P.Cells; c++) count[P.Plate[c]]++;
            for (int i = 1; i < count.Length; i++) if (count[i] > count[big]) big = i;
            Vec3d mid = Vec3d.Zero;
            for (int c = 0; c < P.Cells; c++) if (P.Plate[c] == big) mid = mid + P.Grid.Center[c];
            mid = mid.Normalized;
            var pole = Vec3d.Cross(mid, new Vec3d(0.31, 0.92, 0.24)).Normalized; // the plume sits on the plate's equator
            P.Plates[big] = new PlateInfo { Pole = pole, Omega = 4.0 / P.Radius, Continental = P.Plates[big].Continental };
            P.Hotspots = new[] { new Hotspot { At = mid, Rate = 10.0 } };
            for (int v = P.Vents.Count - 1; v >= 0; v--) if (P.Vents[v].Kind == 1) P.Vents.RemoveAt(v);
            for (int i = 0; i < 70; i++) { w.Now += StepS; w.TectonicStep(); }

            var chain = new List<Vent>();
            foreach (var v in P.Vents) if (v.Kind == 1) chain.Add(v);
            Assert.GreaterOrEqual(chain.Count, 2, "the plume opened new vents as the plate carried the old ones away");
            Vent oldest = chain[0];
            foreach (var v in chain) if (v.Born < oldest.Born) oldest = v;
            double away = Vec3d.ArcDistance(oldest.At, mid, P.Radius);
            Assert.Greater(away, 0.8 * Volcanism.ReachCells * P.CellSize, "the oldest vent rode away from the plume");
            // ...in the direction the plate moves at the plume
            Vec3d motion = Tectonics.Velocity(P, big, mid).Normalized;
            Vec3d toOld = (oldest.At - mid).Normalized;
            Assert.Greater(Vec3d.Dot(toOld, motion), 0.8, "the chain lies along the plate's motion");
        }

        [Test]
        public void ArcVolcanoesStandWhereOceanCrustDivesUnderAPlate()
        {
            var w = TestWorlds.Make(52);
            var P = w.Planet;
            int arcs = 0;
            double reach = (Volcanism.ReachCells + 1.5) * P.Grid.MeanSpacing;
            foreach (var v in P.Vents)
            {
                if (v.Kind != 0) continue;
                arcs++;
                bool fed = false;
                for (int c = 0; c < P.Cells && !fed; c++)
                    if (w.Tect.ArcSupply[c] > 0 && Vec3d.Angle(P.Grid.Center[c], v.At) < reach) fed = true;
                Assert.IsTrue(fed, "vent " + v.Id + " has a boundary feeding it");
            }
            Assert.Greater(arcs, 3, "a planet with moving plates has arc volcanoes from the start");
            Assert.Greater(P.SupplyRef, 0, "and their supply is the carbon cycle's reference");
        }

        [Test]
        public void AnEruptionsConsequencesAllTraceToIt()
        {
            var w = TestWorlds.Make(53);
            var P = w.Planet;
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Crop = CropStage.Growing;
            int cell = s.Cell;
            // a plugged vent under the village: a VEI 5 eruption
            double pressure = 1.2 * Volcanism.PressureCrit * DMath.Exp(3.05 / 3.1 * DMath.Log(10.0));
            int id = w.Ids.Next(EntityKind.Place);
            w.History.Register(new EntityRef(EntityKind.Place, id), "Mount Test", w.Now);
            P.Vents.Add(new Vent { Id = id, Cell = cell, At = P.Grid.Center[cell], Plate = P.Plate[cell], Pressure = pressure, Seal = 8f, Born = w.Now });
            Assert.AreEqual(5, Volcanism.Vei(pressure));
            int alive = w.LivingPeople();
            double aer = P.Aerosol;
            long before = w.History.LastId;
            w.Erupt(id);

            long eruption = 0;
            int ash = 0, deaths = 0, fires = 0;
            for (int i = w.History.FirstIndexAfter(before); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.Eruption) { eruption = e.Id; Assert.AreEqual(5f, e.Magnitude, "VEI"); Assert.AreEqual(Tier.Historical, e.Tier); continue; }
                Assert.AreNotEqual(0, eruption, "the eruption is recorded first");
                Assert.AreEqual(eruption, e.Root, e.Kind + " traces to the eruption");
                if (e.Kind == HistoryKind.AshFall) { ash++; Assert.AreEqual(eruption, e.Cause1); }
                if (e.Kind == HistoryKind.PersonDied) { deaths++; Assert.AreEqual(4, e.ArgA, "died in the eruption"); }
                if (e.Kind == HistoryKind.FireStarted) fires++;
            }
            Assert.Greater(ash, 0, "ash fell on the village's standing crop");
            Assert.Greater(deaths, 0, "people at the vent died");
            Assert.Less(w.LivingPeople(), alive);
            Assert.Greater(P.Aerosol, aer + 0.02, "a veil in the sky");
            int v = Volcanism.IndexOf(P, id);
            Assert.AreEqual(1, P.Vents[v].Eruptions);
            Assert.AreEqual(0.0, P.Vents[v].Pressure);
            Assert.Greater(P.Vents[v].Cone, 0f, "a cone waits for the next tectonic step");
            float elev = P.Elev[cell];
            w.TectonicStep();
            Assert.Greater(P.Elev[cell], elev - 50f, "the cone joined the relief (minus the plates' own work)");
            Assert.AreEqual(0f, P.Vents[Volcanism.IndexOf(P, id)].Cone);
        }

        [Test]
        public void CarbonRelaxesTowardTheBalanceOfVolcanoesAndWeathering()
        {
            var w = TestWorlds.Make(54);
            var P = w.Planet;
            P.TGlobalRef = P.TGlobal; // the temperature held: the cycle alone
            P.CO2 = 560;
            double last = P.CO2;
            for (int y = 0; y < 4000; y++)
            {
                Climate.Carbon(P, 1.0);
                Assert.LessOrEqual(P.CO2, last + 1e-9, "falls steadily");
                last = P.CO2;
            }
            Assert.That(P.CO2, Is.InRange(290.0, 360.0), "most of the excess weathered away in 4,000 years (~2,000-year memory)");
            // twice the volcanic supply: a richer air that rock weathers as fast as it comes
            for (int v = 0; v < P.Vents.Count; v++) { var vt = P.Vents[v]; vt.Supply *= 2f; P.Vents[v] = vt; }
            P.CO2 = 280;
            for (int y = 0; y < 6000; y++) Climate.Carbon(P, 1.0);
            Assert.That(P.CO2, Is.InRange(450.0, 560.0), "more volcanoes, more CO2 at balance");
        }

        [Test]
        public void SaveLoadThroughEruptionsContinuesIdentically()
        {
            var a = TestWorlds.Make(55);
            a.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(a.Now) + SimTime.Day);
            var P = a.Planet;
            for (int v = 0; v < P.Vents.Count; v++) { var vt = P.Vents[v]; vt.Pressure = 3.0 * Volcanism.PressureCrit * vt.Seal; P.Vents[v] = vt; }
            var b0 = Budget.Unlimited;
            a.AdvanceTo(a.Now + 2 * SimTime.Year, ref b0);
            long erupted = 0; foreach (long n in a.Metrics.Eruptions) erupted += n;
            Assert.Greater(erupted, 0, "eruptions before the save");
            int pending = 0; foreach (var v in P.Vents) if (v.Next > a.Now) pending++;
            Assert.Greater(pending, 0, "and eruptions still to come");
            var b = SaveFile.Load(SaveFile.Save(a, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "loaded");
            var b1 = Budget.Unlimited; var b2 = Budget.Unlimited;
            a.AdvanceTo(a.Now + 12 * SimTime.Year, ref b1);
            b.AdvanceTo(b.Now + 12 * SimTime.Year, ref b2);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "the same world through eruptions and a tectonic step");
        }
    }
}
