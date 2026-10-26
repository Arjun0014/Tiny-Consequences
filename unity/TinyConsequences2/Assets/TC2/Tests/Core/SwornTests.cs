using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Simulation;
using TC2.Core.Society;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Sworn men (C10.7b, D2-165): the one first among a village's people, whose household's grain runs years beyond its needs, keeps young men of the
    /// village sworn to them - the landless and land-short first, then the boldest - a year's food each at its harvest, from the household's bin into
    /// theirs; they practise arms every day at the village's ground and go with every band it raises; released when the grain is short, when another is
    /// first among the people, when their lord dies, when they pass forty or leave.
    /// Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class SwornTests
    {
        /// <summary>Premise: a village whose first household is weighed and with more willing men than `men`, its first household's grain set to keep
        /// `men` (no fear of its neighbours: only the sworn practise).</summary>
        static World Village(out int sid, out int first, int men = 3, ulong from = 1101)
        {
            for (ulong seed = from; seed < from + 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s))
                {
                    if (!w.Settlements.Get(s).Inhabited || Forge.Threatened(in w.Settlements.Get(s))) continue;
                    Leaders.Weigh(w, s);
                    int f = w.Settlements.Get(s).HeadHh;
                    if (f == 0 || Candidates(w, s).Count < men + 1) continue;
                    Feed(w, s, f, men);
                    sid = s; first = f;
                    return w;
                }
            }
            sid = first = 0;
            return null;
        }

        /// <summary>The men who may swear, ranked as the first household takes them: the least land a hand first, then the boldest, then the lower id.</summary>
        static List<int> Candidates(World w, int sid)
        {
            var c = new List<(double land, int bold, int pid)>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var q = w.People.Get(res[i]);
                double age = q.AgeYears(w.Now);
                if (!q.Alive || q.SexKind != Sex.Male || q.Household == 0 || q.Bold < Training.BoldFrom || age < Retinue.FromAge || age >= Retinue.ToAge || !Conflict.Fit(in q, w.Now)) continue;
                if (w.Settlements.Get(sid).Head == q.Id || w.Households.Get(q.Household).Party != 0) continue;
                double hands = w.Holdings.Hands(q.Household), land = w.Households.Get(q.Household).Land;
                c.Add((hands > 0 ? land / hands : land, q.Bold, q.Id));
            }
            c.Sort((a, b) => a.land != b.land ? a.land.CompareTo(b.land) : a.bold != b.bold ? b.bold.CompareTo(a.bold) : a.pid.CompareTo(b.pid));
            var l = new List<int>();
            foreach (var x in c) l.Add(x.pid);
            return l;
        }

        /// <summary>The first household's grain set to keep `men`: its own next year and `KeepYears` more, then a man's year for each of those years for
        /// each man and half a man more (the store holding what its bins hold).</summary>
        static void Feed(World w, int sid, int first, int men)
        {
            ref var hh = ref w.Households.Get(first);
            double want = w.SettlementSys.HouseholdNeed(first) * SimTime.DaysPerYear * (1.0 + Retinue.KeepYears) + (men + 0.5) * Retinue.KeepYears * Retinue.ManYear(w);
            w.Settlements.Get(sid).Granary += want - hh.Grain;
            hh.Grain = want;
        }

        /// <summary>The men of a village sworn to `lord` (a person).</summary>
        static List<int> SwornTo(World w, int sid, int lord)
        {
            var l = new List<int>();
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var q = w.People.Get(res[i]); if (q.Alive && lord != 0 && q.Sworn == lord) l.Add(res[i]); }
            l.Sort();
            return l;
        }

        static List<HistoryEvent> Since(World w, long mark, HistoryKind kind)
        {
            var l = new List<HistoryEvent>();
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) if (w.History.At(i).Kind == kind) l.Add(w.History.At(i));
            return l;
        }

        [Test]
        public void AFirstHouseholdWithGrainToSpareSwearsTheWillingYoungMenOfItsVillage()
        {
            var w = Village(out int sid, out int first, 3);
            Assume.That(w != null, "a village with a first household and willing men");
            Assert.AreEqual(3, Retinue.Keepable(w, first), "premise: its grain keeps three");
            var expect = Candidates(w, sid).GetRange(0, 3);
            expect.Sort();
            var grain = new Dictionary<int, double>();
            foreach (int pid in expect) { int h = w.People.Get(pid).Household; grain[h] = w.Households.Get(h).Grain; }
            double firstBefore = w.Households.Get(first).Grain, store = w.Settlements.Get(sid).Granary;
            long mark = w.History.LastId;
            Retinue.Harvest(w, sid, 0);
            var sworn = SwornTo(w, sid, w.Settlements.Get(sid).Head);
            CollectionAssert.AreEqual(expect, sworn, "the landless and land-short first, then the boldest");
            // a year's food each, from the first household's bin into his household's - the same store
            double year = Retinue.ManYear(w), given = 0;
            foreach (int pid in sworn)
            {
                int h = w.People.Get(pid).Household;
                if (h == first) continue;
                given += year;
            }
            Assert.AreEqual(firstBefore - given, w.Households.Get(first).Grain, 1e-6, "from its bin");
            foreach (var kv in grain)
            {
                if (kv.Key == first) continue;
                int n = 0; foreach (int pid in sworn) if (w.People.Get(pid).Household == kv.Key) n++;
                Assert.AreEqual(kv.Value + n * year, w.Households.Get(kv.Key).Grain, 1e-6, "into his household's");
            }
            Assert.AreEqual(store, w.Settlements.Get(sid).Granary, 1e-9, "nothing leaves the store");
            // told: each man's swearing, the page, his own page
            var told = Since(w, mark, HistoryKind.Sworn);
            Assert.AreEqual(3, told.Count);
            foreach (var e in told) { Assert.AreEqual(w.Settlements.Get(sid).Head, e.Object.Id, "sworn to the one first among the people"); Assert.AreEqual(first, e.Other.Id, "fed by their household"); Assert.IsTrue(sworn.Contains(e.Subject.Id)); }
            Console.WriteLine(HistoryText.Line(w, told[2]));
            StringAssert.Contains("swore to " + w.History.NameOf(EntityRef.Person(w.Settlements.Get(sid).Head)), HistoryText.Line(w, told[0]));
            string page = Gazetteer.All(w, sid);
            StringAssert.Contains("3 men are sworn to " + w.History.NameOf(EntityRef.Person(w.Settlements.Get(sid).Head)) + ", fed from the", page, "the settlement page");
            string life = Biography.Work(w, sworn[0]);
            Console.WriteLine(life);
            StringAssert.Contains("sworn to " + w.History.NameOf(EntityRef.Person(w.Settlements.Get(sid).Head)), life, "his own page");
            // (session 27) their standing in a few words, for the pointer's tag: the first, his sworn men, his household; nobody else has one
            int head = w.Settlements.Get(sid).Head;
            string place = w.History.NameOf(EntityRef.Settlement(sid)), headName = w.History.NameOf(EntityRef.Person(head));
            StringAssert.StartsWith("first of " + place, Biography.Standing(w, head));
            Assert.AreEqual("sworn to " + headName + ", first of " + place, Biography.Standing(w, sworn[0]));
            foreach (int pid in w.Members(first)) if (pid != head && !sworn.Contains(pid)) { Assert.AreEqual("of the household of " + headName + ", first of " + place, Biography.Standing(w, pid)); break; }
            foreach (int pid in Candidates(w, sid)) if (!sworn.Contains(pid) && w.People.Get(pid).Household != first) { Assert.AreEqual("", Biography.Standing(w, pid), "the others: none"); break; }
            // --oldsworn: nobody is sworn
            var w2 = Village(out int sid2, out int first2, 3);
            Retinue.Off = true;
            try { Retinue.Harvest(w2, sid2, 0); }
            finally { Retinue.Off = false; }
            Assert.AreEqual(0, SwornTo(w2, sid2, w2.Settlements.Get(sid2).Head).Count, "--oldsworn: none");
        }

        [Test]
        public void ItsGrainShortTheLastSwornGoHomeAndANewFirstHouseholdReleasesTheOldOnesMen()
        {
            var w = Village(out int sid, out int first, 3);
            Assume.That(w != null, "a village with a first household and willing men");
            Retinue.Harvest(w, sid, 0);
            var three = SwornTo(w, sid, w.Settlements.Get(sid).Head);
            Assert.AreEqual(3, three.Count);
            var ranked = Candidates(w, sid);   // (the sworn are no longer candidates: rank the three as the household does)
            // a lean year: its grain keeps one - the most landed and least bold go home first
            Feed(w, sid, first, 1);
            long mark = w.History.LastId;
            Retinue.Harvest(w, sid, 0);
            var one = SwornTo(w, sid, w.Settlements.Get(sid).Head);
            Assert.AreEqual(1, one.Count, "its grain keeps one");
            var released = Since(w, mark, HistoryKind.SwornReleased);
            Assert.AreEqual(2, released.Count);
            foreach (var e in released) { Assert.AreEqual(1, e.ArgA, "the grain short"); Assert.IsTrue(three.Contains(e.Subject.Id)); Assert.IsFalse(one.Contains(e.Subject.Id)); }
            Console.WriteLine(HistoryText.Line(w, released[0]));
            StringAssert.Contains("could keep no more men", HistoryText.Line(w, released[0]));
            // another household's eldest rises to be first, with no grain to spare: the old lord's man goes home - the oath was to the person
            int oldLord = w.Settlements.Get(sid).Head, other = 0, newLord = 0;
            var hl = w.HouseholdsOf(sid);
            for (int i = 0; i < hl.Count && other == 0; i++)
            {
                if (hl[i] == first || w.Households.Get(hl[i]).Party != 0 || w.People.Get(one[0]).Household == hl[i]) continue;
                int e0 = Leaders.Eldest(w, sid, hl[i]);
                if (e0 != 0) { other = hl[i]; newLord = e0; }
            }
            Assume.That(other != 0, "another household with a grown member");
            { ref var s = ref w.Settlements.Get(sid); s.HeadHh = other; s.Head = newLord; }
            { ref var oh = ref w.Households.Get(other); w.Settlements.Get(sid).Granary -= oh.Grain; oh.Grain = 0; }
            Assert.IsFalse(Retinue.SwornHere(w, in w.People.Get(one[0])), "no longer sworn to the one first among the people");
            mark = w.History.LastId;
            Retinue.Harvest(w, sid, 0);
            Assert.AreEqual(0, SwornTo(w, sid, oldLord).Count, "another first: the old lord's men released");
            Assert.AreEqual(0, SwornTo(w, sid, newLord).Count, "the new first keeps none: no grain to spare");
            var gone = Since(w, mark, HistoryKind.SwornReleased);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual(2, gone[0].ArgA, "another first among the people");
            Console.WriteLine(HistoryText.Line(w, gone[0]));
            StringAssert.Contains("another is first among the people", HistoryText.Line(w, gone[0]));
        }

        [Test]
        public void SwornMenPractiseEveryDayAtBothResolutionsAndTheVillageGrowsMightier()
        {
            foreach (bool coarse in new[] { true, false })
            {
                var w = Village(out int sid, out int first, 3);
                Assume.That(w != null, "a village with a first household and willing men");
                if (coarse) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
                Retinue.Harvest(w, sid, 0);
                var sworn = SwornTo(w, sid, w.Settlements.Get(sid).Head);
                Assert.AreEqual(3, sworn.Count);
                Structures.Place(w, sid, Structures.Ground, out float gx, out float gy);
                Assert.AreNotEqual(0, Structures.Standing(w, sid, Structures.Ground, gx, gy));
                Assert.IsFalse(Forge.Threatened(in w.Settlements.Get(sid)), "premise: no fear - nobody else practises");
                double might0 = w.Conflict.Lasting(sid);
                var others = Candidates(w, sid);
                others.RemoveAll(sworn.Contains);   // (the willing not sworn)
                var skill0 = new Dictionary<int, float>();
                foreach (int pid in others) skill0[pid] = w.People.Get(pid).SkFight;   // (some were born with a little: the world's first people)
                Retinue.Tally = new double[Retinue.TallyN];
                var trainedOn = new Dictionary<int, HashSet<long>>();
                foreach (int pid in sworn) trainedOn[pid] = new HashSet<long>();
                // (and should the village's harvest fall in these days, its first household weighs its men again: those sworn at any hour are not "others")
                var ever = new HashSet<int>(sworn);
                string doing = null;
                try
                {
                    for (int h = 0; h < 6 * 24; h++)
                    {
                        { ref var sk = ref w.Settlements.Get(sid); sk.CropWork = Math.Max(sk.CropWork, sk.CropWorkNeed); if (sk.Granary < 20 * sk.Population) sk.Granary = 20 * sk.Population; }
                        w.AdvanceTo(w.Now + SimTime.Hour);
                        foreach (int pid in SwornTo(w, sid, w.Settlements.Get(sid).Head)) ever.Add(pid);
                        if (coarse) continue;
                        foreach (int pid in sworn)
                        {
                            var q = w.People.Get(pid);
                            if (q.Act != ActivityKind.Train) continue;
                            trainedOn[pid].Add(SimTime.FloorDiv(w.Now + w.LocalOffsetOf(sid), SimTime.Day));
                            if (doing == null) doing = HistoryText.Doing(w, in q);
                        }
                    }
                    double hours = Retinue.Tally[7];
                    Console.WriteLine((coarse ? "COARSE" : "lived") + ": the sworn practised " + hours.ToString("0.0") + " h in six days; others " + (w.Metrics.TrainHours - hours).ToString("0.0") + " h" + (doing != null ? "; " + doing : ""));
                    var still = SwornTo(w, sid, w.Settlements.Get(sid).Head);
                    Console.WriteLine("sworn at the start " + sworn.Count + ", at any hour " + ever.Count + ", at the end " + still.Count);
                    foreach (int pid in sworn) Assert.Greater(w.People.Get(pid).SkFight, 0f, "each sworn man grows skilled");
                    foreach (int pid in others) if (!ever.Contains(pid)) Assert.LessOrEqual(w.People.Get(pid).SkFight, skill0[pid], "the village does not fear its neighbours: the others do not practise");
                    Assert.AreEqual(hours, w.Metrics.TrainHours, 1e-6, "only the sworn practised");
                    // (COARSE from the first midnight: five days and a lived evening - a session a day each, as their free hours allow)
                    if (coarse) { Assert.GreaterOrEqual(hours, 3 * 4 * Training.Session, "COARSE: a session most days each"); Assert.LessOrEqual(hours, ever.Count * 6 * Training.Session + 1e-6, "never more than a session a day"); }
                    else
                    {
                        foreach (int pid in sworn) if (still.Contains(pid)) Assert.GreaterOrEqual(trainedOn[pid].Count, 4, "lived: at the ground most afternoons");
                        Assert.IsNotNull(doing);
                        StringAssert.Contains("sworn to " + w.History.NameOf(EntityRef.Person(w.Settlements.Get(sid).Head)), doing);
                    }
                    Assert.Greater(w.Conflict.Lasting(sid), might0, "their skill weighs in the village's might (C10.7a)");
                }
                finally { Retinue.Tally = null; }
                Assert.IsNull(TestWorlds.Audit(w));
            }
        }

        [Test]
        public void SwornMenGoWithEveryBandTheirVillageRaises()
        {
            for (ulong from = 1101; from < 1141; from += 8)
            {
                var w = Village(out int sid, out int first, 3, from);
                if (w == null) continue;
                Retinue.Harvest(w, sid, 0);
                var sworn = SwornTo(w, sid, w.Settlements.Get(sid).Head);
                if (sworn.Count != 3) continue;
                // a neighbour within a day's walk that wronged it heavily, none of the sworn with close kin there
                int foe = 0;
                for (int o = w.Settlements.First(); o != 0 && foe == 0; o = w.Settlements.Next(o))
                {
                    if (o == sid || !w.Settlements.Get(o).Inhabited || w.Routes.Get(w, sid, o) == null) continue;
                    if (Vec3d.ArcDistance(w.Settlements.Get(sid).Anchor, w.Settlements.Get(o).Anchor, w.Planet.Radius) > 6000) continue;
                    bool kin = false; foreach (int pid in sworn) if (w.Conflict.KinIn(in w.People.Get(pid), o)) kin = true;
                    if (!kin) foe = o;
                }
                if (foe == 0) continue;
                ref var s = ref w.Settlements.Get(sid);
                s.Foe = foe; s.FoeWorth = (float)Conflict.RaidFull;
                s.FoeEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Raid, Time = w.Now, Settlement = sid, Subject = EntityRef.Settlement(foe), Object = EntityRef.Settlement(sid) });
                Retinue.Tally = new double[Retinue.TallyN];
                int band;
                try { band = w.Conflict.RaiseBandNow(sid); }
                finally { Retinue.Tally = null; }
                if (band == 0) continue;
                int inBand = 0;
                foreach (int pid in sworn) if (w.People.Get(pid).Band == band || !w.People.Get(pid).Alive) inBand++;
                Console.WriteLine("band of " + w.Bands.Get(band).Size + " against " + w.History.NameOf(EntityRef.Settlement(foe)) + ", the three sworn men in it: " + inBand);
                Assert.AreEqual(3, inBand, "every sworn man goes with his village's band");
                return;
            }
            Assert.Fail("no premise found: a village with three sworn men and a foe a walk away that its people dare");
        }

        [Test]
        public void ASaveWithSwornMenContinuesIdentically()
        {
            var w = Village(out int sid, out int first, 3);
            Assume.That(w != null, "a village with a first household and willing men");
            Retinue.Harvest(w, sid, 0);
            Assert.AreEqual(3, SwornTo(w, sid, w.Settlements.Get(sid).Head).Count);
            Structures.Place(w, sid, Structures.Ground, out float gx, out float gy);
            Structures.Standing(w, sid, Structures.Ground, gx, gy);
            w.AdvanceTo(w.Now + SimTime.Days(1) + SimTime.Hours(15));   // (the afternoon: the sworn at the ground)
            var copy = SaveFile.Load(SaveFile.Save(w, 0, OfflineMode.Paused, 0, 0, ""), out _);
            CollectionAssert.AreEqual(SwornTo(w, sid, w.Settlements.Get(sid).Head), SwornTo(copy, sid, copy.Settlements.Get(sid).Head), "saved and loaded");
            long t = w.Now + SimTime.Days(3);
            w.AdvanceTo(t); copy.AdvanceTo(t);
            Assert.AreEqual(w.StateHash(), copy.StateHash(), "the loaded world lives on as the running one");
        }
    }
}
