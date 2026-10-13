using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The burial ground (D2-151): the dead are buried where they lived (or in the village they died in); the drowned, the wave's and the lava's dead
    /// leave no grave; graves are fresh for about a year and sink from sight over decades; the k-th burial lies in slot k mod Slots; saves keep it.
    /// </summary>
    [TestFixture]
    public class BurialsTests
    {
        static int Resident(World w, int sid)
        {
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) if (w.People.Get(res[i]).Alive) return res[i];
            return 0;
        }

        [Test]
        public void TheDeadAreBuriedAtHomeAndTheDrownedLeaveNoGrave()
        {
            var w = TestWorlds.Make(4511);
            int sid = w.Settlements.First();
            Assume.That(w.Settlements.Get(sid).BurialCount == 0, "a young village has buried nobody yet");
            int a = Resident(w, sid);
            w.Agents.Kill(ref w.People.Get(a), 0, 1);   // (old age)
            var s = w.Settlements.Get(sid);
            Assert.AreEqual(1, s.BurialCount, "buried at home");
            Assert.AreEqual(1.0, s.Graves, 1e-9);
            Assert.AreEqual(1.0, s.GravesFresh, 1e-9);
            Assert.IsTrue(s.GraveX != 0f || s.GraveY != 0f, "its burial ground laid out at the first burial");
            double gx = s.GraveX, gy = s.GraveY;
            double r = System.Math.Sqrt((gx - s.SquareX) * (gx - s.SquareX) + (gy - s.SquareY) * (gy - s.SquareY));
            Assert.Greater(r, 20.0, "beyond the homes");
            int b = Resident(w, sid);
            w.Agents.Kill(ref w.People.Get(b), 0, 12);   // (drowned)
            s = w.Settlements.Get(sid);
            Assert.AreEqual(1, s.BurialCount, "the drowned leave no grave");
            int c = Resident(w, sid);
            w.Agents.Kill(ref w.People.Get(c), 0, 2);   // (hunger)
            s = w.Settlements.Get(sid);
            Assert.AreEqual(2, s.BurialCount);
            Assert.AreEqual(gx, s.GraveX, 0.0, "the ground stays where it was laid out");
        }

        [Test]
        public void GravesAreFreshForAYearAndSinkFromSightOverDecades()
        {
            var w = TestWorlds.Make(4512);
            int sid = w.Settlements.First();
            for (int k = 0; k < 10; k++) { int a = Resident(w, sid); Assume.That(a != 0); w.Agents.Kill(ref w.People.Get(a), 0, 10); }
            var s = w.Settlements.Get(sid);
            Burials.Read(in s, w.Now, out double m0, out double f0);
            Assert.AreEqual(10.0, m0, 1e-9); Assert.AreEqual(10.0, f0, 1e-9);
            Burials.Read(in s, w.Now + SimTime.Year, out double m1, out double f1);
            Assert.AreEqual(10.0 * System.Math.Exp(-1.0 / Burials.MarkedYears), m1, 1e-6, "a year later most are still marked");
            Assert.AreEqual(10.0 * System.Math.Exp(-1.0 / Burials.FreshYears), f1, 1e-6, "a third still fresh");
            Burials.Read(in s, w.Now + 60 * SimTime.Year, out double m60, out _);
            Assert.Less(m60, 1.5, "sixty years on, almost none to be seen");
            int fresh = 0, shown = 0;
            for (int j = 0; j < Burials.Slots; j++) if (Burials.Shows(in s, w.Now, j, out bool fr, out _)) { shown++; if (fr) fresh++; }
            Assert.AreEqual(10, shown, "ten graves dug"); Assert.AreEqual(10, fresh);
            shown = 0; fresh = 0;
            for (int j = 0; j < Burials.Slots; j++) if (Burials.Shows(in s, w.Now + 3 * SimTime.Year, j, out bool fr, out _)) { shown++; if (fr) fresh++; }
            Assert.AreEqual(9, shown, "three years on: the oldest grave of ten sunk (10 x e^-0.1 = 9.05)");
            Assert.AreEqual(0, fresh, "none fresh");
        }

        [Test]
        public void TheOldestGroundIsDugAgainAndSavesKeepIt()
        {
            var w = TestWorlds.Make(4513);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            s.BurialCount = Burials.Slots + 5; s.Graves = 200; s.GravesFresh = 3; s.GravesAt = w.Now; s.GraveX = 80f; s.GraveY = -40f;
            Assert.IsTrue(Burials.Shows(in s, w.Now, 4, out bool fr, out int age), "slot 4 holds burial 124, the latest");
            Assert.IsTrue(fr); Assert.AreEqual(0, age);
            Assert.IsTrue(Burials.Shows(in s, w.Now, 5, out fr, out age), "slot 5: burial 5, dug 119 burials ago");
            Assert.IsFalse(fr); Assert.AreEqual(Burials.Slots - 1, age);
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            var s2 = w2.Settlements.Get(sid);
            Assert.AreEqual(s.BurialCount, s2.BurialCount); Assert.AreEqual(s.Graves, s2.Graves, 0.0); Assert.AreEqual(s.GravesFresh, s2.GravesFresh, 0.0);
            Assert.AreEqual(s.GravesAt, s2.GravesAt); Assert.AreEqual(s.GraveX, s2.GraveX, 0f); Assert.AreEqual(s.GraveY, s2.GraveY, 0f);
        }
    }
}
