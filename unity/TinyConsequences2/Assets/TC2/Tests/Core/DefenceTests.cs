using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Defence that is built (D2-074; F6 slice 5): a palisade weighed from what other villages' people cost a village against its
    /// labour and timber, raised by the village's own hours and wood, seeing takers and doubling its defenders' ground, rotting and
    /// falling when no longer kept. Never a count of raids. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class DefenceTests
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

        /// <summary>Premise: a's people raided b's store and took from it twice this year (b knows the raiders; the takings it found).</summary>
        static long Losses(World w, int a, int b)
        {
            for (int k = 0; k < 2; k++) { int d = w.DeedBook.Record(DeedKind.Take, Grown(w, a), a, 0, b, Trade.Grain, Conflict.LoadFu, Conflict.LoadFu, 0); w.DeedBook.Learn(b, d, KnownHow.Found); }
            long ev = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Settlement = b, Subject = EntityRef.Settlement(a), Object = EntityRef.Settlement(b) });
            int raid = w.DeedBook.Record(DeedKind.Raid, Grown(w, a), a, 0, b, Trade.Grain, 150, 180, ev);
            w.DeedBook.Learn(b, raid, KnownHow.Saw);
            return ev;
        }

        [Test]
        public void AVillageThatLosesToItsNeighboursRaisesAPalisade()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            Assert.AreEqual(0, w.Settlements.Get(b).Wall, "no wall without losses");
            long raid = Losses(w, a, b);
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Day);
            ref var s = ref w.Settlements.Get(b);
            Assert.AreEqual(1, s.Wall, "the losses outweigh a palisade's labour and timber: begun");
            HistoryEvent planned = default, built = default;
            // its people build it with their hours and the village's wood (premise: the woodstore kept full, the store fed)
            for (int d = 0; d < 200 && w.Settlements.Get(b).Wall != 2; d++)
            {
                ref var sb = ref w.Settlements.Get(b);
                sb.Woodstore = Math.Max(sb.Woodstore, 400.0);
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.WallPlanned && e.Settlement == b) { planned = e; Console.WriteLine(HistoryText.Line(w, e)); }
                if (e.Kind == HistoryKind.WallBuilt && e.Settlement == b) { built = e; Console.WriteLine(HistoryText.Line(w, e)); }
            }
            ref var s2 = ref w.Settlements.Get(b);
            Console.WriteLine("palisade " + (2 * Math.PI * s2.WallR).ToString("0") + " m round; seen at the store " + w.Conflict.SeenAt(b).ToString("0.00") + "; the defenders' ground x" + w.Conflict.Ground(b).ToString("0.0"));
            Assert.AreEqual(2, s2.Wall, "built and standing");
            Assert.AreNotEqual(0, s2.Works & Construction.Palisade);
            Assert.AreEqual(HistoryKind.WallPlanned, planned.Kind);
            Assert.AreEqual(raid, planned.Cause1, "begun because of the raid it lived through");
            Assert.AreEqual(planned.Id, built.Cause1, "finished: its cause the beginning");
            Assert.GreaterOrEqual(w.Conflict.SeenAt(b), Conflict.WallSeen, "takers are seen at a walled store");
            Assert.AreEqual(2.0 * Conflict.HomeGround, w.Conflict.Ground(b), 0.01, "and its defenders hold it behind the palisade (new timber, rotting slowly)");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void TheVillagersSetThePostsThemselves()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            Losses(w, a, b);
            w.AdvanceTo(SimTime.StartOfDay(w.Now + w.LocalOffsetOf(b)) + SimTime.Day + SimTime.Hours(9) - w.LocalOffsetOf(b));
            Assume.That(w.Settlements.Get(b).Wall == 1, "a palisade begun");
            string doing = "";
            long end = w.Now + SimTime.Days(3);
            while (w.Now < end && doing.Length == 0)
            {
                ref var sb = ref w.Settlements.Get(b);
                sb.Woodstore = Math.Max(sb.Woodstore, 400.0);
                w.AdvanceTo(w.Now + 900);
                var res = w.Residents(b);
                for (int i = 0; i < res.Count; i++) { var q = w.People.Get(res[i]); if (q.Act == ActivityKind.Build && q.Why == WhyCode.VillageWork) { doing = HistoryText.Doing(w, in q); break; } }
            }
            Console.WriteLine(doing);
            Assert.IsTrue(doing.Contains("palisade"), "someone sets posts in the palisade");
            w.AdvanceTo(w.Now + SimTime.Hours(6));
            Assert.IsTrue(w.Settlements.Get(b).WorkBuild > 0f || w.Settlements.Get(b).Wall == 2, "and the work goes on");
        }

        [Test]
        public void AWalledVillageHoldsOffABandMoreOften()
        {
            int open = 0, walled = 0, tries = 12;
            for (int k = 0; k < tries; k++)
                for (int wall = 0; wall < 2; wall++)
                {
                    var w = Pair(out int a, out int b);
                    w.AdvanceTo(w.Now + SimTime.Hours(30 + 24 * k));
                    ref var sa = ref w.Settlements.Get(a);
                    Trade.DaysOfFood(w, in sa, out double daily);
                    sa.Granary = 60 * daily + 400;
                    if (wall == 1) { sa.Wall = 2; sa.WallCond = 1f; sa.WallR = 40f; sa.Works |= Construction.Palisade; }
                    var raiders = new List<int>();
                    var res = w.Residents(b);
                    for (int i = 0; i < res.Count && raiders.Count < 6; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.AgeYears(w.Now) >= 18 && q.AgeYears(w.Now) < 45 && q.Health > 0.8) raiders.Add(res[i]); }
                    if (raiders.Count < 6) continue;
                    double before = w.Settlements.Get(a).Granary;
                    w.Conflict.FallNow(b, a, raiders, seen: true);
                    if (w.Settlements.Get(a).Granary >= before) { if (wall == 1) walled++; else open++; }
                }
            Console.WriteLine("a band of 6 beaten off: " + open + " of " + tries + " at an open village, " + walled + " at a walled one");
            Assert.Greater(walled, open, "behind a palisade the defenders hold");
        }

        [Test]
        public void AnUnkeptPalisadeRotsAndFalls()
        {
            var w = Pair(out int a, out int b);
            Assume.That(b != 0, "two villages a walk apart");
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            // premise: an old palisade nobody has cause to mend (no losses remembered)
            ref var s = ref w.Settlements.Get(b);
            s.Wall = 2; s.WallCond = 0.001f; s.WallR = 40f; s.WallKeep = 0; s.Works |= Construction.Palisade;
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(3));
            ref var s2 = ref w.Settlements.Get(b);
            HistoryEvent fell = default;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.WallFallen && e.Settlement == b) { fell = e; Console.WriteLine(HistoryText.Line(w, e)); } }
            Assert.AreEqual(3, s2.Wall, "rotted through and fallen");
            Assert.AreEqual(0, s2.Works & Construction.Palisade);
            Assert.AreEqual(HistoryKind.WallFallen, fell.Kind, "and history tells it");
            Assert.AreEqual(0, s2.WallKeep, "nobody mends what nothing threatens");
        }
    }
}
