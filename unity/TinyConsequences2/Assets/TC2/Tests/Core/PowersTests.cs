using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
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
            var part = bw.Part(HistoryBook.YourActionsPart);
            Assert.AreEqual("Your Actions", part.Title);
            Assert.Greater(part.Chapters.Count, 0);
            var ch = part.Chapters[0];
            StringAssert.StartsWith("Raised a storm", ch.Title, "a chapter for the cast");
            Assert.AreEqual(cast, ch.Blocks[0].Event, "told first");
            int lines = 0;
            foreach (var b in ch.Blocks)
                if (b.Kind == BookKind.Line && b.Event != cast) { lines++; Assert.IsTrue(w.History.TryGet(b.Event, out var be) && be.Root == cast, "every line it tells came of the cast"); }
            Assert.Greater(lines, 0, "and what came of it");
            // (session 27) the HUD's "your hand" counts the same records the book follows, and says what they were
            ulong before = w.StateHash();
            string came = HistoryBook.CameOf(w, cast, out int n);
            Assert.AreEqual(followed, n, "your hand counts every record rooted in the cast");
            Assert.IsNotEmpty(came, "and tells them");
            Assert.AreEqual(before, w.StateHash(), "reading it writes nothing");
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
        public void AMeteorFlattensWhatStandsNearItAndKillsThoseAtHomeThere()
        {
            var w = TestWorlds.Make(615);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            int standing0 = 0; var bl = w.BuildingsOf(sid); for (int i = 0; i < bl.Count; i++) if (!TC2.Core.Places.Homes.Fallen(in w.Buildings.Get(bl[i]), w.Now)) standing0++;
            long mark = w.History.LastId;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodMeteor, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Hours(3));
            long cast = 0, strike = 0; int flattened = 0, killed = 0, dead = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand) { cast = e.Id; StringAssert.StartsWith("You threw down a stone", HistoryText.Describe(w, in e)); }
                if (e.Kind == HistoryKind.MeteorStruck) { strike = e.Id; flattened = e.ArgA; killed = e.ArgB; Assert.AreEqual(cast, e.Root); StringAssert.StartsWith("A stone fell from the sky", HistoryText.Describe(w, in e)); }
                if (e.Kind == HistoryKind.PersonDied && e.ArgA == 14) { dead++; Assert.AreEqual(cast, e.Root, "the dead's causes run back to the cast"); }
            }
            Assert.AreNotEqual(0L, strike, "the strike is told");
            Assert.Greater(flattened, 0, "it flattened what stood near it");
            Assert.AreEqual(killed, dead, "those it killed are told so");
            int standing1 = 0; bl = w.BuildingsOf(sid); for (int i = 0; i < bl.Count; i++) if (!TC2.Core.Places.Homes.Fallen(in w.Buildings.Get(bl[i]), w.Now)) standing1++;
            Assert.Less(standing1, standing0, "fewer stand");
            Assert.IsNull(TestWorlds.Audit(w));
            // (session 27) Your Actions tells the dead together when there are more than a few: no death line by line, one note - how many, the eldest by
            // name (each a death the cast caused), the families that lost most
            var ch = new HistoryBook.Writer(w).Part(HistoryBook.YourActionsPart).Chapters[0];
            int deathLines = 0; string note = null;
            foreach (var b in ch.Blocks)
            {
                if (b.Kind == BookKind.Line && w.History.TryGet(b.Event, out var be) && be.Kind == HistoryKind.PersonDied) deathLines++;
                if (b.Kind == BookKind.Note && b.Text.StartsWith("The dead (", StringComparison.Ordinal)) note = b.Text;
            }
            int rooted = 0; long eldest = 0; float age = -1f;   // (every death whose causes run back to the cast)
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.PersonDied && e.Root == cast) { rooted++; if (e.Magnitude > age) { age = e.Magnitude; eldest = e.Id; } } }
            Console.WriteLine(rooted + " dead; " + (note ?? "(no note)"));
            if (rooted > 3)
            {
                Assert.AreEqual(0, deathLines, "no death told line by line");
                Assert.IsNotNull(note, "the dead told together");
                StringAssert.StartsWith("The dead (" + rooted + "), the eldest first: ", note);
                Assert.IsTrue(w.History.TryGet(eldest, out var ee) && note.Contains(w.History.NameOf(ee.Subject)), "the eldest of the cast's dead named");
            }
            else Assert.IsNull(note, "a few dead are told one by one");
        }

        [Test]
        public void ADroughtWithholdsTheRainForItsDaysAndASaveKeepsIt()
        {
            var w = TestWorlds.Make(616);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            int cell = w.Settlements.Get(sid).Cell;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodDrought, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.AreEqual(1, w.DrySpells.Count, "a drought held over the land");
            Assert.IsTrue(w.RainWithheld(cell));
            var w2 = TC2.Core.Persistence.SaveFile.Load(TC2.Core.Persistence.SaveFile.Save(w, 0, TC2.Core.Persistence.OfflineMode.Living), out _);
            Assert.AreEqual(1, w2.DrySpells.Count, "the drought is saved");
            double rain = 0, soil0 = w.Planet.SoilWater[cell];
            for (int d = 0; d < World.DroughtDays - 2; d++) { w.AdvanceTo(w.Now + SimTime.Day); rain += w.Planet.RainToday[cell]; }
            Assert.AreEqual(0.0, rain, 1e-9, "no rain under it");
            Assert.LessOrEqual(w.Planet.SoilWater[cell], soil0 + 1e-6, "the soil dries");
            w2.AdvanceTo(w.Now);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a save in the drought continues as if never saved");
            w.AdvanceTo(w.Now + SimTime.Days(4));
            Assert.AreEqual(0, w.DrySpells.Count, "the rain comes back after its days");
            long dev = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.Drought && e.RootKind == RootKind.You) dev = e.Id; }
            Assert.AreNotEqual(0L, dev, "the drought is told, rooted in the cast");
        }

        [Test]
        public void RainFallsOnTheLandForItsDaysAndIsTold()
        {
            var w = TestWorlds.Make(617);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int cell = w.Settlements.Get(w.Settlements.First()).Cell;
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodRain, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.IsTrue(w.RainBrought(cell), "the rain held over the land");
            Assert.IsFalse(w.RainWithheld(cell));
            for (int d = 0; d < World.RainDays - 1; d++) { w.AdvanceTo(w.Now + SimTime.Day); Assert.GreaterOrEqual(w.Planet.RainToday[cell], (float)World.RainFall - 1e-6f, "it rains there every day"); }
            w.AdvanceTo(w.Now + SimTime.Days(3));
            Assert.AreEqual(0, w.DrySpells.Count, "and stops after its days");
            long rev = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.RainBrought && e.RootKind == RootKind.You) { rev = e.Id; StringAssert.StartsWith("Rain came over the land", HistoryText.Describe(w, in e)); } }
            Assert.AreNotEqual(0L, rev, "told, rooted in the cast");
        }

        [Test]
        public void TheGodsOreLiesUnknownUntilSomeoneSeesItAndItsFindingRunsBackToTheCast()
        {
            var w = TestWorlds.Make(617);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            var P = w.Planet;
            // a village that knows no copper, and a place of dry land a few hundred metres from it where the ground holds none of its own
            int sid = 0, cell = -1;
            var near = new System.Collections.Generic.List<int>();
            for (int v = w.Settlements.First(); v != 0 && cell < 0; v = w.Settlements.Next(v))
            {
                if (!w.Settlements.Get(v).Inhabited || w.Settlements.Get(v).OreCell != 0) continue;
                w.Hazards.CellsWithin(w, w.Settlements.Get(v).Cell, 900.0, near);
                foreach (int c in near)
                {
                    double d = Vec3d.ArcDistance(w.Settlements.Get(v).Anchor, P.Grid.Center[c], P.Radius);
                    if (d >= 250 && P.IsLand(c) && Shore.Dry(P, P.Grid.Center[c]) && !Mining.CopperAt(w, c) && w.SettlementAtCell(c) == 0) { sid = v; cell = c; break; }
                }
            }
            Assume.That(cell >= 0, "dry land near a village that knows no copper");
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodOre, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            int di = w.Deposits.IndexAt(cell, Mining.Copper);
            Assert.GreaterOrEqual(di, 0, "a body of copper lies there");
            Assert.AreEqual(Mining.GodOreKg, w.Deposits.Get(di).Ore, 1e-6, "rich ore");
            long laid = w.Deposits.Get(di).Event;
            Assert.IsTrue(w.History.TryGet(laid, out var le) && le.Kind == HistoryKind.OreRevealed && le.RootKind == RootKind.You, "laid bare by the cast");
            StringAssert.StartsWith("Copper ore lay bare", HistoryText.Describe(w, in le));
            Assert.AreEqual(0, w.Settlements.Get(sid).OreCell, "nobody knows it yet");
            // someone of the village goes there and sees it, as a hunter or a gatherer would
            int seer = w.Residents(sid)[0];
            Mining.Saw(w, sid, seer, cell);
            Assert.AreEqual(cell + 1, w.Settlements.Get(sid).OreCell, "now the village knows it");
            long found = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.OreFound && e.Settlement == sid && e.ArgA == Mining.Copper && e.Cell == cell) { found = e.Id; Assert.AreEqual(laid, e.Cause1, "found because it was laid bare"); Assert.AreEqual(RootKind.You, e.RootKind, "its root the cast"); } }
            Assert.AreNotEqual(0L, found, "the finding is told");
            var part = new HistoryBook.Writer(w).Part(HistoryBook.YourActionsPart);
            Assert.IsTrue(part.Chapters.Count > 0 && part.Chapters[0].Title.Contains("copper"), "Your Actions tells it");
            // and a save keeps it
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            Assert.AreEqual(w.StateHash(), copy.StateHash());
            Assert.AreEqual(laid, copy.Deposits.Get(copy.Deposits.IndexAt(cell, Mining.Copper)).Event);
        }

        [Test]
        public void ABountyRenewsTheTiredFieldsRoundThePlaceAndTheyTireAgain()
        {
            var w = TestWorlds.Make(617);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First();
            // premise: the village's fields long cropped (tired, as a full land's are)
            w.Settlements.Get(sid).Tired = 0.6;
            double heart0 = TC2.Core.Places.SettlementSystem.Heart(in w.Settlements.Get(sid));
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodBounty, Cell = w.Settlements.Get(sid).Cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.AreEqual(0.0, w.Settlements.Get(sid).Tired, 1e-12, "renewed as when first broken");
            Assert.Greater(TC2.Core.Places.SettlementSystem.Heart(in w.Settlements.Get(sid)), heart0, "its fields' heart full");
            long rev = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.SoilRenewed && e.Settlement == sid) { rev = e.Id; Assert.AreEqual(RootKind.You, e.RootKind); StringAssert.StartsWith("The fields of", HistoryText.Describe(w, in e)); } }
            Assert.AreNotEqual(0L, rev, "told, rooted in the cast");
            // and the fields tire again as they are cropped, by the soil's own law
            w.AdvanceTo(w.Now + SimTime.Years(3));
            Assert.Greater(w.Settlements.Get(sid).Tired, 0.0, "tiring again");
        }

        /// <summary>The animals of the species matching a test within a god's reach of a cell, in that cell's habitat.</summary>
        static double Within(World w, int cell, System.Func<Species, bool> which)
        {
            var P = w.Planet; double sum = 0; var at = P.Grid.Center[cell]; bool land = P.IsLand(cell);
            for (int s = 0; s < P.Species.Count; s++)
            {
                if (!which(P.Species[s])) continue;
                for (int q = 0; q < P.Cells; q++)
                    if (P.IsLand(q) == land && Vec3d.ArcDistance(at, P.Grid.Center[q], P.Radius) <= World.GodReach) sum += P.Abund[s * P.Cells + q];
            }
            return sum;
        }

        [Test]
        public void ACullTakesNineInTenOfTheHuntersRoundThePlaceAndMoreWanderIn()
        {
            var w = TestWorlds.Make(618);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First(), cell = w.Settlements.Get(sid).Cell;
            System.Func<Species, bool> hunters = sp => sp.Habitat == 0 && sp.Gone == 0 && sp.Prey != 0;
            System.Func<Species, bool> game = sp => sp.Habitat == 0 && sp.Gone == 0 && sp.Prey == 0;
            double h0 = Within(w, cell, hunters), g0 = Within(w, cell, game);
            Assert.Greater(h0, 1.0, "premise: hunting beasts round the village");
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodCull, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            double h1 = Within(w, cell, hunters);
            Assert.AreEqual(h0 * (1.0 - World.CullShare), h1, h0 * 0.02, "nine in ten of them taken");
            Assert.AreEqual(g0, Within(w, cell, game), g0 * 1e-6 + 1e-6, "the game left as it was");
            long cast = 0; int told = 0; double took = 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.GodCommand && e.ArgA == (int)CommandKind.GodCull) { cast = e.Id; StringAssert.StartsWith("You culled the hunting beasts", HistoryText.Describe(w, in e)); }
                if (e.Kind != HistoryKind.BeastsCulled) continue;
                told++; took += e.Magnitude;
                Assert.AreEqual(cast, e.Cause1, "caused by the cast"); Assert.AreEqual(RootKind.You, e.RootKind);
                StringAssert.StartsWith("Nine in ten of the", HistoryText.Describe(w, in e));
            }
            Assert.Greater(told, 0, "each kind's cull told");
            Assert.AreEqual(h0 - h1, took, (h0 - h1) * 0.01 + 1.0, "as many as were taken");
            Assert.Greater(new HistoryBook.Writer(w).Part(HistoryBook.YourActionsPart).Chapters.Count, 0, "in Your Actions");
            // and the hunters beyond the reach wander in, as the world's own laws move them
            w.AdvanceTo(w.Now + SimTime.Years(2));
            Assert.Greater(Within(w, cell, hunters), h1 * 1.5, "more wandered in");
        }

        [Test]
        public void BringingBringsTheGameTheLandLacksMostAndAtSeaAShoal()
        {
            var w = TestWorlds.Make(619);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            var P = w.Planet;
            int sid = w.Settlements.First(), cell = w.Settlements.Get(sid).Cell;
            // premise: the deer round the village hunted out (none left within the reach) - the land lacks them most
            int deer = -1;
            for (int s = 0; s < P.Species.Count; s++) if (P.Species[s].Kind == 0 && P.Species[s].Gone == 0 && P.Species[s].Habitat == 0) { deer = s; break; }
            Assert.GreaterOrEqual(deer, 0, "premise: deer in the world");
            for (int q = 0; q < P.Cells; q++)
                if (P.IsLand(q) && Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[q], P.Radius) <= World.GodReach) P.Abund[deer * P.Cells + q] = 0f;
            // the rule: the kind with the most kilos lacking within the reach - each cell short of as thick as the kind lives over its range for what
            // the land offers it (World.Stocking), as far as the cell suits it
            int expect = -1; double most = 0;
            for (int s = 0; s < P.Species.Count; s++)
            {
                var sp = P.Species[s];
                if (sp.Habitat != 0 || sp.Gone != 0 || sp.Prey != 0 || sp.Mass < 2f) continue;
                double stock = w.Stocking(sp, s, out int src);
                if (stock <= 0 || src < 0) continue;
                double lack = 0;
                for (int q = 0; q < P.Cells; q++)
                {
                    if (!P.IsLand(q) || Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[q], P.Radius) > World.GodReach) continue;
                    double want = stock * P.AreaHa(q) * Ecology.Suitability(P, sp, q), a = System.Math.Max(0.0, P.Abund[s * P.Cells + q]);
                    if (want > a) lack += (want - a) * sp.Mass;
                }
                if (lack > most + 1e-9) { most = lack; expect = s; }
            }
            Assert.GreaterOrEqual(expect, 0, "premise: game the land lacks");
            Console.WriteLine("the land lacks most: " + Ecology.KindName(P.Species[expect].Kind) + " (" + most.ToString("0") + " kg); the deer hunted out");
            int bid = P.Species[expect].Id;
            System.Func<Species, bool> it = sp => sp.Id == bid;
            System.Func<Species, bool> others = sp => sp.Id != bid;
            double b0 = Within(w, cell, it), o0 = Within(w, cell, others);
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodBring, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            double b1 = Within(w, cell, it);
            Assert.Greater(b1, b0 + 1.0, "brought");
            Assert.AreEqual(o0, Within(w, cell, others), o0 * 1e-6 + 1e-6, "no other kind brought");
            int told = 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.BeastsBrought) continue;
                told++;
                Assert.AreEqual(new EntityRef(EntityKind.Species, bid), e.Subject, "the kind the land lacks most by weight");
                Assert.AreEqual(b1 - b0, e.Magnitude, (b1 - b0) * 0.01 + 1.0, "as many as came");
                Assert.AreEqual(RootKind.You, e.RootKind);
                StringAssert.Contains("as thick as they live where they do best", HistoryText.Describe(w, in e));
            }
            Assert.AreEqual(1, told, "told once");
            // at sea: a shoal of a kind that hunts nothing, in the waters off the coast
            int sea = -1;
            for (int q = 0; q < P.Cells && sea < 0; q++)
            {
                if (P.IsLand(q)) continue;
                for (int s = 0; s < P.Species.Count; s++) { var sp = P.Species[s]; if (sp.Habitat == 1 && sp.Gone == 0 && sp.Prey == 0 && Ecology.Suitability(P, sp, q) > 0.3) { sea = q; break; } }
            }
            Assert.GreaterOrEqual(sea, 0, "premise: a sea some shoal suits");
            System.Func<Species, bool> fish = sp => sp.Habitat == 1 && sp.Prey == 0;
            double f0 = Within(w, sea, fish);
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodBring, Cell = sea });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.Greater(Within(w, sea, fish), f0, "a shoal brought");
            bool seaTold = false;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.BeastsBrought && e.ArgA == 1) { seaTold = true; StringAssert.Contains("the waters off", HistoryText.Describe(w, in e)); } }
            Assert.IsTrue(seaTold, "the shoal told");
        }

        [Test]
        public void WarmAirWarmsTheLandForItsDaysAndASaveKeepsIt()
        {
            var w = TestWorlds.Make(620);
            var twin = TestWorlds.Make(620);
            Coarse(w); Coarse(twin);
            w.AdvanceTo(w.Now + SimTime.Days(2)); twin.AdvanceTo(w.Now);
            int sid = w.Settlements.First(), cell = w.Settlements.Get(sid).Cell;
            var P = w.Planet;
            int far = -1;
            for (int q = 0; q < P.Cells && far < 0; q++) if (Vec3d.ArcDistance(P.Grid.Center[cell], P.Grid.Center[q], P.Radius) > 2 * World.AirReach) far = q;
            double season = PlanetSystems.Season(w.Now);
            double t0 = w.PlanetSys.DayMean(cell, season), f0 = w.PlanetSys.DayMean(far, season);
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodWarm, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.AreEqual(t0 + World.AirBy, w.PlanetSys.DayMean(cell, season), 1e-9, "the summer's air warmer there by its degrees");
            Assert.AreEqual(f0, w.PlanetSys.DayMean(far, season), 1e-12, "and not beyond its reach");
            var w2 = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Living), out _);
            w.AdvanceTo(w.Now + SimTime.Days(3)); twin.AdvanceTo(w.Now);
            Assert.AreEqual(twin.Planet.Temp[cell] + World.AirBy, w.Planet.Temp[cell], 1e-3, "the day's air warmer than the uncast twin's");
            Assert.AreEqual(twin.Planet.Temp[far], w.Planet.Temp[far], 1e-6, "the far air as the twin's");
            w2.AdvanceTo(w.Now);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a save under the warm air continues as if never saved");
            w.AdvanceTo(w.Now + SimTime.Days(World.AirDays));
            Assert.AreEqual(0.0, w.AirShift(cell), 1e-12, "the air as it was after its days");
            long aev = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.WarmAir) { aev = e.Id; Assert.AreEqual(RootKind.You, e.RootKind); StringAssert.StartsWith("Warm air came over the land", HistoryText.Describe(w, in e)); } }
            Assert.AreNotEqual(0L, aev, "told, rooted in the cast");
        }

        [Test]
        public void ColdAirChillsTheLandAndItsStandingCropsNameIt()
        {
            var w = TestWorlds.Make(621);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            int sid = w.Settlements.First(), cell = w.Settlements.Get(sid).Cell;
            w.Settlements.Get(sid).CropCause = 0;
            double season = PlanetSystems.Season(w.Now), t0 = w.PlanetSys.DayMean(cell, season);
            w.Issue(new Command { Time = w.Now, Kind = CommandKind.GodCold, Cell = cell });
            w.AdvanceTo(w.Now + SimTime.Hours(1));
            Assert.AreEqual(t0 - World.AirBy, w.PlanetSys.DayMean(cell, season), 1e-9, "colder by its degrees");
            long aev = 0;
            for (int i = 0; i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.ColdAir) { aev = e.Id; StringAssert.StartsWith("Cold air came over the land", HistoryText.Describe(w, in e)); } }
            Assert.AreNotEqual(0L, aev, "told");
            Assert.AreEqual(aev, w.Settlements.Get(sid).CropCause, "its standing crop names the cold as its harm, as under a drought");
        }

        [Test]
        public void WithNoCastTheWorldsHistoryIsItsOwn()
        {
            var w = TestWorlds.Make(612);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Days(3));
            var part = new HistoryBook.Writer(w).Part(HistoryBook.YourActionsPart);
            Assert.AreEqual(1, part.Chapters.Count);
            Assert.AreEqual("Nothing yet", part.Chapters[0].Title);
        }
    }
}
