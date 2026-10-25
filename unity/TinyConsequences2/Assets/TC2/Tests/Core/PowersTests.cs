using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The god's powers as play (F9): a cast is a logged command the world applies as YOUR doing - told by what it was - and the book's "Your Actions"
    /// tells each cast and the records whose causes run back to it, nothing that the ledger does not hold.
    /// </summary>
    [TestFixture]
    public class PowersTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        [Test]
        public void ACastIsToldAndYourActionsTellsItAndWhatFollowed()
        {
            var w = TestWorlds.Make(611);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            int cell = w.Settlements.Get(sid).Cell;
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodStorm, Cell = cell, Arg = 55 });
            w.AdvanceTo(w.Now + SimTime.Days(6));
            long cast = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.GodCommand) continue;
                cast = e.Id;
                Assert.AreEqual(RootKind.You, e.RootKind, "your doing");
                StringAssert.StartsWith("You raised a storm", HistoryText.Describe(w, in e));
            }
            Assert.AreNotEqual(0L, cast, "the cast is recorded");
            int followed = 0;
            for (int i = w.History.FirstIndexAfter(cast); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Root == cast) { followed++; Assert.AreEqual(RootKind.You, e.RootKind); } }
            Assert.Greater(followed, 0, "the storm it raised is in the world's records, rooted in the cast");
            var bw = new HistoryBook.Writer(w);
            var part = bw.Part(8);
            Assert.AreEqual("Your Actions", part.Title);
            Assert.Greater(part.Chapters.Count, 0);
            var ch = part.Chapters[0];
            StringAssert.StartsWith("Raised a storm", ch.Title, "a chapter for the cast");
            Assert.AreEqual(cast, ch.Blocks[0].Event, "told first");
            int lines = 0;
            foreach (var b in ch.Blocks)
                if (b.Kind == BookKind.Line && b.Event != cast) { lines++; Assert.IsTrue(w.History.TryGet(b.Event, out var be) && be.Root == cast, "every line it tells came of the cast"); }
            Assert.Greater(lines, 0, "and what came of it");
        }

        [Test]
        public void WithNoCastTheWorldsHistoryIsItsOwn()
        {
            var w = TestWorlds.Make(612);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            var part = new HistoryBook.Writer(w).Part(8);
            Assert.AreEqual(1, part.Chapters.Count);
            Assert.AreEqual("Nothing yet", part.Chapters[0].Title);
        }
    }
}
