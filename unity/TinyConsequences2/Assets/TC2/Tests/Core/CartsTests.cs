using System;
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
    /// Carts (C4.2, D2-122): a household whose traders keep carrying a back's load where more was spared and wanted along a worn way, or whose
    /// people carry wood and stone home from far off, makes a cart if one of its people can shape wheels - an ox cart where it keeps a grown ox;
    /// it is made in the household's hours at both resolutions; on a path or a road it carries a cart's load at a cart's pace, and wood and stone
    /// come home by the cartload; worn carts are left. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class CartsTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Premise: a household of the village with an able woodworker in a house (the first such), or 0.</summary>
        static int Wrights(World w, int sid)
        {
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count; i++)
            {
                ref var hh = ref w.Households.Get(hl[i]);
                if (hh.Building == 0) continue;
                var mem = w.Members(hl[i]);
                for (int j = 0; j < mem.Count; j++)
                {
                    ref var p = ref w.People.Get(mem[j]);
                    double a = p.AgeYears(w.Now);
                    if (a >= 18 && a < 50) { p.SkWood = 0.6f; p.Health = 1f; return hl[i]; }
                }
            }
            return 0;
        }

        static void Worn(World w, int a, int b, double walks) => w.Ways.Walk(w, a, b, walks);   // (premise: the way between them worn by walking)

        [Test]
        public void ATraderWhoseBackWasTooSmallBringsACartHome()
        {
            var w = TestWorlds.Make(641);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            Assume.That(b != 0);
            int hid = Wrights(w, a);
            Assume.That(hid != 0, "a household with a woodworker in a house");
            Worn(w, a, b, 100);   // a path
            Assert.IsTrue(Carts.Wheeled(w, a, b), "a path: wheels can go");
            for (int k = 0; k < Carts.NeedTrips - 1; k++) Carts.Want(w, hid, a, b);
            Assert.AreEqual(Carts.NoCart, w.Households.Get(hid).CartState, "a trip or two is not yet a cart");
            Carts.Want(w, hid, a, b);
            Assert.AreEqual(Carts.Wanted, w.Households.Get(hid).CartState, "its backs too small again and again: a cart wanted");
            ref var s = ref w.Settlements.Get(a); s.Woodstore = 200;
            double wood0 = s.Woodstore;
            Carts.Plan(w, a);
            var hh = w.Households.Get(hid);
            Assert.AreEqual(Carts.Making, hh.CartState, "begun");
            Assert.AreEqual(w.Stock.HasOx(hid) ? Carts.Ox : Carts.Hand, hh.CartKind, "an ox cart where it keeps a grown ox");
            Assert.AreEqual(wood0 - Carts.WoodOf(hh.CartKind), w.Settlements.Get(a).Woodstore, 1e-9, "from the store's wood");
            // the need fades: trips years apart never add up
            var w2 = TestWorlds.Make(641);
            int h2 = Wrights(w2, a);
            Worn(w2, a, b, 100);
            for (int k = 0; k < Carts.NeedTrips; k++) { Carts.Want(w2, h2, a, b); ref var hh2 = ref w2.Households.Get(h2); hh2.CartAt -= (long)((Carts.NeedYears + 0.1) * SimTime.Year); }   // (the last need aged past the years)
            Assert.AreEqual(Carts.NoCart, w2.Households.Get(h2).CartState, "trips years apart");
            // a track: no wheels, no want; nobody who can shape a wheel: no cart
            var w3 = TestWorlds.Make(641);
            int h3 = Wrights(w3, a);
            Worn(w3, a, b, 6);
            Assert.IsFalse(Carts.Wheeled(w3, a, b), "a track");
            for (int k = 0; k < 5; k++) Carts.Want(w3, h3, a, b);
            Assert.AreEqual(Carts.NoCart, w3.Households.Get(h3).CartState);
            var mem = w3.Members(h3);
            for (int j = 0; j < mem.Count; j++) { ref var q = ref w3.People.Get(mem[j]); q.SkWood = 0.1f; }
            Worn(w3, a, b, 100);
            for (int k = 0; k < 5; k++) Carts.Want(w3, h3, a, b);
            Assert.AreEqual(Carts.NoCart, w3.Households.Get(h3).CartState, "nobody can shape a wheel");
            // carrying wood home from far woods counts as well
            var w4 = TestWorlds.Make(641);
            int h4 = Wrights(w4, a);
            for (int k = 0; k < Carts.NeedTrips; k++) Carts.WantHaul(w4, h4, Carts.HaulFrom - 1);
            Assert.AreEqual(Carts.NoCart, w4.Households.Get(h4).CartState, "near woods: carried on the back");
            for (int k = 0; k < Carts.NeedTrips; k++) Carts.WantHaul(w4, h4, Carts.HaulFrom + 200);
            Assert.AreEqual(Carts.Wanted, w4.Households.Get(h4).CartState, "far woods: a cart");
        }

        [Test]
        public void NoCartIsBegunWhileHungerSendsTheVillageToTheWildAndOneGivenUpGoesBackToTheWoodpile()
        {
            // (measured: begun in a hungry village, nobody worked at it - 95 % of the carts given up in the hungry worlds were given up short, most
            // with no hour in them, their wood laid out for nothing)
            var w = TestWorlds.Make(641);
            int a = w.Settlements.First();
            int hid = Wrights(w, a);
            Assume.That(hid != 0);
            ref var hh = ref w.Households.Get(hid); hh.CartState = Carts.Wanted;
            ref var s = ref w.Settlements.Get(a); s.Woodstore = 200; s.Granary = 40 * s.Population;
            s.Ration = 0.5f;   // a deep cut: everyone who can goes out for food
            Assume.That(AgentSystem.WildNeed(in s, w.Now), Is.GreaterThan(Carts.ShortBound));
            Carts.Plan(w, a);
            Assert.AreEqual(Carts.Wanted, w.Households.Get(hid).CartState, "short: it waits, still wanted");
            Assert.AreEqual(200, w.Settlements.Get(a).Woodstore, 1e-9, "no wood laid out");
            w.Settlements.Get(a).Ration = 1f;   // fed again
            Assume.That(AgentSystem.WildNeed(in w.Settlements.Get(a), w.Now), Is.LessThanOrEqualTo(Carts.ShortBound));
            Carts.Plan(w, a);
            Assert.AreEqual(Carts.Making, w.Households.Get(hid).CartState, "fed: begun");
            double wood1 = w.Settlements.Get(a).Woodstore;
            Assert.Less(wood1, 200);
            // not made in its years: given up, its timber back on the woodpile
            ref var h1 = ref w.Households.Get(hid); h1.CartAt -= (long)((Carts.PatienceYears + 0.1) * SimTime.Year); h1.CartHours = 4f;
            byte kind = h1.CartKind;
            long given = w.Metrics.CartsGivenUp;
            Carts.Plan(w, a);
            Assert.AreEqual(given + 1, w.Metrics.CartsGivenUp);
            Assert.AreEqual(wood1 + Carts.WoodOf(kind), w.Settlements.Get(a).Woodstore, 1e-9, "the timber laid out goes back to the woodpile");
            Assert.AreNotEqual(Carts.Making, w.Households.Get(hid).CartState);
        }

        [Test]
        public void ACartIsMadeInTheHouseholdsHoursAtBothResolutions()
        {
            var days = new int[2];
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(642);
                int sid = w.Settlements.First();
                if (coarse) Coarse(w);
                int hid = Wrights(w, sid);
                Assume.That(hid != 0);
                ref var hh = ref w.Households.Get(hid);
                hh.CartState = Carts.Making; hh.CartKind = Carts.Hand; hh.CartHours = 0f; hh.CartAt = w.Now;
                ref var s = ref w.Settlements.Get(sid); s.Granary = 40 * s.Population; s.Woodstore = 500;
                bool saw = false; int d = 0;
                for (; d < 40 && w.Households.Get(hid).CartState != Carts.Made; d++)
                {
                    for (int h = 0; h < 24; h++)
                    {
                        w.AdvanceTo(w.Now + SimTime.Hour);
                        if (!coarse && !saw) { var mem = w.Members(hid); for (int j = 0; j < mem.Count; j++) { var q = w.People.Get(mem[j]); if (q.Act == ActivityKind.Build && q.Why == WhyCode.Cart) { saw = true; Console.WriteLine(HistoryText.Doing(w, in q)); } } }
                    }
                }
                days[coarse ? 0 : 1] = d;
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": the handcart made in " + d + " days (" + w.Households.Get(hid).CartHours.ToString("0") + " h)");
                Assert.AreEqual(Carts.Made, w.Households.Get(hid).CartState, "made");
                if (!coarse) Assert.IsTrue(saw, "seen making it");
                bool told = false;
                for (long id = w.History.LastId; id > 0 && !told; id--) if (w.History.TryGet(id, out var e) && e.Kind == HistoryKind.CartMade && e.Subject.Id == hid) { told = true; Console.WriteLine(HistoryText.Describe(w, in e)); }
                Assert.IsTrue(told, "history tells it");
                Assert.IsNull(TestWorlds.Audit(w));
                // a save with a cart half made continues as the world does
                var w2 = TestWorlds.Make(643);
                if (coarse) Coarse(w2);
                int h2 = Wrights(w2, w2.Settlements.First());
                ref var hh2 = ref w2.Households.Get(h2); hh2.CartState = Carts.Making; hh2.CartKind = Carts.Hand; hh2.CartAt = w2.Now;
                w2.AdvanceTo(w2.Now + SimTime.Days(2));
                var w3 = SaveFile.Load(SaveFile.Save(w2, 0, OfflineMode.Paused, 0, 0, ""), out _);
                w2.AdvanceTo(w2.Now + SimTime.Days(3)); w3.AdvanceTo(w3.Now + SimTime.Days(3));
                Assert.AreEqual(w2.StateHash(), w3.StateHash(), "saved and loaded, the same world");
            }
            Assert.That(days[0], Is.LessThanOrEqualTo(30)); Assert.That(days[1], Is.LessThanOrEqualTo(30), "a small job: days, not months");
        }

        [Test]
        public void ACartCarriesACartsLoadOnAWornWayOnly()
        {
            var w = TestWorlds.Make(644);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            Assume.That(b != 0);
            int hid = Wrights(w, a);
            Assume.That(hid != 0);
            ref var hh = ref w.Households.Get(hid);
            hh.CartState = Carts.Made; hh.CartKind = Carts.Hand; hh.CartAt = w.Now;
            Worn(w, a, b, 6);
            Assert.AreEqual(Trade.LoadKg, Carts.LoadFor(w, hid, a, b), 1e-9, "a track: a back's load");
            Worn(w, a, b, 100);
            Assert.AreEqual(Carts.HandKg, Carts.LoadFor(w, hid, a, b), 1e-9, "a path: a handcart's");
            ref var h2 = ref w.Households.Get(hid); h2.CartKind = Carts.Ox;
            Assert.AreEqual(w.Stock.HasOx(hid) ? Carts.OxKg : Trade.LoadKg, Carts.LoadFor(w, hid, a, b), 1e-9, "an ox cart only while it keeps a grown ox");
            ref var h3 = ref w.Households.Get(hid); h3.CartKind = Carts.Hand;
            // a trader of the household loads a cart's load where much is spared and wanted
            ref var sa = ref w.Settlements.Get(a); ref var sb = ref w.Settlements.Get(b);
            sa.Fibre = 2000; sb.Fibre = 0; sb.Granary = 200.0 * sb.Population; sa.Granary = 5;
            int trader = 0; var mem = w.Members(hid);
            for (int j = 0; j < mem.Count && trader == 0; j++) { var q = w.People.Get(mem[j]); if (q.AgeYears(w.Now) >= 18 && q.Health > 0.7) trader = mem[j]; }
            Assume.That(trader != 0);
            ref var p = ref w.People.Get(trader);
            Assume.That(w.Trade.Plan(a, b, out _, out _, out _, Carts.HandKg) > 0, "a deal");
            w.Trade.Load(ref p, a, b);
            double kg = Trade.KgOf(p.Cargo) * p.CargoQty;
            Console.WriteLine("loaded " + kg.ToString("0") + " kg of " + p.Cargo + " on the handcart; pace " + Carts.Pace(w, in p).ToString("0.00"));
            Assert.Greater(kg, Trade.LoadKg + 1, "more than a back's load");
            Assert.LessOrEqual(kg, Carts.HandKg + 1e-6);
            Assert.AreEqual(Carts.HandPace, Carts.Pace(w, in p), 1e-12, "at the cart's pace");
            Assert.Greater(w.Metrics.CartTrips, 0);
            // the same load on a person whose household has no cart is carried at a walk (raiders' loot, say)
            Carts.Off = true;
            try { Assert.AreEqual(1.0, Carts.Pace(w, in p), 1e-12); Assert.AreEqual(Trade.LoadKg, Carts.LoadFor(w, hid, a, b), 1e-9, "Off: the world before carts"); }
            finally { Carts.Off = false; }
        }

        [Test]
        public void WoodAndStoneComeHomeByTheCartload()
        {
            double walk = 1.25;
            foreach (double d in new[] { 200.0, 800.0, 1400.0 })
            {
                double back = Woods.Rate(1.2, d, walk), hand = Woods.Rate(1.2, d, walk, Carts.HandKg / Trade.LoadKg, Carts.HandPace), ox = Woods.Rate(1.2, d, walk, Carts.OxKg / Trade.LoadKg, Carts.OxPace);
                Console.WriteLine("woods " + d + " m: loads an hour on the back " + back.ToString("0.00") + ", by handcart " + hand.ToString("0.00") + ", by ox cart " + ox.ToString("0.00"));
                Assert.GreaterOrEqual(hand, back - 1e-12); Assert.GreaterOrEqual(ox, hand - 1e-12);
                double sBack = Stone.Rate(0.8, d, walk), sHand = Stone.Rate(0.8, d, walk, Carts.HandKg / Trade.LoadKg, Carts.HandPace);
                Assert.Greater(sHand, sBack, "stone too");
            }
            Assert.AreEqual(Woods.Rate(1.2, 800, walk), Woods.Rate(1.2, 800, walk, 1.0, 1.0), 0.0, "no cart: as before");
            // in a world: a cutter of a household with a handcart brings more loads home from far woods than one without
            var w = TestWorlds.Make(645);
            int sid = w.Settlements.First();
            int hid = Wrights(w, sid);
            Assume.That(hid != 0);
            Assume.That(Woods.Ensure(w, sid) >= 0, "woods within reach");
            ref var s = ref w.Settlements.Get(sid);
            double without = Woods.HourRate(w, sid, 1.2, false, hid);
            ref var hh = ref w.Households.Get(hid); hh.CartState = Carts.Made; hh.CartKind = Carts.Hand; hh.CartAt = w.Now;
            double with = Woods.HourRate(w, sid, 1.2, false, hid);
            Console.WriteLine("its woodlot " + w.Settlements.Get(sid).WoodDist.ToString("0") + " m: " + without.ToString("0.00") + " -> " + with.ToString("0.00") + " loads an hour");
            Assert.GreaterOrEqual(with, without);
        }

        [Test]
        public void WornCartsAreLeftAndTheHeirTakesACart()
        {
            var w = TestWorlds.Make(646);
            int sid = w.Settlements.First();
            int hid = Wrights(w, sid);
            Assume.That(hid != 0);
            ref var hh = ref w.Households.Get(hid);
            hh.CartState = Carts.Made; hh.CartKind = Carts.Hand; hh.CartAt = w.Now - (long)(Carts.HandYears * SimTime.Year) - 1;
            Assert.IsTrue(Carts.WornOut(in w.Households.Get(hid), w.Now));
            Assert.AreEqual(Carts.None, Carts.Usable(w, hid), "worn: not taken out");
            Assert.AreEqual(Carts.Made, w.Households.Get(hid).CartState, "reading changes nothing");
            Carts.Plan(w, sid);
            Assert.AreEqual(Carts.NoCart, w.Households.Get(hid).CartState, "left at the day's planning");
            Assert.Greater(w.Metrics.CartsWorn, 0);
        }
    }
}
