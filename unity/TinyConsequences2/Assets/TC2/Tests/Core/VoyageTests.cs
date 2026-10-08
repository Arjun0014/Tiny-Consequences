using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Voyages (C9.3, D2-113): ways over the sea between villages' landings; a plank boat's load in a deal; grain sent by sea to a famine
    /// beyond a day's walk, a boatload a day for each plank boat, in a calm sea. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class VoyageTests
    {
        /// <summary>A world with two villages that keep landings, 6 km to SeaRoutes.Most apart, with a way over the sea between them.</summary>
        static World Pair(out int a, out int b, ulong from = 701)
        {
            for (ulong seed = from; seed < from + 60; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int s1 = w.Settlements.First(); s1 != 0; s1 = w.Settlements.Next(s1))
                {
                    if (!Boats.EnsureLanding(w, s1)) continue;
                    for (int s2 = w.Settlements.Next(s1); s2 != 0; s2 = w.Settlements.Next(s2))
                    {
                        double d = Vec3d.ArcDistance(w.Settlements.Get(s1).Anchor, w.Settlements.Get(s2).Anchor, w.Planet.Radius);
                        if (d <= 6000 || d > SeaRoutes.Most || !Boats.EnsureLanding(w, s2)) continue;
                        if (w.SeaRoutes.Get(w, s1, s2) == null) continue;
                        a = s1; b = s2;
                        return w;
                    }
                }
            }
            a = b = 0;
            return null;
        }

        [Test]
        public void TheSeaHasWaysBetweenLandingsThatKeepToTheWater()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages with landings, beyond a day's walk, the sea between them");
            var P = w.Planet;
            var r = w.SeaRoutes.Get(w, a, b);
            Assert.AreSame(r, w.SeaRoutes.Get(w, b, a), "one way for the pair (cached)");
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            double straight = Vec3d.ArcDistance(sa.LocalToSphere(sa.LandX, sa.LandY, P.Radius), sb.LocalToSphere(sb.LandX, sb.LandY, P.Radius), P.Radius);
            Console.WriteLine("the way by sea " + (r.Length / 1000).ToString("0.0") + " km between landings " + (straight / 1000).ToString("0.0") + " km apart, " + r.Pts.Length + " points");
            Assert.GreaterOrEqual(r.Length, straight - 1.0, "no shorter than the straight line");
            Assert.LessOrEqual(r.Length, SeaRoutes.Most);
            for (int i = 1; i < r.Pts.Length - 1; i++) Assert.IsFalse(P.IsLand(P.Grid.CellOf(r.Pts[i])), "point " + i + " over the sea");
            P.CoastVersion++;   // (the coast moved: ways are found again)
            Assert.AreNotSame(r, w.SeaRoutes.Get(w, a, b));
        }

        [Test]
        public void ABoatsLoadIsTwentyWalkersLoads()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages");
            // premise: one has iron to spare and wants hides, the other the reverse
            ref var sa = ref w.Settlements.Get(a); sa.Iron = 4000 + TC2.Core.Knowledge.Tech.IronPerHead * sa.Population; sa.Hides = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Iron = 0; sb.Hides = 2000 + sb.Population;
            double walk = w.Trade.Plan(a, b, out byte g1, out double q1, out _);
            double boat = w.Trade.Plan(a, b, out byte g2, out double q2, out _, Trade.BoatLoadKg);
            Console.WriteLine("a walker's load: " + q1.ToString("0") + " of good " + g1 + ", gain " + walk.ToString("0") + "; a boat's: " + q2.ToString("0") + " of good " + g2 + ", gain " + boat.ToString("0"));
            Assert.Greater(walk, 0.0); Assert.AreEqual(g1, g2);
            Assert.AreEqual(Trade.BoatLoadKg / Trade.LoadKg, q2 / q1, 0.01, "twenty walkers' loads where both spare enough");
            Assert.Greater(boat, Trade.WorthAVoyage);
        }

        [Test]
        public void GrainGoesBySeaToAFamineBeyondADaysWalkInACalmSea()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages");
            var P = w.Planet;
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + 1);   // (COARSE: a village's day gives at once; lived, it books a boat - below)
            P.Storms.Clear();   // (premise: a calm sea)
            // premise: a has grain to spare; b is in famine with an empty store
            ref var sa = ref w.Settlements.Get(a); sa.Granary = 100000;
            ref var sb = ref w.Settlements.Get(b); sb.Granary = 0; sb.FamineAt = w.Now; sb.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = sb.Cell, Settlement = b, Subject = EntityRef.Settlement(b), RootKind = RootKind.Nature });
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (o != a) { ref var so = ref w.Settlements.Get(o); if (o != b) so.FamineEvent = 0; }
            // without a plank boat, a famine beyond a day's walk gets nothing from a
            double g0 = w.Settlements.Get(b).Granary;
            w.Trade.Day(a);
            Assert.AreEqual(g0, w.Settlements.Get(b).Granary, 1e-6, "no boat: no relief beyond a day's walk");
            // a plank boat of a's
            var hl = w.HouseholdsOf(a);
            int hid = 0;
            for (int i = 0; i < hl.Count && hid == 0; i++) if (w.Trade.Crewed(hl[i], a)) hid = hl[i];   // (a household with two who can row)
            Assume.That(hid != 0, "a household that can crew a boat");
            ref var sa2 = ref w.Settlements.Get(a);
            sa2.TechKnown |= (byte)Boats.Bit; sa2.Woodstore += Boats.PlankWood;
            int bid = Boats.Begin(w, a, hid, Structures.PlankBoat);
            Boats.Work(w, a, hid, 2000.0, 1.0, 0);   // (a learner's pace: no builder named)
            Assume.That(Boats.Of(w, hid) == bid, "a's plank boat made");
            long mark = w.History.LastId; double sea0 = w.Metrics.ReliefBySeaFu;
            w.Trade.Day(a);
            double sent = w.Settlements.Get(b).Granary - g0;
            Console.WriteLine("by sea: " + sent.ToString("0") + " FU (a boatload is " + (Trade.BoatLoadKg / 0.75).ToString("0") + ")");
            Assert.Greater(sent, 0.0, "a boatload of grain along the coast");
            Assert.LessOrEqual(sent, Trade.BoatLoadKg / 0.75 + 1e-6, "no more than its boat carries");
            Assert.AreEqual(sent, w.Metrics.ReliefBySeaFu - sea0, 1e-6);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.ReliefSent) { told = true; Assert.AreEqual(1, w.History.At(i).ArgA, "told as by sea"); Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // a storm over the way: they wait for a calm sea
            var r = w.SeaRoutes.Get(w, a, b);
            P.Storms.Add(new Storm { Id = w.Ids.Next(EntityKind.Process), At = r.PointAt(0.5 * r.Length), Heading = Vec3d.UnitX, Speed = 0f, Wind = 30f, Radius = 3000f, Peak = 30f, Born = w.Now, Kind = 1 });
            double g1 = w.Settlements.Get(b).Granary;
            w.Trade.Day(a);
            Assert.AreEqual(g1, w.Settlements.Get(b).Granary, 1e-6, "no boat puts out in a storm");
        }

        // ------------------------------------------------------------------ lived voyages
        /// <summary>Premise: a plank boat for each household of the village with two who could take an oar (returns how many).</summary>
        static int PlankBoats(World w, int sid, int most = 99)
        {
            int n = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count && n < most; i++)
            {
                if (w.Households.Get(hl[i]).Boat != 0) continue;
                if (!w.Trade.Crewed(hl[i], sid)) continue;   // (two who can take an oar)
                ref var s = ref w.Settlements.Get(sid);
                s.TechKnown |= (byte)Boats.Bit; s.Woodstore += Boats.PlankWood;
                Boats.Begin(w, sid, hl[i], Structures.PlankBoat);
                Boats.Work(w, sid, hl[i], 2000.0, 1.0, 0);
                if (Boats.Of(w, hl[i]) != 0) n++;
            }
            return n;
        }

        /// <summary>Premise: a has iron to spare and wants hides, b the reverse - a deal worth a boat's load.</summary>
        static void Deal(World w, int a, int b)
        {
            ref var sa = ref w.Settlements.Get(a); sa.Iron = 4000 + TC2.Core.Knowledge.Tech.IronPerHead * sa.Population; sa.Hides = 0;
            ref var sb = ref w.Settlements.Get(b); sb.Iron = 0; sb.Hides = 2000 + sb.Population;
        }

        static long ToLocal(World w, int sid, double hour)
        {
            long lsec = SimTime.FloorMod(w.Now + w.LocalOffsetOf(sid), SimTime.Day);
            long d = SimTime.FloorMod(SimTime.Hours(hour) - lsec, SimTime.Day);
            return w.Now + (d == 0 ? SimTime.Day : d);
        }

        /// <summary>
        /// Lived (C9.3): a voyage the village booked is sailed by the household in the morning - its crew walk down to the landing, row the way
        /// over the sea together in the one boat, deal at the other landing (the iron for hides), stay as visitors and row home with the return load.
        /// </summary>
        [Test]
        public void ALivedVoyageIsRowedOverTheSeaByItsCrewAndComesHomeWithTheReturnLoad()
        {
            var w = Pair(out int a, out int b);
            Assume.That(w != null, "two villages with a way over the sea");
            var P = w.Planet;
            Assume.That(PlankBoats(w, a, 1) == 1, "a plank boat at a");
            w.AdvanceTo(ToLocal(w, a, 1.0));   // (after a's day: what is booked now is sailed this morning)
            P.Storms.Clear();
            Deal(w, a, b);
            int hid = 0, bid = 0;
            var hl = w.HouseholdsOf(a);
            for (int i = 0; i < hl.Count && bid == 0; i++) { bid = Boats.Of(w, hl[i]); hid = bid != 0 ? hl[i] : 0; }
            w.Trade.Book(bid, hid, a, b, Trade.ForTrade, 0.0);
            double ironB = w.Settlements.Get(b).Iron, hidesA = w.Settlements.Get(a).Hides;
            long v0 = w.Metrics.Voyages;
            var r = w.SeaRoutes.Get(w, a, b);
            // through the day in ten-minute steps: who is aboard, where
            int crewMax = 0, onWater = 0, apart = 0, visited = 0;
            bool landedThere = false;
            long end = ToLocal(w, a, 23.0);
            while (w.Now < end)
            {
                P.Storms.Clear();
                w.AdvanceTo(w.Now + SimTime.Minutes(10));
                int crew = 0; Vec3d first = default; bool any = false;
                var mem = w.Members(hid);
                for (int j = 0; j < mem.Count; j++)
                {
                    var q = w.People.Get(mem[j]);
                    if (!q.Alive || q.Voyage != bid) continue;
                    crew++;
                    if (q.At == b) visited++;
                    if (q.Act != ActivityKind.Voyage || q.Step != StepKind.Travel) continue;
                    var at = w.Agents.PositionAt(q, w.Now);
                    double sAt = AgentSystem.RouteSAt(q, w.Now);
                    if (sAt > 60 && sAt < r.Length - 60) { onWater++; Assert.IsFalse(P.IsLand(P.Grid.CellOf(at)) && !Near(P, at, r), "rowed over the water, not the land"); }
                    if (!any) { first = at; any = true; }
                    else if (Vec3d.ArcDistance(first, at, P.Radius) > 30.0) apart++;
                }
                crewMax = Math.Max(crewMax, crew);
                if (w.Settlements.Get(b).Iron > ironB) landedThere = true;
            }
            Console.WriteLine("crew " + crewMax + ", samples on the water " + onWater + ", apart " + apart + ", visitor-samples at b " + visited + "; voyages " + (w.Metrics.Voyages - v0) +
                              "; b's iron +" + (w.Settlements.Get(b).Iron - ironB).ToString("0") + ", a's hides +" + (w.Settlements.Get(a).Hides - hidesA).ToString("0") + "; the way " + (r.Length / 1000).ToString("0.0") + " km");
            Assert.AreEqual(1, w.Metrics.Voyages - v0, "the booked voyage was sailed");
            Assert.GreaterOrEqual(crewMax, 2, "with a crew of the household");
            Assert.Greater(onWater, 0, "seen rowing the way");
            Assert.AreEqual(0, apart, "together in the one boat");
            Assert.IsTrue(landedThere, "the load dealt at the other landing");
            Assert.Greater(visited, 0, "they stayed there a while");
            Assert.Greater(w.Settlements.Get(a).Hides, hidesA, "home with the return load");
            var mm = w.Members(hid);
            for (int j = 0; j < mm.Count; j++) { var q = w.People.Get(mm[j]); if (q.Alive) { Assert.AreEqual(0, q.Voyage, "the voyage over"); Assert.AreEqual(a, q.At, "home"); } }
            Assert.AreEqual(0, Boats.Aboard(w, hid, bid), "the boat back at its landing");
        }

        static bool Near(PlanetState P, Vec3d at, Route r) => Vec3d.ArcDistance(at, r.Pts[0], P.Radius) < 400 || Vec3d.ArcDistance(at, r.Pts[r.Pts.Length - 1], P.Radius) < 400;

        /// <summary>
        /// Voyages alike at both resolutions (C9.3): the same coasts and deals, lived hour by hour and as COARSE days - voyages a boat-day, their
        /// crews and the cargo carried at par (lived: booked by the village's day, sailed by its people in the morning).
        /// </summary>
        [Test]
        public void VoyagesGoAlikeAtBothResolutions()
        {
            double vA = 0, vB = 0, kgA = 0, kgB = 0, seatsA = 0, seatsB = 0, bdA = 0, bdB = 0;
            int worlds = 0;
            for (ulong from = 701; from < 760 && worlds < 6; )
            {
                var w0 = Pair(out int a0, out int b0, from);
                if (w0 == null) break;
                ulong seed = w0.Seed;
                from = seed + 1;
                worlds++;
                foreach (bool coarse in new[] { false, true })
                {
                    var w = TestWorlds.Make(seed);
                    Boats.EnsureLanding(w, a0); Boats.EnsureLanding(w, b0);
                    int boats = PlankBoats(w, a0) + PlankBoats(w, b0);
                    if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                    w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hours(1));
                    long v0 = w.Metrics.Voyages, s0 = w.Metrics.VoyageSeats; double k0 = w.Metrics.SeaCargoKg;
                    for (int d = 0; d < 24; d++)
                    {
                        Deal(w, a0, b0);   // (the want stands: each day's voyages find the deal)
                        w.Planet.Storms.Clear();
                        w.AdvanceTo(w.Now + SimTime.Day);
                    }
                    double v = w.Metrics.Voyages - v0, kg = w.Metrics.SeaCargoKg - k0, seats = w.Metrics.VoyageSeats - s0, bd = boats * 24.0;
                    Console.WriteLine("seed " + seed + (coarse ? " COARSE" : " lived ") + ": " + boats + " plank boats; voyages " + v + " (seats " + seats + "), cargo " + kg.ToString("0") + " kg; lapsed " + w.Metrics.VoyagesLapsed);
                    if (coarse) { vB += v; kgB += kg; seatsB += seats; bdB += bd; } else { vA += v; kgA += kg; seatsA += seats; bdA += bd; }
                }
            }
            Assume.That(worlds >= 3, "coasts with ways between them");
            Console.WriteLine("lived: " + (vA / bdA).ToString("0.000") + " voyages a boat-day, " + (seatsA / Math.Max(1, vA)).ToString("0.0") + " a crew, " + (kgA / Math.Max(1, vA)).ToString("0") + " kg a voyage");
            Console.WriteLine("COARSE: " + (vB / bdB).ToString("0.000") + " voyages a boat-day, " + (seatsB / Math.Max(1, vB)).ToString("0.0") + " a crew, " + (kgB / Math.Max(1, vB)).ToString("0") + " kg a voyage");
            Assert.Greater(vA, 5); Assert.Greater(vB, 5);
            Assert.That((vB / bdB) / (vA / bdA), Is.InRange(0.6, 1.6), "voyages a boat-day at par");
            Assert.That((kgB / bdB) / (kgA / bdA), Is.InRange(0.6, 1.6), "and the cargo carried");
        }

        /// <summary>
        /// Grain by sea to a famine at both resolutions (C9.3): lived, the giver's day books its boats with the grain and their crews row it over
        /// in the morning - given when landed, told as by sea; the grain reaching the famine over twelve days at par with COARSE days.
        /// </summary>
        [Test]
        public void GrainBySeaReachesAFamineAlikeAtBothResolutions()
        {
            double fuA = 0, fuB = 0;
            int told = 0, worlds = 0;
            for (ulong from = 701; from < 760 && worlds < 4; )
            {
                var w0 = Pair(out int a0, out int b0, from);
                if (w0 == null) break;
                ulong seed = w0.Seed;
                from = seed + 1;
                worlds++;
                foreach (bool coarse in new[] { false, true })
                {
                    var w = TestWorlds.Make(seed);
                    Boats.EnsureLanding(w, a0); Boats.EnsureLanding(w, b0);
                    PlankBoats(w, a0, 3);
                    if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                    w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hours(1));
                    long mark = w.History.LastId;
                    double f0 = w.Metrics.ReliefBySeaFu;
                    for (int d = 0; d < 12; d++)
                    {
                        // premise: a famine at b all these days, its store eaten as it comes; a has grain to spare; a calm sea
                        for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (o != b0) w.Settlements.Get(o).FamineEvent = 0;
                        ref var sb = ref w.Settlements.Get(b0);
                        if (sb.FamineEvent == 0) { sb.FamineAt = w.Now; sb.FamineEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Famine, Time = w.Now, Cell = sb.Cell, Settlement = b0, Subject = EntityRef.Settlement(b0), RootKind = RootKind.Nature }); }
                        sb.Granary = 0;
                        w.Settlements.Get(a0).Granary = 100000;
                        w.Planet.Storms.Clear();
                        w.AdvanceTo(w.Now + SimTime.Day);
                    }
                    double fu = w.Metrics.ReliefBySeaFu - f0;
                    for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.ReliefSent && e.ArgA == 1 && e.Settlement == b0 && !coarse) { told++; Console.WriteLine(HistoryText.Line(w, e)); } }
                    Console.WriteLine("seed " + seed + (coarse ? " COARSE" : " lived ") + ": by sea " + fu.ToString("0") + " FU; voyages " + w.Metrics.Voyages + ", lapsed " + w.Metrics.VoyagesLapsed);
                    if (coarse) fuB += fu; else fuA += fu;
                }
            }
            Assume.That(worlds >= 2, "coasts with ways between them");
            Console.WriteLine("by sea: lived " + fuA.ToString("0") + " FU, COARSE " + fuB.ToString("0") + " FU");
            Assert.Greater(fuA, 0.0, "lived: grain rowed over to the famine");
            Assert.Greater(told, 0, "told as by sea");
            Assert.That(fuB / fuA, Is.InRange(0.6, 1.6), "the grain at par");
        }
    }
}
