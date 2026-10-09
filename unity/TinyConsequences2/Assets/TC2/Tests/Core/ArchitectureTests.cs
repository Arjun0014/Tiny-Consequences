using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// How buildings are built (D2-114): a home's form read from its household and village when it is raised - size and means, the
    /// village's size and ground and knowledge, its tradition carried by its founders (with a founder's drift); and houses rebuilt by
    /// households of means when theirs is old or poor beside what they would build now, with their own labour and the village's wood.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class ArchitectureTests
    {
        static int HouseOf(World w, int sid, out int hid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Shelter >= Construction.House && hh.Building != 0) { hid = hl[i]; return hh.Building; }
            }
            hid = 0; return 0;
        }

        [Test]
        public void EveryHomeAndWorkHasTheFormItsBuildersGaveIt()
        {
            var w = TestWorlds.Make(7);
            int homes = 0, works = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                Assert.AreNotEqual(0, w.Settlements.Get(sid).Tradition, "each first people its own way of building");
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    Assert.IsTrue(Architecture.Has(b.Form), "building " + bl[i] + " has its form");
                    if (Structures.IsHome(b.Kind)) homes++; else works++;
                    Assert.AreEqual(Architecture.TraditionRoof(w.Settlements.Get(sid).Tradition), Architecture.RoofOf(b.Form), "roofed in its village's way");
                }
            }
            Console.WriteLine(homes + " homes and " + works + " works with their forms");
            Assert.Greater(homes, 0); Assert.Greater(works, 0);
        }

        [Test]
        public void APlanFollowsTheHouseholdAndItsVillage()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            int bid = HouseOf(w, sid, out int hid);
            Assume.That(bid != 0, "a household with a house");
            ref var s = ref w.Settlements.Get(sid);
            int people = w.Members(hid).Count;
            // poor and few, nothing in the village's granary: a one-room cottage
            ref var hh = ref w.Households.Get(hid);
            double granaryWas = s.Granary;
            hh.Larder = 0; hh.Gear = 0f; s.Granary = 0; hh.Grain = 0;
            int poor = Architecture.HomeForm(w, sid, hid, bid, Construction.House);
            double poorMeans = Architecture.Means(w, hid);
            Console.WriteLine("household of " + people + ", nothing in hand (means " + poorMeans.ToString("0.00") + "): " + Architecture.Describe(poor, true));
            int cattle = 0, small = 0;
            var beasts = w.BeastsOf(hid);
            for (int i = 0; i < beasts.Count; i++) { var bb = w.Beasts.Get(beasts[i]); if (!bb.Alive) continue; if (bb.Kind == Livestock.Cattle) cattle++; else small++; }
            if (cattle > 0 || small >= 4 || people >= 7) Assert.AreEqual(Architecture.Long, Architecture.PlanOf(poor), "beasts (or many) want a byre-house");
            else if (people <= 2 && poorMeans < 1.0 || people <= 3 && poorMeans < 0.6) Assert.AreEqual(Architecture.Small, Architecture.PlanOf(poor));
            else Assert.AreEqual(Architecture.Cottage, Architecture.PlanOf(poor));
            // a household with its own grain in the village's granary (C3.3, D2-126): its means are the more (its hours are free to build)
            hh.Grain = people * w.Config.FoodPerAdultDay * 365.0;
            Assert.AreEqual(1.0, Architecture.Means(w, hid) - poorMeans, 1e-9, "a long year of its own food in store (D2-114's scale)");
            // the best off in a village of 150: a hall-house with its chimney
            hh.Larder = 1e6; hh.Gear = 50f;
            int popWas = s.Population; s.Population = 150;
            int rich = Architecture.HomeForm(w, sid, hid, bid, Construction.House);
            Console.WriteLine("the village's best off, 150 people: " + Architecture.Describe(rich, true));
            Assert.AreEqual(Architecture.Hall, Architecture.PlanOf(rich));
            Assert.IsTrue(Architecture.Chimney(rich));
            Assert.Greater(Architecture.Gain(rich, poor), 2.0, "far better than the poor one");
            // pottery and clay known, a village of size: tiles for those of means
            s.TechKnown |= (byte)(1 << TC2.Core.Knowledge.Tech.Pottery);
            bool clay = w.Learning.Clay(s.Cell);
            int tiled = Architecture.HomeForm(w, sid, hid, bid, Construction.House);
            Console.WriteLine("knowing pottery (clay near: " + clay + "): " + Architecture.Describe(tiled, true));
            if (clay) Assert.AreEqual(Architecture.Tile, Architecture.CoverOf(tiled));
            s.Population = popWas; s.Granary = granaryWas;
        }

        [Test]
        public void TraditionsAreCarriedByFoundersAndDriftNowAndThen()
        {
            var w = TestWorlds.Make(7);
            int origin = w.Settlements.First();
            int t0 = w.Settlements.Get(origin).Tradition;
            int same = 0, n = 400;
            for (int sid = 1000; sid < 1000 + n; sid++)
            {
                int t = Architecture.TraditionAt(w, sid, origin);
                Assert.AreEqual(Architecture.Line(t0), Architecture.Line(t), "the line it descends from is kept");
                if (t == t0) same++;
                else Assert.IsTrue(Architecture.TraditionRoof(t) != Architecture.TraditionRoof(t0) ^ Architecture.TraditionPalette(t) != Architecture.TraditionPalette(t0), "one trait changes");
            }
            Console.WriteLine(same + " of " + n + " daughter villages build as their mother did");
            Assert.That(same / (double)n, Is.InRange(1.0 - Architecture.Drift - 0.08, 1.0 - Architecture.Drift + 0.08));
            Assert.AreNotEqual(Architecture.TraditionAt(w, 2000, 0), Architecture.TraditionAt(w, 2001, 0), "first peoples their own");
        }

        [Test]
        public void AHouseholdOfMeansRebuildsItsOldPoorHouseWithItsLabourAndTheVillagesWood()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            int bid = HouseOf(w, sid, out int hid);
            Assume.That(bid != 0, "a household with a house");
            ref var s = ref w.Settlements.Get(sid);
            s.Ration = 1.0; s.Woodstore = 5000; s.Project = 0; s.Works |= Construction.Well | Construction.Granary;
            // premise: their house is forty years old and poor; they have come to some means
            ref var b = ref w.Buildings.Get(bid);
            b.Form = Architecture.Pack(Architecture.Small, Architecture.RoofOf(b.Form), Architecture.Straw, Architecture.Daub, false, 0);
            b.Built = w.Now - SimTime.Years(40);
            ref var hh = ref w.Households.Get(hid);
            hh.Larder = 1e5; hh.Gear = 20f;
            // another household, poor, in as old and poor a house: it does not rebuild
            var hl = w.HouseholdsOf(sid);
            int other = 0;
            for (int i = 0; i < hl.Count; i++) if (hl[i] != hid && w.Households.Get(hl[i]).Shelter >= Construction.House) { other = hl[i]; break; }
            if (other != 0)
            {
                ref var ho = ref w.Households.Get(other); ho.Larder = 0; ho.Gear = 0f;
                ref var bo = ref w.Buildings.Get(ho.Building); bo.Form = b.Form; bo.Built = b.Built;
            }
            Homes.WeighRebuilding(w, sid);
            Assert.AreNotEqual(0, w.Households.Get(hid).Rebuild, "they set about rebuilding");
            if (other != 0) Assert.AreEqual(0, w.Households.Get(other).Rebuild, "the poor do not");
            Assert.IsTrue(Construction.OwnWork(w, in w.Settlements.Get(sid), hid), "their hours go to it when the village has no work");
            Construction.OwnStage(in w.Households.Get(hid), out double need, out double wood);
            double wood0 = w.Settlements.Get(sid).Woodstore;
            long mark = w.History.LastId;
            int before = w.Buildings.Get(bid).Form;
            for (int k = 0; k < 200 && w.Households.Get(hid).Rebuild != 0; k++) Construction.Work(w, sid, hid, 4.0, 1.0);
            var nb = w.Buildings.Get(bid);
            Console.WriteLine("rebuilt: " + Architecture.Describe(before, true) + " -> " + Architecture.Describe(nb.Form, true) + "; " + need.ToString("0") + " h, wood " + (wood0 - w.Settlements.Get(sid).Woodstore).ToString("0"));
            Assert.AreEqual(0, w.Households.Get(hid).Rebuild, "done");
            Assert.Greater(Architecture.Gain(nb.Form, before), 0.99, "a better house");
            Assert.AreEqual(w.Now, nb.Built, "its years counted from now");
            Assert.AreEqual(wood, wood0 - w.Settlements.Get(sid).Woodstore, 1e-6, "the village's wood went into it");
            int told = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.HomeRebuilt) { told++; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.AreEqual(1, told, "told");
        }

        [Test]
        public void AVillageKeepsOneHallForEachHundredAndFiftyOfItsPeople()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            int bid = HouseOf(w, sid, out int hid);
            Assume.That(bid != 0, "a household with a house");
            ref var s = ref w.Settlements.Get(sid);
            int popWas = s.Population;
            ref var hh = ref w.Households.Get(hid);
            hh.Larder = 1e6; hh.Gear = 50f;
            s.Population = 150;
            Assert.AreEqual(Architecture.Hall, Architecture.PlanOf(Architecture.HomeForm(w, sid, hid, bid, Construction.House)), "the best off of 150 builds a hall");
            // its own hall rebuilt is not counted against it
            ref var mine = ref w.Buildings.Get(bid);
            mine.Form = Architecture.Pack(Architecture.Hall, 0, Architecture.Straw, Architecture.Daub, true, 0);
            Assert.IsTrue(Architecture.HallRoom(w, sid, bid), "its own hall is not another's");
            // another household's hall stands, lived in: no room for a second in a village of 150
            int other = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
                if (hl[i] != hid && w.Households.Get(hl[i]).Shelter >= Construction.House && w.Households.Get(hl[i]).Building != 0) { other = hl[i]; break; }
            Assume.That(other != 0, "another housed household");
            int ob = w.Households.Get(other).Building;
            w.Buildings.Get(ob).Form = Architecture.Pack(Architecture.Hall, 0, Architecture.Straw, Architecture.Daub, true, 0);
            int f = Architecture.HomeForm(w, sid, hid, bid, Construction.House);
            Console.WriteLine("150 people, another's hall standing: " + Architecture.Describe(f, true));
            Assert.AreNotEqual(Architecture.Hall, Architecture.PlanOf(f), "one hall a village");
            // a town of 300: room for two
            s.Population = 300;
            Assert.AreEqual(Architecture.Hall, Architecture.PlanOf(Architecture.HomeForm(w, sid, hid, bid, Construction.House)), "a town of 300 keeps two");
            // the other hall left empty: not counted (whoever needs a home takes it)
            s.Population = 150;
            ref var obb = ref w.Buildings.Get(ob);
            int keep = obb.Household; obb.Household = 0;
            Assert.IsTrue(Architecture.HallRoom(w, sid, bid), "an empty hall is not the village's leading house");
            obb.Household = keep;
            // a hall set out to be built while another was raised meanwhile: built a storey high instead
            s.Woodstore = 1e5;
            ref var h2 = ref w.Households.Get(hid);
            h2.Rebuild = (byte)(1 + Architecture.Hall);
            Homes.Rebuilt(w, hid);
            Console.WriteLine("rebuilt with another's hall standing: " + Architecture.Describe(w.Buildings.Get(bid).Form, true));
            Assert.AreEqual(Architecture.Two, Architecture.PlanOf(w.Buildings.Get(bid).Form));
            s.Population = popWas;
        }

        [Test]
        public void AVillageRebuildsFewHousesAtOnceAndNotWhileRationsAreShort()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Woodstore = 1e5;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Building == 0) continue;
                hh.Larder = 1e5; hh.Gear = 20f;
                ref var b = ref w.Buildings.Get(hh.Building);
                b.Form = Architecture.Pack(Architecture.Small, 0, Architecture.Straw, Architecture.Daub, false, 0); b.Built = w.Now - SimTime.Years(90);
            }
            s.Ration = 0.8;
            Homes.WeighRebuilding(w, sid);
            int rebuilding = 0;
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Rebuild != 0) rebuilding++;
            Assert.AreEqual(0, rebuilding, "short rations: nobody rebuilds");
            s.Ration = 1.0;
            Homes.WeighRebuilding(w, sid);
            for (int i = 0; i < hl.Count; i++) if (w.Households.Get(hl[i]).Rebuild != 0) rebuilding++;
            Console.WriteLine(rebuilding + " of " + hl.Count + " households rebuild at once");
            Assert.Greater(rebuilding, 0);
            Assert.LessOrEqual(rebuilding, Math.Max(1, hl.Count / 10), "a tenth of the village at most");
        }
    }
}
