using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Homes stand on their own (D2-059): a building outlives the household that raised it, stands empty, is taken by a household
    /// without a home before it raises its own, and decays to a ruin that fades into the land. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class HomesTests
    {
        /// <summary>Kills every living member of a household (a state the world reaches by itself: one death after another).</summary>
        static void DieOut(World w, int hid)
        {
            var mem = w.Members(hid);
            while (mem.Count > 0) { ref var p = ref w.People.Get(mem[0]); w.Agents.Kill(ref p, 0, 1); }
        }

        static int HouseholdWithKin(World w, int sid, out int heirHousehold)
        {
            // a household whose member has a child or a parent living in another household of the village
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                var mem = w.Members(hhs[i]);
                for (int k = 0; k < mem.Count; k++)
                {
                    var p = w.People.Get(mem[k]);
                    for (int j = 0; j < hhs.Count; j++)
                    {
                        if (j == i) continue;
                        var other = w.Members(hhs[j]);
                        for (int m = 0; m < other.Count; m++)
                        {
                            var q = w.People.Get(other[m]);
                            if (q.Mother == p.Id || q.Father == p.Id || q.Id == p.Mother || q.Id == p.Father) { heirHousehold = hhs[j]; return hhs[i]; }
                        }
                    }
                }
            }
            heirHousehold = 0;
            return hhs[0];
        }

        [Test]
        public void AHomeOutlivesTheHouseholdThatLivedInIt()
        {
            var w = TestWorlds.Make(441);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Years(25));   // grown children have households of their own
            int hid = HouseholdWithKin(w, sid, out int heir);
            ref var hh = ref w.Households.Get(hid);
            int home = hh.Building;
            Assert.AreNotEqual(0, home, "(the household lives in a building)");
            hh.Larder = 7.0;
            int beasts = w.BeastsOf(hid).Count;
            double heirLarder = heir != 0 ? w.Households.Get(heir).Larder + w.Households.Get(heir).Ready : 0;
            int heirBeasts = heir != 0 ? w.BeastsOf(heir).Count : 0;
            DieOut(w, hid);
            var b = w.Buildings.Get(home);
            Assert.AreEqual(0, b.Household, "its home stands empty");
            Assert.AreEqual(w.Now, b.Left);
            Assert.AreNotEqual(0L, w.Households.Get(hid).Dissolved, "the household is no more");
            Assert.IsFalse(w.HouseholdsOf(sid).Contains(hid), "nor among the village's households");
            Console.WriteLine("heir " + heir + ": larder " + heirLarder.ToString("0.0") + " -> " + (w.Households.Exists(heir) ? w.Households.Get(heir).Larder + w.Households.Get(heir).Ready : 0).ToString("0.0") + ", animals " + heirBeasts + " -> " + (heir != 0 ? w.BeastsOf(heir).Count : 0) + " (the dead household's " + beasts + ")");
            if (heir != 0)
            {
                Assert.AreEqual(heirLarder + 7.0, w.Households.Get(heir).Larder + w.Households.Get(heir).Ready, 1e-6, "its larder to its kin");
                Assert.AreEqual(heirBeasts + beasts, w.BeastsOf(heir).Count, "and its animals");
            }
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ACoupleTakesAnEmptyHomeBeforeRaisingItsOwn()
        {
            var w = TestWorlds.Make(443);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            int sid = w.Settlements.First();
            w.AdvanceTo(w.Now + SimTime.Years(16));
            // premise: a household dies out, and an unmarried woman and man of the village wed
            int gone = w.HouseholdsOf(sid)[0];
            int home = w.Households.Get(gone).Building;
            DieOut(w, gone);
            int woman = 0, man = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                if (!p.Alive || p.Partner != 0 || p.AgeYears(w.Now) < 18) continue;
                if (p.IsFemale && woman == 0) woman = p.Id; else if (!p.IsFemale && man == 0) man = p.Id;
            }
            Assume.That(woman != 0 && man != 0, "an unmarried couple in the village");
            int lots = w.Settlements.Get(sid).Lots;
            long mark = w.History.LastId;
            w.SettlementSys.Wed(sid, woman, man);
            int couple = w.People.Get(woman).Household;
            Assert.AreEqual(home, w.Households.Get(couple).Building, "they made their home in the empty one");
            Assert.AreEqual(w.Settlements.Get(sid).Lots, lots, "no new lot");
            Assert.AreEqual(couple, w.Buildings.Get(home).Household);
            Assert.AreEqual(Construction.House, w.Households.Get(couple).Shelter, "a house to live in, not a camp");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.HomeTaken) { told = true; Assert.AreEqual(EntityRef.Household(gone), e.Object, "whose it was"); Console.WriteLine(HistoryText.Line(w, e)); }
            }
            Assert.IsTrue(told, "history tells it");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void AnEmptyHomeFallsInAndFadesWhileALivedInOneStands()
        {
            // the law: a house left empty is half gone at 30 years and fallen at 60; its ruin half gone 150 years later
            long y = SimTime.Year, t0 = 1000 * y;
            var house = new Building { Kind = Construction.House, Left = t0 };
            Assert.AreEqual(1.0, Homes.Condition(new Building { Kind = Construction.House }, t0 + 500 * y), "lived in: kept sound");
            Assert.AreEqual(0.5, Homes.Condition(house, t0 + 30 * y), 1e-9);
            Assert.IsFalse(Homes.Fallen(house, t0 + 59 * y));
            Assert.IsTrue(Homes.Fallen(house, t0 + 60 * y));
            Assert.AreEqual(0.5, Homes.RuinLeft(house, t0 + 210 * y), 1e-9);
            Assert.AreEqual(0.0, Homes.RuinLeft(house, t0 + 360 * y), 1e-9);
            var hut = new Building { Kind = Construction.Hut, Left = t0 };
            Assert.IsTrue(Homes.Fallen(hut, t0 + 20 * y), "a hut goes sooner");
            // the world: an empty home nobody wants is not taken once it is past mending, and its gone ruin leaves the table
            var w = TestWorlds.Make(445);
            int sid = w.Settlements.First();
            int gone = w.HouseholdsOf(sid)[0];
            int home = w.Households.Get(gone).Building;
            DieOut(w, gone);
            ref var b = ref w.Buildings.Get(home);
            b.Left = w.Now - 45 * y;   // premise: left 45 years ago (a house past mending: 0.25)
            Assert.Less(Homes.Condition(b, w.Now), Homes.Habitable);
            int other = w.HouseholdsOf(sid)[0];
            Homes.Leave(w, other);   // premise: a household without a home
            int otherHome = w.Buildings.Exists(w.Households.Get(other).Building) ? w.Households.Get(other).Building : 0;
            Assert.IsTrue(Homes.TakeEmpty(w, other), "(it takes the sound home it just left)");
            Assert.AreNotEqual(home, w.Households.Get(other).Building, "not the one past mending");
            w.Buildings.Get(home).Left = w.Now - 400 * y;   // premise: its ruin is gone into the land
            Homes.Prune(w);
            Assert.IsFalse(w.Buildings.Exists(home), "a ruin gone into the land leaves the table");
            Assert.IsFalse(w.BuildingsOf(sid).Contains(home));
            Assert.IsNull(TestWorlds.Audit(w));
        }
            [Test]
        public void ANewHomeTakesTheInnermostFreeLotSoAVillageKeepsItsShape()
        {
            // a ruin keeps its lot; when it has gone into the land its lot is the next new home's (before, every new home took the next
            // lot of an ever-longer spiral and villages of a few hundred sprawled 400 m out over the centuries); never a lot in the water
            const long y = SimTime.Year;
            var w = TestWorlds.Make(445);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            int lots = s.Lots;
            Homes.FreeLot(w, sid, 0, out float nx, out float ny);
            Assert.GreaterOrEqual(w.Settlements.Get(sid).Lots, lots, "a lived-in village's lots are taken: a new one beyond them");
            // premise: the household on the innermost lot dies out and its home stands empty, then falls, then its ruin goes
            int inner = 0; double best = double.MaxValue;
            var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++)
            {
                var b0 = w.Buildings.Get(bl[i]);
                double d = Math.Sqrt(((double)b0.X - s.SquareX) * ((double)b0.X - s.SquareX) + ((double)b0.Y - s.SquareY) * ((double)b0.Y - s.SquareY));
                if (d < best) { best = d; inner = bl[i]; }
            }
            Assert.AreNotEqual(0, inner);
            var b = w.Buildings.Get(inner);
            int gone = b.Household;
            DieOut(w, gone);
            w.Buildings.Get(inner).Left = w.Now - 100 * y;   // premise: fallen in long ago, its ruin still there
            Homes.FreeLot(w, sid, 0, out float rx, out float ry);
            Assert.Greater((rx - b.X) * (double)(rx - b.X) + (ry - b.Y) * (double)(ry - b.Y), 36.0, "a ruin keeps its lot");
            w.Buildings.Get(inner).Left = w.Now - 400 * y;   // premise: its ruin is gone into the land
            Homes.Prune(w);
            Homes.FreeLot(w, sid, 0, out float fx, out float fy);
            var P = w.Planet;
            int cell = P.Grid.CellOf(w.Settlements.Get(sid).LocalToSphere(fx, fy, P.Radius));
            Assert.IsTrue(P.IsLand(cell) && !P.IsLake(cell), "on dry land");
            double fromSquare = Math.Sqrt(((double)fx - s.SquareX) * ((double)fx - s.SquareX) + ((double)fy - s.SquareY) * ((double)fy - s.SquareY));
            Assert.LessOrEqual(fromSquare, best + 1e-3, "the freed inner lot (or one as near the square) is the new home's, not the spiral's end");
            Console.WriteLine("innermost home " + best.ToString("0.0") + " m from the square; the next new home after its ruin went " + fromSquare.ToString("0.0") + " m; lots used " + w.Settlements.Get(sid).Lots);
        }
    }
}
