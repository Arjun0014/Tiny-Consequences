using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Charcoal (C5.2a, D2-101): a village whose smiths smelt lays out a charcoal hearth at its woodlot; its burners fell real woods there
    /// (each load off the forest share) and a load makes 5 kg of charcoal; the clamp smoulders days after; smelting burns charcoal - none in
    /// store, no copper; the hearth follows the woods as they recede. Both resolutions. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class CharcoalTests
    {
        /// <summary>Premise: someone of a village that has seen copper smelts well; its tools have no metal; ore in store.</summary>
        static World Smelting(out int sid)
        {
            var w = TestWorlds.WithCopper(out sid);
            if (w == null) return null;
            ref var s = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { ref var p = ref w.People.Get(res[i]); if (p.AgeYears(w.Now) >= 20) { p.KMetal = 0.8f; p.KPottery = 0.8f; break; } }
            s.BestMetal = 0.8f; s.Metal = 0; s.Wall = 0; s.Project = 0; s.Ore = Mining.StoreMost; s.Charcoal = 0;
            s.OreWant = (float)Mining.Want(w, sid); s.CharcoalWant = (float)Charcoal.Want(w, sid);
            return w;
        }

        [Test]
        public void AHearthIsLaidOutAtTheWoodlotWhenItsSmithsWantCharcoal()
        {
            var w = Smelting(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            ref var s = ref w.Settlements.Get(sid);
            Assert.Greater(s.CharcoalWant, 0f, "charcoal wanted for the metal its tools want");
            Assume.That(Woods.Ensure(w, sid) >= 0, "woods within reach");
            s.Mine = Structures.Standing(w, sid, Structures.Mine, 0, 0);   // (its mine stands: the next need is charcoal)
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid);
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.CharHearth, b.Kind, "a charcoal hearth begun");
            Assert.AreEqual(s.WoodX, b.X, 0.01); Assert.AreEqual(s.WoodY, b.Y, 0.01);
            // nobody who smelts: no charcoal wanted
            s.BestMetal = 0.1f;
            Assert.AreEqual(0.0, Charcoal.Want(w, sid));
        }

        [Test]
        public void BurningFellsTheWoodsAndALoadMakesFiveKilos()
        {
            var w = Smelting(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            int lot = Woods.Ensure(w, sid);
            Assume.That(lot >= 0, "woods within reach");
            ref var s = ref w.Settlements.Get(sid);
            Structures.Place(w, sid, Structures.CharHearth, out float x, out float y);
            int hearth = Structures.Standing(w, sid, Structures.CharHearth, x, y);
            double forest0 = w.Planet.Forest[lot], felled0 = w.Metrics.WoodFelled, wood0 = s.Woodstore;
            double r0 = 1.0, rate = Charcoal.HourRate(w, sid, r0, false);
            double kg = Charcoal.Burn(w, sid, hearth, 4.0, r0, false);
            Console.WriteLine("4 h of burning at the woods " + s.WoodDist.ToString("0") + " m out: " + kg.ToString("0.0") + " kg of charcoal (" + rate.ToString("0.00") + " kg an hour)");
            Assert.AreEqual(4.0 * rate, kg, 1e-6);
            Assert.AreEqual(kg / (Woods.LoadKg * Charcoal.Yield), w.Metrics.WoodFelled - felled0, 1e-6, "a load of wood for every five kilos");
            Assert.Less(w.Planet.Forest[lot], forest0, "felled from the woods");
            Assert.AreEqual(wood0, w.Settlements.Get(sid).Woodstore, 1e-9, "none of it into the hearths' woodstore");
            Assert.AreEqual(kg, w.Settlements.Get(sid).Charcoal, 1e-9);
            Assert.Greater(w.Settlements.Get(sid).ClampUntil, w.Now, "the clamp smoulders on");
            Assert.Less(rate, r0 * Woods.LoadKg * Charcoal.Yield, "the clamp's work beside the felling");
        }

        [Test]
        public void NoCharcoalNoCopper()
        {
            var w = Smelting(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            Structures.Place(w, sid, Structures.Smithy, out float x, out float y);
            int smithy = Structures.Standing(w, sid, Structures.Smithy, x, y);
            ref var s = ref w.Settlements.Get(sid);
            s.FoeWorth = 0; s.WatchEvent = 0; s.Woodstore = 10000;   // (wood for the homes' hearths is no charcoal)
            int smith = 0; var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).KMetal >= 0.8f) smith = res[i];
            Assert.IsFalse(Forge.Use(w, in w.Settlements.Get(sid)), "ore but no charcoal: nothing to smelt with");
            Assert.AreEqual(0.0, Forge.Work(w, ref w.People.Get(smith), sid, smithy, 4.0));
            Assert.AreEqual(0.0, w.Settlements.Get(sid).Metal);
            s.Charcoal = 10.0;
            Forge.Work(w, ref w.People.Get(smith), sid, smithy, 200.0);
            var s2 = w.Settlements.Get(sid);
            Assert.AreEqual(10.0 / (Charcoal.PerKg * Forge.FuelShare), s2.Metal, 1e-9, "as far as the charcoal goes");
            Assert.AreEqual(0.0, s2.Charcoal, 1e-9);
        }

        [Test]
        public void WhenTheWoodsRecedeTheNextHearthIsLaidAtTheWoods()
        {
            var w = Smelting(out int sid);
            Assume.That(w != null, "a village with copper within reach");
            Assume.That(Woods.Ensure(w, sid) >= 0, "woods within reach");
            ref var s = ref w.Settlements.Get(sid);
            s.Mine = Structures.Standing(w, sid, Structures.Mine, 0, 0);
            int old = Structures.Standing(w, sid, Structures.CharHearth, s.WoodX + 400f, s.WoodY);   // premise: the woods were cut back from it
            Assert.IsTrue(Charcoal.Far(w, sid, old));
            Structures.Plan(w, sid);
            var b = w.Buildings.Get(w.Settlements.Get(sid).Project);
            Assert.AreEqual(Structures.CharHearth, b.Kind, "the next hearth begun");
            Assert.AreEqual(s.WoodX, b.X, 0.01, "at the woods");
            Assert.AreNotEqual(0L, w.Buildings.Get(old).Left, "the old one left");
            Assert.AreEqual(0, Charcoal.Site(w, sid));
        }

        [Test]
        public void AVillageBurnsCharcoalAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = Smelting(out int sid);
                Assume.That(w != null, "a village with copper within reach");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Days(30));   // (past the first month's roofs)
                ref var s = ref w.Settlements.Get(sid);
                s.BestMetal = 0.8f; s.Metal = 0; s.Charcoal = 0; s.Wall = 0; s.Granary = 30 * s.Population;
                if (Charcoal.Site(w, sid) == 0) { Structures.Place(w, sid, Structures.CharHearth, out float x, out float y); Structures.Standing(w, sid, Structures.CharHearth, x, y); }
                // (premise, C9.1: its households have made the boats they wanted - the six days measured are not spent hollowing trunks)
                var hl = w.HouseholdsOf(sid);
                for (int i = 0; i < hl.Count; i++) if (Boats.Making(w, hl[i]) != 0) { w.Settlements.Get(sid).Woodstore += Boats.Wood; Boats.Work(w, sid, hl[i], Boats.Hours + 1.0, 1.0, 0); }
                double kg0 = w.Metrics.CharcoalKg, h0 = w.Metrics.BurnHours;
                w.AdvanceTo(w.Now + SimTime.Days(6));
                var s3 = w.Settlements.Get(sid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": charcoal " + (w.Metrics.CharcoalKg - kg0).ToString("0") + " kg in " + (w.Metrics.BurnHours - h0).ToString("0.0") + " h at " + s3.WoodDist.ToString("0") + " m; store " + s3.Charcoal.ToString("0") + " (wants " + s3.CharcoalWant.ToString("0") + "), metal " + s3.Metal.ToString("0.0") + " kg; boats begun " + w.Metrics.BoatsBegun + ", made " + w.Metrics.BoatsBuilt + ", trips " + w.Metrics.BoatTrips);
                Assert.Greater(w.Metrics.CharcoalKg - kg0, 10, (coarse ? "COARSE" : "lived") + ": charcoal burnt");
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
