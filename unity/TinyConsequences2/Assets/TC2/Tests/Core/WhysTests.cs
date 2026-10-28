using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// (session 29: "why?") Every question a place or a person can be asked stands on a record; its answer is the ledger's own chain told from its root to
    /// that record; a place's trend counts its own records. Reads only.
    /// </summary>
    [TestFixture]
    public class WhysTests
    {
        static World Lived(ulong seed, int years)
        {
            var w = TestWorlds.Make(seed);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Years(years));
            return w;
        }

        [Test]
        public void EveryQuestionStandsOnARecordAndItsAnswerEndsThere()
        {
            var w = Lived(631, 40);
            int asked = 0, longest = 0;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                foreach (var q in Whys.OfPlace(w, sid))
                {
                    asked++;
                    Assert.IsTrue(w.History.TryGet(q.Event, out _), q.Question);
                    var story = Whys.Story(w, q.Event);
                    Assert.Greater(story.Count, 0);
                    var last = story[story.Count - 1];
                    Assert.AreEqual(q.Event, last.Event, "the story ends at the record asked about");
                    Assert.AreEqual("", last.Link);
                    long t = long.MinValue;
                    foreach (var st in story) { Assert.IsTrue(w.History.TryGet(st.Event, out _)); if (!st.Side) { Assert.GreaterOrEqual(st.Time, t, "told oldest first"); t = st.Time; } }
                    if (story.Count > longest)
                    {
                        longest = story.Count;
                        Console.WriteLine(q.Question);
                        foreach (var st in story) Console.WriteLine("   " + (st.Side ? "(beside) " : "") + st.Text + (st.Link.Length > 0 ? "  -- " + st.Link : ""));
                        Console.WriteLine("   " + Whys.Began(w, story));
                    }
                }
            }
            Console.WriteLine(asked + " questions asked of the places; the longest answer " + longest + " records");
            Assert.Greater(asked, 0);
        }

        [Test]
        public void AWayIsReadFromItsWearAndWhoIsOnItNow()
        {
            var w = Lived(635, 30);
            var ways = new List<TC2.Core.Agents.Way>();
            w.Ways.Collect(w.Now, ways);
            Assert.Greater(ways.Count, 0, "the villages' people have worn ways between them");
            var top = ways[0];
            foreach (var way in ways) if (way.Use > top.Use) top = way;
            var lines = Whys.Way(w, top.A, top.B);
            foreach (var l in lines) Console.WriteLine(l);
            Assert.That(lines[0], Does.StartWith("The ").And.Contain(" between "));
            Assert.That(lines[1], Does.StartWith("walked about " + (w.Ways.UseOf(w.Now, top.A, top.B) * 0.69).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " times a year"));
            int on = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (p.Alive && p.Step == TC2.Core.Agents.StepKind.Travel && p.Act != TC2.Core.Agents.ActivityKind.Voyage && ((p.RouteA == top.A && p.RouteB == top.B) || (p.RouteA == top.B && p.RouteB == top.A))) on++;
            }
            Assert.IsTrue(on == 0 ? lines.Contains("nobody on it at this moment") : lines.Exists(l => l.StartsWith("on it now: ")), "who is on it now, from the people on its route");
        }

        [Test]
        public void APlacesTrendCountsItsOwnRecords()
        {
            var w = Lived(633, 25);
            int sid = w.Settlements.First();
            var lines = Whys.Trend(w, sid, 10, out _);
            foreach (var l in lines) Console.WriteLine(l);
            int born = 0;
            var pev = w.History.RecordsAt(sid);
            for (int i = 0; pev != null && i < pev.Count; i++) if (w.History.TryGet(pev[i], out var e) && e.Kind == HistoryKind.PersonBorn && e.Time >= w.Now - 10 * SimTime.Year) born++;
            Assert.That(lines[0], Does.StartWith("In the last 10 years " + born + " were born here"));
            foreach (var l in Whys.Now(w, sid)) Console.WriteLine("now: " + l);
        }
    }
}
