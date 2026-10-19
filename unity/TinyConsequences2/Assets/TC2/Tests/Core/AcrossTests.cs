using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>Grain across water goes by boat (C9.5a): within a day's walk, grain moves at once only where a way on foot joins the two villages; across
    /// water it goes by sea in a boat somebody rows - relief, grain given back and the lord's tenth (in the tributary's boats, or the lord's come for
    /// it) - or not at all.</summary>
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
                        if (w.Routes.Get(w, s1, s2) != null || w.SeaRoutes.Get(w, s1, s2) == null) { a = b = 0; return null; }
                        // the world as a save of it holds it: ground sunk by hand, outside the plates' turn and its `LandChanged`, leaves the running
                        // world's waters drained for the old ground, and a save of it - its waters drained anew at load - would not continue it (the planet
                        // alone diverged within hours); loaded once, its waters are the sunk ground's in every copy of it
                        w = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
                        if (w.Routes.Get(w, s1, s2) != null || w.SeaRoutes.Get(w, s1, s2) == null) { a = b = 0; return null; }
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
            Assume.That(PlankBoat(w, a) != 0, "a plank boat of a's");
            long mark = w.History.LastId;
            w.Trade.Day(a);
            double sent = w.Settlements.Get(b).Granary - g0;
            Assert.Greater(sent, 0.0, "a boat's load over the sea");
            Assert.LessOrEqual(sent, Trade.BoatLoadKg / Trade.KgOf(Trade.Grain) + 1e-6, "no more than its boat carries");
            bool bySea = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.ReliefSent && w.History.At(i).ArgA == 1) bySea = true;
            Assert.IsTrue(bySea, "told as by sea");
            // --oldacross: at once, as before (the store empty again)
            w.Settlements.Get(b).Granary = 0;
            Trade.AcrossOff = true;
            try { w.Trade.Day(a); }
            finally { Trade.AcrossOff = false; }
            double atOnce = w.Settlements.Get(b).Granary;
            Console.WriteLine("across the water: by boat " + sent.ToString("0") + " FU; at once " + atOnce.ToString("0") + " FU (the old rule)");
            Assert.Greater(atOnce, 0.0, "--oldacross: at once, as before");
        }

        [Test]
        public void ATraderGoesAcrossWaterBySeaNotOnFoot()
        {
            var w = Across(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            // premise: a deal to be had between them - a's store full of grain and no hides, b's store bare with hides to spare
            ref var sa = ref w.Settlements.Get(a); sa.Granary = 100000; sa.Hides = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Granary = 0; sb.Sheaves = 0; sb.Hides = 5000;
            Assume.That(w.Trade.Plan(a, b, out _, out _, out _) > 0, "a deal between them");
            int old;
            Trade.AcrossOff = true;
            try { old = w.Trade.BestPartner(a, out _); }
            finally { Trade.AcrossOff = false; }
            Assume.That(old == b, "the old rule: its traders' best deal across the water");
            int now = w.Trade.BestPartner(a, out _);
            Console.WriteLine("a trader's partner: " + now + " (across the water " + b + "); by sea " + w.Trade.BestSeaPartner(a, out _, out _));
            Assert.AreNotEqual(b, now, "no trader walks across the water");
            if (now != 0) Assert.IsNotNull(w.Routes.Get(w, a, now), "a partner a way on foot reaches");
            Assert.AreNotEqual(0, w.Trade.BestSeaPartner(a, out _, out _), "by sea a boat's load can go");
        }

        /// <summary>Premise on `Across`: the weaker of the two pays the stronger a tribute (the lord must be the stronger at each harvest: C10.5).</summary>
        static World Bound(out int lord, out int trib)
        {
            var w = Across(out int a, out int b);
            lord = trib = 0;
            if (w == null) return null;
            if (w.Conflict.Might(a, b) >= w.Conflict.Might(b, a)) { lord = a; trib = b; } else { lord = b; trib = a; }
            ref var s = ref w.Settlements.Get(trib);
            s.Lord = lord; s.LordSince = w.Now;
            return w;
        }

        [Test]
        public void TheTenthAcrossWaterGoesInTheTributarysBoats()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Coarse(w);
            w.Planet.Storms.Clear();
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                // no boat on either side: nothing set aside - it stays in its store, as where no way by sea joins them
                Assume.That(w.Conflict.PlankBoats(a) == 0 && w.Conflict.PlankBoats(b) == 0, "no boats yet");
                double g0 = w.Settlements.Get(b).Granary;
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assert.AreEqual(0.0, w.Settlements.Get(b).TributeDue, 1e-9, "no boat: nothing set aside");
                Assert.AreEqual(g0 + 1000.0, w.Settlements.Get(b).Granary, 1e-6, "the harvest in its store");
                StringAssert.Contains("its people cannot carry the tenth there", Gazetteer.Neighbours(w, b), "the page tells why");
                // a plank boat of its own: the next tenth is set aside and rowed over in a calm sea, at once on a COARSE day
                Assume.That(PlankBoat(w, b) != 0, "a plank boat of the tributary's");
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assert.AreEqual(100.0, w.Settlements.Get(b).TributeDue, 1e-6, "a way by sea and a boat: the tenth set aside");
                StringAssert.Contains("goes over the sea in its own boats", Gazetteer.Neighbours(w, b), "the page tells how it goes");
                StringAssert.Contains("over the sea, in its boats", Gazetteer.Neighbours(w, a), "and the lord's");
                double lord0 = w.Settlements.Get(a).Granary, fu0 = w.Metrics.TributeFu;
                w.Conflict.Day(b);
                Console.WriteLine("by sea: " + Conflict.CarryTally[14] + " loads, " + Conflict.CarryTally[15].ToString("0.0") + " FU; fetched " + Conflict.CarryTally[17].ToString("0.0") + " FU");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0, "carried");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-6, "all of it by sea");
                Assert.AreEqual(0.0, Conflict.CarryTally[17], 1e-9, "in its own boat: the lord's did not come");
                Assert.AreEqual(lord0 + 100.0, w.Settlements.Get(a).Granary, 1e-6, "into the lord's store");
                Assert.AreEqual(fu0 + 100.0, w.Metrics.TributeFu, 1e-6, "the dues paid");
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

        [Test]
        public void TheLordsBoatsComeForTheTenthWhereItsTributaryHasNone()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Coarse(w);
            w.Planet.Storms.Clear();
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                Assume.That(w.Conflict.PlankBoats(a) == 0 && w.Conflict.PlankBoats(b) == 0, "no boats yet");
                Assume.That(PlankBoat(w, a) != 0, "a plank boat of the lord's");
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assert.AreEqual(100.0, w.Settlements.Get(b).TributeDue, 1e-6, "a way by sea and the lord's boat: the tenth set aside");
                StringAssert.Contains("'s boats come over the sea for its tenth", Gazetteer.Neighbours(w, b), "the page tells how it goes");
                StringAssert.Contains("boats from here go for its tenth", Gazetteer.Neighbours(w, a), "and the lord's");
                double lord0 = w.Settlements.Get(a).Granary, fu0 = w.Metrics.TributeFu;
                w.Conflict.Day(b);
                Console.WriteLine("fetched by the lord's boats: " + Conflict.CarryTally[16] + " loads, " + Conflict.CarryTally[17].ToString("0.0") + " FU (by sea " + Conflict.CarryTally[15].ToString("0.0") + " FU)");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0, "fetched");
                Assert.AreEqual(100.0, Conflict.CarryTally[17], 1e-6, "all of it in the lord's boats");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-6, "by sea");
                Assert.AreEqual(lord0 + 100.0, w.Settlements.Get(a).Granary, 1e-6, "into the lord's store");
                Assert.AreEqual(fu0 + 100.0, w.Metrics.TributeFu, 1e-6, "the dues paid");
                // --oldacross: nothing set aside across water, boats or none
                Trade.AcrossOff = true;
                try
                {
                    w.Conflict.Harvested(b, 1000);
                    Assert.AreEqual(0.0, w.Settlements.Get(b).TributeDue, 1e-9, "--oldacross: nothing set aside");
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

        /// <summary>The skipper of a village's boat out on a voyage with the given load (`Person.CargoTo`), or 0.</summary>
        static int Skipper(World w, int sid, byte cargoTo)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.Voyage != 0 && q.CargoTo == cargoTo) return q.Id; }
            return 0;
        }

        [Test]
        public void ALivedCrewRowsTheTenthOverToTheLordsLanding()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
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
                string rowing = null;
                while (w.Now < end && Conflict.CarryTally[15] < 99.0)
                {
                    w.Planet.Storms.Clear();
                    w.AdvanceTo(w.Now + SimTime.Minutes(30));
                    int sk = Skipper(w, b, Trade.ToLord);
                    if (rowing == null && sk != 0 && w.People.Get(sk).CargoQty > 0f) rowing = HistoryText.Doing(w, in w.People.Get(sk));
                }
                Console.WriteLine("lived, by sea: " + Conflict.CarryTally[14] + " loads, " + Conflict.CarryTally[15].ToString("0.0") + " FU; still set aside " + w.Settlements.Get(b).TributeDue.ToString("0.0"));
                Console.WriteLine("the skipper: " + rowing);
                Assert.Greater(Conflict.CarryTally[2], 0.0, "rowed by a lived crew");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-3, "the whole tenth delivered at the lord's landing");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0);
                Assert.IsNotNull(rowing, "seen with the load on the way");
                StringAssert.Contains("the tenth", rowing, "told as the tenth it pays");
                StringAssert.DoesNotContain("to trade", rowing, "not as a trade");
            }
            finally { Conflict.CarryTally = null; }
        }

        [Test]
        public void ALivedCrewOfTheLordsFetchesTheTenthHome()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(w.Conflict.PlankBoats(b) == 0, "no boat of the tributary's");
            Assume.That(PlankBoat(w, a) != 0, "a plank boat of the lord's");
            w.AdvanceTo(ToLocal(w, b, 1.0));
            w.Planet.Storms.Clear();
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                w.Settlements.Get(b).Granary += 1000;
                w.Conflict.Harvested(b, 1000);
                Assume.That(w.Settlements.Get(b).TributeDue > 99.0);
                long end = w.Now + SimTime.Days(4);
                string going = null, home = null;
                while (w.Now < end && Conflict.CarryTally[17] < 99.0)
                {
                    w.Planet.Storms.Clear();
                    w.AdvanceTo(w.Now + SimTime.Minutes(30));
                    int sk = Skipper(w, a, Trade.ToTenth);
                    if (going == null && sk != 0 && w.People.Get(sk).Step == StepKind.Travel) going = HistoryText.Doing(w, in w.People.Get(sk));
                    sk = Skipper(w, a, Trade.TenthAboard);
                    if (home == null && sk != 0 && w.People.Get(sk).Step == StepKind.Travel) home = HistoryText.Doing(w, in w.People.Get(sk));
                }
                Console.WriteLine("lived, fetched: " + Conflict.CarryTally[16] + " loads, " + Conflict.CarryTally[17].ToString("0.0") + " FU home (handed over by sea " + Conflict.CarryTally[15].ToString("0.0") +
                                  " FU); still set aside " + w.Settlements.Get(b).TributeDue.ToString("0.0"));
                Console.WriteLine("going: " + going);
                Console.WriteLine("home: " + home);
                Assert.Greater(Conflict.CarryTally[2], 0.0, "by a lived crew");
                Assert.AreEqual(100.0, Conflict.CarryTally[15], 1e-3, "the whole tenth handed over at the tributary's landing");
                Assert.AreEqual(100.0, Conflict.CarryTally[17], 1e-3, "and home in the lord's store");
                Assert.Less(w.Settlements.Get(b).TributeDue, 1.0);
                Assert.AreEqual(0, Skipper(w, a, Trade.TenthAboard), "nobody left holding it");
                Assert.IsNotNull(going, "seen rowing over for it");
                StringAssert.Contains("to fetch the tenth", going);
                Assert.IsNotNull(home, "seen rowing home with it");
                StringAssert.Contains("the tenth a tributary paid", home);
            }
            finally { Conflict.CarryTally = null; }
        }

        /// <summary>To the morning the lord's boat is out for the tenth (lived): the skipper, or 0 within three days.</summary>
        static int OutForTheTenth(World w, int lord, int trib)
        {
            w.AdvanceTo(ToLocal(w, trib, 1.0));
            w.Planet.Storms.Clear();
            w.Settlements.Get(trib).Granary += 1000;
            w.Conflict.Harvested(trib, 1000);
            long end = w.Now + SimTime.Days(3);
            while (w.Now < end)
            {
                w.Planet.Storms.Clear();
                w.AdvanceTo(w.Now + SimTime.Minutes(20));
                int sk = Skipper(w, lord, Trade.ToTenth);
                if (sk != 0 && w.People.Get(sk).Step == StepKind.Travel) return sk;
            }
            return 0;
        }

        [Test]
        public void ASaveTakenWhileTheLordsBoatIsOutForTheTenthContinuesIdentically()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(w.Conflict.PlankBoats(b) == 0 && PlankBoat(w, a) != 0, "the lord's boat only");
            Assume.That(OutForTheTenth(w, a, b) != 0, "the lord's boat on its way");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Days(2);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }

        [Test]
        public void ABondEndedWhileTheLordsBoatIsOutBringsItHomeEmpty()
        {
            var w = Bound(out int a, out int b);
            Assume.That(w != null, "two villages within a day's walk, water between them, a way by sea");
            Assume.That(w.Conflict.PlankBoats(b) == 0 && PlankBoat(w, a) != 0, "the lord's boat only");
            Conflict.CarryTally = new double[Conflict.CarryTallyN];
            try
            {
                int sk = OutForTheTenth(w, a, b);
                Assume.That(sk != 0, "the lord's boat on its way");
                // the bond ends as a harvest ends it (the lord no longer the stronger): what was set aside is the village's again
                ref var s = ref w.Settlements.Get(b);
                double back = s.TributeDue;
                s.Lord = 0; s.LordEvent = 0; s.Granary += s.TributeDue; s.TributeDue = 0; s.CarryToday = 0;
                Assume.That(back > 99.0, "the tenth set aside when the bond ended");
                double fu0 = w.Metrics.TributeFu;
                long end = w.Now + SimTime.Days(2);
                while (w.Now < end && w.People.Get(sk).Voyage != 0) { w.Planet.Storms.Clear(); w.AdvanceTo(w.Now + SimTime.Minutes(30)); }
                Console.WriteLine("the bond ended with " + back.ToString("0.0") + " FU set aside; the skipper: " + HistoryText.Doing(w, in w.People.Get(sk)));
                Assert.AreEqual(0, w.People.Get(sk).Voyage, "home again");
                Assert.AreEqual(0.0, Conflict.CarryTally[15], 1e-9, "nothing handed over");
                Assert.AreEqual(0.0, Conflict.CarryTally[17], 1e-9, "nothing brought home");
                Assert.AreEqual(fu0, w.Metrics.TributeFu, 1e-9, "no dues paid");
                Assert.AreEqual(0, Skipper(w, a, Trade.TenthAboard), "nobody holding grain");
                Assert.AreEqual(Trade.None, w.People.Get(sk).Cargo, "the boat home empty");
                Assert.AreEqual(0.0, w.Settlements.Get(b).TributeDue, 1e-9, "nothing set aside without a lord");
            }
            finally { Conflict.CarryTally = null; }
        }
    }
}
