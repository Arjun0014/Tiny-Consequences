using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Structures by kind (C1.1, D2-090): a village's works are structures on its ground, planned by need with their cause, raised by
    /// its people's labour and wood at both resolutions, and doing what they are for (a granary keeps grain; beyond the granaries'
    /// capacity it rots faster). Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class StructuresTests
    {
        static int CountKind(World w, int sid, byte kind, bool standing)
        {
            int n = 0; var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) { var b = w.Buildings.Get(bl[i]); if (b.Kind == kind && (!standing || Structures.Working(in b, w.Now))) n++; }
            return n;
        }

        [Test]
        public void TheFirstVillagesStandWithAWellAndAGranaryAsStructures()
        {
            var w = TestWorlds.Make(501);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                Assert.AreEqual(1, CountKind(w, sid, Structures.Well, true), "a well stands");
                Assert.AreEqual(1, CountKind(w, sid, Structures.Granary, true), "a granary stands");
                Assert.AreEqual(1, s.Granaries);
                Assert.AreNotEqual(0, s.Works & Construction.Well); Assert.AreNotEqual(0, s.Works & Construction.Granary);
                Assert.AreEqual(Structures.GranaryHolds, Structures.StoreCapacity(in s));
                // no home's lot within a work's clearance
                var bl = w.BuildingsOf(sid);
                for (int i = 0; i < bl.Count; i++)
                {
                    var a = w.Buildings.Get(bl[i]);
                    if (Structures.IsHome(a.Kind)) continue;
                    for (int j = 0; j < bl.Count; j++)
                    {
                        var h = w.Buildings.Get(bl[j]);
                        if (!Structures.IsHome(h.Kind)) continue;
                        double d = Math.Sqrt(((double)a.X - h.X) * ((double)a.X - h.X) + ((double)a.Y - h.Y) * ((double)a.Y - h.Y));
                        Assert.GreaterOrEqual(d, Structures.Of(a.Kind).Footprint + 6.0 - 1e-3, "homes keep clear of the " + Structures.Name(a.Kind));
                    }
                }
            }
        }

        [Test]
        public void GrainBeyondTheGranariesRotsFaster()
        {
            var w = TestWorlds.Make(502);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            double rot = 0.12 / SimTime.DaysPerYear;
            s.Granary = 0.5 * Structures.GranaryHolds;
            Structures.Rot(ref s, rot, 1);
            double keptWithin = 1.0 - s.Granary / (0.5 * Structures.GranaryHolds);
            s.Granary = 3.0 * Structures.GranaryHolds;
            Structures.Rot(ref s, rot, 1);
            double lostOver = 3.0 * Structures.GranaryHolds - s.Granary;
            double expect = Structures.GranaryHolds * rot + 2.0 * Structures.GranaryHolds * rot * Structures.HeapRot;
            Assert.AreEqual(rot, keptWithin, 1e-12, "within capacity, the store's own rate");
            Assert.AreEqual(expect, lostOver, 1e-6, "beyond it, the heaps rot HeapRot times as fast");
            // a step of days is the days lived (DEEP)
            s.Granary = 3.0 * Structures.GranaryHolds;
            Structures.Rot(ref s, rot, 5);
            double stepped = s.Granary;
            s.Granary = 3.0 * Structures.GranaryHolds;
            for (int d = 0; d < 5; d++) { double cap = Structures.StoreCapacity(in s); double over = Math.Max(0, s.Granary - cap); double kept = s.Granary - over; s.Granary = kept * (1 - rot) + over * (1 - rot * Structures.HeapRot); }
            Assert.Greater(stepped, 0);
            Assert.AreEqual(1.0, stepped / s.Granary, 0.02, "a step of five days rots about as five days lived");
        }

        [Test]
        public void AnOverfilledStoreAtHarvestBeginsAnotherGranaryWithItsCause()
        {
            var w = TestWorlds.Make(503);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.Granary = 0.5 * Structures.GranaryHolds;
            long h1 = w.Record(new HistoryEvent { Kind = HistoryKind.Harvest, Time = w.Now, Settlement = sid, Subject = EntityRef.Settlement(sid), Magnitude = 100f });
            Structures.AfterHarvest(w, sid, h1);
            Assert.AreEqual(0, w.Settlements.Get(sid).Project, "a store with room: nothing to build");
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Granary = 1.5 * Structures.GranaryHolds;   // premise: the harvest overfilled it
            long h2 = w.Record(new HistoryEvent { Kind = HistoryKind.Harvest, Time = w.Now, Settlement = sid, Subject = EntityRef.Settlement(sid), Magnitude = 9000f });
            Structures.AfterHarvest(w, sid, h2);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid, "another granary is begun");
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.Granary, b.Kind);
            Assert.AreEqual(0, b.Done, "laid out, not yet standing");
            Assert.AreEqual(h2, b.Cause, "its cause is the harvest that overfilled the store");
            Assert.IsTrue(Structures.Clear(w, sid, b.X, b.Y, 0.0) == false, "its ground is taken by it");
            // history tells it was begun and why
            bool begun = false; var evs = w.History.EventsOf(new EntityRef(EntityKind.Building, bid));
            Assert.IsNotNull(evs);
            for (int i = 0; i < evs.Count; i++) if (w.History.TryGet(evs[i], out var e) && e.Kind == HistoryKind.WorkBegun) { begun = true; Assert.AreEqual(h2, e.Cause1); Console.WriteLine(HistoryText.Line(w, e)); }
            Assert.IsTrue(begun);
            // the work: hours and wood go in; it stands when they are in, with the first worker as the one who led it
            ref var s3 = ref w.Settlements.Get(sid);
            s3.Woodstore = 100;
            int worker = w.Residents(sid)[0];
            Assert.AreEqual(Construction.Structure, Construction.NextWork(in w.Settlements.Get(sid)));
            double wood0 = w.Settlements.Get(sid).Woodstore;
            for (int k = 0; k < 40 && w.Settlements.Get(sid).Project != 0; k++) Construction.Work(w, sid, 0, 5, 1.0, worker);
            var built = w.Buildings.Get(bid);
            Assert.AreEqual(1, built.Done, "it stands");
            Assert.AreEqual(worker, built.Leader);
            Assert.AreEqual(Structures.Of(Structures.Granary).Hours, built.Hours, 1e-3);
            Assert.AreEqual(Structures.Of(Structures.Granary).Wood, wood0 - w.Settlements.Get(sid).Woodstore, 1e-3, "its wood came out of the store");
            Assert.AreEqual(2, w.Settlements.Get(sid).Granaries);
            Assert.AreEqual(2.0 * Structures.GranaryHolds, Structures.StoreCapacity(in w.Settlements.Get(sid)));
            Assert.AreNotEqual(0L, built.Event);
            Assert.IsTrue(w.History.TryGet(built.Event, out var done));
            Assert.AreEqual(HistoryKind.WorkBuilt, done.Kind);
            Console.WriteLine(HistoryText.Line(w, done));
        }

        [Test]
        public void NoWoodNoGranary()
        {
            var w = TestWorlds.Make(504);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            int bid = Structures.Begin(w, sid, Structures.Granary, 0, CauseRel.CausedBy);
            ref var s2 = ref w.Settlements.Get(sid);
            s2.Woodstore = 0;
            double used = Construction.Work(w, sid, 0, 10, 1.0, 0);
            Assert.AreEqual(0.0, used, "no wood: no progress");
            Assert.AreEqual(0f, w.Buildings.Get(bid).Hours);
        }

        [Test]
        public void ThePlannedGranaryIsRaisedInCoarseAndInLivedDays()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(505);
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                int sid = w.Settlements.First();
                ref var s = ref w.Settlements.Get(sid);
                s.Woodstore = 400; s.Granary = 30 * s.Population;   // premise: wood to build with, food enough not to go to the wild
                int bid = Structures.Begin(w, sid, Structures.Granary, 0, CauseRel.CausedBy);
                for (int d = 0; d < 30 && w.Buildings.Get(bid).Done == 0; d++)
                {
                    ref var sk = ref w.Settlements.Get(sid);
                    if (sk.Woodstore < 200) sk.Woodstore = 400;
                    w.AdvanceTo(w.Now + SimTime.Day);
                }
                var b = w.Buildings.Get(bid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": granary " + (b.Done != 0 ? "stands" : "unfinished") + " after " + (w.Now - b.Built) / (double)SimTime.Day + " days, " + b.Hours.ToString("0") + " h, led by " + b.Leader);
                Assert.AreEqual(1, b.Done, (coarse ? "COARSE" : "lived") + ": its people raised it");
                Assert.AreNotEqual(0, b.Leader);
                Assert.AreEqual(2, w.Settlements.Get(sid).Granaries);
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }

        [Test]
        public void AGranaryTheSeaTakesTakesItsCapacity()
        {
            var w = TestWorlds.Make(506);
            int sid = w.Settlements.First();
            int gran = 0; var bl = w.BuildingsOf(sid);
            for (int i = 0; i < bl.Count; i++) if (w.Buildings.Get(bl[i]).Kind == Structures.Granary) gran = bl[i];
            Assert.AreNotEqual(0, gran);
            ref var b = ref w.Buildings.Get(gran);
            b.Left = w.Now - SimTime.Years(41);   // premise: fallen (as the sea leaves it: D2-087)
            Structures.Lost(w, gran);
            Assert.AreEqual(0, w.Settlements.Get(sid).Granaries);
            Assert.AreEqual(0, w.Settlements.Get(sid).Works & Construction.Granary, "no granary stands");
            Assert.AreEqual(0.0, Structures.StoreCapacity(in w.Settlements.Get(sid)));
            // and the village plans a new one by need (a harvest to keep)
            ref var s = ref w.Settlements.Get(sid); s.LastHarvest = 100;
            Structures.Plan(w, sid);
            int p = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, p);
            Assert.AreEqual(Structures.Granary, w.Buildings.Get(p).Kind);
        }
    }
}
