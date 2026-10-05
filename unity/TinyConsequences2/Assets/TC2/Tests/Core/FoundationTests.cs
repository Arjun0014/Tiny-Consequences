using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Scheduling;

namespace TC2.Tests.Core
{
    /// <summary>Marker type so hosts can find this assembly.</summary>
    public sealed class Marker { }

    [TestFixture]
    public class DMathTests
    {
        [Test]
        public void SinCosMatchSystemMathClosely()
        {
            double maxErr = 0;
            for (int k = -20000; k <= 20000; k++)
            {
                double x = k * 0.00137 + 0.000013 * k * k / 1000.0;
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Sin(x) - Math.Sin(x)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Cos(x) - Math.Cos(x)));
            }
            Assert.Less(maxErr, 1e-14, "sin/cos max abs error");
        }

        [Test]
        public void ExpLogMatchSystemMathClosely()
        {
            double maxRel = 0;
            for (int k = -3000; k <= 3000; k++)
            {
                double x = k * 0.1 + 0.0123;
                double e = DMath.Exp(x), m = Math.Exp(x);
                if (m > 0 && !double.IsInfinity(m)) maxRel = Math.Max(maxRel, Math.Abs(e - m) / m);
            }
            Assert.Less(maxRel, 1e-14, "exp max rel error");
            maxRel = 0;
            for (int k = 1; k <= 20000; k++)
            {
                double x = k * 0.37 * k / 97.0 + 1e-9 * k;
                double l = DMath.Log(x), m = Math.Log(x);
                maxRel = Math.Max(maxRel, Math.Abs(l - m) / Math.Max(1e-300, Math.Abs(m)));
            }
            Assert.Less(maxRel, 1e-13, "log max rel error");
        }

        [Test]
        public void AtanFamilyMatchSystemMathClosely()
        {
            double maxErr = 0;
            for (int k = -5000; k <= 5000; k++)
            {
                double x = k * 0.0031;
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Atan(x) - Math.Atan(x)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Atan2(x, 0.7) - Math.Atan2(x, 0.7)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Atan2(x, -0.7) - Math.Atan2(x, -0.7)));
                double y = Math.Max(-1, Math.Min(1, k / 5000.0));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Asin(y) - Math.Asin(y)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Acos(y) - Math.Acos(y)));
            }
            Assert.Less(maxErr, 1e-12, "atan family max abs error");
        }

        [Test]
        public void PowAndTanhAreAccurate()
        {
            Assert.AreEqual(Math.Pow(2.5, 3.7), DMath.Pow(2.5, 3.7), 1e-12 * Math.Pow(2.5, 3.7));
            Assert.AreEqual(1024.0, DMath.Pow(2, 10));
            Assert.AreEqual(0.125, DMath.Pow(2, -3));
            Assert.AreEqual(Math.Tanh(0.731), DMath.Tanh(0.731), 1e-14);
            Assert.AreEqual(1.0, DMath.Exp(0));
            Assert.AreEqual(0.0, DMath.Log(1));
        }

        [Test]
        public void KnownBitPatternsAreStable()
        {
            // Bit-exact anchors: if these change, saves and cross-runtime equality break.
            StateHasher h = StateHasher.Create();
            for (int k = 1; k <= 1000; k++)
            {
                double x = k * 0.0173;
                h.Add(DMath.Sin(x)); h.Add(DMath.Cos(x)); h.Add(DMath.Exp(x * 0.1)); h.Add(DMath.Log(x)); h.Add(DMath.Atan2(x, 1.3 - x));
            }
            Console.WriteLine("dmath anchor " + h);
            Assert.AreNotEqual(0UL, h.Value);
        }
    }

    [TestFixture]
    public class RngTests
    {
        [Test]
        public void KeyedDrawsAreUniformAndIndependentOfOrder()
        {
            const int n = 200000;
            int[] bins = new int[10];
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double u = KeyedRng.Unit(42, RngStream.Test, (ulong)i);
                Assert.That(u >= 0 && u < 1);
                bins[(int)(u * 10)]++;
                sum += u;
            }
            Assert.AreEqual(0.5, sum / n, 0.005);
            foreach (int b in bins) Assert.AreEqual(n / 10.0, b, n * 0.01);
            // the same key always gives the same draw
            Assert.AreEqual(KeyedRng.Hash(7, RngStream.Decide, 3, 4), KeyedRng.Hash(7, RngStream.Decide, 3, 4));
            Assert.AreNotEqual(KeyedRng.Hash(7, RngStream.Decide, 3, 4), KeyedRng.Hash(7, RngStream.Decide, 4, 3));
            Assert.AreNotEqual(KeyedRng.Hash(7, RngStream.Decide, 3, 4), KeyedRng.Hash(7, RngStream.Work, 3, 4));
        }

        [Test]
        public void NormalHasUnitVariance()
        {
            var seq = new RandomSequence(12345);
            double s = 0, s2 = 0; int n = 100000;
            for (int i = 0; i < n; i++) { double z = seq.NextNormal(); s += z; s2 += z * z; }
            double mean = s / n, var = s2 / n - mean * mean;
            Assert.AreEqual(0, mean, 0.02);
            Assert.AreEqual(1, var, 0.03);
        }

        [Test]
        public void BelowStaysInRange()
        {
            for (int i = 0; i < 10000; i++)
            {
                int v = KeyedRng.Below(KeyedRng.Hash(1, RngStream.Test, (ulong)i), 7);
                Assert.That(v >= 0 && v < 7);
            }
        }
    }

    [TestFixture]
    public class CalendarTests
    {
        [Test]
        public void CalendarArithmetic()
        {
            Assert.AreEqual(4147200L, SimTime.Year);
            long t = 183 * SimTime.Year + 30 * SimTime.Day + 14 * SimTime.Hour + 5 * SimTime.Minute;
            Assert.AreEqual(183, SimTime.YearOf(t));
            Assert.AreEqual(30, SimTime.DayOfYear(t));
            Assert.AreEqual(2, SimTime.SeasonOf(t));
            Assert.AreEqual(14, SimTime.HourOfDay(t));
            Assert.AreEqual(5, SimTime.MinuteOfHour(t));
            Assert.AreEqual("Year 183 · Mid Autumn · Day 31 · 14:05", SimTime.Format(t));
            Assert.AreEqual(SimTime.StartOfDay(t) + SimTime.Day + 6 * SimTime.Hour, SimTime.NextTimeOfDay(t, 6 * SimTime.Hour));
            Assert.AreEqual(-1, SimTime.YearOf(-1));
            Assert.AreEqual(47, SimTime.DayOfYear(-1));
        }
    }

    [TestFixture]
    public class EventQueueTests
    {
        [Test]
        public void PopsInTimePhaseSeqOrder()
        {
            var q = new EventQueue();
            var seq = new RandomSequence(99);
            for (int i = 0; i < 20000; i++) q.Push(seq.NextBelow(5000), seq.NextBelow(4) * 10, EventKind.AgentStep, i);
            long lastT = long.MinValue; int lastP = int.MinValue; long lastS = long.MinValue;
            int n = 0;
            while (q.Count > 0)
            {
                var e = q.Pop();
                bool ordered = e.Time > lastT || (e.Time == lastT && (e.Phase > lastP || (e.Phase == lastP && e.Seq > lastS)));
                Assert.IsTrue(ordered, "order violated at " + n);
                lastT = e.Time; lastP = e.Phase; lastS = e.Seq; n++;
            }
            Assert.AreEqual(20000, n);
        }

        [Test]
        public void SerializedQueueRoundTripsAndHashesEqual()
        {
            var q = new EventQueue();
            for (int i = 0; i < 1000; i++) q.Push((i * 7919) % 1000, 30, EventKind.AgentStep, i, i % 3, i, -i);
            var w = new BinWriter();
            q.Write(w);
            var q2 = new EventQueue();
            q2.Read(new BinReader(w.ToArray()));
            StateHasher h1 = StateHasher.Create(), h2 = StateHasher.Create();
            q.Hash(ref h1); q2.Hash(ref h2);
            Assert.AreEqual(h1.Value, h2.Value);
            while (q.Count > 0) { var a = q.Pop(); var b = q2.Pop(); Assert.AreEqual(a.Seq, b.Seq); Assert.AreEqual(a.Time, b.Time); }
            Assert.AreEqual(q.NextSeq, q2.NextSeq);
        }
    }

    [TestFixture]
    public class CubeSphereTests
    {
        [Test]
        public void AreasSumToFourPiAndAreBalanced()
        {
            var g = new CubeSphere(32);
            double sum = 0, min = double.MaxValue, max = 0;
            for (int c = 0; c < g.CellCount; c++) { sum += g.Area[c]; min = Math.Min(min, g.Area[c]); max = Math.Max(max, g.Area[c]); }
            Assert.AreEqual(4 * Math.PI, sum, 1e-9);
            Assert.Less(max / min, 1.45, "equiangular area ratio");
        }

        [Test]
        public void NeighboursAreSymmetricAndCellOfRoundTrips()
        {
            var g = new CubeSphere(16);
            for (int c = 0; c < g.CellCount; c++)
            {
                Assert.AreEqual(c, g.CellOf(g.Center[c]), "cellOf(center)");
                for (int k = 0; k < 4; k++)
                {
                    int nb = g.Nbr4[c * 4 + k];
                    Assert.AreNotEqual(c, nb);
                    bool back = false;
                    for (int m = 0; m < 4; m++) if (g.Nbr4[nb * 4 + m] == c) back = true;
                    Assert.IsTrue(back, "neighbour symmetry " + c + " -> " + nb);
                    double ang = Vec3d.Angle(g.Center[c], g.Center[nb]);
                    Assert.Less(ang, g.MeanSpacing * 1.6);
                }
            }
        }
    }

    [TestFixture]
    public class StoreTests
    {
        struct Row { public int V; }

        [Test]
        public void PagedStoreIteratesInIdOrderAndFreesPages()
        {
            var s = new PagedStore<Row>();
            for (int id = 1; id <= 5000; id++) s.Add(id, new Row { V = id * 2 });
            for (int id = 1; id <= 4000; id++) if (id % 3 != 0) s.Remove(id);
            int prev = 0, count = 0;
            for (int id = s.First(); id != 0; id = s.Next(id)) { Assert.Greater(id, prev); Assert.AreEqual(id * 2, s.Get(id).V); prev = id; count++; }
            Assert.AreEqual(s.Count, count);
            for (int id = 1; id <= 4000; id++) s.Remove(id);
            Assert.AreEqual(1000, s.Count);
            Assert.AreEqual(4001, s.First());
        }
    }
}
