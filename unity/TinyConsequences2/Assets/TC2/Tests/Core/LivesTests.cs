using System;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// (session 29: the person as a person) A life told from the world: the chronicle oldest first, every entry a record of theirs or their kin's state; the
    /// family tree as the people keep it; the temperament only where it is marked. Reads only.
    /// </summary>
    [TestFixture]
    public class LivesTests
    {
        static World Lived(ulong seed, int years)
        {
            var w = TestWorlds.Make(seed);
            w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            w.AdvanceTo(w.Now + SimTime.Years(years));
            return w;
        }

        /// <summary>The living person with the most children (a long life to tell).</summary>
        static int Parent(World w)
        {
            int best = 0, most = -1;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                int n = 0;
                for (int c = w.People.First(); c != 0; c = w.People.Next(c)) { ref var q = ref w.People.Get(c); if (q.Mother == id || q.Father == id) n++; }
                if (n > most) { most = n; best = id; }
            }
            return best;
        }

        [Test]
        public void ALifeIsToldOldestFirstFromItsRecordsAndItsKin()
        {
            var w = Lived(621, 30);
            int pid = Parent(w);
            Assert.AreNotEqual(0, pid);
            var life = Lives.Chronicle(w, pid);
            Console.WriteLine(Biography.Title(w, pid) + ": " + life.Count + " entries");
            foreach (var e in life) Console.WriteLine("  " + SimTime.YearOf(e.Time) + " (" + e.Age + ") [" + e.Kind + "/" + e.Weight + "] " + e.Text);
            Assert.Greater(life.Count, 0);
            for (int i = 1; i < life.Count; i++) Assert.LessOrEqual(life[i - 1].Time, life[i].Time, "oldest first");
            int births = 0;
            foreach (var e in life)
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.Text));
                if (e.Event != 0) Assert.IsTrue(w.History.TryGet(e.Event, out _), "an entry on a record stands on one the ledger holds");
                if (e.Person != 0) Assert.IsTrue(w.People.Exists(e.Person), "a person it names can be gone to");
                if (e.Kind == Lives.Family && e.Text.Contains("was born") && !e.Text.StartsWith("Became a grandparent")) births++;
            }
            int children = 0;
            for (int c = w.People.First(); c != 0; c = w.People.Next(c)) { ref var q = ref w.People.Get(c); if ((q.Mother == pid || q.Father == pid) && q.Born >= w.People.Get(pid).Born) children++; }
            Assert.AreEqual(children, births, "each child's birth told once, by its record or by the child");
        }

        [Test]
        public void TheTreeHoldsEachChildUnderTheirOtherParent()
        {
            var w = Lived(623, 30);
            int pid = Parent(w);
            var t = Lives.KinOf(w, pid);
            ref var p = ref w.People.Get(pid);
            Assert.AreEqual(p.Mother, t.Mother); Assert.AreEqual(p.Father, t.Father);
            int n = 0;
            for (int k = 0; k < t.ChildrenOf.Count; k++)
                foreach (int c in t.ChildrenOf[k])
                {
                    ref var q = ref w.People.Get(c);
                    Assert.IsTrue(q.Mother == pid || q.Father == pid);
                    int other = q.Mother == pid ? q.Father : q.Mother;
                    if (k < t.Partners.Count) Assert.AreEqual(t.Partners[k], other, "under the partner they were born to");
                    n++;
                }
            int children = 0;
            for (int c = w.People.First(); c != 0; c = w.People.Next(c)) { ref var q = ref w.People.Get(c); if (q.Mother == pid || q.Father == pid) children++; }
            Assert.AreEqual(children, n, "every child in the tree once");
            foreach (int s in t.Siblings) Assert.AreNotEqual(pid, s);
            Console.WriteLine(Biography.Title(w, pid) + ": " + t.Partners.Count + " partners, " + n + " children, " + t.Siblings.Count + " brothers and sisters, " + t.Grandchildren.Count + " grandchildren");
        }

        [Test]
        public void TheFirstsHouseholdIsFollowedAndReadingItChangesNothing()
        {
            var a = TestWorlds.Make(627); var b = TestWorlds.Make(627);
            a.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(a.Now) + SimTime.Day);
            b.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(b.Now) + SimTime.Day);
            int told = 0;
            for (int d = 0; d < 20 * 48; d++)
            {
                a.AdvanceTo(a.Now + SimTime.Day);
                b.AdvanceTo(b.Now + SimTime.Day);
                if (d % 12 != 0) continue;
                for (int sid = b.Settlements.First(); sid != 0; sid = b.Settlements.Next(sid)) { int h = b.Settlements.Get(sid).Head; if (h != 0) told += Lives.Following(b, h).Count; }
            }
            Assert.AreEqual(a.StateHash(), b.StateHash(), "a world whose followings were read lives as the one never read");
            Assert.Greater(told, 0);
            bool firstSaid = false;
            for (int sid = b.Settlements.First(); sid != 0; sid = b.Settlements.Next(sid))
            {
                int h = b.Settlements.Get(sid).Head;
                if (h == 0) continue;
                var l = Lives.Following(b, h);
                foreach (var s in l) Console.WriteLine(Biography.Title(b, h) + ": " + s);
                if (l.Exists(s => s.Contains("theirs is the first household"))) firstSaid = true;
            }
            Assert.IsTrue(firstSaid, "a first's household is told as followed the most");
        }

        [Test]
        public void ATemperamentIsToldOnlyWhereItIsMarked()
        {
            var p = new Person { Bold = 128, Social = 128, Diligence = 128, Chrono = 128, Pace = 128, Curious = 128 };
            Assert.AreEqual(0, Lives.Temperament(in p).Count, "a middling temperament has nothing to tell");
            p.Bold = 240; p.Chrono = 20;
            var l = Lives.Temperament(in p);
            Assert.AreEqual(2, l.Count);
            Assert.AreEqual("Bold", l[0][0]);
            Assert.AreEqual("An early riser", l[1][0]);
        }
    }
}
