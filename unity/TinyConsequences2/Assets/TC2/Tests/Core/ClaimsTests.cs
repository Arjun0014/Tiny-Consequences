using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Whose ore (C5.4, D2-105): a body is worked by the village whose mine there was raised first; another village's people dig it only
    /// when their need presses or they hold a wrong against the owners; every stint they dig there is a taking of the owners' ore, known to
    /// them, weighed as any taking is. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class ClaimsTests
    {
        /// <summary>Premise: a village works a copper body (its mine there); a neighbour of it knows the same body and its people smelt.</summary>
        static World TwoAtOneBody(out int owner, out int other, out int cell)
        {
            var w = TestWorlds.WithCopper(out owner);
            other = 0; cell = -1;
            if (w == null) return null;
            ref var so = ref w.Settlements.Get(owner);
            cell = so.OreCell - 1;
            so.BestMetal = 0.8f; so.Wall = 0; so.Project = 0;
            Structures.Place(w, owner, Structures.Mine, out float x, out float y);
            Structures.Standing(w, owner, Structures.Mine, x, y);
            other = w.Settlements.Next(owner) != 0 ? w.Settlements.Next(owner) : w.Settlements.First();
            ref var s = ref w.Settlements.Get(other);
            s.OreCell = cell + 1; s.OreDist = 1500f; s.BestMetal = 0.8f; s.Metal = 0; s.Iron = 0; s.Wall = 0; s.Project = 0; s.Mine = 0; s.Ore = 0;
            s.OreWant = (float)Mining.Want(w, other);
            return w;
        }

        [Test]
        public void ABodyIsWorkedByTheVillageWhoseMineThereWasRaisedFirst()
        {
            var w = TwoAtOneBody(out int owner, out int other, out int cell);
            Assume.That(w != null, "a village with copper within reach");
            Assert.AreEqual(owner, Mining.WorkedBy(w, cell, Mining.Copper));
            Structures.Place(w, other, Structures.Mine, out float x, out float y);
            Structures.Standing(w, other, Structures.Mine, x, y);
            Assert.AreEqual(owner, Mining.WorkedBy(w, cell, Mining.Copper), "the first mine's village still");
            Assert.IsTrue(Mining.MayDig(w, owner, Mining.Copper), "the owners dig freely");
        }

        [Test]
        public void ANeighbourDigsAnothersOreOnlyWhenItsNeedPresses()
        {
            var w = TwoAtOneBody(out int owner, out int other, out int cell);
            Assume.That(w != null, "a village with copper within reach");
            ref var s = ref w.Settlements.Get(other);
            // its people mostly have metal tools: it leaves the others' ore alone
            s.Metal = Tech.MetalPerHead * s.Population * 0.8; s.OreWant = (float)Mining.Want(w, other);
            Assume.That(s.OreWant > 0, "it still wants some copper");
            Assert.IsFalse(Mining.MayDig(w, other, Mining.Copper));
            Structures.Plan(w, other);
            Assert.AreNotEqual(Structures.Mine, w.Settlements.Get(other).Project != 0 ? w.Buildings.Get(w.Settlements.Get(other).Project).Kind : (byte)0, "no mine at another's ore");
            // most of its people without metal tools: it digs there
            ref var s2 = ref w.Settlements.Get(other); s2.Metal = 0; s2.Project = 0; s2.OreWant = (float)Mining.Want(w, other);
            Assert.IsTrue(Mining.MayDig(w, other, Mining.Copper), "need presses");
            Structures.Plan(w, other);
            var b = w.Buildings.Get(w.Settlements.Get(other).Project);
            Assert.AreEqual(Structures.Mine, b.Kind, "a mine at the others' ore");
            // or with a wrong held against the owners, whatever its need
            ref var s3 = ref w.Settlements.Get(other); s3.Metal = Tech.MetalPerHead * s3.Population * 0.8;
            int d = w.DeedBook.Record(DeedKind.Take, 0, owner, 0, other, Trade.Grain, 100, 100, 0);
            w.DeedBook.Learn(other, d, KnownHow.Saw);
            Assert.IsTrue(Mining.MayDig(w, other, Mining.Copper), "taking back is not taking to them");
        }

        [Test]
        public void DiggingAnothersOreIsATakingTheOwnersKnow()
        {
            var w = TwoAtOneBody(out int owner, out int other, out int cell);
            Assume.That(w != null, "a village with copper within reach");
            Structures.Place(w, other, Structures.Mine, out float x, out float y);
            int mine = Structures.Standing(w, other, Structures.Mine, x, y);
            int digger = w.Residents(other)[0];
            Assert.AreEqual(0.0, w.DeedBook.Grievance(owner, other));
            long mark = w.History.LastId;
            double kg = Mining.Dig(w, other, mine, 2.0, Mining.DigPerHour, false, double.MaxValue, Mining.Copper, digger);
            Assert.Greater(kg, 0.0);
            double g1 = w.DeedBook.Grievance(owner, other);
            Assert.Greater(g1, 0.0, "the owners hold it against them");
            int deed = w.Settlements.Get(other).ClaimDeed;
            var dd = w.Deeds.Get(deed);
            Assert.AreEqual(DeedKind.Take, dd.Kind); Assert.AreEqual(Trade.Metal, dd.Good); Assert.AreEqual(digger, dd.By);
            Assert.AreEqual(kg / Mining.OrePerKg, dd.Qty, 1e-4, "the copper it would have made");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.OreTaken) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // another stint within days: the same taking, heavier
            Mining.Dig(w, other, mine, 2.0, Mining.DigPerHour, false, double.MaxValue, Mining.Copper, digger);
            Assert.AreEqual(deed, w.Settlements.Get(other).ClaimDeed, "one taking");
            Assert.Greater(w.DeedBook.Grievance(owner, other), g1);
            // the owners' own digging takes from nobody
            int ownerMine = Mining.Mine(w, owner, Mining.Copper);
            int deeds0 = w.Deeds.Count;
            Mining.Dig(w, owner, ownerMine, 2.0, Mining.DigPerHour, false, double.MaxValue, Mining.Copper, w.Residents(owner)[0]);
            Assert.AreEqual(deeds0, w.Deeds.Count);
        }
    }
}
