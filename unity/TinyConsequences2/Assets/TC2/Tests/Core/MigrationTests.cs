using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Building, knowing places, moving and founding (D2-045): mechanisms, not outcomes. Nothing stands without labour and
    /// wood; nobody knows a place nobody went to see; a pressed village's households go to a place their people saw, walk
    /// there and begin with nothing built.
    /// </summary>
    [TestFixture]
    public class MigrationTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static int FirstHousehold(World w, int sid) => w.HouseholdsOf(sid)[0];

        static int CountKind(World w, HistoryKind k, long afterId)
        {
            int n = 0;
            for (int i = w.History.FirstIndexAfter(afterId); i < w.History.Count; i++) if (w.History.At(i).Kind == k) n++;
            return n;
        }

        /// <summary>A land cell 2-4 km from a village whose surroundings hold a site good enough to found (a scout's goal).</summary>
        static int GoodPlace(World w, int sid)
        {
            var P = w.Planet; var s = w.Settlements.Get(sid);
            var scratch = new List<int>();
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                double d = Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius);
                if (d < 2000 || d > 4000) continue;
                if (Sites.Quality(w, c, scratch) >= 0.4 && w.Routes.GetToCell(w, sid, c) != null) return c;
            }
            return -1;
        }

        [Test]
        public void AHomeIsRaisedOnlyByLabourAndWood()
        {
            var a = TestWorlds.Make(401); var b = TestWorlds.Make(401);
            int sid = a.Settlements.First();
            int hid = FirstHousehold(a, sid);
            Coarse(a); Coarse(b);
            // premise: the household's house is gone (a camp); a: the village has wood; b: none is to be had
            foreach (var w in new[] { a, b }) { ref var hh = ref w.Households.Get(hid); hh.Shelter = Construction.Camp; hh.Build = 0f; }
            long mark = a.History.LastId;
            for (int d = 0; d < 60; d++)
            {
                // (kept stocked: since D2-056 every hearth in the village burns the store's wood too, and the household's own
                // work - meals, water - takes its hours: a house of 240 hours takes a working household some 50 days)
                if (a.Settlements.Get(sid).Woodstore < 200) a.Settlements.Get(sid).Woodstore = 200;
                b.Settlements.Get(sid).Woodstore = 0;
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
            }
            Assert.AreEqual(Construction.House, a.Households.Get(hid).Shelter, "with labour and wood, a hut and then a house");
            Assert.GreaterOrEqual(CountKind(a, HistoryKind.HomeBuilt, mark), 2, "both told");
            // b: wood gathered on a day is gone again the next (the premise), so a stage that needs wood never finishes in one day
            Assert.Less(b.Households.Get(hid).Shelter, Construction.House, "no wood, no house");
        }

        [Test]
        public void AnAbandonedVillagesLandGoesBackToTheLand()
        {
            var w = TestWorlds.Make(403);
            int sid = w.Settlements.First();
            var P = w.Planet;
            int cell = w.Settlements.Get(sid).Cell;
            var fields = new List<int>(); var mine = w.FieldCellsOf(sid); for (int i = 0; i < mine.Count; i++) fields.Add(mine[i]);
            Assert.Greater(fields.Count, 0, "the village holds fields");
            Assert.AreEqual(sid, w.SettlementAtCell(cell));
            // premise: everyone in the village dies
            var rs = w.Residents(sid); var people = new List<int>(); for (int i = 0; i < rs.Count; i++) people.Add(rs[i]);
            foreach (int pid in people) { ref var p = ref w.People.Get(pid); w.Agents.Kill(ref p, 0, 1); }
            w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hour);
            Assert.IsFalse(w.Settlements.Get(sid).Inhabited);
            foreach (int c in fields) Assert.AreNotEqual(sid, P.FieldOwner[c], "its fields go back to the land");
            Assert.AreEqual(0, w.FieldCellsOf(sid).Count);
            Assert.AreNotEqual(sid, w.SettlementAtCell(cell), "its ground is no longer a village's");
            // a world saved after it and loaded sees the same land
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            var w2 = SaveFile.Load(bytes, out _);
            Assert.AreEqual(w.SettlementAtCell(cell), w2.SettlementAtCell(cell), "the loaded world indexes only the living villages");
            long end = w.Now + SimTime.Days(10);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "and lives on identically");
        }

        [Test]
        public void NobodyKnowsAPlaceNobodyWentToSee()
        {
            var w = TestWorlds.Make(402);
            int sid = w.Settlements.First();
            Assert.AreEqual(0, w.Settlements.Get(sid).Lore.Count, "a village knows no place beyond its own at the start");
            int at = GoodPlace(w, sid);
            Assert.GreaterOrEqual(at, 0, "there is good land 2-4 km out");
            w.Migration.Look(sid, at, 0);
            var s = w.Settlements.Get(sid);
            Assert.Greater(s.Lore.Count, 0, "someone who stood there knows it now");
            int site = s.Lore.Best(out float q);
            Assert.GreaterOrEqual(q, Sites.MinQuality);
            var P = w.Planet;
            Assert.LessOrEqual(Vec3d.ArcDistance(P.Grid.Center[site], P.Grid.Center[at], P.Radius), Sites.LookRadius + P.CellSize, "a place within sight of where they stood");
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                Assert.GreaterOrEqual(Vec3d.ArcDistance(P.Grid.Center[site], w.Settlements.Get(o).Anchor, P.Radius), Sites.Spacing, "no village too near");
        }

        static World PressedVillageWithAKnownPlace(ulong seed, out int sid, out int site, bool activity)
        {
            var w = TestWorlds.Make(seed);
            sid = w.Settlements.First();
            if (!activity) Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(3) + SimTime.Day);
            // premise: a famine has just struck (every grown person will remember it) and a scout has seen good land
            ref var s = ref w.Settlements.Get(sid);
            s.FamineAt = w.Now;
            site = -1;
            int at = GoodPlace(w, sid);
            if (at >= 0) { w.Migration.Look(sid, at, 0); site = w.Settlements.Get(sid).Lore.Best(out _); }
            return w;
        }

        static int NewestSettlement(World w) { int last = 0; for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) last = s; return last; }

        [Test]
        public void APressedVillageFoundsAnUnfinishedVillageAtThePlaceItKnows()
        {
            var w = PressedVillageWithAKnownPlace(403, out int sid, out int site, activity: false);
            Assert.GreaterOrEqual(site, 0, "the village knows a place");
            int before = 0; for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) before++;
            long mark = w.History.LastId;
            // until the year's weighing after the harvest, and the party's journey
            for (int d = 0; d < 60 && w.Metrics.Foundings == 0; d++)
            {
                ref var s = ref w.Settlements.Get(sid); s.FamineAt = w.Now - SimTime.Day;   // (kept fresh)
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            Assert.Greater(w.Metrics.Foundings, 0, "a party set out and founded a village");
            int nv = NewestSettlement(w);
            var v = w.Settlements.Get(nv);
            Assert.AreEqual(site, v.Cell, "at the place its people saw");
            Assert.AreEqual(0, v.Works, "no well, no granary yet");
            Assert.AreEqual(0, v.FieldCells, "no fields until spring");
            Assert.Greater(v.Population, 0);
            var hhs = w.HouseholdsOf(nv);
            for (int i = 0; i < hhs.Count; i++) Assert.Less(w.Households.Get(hhs[i]).Shelter, Construction.House, "they live in camps or huts");
            // the history: the setting out in response to the famine, the founding caused by the setting out
            long setOut = 0, founded = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.PartySetOut && e.Settlement == sid) setOut = e.Id;
                if (e.Kind == HistoryKind.SettlementFounded && e.Settlement == nv) { founded = e.Id; Assert.AreEqual(setOut, e.Cause1, "founded because they set out"); }
            }
            Assert.AreNotEqual(0L, setOut); Assert.AreNotEqual(0L, founded);
            // a year on: they have built and sown
            w.AdvanceTo(w.Now + SimTime.Years(1) + SimTime.Days(2));
            var v2 = w.Settlements.Get(nv);
            if (v2.Inhabited)
            {
                Assert.AreNotEqual(0, v2.Works & Construction.Well, "they dug a well");
                Assert.Greater(v2.FieldCells, 0, "they claimed fields in the spring");
            }
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void HouseholdsWithNoPlaceToFoundJoinAVillageWithLand()
        {
            var w = TestWorlds.Make(404);
            int sid = w.Settlements.First();
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(3) + SimTime.Day);
            long mark = w.History.LastId;
            int moved = 0;
            for (int d = 0; d < 60 && moved == 0; d++)
            {
                // premise: a famine fresh in mind, and no scout ever sees a place (none goes)
                ref var s = ref w.Settlements.Get(sid); s.FamineAt = w.Now - SimTime.Day; s.ScoutAt = w.Now; s.Lore = KnownSites.Empty;
                w.AdvanceTo(w.Now + SimTime.Day);
                moved = CountKind(w, HistoryKind.HouseholdMoved, mark);
            }
            Assert.Greater(moved, 0, "a household left for a village with land to spare");
            Assert.AreEqual(0L, w.Metrics.Foundings, "no village founded: no place was known");
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.HouseholdMoved) continue;
                int dest = e.Object.Id;
                Assert.AreNotEqual(sid, dest);
                Assert.IsTrue(w.Settlements.Get(dest).Inhabited);
            }
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void APartyWalksTogetherAndASaveOnTheRoadContinuesIdentically()
        {
            var w = PressedVillageWithAKnownPlace(405, out int sid, out int site, activity: true);
            Assert.GreaterOrEqual(site, 0);
            // until the party is on its way
            int pid = 0;
            for (int h = 0; h < 24 * 60 && pid == 0; h++)
            {
                if (h % 24 == 0) { ref var s = ref w.Settlements.Get(sid); s.FamineAt = w.Now - SimTime.Day; }
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int p = w.Parties.First(); p != 0; p = w.Parties.Next(p)) if (w.Parties.Get(p).State == 1) pid = p;
            }
            Assert.AreNotEqual(0, pid, "a party set out");
            var party = w.Parties.Get(pid);
            // its people walk the way to the place, all arriving together
            int walkers = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (!p.Alive || p.Act != ActivityKind.Move) continue;
                walkers++;
                Assert.AreEqual(StepKind.Travel, p.Step);
                Assert.AreEqual(party.Arrive, p.T1, "at the party's pace");
            }
            Assert.Greater(walkers, 0, "they walk");
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            var w2 = SaveFile.Load(bytes, out _);
            long end = w.Now + SimTime.Days(2);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a world saved with a party on the road continues as if never saved");
            Assert.Greater(w.Metrics.Foundings, 0, "they arrived and founded");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        // ---------------------------------------------------------------- two villages on one spot (D2-137)
        /// <summary>Hour by hour until a party of the village is on its way to the place it knows; 0 if none set out.</summary>
        static int PartyOnItsWay(World w, int sid)
        {
            for (int h = 0; h < 24 * 60; h++)
            {
                if (h % 24 == 0) { ref var s = ref w.Settlements.Get(sid); s.FamineAt = w.Now - SimTime.Day; }
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int p = w.Parties.First(); p != 0; p = w.Parties.Next(p)) if (w.Parties.Get(p).State == 1 && w.Parties.Get(p).Site >= 0) return p;
            }
            return 0;
        }

        [Test]
        public void APartyFindingAVillageAtItsPlaceMakesItsHomeThere()
        {
            var w = PressedVillageWithAKnownPlace(403, out int sid, out int site, activity: false);
            Assert.GreaterOrEqual(site, 0, "the village knows a place");
            int pid = PartyOnItsWay(w, sid);
            Assert.AreNotEqual(0, pid, "a party set out");
            // premise: while they walk, their place becomes a village's ground (another village of the world stands there)
            int there = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (s != sid && w.Settlements.Get(s).Inhabited) { there = s; break; }
            Assert.AreNotEqual(0, there);
            w.Parties.Get(pid).Site = w.Settlements.Get(there).Cell;
            long setOut = w.Parties.Get(pid).Event;
            var going = new List<int>();
            for (int h = w.Households.First(); h != 0; h = w.Households.Next(h)) if (w.Households.Get(h).Party == pid) going.Add(h);
            Assert.Greater(going.Count, 0);
            long foundings = w.Metrics.Foundings, mark = w.History.LastId;
            for (int h = 0; h < 24 * 10 && w.Parties.Exists(pid); h++) w.AdvanceTo(w.Now + SimTime.Hour);
            Assert.IsFalse(w.Parties.Exists(pid), "the party arrived");
            Assert.AreEqual(foundings, w.Metrics.Foundings, "no village founded on another's ground");
            for (int i = 0; i < going.Count; i++)
            {
                var hh = w.Households.Get(going[i]);
                if (hh.Dissolved != 0) continue;
                Assert.AreEqual(there, hh.Settlement, "they made their home in the village standing there");
                Assert.IsTrue(w.HouseholdsOf(there).Contains(going[i]));
            }
            int joined = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.PartyJoined) continue;
                joined++;
                Assert.AreEqual(sid, e.Subject.Id, "the village they left");
                Assert.AreEqual(there, e.Object.Id, "the village they joined");
                Assert.AreEqual(setOut, e.Cause1, "because they set out");
                StringAssert.Contains("made their home", HistoryText.Line(w, e));
            }
            Assert.AreEqual(1, joined, "the joining recorded once");
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ATravellerOnAWayOfNoLengthArrivesWhereTheyWereGoing()
        {
            // the bug's own premise (a Web catch-up of 2026-10-09 ran out of memory): a party founds on its own village's spot - villages on one
            // cell, a way of no length between them - and their kin visit across it; each must arrive where they were going, not walk it forever
            var w = PressedVillageWithAKnownPlace(405, out int sid, out int site, activity: true);
            Assert.GreaterOrEqual(site, 0);
            int pid = PartyOnItsWay(w, sid);
            Assert.AreNotEqual(0, pid, "a party set out");
            Migration.JoinOff = true;
            try
            {
                w.Parties.Get(pid).Site = w.Settlements.Get(sid).Cell;
                // (its anchor at its cell's centre, as a village a party founded has: two such on one cell have one anchor)
                { ref var so = ref w.Settlements.Get(sid); so.Anchor = w.Planet.Grid.Center[so.Cell]; }
                w.Routes.Invalidate();
                for (int h = 0; h < 24 * 10 && w.Parties.Exists(pid); h++) w.AdvanceTo(w.Now + SimTime.Hour);
                int b = 0;
                for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) if (s != sid && w.Settlements.Get(s).Cell == w.Settlements.Get(sid).Cell && w.Settlements.Get(s).Inhabited) b = s;
                Assert.AreNotEqual(0, b, "premise: a village on the same cell");
                var r = w.Routes.Get(w, sid, b);
                Assert.IsNotNull(r);
                Assert.LessOrEqual(r.Length, 1.0, "premise: a way of no length");
                // premise: someone of the new village at home goes over to the old one (their kin's), then lives on by their own choices
                int walker = 0;
                for (int i = 0; i < 24 * 4 && walker == 0; i++)
                {
                    var res = w.Residents(b);
                    for (int k = 0; k < res.Count && walker == 0; k++) { var q = w.People.Get(res[k]); if (q.Alive && q.At == b && q.AgeYears(w.Now) >= 16 && q.Step != StepKind.Travel) walker = q.Id; }
                    if (walker == 0) w.AdvanceTo(w.Now + SimTime.Hour);
                }
                Assert.AreNotEqual(0, walker, "premise: someone of the new village at home");
                int visits0 = w.People.Get(walker).Visits;
                w.Agents.Walk(walker, r, r.A == b, w.Now + SimTime.Minute, sid);
                var ver = new Dictionary<int, int>();
                for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive) ver[id] = p.ActVer; }
                bool there = false, backHome = false;
                for (int h = 0; h < 24 * 20; h++)
                {
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    var p = w.People.Get(walker);
                    if (!p.Alive) break;
                    if (p.At == sid) there = true;
                    else if (there && p.At == b) backHome = true;
                }
                int most = 0;
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (p.Alive && ver.TryGetValue(id, out int v0)) most = Math.Max(most, p.ActVer - v0);
                }
                Assert.IsTrue(there, "they got to the old village");
                Assert.Greater(w.People.Get(walker).Visits, visits0, "a visit");
                Assert.IsTrue(backHome, "and came home - not back to where they were, again and again");
                Assert.Less(most, 2000, "nobody walks a way of no length forever (a day lived is some 20 activities)");
                Assert.Less(w.Travel.LiveEntries, 20000, "the travel index holds live journeys only");
                Assert.IsNull(TestWorlds.Audit(w, oneCellOk: true));   // (the premise is two villages on one cell)
            }
            finally { Migration.JoinOff = false; }
        }

        [Test]
        public void TheTravelIndexDropsStaleLegsBeforeItGrows()
        {
            // two indexes alike: one swept when full, one not - every query reads the same, the swept one has room again
            var a = new TravelIndex(16); var b = new TravelIndex(16);
            for (int i = 0; i < 1024; i++) { a.Add(i % 16, 1 + i % 8, 1, i, 2000 + i); b.Add(i % 16, 1 + i % 8, 1, i, 2000 + i); }
            Assert.IsTrue(a.Full);
            Func<int, int> ver = p => p <= 4 ? 2 : 1;   // (persons 1-4 have moved on to another activity)
            a.Sweep(100, ver);
            Assert.IsFalse(a.Full, "room again");
            Assert.AreEqual(512, a.LiveEntries);
            var qa = new List<(int, long, long)>(); var qb = new List<(int, long, long)>();
            for (int c = 0; c < 16; c++)
            {
                qa.Clear(); qb.Clear();
                a.Query(c, 100, ver, qa); b.Query(c, 100, ver, qb);
                CollectionAssert.AreEqual(qb, qa, "cell " + c + " reads the same, in the same order");
            }
            // all live: the pool doubles at once rather than sweeping at every leg
            var d = new TravelIndex(4);
            for (int i = 0; i < 1024; i++) d.Add(i % 4, 1, 1, 0, 5000);
            d.Sweep(100, p => 1);
            Assert.AreEqual(1024, d.LiveEntries);
            Assert.IsFalse(d.Full);
        }
    }
}
