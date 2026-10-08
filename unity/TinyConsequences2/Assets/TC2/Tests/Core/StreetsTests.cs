using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// A village's streets (C13.1, D2-116): the ways its people wear to paths are laid as streets at the bearing they leave by, merged within
    /// 35 degrees, four at most, kept when the way fades; the frontage along them is sought first, so new lots line the streets and face them;
    /// a village with no worn way keeps its spiral of lots round the green; nothing is built in a street. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class StreetsTests
    {
        static int Other(World w, int sid)
        {
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o)) if (o != sid && w.Routes.Get(w, sid, o) != null) return o;
            return 0;
        }

        [Test]
        public void AWayWornToAPathIsLaidAsAStreetAtTheBearingItLeavesBy()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First(), o = Other(w, sid);
            Assume.That(o != 0, "a neighbour reached over land");
            Streets.Refresh(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).StreetN, "no worn way, no street");
            w.Ways.Walk(w, sid, o, 3.0);
            Streets.Refresh(w, sid);
            Assert.AreEqual(0, w.Settlements.Get(sid).StreetN, "a track is not yet a street");
            w.Ways.Walk(w, sid, o, 60.0);
            Streets.Refresh(w, sid);
            ref var s = ref w.Settlements.Get(sid);
            Assert.AreEqual(1, s.StreetN, "a path: a street");
            // its bearing points along the way as it leaves the village
            var r = w.Routes.Get(w, sid, o);
            s.SphereToLocal(r.PointAt(r.A == sid ? 80.0 : r.Length - 80.0), w.Planet.Radius, out double x, out double y);
            double a = Math.Atan2(y - s.SquareY, x - s.SquareX);
            Console.WriteLine("street bearing " + s.StreetAt(0).ToString("0.000") + ", the way at 80 m " + a.ToString("0.000"));
            Assert.Less(Streets.Apart(a, s.StreetAt(0)), 1e-6);
            // walked again, or a second way leaving within 35 degrees: still one street; laid once, kept when the way fades
            Streets.Refresh(w, sid);
            Assert.AreEqual(1, w.Settlements.Get(sid).StreetN);
            w.Ways.Prune(w.Now + SimTime.Years(40));
            Streets.Refresh(w, sid);
            Assert.AreEqual(1, w.Settlements.Get(sid).StreetN, "a street outlives its way");
        }

        [Test]
        public void NewLotsLineTheStreetAndFaceItAndNothingIsBuiltInIt()
        {
            var w = TestWorlds.Make(7);
            int sid = w.Settlements.First();
            ref var s = ref w.Settlements.Get(sid);
            // without streets: the spiral, facing the square
            Homes.FreeLot(w, sid, 0, out float x0, out float y0);
            Assert.IsFalse(Streets.Facing(in w.Settlements.Get(sid), x0, y0, out _, out _), "a lot round the green faces the square");
            // a street east (the premise: laid as a worn way would lay it)
            s.SetStreet(0, 0.0); s.StreetN = 1;
            int fronting = 0;
            for (int k = 0; k < 12; k++)
            {
                Homes.FreeLot(w, sid, 0, out float x, out float y);
                ref var s2 = ref w.Settlements.Get(sid);
                bool front = Streets.Facing(in s2, x, y, out double fx, out double fy);
                if (front)
                {
                    fronting++;
                    double off = Math.Sqrt((x - fx) * (x - fx) + (y - fy) * (y - fy));
                    Assert.AreEqual(Streets.Setback, off, 0.01, "set back from the street's line");
                }
                Assert.IsFalse(Streets.On(in s2, x, y, Streets.Width + 2.0), "never in the street");
                // a camp on it, so the next lot is another (as a household would take it)
                int hid = w.Ids.Next(EntityKind.Household);
                w.Households.Add(hid, new Household { Id = hid, Settlement = sid, HomeX = x, HomeY = y, Shelter = Construction.Camp });
                w.HouseholdsOf(sid).InsertSorted(hid);
            }
            Console.WriteLine(fronting + " of 12 new lots front the street");
            Assert.Greater(fronting, 6, "frontage is sought first");
            ref var s3 = ref w.Settlements.Get(sid);
            Assert.IsFalse(Structures.Clear(w, sid, s3.SquareX + 60.0, s3.SquareY, 4.0), "no work laid out in the street");
        }
    }
}
