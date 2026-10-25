using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Stone walls (C7.2): a walled village that knows rock at hand and loses much to other villages' people walls its ring in dry stone when the curtain is
    /// worth its labour over the century it stands; its people lay it stretch by stretch from the quarry's rubble after every other work; stone does not rot
    /// and triples the defenders' ground; the stretches laid stand when the timber between them falls. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class StoneWallTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static World Pair(out int a, out int b)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            a = w.Settlements.First(); b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s))
                if (Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(s).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, s) != null) { b = s; break; }
            return w;
        }

        static int Grown(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); double age = p.AgeYears(w.Now); if (p.Alive && age >= 18 && age < 50) return res[i]; }
            return 0;
        }

        /// <summary>Premise: b stands behind a sound palisade round all its homes, and its people know rock 200 m off (`rock` false: none known).</summary>
        static void Walled(World w, int b, bool rock)
        {
            double r = w.Conflict.WallRadius(b);
            ref var s = ref w.Settlements.Get(b);
            s.Wall = 2; s.WallCond = 1f; s.WallR = (float)r; s.WallKeep = 1; s.Works |= Construction.Palisade;
            s.WallDry = (float)Structures.DryShare(w, in s, r);
            if (rock) { s.StoneCell = s.Cell + 1; s.StoneDist = 200f; }
            else { s.StoneCell = 0; s.StoneDist = 0f; }
        }

        /// <summary>Premise: a's band fell on b's store and carried off much this year (b saw them): heavy losses.</summary>
        static long Raided(World w, int a, int b, double worth)
        {
            long ev = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Settlement = b, Subject = EntityRef.Settlement(a), Object = EntityRef.Settlement(b) });
            int raid = w.DeedBook.Record(DeedKind.Raid, Grown(w, a), a, 0, b, Trade.Grain, worth, worth, ev);
            w.DeedBook.Learn(b, raid, KnownHow.Saw);
            return ev;
        }

        static HistoryEvent Find(World w, long mark, HistoryKind kind, int sid, int argB)
        {
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == kind && e.Settlement == sid && e.ArgB == argB) return e; }
            return default;
        }

        /// <summary>A walled village with rock at hand and heavy losses, its stone wall begun (the rule's own weighing, after a day).</summary>
        static World Begun(out int a, out int b, out long raid, out long mark)
        {
            var w = Pair(out a, out b);
            Assume.That(b != 0, "two villages a walk apart");
            // (its first works - a well, a granary - raised, as the world raises them: the stone waits for every other work)
            Coarse(w);
            for (int d = 0; d < 240 && (w.Settlements.Get(b).Project != 0 || w.Settlements.Get(b).Granaries == 0); d++) w.AdvanceTo(w.Now + SimTime.Day);
            Assume.That(w.Settlements.Get(b).Project == 0, "its first works raised");
            w.AdvanceTo(w.Now + SimTime.Day);
            Walled(w, b, rock: true);
            raid = Raided(w, a, b, 1500.0);
            mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Day);
            return w;
        }

        [Test]
        public void AHeavilyRaidedVillageWithRockAtHandWallsItsRingInStone()
        {
            Conflict.StoneWallsOff = false;
            var w = Begun(out int a, out int b, out long raid, out long mark);
            ref var s0 = ref w.Settlements.Get(b);
            Assert.AreNotEqual(0L, s0.StoneWallEvent, "the curtain is worth more than its labour over its century: begun");
            var planned = Find(w, mark, HistoryKind.WallPlanned, b, 2);
            Assert.AreEqual(s0.StoneWallEvent, planned.Id);
            Assert.AreEqual(raid, planned.Cause1, "in response to the raid it lived through");
            Assert.AreEqual(s0.StoneEvent, planned.Cause2, "enabled by the rock its people know");
            Console.WriteLine(HistoryText.Line(w, planned));
            StringAssert.Contains("dry stone", HistoryText.Describe(w, in planned));
            Assert.Greater(Stone.Want(w, b), Stone.FootingLoads * Stone.AheadHouses - 1e-6, "stone is wanted for the curtain's rubble beside the footings");
            byte next = Construction.NextWork(in s0);
            Assert.IsTrue(Construction.Laying(in s0) && (next == Construction.StoneWork || next == Construction.Structure),
                          "the stone is the village's work now - or after the works under way (the quarry it opens at its rock, a training ground)");
            // its people lay it from the store's rubble (premise: the quarry's stone kept in store, the woodstore kept for any mending)
            double ground0 = w.Conflict.Ground(b);
            bool rose = false;
            for (int d = 0; d < 400 && w.Settlements.Get(b).StoneWall < 1f; d++)
            {
                ref var sb = ref w.Settlements.Get(b);
                sb.Stone = Math.Max(sb.Stone, 4000.0); sb.Woodstore = Math.Max(sb.Woodstore, 400.0);
                float before = sb.StoneWall;
                w.AdvanceTo(w.Now + SimTime.Day);
                if (!rose && before > 0f && w.Settlements.Get(b).StoneWall < 1f) { Assert.Greater(w.Conflict.Ground(b), ground0, "each stretch laid stands the stronger"); rose = true; }
            }
            Assert.IsTrue(rose, "laid over days, stretch by stretch");
            ref var s = ref w.Settlements.Get(b);
            Console.WriteLine("ring " + Construction.RingLength(in s).ToString("0") + " m, laid " + s.StoneWall.ToString("0.00") + "; the defenders' ground x" + w.Conflict.Ground(b).ToString("0.0"));
            Assert.AreEqual(1f, s.StoneWall, "every stretch laid in stone");
            var built = Find(w, mark, HistoryKind.WallBuilt, b, 2);
            Assert.AreEqual(planned.Id, built.Cause1, "finished: its cause the beginning");
            Console.WriteLine(HistoryText.Line(w, built));
            Assert.AreEqual(3.0 * Conflict.HomeGround, w.Conflict.Ground(b), 0.01, "a stone curtain triples the defenders' ground");
            Assert.AreNotEqual(Construction.StoneWork, Construction.NextWork(in s), "nothing left to lay");
            // stone does not rot: a year on it stands as sound, and nothing is mended
            w.AdvanceTo(w.Now + SimTime.Year);
            Assert.IsTrue(Conflict.Walled(in w.Settlements.Get(b), out double cond) && cond >= 1.0 - 1e-9, "sound after a year");
            Assert.AreEqual(3.0 * Conflict.HomeGround, w.Conflict.Ground(b), 0.01);
            StringAssert.Contains("Walled in stone", Gazetteer.All(w, b));
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void NoStoneWithoutRockAtHandLossesOrWithTheRuleOff()
        {
            foreach (int k in new[] { 0, 1, 2 })
            {
                Conflict.StoneWallsOff = k == 2;
                try
                {
                    var w = Pair(out int a, out int b);
                    Assume.That(b != 0, "two villages a walk apart");
                    Coarse(w);
                    w.AdvanceTo(w.Now + SimTime.Day);
                    Walled(w, b, rock: k != 0);
                    if (k != 1) Raided(w, a, b, 1500.0);
                    w.AdvanceTo(w.Now + SimTime.Days(3));
                    Assert.AreEqual(0L, w.Settlements.Get(b).StoneWallEvent, k == 0 ? "no rock known within reach" : k == 1 ? "losses too small for the labour" : "the rule off");
                    Assert.AreNotEqual(Construction.StoneWork, Construction.NextWork(in w.Settlements.Get(b)));
                }
                finally { Conflict.StoneWallsOff = false; }
            }
        }

        [Test]
        public void TheVillagersLayTheStoneThemselves()
        {
            Conflict.StoneWallsOff = false;
            var w = Begun(out int a, out int b, out _, out _);
            Assume.That(w.Settlements.Get(b).StoneWallEvent != 0, "the stone wall begun");
            // (its quarry opened first, as the world opens it; then lived days)
            for (int d = 0; d < 200 && Construction.NextWork(in w.Settlements.Get(b)) != Construction.StoneWork; d++) { ref var sq = ref w.Settlements.Get(b); sq.Woodstore = Math.Max(sq.Woodstore, 400.0); w.AdvanceTo(w.Now + SimTime.Day); }
            Assume.That(Construction.NextWork(in w.Settlements.Get(b)) == Construction.StoneWork, "the stone the village's work");
            w.RequestResolution(Resolution.Activity, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(SimTime.StartOfDay(w.Now + w.LocalOffsetOf(b)) + SimTime.Day + SimTime.Hours(8) - w.LocalOffsetOf(b));
            string doing = "";
            long end = w.Now + SimTime.Days(3);
            while (w.Now < end && doing.Length == 0)
            {
                ref var sb = ref w.Settlements.Get(b);
                sb.Stone = Math.Max(sb.Stone, 4000.0); sb.Woodstore = Math.Max(sb.Woodstore, 400.0);
                w.AdvanceTo(w.Now + 900);
                var res = w.Residents(b);
                for (int i = 0; i < res.Count; i++) { var q = w.People.Get(res[i]); if (q.Act == ActivityKind.Build && q.Why == WhyCode.VillageWork) { doing = HistoryText.Doing(w, in q); break; } }
            }
            Console.WriteLine(doing);
            Assert.IsTrue(doing.Contains("laying stone"), "someone lays stone in the walls");
            Assert.Greater(w.Settlements.Get(b).StoneWall, 0f, "and stretches are laid");
        }

        [Test]
        public void TheStretchesLaidStandWhenTheTimberBetweenThemFalls()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            Walled(w, b, rock: false);
            // premise: half the ring laid in stone long ago, its laying given up; the timber between rotted almost through
            ref var s = ref w.Settlements.Get(b);
            s.StoneWall = 0.5f; s.StoneWallEvent = 0; s.WallKeep = 0; s.WallCond = 0.0001f;
            float r = s.WallR;
            w.AdvanceTo(w.Now + SimTime.Days(2));
            ref var s2 = ref w.Settlements.Get(b);
            Assert.AreEqual(3, s2.Wall, "the timber has fallen: the ring is breached");
            Assert.IsFalse(Conflict.Walled(in s2, out _));
            Assert.AreEqual(r, s2.OldR, 1e-3, "the stone stretches stand as an old ring");
            Assert.AreEqual(0.5f, s2.OldStone, 1e-6);
            w.AdvanceTo(w.Now + SimTime.Year);
            Assert.AreEqual(r, w.Settlements.Get(b).OldR, 1e-3, "and do not rot away");
            StringAssert.Contains("old ring of stone", Gazetteer.All(w, b));
        }

        [Test]
        public void ASaveTakenWhileTheStoneIsBeingLaidContinuesIdentically()
        {
            Conflict.StoneWallsOff = false;
            var w = Begun(out int a, out int b, out _, out _);
            Assume.That(w.Settlements.Get(b).StoneWallEvent != 0, "the stone wall begun");
            for (int d = 0; d < 200 && w.Settlements.Get(b).StoneWall <= 0f; d++) { ref var sb = ref w.Settlements.Get(b); sb.Stone = Math.Max(sb.Stone, 4000.0); sb.Woodstore = Math.Max(sb.Woodstore, 400.0); w.AdvanceTo(w.Now + SimTime.Day); }
            Assume.That(w.Settlements.Get(b).StoneWall > 0f && w.Settlements.Get(b).StoneWall < 1f, "part laid");
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            long t = w.Now + SimTime.Days(4);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }
    }
}
