using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Persistence;
using TC2.Core.Places;
using TC2.Core.Planet;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Villages live off the land (D2-042): the wild's food comes out of the same fields the ecology steps, bodies hold
    /// reserves, famine is remembered. Mechanisms, not outcomes: no test asks that a village survive.
    /// </summary>
    [TestFixture]
    public class WildFoodTests
    {
        static double GameKgIn(World w, int c)
        {
            var P = w.Planet; double v = 0;
            for (int q = 0; q < P.Species.Count; q++) if (WildFood.IsGame(P.Species[q])) v += P.Abund[q * P.Cells + c] * P.Species[q].Mass;
            return v;
        }

        static List<int> Within(World w, int sid, double reach)
        {
            var list = new List<int>();
            w.Hazards.CellsWithin(w, w.Settlements.Get(sid).Cell, reach, list);
            return new List<int>(list);
        }

        static double GameNear(World w, int sid) { double v = 0; foreach (int c in Within(w, sid, WildFood.HuntReach)) v += GameKgIn(w, c); return v; }

        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        static int BestVillage(World w, Func<Settlement, double> score)
        {
            int best = 0; double bs = -1;
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var s = w.Settlements.Get(sid);
                if (!s.Inhabited) continue;
                double v = score(s);
                if (v > bs) { bs = v; best = sid; }
            }
            return best;
        }

        /// <summary>Premise: a barren land round a village - no game or fish within reach (and a margin), no woods to gather in.</summary>
        static void Strip(World w, int sid, bool woods)
        {
            var P = w.Planet; int n = P.Cells;
            foreach (int c in Within(w, sid, WildFood.HuntReach + 600))
                for (int q = 0; q < P.Species.Count; q++)
                {
                    var sp = P.Species[q];
                    if (WildFood.IsGame(sp) || WildFood.IsFish(sp)) P.Abund[q * n + c] = 0f;
                }
            if (woods) foreach (int c in Within(w, sid, WildFood.GatherReach + 600)) if (P.IsLand(c)) P.Forest[c] = 0f;
        }

        static void EmptyStores(World w, int sid)
        {
            ref var s = ref w.Settlements.Get(sid);
            s.Granary = 0;
            var hh = w.HouseholdsOf(sid);
            for (int i = 0; i < hh.Count; i++) w.Households.Get(hh[i]).Larder = 0;
        }

        /// <summary>Hunger deaths at a village since an event id: (days after t0, age at death).</summary>
        static List<(double day, double age)> HungerDeaths(World w, int sid, long afterId, long t0)
        {
            var list = new List<(double, double)>();
            for (int i = w.History.FirstIndexAfter(afterId); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.PersonDied && e.ArgA == 2 && e.Settlement == sid) list.Add(((e.Time - t0) / (double)SimTime.Day, e.Magnitude));
            }
            return list;
        }

        [Test]
        public void HuntersTakeWholeAnimalsOnlyFromTheirGround()
        {
            var w = TestWorlds.Make(301);
            var P = w.Planet; int n = P.Cells;
            int sid = BestVillage(w, s => s.Hunt.Expect);
            int ground = w.Settlements.Get(sid).Hunt.A;
            Assert.GreaterOrEqual(ground, 0, "a village has hunting grounds");
            var before = (float[])P.Abund.Clone();
            double fu = 0;
            for (int i = 0; i < 60; i++) fu += w.Wild.Hunt(ground, 4.0, 0.5, (ulong)(1000 + i), out _);
            Assert.Greater(fu, 0.0, "sixty days of hunting bring meat home");
            var g = P.Grid;
            var groundCells = new HashSet<int> { ground };
            for (int j = 0; j < 8; j++) groundCells.Add(g.Nbr8[ground * 8 + j]);
            double kg = 0;
            for (int q = 0; q < P.Species.Count; q++)
                for (int c = 0; c < n; c++)
                {
                    double d = before[q * n + c] - P.Abund[q * n + c];
                    if (d == 0) continue;
                    Assert.Greater(d, 0.0, "nothing is ever added");
                    Assert.IsTrue(WildFood.IsGame(P.Species[q]), "only game is taken (" + Ecology.KindName(P.Species[q].Kind) + ")");
                    Assert.IsTrue(groundCells.Contains(c), "only on the ground they hunted");
                    // whole animals, unless a cell's last fraction of one went (the field holds fractions)
                    if (P.Abund[q * n + c] > 1e-6f) Assert.AreEqual(Math.Round(d), d, 1e-3, "whole animals");
                    kg += d * P.Species[q].Mass;
                }
            Assert.AreEqual(kg * WildFood.MeatShare * WildFood.FuPerKg, fu, 1e-3 * fu, "the meat is exactly the edible share of the animals taken");
        }

        [Test]
        public void FishersTakeFromTheShoalsOffTheirShore()
        {
            World w = null; int sid = 0;
            for (ulong seed = 302; seed < 312 && sid == 0; seed++)
            {
                w = TestWorlds.Make(seed);
                sid = BestVillage(w, s => s.Fish.Expect);
                if (sid != 0 && w.Settlements.Get(sid).Fish.Expect <= 0f) sid = 0;
            }
            Assert.AreNotEqual(0, sid, "some village lies by the sea");
            var P = w.Planet; int n = P.Cells;
            int ground = w.Settlements.Get(sid).Fish.A;
            Assert.IsFalse(P.IsLand(ground), "a fishing ground is water");
            var before = (float[])P.Abund.Clone();
            double fu = 0;
            for (int i = 0; i < 40; i++) fu += w.Wild.Fish(ground, 4.0, 0.5, (ulong)(2000 + i));
            Assert.Greater(fu, 0.0);
            double kg = 0;
            for (int q = 0; q < P.Species.Count; q++)
                for (int c = 0; c < n; c++)
                {
                    double d = before[q * n + c] - P.Abund[q * n + c];
                    if (d == 0) continue;
                    Assert.Greater(d, 0.0);
                    Assert.IsTrue(WildFood.IsFish(P.Species[q]), "only fish are taken");
                    Assert.IsFalse(P.IsLand(c), "from the water");
                    kg += d * P.Species[q].Mass;
                }
            Assert.AreEqual(kg * WildFood.FishShare * WildFood.FuPerKg, fu, 2e-3 * fu, "the food is the edible share of the fish taken");
        }

        [Test]
        public void GatherersLeaveLessMastForTheBoar()
        {
            var a = TestWorlds.Make(303); var b = TestWorlds.Make(303);
            int sid = BestVillage(a, s => s.Gath.Expect);
            int ground = a.Settlements.Get(sid).Gath.A;
            Assert.GreaterOrEqual(ground, 0);
            var P = a.Planet; int n = P.Cells;
            var cells = new List<int> { ground }; for (int j = 0; j < 8; j++) if (P.IsLand(P.Grid.Nbr8[ground * 8 + j])) cells.Add(P.Grid.Nbr8[ground * 8 + j]);
            double Left() { double v = 0; foreach (int c in cells) v += WildFood.MastLeft(P, c); return v; }
            double left0 = Left();
            // a hungry village's gatherers on one ground until it is picked over (a premise: many hands, many hours)
            double fu = 0;
            for (int i = 0; i < 400 && Left() > 0.3 * left0; i++) fu += a.Wild.Gather(ground, 40.0, 1.0, (ulong)(3000 + i));
            Assert.Greater(fu, 0.0);
            Assert.Less(Left(), 0.6 * left0, "what was gathered is gone from the woods");
            Assert.AreEqual(fu / WildFood.FuPerKgMast, (left0 - Left()), 1e-3 * left0, "the food is exactly what left the woods");
            Assert.Greater(P.Gathered[ground], 0f);
            // the next step of the animals: the boar of that woods find less mast than in an untouched copy of the world
            int boar = -1; for (int q = 0; q < P.Species.Count; q++) if (P.Species[q].Kind == 3) boar = q;
            Ecology.Step(a, 0.25, a.Now); Ecology.Step(b, 0.25, b.Now);
            double ca = 0, cb = 0, wa = 0, wb = 0;
            foreach (int c in cells) { double x = a.Planet.Abund[boar * n + c], y = b.Planet.Abund[boar * n + c]; ca += x * a.Planet.Cond[boar * n + c]; wa += x; cb += y * b.Planet.Cond[boar * n + c]; wb += y; }
            Assert.Greater(wa, 0.5, "boar live in these woods");
            Assert.Less(ca / wa, cb / wb, "where people gathered, the boar ate worse");
            Assert.AreEqual(0f, a.Planet.Gathered[ground], "the tally is the animals' step's to settle");
        }

        [Test]
        public void HuntingHardThinsTheGameNearAVillageAndItComesBackWhenItStops()
        {
            var a = TestWorlds.Make(304); var b = TestWorlds.Make(304);
            int sid = BestVillage(a, s => s.Hunt.Expect);
            Coarse(a); Coarse(b);
            double g0 = GameNear(a, sid);
            // two years of a hungry, crowded village hunting hard on its best grounds (a premise: enough hunter-hours a day to
            // take about three in a hundred of the animals within reach each day, were they all to be found)
            double hoursADay = 0.03 * g0 / (WildFood.HuntRate * 1.1);
            for (int d = 0; d < 2 * SimTime.DaysPerYear; d++)
            {
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
                var s = a.Settlements.Get(sid);
                foreach (int g in new[] { s.Hunt.A, s.Hunt.B, s.Hunt.C })
                    if (g >= 0) for (int k = 0; k < 8; k++) a.Wild.Hunt(g, hoursADay / 24.0, 0.6, KeyedRng.Hash(77, RngStream.Test, (ulong)d, (ulong)(g * 8 + k)), out _);
            }
            double ha = GameNear(a, sid), hb = GameNear(b, sid);
            Console.WriteLine("game within reach: start " + g0.ToString("0") + " kg, hunted " + ha.ToString("0") + ", control " + hb.ToString("0"));
            Assert.Less(ha, 0.75 * hb, "hard hunting thins the game within reach");
            // then the village stops: the land fills again from the ranges round it and from births
            a.AdvanceTo(a.Now + SimTime.Years(8)); b.AdvanceTo(b.Now + SimTime.Years(8));
            double ra = GameNear(a, sid), rb = GameNear(b, sid);
            Console.WriteLine("eight years later: " + ra.ToString("0") + " kg against " + rb.ToString("0"));
            Assert.Greater(ra / rb, ha / hb + 0.1, "the game comes back once the hunting stops");
        }

        [Test]
        public void ABodyStarvesOnItsReserveOverWeeksTheYoungFirst()
        {
            var w = TestWorlds.Make(305);
            int sid = w.Settlements.First();
            Coarse(w);
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hour);
            long t0 = w.Now; long mark = w.History.LastId;
            Strip(w, sid, woods: true);
            int adults0 = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { double age = w.People.Get(res[i]).AgeYears(w.Now); if (age >= 16 && age < 60) adults0++; }
            // nothing to eat at all: stores emptied every day, the crop lost, no game, fish or woods within reach
            for (int d = 0; d < 70; d++)
            {
                EmptyStores(w, sid);
                ref var s = ref w.Settlements.Get(sid); s.CropBurnt = 1.0;
                Strip(w, sid, woods: false);
                w.AdvanceTo(w.Now + SimTime.Day);
            }
            var deaths = HungerDeaths(w, sid, mark, t0);
            Assert.Greater(deaths.Count, 0, "people starve");
            var kids = new List<double>(); var grown = new List<double>();
            foreach (var (day, age) in deaths)
            {
                Assert.GreaterOrEqual(day, 9.0, "no one starves within the first days: the body holds reserves");
                if (age >= 16 && age < 60) { Assert.GreaterOrEqual(day, 28.0, "a grown body lasts weeks"); grown.Add(day); }
                else if (age >= 2 && age < 12) kids.Add(day);
            }
            kids.Sort(); grown.Sort();
            Assert.Greater(kids.Count, 0); Assert.Greater(grown.Count, 0);
            Assert.Less(kids[kids.Count / 2], grown[grown.Count / 2], "children starve before grown people");
            Assert.GreaterOrEqual(grown.Count, (int)(0.8 * adults0), "with nothing at all, most grown people are dead within ten weeks");
            // every hunger death is caused by the village's famine, rooted where the hunger came from
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.PersonDied || e.ArgA != 2 || e.Settlement != sid) continue;
                Assert.IsTrue(w.History.TryGet(e.Cause1, out var famine) && famine.Kind == HistoryKind.Famine, "a hunger death is caused by the famine");
            }
        }

        [Test]
        public void TheWildCarriesAVillageThroughAFailedHarvestThatABarrenLandDoesNot()
        {
            var a = TestWorlds.Make(306); var b = TestWorlds.Make(306);
            int sid = BestVillage(a, s => s.Hunt.Expect + s.Fish.Expect + s.Gath.Expect);
            Coarse(a); Coarse(b);
            a.AdvanceTo(SimTime.StartOfDay(a.Now) + SimTime.Day + SimTime.Hour); b.AdvanceTo(a.Now);
            long t0 = a.Now; long markA = a.History.LastId, markB = b.History.LastId;
            // both: the year's crop is lost and the granary is bare; b: nothing in the wild within reach either
            foreach (var w in new[] { a, b })
            {
                ref var s = ref w.Settlements.Get(sid);
                if (s.Crop == CropStage.Harvested || s.Crop == CropStage.Fallow) { s.Crop = CropStage.Growing; s.CropWorkNeed = s.FieldHa * 8; }
                s.CropBurnt = 1.0;
                EmptyStores(w, sid);
            }
            Strip(b, sid, woods: true);
            int pop0 = a.Settlements.Get(sid).Population;
            // until the next harvest: seventy days
            for (int d = 0; d < 70; d++)
            {
                Strip(b, sid, woods: false);
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
                ref var sa = ref a.Settlements.Get(sid); ref var sb = ref b.Settlements.Get(sid);
                if (d < 30) { sa.CropBurnt = 1.0; sb.CropBurnt = 1.0; }   // (only the year that failed)
            }
            int da = HungerDeaths(a, sid, markA, t0).Count, db = HungerDeaths(b, sid, markB, t0).Count;
            Console.WriteLine("village of " + pop0 + ": hunger deaths with the wild " + da + ", in a barren land " + db + "; wild FU " + a.Metrics.WildFu.ToString("0"));
            Assert.Greater(a.Metrics.WildFu, 0.0, "they lived off the wild");
            Assert.Greater(db, pop0 / 3, "a barren land and a failed harvest kill many");
            Assert.Less(da, 0.5 * db, "the woods, the game and the shore carry most of them through");
            // the history tells the response: they turned to the wild in response to the shortage
            bool told = false;
            for (int i = a.History.FirstIndexAfter(markA); i < a.History.Count; i++) { var e = a.History.At(i); if (e.Kind == HistoryKind.WildFood && e.Settlement == sid) { told = true; Assert.AreNotEqual(0L, e.Cause1, "in response to something"); } }
            Assert.IsTrue(told, "the history records that the village turned to the wild");
        }

        [Test]
        public void FamineIsRememberedByThoseWhoLivedItAndTheyKeepMore()
        {
            var a = TestWorlds.Make(307); var b = TestWorlds.Make(307);
            int sid = a.Settlements.First();
            Coarse(a); Coarse(b);
            a.AdvanceTo(SimTime.StartOfDay(a.Now) + SimTime.Day + SimTime.Hour); b.AdvanceTo(a.Now);
            // a: a famine has just struck (a premise); b: never
            a.Settlements.Get(sid).FamineAt = a.Now;
            // both hold the same stores: enough to the harvest, not much beyond
            foreach (var w in new[] { a, b }) { ref var s = ref w.Settlements.Get(sid); s.Granary = s.Population * 30.0; }
            a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
            var sa = a.Settlements.Get(sid); var sb = b.Settlements.Get(sid);
            Assert.Greater(sa.Memory, 0.9, "everyone grown lived through the famine");
            Assert.AreEqual(0.0, sb.Memory);
            Assert.Less(sa.Ration, sb.Ration - 0.01, "remembered hunger makes them keep more back");
            // a village that remembers sows more next spring (as far as its land allows)
            var a2 = TestWorlds.Make(307); var b2 = TestWorlds.Make(307);
            Coarse(a2); Coarse(b2);
            a2.Settlements.Get(sid).FamineAt = a2.Now;
            a2.AdvanceTo(a2.Now + SimTime.Years(1)); b2.AdvanceTo(b2.Now + SimTime.Years(1));
            var x = a2.Settlements.Get(sid); var y = b2.Settlements.Get(sid);
            Assert.Greater(x.FieldHa / Math.Max(1, x.Population), y.FieldHa / Math.Max(1, y.Population) * 1.05, "more fields for each mouth where famine is remembered");
            // after a lifetime the memory is held by the old alone: it fades as those who lived it die
            a2.AdvanceTo(a2.Now + SimTime.Years(45));
            var z = a2.Settlements.Get(sid);
            if (z.Inhabited) Assert.Less(z.Memory, 0.35, "forty-six years on, few remember");
        }

        [Test]
        public void SaveLoadInTheMiddleOfAHuntContinuesIdentically()
        {
            var w = TestWorlds.Make(309);
            int hunter = 0;
            for (int h = 0; h < 24 * 30 && hunter == 0; h++)
            {
                w.AdvanceTo(w.Now + SimTime.Hour);
                for (int id = w.People.First(); id != 0; id = w.People.Next(id))
                {
                    var p = w.People.Get(id);
                    if (p.Alive && (p.Act == ActivityKind.Hunt || p.Act == ActivityKind.Fish) && p.T1 > w.Now + SimTime.Hour) { hunter = id; break; }
                }
            }
            Assert.AreNotEqual(0, hunter, "someone goes hunting or fishing within a month");
            var bytes = SaveFile.Save(w, 0, OfflineMode.Living);
            var w2 = SaveFile.Load(bytes, out _);
            long end = w.Now + SimTime.Days(3);
            w.AdvanceTo(end); w2.AdvanceTo(end);
            Assert.AreEqual(w.StateHash(), w2.StateHash(), "a world saved mid-hunt continues as if never saved");
            Assert.IsNull(TestWorlds.Audit(w2));
        }

        [Test]
        public void CoarseAndActivityLiveOffTheWildAlike()
        {
            var a = TestWorlds.Make(308); var b = TestWorlds.Make(308);
            Coarse(b);
            double adultDaysA = 0, adultDaysB = 0;
            long end = a.Now + SimTime.Years(4);
            while (a.Now < end)
            {
                a.AdvanceTo(a.Now + SimTime.Days(4)); b.AdvanceTo(a.Now);
                adultDaysA += 4 * Adults(a); adultDaysB += 4 * Adults(b);
            }
            double ha = a.Metrics.WildHours / adultDaysA, hb = b.Metrics.WildHours / adultDaysB;
            double fa = a.Metrics.WildFu / adultDaysA, fb = b.Metrics.WildFu / adultDaysB;
            Console.WriteLine("wild hours per adult-day: activity " + ha.ToString("0.000") + " coarse " + hb.ToString("0.000") +
                                  "; FU per adult-day " + fa.ToString("0.000") + " / " + fb.ToString("0.000") +
                                  "; trips hunt/fish/gather " + a.Metrics.HuntTrips + "/" + a.Metrics.FishTrips + "/" + a.Metrics.GatherTrips + " vs " + b.Metrics.HuntTrips + "/" + b.Metrics.FishTrips + "/" + b.Metrics.GatherTrips);
            Assert.Greater(ha, 0.0); Assert.Greater(hb, 0.0);
            Assert.That(hb / ha, Is.InRange(0.6, 1.6), "the same hours go to the wild at both resolutions");
            Assert.That(fb / fa, Is.InRange(0.6, 1.6), "and the same food comes home");
        }

        [Test]
        public void CoarseAndActivityTurnToTheWildAlikeInAShortage()
        {
            // one village alone in its world, its harvest lost and its stores bare: how many hours go to the wild in 30 days
            WorldConfig One() => new WorldConfig { Seed = 310, SurfaceN = 48, Settlements = 1, FoundersPerSettlement = 30 };
            var a = WorldGenerator.Create(One()); var b = WorldGenerator.Create(One());
            int sid = BestVillage(a, s => s.Hunt.Expect + s.Fish.Expect + s.Gath.Expect);
            Coarse(b);
            b.AdvanceTo(SimTime.StartOfDay(b.Now) + SimTime.Day + SimTime.Hour); a.AdvanceTo(b.Now);
            foreach (var w in new[] { a, b }) { ref var s = ref w.Settlements.Get(sid); s.CropBurnt = 1.0; EmptyStores(w, sid); }
            double h0a = a.Metrics.WildHours, h0b = b.Metrics.WildHours, f0a = a.Metrics.WildFu, f0b = b.Metrics.WildFu;
            double adA = 0, adB = 0;
            for (int d = 0; d < 30; d++)
            {
                foreach (var w in new[] { a, b }) { ref var s = ref w.Settlements.Get(sid); s.Granary = DMath.Min(s.Granary, 0.0); }
                a.AdvanceTo(a.Now + SimTime.Day); b.AdvanceTo(b.Now + SimTime.Day);
                adA += Adults(a, sid); adB += Adults(b, sid);
            }
            double ha = (a.Metrics.WildHours - h0a) / adA, hb = (b.Metrics.WildHours - h0b) / adB;
            double fa = (a.Metrics.WildFu - f0a) / adA, fb = (b.Metrics.WildFu - f0b) / adB;
            Console.WriteLine("in a shortage, wild hours per adult-day: activity " + ha.ToString("0.00") + " coarse " + hb.ToString("0.00") + "; FU " + fa.ToString("0.00") + " / " + fb.ToString("0.00"));
            Assert.That(hb / ha, Is.InRange(0.6, 1.6), "a shortage sends people to the wild alike at both resolutions");
            Assert.That(fb / fa, Is.InRange(0.6, 1.6));
        }

        static int Adults(World w, int sid)
        {
            int n = 0; var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++) { var p = w.People.Get(res[i]); if (p.Alive && p.AgeYears(w.Now) >= 16) n++; }
            return n;
        }

        static int Adults(World w)
        {
            int n = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); if (p.Alive && p.AgeYears(w.Now) >= 16) n++; }
            return n;
        }
    }
}
