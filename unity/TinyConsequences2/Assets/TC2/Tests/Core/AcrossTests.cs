using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>Grain across water goes by boat (C9.5a): within a day's walk, grain moves at once only where a way on foot joins the two villages; across
    /// water it goes by sea in a boat somebody rows - relief, grain given back and the lord's tenth - or not at all.</summary>
    [TestFixture]
    public class AcrossTests
    {
        /// <summary>
        /// Premise: two villages within a day's walk that keep landings, a way by sea between them - and the way on foot between them cut by water
        /// sunk all round the second's ground (after the made world's first ticks: the plates' first step lays the ground anew).
        /// </summary>
        static World Across(out int a, out int b, ulong from = 701)
        {
            for (ulong seed = from; seed < from + 60; seed++)
            {
                var w = TestWorlds.Make(seed);
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int s1 = w.Settlements.First(); s1 != 0; s1 = w.Settlements.Next(s1))
                {
                    if (!w.Settlements.Get(s1).Inhabited || !Boats.EnsureLanding(w, s1)) continue;
                    for (int s2 = w.Settlements.First(); s2 != 0; s2 = w.Settlements.Next(s2))
                    {
                        if (s2 == s1 || !w.Settlements.Get(s2).Inhabited) continue;
                        double d = Vec3d.ArcDistance(w.Settlements.Get(s1).Anchor, w.Settlements.Get(s2).Anchor, w.Planet.Radius);
                        if (d > 6000 || !Boats.EnsureLanding(w, s2) || w.SeaRoutes.Get(w, s1, s2) == null) continue;
                        var P = w.Planet;
                        int c = w.Settlements.Get(s2).Cell, c1 = w.Settlements.Get(s1).Cell;
                        bool clear = true;
                        for (int q = 0; q < 8; q++) if (P.Grid.Nbr8[c * 8 + q] == c1) clear = false;
                        if (!clear) continue;
                        for (int q = 0; q < 8; q++) { int nb = P.Grid.Nbr8[c * 8 + q]; if (P.IsLand(nb)) P.Elev[nb] = (float)(P.SeaLevel - 20.0); }
                        P.CoastVersion++;   // (the ways by sea are found again)
                        w.Routes.Invalidate();
                        if (w.Routes.Get(w, s1, s2) != null || w.SeaRoutes.Get(w, s1, s2) == null) return null;
                        a = s1; b = s2;
                        return w;
                    }
                }
            }
            a = b = 0;
            return null;
        }

        static int PlankBoat(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                if (w.Households.Get(hl[i]).Boat != 0 || !w.Trade.Crewed(hl[i], sid)) continue;
                ref var s = ref w.Settlements.Get(sid);
                s.TechKnown |= (byte)Boats.Bit; s.Woodstore += Boats.PlankWood;
                Boats.Begin(w, sid, hl[i], Structures.PlankBoat);
                Boats.Work(w, sid, hl[i], 2000.0, 1.0, 0);
                if (Boats.Of(w, hl[i]) != 0) return Boats.Of(w, hl[i]);
            }
            return 0;
        }

        static void Coarse(World w)
        {
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + 1);
        }

        static void Famine(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.Granary = 0; s.Sheaves = 0;
            s.FamineAt = w.Now;
            s.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), RootKind = RootKind.Nature });
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (o != sid) w.Settlements.Get(o).FamineEvent = 0;
        }

        [Test]
        public void ReliefAcrossWaterGoesByBoatOrNotAtAll()
        {
            var w = Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Coarse(w);
            w.Planet.Storms.Clear();   // (premise: a calm sea)
            Famine(w, b);
            w.Settlements.Get(a).Granary = 100000;
            double g0 = w.Settlements.Get(b).Granary;
            w.Trade.Day(a);
            Assert.AreEqual(g0, w.Settlements.Get(b).Granary, 1e-6, "no boat: no grain across the water");
            Trade.AcrossOff = true;
            try { w.Trade.Day(a); }
            finally { Trade.AcrossOff = false; }
            double atOnce = w.Settlements.Get(b).Granary - g0;
            Assert.Greater(atOnce, 0.0, "--oldacross: at once, as before");
            w.Settlements.Get(b).Granary = 0;
            Assume.That(PlankBoat(w, a) != 0, "a plank boat of a's");
            long mark = w.History.LastId;
            w.Trade.Day(a);
            double sent = w.Settlements.Get(b).Granary;
            Console.WriteLine("across the water: at once " + atOnce.ToString("0") + " FU (the old rule); by boat " + sent.ToString("0") + " FU");
            Assert.Greater(sent, 0.0, "a boat's load over the sea");
            Assert.LessOrEqual(sent, Trade.BoatLoadKg / Trade.KgOf(Trade.Grain) + 1e-6, "no more than its boat carries");
            bool bySea = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.ReliefSent && w.History.At(i).ArgA == 1) bySea = true;
            Assert.IsTrue(bySea, "told as by sea");
        }

        /// <summary>Premise on `Across`: b pays a tribute (a the lord, the stronger).</summary>
        static void Bound(World w, int lord, int trib)
        {
            ref var s = ref w.Settlements.Get(trib);
            s.Lord = lord; s.LordSince = w.Now;
        }

        [Test]
        public void TheTenthAcrossWaterGoesInTheTributarysBoats()
        {
            var w = Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(w.Conflict.Might(a, b) >= w.Conflict.Might(b, a), "the lord the stronger");
            Bound(w, a, b);
            Coarse(w);
            w.Planet.Storms.Clear();
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assert.AreEqual(100.0, w.Settlements.Get(b).TributeDue, 1e-6, "a way by sea: the tenth set aside");
                // no boat: it waits
                double lord0 = w.Settlements.Get(a).Granary;
                w.AdvanceTo(w.Now + SimTime.Days(3));
                Assert.AreEqual(100.0, w.Settlements.Get(b).TributeDue, 1e-6, "no boat of its own: the tenth waits");
                StringAssert.Contains("goes over the sea in its own boats", Gazetteer.Neighbours(w, b), "the page tells how it goes");
                // a plank boat: rowed over in a calm sea
                Assume.That(PlankBoat(w, b) != 0, "a plank boat of the tributary's");
                long t0 = w.Now;
                while (w.Settlements.Get(b).TributeDue >= 1.0 && w.Now - t0 < SimTime.Days(10)) w.AdvanceTo(w.Now + SimTime.Day);
                Console.WriteLine("by sea: " + Conflict.CarryTally[14] + " loads, " + Conflict.CarryTally[15].ToString("0.0") + " FU in " + ((w.Now - t0) / (double)SimTime.Day).ToString("0") + " days");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0, "carried");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-6, "all of it by sea");
                Assert.GreaterOrEqual(w.Settlements.Get(a).Granary, lord0 + 100.0 - 1e-6, "into the lord's store");
                // --oldacross: across water it stays at home (C10.6 as landed)
                Trade.AcrossOff = true;
                try
                {
                    double g = w.Settlements.Get(b).Granary;
                    w.Settlements.Get(b).Granary += 1000;
                    w.Conflict.Harvested(b, 1000);
                    Assert.AreEqual(0.0, w.Settlements.Get(b).TributeDue, 1e-9, "--oldacross: nothing set aside across water");
                    Assert.AreEqual(g + 1000.0, w.Settlements.Get(b).Granary, 1e-6);
                }
                finally { Trade.AcrossOff = false; }
            }
            finally { Conflict.CarryTally = null; }
        }

        static long ToLocal(World w, int sid, double hour)
        {
            long lsec = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long d = SimTime.FloorMod(SimTime.Hours(hour) - lsec, SimTime.Day);
            return w.Now + (d == 0 ? SimTime.Day : d);
        }

        [Test]
        public void ALivedCrewRowsTheTenthOverToTheLordsLanding()
        {
            var w = Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(w.Conflict.Might(a, b) >= w.Conflict.Might(b, a), "the lord the stronger");
            Bound(w, a, b);
            Assume.That(PlankBoat(w, b) != 0, "a plank boat of the tributary's");
            w.AdvanceTo(ToLocal(w, b, 1.0));
            w.Planet.Storms.Clear();
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assume.That(w.Settlements.Get(b).TributeDue > 99.0);
                long end = w.Now + SimTime.Days(4);
                while (w.Now < end && Conflict.CarryTally[15] < 99.0) { w.Planet.Storms.Clear(); w.AdvanceTo(w.Now + SimTime.Hours(1)); }
                Console.WriteLine("lived, by sea: " + Conflict.CarryTally[14] + " loads, " + Conflict.CarryTally[15].ToString("0.0") + " FU; still set aside " + w.Settlements.Get(b).TributeDue.ToString("0.0"));
                Assert.Greater(Conflict.CarryTally[2], 0.0, "rowed by a lived crew");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-3, "the whole tenth delivered at the lord's landing");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0);
            }
            finally { Conflict.CarryTally = null; }
        }
    }
}
