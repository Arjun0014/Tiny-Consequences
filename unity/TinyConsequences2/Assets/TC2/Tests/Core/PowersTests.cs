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
        public void APlagueBreaksOutInTheNearestVillageAndSpreadsFromTheCast()
        {
            var w = TestWorlds.Make(613);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodPlague, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Days(20));
            long cast = 0, appeared = 0; int sick = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) { cast = e.Id; StringAssert.StartsWith("You sent a sickness", HistoryText.Describe(w, in e)); }
                if (e.Kind == HistoryKind.PathogenAppeared) { appeared = e.Id; Assert.AreEqual(sid, e.Settlement, "in the village nearest the place"); Assert.AreEqual(cast, e.Root, "rooted in the cast"); Assert.AreEqual(RootKind.You, e.RootKind); }
                if (e.Kind == HistoryKind.PersonDied && e.ArgA == 10 && e.Root == cast) sick++;
            }
            Assert.AreNotEqual(0L, cast); Assert.AreNotEqual(0L, appeared, "a new sickness appeared");
            int ill = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive && p.Sick != 0) ill++; }
            Assert.Greater(ill + sick, 0, "it took hold among the people");
        }

        [Test]
        public void ABlightTakesTheStandingCropAndTheFailedHarvestNamesIt()
        {
            var w = TestWorlds.Make(614);
            Coarse(w);
            // to the growing season: a crop standing in the fields
            int sid = w.Settlements.First();
            for (int d = 0; d < 60 && w.Settlements.Get(sid).Crop != TC2.Core.Places.CropStage.Growing; d++) w.AdvanceTo(w.Now + SimTime.Day);
            Assume.That(w.Settlements.Get(sid).Crop == TC2.Core.Places.CropStage.Growing, "a crop growing");
            double before = w.Settlements.Get(sid).CropBurnt;
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodBlight, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Hours(2));
            Assert.Greater(w.Settlements.Get(sid).CropBurnt, before + 0.5, "most of the standing crop taken");
            long cast = 0, blight = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) cast = e.Id;
                if (e.Kind == HistoryKind.CropsBlighted && e.Settlement == sid) { blight = e.Id; Assert.AreEqual(cast, e.Root); StringAssert.Contains("A blight took the crop of", HistoryText.Describe(w, in e)); }
            }
            Assert.AreNotEqual(0L, blight, "the blight is told");
            // to the harvest: it fails, and names the blight
            long failed = 0;
            for (int d = 0; d < 48 && failed == 0; d++)
            {
                w.AdvanceTo(w.Now + SimTime.Day);
                for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.HarvestFailed && e.Settlement == sid) failed = e.Id; }
            }
            Assert.AreNotEqual(0L, failed, "the harvest failed");
            Assert.IsTrue(w.History.TryGet(failed, out var fe) && fe.Root == cast, "and its causes run back to the cast");
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
