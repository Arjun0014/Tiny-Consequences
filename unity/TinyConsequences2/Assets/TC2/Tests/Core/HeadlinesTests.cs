using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// (session 29: the long skip as a timelapse) What a span watched from orbit tells: a few great records as headlines, each a record the ledger holds,
    /// each plague and each war once; when it ends, a summary from the world's own counters. An observer - it never changes what is lived.
    /// </summary>
    [TestFixture]
    public class HeadlinesTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void ASpansHeadlinesAreFewAndEachIsARecord()
        {
            var w = TestWorlds.Make(611);
            Coarse(w);
            var heads = new Headlines(w);
            var items = new List<Headlines.Item>();
            int years = 40;
            for (int y = 0; y < years; y++) { w.AdvanceTo(w.Now + SimTime.Year); heads.Poll(w, items); }
            int records = 0;
            for (int i = w.History.FirstIndexAfter(heads.FromHistory); i < w.History.Count; i++) records++;
            Console.WriteLine(items.Count + " headlines from " + records + " records over " + years + " years");
            foreach (var it in items) Console.WriteLine("  " + SimTime.YearOf(it.Time) + " [" + it.Kicker + "/" + it.Rank + "] " + it.Text);
            Assert.Greater(records, 0, "the span made records");
            Assert.LessOrEqual(items.Count, Math.Max(12, records / 20), "a span reads as a few lines, not a feed");
            var pathogens = new HashSet<long>(); var pairs = new HashSet<long>();
            foreach (var it in items)
            {
                Assert.IsTrue(w.History.TryGet(it.Event, out var e), "every headline is a record the ledger holds");
                Assert.IsFalse(string.IsNullOrEmpty(it.Text));
                Assert.Greater(it.Rank, 0);
                if (e.Kind == HistoryKind.EpidemicArrived) Assert.IsTrue(pathogens.Add(e.Object.Packed), "a plague comes up once, at its first coming");
                if (e.Kind == HistoryKind.Raid) { int a = e.Subject.Id, b = e.Object.Id; Assert.IsTrue(pairs.Add(a < b ? (long)a * 1000003L + b : (long)b * 1000003L + a), "a war comes up once, at its first raid"); }
            }
        }

        [Test]
        public void TheSummaryIsTheWorldsOwnCounts()
        {
            var w = TestWorlds.Make(613);
            Coarse(w);
            var heads = new Headlines(w);
            long births0 = w.Metrics.Births;
            int people0 = w.LivingPeople();
            w.AdvanceTo(w.Now + SimTime.Years(15));
            var lines = heads.Summary(w);
            foreach (var l in lines) Console.WriteLine(l);
            Assert.That(lines[0], Does.StartWith("15 years passed"));
            string people = lines[1];
            Assert.That(people, Does.Contain(people0.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " then"));
            Assert.That(people, Does.Contain(w.LivingPeople().ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " now"));
            Assert.That(people, Does.Contain((w.Metrics.Births - births0).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " birth"));
            Assert.IsTrue(lines.Exists(l => l.StartsWith("Places: ")));
        }

        [Test]
        public void WatchingTheHeadlinesChangesNothing()
        {
            var a = TestWorlds.Make(617); var b = TestWorlds.Make(617);
            Coarse(a); Coarse(b);
            var heads = new Headlines(b);
            var items = new List<Headlines.Item>();
            for (int d = 0; d < 6 * 48; d++)
            {
                a.AdvanceTo(a.Now + SimTime.Day);
                b.AdvanceTo(b.Now + SimTime.Day);
                heads.Poll(b, items);
                if (d % 48 == 47) heads.Summary(b);
            }
            Assert.AreEqual(a.StateHash(), b.StateHash(), "the world read every day for headlines lives as the one never read");
        }
    }
}
