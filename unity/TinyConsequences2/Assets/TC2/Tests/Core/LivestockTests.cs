using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Livestock (D2-048): households' animals as individuals that graze the same grass the wild live on, breed in their season
    /// with a sire near, give milk and meat, are kept to what hands can tend and the land feeds, and outlive their keepers.
    /// Mechanisms, not outcomes: no test asks for a herd of a size.
    /// </summary>
    [TestFixture]
    public class LivestockTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static List<int> Live(World w, Func<Beast, bool> where = null)
        {
            var list = new List<int>();
            for (int id = w.Beasts.First(); id != 0; id = w.Beasts.Next(id)) { var b = w.Beasts.Get(id); if (b.Alive && (where == null || where(b))) list.Add(id); }
            return list;
        }

        /// <summary>Premise: an animal is gone from the world (as if never there).</summary>
        static void Remove(World w, int id)
        {
            ref var b = ref w.Beasts.Get(id);
            b.Alive = false; b.Died = w.Now;
            w.BeastsOf(b.Owner).Remove(id);
        }

        static List<int> Pasture(World w, int sid, double reach)
        {
            var list = new List<int>();
            w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, reach, list);
            return new List<int>(list);
        }

        static int VillageWithMost(World w)
        {
            int best = 0, most = -1;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                if (!w.Settlements.Get(sid).Inhabited) continue;
                int n = w.Stock.CountOf(sid, Livestock.Cattle) * 5 + w.Stock.CountOf(sid, Livestock.Goat) + w.Stock.CountOf(sid, Livestock.Pig);
                if (n > most) { most = n; best = sid; }
            }
            return best;
        }

        [Test]
        public void TheYoungAreBornToTheirMothersHouseholdsInTheSeason()
        {
            var w = TestWorlds.Make(401);
            Coarse(w);
            Assert.Greater(Live(w).Count, 0, "the first households keep animals");
            int seen = 0;
            int top = Live(w).Count > 0 ? Live(w)[Live(w).Count - 1] : 0;
            for (int d = 0; d < 2 * SimTime.DaysPerYear; d++)
            {
                w.AdvanceTo(w.Now + SimTime.Day);
                foreach (int id in Live(w, b => b.Id > top))
                {
                    var b = w.Beasts.Get(id);
                    if (b.Mother == 0) continue;
                    var m = w.Beasts.Get(b.Mother);
                    Assert.AreEqual(m.Kind, b.Kind, "a mother bears her own kind");
                    Assert.AreEqual(Sex.Female, m.Sex);
                    Assert.GreaterOrEqual(m.AgeYears(b.Born), Livestock.Laws[m.Kind].Mature, "only grown females breed");
                    Assert.Greater(m.MilkUntil, b.Born, "she comes into milk");
                    if (b.Born > w.Now - SimTime.Day) Assert.AreEqual(m.Owner, b.Owner, "the young are her household's");
                    seen++;
                    top = Math.Max(top, id);
                }
            }
            Assert.Greater(seen, 0, "animals are born within two years");
            Assert.Greater(w.Metrics.BeastBirths, 0);
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void NoSireNoYoung()
        {
            // premise: every male goat in the world gone - the does have no buck to go to, in their village or the next
            var w = TestWorlds.Make(402);
            Coarse(w);
            foreach (int id in Live(w, b => b.Kind == Livestock.Goat && b.Sex == Sex.Male)) Remove(w, id);
            foreach (int id in Live(w, b => b.Kind == Livestock.Goat && b.Due != 0)) w.Beasts.Get(id).Due = 0;   // (and none already with kid)
            int top = 0; foreach (int id in Live(w)) top = Math.Max(top, id);
            int does = Live(w, b => b.Kind == Livestock.Goat).Count;
            Assert.Greater(does, 0);
            w.AdvanceTo(w.Now + SimTime.Years(1));
            Assert.AreEqual(0, Live(w, b => b.Kind == Livestock.Goat && b.Id > top).Count, "no kids without a buck");
            Assert.Greater(Live(w, b => b.Kind != Livestock.Goat && b.Id > top).Count, 0, "the other kinds still breed");
        }

        [Test]
        public void GrazingTakesTheGrassTheWildGrazersLiveOn()
        {
            var a = TestWorlds.Make(403); var b = TestWorlds.Make(403);
            Coarse(a); Coarse(b);
            int sid = VillageWithMost(a);
            // premise: in one copy the households keep no animals; in the other, the first village's herds are many
            foreach (int id in Live(b)) Remove(b, id);
            int hid = a.HouseholdsOf(sid)[0];
            for (int i = 0; i < 30; i++) a.Stock.Add(Livestock.Cattle, i % 2 == 0 ? Sex.Female : Sex.Male, a.Now - SimTime.Years(4), 0, hid);
            var cells = Pasture(a, sid, Livestock.PastureReach);
            double Grass(World w) { double g = 0; int n = 0; foreach (int c in cells) if (w.Planet.IsLand(c) && a.Planet.FieldOwner[c] == 0 && b.Planet.FieldOwner[c] == 0) { g += w.Planet.Grass[c]; n++; } return g / Math.Max(1, n); }
            double g0 = Grass(a);
            Assert.AreEqual(g0, Grass(b), 1e-9, "the same land at the start");
            for (int d = 0; d < 12; d++) { a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day); }
            double ga = Grass(a), gb = Grass(b);
            Console.WriteLine("pasture grass after 12 days: grazed " + ga.ToString("0.0000") + " ungrazed " + gb.ToString("0.0000"));
            Assert.Less(ga, gb, "the herds eat the pasture's grass");
            // the wild grazers' next step finds less grass where the herds grazed: their condition there is worse
            Ecology.Step(a, 0.25, a.Now); Ecology.Step(b, 0.25, b.Now);
            var P = a.Planet; int nc = P.Cells; double ca = 0, wa = 0, cb = 0, wb = 0;
            for (int q = 0; q < P.Species.Count; q++)
            {
                if (P.Species[q].Habitat != 0 || P.Species[q].Grass < 0.5f) continue;
                foreach (int c in cells)
                {
                    double x = a.Planet.Abund[q * nc + c], y = b.Planet.Abund[q * nc + c];
                    ca += x * a.Planet.Cond[q * nc + c]; wa += x; cb += y * b.Planet.Cond[q * nc + c]; wb += y;
                }
            }
            Console.WriteLine("wild grazers round the pasture: " + wa.ToString("0.0") + " (condition " + (ca / Math.Max(1e-9, wa)).ToString("0.000") + ") against " + wb.ToString("0.0") + " (" + (cb / Math.Max(1e-9, wb)).ToString("0.000") + ")");
            if (wa > 0.5 && wb > 0.5) Assert.LessOrEqual(ca / wa, cb / wb + 1e-9, "the wild grazers ate no better where the herds grazed");
        }

        [Test]
        public void AHungryHouseholdEatsItsHerdButKeepsAPair()
        {
            var w = TestWorlds.Make(404);
            Coarse(w);
            int sid = VillageWithMost(w);
            int hid = 0, most = 0;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++) if (w.BeastsOf(hhs[i]).Count > most && w.Members(hhs[i]).Count > 0) { most = w.BeastsOf(hhs[i]).Count; hid = hhs[i]; }
            // premise: enough goats to eat from, none heavy with kid or in milk, and the larder and the granary bare
            for (int i = 0; i < 6; i++) w.Stock.Add(Livestock.Goat, Sex.Female, w.Now - SimTime.Years(3), 0, hid);
            var mine = w.BeastsOf(hid);
            for (int i = 0; i < mine.Count; i++) { ref var b = ref w.Beasts.Get(mine[i]); b.Due = 0; b.MilkUntil = 0; b.Cond = 0.8f; }
            var before = new int[Livestock.Kinds];
            for (int i = 0; i < mine.Count; i++) before[w.Beasts.Get(mine[i]).Kind]++;
            long eaten = w.Metrics.BeastsEaten; double fu = w.Metrics.BeastFu;
            w.Settlements.Get(sid).Granary = 0;
            for (int i = 0; i < hhs.Count; i++) w.Households.Get(hhs[i]).Larder = 0;
            w.Stock.Day(sid);   // the herds' day with the stores bare (no hunter home first)
            Assert.Greater(w.Metrics.BeastsEaten, eaten, "a hungry household slaughters");
            Assert.Greater(w.Metrics.BeastFu, fu, "and the meat is food");
            var after = new int[Livestock.Kinds];
            mine = w.BeastsOf(hid);
            for (int i = 0; i < mine.Count; i++) after[w.Beasts.Get(mine[i]).Kind]++;
            for (int k = 0; k < Livestock.Kinds; k++) Assert.GreaterOrEqual(after[k], Math.Min(2, before[k]), "a pair of each kind is kept to breed again");
            Assert.Less(after[Livestock.Goat], before[Livestock.Goat], "the goats beyond the pair are eaten");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void WolvesTakeAnimalsOnlyWhereWolvesAre()
        {
            var a = TestWorlds.Make(405); var b = TestWorlds.Make(405);
            Coarse(a); Coarse(b);
            var P = a.Planet; int n = P.Cells;
            int wolf = -1; for (int q = 0; q < P.Species.Count; q++) if (P.Species[q].Kind == 5 && P.Species[q].Habitat == 0) wolf = q;
            Assert.GreaterOrEqual(wolf, 0, "wolves live in this world");
            // premise: a pack on every village's pasture in one copy, none within reach in the other (set again each day: wolves wander)
            void Packs(World w, float per)
            {
                for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
                    foreach (int c in Pasture(w, sid, Livestock.FarReach + 300)) if (w.Planet.IsLand(c)) w.Planet.Abund[wolf * n + c] = per;
            }
            for (int d = 0; d < 30; d++)
            {
                Packs(a, 2f); Packs(b, 0f);
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
            }
            Console.WriteLine("taken by wolves in 30 days: " + a.Metrics.BeastsTaken + " with packs, " + b.Metrics.BeastsTaken + " without");
            Assert.Greater(a.Metrics.BeastsTaken, 0, "wolves round the pasture take animals");
            Assert.AreEqual(0, b.Metrics.BeastsTaken, "no wolves, no animals taken");
        }

        [Test]
        public void TheAnimalsOfAHouseholdThatDiesOutPassToTheLiving()
        {
            var w = TestWorlds.Make(406);
            int sid = VillageWithMost(w);
            int hid = 0; var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count && hid == 0; i++) if (w.BeastsOf(hhs[i]).Count > 0) hid = hhs[i];
            Assert.AreNotEqual(0, hid);
            var animals = new List<int>(); var mine = w.BeastsOf(hid); for (int i = 0; i < mine.Count; i++) animals.Add(mine[i]);
            // premise: everyone of that household dies
            var ms = w.Members(hid); var people = new List<int>(); for (int i = 0; i < ms.Count; i++) people.Add(ms[i]);
            foreach (int pid in people) { ref var p = ref w.People.Get(pid); w.Agents.Kill(ref p, 0, 1); }
            w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hour);   // (the village's day turns at its own midnight)
            Assert.AreEqual(0, w.BeastsOf(hid).Count, "a household with nobody left keeps no animals");
            foreach (int id in animals)
            {
                var b = w.Beasts.Get(id);
                if (!b.Alive) continue;
                Assert.AreNotEqual(hid, b.Owner);
                Assert.AreEqual(sid, w.Households.Get(b.Owner).Settlement, "they stay in their village");
                Assert.Greater(w.Members(b.Owner).Count, 0, "with a household that has people");
            }
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void AnAbandonedVillagesAnimalsGoWild()
        {
            var w = TestWorlds.Make(407);
            int sid = VillageWithMost(w);
            int cell = w.Settlements.Get(sid).Cell;
            var P = w.Planet;
            int pigs = w.Stock.CountOf(sid, Livestock.Pig), all = pigs + w.Stock.CountOf(sid, Livestock.Goat) + w.Stock.CountOf(sid, Livestock.Cattle);
            Assert.Greater(all, 0);
            long feral = w.Metrics.BeastsFeral, died = w.Metrics.BeastDeaths;
            // premise: everyone in the village dies
            var rs = w.Residents(sid); var people = new List<int>(); for (int i = 0; i < rs.Count; i++) people.Add(rs[i]);
            foreach (int pid in people) { ref var p = ref w.People.Get(pid); w.Agents.Kill(ref p, 0, 1); }
            w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hour);   // (the village's day turns at its own midnight)
            Assert.IsFalse(w.Settlements.Get(sid).Inhabited, "the village is abandoned");
            Assert.AreEqual(0, w.Stock.CountOf(sid, Livestock.Pig) + w.Stock.CountOf(sid, Livestock.Goat) + w.Stock.CountOf(sid, Livestock.Cattle), "nobody keeps its animals");
            Assert.AreEqual(all, (w.Metrics.BeastsFeral - feral) + (w.Metrics.BeastDeaths - died), "each went wild or died (none of its wild kind lives here)");
            Assert.Greater(w.Metrics.BeastsFeral, feral, "the wild of their kinds live here: they joined them");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void AHouseholdKeepsWhatItsHandsCanTend()
        {
            var w = TestWorlds.Make(408);
            Coarse(w);
            int sid = VillageWithMost(w);
            int hid = 0; var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count && hid == 0; i++) if (w.Members(hhs[i]).Count > 0) hid = hhs[i];
            // premise: sixty grown goats more than anyone could herd
            for (int i = 0; i < 60; i++) w.Stock.Add(Livestock.Goat, i % 3 == 0 ? Sex.Male : Sex.Female, w.Now - SimTime.Years(2 + (i % 5)), 0, hid);
            w.AdvanceTo(w.Now + SimTime.Day + SimTime.Hour);
            double eats = 0; var mine = w.BeastsOf(hid);
            var kinds = new int[Livestock.Kinds];
            for (int i = 0; i < mine.Count; i++) { var b = w.Beasts.Get(mine[i]); eats += Livestock.IntakeDay(b.Kind) * Livestock.GrownShare(in b, w.Now); kinds[b.Kind]++; }
            double cap = w.Stock.Hands(hid) * Livestock.TendKg;
            Console.WriteLine("hands " + w.Stock.Hands(hid).ToString("0.0") + ", herd eats " + eats.ToString("0.0") + " kg a day of " + cap.ToString("0.0") + "; goats " + kinds[Livestock.Goat]);
            bool coreOnly = kinds[Livestock.Goat] <= Livestock.CoreMales + Livestock.Laws[Livestock.Goat].Core && kinds[Livestock.Cattle] <= Livestock.CoreMales + Livestock.Laws[Livestock.Cattle].Core && kinds[Livestock.Pig] <= Livestock.CoreMales + Livestock.Laws[Livestock.Pig].Core;
            Assert.IsTrue(eats <= cap + 1e-9 || coreOnly, "the herd is cut to what its hands can tend, or to its breeding core");
            Assert.Less(kinds[Livestock.Goat], 60);
        }

        [Test]
        public void SaveLoadWithHerdsContinuesIdentically()
        {
            var w = TestWorlds.Make(409);
            w.AdvanceTo(w.Now + SimTime.Days(30));
            Assert.Greater(Live(w).Count, 0);
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            var w2 = SaveFile.Load(bytes, out _);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "loaded as saved");
            long end = w.Now + SimTime.Days(20);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a world saved with its herds continues as if never saved");
            Assert.IsNull(TestWorlds.Audit(w2));
        }

        [Test]
        public void CountingTheHerdsChangesNothing()
        {
            var a = TestWorlds.Make(410); var b = TestWorlds.Make(410);
            for (int d = 0; d < 20; d++)
            {
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
                for (int sid = a.Settlements.First(); sid != 0; sid = a.Settlements.Next(sid))
                    for (byte k = 0; k < Livestock.Kinds; k++) a.Stock.CountOf(sid, k);
            }
            Assert.AreEqual(a.StateHash(), b.StateHash(), "watching the herds never changes them");
        }

        [Test]
        public void CoarseAndActivityKeepHerdsAlike()
        {
            var a = TestWorlds.Make(411); var b = TestWorlds.Make(411);
            Coarse(b);
            a.AdvanceTo(a.Now + SimTime.Years(3)); b.AdvanceTo(a.Now);
            int na = Live(a).Count, nb = Live(b).Count;
            Console.WriteLine("animals after three years: activity " + na + " (born " + a.Metrics.BeastBirths + ", slaughtered " + a.Metrics.BeastsEaten + ") coarse " + nb + " (born " + b.Metrics.BeastBirths + ", slaughtered " + b.Metrics.BeastsEaten + ")");
            Assert.Greater(na, 0); Assert.Greater(nb, 0);
            Assert.That((double)b.Metrics.BeastBirths / Math.Max(1, a.Metrics.BeastBirths), Is.InRange(0.6, 1.6), "the herds breed alike at both resolutions");
            Assert.That((double)nb / na, Is.InRange(0.5, 2.0), "and are kept alike");
            Assert.IsNull(TestWorlds.Audit(a)); Assert.IsNull(TestWorlds.Audit(b));
        }
    }
}
