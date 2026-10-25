using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The land between villages (C3.6g): households that choose to leave, know no free good place and find no village to join go together - enough to
    /// make a village - to their own village's margins, its houses beyond every village's fields, and found a daughter village there; with the rule off
    /// they stay. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class MarginsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        // a world three years on, and its village of the most households
        static World Grown(ulong seed, out int sid)
        {
            var w = TestWorlds.Make(seed);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(3) + SimTime.Day);
            sid = 0; int most = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (w.Settlements.Get(o).Inhabited && w.HouseholdsOf(o).Count > most) { most = w.HouseholdsOf(o).Count; sid = o; }
            return w;
        }

        // premise: a famine fresh in the village's mind, its scouts see nothing (it knows no place beyond the spacing) and no village lies within a
        // day's walk (none to join)
        static void Press(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid); s.FamineAt = w.Now - SimTime.Day; s.ScoutAt = w.Now; s.Lore = KnownSites.Empty;
            w.Config.MarriageReach = 1;
        }

        static int DaughterParty(World w)
        {
            for (int p = w.Parties.First(); p != 0; p = w.Parties.Next(p)) if (w.Parties.Get(p).Kind == 1) return p;
            return 0;
        }

        static int Daughter(World w, long mark)
        {
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.SettlementFounded && e.ArgA == 1) return e.Settlement; }
            return 0;
        }

        [Test]
        public void LeaversWithNowhereToGoFoundADaughterVillageOnTheirOwnMargins()
        {
            Migration.MarginsOff = false;
            var w = Grown(411, out int sid);
            long mark = w.History.LastId;
            int pid = 0;
            for (int d = 0; d < 100 && pid == 0; d++) { Press(w, sid); w.AdvanceTo(w.Now + SimTime.Day); pid = DaughterParty(w); }
            Assert.AreNotEqual(0, pid, "leavers who knew no place and found no village to join set out for their village's margins");
            var party = w.Parties.Get(pid);
            Assert.AreEqual(sid, party.Origin);
            Assert.AreEqual(0, party.Dest, "to found, not to join");
            int hh = 0; for (int h = w.Households.First(); h != 0; h = w.Households.Next(h)) if (w.Households.Get(h).Party == pid) hh++;
            Assert.GreaterOrEqual(hh, Migration.MinHouseholds, "enough households to make a village");
            // a world saved with the party gathering continues identically, its kind kept
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Living), out _);
            Assert.AreEqual(1, w2.Parties.Get(pid).Kind, "the party's kind is saved");
            long end = w.Now + SimTime.Days(3);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a save with a daughter's party continues as if never saved");
            int nv = Daughter(w, mark);
            Assert.AreNotEqual(0, nv, "they founded a daughter village");
            Assert.AreEqual(nv, Daughter(w2, mark));
            // where: on its mother's margins - nearer to it than to any other village, inside its spacing, its houses beyond every village's fields
            var P = w.Planet; var v = w.Settlements.Get(nv); var mother = w.Settlements.Get(sid);
            double dm = Vec3d.ArcDistance(v.Anchor, mother.Anchor, P.Radius);
            Assert.Less(dm, Sites.Spacing, "inside the spacing no village beyond it is founded in");
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == nv) continue;
                var t = w.Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(v.Anchor, t.Anchor, P.Radius);
                Assert.GreaterOrEqual(d, Migration.MarginGap(w) - 1e-6, "its houses beyond every village's fields");
                if (o != sid) Assert.GreaterOrEqual(d, dm, "on its mother's side of the land between them");
            }
            int owner = P.FieldOwner[v.Cell];
            Assert.IsTrue(owner == 0 || owner == nv, "not on another village's field");
            // the history: the setting out from the mother, the founding caused by it and told as a daughter's
            long setOut = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.PartySetOut && e.Settlement == sid) setOut = e.Id;
                if (e.Kind == HistoryKind.SettlementFounded && e.Settlement == nv)
                {
                    Assert.AreEqual(setOut, e.Cause1, "founded because they set out");
                    Assert.AreEqual(sid, e.Object.Id, "by people of its mother");
                    StringAssert.Contains("on the margins of " + w.History.NameOf(EntityRef.Settlement(sid)), HistoryText.Describe(w, in e));
                }
            }
            Assert.AreNotEqual(0L, setOut);
            Assert.Greater(w.Settlements.Get(nv).Population, 0);
            Assert.AreEqual(w.Settlements.Get(sid).Tongue != 0, w.Settlements.Get(nv).Tongue != 0, "they speak as their mother's people");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void WithTheRuleOffTheyStay()
        {
            Migration.MarginsOff = true;
            try
            {
                var w = Grown(411, out int sid);
                long mark = w.History.LastId;
                int left = 0;
                for (int d = 0; d < 100; d++)
                {
                    Press(w, sid); w.AdvanceTo(w.Now + SimTime.Day);
                    Assert.AreEqual(0, DaughterParty(w), "no party for the margins");
                    for (int p = w.Parties.First(); p != 0; p = w.Parties.Next(p)) if (w.Parties.Get(p).Origin == sid) left++;
                }
                Assert.AreEqual(0, Daughter(w, mark), "no daughter village with the rule off");
                Assert.AreEqual(0, left, "its leavers stay: no place known, no village to join");
            }
            finally { Migration.MarginsOff = false; }
        }

        [Test]
        public void AMarginSiteKeepsItsRoomAndIsWorthLiving()
        {
            Migration.MarginsOff = false;
            var w = Grown(412, out int sid);
            var P = w.Planet;
            int c = w.Migration.MarginSite(sid, out double q);
            Assert.GreaterOrEqual(c, 0, "a grown village has free land on its margins");
            Assert.GreaterOrEqual(q, Sites.MinQuality, "worth living on");
            Assert.AreEqual(q, Sites.Quality(w, c, new System.Collections.Generic.List<int>(), Migration.MarginGap(w)), 1e-12, "by the site's own worth with that room kept");
            Assert.AreEqual(0.0, Sites.Quality(w, c, new System.Collections.Generic.List<int>()), "and no place a village beyond the spacing could be founded at");
            Assert.AreEqual(0, P.FieldOwner[c], "on no village's field");
            Assert.AreEqual(0, w.SettlementAtCell(c), "nor a village's ground");
            Assert.IsNotNull(w.Routes.GetToCell(w, sid, c), "and a way on foot reaches it");
        }
    }
}
