using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The lord's death (C10.9, D2-173): when the one first among a lord village's people dies, the bonds for peace made with that village lapse -
    /// its tributaries owe the heir nothing until it has won them again; bonds for bread (a debt) stand; what was set aside and not carried is the
    /// tributary's again; a lapse is no refusal. Under `--oldlapse` the bonds outlive the death.
    /// </summary>
    [TestFixture]
    public class LapseTests
    {
        static World Lordship(ulong seed, out int lord, out int peace, out int bread)
        {
            var w = TestWorlds.Make(seed);
            w.AdvanceTo(w.Now + SimTime.Hour);
            lord = peace = bread = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                if (!w.Settlements.Get(sid).Inhabited || w.HouseholdsOf(sid).Count < 2) continue;
                if (lord == 0) lord = sid; else if (peace == 0) peace = sid; else if (bread == 0) bread = sid;
            }
            Assert.IsTrue(lord != 0 && peace != 0 && bread != 0, "three villages");
            Leaders.Weigh(w, lord);
            Assert.AreNotEqual(0, w.Settlements.Get(lord).Head, "the lord village has a first among its people");
            Bind(w, peace, lord, 0);
            Bind(w, bread, lord, 1);
            return w;
        }

        /// <summary>The premise's bond, as the game would tell it (a TributeAgreed record: ArgB 1 for bread).</summary>
        static void Bind(World w, int payer, int lord, int bread)
        {
            ref var s = ref w.Settlements.Get(payer);
            s.Lord = lord; s.LordSince = w.Now;
            s.LordEvent = w.Record(new HistoryEvent { Kind = HistoryKind.TributeAgreed, Time = w.Now, Cell = s.Cell, Settlement = payer, Subject = EntityRef.Settlement(payer), Object = EntityRef.Settlement(lord), ArgB = bread, RootKind = RootKind.People });
        }

        static void KillTheFirst(World w, int lord)
        {
            int head = w.Settlements.Get(lord).Head;
            ref var p = ref w.People.Get(head);
            w.Agents.Kill(ref p, 0, 1);
            Leaders.Day(w, lord);
        }

        [Test]
        public void AtTheLordsDeathTheBondsForPeaceLapseAndTheBondsForBreadStand()
        {
            var w = Lordship(1101, out int lord, out int peace, out int bread);
            w.Settlements.Get(peace).TributeDue = 50;
            double granary = w.Settlements.Get(peace).Granary;
            int first = w.Settlements.Get(lord).Head;
            int known0 = w.DeedBook.KnownCount(lord);
            KillTheFirst(w, lord);
            ref var s = ref w.Settlements.Get(lord);
            Assert.AreNotEqual(first, s.Head, "an heir is first now");
            Assert.AreEqual(0, w.Settlements.Get(peace).Lord, "the bond for peace lapsed");
            Assert.AreEqual(lord, w.Settlements.Get(bread).Lord, "the bond for bread, a debt, stands");
            Assert.AreEqual(0, w.Settlements.Get(peace).TributeDue, 1e-9, "what was set aside is the village's again");
            Assert.AreEqual(granary + 50, w.Settlements.Get(peace).Granary, 1e-6);
            // its record: caused by the heir's rising (whose cause is the death), part of the bond
            HistoryEvent end = default; bool found = false;
            for (int i = w.History.Count - 1; i >= 0 && !found; i--) { var e = w.History.At(i); if (e.Kind == HistoryKind.TributeEnded && e.Subject.Id == peace) { end = e; found = true; } }
            Assert.IsTrue(found, "the lapse is told");
            Assert.AreEqual(2, end.ArgA, "for its lord's death");
            Assert.AreEqual(s.HeadEvent, end.Cause1, "caused by the heir's rising at the death");
            Assert.IsTrue(w.History.TryGet(end.Cause1, out var rose) && rose.Cause1 != 0, "whose cause is the death");
            StringAssert.Contains("owed the heir nothing until it was won again", HistoryText.Describe(w, end));   // (session 32 text QA: told in the past like every other line)
            System.Console.WriteLine(HistoryText.Dated(w, end));
            // no refusal: nobody holds a lapse a wrong
            Assert.AreEqual(known0, w.DeedBook.KnownCount(lord), "a lapse is no refusal: the lord village learns no deed of it");
        }

        [Test]
        public void UnderTheOldRulesTheBondsOutliveTheLord()
        {
            Conflict.LapseOff = true;
            try
            {
                var w = Lordship(1103, out int lord, out int peace, out int bread);
                KillTheFirst(w, lord);
                Assert.AreEqual(lord, w.Settlements.Get(peace).Lord, "--oldlapse: the bond for peace outlives its lord");
                Assert.AreEqual(lord, w.Settlements.Get(bread).Lord);
            }
            finally { Conflict.LapseOff = false; }
        }

        [Test]
        public void AFirstWhoMerelyGivesWayFreesNobody()
        {
            // another household rising while the first lives (or the first gone to live with a child) is no death: the bonds stand
            var w = Lordship(1105, out int lord, out int peace, out int bread);
            int first = w.Settlements.Get(lord).Head;
            Leaders.Weigh(w, lord, 0, true);   // (a vacancy weighed with the first alive)
            Assert.IsTrue(w.People.Get(first).Alive);
            Assert.AreEqual(lord, w.Settlements.Get(peace).Lord, "no death, no lapse");
        }
    }
}
