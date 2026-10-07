using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Knowledge;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The smithy (C1.4, D2-093): raised where a competent smith has use for metal; a smith there works twice the open hearth's pace on
    /// less fuel; arms are forged only there, by a village that fears its neighbours, and its fighters strike harder with them.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class ForgeTests
    {
        static World WorldWithOre(out int sid)
        {
            for (ulong seed = 481; seed < 540; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    if (w.Learning.Ore(w.Settlements.Get(sid).Cell)) return w;
            }
            sid = 0;
            return null;
        }

        /// <summary>Premise: one grown person of the village is a competent smith (the village knows the technique).</summary>
        static int MakeSmith(World w, int sid)
        {
            var res = w.Residents(sid);
            int smith = 0;
            for (int i = 0; i < res.Count && smith == 0; i++) { var p = w.People.Get(res[i]); double a = p.AgeYears(w.Now); if (a >= 20 && a < 50) smith = res[i]; }
            ref var q = ref w.People.Get(smith); q.KMetal = 0.9f;
            ref var s = ref w.Settlements.Get(sid);
            s.TechKnown |= (byte)Forge.Bit; s.BestMetal = 0.9f;
            s.SmithEvent = w.Record(new HistoryEvent { Kind = HistoryKind.TechniqueKnown, Time = w.Now, Settlement = sid, Subject = EntityRef.Settlement(sid), Object = EntityRef.Person(smith), ArgA = Tech.Metal });
            return smith;
        }

        static int StandingSmithy(World w, int sid)
        {
            Structures.Place(w, sid, Structures.Smithy, out float x, out float y);
            return Structures.Standing(w, sid, Structures.Smithy, x, y);
        }

        [Test]
        public void ASmithyIsRaisedWhereASmithHasUseForMetal()
        {
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            MakeSmith(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Metal = 0; s.Project = 0; s.Wall = 0;
            Assert.IsTrue(Forge.Use(w, in w.Settlements.Get(sid)), "tools short of metal, ore near");
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid, "a smithy begun");
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.Smithy, b.Kind);
            Assert.AreEqual(w.Settlements.Get(sid).SmithEvent, b.Cause, "because they have a smith");
            double r = Math.Sqrt(((double)b.X - s.SquareX) * ((double)b.X - s.SquareX) + ((double)b.Y - s.SquareY) * ((double)b.Y - s.SquareY));
            Assert.Greater(r, Structures.HomesReach(w, sid), "at the edge of the homes, on the road (fire away from thatch)");
            // no smith: no smithy
            var w2 = WorldWithOre(out int sid2);
            ref var s2 = ref w2.Settlements.Get(sid2); s2.TechKnown = (byte)(s2.TechKnown & ~Forge.Bit); s2.Project = 0; s2.Metal = 0;
            Structures.Plan(w2, sid2);
            Assert.AreNotEqual(Structures.Smithy, w2.Settlements.Get(sid2).Project != 0 ? w2.Buildings.Get(w2.Settlements.Get(sid2).Project).Kind : (byte)0, "no smith, no smithy");
        }

        [Test]
        public void AtTheSmithyASmithMakesTwiceTheMetalOnLessFuel()
        {
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            int smith = MakeSmith(w, sid);
            int fid = StandingSmithy(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Metal = 0; s.Woodstore = 500; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0;   // (no fear: metal for tools)
            double m0 = s.Metal, wood0 = s.Woodstore;
            ref var p = ref w.People.Get(smith);
            double used = Forge.Work(w, ref p, sid, fid, 3.0);
            double kg = w.Settlements.Get(sid).Metal - m0, fuel = wood0 - w.Settlements.Get(sid).Woodstore;
            Assert.AreEqual(3.0, used, 1e-9);
            Assert.AreEqual(Tech.MetalPerHour(in w.People.Get(smith)) * Forge.Pace * 3.0, kg, 1e-6, "twice the open hearth's kilos an hour");
            Assert.AreEqual(kg * Tech.MetalFuel * Forge.FuelShare, fuel, 1e-6, "on three-quarters of its fuel a kilo");
            Assert.AreEqual(0.0, w.Settlements.Get(sid).Arms, "no fear, no arms");
            Assert.Greater(w.Buildings.Get(fid).Use, 0f, "the smithy's hours");
        }

        [Test]
        public void AThreatenedVillageForgesArmsAndItsFightersStrikeHarder()
        {
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            int smith = MakeSmith(w, sid);
            int fid = StandingSmithy(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Woodstore = 500; s.Arms = 0; s.Metal = Tech.MetalPerHead * s.Population;   // (premise: its tools are metal already - the strike's other factor held)
            int fighter = w.Residents(sid)[0];
            double before = w.Conflict.Strength(in w.People.Get(fighter));
            Assert.AreEqual(0.0, Forge.ArmsWant(in w.Settlements.Get(sid)), "no fear yet");
            ref var s2 = ref w.Settlements.Get(sid); s2.Wall = 2;   // premise: it has walled itself in: it fears its neighbours
            Assert.Greater(Forge.ArmsWant(in w.Settlements.Get(sid)), 0.0);
            long mark = w.History.LastId;
            for (int k = 0; k < 20 && w.Settlements.Get(sid).Arms < Forge.ArmsWant(in w.Settlements.Get(sid)) - 1e-6; k++) { ref var p = ref w.People.Get(smith); Forge.Work(w, ref p, sid, fid, 4.0); }
            var s3 = w.Settlements.Get(sid);
            Assert.AreEqual(Forge.ArmsWant(in s3), s3.Arms, 1e-6, "arms for its fighters");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.ArmsMade) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told, "the arming is history");
            double after = w.Conflict.Strength(in w.People.Get(fighter));
            Assert.AreEqual(before * 1.3, after, 1e-9, "armed, its fighters strike 1.3 times as hard");
            // arms wear out
            ref var s4 = ref w.Settlements.Get(sid);
            double a0 = s4.Arms;
            s4.Arms *= SettlementSystem.Keep(Forge.ArmsDecay / SimTime.DaysPerYear, SimTime.DaysPerYear);
            Assert.AreEqual(a0 * (1.0 - Forge.ArmsDecay), w.Settlements.Get(sid).Arms, a0 * 0.002, "about 3 % a year");
        }

        [Test]
        public void WithoutASmithyNoArmsAreForged()
        {
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            int smith = MakeSmith(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Wall = 2; s.Arms = 0; s.Smithy = 0;
            Assert.IsFalse(Forge.Wanted(w, in w.Settlements.Get(sid), in w.People.Get(smith), out _), "no smithy: no work there");
            // the world lives on a few days: its smith keeps to the open hearth (tools), no arms
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assert.AreEqual(0.0, w.Settlements.Get(sid).Arms, "arms are forged only at a smithy");
        }

        [Test]
        public void SmithsWorkAtTheSmithyAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = WorldWithOre(out int sid);
                Assume.That(w != null, "a village with copper ore near");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                MakeSmith(w, sid);
                int fid = StandingSmithy(w, sid);
                ref var s = ref w.Settlements.Get(sid);
                s.Metal = 0; s.Woodstore = 600; s.Granary = 30 * s.Population; s.Wall = 2;   // premise: tools short, walls up (arms wanted), food and wood enough
                bool sawSmith = false;
                for (int h = 0; h < 6 * 24; h++)
                {
                    { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); sk.WallCond = 1f; }   // (premise: the fields need no hands now; the wall stands)
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    if (!coarse && !sawSmith) { var res = w.Residents(sid); for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Act == ActivityKind.Smith) { sawSmith = true; Console.WriteLine(HistoryText.Doing(w, in w.People.Get(res[i]))); } }
                }
                var s2 = w.Settlements.Get(sid);
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": smithy hours " + w.Metrics.SmithyHours.ToString("0") + ", arms " + s2.Arms.ToString("0.0") + " (want " + Forge.ArmsWant(in s2).ToString("0") + "), metal " + s2.Metal.ToString("0.0") + " kg");
                Assert.Greater(w.Metrics.SmithyHours, 3, (coarse ? "COARSE" : "lived") + ": the smith worked at the smithy");
                Assert.Greater(s2.Arms, 0.5, "arms forged");
                if (!coarse) Assert.IsTrue(sawSmith, "seen at the smithy");
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
