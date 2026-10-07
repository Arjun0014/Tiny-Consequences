using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Ways worn by use (C4.1, D2-097): walking between villages wears the ground into a track, a path, a road; the wear halves each year it
    /// is not renewed; worn ways are quicker underfoot; history tells a path the first time; both resolutions wear them. Mechanisms.
    /// </summary>
    [TestFixture]
    public class WaysTests
    {
        [Test]
        public void WalkingWearsAWayThatHalvesEachYearUnwalked()
        {
            var w = TestWorlds.Make(1001);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            Assert.AreEqual(0, w.Ways.State(w.Now, a, b), "nothing worn yet");
            long mark = w.History.LastId;
            for (int i = 0; i < 50; i++) w.Ways.Walk(w, a, b, 1.0);
            Assert.AreEqual(50.0, w.Ways.UseOf(w.Now, a, b), 1e-9);
            Assert.AreEqual(2, w.Ways.State(w.Now, b, a), "a path, whichever way it is named");
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == HistoryKind.WayWorn) { told = true; Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told, "a path worn is history");
            Assert.AreEqual(25.0, w.Ways.UseOf(w.Now + SimTime.Year, a, b), 1e-6, "halved in a year unwalked");
            Assert.AreEqual(1, Ways.StateOf(w.Ways.UseOf(w.Now + SimTime.Years(3), a, b)), "a track after three years");
            w.Ways.Prune(w.Now + SimTime.Years(12));
            Assert.AreEqual(0, w.Ways.Count, "gone into the grass");
        }

        [Test]
        public void AWornWayIsQuickerUnderfoot()
        {
            Assert.AreEqual(1.0, new Ways().Speed(0, 1, 2));
            var w = TestWorlds.Make(1002);
            int a = w.Settlements.First(), b = w.Settlements.Next(a);
            for (int i = 0; i < 60; i++) w.Ways.Walk(w, a, b, 1.0);
            Assert.AreEqual(1.08, w.Ways.Speed(w.Now, a, b), 1e-12, "a path");
            for (int i = 0; i < 300; i++) w.Ways.Walk(w, a, b, 1.0);
            Assert.AreEqual(1.15, w.Ways.Speed(w.Now, a, b), 1e-12, "a road");
            Assert.AreEqual(3, w.Ways.State(w.Now, a, b));
        }

        [Test]
        public void VillagesWearTheWaysBetweenThemAtBothResolutions()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = TestWorlds.Make(1003);
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                w.AdvanceTo(w.Now + SimTime.Days(24));
                var list = new List<Way>();
                w.Ways.Collect(w.Now, list);
                double total = 0; foreach (var x in list) total += x.Use;
                Console.WriteLine((coarse ? "COARSE" : "lived") + ": " + w.Ways.Count + " ways walked, " + list.Count + " worn to a track or more, wear " + total.ToString("0"));
                Assert.Greater(w.Ways.Count, 0, (coarse ? "COARSE" : "lived") + ": its people walked between villages");
            }
        }
    }
}
