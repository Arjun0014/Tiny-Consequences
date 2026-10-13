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

        [Test]
        public void TheVillageBuildsRoundItsBurialGround()
        {
            // D2-154: once laid out, the ground is kept - no home's lot, no work, no croft's bed over the graves (a town grows round its old ground
            // as round a churchyard); `--oldgraves` lays them as before
            var w = TestWorlds.Make(4514);
            int sid = w.Settlements.First();
            int a = Resident(w, sid);
            w.Agents.Kill(ref w.People.Get(a), 0, 1);
            Assume.That(Burials.Laid(in w.Settlements.Get(sid)));
            // the ground moved onto the very lot the next household would take
            Homes.FreeLot(w, sid, 0, out float lx, out float ly);
            ref var s = ref w.Settlements.Get(sid);
            s.GraveX = lx; s.GraveY = ly;
            int plan = Footprints.LotPlan(in s, lx, ly);
            double face = Footprints.FaceAt(in s, lx, ly, 0);
            Assert.IsFalse(Footprints.HomeFits(w, sid, lx, ly, face, plan, 0, 0), "no home's lot on the graves");
            Assert.IsFalse(Footprints.WorkFits(w, sid, lx, ly, Structures.Barn, 0.0, false), "no work on them");
            Homes.FreeLot(w, sid, 0, out float mx, out float my);
            ref var s1 = ref w.Settlements.Get(sid);
            int plan2 = Footprints.LotPlan(in s1, mx, my);
            double face2 = Footprints.FaceAt(in s1, mx, my, 0);
            for (int r = 0; r < Footprints.Count(plan2); r++)
            {
                Footprints.Rect(plan2, r, out double cx, out double cz, out double hx, out double hz);
                Assert.IsFalse(Footprints.Boxes(mx, my, face2, cx, cz, hx, hz, s1.GraveX, s1.GraveY, 0.0, 0.0, 0.0, Burials.HalfY, Burials.HalfX, 0.0),
                               "the next household's lot lies off the ground");
            }
            System.Console.WriteLine("the lot it would have taken (" + lx.ToString("0.0") + ", " + ly.ToString("0.0") + ") holds the ground; it takes (" + mx.ToString("0.0") + ", " + my.ToString("0.0") + ")");
            Burials.Unkept = true;
            try
            {
                Assert.IsTrue(Footprints.HomeFits(w, sid, lx, ly, face, plan, 0, 0), "--oldgraves: the lot taken over the graves as before");
                Homes.FreeLot(w, sid, 0, out float ox, out float oy);
                Assert.AreEqual(lx, ox, 0f); Assert.AreEqual(ly, oy, 0f);
            }
            finally { Burials.Unkept = false; }
        }
    }
}
