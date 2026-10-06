using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The History Book (D2-077; F7): the annals keep each year in numbers; ages are found after the fact where the years change
    /// and named from what marked them; every line of the book stands on its record - an event the ledger holds, whose causes can
    /// be walked, or the annals' years. Historians read; they never write the world.
    /// </summary>
    [TestFixture]
    public class ChronicleTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void TheAnnalsKeepEachYearFromTheWorldsOwnCounters()
        {
            var w = TestWorlds.Make(601);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(12));
            Console.WriteLine(w.Annals.Count + " years in the annals; the last: " + w.Annals[w.Annals.Count - 1].People + " people in " + w.Annals[w.Annals.Count - 1].Places + " places");
            Assert.GreaterOrEqual(w.Annals.Count, 11, "a record a year");
            long births = 0, deaths = 0;
            for (int i = 0; i < w.Annals.Count; i++) { births += w.Annals[i].Births; deaths += w.Annals[i].Deaths; Assert.AreEqual(i == 0 ? w.Annals[0].Year : w.Annals[i - 1].Year + 1, w.Annals[i].Year, "year after year"); }
            Assert.LessOrEqual(births, w.Metrics.Births, "the births of its years are the world's own (to the last year's end)");
            Assert.LessOrEqual(deaths, w.Metrics.Deaths);
            Assert.Greater(births, 0);
            Assert.Greater(w.Annals[w.Annals.Count - 1].People, 0);
        }

        [Test]
        public void AgesAreFoundWhereTheYearsChangeAndNamedForWhatMarkedThem()
        {
            // premise: a hundred years in the annals, the first fifty quiet, the last fifty hungry
            var w = TestWorlds.Make(603);
            for (int y = 0; y < 100; y++)
            {
                int people = 1000;
                w.Annals.Push(new AnnalYear { Year = y, People = people, Places = 6, Births = 30, Deaths = 28, Hunger = y < 50 ? 0 : 25, Famines = y < 50 ? 0 : 2, Temp = 14f });
            }
            var eras = Chronicle.Eras(w);
            foreach (var e in eras) Console.WriteLine(e.Name + " (" + e.From + "-" + e.To + ")");
            Assert.AreEqual(2, eras.Count, "two ages");
            Assert.That(eras[1].From, Is.InRange(44, 56), "the change found where it came (smoothed over nine years)");
            Assert.AreEqual(Chronicle.SignHunger, eras[1].Sign);
            Assert.AreEqual("The Hungry Years", eras[1].Name, "named for what marked it");
            Assert.IsTrue(eras[1].Present);
        }

        [Test]
        public void EveryLineOfTheBookStandsOnItsRecord()
        {
            var w = TestWorlds.Make(607);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Years(90));
            var eras = Chronicle.Eras(w);
            Assert.Greater(eras.Count, 0);
            int lines = 0;
            foreach (var era in eras)
            {
                Console.WriteLine("== " + era.Name + " (" + era.From + "-" + era.To + ")");
                foreach (var l in Chronicle.Chapter(w, era, 8))
                {
                    lines++;
                    Console.WriteLine("  " + l.Text);
                    if (l.Event == 0)
                    {
                        Assert.That(l.AnnalFrom, Is.GreaterThanOrEqualTo(0));
                        Assert.That(l.AnnalTo, Is.InRange(l.AnnalFrom + 1, w.Annals.Count), "a numbers line reads years the annals hold");
                        continue;
                    }
                    Assert.IsTrue(w.History.TryGet(l.Event, out var e), "every event line is an event the ledger holds");
                    Assert.AreEqual(HistoryText.Dated(w, e), l.Text, "and says what the record says, under its date");
                    Assert.IsNotEmpty(HistoryText.Why(w, l.Event), "and its causes can be walked");
                }
            }
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                foreach (var l in Chronicle.Place(w, sid, 6)) { lines++; Assert.IsTrue(w.History.TryGet(l.Event, out _)); }
            Assert.Greater(lines, 10);
            // (reading the book changes nothing)
            ulong h0 = w.StateHash();
            Chronicle.Eras(w);
            Assert.AreEqual(h0, w.StateHash(), "historians read; they never write the world");
        }
    }
}
