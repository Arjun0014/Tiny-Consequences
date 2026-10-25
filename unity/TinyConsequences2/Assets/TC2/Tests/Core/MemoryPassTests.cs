using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The memory pass (14 §7): past the Local window the night's deeds and the ordinary are kept only for what they led to, and the names nothing kept can
    /// show leave the registry - the dead with no row, kin, work, deed or kept event to name them; old processes. Every name a kept record shows stays.
    /// </summary>
    [TestFixture]
    public class MemoryPassTests
    {
        static long Rec(HistoryLedger h, IdAllocator ids, HistoryKind k, long t, float mag = 0f, long cause = 0, EntityRef subject = default)
            => h.Record(ids, new HistoryEvent { Kind = k, Time = t, Magnitude = mag, Cause1 = cause, Subject = subject });

        [Test]
        public void OldOrdinaryEventsAreKeptOnlyForWhatTheyLedTo()
        {
            foreach (bool off in new[] { false, true })
            {
                HistoryLedger.CompactOff = off;
                try
                {
                    var h = new HistoryLedger(); var ids = new IdAllocator();
                    long blowAlone = Rec(h, ids, HistoryKind.Struck, 0);
                    long blowAnswered = Rec(h, ids, HistoryKind.Struck, 0);
                    long raid = Rec(h, ids, HistoryKind.Raid, SimTime.Day, 10f, blowAnswered);
                    long sworn = Rec(h, ids, HistoryKind.Sworn, 0, 150f);   // (a year's food: its size alone over the keep line)
                    long work = Rec(h, ids, HistoryKind.WorkBuilt, 0, 0f);   // (another Local kind: as before)
                    long young = Rec(h, ids, HistoryKind.Struck, SimTime.Years(150));
                    Assert.GreaterOrEqual(h.TryGet(sworn, out var se) ? se.Importance : 0f, 10f, "a sworn man's record scores over the keep line by its size");
                    h.Compact(SimTime.Years(201), null);
                    Assert.AreEqual(off, h.TryGet(blowAlone, out _), off ? "kept by its importance alone (the old rule)" : "a blow that led to nothing falls out after 200 years");
                    Assert.AreEqual(off, h.TryGet(sworn, out _), off ? "kept by its size" : "so does the ordinary, however large");
                    Assert.IsTrue(h.TryGet(blowAnswered, out _), "a blow a raid answered is kept");
                    Assert.IsTrue(h.TryGet(raid, out var re) && re.Cause1 == blowAnswered, "and the raid still names it as its cause");
                    Assert.IsTrue(h.TryGet(young, out _), "inside the window everything is kept");
                    Assert.IsFalse(h.TryGet(work, out _), "other kinds as before (a work's record of small importance past the window)");
                }
                finally { HistoryLedger.CompactOff = false; }
            }
        }

        [Test]
        public void NamesNothingKeptCanShowLeaveTheRegistry()
        {
            var h = new HistoryLedger(); var ids = new IdAllocator();
            EntityRef forgotten = EntityRef.Person(1), cited = EntityRef.Person(2), kin = EntityRef.Person(3), living = EntityRef.Person(4);
            var oldProcess = new EntityRef(EntityKind.Process, 5); var youngProcess = new EntityRef(EntityKind.Process, 6); var household = EntityRef.Household(7);
            h.Register(forgotten, "Ana", 0); h.EndEntity(forgotten, SimTime.Years(30));
            h.Register(cited, "Bede", 0); h.EndEntity(cited, SimTime.Years(30));
            h.Register(kin, "Cerdic", 0); h.EndEntity(kin, SimTime.Years(30));
            h.Register(living, "Dunn", 0);
            h.Register(oldProcess, "the party from Hope", 0);
            h.Register(youngProcess, "the band from Hope", SimTime.Years(80));
            h.Register(household, "the Wulfings", 0); h.EndEntity(household, SimTime.Years(30));
            Rec(h, ids, HistoryKind.Raid, SimTime.Years(10), 1f, 0, cited);
            int dropped = h.PruneNames(SimTime.Years(100), r => r.Packed == kin.Packed);
            Assert.AreEqual(2, dropped, "the dead nothing names, and the old process nothing names");
            Assert.IsFalse(h.TryGetEntity(forgotten, out _));
            Assert.IsFalse(h.TryGetEntity(oldProcess, out _));
            foreach (var r in new[] { cited, kin, living, youngProcess, household }) Assert.IsTrue(h.TryGetEntity(r, out _), r + " kept");
            HistoryLedger.CompactOff = true;
            try { h.Register(forgotten, "Ana", 0); h.EndEntity(forgotten, SimTime.Years(30)); Assert.AreEqual(0, h.PruneNames(SimTime.Years(100), r => false), "the rule off"); }
            finally { HistoryLedger.CompactOff = false; }
        }

        static World Lived75(bool off)
        {
            HistoryLedger.CompactOff = off;
            try
            {
                var w = TestWorlds.Make(32);
                long midnight = SimTime.StartOfDay(w.Now) + SimTime.Day;
                w.RequestResolution(Resolution.Coarse, midnight);
                w.RequestStepDays(12, midnight);
                w.AdvanceTo(w.Now + SimTime.Years(75));
                return w;
            }
            finally { HistoryLedger.CompactOff = false; }
        }

        [Test]
        public void EveryNameAKeptRecordShowsStaysAfterGenerations()
        {
            // the same world lived with the pass off and on: it changes no history (the annals are the same), only what the ledger keeps
            var old = Lived75(true);
            var w = Lived75(false);
            System.Console.WriteLine("year 75: names dropped " + w.History.NamesDropped + ", named " + w.History.EntityCount + " (with the pass off " + old.History.EntityCount + "), events " +
                                     w.History.Count + " (" + old.History.Count + "), people rows " + w.People.Count);
            Assert.AreEqual(old.Now, w.Now);
            Assert.AreEqual(old.LivingPeople(), w.LivingPeople(), "the same people live");
            Assert.AreEqual(old.People.Count, w.People.Count);
            Assert.Greater(w.History.NamesDropped, 0, "the long dead nothing names have left the registry");
            Assert.Less(w.History.EntityCount, old.History.EntityCount);
            // every person or process a kept event, a row, a deed or a work names has the name it had with the pass off
            int lost = 0, refs = 0;
            void Check(EntityRef r)
            {
                if (r.IsNone || (r.Kind != EntityKind.Person && r.Kind != EntityKind.Process)) return;
                refs++;
                if (!w.History.TryGetEntity(r, out _) && old.History.TryGetEntity(r, out _)) lost++;
            }
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); Check(e.Subject); Check(e.Object); Check(e.Other); }
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                Check(EntityRef.Person(id));
                if (p.Mother != 0) Check(EntityRef.Person(p.Mother));
                if (p.Father != 0) Check(EntityRef.Person(p.Father));
                if (p.Partner != 0) Check(EntityRef.Person(p.Partner));
            }
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id)) if (w.Deeds.Get(id).By != 0) Check(EntityRef.Person(w.Deeds.Get(id).By));
            for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id)) if (w.Buildings.Get(id).Leader != 0) Check(EntityRef.Person(w.Buildings.Get(id).Leader));
            System.Console.WriteLine("names checked " + refs + ", lost " + lost);
            Assert.AreEqual(0, lost, "every name a kept record shows is kept");
            // and a save continues identically with the registry pruned
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Years(2);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
            Assert.IsNull(TestWorlds.Audit(w));
        }
    }
}
