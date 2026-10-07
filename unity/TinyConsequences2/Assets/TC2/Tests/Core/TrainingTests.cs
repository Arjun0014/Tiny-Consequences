using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Training grounds and fighting skill (C1.5, D2-094): a village that fears its neighbours lays out a training ground; practice grows
    /// skill toward a ceiling, faster with a skilled one present; a fight teaches more; skill fades unused; the trained strike harder; the
    /// hours are real at both resolutions. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class TrainingTests
    {
        static void Fear(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.Wall = 2; s.WallCond = 1f; s.WallR = (float)w.Conflict.WallRadius(sid);
            s.WallEvent = w.Record(new HistoryEvent { Kind = HistoryKind.WallBuilt, Time = w.Now, Settlement = sid, Subject = EntityRef.Settlement(sid) });
        }

        static int StandingGround(World w, int sid)
        {
            Structures.Place(w, sid, Structures.Ground, out float x, out float y);
            return Structures.Standing(w, sid, Structures.Ground, x, y);
        }

        [Test]
        public void AVillageThatFearsItsNeighboursLaysOutATrainingGround()
        {
            var w = TestWorlds.Make(801);
            int sid = w.Settlements.First();
            ref var s0 = ref w.Settlements.Get(sid); s0.Project = 0;
            Structures.Plan(w, sid);
            Assert.AreNotEqual(Structures.Ground, w.Settlements.Get(sid).Project != 0 ? w.Buildings.Get(w.Settlements.Get(sid).Project).Kind : (byte)0, "no fear, no ground");
            Fear(w, sid);
            ref var s = ref w.Settlements.Get(sid); s.Project = 0; s.Smithy = 0; s.TechKnown = 0;
            Structures.Plan(w, sid);
            int bid = w.Settlements.Get(sid).Project;
            Assert.AreNotEqual(0, bid);
            var b = w.Buildings.Get(bid);
            Assert.AreEqual(Structures.Ground, b.Kind);
            Assert.AreEqual(w.Settlements.Get(sid).WallEvent, b.Cause, "in response to what it fears");
            double r = Math.Sqrt(((double)b.X - s.SquareX) * ((double)b.X - s.SquareX) + ((double)b.Y - s.SquareY) * ((double)b.Y - s.SquareY));
            Assert.Greater(r, Structures.HomesReach(w, sid), "beyond the homes");
            Assert.IsFalse(Structures.OnRing(in w.Settlements.Get(sid), b.X, b.Y, Structures.Of(Structures.Ground).Footprint), "not astride the palisade");
        }

        [Test]
        public void PracticeGrowsSkillTowardItsCeilingFasterWithATrainerAndItFadesUnused()
        {
            var w = TestWorlds.Make(802);
            int sid = w.Settlements.First();
            int gid = StandingGround(w, sid);
            int pid = w.Residents(sid)[0];
            ref var p = ref w.People.Get(pid);
            p.SkFight = 0f;
            ref var s = ref w.Settlements.Get(sid); s.BestFight = 0f;
            Training.Practise(w, ref p, sid, gid, 10);
            double alone = p.SkFight;
            Assert.AreEqual(Training.PerHour * 10, alone, 1e-6, "a beginner's ten hours");
            ref var p2 = ref w.People.Get(w.Residents(sid)[1]);
            p2.SkFight = 0f;
            ref var s2 = ref w.Settlements.Get(sid); s2.BestFight = 0.8f;   // premise: a seasoned fighter among them
            Training.Practise(w, ref p2, sid, gid, 10);
            Assert.AreEqual(alone * (1 + Training.Lift * 0.8), p2.SkFight, 1e-6, "a trainer lifts their learning");
            for (int k = 0; k < 400; k++) Training.Practise(w, ref p2, sid, gid, 10);
            Assert.Less(p2.SkFight, 1.0f + 1e-6f); Assert.Greater(p2.SkFight, 0.9f, "toward the ceiling");
            // unused for a year: about 5 % gone (twice that past fifty)
            double before = p2.SkFight;
            p2.Born = w.Now - SimTime.Years(30);
            for (int d = 0; d < SimTime.DaysPerYear; d++) Training.Daily(w, sid, 1);
            Assert.AreEqual(before * (1 - Training.Fade), w.People.Get(w.Residents(sid)[1]).SkFight, before * 0.003, "fades unused");
            Assert.AreEqual(w.People.Get(w.Residents(sid)[1]).SkFight, w.Settlements.Get(sid).BestFight, 1e-6, "the village's best");
            Assert.Greater(w.Buildings.Get(gid).Use, 4000f, "the ground's hours");
        }

        [Test]
        public void AFightTeachesAndTheTrainedStrikeHarder()
        {
            var w = TestWorlds.Make(803);
            int sid = w.Settlements.First();
            ref var p = ref w.People.Get(w.Residents(sid)[0]);
            p.SkFight = 0f; p.Fights = 0;
            double s0 = w.Conflict.Strength(in p);
            Training.Veteran(ref p);
            Assert.AreEqual(Training.FightLesson, p.SkFight, 1e-6, "one blow given or taken teaches as much as ten hours' practice");
            Assert.AreEqual(1, p.Fights);
            p.SkFight = 0.5f;
            Assert.AreEqual(s0 * 1.3, w.Conflict.Strength(in p), 1e-9, "half-trained: 1.3x");
        }

        [Test]
        public void TheBoldPractiseAtTheGroundAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(804);
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                int sid = w.Settlements.First();
                Fear(w, sid);
                StandingGround(w, sid);
                bool seen = false;
                for (int h = 0; h < 6 * 24; h++)
                {
                    { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); sk.WallCond = 1f; if (sk.Granary < 20 * sk.Population) sk.Granary = 20 * sk.Population; }
                    w.AdvanceTo(w.Now + SimTime.Hour);
                    if (!coarse && !seen) { var res = w.Residents(sid); for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Act == ActivityKind.Train) { seen = true; Console.WriteLine(HistoryText.Doing(w, in w.People.Get(res[i]))); break; } }
                }
                double skill = 0; int trained = 0; var rs = w.Residents(sid);
                for (int i = 0; i < rs.Count; i++) { var q = w.People.Get(rs[i]); if (q.SkFight > 0) { trained++; skill += q.SkFight; } }
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": practice " + w.Metrics.TrainHours.ToString("0") + " h in six days, " + trained + " with some skill (mean " + (trained > 0 ? skill / trained : 0).ToString("0.000") + ")");
                Assert.Greater(w.Metrics.TrainHours, 10, (coarse ? "COARSE" : "lived") + ": they practised");
                Assert.Greater(trained, 0);
                if (!coarse) Assert.IsTrue(seen, "seen at the ground");
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }
    }
}
