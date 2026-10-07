using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// The lord's call (C10.1, D2-107): against those who wronged it, a lord's band is joined by people of its tributaries who answer the call
    /// (owed, never against their kin), and they go home with their share; a raid on a tributary is a wrong to its lord too. Mechanisms.
    /// </summary>
    [TestFixture]
    public class LordshipTests
    {
        /// <summary>Three villages each within a day's walk of the others by a route: a lord, its tributary, and their foe.</summary>
        static World Three(out int lord, out int trib, out int foe)
        {
            var w = WorldGenerator.Create(new WorldConfig { Seed = 42 });
            var ids = new List<int>();
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) ids.Add(s);
            bool Near(int a, int b) => Vec3d.ArcDistance(w.Settlements.Get(a).Anchor, w.Settlements.Get(b).Anchor, w.Planet.Radius) <= 6000 && w.Routes.Get(w, a, b) != null;
            foreach (int a in ids) foreach (int b in ids) foreach (int c in ids)
                    {
                        if (a == b || b == c || a == c) continue;
                        if (Near(a, b) && Near(a, c) && Near(b, c)) { lord = a; trib = b; foe = c; w.Settlements.Get(trib).Lord = lord; return w; }
                    }
            lord = trib = foe = 0;
            return null;
        }

        /// <summary>Premise: the lord holds a heavy wrong against the foe (its people took and struck there).</summary>
        static void Wronged(World w, int lord, int foe)
        {
            ref var s = ref w.Settlements.Get(lord);
            s.Foe = foe; s.FoeWorth = (float)Conflict.RaidFull; s.FoeEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Settlement = lord, Subject = EntityRef.Settlement(foe), Object = EntityRef.Settlement(lord) });
        }

        [Test]
        public void ALordsBandIsJoinedByItsTributariesPeople()
        {
            var w = Three(out int lord, out int trib, out int foe);
            Assume.That(w != null, "three villages near each other");
            Wronged(w, lord, foe);
            long mark = w.History.LastId;
            int band = w.Conflict.RaiseBandNow(lord);
            Assume.That(band != 0, "the lord's people dare it");
            var b = w.Bands.Get(band);
            int fromTrib = 0, fromLord = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                if (p.Band != band) continue;
                if (p.Home == trib) fromTrib++; else if (p.Home == lord) fromLord++;
            }
            Console.WriteLine("band of " + b.Size + ": " + fromLord + " of the lord's village, " + fromTrib + " of its tributary; called " + b.Called);
            Assert.Greater(fromTrib, 0, "its tributary's people answered the call");
            Assert.AreEqual(1, b.Called);
            Assert.AreEqual(b.Size, fromLord + fromTrib);
            bool told = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
                if (w.History.At(i).Kind == HistoryKind.CallAnswered) { told = true; Assert.AreEqual(trib, w.History.At(i).Subject.Id); Console.WriteLine(HistoryText.Line(w, w.History.At(i))); }
            Assert.IsTrue(told);
            // without the tie of tribute: its own people only
            var w2 = Three(out int lord2, out int trib2, out int foe2);
            w2.Settlements.Get(trib2).Lord = 0;
            Wronged(w2, lord2, foe2);
            int band2 = w2.Conflict.RaiseBandNow(lord2);
            if (band2 != 0) Assert.AreEqual(0, w2.Bands.Get(band2).Called, "no tributary, no call");
        }

        [Test]
        public void ARaidOnATributaryIsAWrongToItsLord()
        {
            var w = Three(out int lord, out int trib, out int foe);
            Assume.That(w != null, "three villages near each other");
            Assert.AreEqual(0.0, w.DeedBook.Grievance(lord, foe));
            var raiders = new List<int>();
            var res = w.Residents(foe);
            for (int i = 0; i < res.Count && raiders.Count < 5; i++) { var q = w.People.Get(res[i]); if (q.Alive && q.AgeYears(w.Now) >= 18 && q.AgeYears(w.Now) < 45 && q.Health > 0.8) raiders.Add(res[i]); }
            Assume.That(raiders.Count >= 3, "raiders");
            w.Conflict.FallNow(foe, trib, raiders, seen: true);
            Assert.Greater(w.DeedBook.Grievance(trib, foe), 0.0, "the tributary holds it");
            Assert.Greater(w.DeedBook.Grievance(lord, foe), 0.0, "and its lord takes it as a wrong to itself");
            Assert.Less(w.DeedBook.Grievance(lord, foe), w.DeedBook.Grievance(trib, foe), "half of it");
        }

        [Test]
        public void TributariesPeopleGoHomeWithTheirShare()
        {
            var w = Three(out int lord, out int trib, out int foe);
            Assume.That(w != null, "three villages near each other");
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Days(2));
            Wronged(w, lord, foe);
            int band = w.Conflict.RaiseBandNow(lord);
            Assume.That(band != 0, "the lord's people dare it");
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                var p = w.People.Get(id);
                Assert.AreNotEqual(band, p.Band, "every member home again, the band over");
                if (p.Home == trib) Assert.AreEqual(0f, p.CargoQty, "what they carried unloaded at home");
            }
        }
    }
}
