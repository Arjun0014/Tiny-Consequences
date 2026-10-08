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
        /// <summary>Premise: a village whose people have seen copper and have dug ore and carried it home (C5.1).</summary>
        static World WorldWithOre(out int sid)
        {
            var w = TestWorlds.WithCopper(out sid);
            if (w != null) { w.Settlements.Get(sid).Ore = Mining.StoreMost; w.Settlements.Get(sid).Charcoal = Charcoal.StoreMost; }   // (and charcoal burnt: C5.2a)
            return w;
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
            double m0 = s.Metal, char0 = s.Charcoal;
            ref var p = ref w.People.Get(smith);
            double hand = Forge.Hand(in p);   // (a new hand: practice quickens it, C1.4b)
            double used = Forge.Work(w, ref p, sid, fid, 3.0);
            double kg = w.Settlements.Get(sid).Metal - m0, fuel = char0 - w.Settlements.Get(sid).Charcoal;
            Assert.AreEqual(3.0, used, 1e-9);
            Assert.AreEqual(Tech.MetalPerHour(in w.People.Get(smith)) * Forge.Pace * hand * 3.0, kg, 1e-6, "twice the open hearth's kilos an hour");
            Assert.AreEqual(kg * Charcoal.PerKg * Forge.FuelShare, fuel, 1e-6, "on three-quarters of the open hearth's charcoal a kilo");
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

        /// <summary>Premise: the village's smiths are exactly these grown people with these crafts in copper (everyone else knows none).</summary>
        static int[] Smiths(World w, int sid, params float[] crafts)
        {
            var res = w.Residents(sid);
            var ids = new System.Collections.Generic.List<int>();
            for (int i = 0; i < res.Count; i++)
            {
                ref var q = ref w.People.Get(res[i]);
                q.KMetal = 0f; q.KIron = 0f; q.SkSmith = 0f;
                double a = q.AgeYears(w.Now);
                if (ids.Count < crafts.Length && q.Alive && a >= 18 && a < 55) { q.KMetal = crafts[ids.Count]; q.Health = 1f; ids.Add(res[i]); }
            }
            return ids.ToArray();
        }

        [Test]
        public void TheSmithysPlacesAreItsBestSmithsAndPracticeMakesThem()
        {
            // (C1.4b, D2-125: before, every smith who could work copper worked at the smithy in the order its people came - up to 355 hands in a
            // smithy-year, none of them its smith)
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            int[] sm = Smiths(w, sid, 0.6f, 0.7f, 0.8f, 0.9f, 0.55f, 0.65f);
            Assume.That(sm.Length, Is.EqualTo(6), "six grown people");
            int fid = StandingSmithy(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Metal = 0; s.Woodstore = 500; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0;   // (tools short of metal; no fear)
            Forge.Bar(w, sid);
            bool[] maker = new bool[sm.Length];
            for (int k = 0; k < sm.Length; k++) maker[k] = Forge.Maker(in w.Settlements.Get(sid), in w.People.Get(sm[k]), w.Now);
            CollectionAssert.AreEqual(new[] { false, true, true, true, false, true }, maker, "its four best smiths have its places");
            Assert.IsTrue(Forge.Wanted(w, in w.Settlements.Get(sid), in w.People.Get(sm[3]), out _), "the best smith: work at the smithy");
            Assert.IsFalse(Forge.Wanted(w, in w.Settlements.Get(sid), in w.People.Get(sm[0]), out _), "a fifth smith: no place there");
            // practice makes a smith: a practised hand outranks a better-taught one
            { ref var q = ref w.People.Get(sm[0]); q.SkSmith = 0.5f; }
            Forge.Bar(w, sid);
            Assert.IsTrue(Forge.Maker(in w.Settlements.Get(sid), in w.People.Get(sm[0]), w.Now), "practised: a place");
            Assert.IsFalse(Forge.Maker(in w.Settlements.Get(sid), in w.People.Get(sm[5]), w.Now), "the fourth best before: none now");
            // practice quickens the hand, and hours at the smithy are practice
            Assert.AreEqual(0.8, Forge.Hand(in w.People.Get(sm[3])), 1e-12, "a new hand");
            { ref var q = ref w.People.Get(sm[3]); q.SkSmith = 1f; }
            Assert.AreEqual(1.2, Forge.Hand(in w.People.Get(sm[3])), 1e-12, "a practised hand");
            { ref var q = ref w.People.Get(sm[3]); q.SkSmith = 0f; }
            double used = Forge.Work(w, ref w.People.Get(sm[3]), sid, fid, 6.0);
            Assert.Greater(used, 0);
            Assert.AreEqual(Forge.SkillRate * used, w.People.Get(sm[3]).SkSmith, 1e-6, "practice by the hour");
            // the law before (tools): every smith who can work copper, at an untaught pace
            Forge.CrewOff = true;
            try
            {
                Assert.IsTrue(Forge.Wanted(w, in w.Settlements.Get(sid), in w.People.Get(sm[5]), out _));
                Assert.AreEqual(1.0, Forge.Hand(in w.People.Get(sm[0])), 1e-12);
            }
            finally { Forge.CrewOff = false; }
        }

        [Test]
        public void AVillageWhoseSmithsCannotKeepUpRaisesAnotherSmithy()
        {
            // (C1.4b: with four places a smithy-year's work tops out near 2,000 hours - iron towns smelted 40 % less and sold a third of their iron;
            // before, a town's whole people had worked at one smithy in a year of iron)
            var w = WorldWithOre(out int sid);
            Assume.That(w != null, "a village with copper ore near");
            int[] sm = Smiths(w, sid, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f, 0.58f, 0.56f);
            Assume.That(sm.Length, Is.EqualTo(10), "ten smiths");
            int fid = StandingSmithy(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            s.Metal = 0; s.Woodstore = 500; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0; s.Project = 0; s.BestMetal = 0.95f; s.SmithShort = 0; s.SmithMore = 0;
            Assert.AreEqual(1, s.Smithies);
            Assert.AreEqual(Forge.Places, Forge.PlacesOf(in s));
            // ore or charcoal short is not the smiths' shortfall
            double ore0 = s.Ore; s.Ore = 0;
            Forge.Tally(w, sid, 1);
            Assert.AreEqual(0, w.Settlements.Get(sid).SmithShort, "no ore to smelt: the smiths are not short");
            ref var s1 = ref w.Settlements.Get(sid); s1.Ore = ore0;
            Assume.That(Forge.Backlog(in w.Settlements.Get(sid)), Is.GreaterThan(Forge.Places * AgentSystem.LabourDay), "more work than four can do in a day");
            for (int d = 0; d < Forge.ShortDays; d++) Forge.Tally(w, sid, 1);
            Assert.AreEqual(Forge.ShortDays, w.Settlements.Get(sid).SmithShort);
            // short only while it first equips its people: work done once, over some years - no second smithy
            Assert.IsFalse(Forge.NeedsMore(in w.Settlements.Get(sid)), "its tools' upkeep is a smith's few weeks a year");
            Forge.Year(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).SmithMore, "a first equipping is no reason for another smithy");
            // premise: it smelts iron and sells it - buyers keep its smiths at the anvil year after year
            { ref var sx = ref w.Settlements.Get(sid); sx.BestIron = 0.9f; sx.IronSold = 400f; }
            Assert.IsTrue(Forge.NeedsMore(in w.Settlements.Get(sid)), "upkeep and sales beyond its places' year");
            for (int d = 0; d < Forge.ShortDays; d++) Forge.Tally(w, sid, 1);
            Forge.Year(w, sid);
            Assert.AreEqual(1, w.Settlements.Get(sid).SmithMore, "short most of the year, smiths enough for more places: another smithy wanted");
            Assert.AreEqual(0, w.Settlements.Get(sid).SmithShort, "the count starts again");
            Assert.IsTrue(Forge.MoreWanted(in w.Settlements.Get(sid)));
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid, "begun");
            Assert.AreEqual(Structures.Smithy, w.Buildings.Get(bid).Kind, "a second smithy");
            Assert.AreEqual(w.Buildings.Get(fid).Event, w.Buildings.Get(bid).Cause, "because of the first's shortfall");
            // raised: eight places, its eight best smiths
            Structures.Place(w, sid, Structures.Smithy, out float x2, out float y2);
            int fid2 = Structures.Standing(w, sid, Structures.Smithy, x2, y2);
            Assert.AreEqual(2, w.Settlements.Get(sid).Smithies);
            Assert.AreEqual(fid, w.Settlements.Get(sid).Smithy, "the first stays the first");
            Assert.AreEqual(2 * Forge.Places, Forge.PlacesOf(in w.Settlements.Get(sid)));
            int makers = 0;
            foreach (int id in sm) if (Forge.Maker(in w.Settlements.Get(sid), in w.People.Get(id), w.Now)) makers++;
            Assert.AreEqual(2 * Forge.Places, makers, "its eight best smiths");
            // each works at the smithy nearest home
            foreach (int id in sm)
            {
                int at = Forge.SmithyFor(w, in w.Settlements.Get(sid), in w.People.Get(id));
                Assert.IsTrue(at == fid || at == fid2);
                ref var hh = ref w.Households.Get(w.People.Get(id).Household);
                var b1 = w.Buildings.Get(fid); var b2 = w.Buildings.Get(fid2);
                double d1 = (b1.X - hh.HomeX) * (double)(b1.X - hh.HomeX) + (b1.Y - hh.HomeY) * (double)(b1.Y - hh.HomeY), d2 = (b2.X - hh.HomeX) * (double)(b2.X - hh.HomeX) + (b2.Y - hh.HomeY) * (double)(b2.Y - hh.HomeY);
                Assert.AreEqual(d1 <= d2 ? fid : fid2, at, "the nearer");
            }
            // a smithy lost: one fewer; the first lost, the other takes its place
            Structures.Lost(w, fid);
            Assert.AreEqual(1, w.Settlements.Get(sid).Smithies);
            Assert.AreEqual(fid2, w.Settlements.Get(sid).Smithy);
            // too few smiths for more places: none wanted
            ref var s3 = ref w.Settlements.Get(sid); s3.SmithShort = (byte)Forge.ShortDays; s3.SmithMore = 0;
            Assume.That(Forge.NeedsMore(in s3), "still selling");
            foreach (int id in sm) { ref var q = ref w.People.Get(id); if (q.KMetal < 0.8f) q.KMetal = 0.2f; }
            Forge.Year(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).SmithMore, "four able smiths for four places: no more smithies");
        }

        [Test]
        public void OnlyItsBestSmithsWorkAtTheSmithyAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = WorldWithOre(out int sid);
                Assume.That(w != null, "a village with copper ore near");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                int[] sm = Smiths(w, sid, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f, 0.55f);
                Assume.That(sm.Length, Is.EqualTo(8), "eight smiths");
                int fid = StandingSmithy(w, sid);
                ref var s = ref w.Settlements.Get(sid);
                s.Metal = 0; s.Woodstore = 600; s.Granary = 30 * s.Population; s.Wall = 0; s.FoeWorth = 0; s.WatchEvent = 0;   // premise: tools short, much to smelt; food and wood enough
                var worked = new System.Collections.Generic.HashSet<int>();
                for (int h = 0; h < 6 * 24; h++)
                {
                    { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); sk.Metal = 0; sk.Ore = Mining.StoreMost; sk.Charcoal = Charcoal.StoreMost; }   // (the fields need no hands; the work never runs out)
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    foreach (int id in sm) if (w.People.Get(id).SkSmith > 0f) worked.Add(id);   // (hours at the smithy are practice)
                }
                var names = new System.Collections.Generic.List<string>();
                foreach (int id in worked) names.Add(id + " (" + w.People.Get(id).KMetal.ToString("0.00") + ")");
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": smithy hours " + w.Metrics.SmithyHours.ToString("0") + " by " + string.Join(", ", names));
                Assert.Greater(w.Metrics.SmithyHours, 3);
                Assert.LessOrEqual(worked.Count, Forge.Places, "no more hands than its places");
                foreach (int id in worked) Assert.GreaterOrEqual(w.People.Get(id).KMetal, 0.75f - 1e-6f, (coarse ? "COARSE" : "lived") + ": its best smiths only");
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
